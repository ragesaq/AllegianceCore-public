using System.Runtime.InteropServices;
using Allegiance.ClientCore.MonoGame.Vr;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.KHR;
using XrSession = Silk.NET.OpenXR.Session;

namespace Allegiance.ClientCore.Vr;

/// <summary>Raised for any reason VR cannot start or continue. The presenter
/// turns it into <see cref="OpenXrQuadPresenter.Failed"/>; the game runs flat.</summary>
public sealed class VrUnavailableException(string message) : Exception(message);

/// <summary>Milestone 1 of the VR client: the ordinary flat frame, shown in the
/// headset on one OpenXR quad layer (a virtual screen). OpenXR starts lazily on
/// the first frame, on the render thread, with MonoGame's OpenGL context current.
/// Windows only (XR_KHR_opengl_enable with a WGL binding).</summary>
public sealed unsafe class OpenXrQuadPresenter : IVrFrameSink
{
    private const string OpenGlExtension = "XR_KHR_opengl_enable";
    private const long GlSrgb8Alpha8 = 0x8C43;
    private const long GlRgba8 = 0x8058;
    private const int IdleSleepMilliseconds = 11;

    private readonly IReadOnlyList<string> _loaderCandidates;
    private readonly Action<string> _log;
    private bool _initialized;
    private bool _disposed;

    private XR? _xr;
    private Instance _instance;
    private ulong _systemId;
    private XrSession _session;
    private Space _space;
    private bool _sessionRunning;
    private bool _firstFrameLogged;
    private nint _hdc;
    private nint _hglrc;
    private GlBlitter? _blitter;

    private Swapchain _swapchain;
    private uint[] _swapchainImages = [];
    private int _swapchainWidth;
    private int _swapchainHeight;
    private long _swapchainFormat;

    public OpenXrQuadPresenter(VrOptions options) : this(options.LoaderCandidates(), Console.WriteLine) { }

    internal OpenXrQuadPresenter(IReadOnlyList<string> loaderCandidates, Action<string> log)
    {
        _loaderCandidates = loaderCandidates;
        _log = log;
    }

    public bool Failed { get; private set; }

    public string? FailureReason { get; private set; }

    public void SubmitFrame(int backBufferWidth, int backBufferHeight)
    {
        if (Failed || _disposed) return;
        try
        {
            if (!_initialized)
            {
                _initialized = true;
                Initialize();
            }
            PollEvents();
            if (Failed) return;
            if (!_sessionRunning)
            {
                // No frame loop while the headset is idle; keep the uncapped game loop cool.
                Thread.Sleep(IdleSleepMilliseconds);
                return;
            }
            EnsureSwapchain(backBufferWidth, backBufferHeight);
            SubmitQuadFrame(backBufferWidth, backBufferHeight);
        }
        catch (Exception ex) when (ex is VrUnavailableException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Fail(ex.Message);
        }
    }

    private void Initialize()
    {
        var xr = LoadApi();
        _xr = xr;
        if (!OperatingSystem.IsWindows()) throw new VrUnavailableException("VR milestone 1 supports Windows only");
        _hglrc = Wgl.GetCurrentContext();
        _hdc = Wgl.GetCurrentDC();
        if (_hglrc == 0 || _hdc == 0) throw new VrUnavailableException("no OpenGL context is current on the render thread");

        if (!InstanceExtensions(xr).Contains(OpenGlExtension))
            throw new VrUnavailableException($"the OpenXR runtime does not offer {OpenGlExtension}");
        CreateInstance(xr);
        LogRuntime(xr);

        var systemInfo = new SystemGetInfo { Type = StructureType.SystemGetInfo, FormFactor = FormFactor.HeadMountedDisplay };
        ulong systemId;
        Result systemResult = xr.GetSystem(_instance, &systemInfo, &systemId);
        if (systemResult == Result.ErrorFormFactorUnavailable) throw new VrUnavailableException("no headset is connected (XR_ERROR_FORM_FACTOR_UNAVAILABLE)");
        Check(systemResult, "xrGetSystem");
        _systemId = systemId;
        LogSystem(xr);

        if (!xr.TryGetInstanceExtension<KhrOpenglEnable>(null, _instance, out var gl))
            throw new VrUnavailableException($"{OpenGlExtension} functions could not be loaded");
        var requirements = new GraphicsRequirementsOpenGLKHR { Type = StructureType.GraphicsRequirementsOpenglKhr };
        Check(gl.GetOpenGlgraphicsRequirements(_instance, _systemId, &requirements), "xrGetOpenGLGraphicsRequirementsKHR");
        _log($"[vr] runtime needs OpenGL {Version(requirements.MinApiVersionSupported)} or newer");

        var binding = new GraphicsBindingOpenGLWin32KHR
        {
            Type = StructureType.GraphicsBindingOpenglWin32Khr,
            HDC = _hdc,
            HGlrc = _hglrc,
        };
        var sessionInfo = new SessionCreateInfo { Type = StructureType.SessionCreateInfo, Next = &binding, SystemId = _systemId };
        XrSession session;
        Check(xr.CreateSession(_instance, &sessionInfo, &session), "xrCreateSession");
        _session = session;
        KeepContextCurrent();

        var spaceInfo = new ReferenceSpaceCreateInfo
        {
            Type = StructureType.ReferenceSpaceCreateInfo,
            ReferenceSpaceType = ReferenceSpaceType.Local,
            PoseInReferenceSpace = new Posef { Orientation = new Quaternionf(0, 0, 0, 1), Position = new Vector3f(0, 0, 0) },
        };
        Space space;
        Check(xr.CreateReferenceSpace(_session, &spaceInfo, &space), "xrCreateReferenceSpace");
        _space = space;
        KeepContextCurrent();
        _blitter = GlBlitter.Create(_log);
        _log("[vr] OpenXR session created; waiting for the headset to be ready");
    }

    private XR LoadApi()
    {
        foreach (string candidate in _loaderCandidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var handle)) continue;
            if (!NativeLibrary.TryGetExport(handle, "xrGetInstanceProcAddr", out _))
            {
                NativeLibrary.Free(handle);
                continue;
            }
            _log($"[vr] OpenXR loader: {candidate}");
            // The loader stays loaded for the life of the process: the runtime it
            // brought in may still hold threads after the instance is destroyed.
            return new XR(new LamdaNativeContext(name => NativeLibrary.TryGetExport(handle, name, out var address) ? address : 0));
        }
        throw new VrUnavailableException(
            $"no OpenXR loader found (tried {string.Join(", ", _loaderCandidates)}); set {VrOptions.LoaderVariable} to openxr_loader.dll");
    }

    private static HashSet<string> InstanceExtensions(XR xr)
    {
        uint count;
        Check(xr.EnumerateInstanceExtensionProperties((byte*)null, 0, &count, null), "xrEnumerateInstanceExtensionProperties");
        var properties = new ExtensionProperties[count];
        for (int i = 0; i < properties.Length; i++) properties[i].Type = StructureType.ExtensionProperties;
        var names = new HashSet<string>(StringComparer.Ordinal);
        fixed (ExtensionProperties* p = properties)
        {
            Check(xr.EnumerateInstanceExtensionProperties((byte*)null, count, &count, p), "xrEnumerateInstanceExtensionProperties");
            for (int i = 0; i < count; i++) names.Add(Marshal.PtrToStringUTF8((nint)p[i].ExtensionName) ?? "");
        }
        return names;
    }

    private void CreateInstance(XR xr)
    {
        var info = new InstanceCreateInfo { Type = StructureType.InstanceCreateInfo };
        CopyName("AllegianceCore", info.ApplicationInfo.ApplicationName, 128);
        CopyName("MonoGame", info.ApplicationInfo.EngineName, 128);
        info.ApplicationInfo.ApplicationVersion = 1;
        info.ApplicationInfo.EngineVersion = 1;
        info.ApplicationInfo.ApiVersion = 1UL << 48; // XR_MAKE_VERSION(1, 0, 0)
        byte[] extension = System.Text.Encoding.ASCII.GetBytes(OpenGlExtension + "\0");
        fixed (byte* name = extension)
        {
            byte* names = name;
            info.EnabledExtensionCount = 1;
            info.EnabledExtensionNames = &names;
            Instance instance;
            Check(xr.CreateInstance(&info, &instance), "xrCreateInstance");
            _instance = instance;
        }
    }

    private void LogRuntime(XR xr)
    {
        var properties = new InstanceProperties { Type = StructureType.InstanceProperties };
        if (xr.GetInstanceProperties(_instance, &properties) < 0) return;
        _log($"[vr] OpenXR runtime: {Marshal.PtrToStringUTF8((nint)properties.RuntimeName)} {Version(properties.RuntimeVersion)}");
    }

    private void LogSystem(XR xr)
    {
        var properties = new SystemProperties { Type = StructureType.SystemProperties };
        if (xr.GetSystemProperties(_instance, _systemId, &properties) < 0) return;
        _log($"[vr] headset: {Marshal.PtrToStringUTF8((nint)properties.SystemName)}");
    }

    private void PollEvents()
    {
        var xr = _xr!;
        while (!Failed)
        {
            var buffer = new EventDataBuffer { Type = StructureType.EventDataBuffer };
            Result result = xr.PollEvent(_instance, &buffer);
            if (result == Result.EventUnavailable) return;
            Check(result, "xrPollEvent");
            if (buffer.Type == StructureType.EventDataInstanceLossPending)
            {
                Fail("the OpenXR instance is being lost");
                return;
            }
            if (buffer.Type != StructureType.EventDataSessionStateChanged) continue;
            var changed = (EventDataSessionStateChanged*)&buffer;
            _log($"[vr] session state: {changed->State}");
            switch (VrSessionLifecycle.OnStateChanged(changed->State))
            {
                case VrSessionAction.Begin:
                    var begin = new SessionBeginInfo { Type = StructureType.SessionBeginInfo, PrimaryViewConfigurationType = ViewConfigurationType.PrimaryStereo };
                    Check(xr.BeginSession(_session, &begin), "xrBeginSession");
                    _sessionRunning = true;
                    break;
                case VrSessionAction.End:
                    _sessionRunning = false;
                    Check(xr.EndSession(_session), "xrEndSession");
                    break;
                case VrSessionAction.TearDown:
                    Fail($"the OpenXR session ended ({changed->State})");
                    return;
            }
        }
    }

    private void EnsureSwapchain(int width, int height)
    {
        if (_swapchain.Handle != 0 && width == _swapchainWidth && height == _swapchainHeight) return;
        var xr = _xr!;
        DestroySwapchain();
        if (_swapchainFormat == 0) _swapchainFormat = PickFormat(xr);
        var info = new SwapchainCreateInfo
        {
            Type = StructureType.SwapchainCreateInfo,
            UsageFlags = SwapchainUsageFlags.ColorAttachmentBit | SwapchainUsageFlags.TransferDstBit,
            Format = _swapchainFormat,
            SampleCount = 1,
            Width = (uint)width,
            Height = (uint)height,
            FaceCount = 1,
            ArraySize = 1,
            MipCount = 1,
        };
        Swapchain swapchain;
        Check(xr.CreateSwapchain(_session, &info, &swapchain), "xrCreateSwapchain");
        _swapchain = swapchain;
        _swapchainWidth = width;
        _swapchainHeight = height;
        uint count;
        Check(xr.EnumerateSwapchainImages(_swapchain, 0, &count, null), "xrEnumerateSwapchainImages");
        var images = new SwapchainImageOpenGLKHR[count];
        for (int i = 0; i < images.Length; i++) images[i].Type = StructureType.SwapchainImageOpenglKhr;
        fixed (SwapchainImageOpenGLKHR* p = images)
            Check(xr.EnumerateSwapchainImages(_swapchain, count, &count, (SwapchainImageBaseHeader*)p), "xrEnumerateSwapchainImages");
        _swapchainImages = images.Select(i => i.Image).ToArray();
        var (w, h) = VirtualScreen.SizeMetres(width, height);
        _log($"[vr] swapchain {width}x{height}, GL format 0x{_swapchainFormat:X4}, {count} images; screen {w:0.00} m x {h:0.00} m at {VirtualScreen.DistanceMetres:0.0} m");
    }

    private long PickFormat(XR xr)
    {
        uint count;
        Check(xr.EnumerateSwapchainFormats(_session, 0, &count, null), "xrEnumerateSwapchainFormats");
        var formats = new long[count];
        fixed (long* p = formats) Check(xr.EnumerateSwapchainFormats(_session, count, &count, p), "xrEnumerateSwapchainFormats");
        // The back buffer holds display-ready (sRGB-encoded) bytes and the copy does
        // no conversion, so an sRGB swapchain shows them as the window does.
        if (formats.Contains(GlSrgb8Alpha8)) return GlSrgb8Alpha8;
        if (formats.Contains(GlRgba8))
        {
            _log("[vr] runtime offers no GL_SRGB8_ALPHA8 swapchain; using GL_RGBA8 (colours may look washed out)");
            return GlRgba8;
        }
        throw new VrUnavailableException("the runtime offers neither GL_SRGB8_ALPHA8 nor GL_RGBA8 swapchains");
    }

    private void SubmitQuadFrame(int width, int height)
    {
        var xr = _xr!;
        var waitInfo = new FrameWaitInfo { Type = StructureType.FrameWaitInfo };
        var state = new FrameState { Type = StructureType.FrameState };
        // The runtime can leave the running state between the event poll and the
        // frame calls; its STOPPING event follows, so this frame is just skipped,
        // at the idle rate because nothing else paces the loop.
        Result waited = xr.WaitFrame(_session, &waitInfo, &state);
        if (waited == Result.ErrorSessionNotRunning) { SkipFrame(); return; }
        Check(waited, "xrWaitFrame");
        if (!_firstFrameLogged)
        {
            _firstFrameLogged = true;
            _log($"[vr] first frame: display period {state.PredictedDisplayPeriod / 1e6:0.00} ms");
        }
        var beginInfo = new FrameBeginInfo { Type = StructureType.FrameBeginInfo };
        Result begun = xr.BeginFrame(_session, &beginInfo);
        if (begun == Result.ErrorSessionNotRunning) { SkipFrame(); return; }
        Check(begun, "xrBeginFrame");

        var quad = new CompositionLayerQuad { Type = StructureType.CompositionLayerQuad };
        var layer = (CompositionLayerBaseHeader*)&quad;
        uint layerCount = 0;
        if (state.ShouldRender != 0)
        {
            // Runtime calls since the last check (swapchain creation included) may
            // have switched the GL context; the copy must run in the game's.
            try
            {
                KeepContextCurrent();
                CopyFrameIntoSwapchain(xr, width, height);
            }
            catch (VrUnavailableException)
            {
                // Close the begun frame before the teardown that follows.
                var empty = new FrameEndInfo
                {
                    Type = StructureType.FrameEndInfo,
                    DisplayTime = state.PredictedDisplayTime,
                    EnvironmentBlendMode = EnvironmentBlendMode.Opaque,
                };
                xr.EndFrame(_session, &empty);
                throw;
            }
            var (w, h) = VirtualScreen.SizeMetres(width, height);
            quad.Space = _space;
            quad.EyeVisibility = EyeVisibility.Both;
            quad.SubImage = new SwapchainSubImage
            {
                Swapchain = _swapchain,
                ImageRect = new Rect2Di(new Offset2Di(0, 0), new Extent2Di(width, height)),
                ImageArrayIndex = 0,
            };
            quad.Pose = new Posef { Orientation = new Quaternionf(0, 0, 0, 1), Position = new Vector3f(0, 0, -VirtualScreen.DistanceMetres) };
            quad.Size = new Extent2Df(w, h);
            layerCount = 1;
        }
        var endInfo = new FrameEndInfo
        {
            Type = StructureType.FrameEndInfo,
            DisplayTime = state.PredictedDisplayTime,
            EnvironmentBlendMode = EnvironmentBlendMode.Opaque,
            LayerCount = layerCount,
            Layers = layerCount > 0 ? &layer : null,
        };
        Result ended = xr.EndFrame(_session, &endInfo);
        if (ended == Result.ErrorSessionNotRunning) SkipFrame();
        else Check(ended, "xrEndFrame");
        KeepContextCurrent();
    }

    private static void SkipFrame() => Thread.Sleep(IdleSleepMilliseconds);

    private void CopyFrameIntoSwapchain(XR xr, int width, int height)
    {
        var acquireInfo = new SwapchainImageAcquireInfo { Type = StructureType.SwapchainImageAcquireInfo };
        uint index;
        Check(xr.AcquireSwapchainImage(_swapchain, &acquireInfo, &index), "xrAcquireSwapchainImage");
        var waitInfo = new SwapchainImageWaitInfo { Type = StructureType.SwapchainImageWaitInfo, Timeout = long.MaxValue };
        Check(xr.WaitSwapchainImage(_swapchain, &waitInfo), "xrWaitSwapchainImage");
        try
        {
            _blitter!.CopyBackBuffer(_swapchainImages[index], width, height);
        }
        finally
        {
            var releaseInfo = new SwapchainImageReleaseInfo { Type = StructureType.SwapchainImageReleaseInfo };
            Check(xr.ReleaseSwapchainImage(_swapchain, &releaseInfo), "xrReleaseSwapchainImage");
        }
    }

    /// <summary>A runtime may switch contexts inside a call; the game must get its own back.</summary>
    private void KeepContextCurrent()
    {
        if (Wgl.GetCurrentContext() == _hglrc) return;
        _log("[vr] the OpenXR runtime changed the current OpenGL context; restoring the game's");
        if (!Wgl.MakeCurrent(_hdc, _hglrc)) throw new VrUnavailableException("could not make the game's OpenGL context current again");
    }

    private void Fail(string reason)
    {
        if (Failed) return;
        Failed = true;
        FailureReason = reason;
        _log($"[vr] running flat: {reason}");
        TearDown();
    }

    private void DestroySwapchain()
    {
        if (_swapchain.Handle == 0) return;
        _xr?.DestroySwapchain(_swapchain);
        _swapchain = default;
        _swapchainImages = [];
    }

    private void TearDown()
    {
        var xr = _xr;
        _sessionRunning = false;
        _blitter?.Dispose();
        _blitter = null;
        if (xr is null) return;
        DestroySwapchain();
        if (_space.Handle != 0) xr.DestroySpace(_space);
        if (_session.Handle != 0) xr.DestroySession(_session);
        if (_instance.Handle != 0) xr.DestroyInstance(_instance);
        _space = default;
        _session = default;
        _instance = default;
        _xr = null;
        if (_hglrc != 0 && Wgl.GetCurrentContext() != _hglrc) Wgl.MakeCurrent(_hdc, _hglrc);
        xr.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        TearDown();
    }

    private static void Check(Result result, string call)
    {
        if (result < 0) throw new VrUnavailableException($"{call} failed: {result}");
    }

    private static void CopyName(string text, byte* destination, int capacity)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
        int length = Math.Min(bytes.Length, capacity - 1);
        for (int i = 0; i < length; i++) destination[i] = bytes[i];
        destination[length] = 0;
    }

    private static string Version(ulong version) => $"{version >> 48}.{(version >> 32) & 0xFFFF}.{version & 0xFFFFFFFF}";

    private static class Wgl
    {
        [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentContext")] public static extern nint GetCurrentContext();
        [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentDC")] public static extern nint GetCurrentDC();
        [DllImport("opengl32.dll", EntryPoint = "wglMakeCurrent")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool MakeCurrent(nint hdc, nint hglrc);
    }
}
