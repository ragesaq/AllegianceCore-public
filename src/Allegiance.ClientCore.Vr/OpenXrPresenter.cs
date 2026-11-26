using System.Runtime.InteropServices;
using Allegiance.ClientCore.MonoGame.Vr;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.KHR;
using XnaQuaternion = Microsoft.Xna.Framework.Quaternion;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using XrSession = Silk.NET.OpenXR.Session;

namespace Allegiance.ClientCore.Vr;

/// <summary>Raised for any reason VR cannot start or continue. The presenter
/// turns it into <see cref="OpenXrPresenter.Failed"/>; the game runs flat.</summary>
public sealed class VrUnavailableException(string message) : Exception(message);

/// <summary>The VR client's OpenXR side. Milestone 2: the game draws the 3-D
/// scene once per eye with the headset's pose, shown on a projection layer (one
/// swapchain per eye), and
/// the HUD and 2-D screens on a quad in front of it. A frame the game draws flat
/// (no head pose, or the eyes were not drawn) is shown on the quad alone, as in
/// milestone 1. OpenXR starts lazily on the first frame, on the render thread,
/// with MonoGame's OpenGL context current. Windows only (XR_KHR_opengl_enable
/// with a WGL binding).</summary>
public sealed unsafe class OpenXrPresenter : IVrFrameSink
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
    // _space is where the eyes and layers are placed. Recenter replaces it with a
    // LOCAL space offset to the head; _localSpace and _viewSpace stay for locating
    // the head when that happens.
    private Space _space;
    private Space _localSpace;
    private Space _viewSpace;
    private readonly VrRecenterRequest _recenter = new();
    private bool _sessionRunning;
    private bool _firstFrameLogged;
    private bool _firstStereoLogged;
    private nint _hdc;
    private nint _hglrc;
    private GlBlitter? _blitter;
    private long _swapchainFormat;
    private int _eyeWidth;
    private int _eyeHeight;

    // The frame begun in BeginFrame and ended in SubmitFrame, and the eye views
    // the game was given for it (the projection layer must carry the same poses).
    private bool _frameOpen;
    private long _displayTime;
    private bool _shouldRender;
    private bool _viewsGiven;
    private readonly View[] _views = new View[2];

    private SwapchainSet _hud;
    private SwapchainSet _leftEye;
    private SwapchainSet _rightEye;

    public OpenXrPresenter(VrOptions options) : this(options.LoaderCandidates(), Console.WriteLine) { }

    internal OpenXrPresenter(IReadOnlyList<string> loaderCandidates, Action<string> log)
    {
        _loaderCandidates = loaderCandidates;
        _log = log;
    }

    public bool Failed { get; private set; }

    /// <summary>Asks for the seated view to be recentred on the head's current
    /// position and facing at the start of the next headset frame.</summary>
    public void Recenter() => _recenter.Press();

    public string? FailureReason { get; private set; }

    public VrStereoFrame? BeginFrame()
    {
        if (Failed || _disposed) return null;
        try
        {
            if (!_initialized)
            {
                _initialized = true;
                Initialize();
            }
            // The game always submits what it begins; this only guards against a
            // frame left open by a draw that threw.
            if (_frameOpen) EndFrame(null, 0);
            PollEvents();
            if (Failed) return null;
            if (!_sessionRunning)
            {
                // No frame loop while the headset is idle; keep the uncapped game loop cool.
                Thread.Sleep(IdleSleepMilliseconds);
                return null;
            }
            return WaitAndLocate();
        }
        catch (Exception ex) when (IsVrFailure(ex))
        {
            Fail(ex.Message);
            return null;
        }
    }

    public void SubmitFrame(int backBufferWidth, int backBufferHeight, VrEyeImages eyes)
    {
        if (Failed || _disposed || !_frameOpen) return;
        try
        {
            SubmitLayers(backBufferWidth, backBufferHeight, eyes);
        }
        catch (Exception ex) when (IsVrFailure(ex))
        {
            Fail(ex.Message);
        }
    }

    private static bool IsVrFailure(Exception ex) =>
        ex is VrUnavailableException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

    private void Initialize()
    {
        var xr = LoadApi();
        _xr = xr;
        if (!OperatingSystem.IsWindows()) throw new VrUnavailableException("the VR client supports Windows only");
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
        ReadEyeSize(xr);

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

        var identity = new Posef { Orientation = new Quaternionf(0, 0, 0, 1), Position = new Vector3f(0, 0, 0) };
        _localSpace = CreateSpace(xr, ReferenceSpaceType.Local, identity);
        _viewSpace = CreateSpace(xr, ReferenceSpaceType.View, identity);
        _space = CreateSpace(xr, ReferenceSpaceType.Local, identity);
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

    /// <summary>The runtime's recommended image size per eye, shared by both eye
    /// render targets and by each eye's own swapchain.</summary>
    private void ReadEyeSize(XR xr)
    {
        uint count;
        Check(xr.EnumerateViewConfigurationView(_instance, _systemId, ViewConfigurationType.PrimaryStereo, 0, &count, null),
            "xrEnumerateViewConfigurationViews");
        if (count != 2) throw new VrUnavailableException($"the headset reports {count} views; stereo needs 2");
        var views = stackalloc ViewConfigurationView[2];
        for (int i = 0; i < 2; i++) views[i] = new ViewConfigurationView { Type = StructureType.ViewConfigurationView };
        Check(xr.EnumerateViewConfigurationView(_instance, _systemId, ViewConfigurationType.PrimaryStereo, 2, &count, views),
            "xrEnumerateViewConfigurationViews");
        _eyeWidth = (int)Math.Max(views[0].RecommendedImageRectWidth, views[1].RecommendedImageRectWidth);
        _eyeHeight = (int)Math.Max(views[0].RecommendedImageRectHeight, views[1].RecommendedImageRectHeight);
        if (_eyeWidth <= 0 || _eyeHeight <= 0) throw new VrUnavailableException("the headset recommends an empty eye image");
        _log($"[vr] eye image {_eyeWidth}x{_eyeHeight} (runtime recommendation)");
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

    /// <summary>Waits for the headset's frame, begins it and locates the eyes
    /// at its predicted display time. Null means the game draws flat this frame;
    /// the frame stays open either way unless the session stopped.</summary>
    private VrStereoFrame? WaitAndLocate()
    {
        var xr = _xr!;
        var waitInfo = new FrameWaitInfo { Type = StructureType.FrameWaitInfo };
        var state = new FrameState { Type = StructureType.FrameState };
        // The runtime can leave the running state between the event poll and the
        // frame calls; its STOPPING event follows, so this frame is just skipped,
        // at the idle rate because nothing else paces the loop.
        Result waited = xr.WaitFrame(_session, &waitInfo, &state);
        if (waited == Result.ErrorSessionNotRunning) { SkipFrame(); return null; }
        Check(waited, "xrWaitFrame");
        if (!_firstFrameLogged)
        {
            _firstFrameLogged = true;
            _log($"[vr] first frame: display period {state.PredictedDisplayPeriod / 1e6:0.00} ms");
        }
        var beginInfo = new FrameBeginInfo { Type = StructureType.FrameBeginInfo };
        Result begun = xr.BeginFrame(_session, &beginInfo);
        if (begun == Result.ErrorSessionNotRunning) { SkipFrame(); return null; }
        Check(begun, "xrBeginFrame");
        _frameOpen = true;
        _displayTime = state.PredictedDisplayTime;
        _shouldRender = state.ShouldRender != 0;
        _viewsGiven = false;
        if (_recenter.Pending)
        {
            if (RecenterSpace(xr)) _recenter.Done();
            else if (_recenter.Missed()) _log("[vr] recenter skipped: the headset was not tracked");
        }
        if (!_shouldRender)
        {
            // Nothing is shown this frame; SubmitFrame ends it with no layers.
            KeepContextCurrent();
            return null;
        }

        var locateInfo = new ViewLocateInfo
        {
            Type = StructureType.ViewLocateInfo,
            ViewConfigurationType = ViewConfigurationType.PrimaryStereo,
            DisplayTime = _displayTime,
            Space = _space,
        };
        var viewState = new ViewState { Type = StructureType.ViewState };
        uint count;
        fixed (View* views = _views)
        {
            views[0] = new View { Type = StructureType.View };
            views[1] = new View { Type = StructureType.View };
            Check(xr.LocateView(_session, &locateInfo, &viewState, 2, &count, views), "xrLocateViews");
        }
        KeepContextCurrent();
        // Without a tracked orientation the frame is drawn flat and shown on the
        // quad, which stays put in the room.
        if (count != 2 || (viewState.ViewStateFlags & ViewStateFlags.OrientationValidBit) == 0) return null;
        _viewsGiven = true;
        return new VrStereoFrame(EyeView(_views[0]), EyeView(_views[1]), _eyeWidth, _eyeHeight);
    }

    private static VrEyeView EyeView(in View view) => new(
        new XnaQuaternion(view.Pose.Orientation.X, view.Pose.Orientation.Y, view.Pose.Orientation.Z, view.Pose.Orientation.W),
        new XnaVector3(view.Pose.Position.X, view.Pose.Position.Y, view.Pose.Position.Z),
        view.Fov.AngleLeft, view.Fov.AngleRight, view.Fov.AngleUp, view.Fov.AngleDown);

    /// <summary>Ends the open frame with the eye images on a projection layer
    /// (when the game drew the views it was given) and the back buffer on a quad:
    /// the HUD alone over transparent in a stereo frame, the whole flat frame
    /// otherwise.</summary>
    private void SubmitLayers(int width, int height, VrEyeImages eyes)
    {
        var xr = _xr!;
        if (!_shouldRender)
        {
            EndFrame(null, 0);
            return;
        }
        bool stereo = _viewsGiven && eyes.Present;
        try
        {
            KeepContextCurrent();
            var projectionViews = stackalloc CompositionLayerProjectionView[2];
            var projection = new CompositionLayerProjection { Type = StructureType.CompositionLayerProjection };
            if (stereo)
            {
                EnsureSwapchain(ref _leftEye, "left eye", eyes.Width, eyes.Height);
                EnsureSwapchain(ref _rightEye, "right eye", eyes.Width, eyes.Height);
                CopyEye(xr, _leftEye, eyes.Left, eyes.Width, eyes.Height);
                CopyEye(xr, _rightEye, eyes.Right, eyes.Width, eyes.Height);
                for (int i = 0; i < 2; i++)
                {
                    projectionViews[i] = new CompositionLayerProjectionView
                    {
                        Type = StructureType.CompositionLayerProjectionView,
                        Pose = _views[i].Pose,
                        Fov = _views[i].Fov,
                        SubImage = new SwapchainSubImage
                        {
                            Swapchain = (i == 0 ? _leftEye : _rightEye).Handle,
                            ImageRect = new Rect2Di(new Offset2Di(0, 0), new Extent2Di(eyes.Width, eyes.Height)),
                            ImageArrayIndex = 0,
                        },
                    };
                }
                projection.Space = _space;
                projection.ViewCount = 2;
                projection.Views = projectionViews;
                if (!_firstStereoLogged)
                {
                    _firstStereoLogged = true;
                    _log($"[vr] first stereo frame: eyes {eyes.Width}x{eyes.Height}, HUD {width}x{height}, back buffer alpha bits {_blitter!.BackBufferAlphaBits()}");
                }
            }

            EnsureSwapchain(ref _hud, "HUD", width, height);
            CopyBackBuffer(xr, width, height);
            var (quadWidth, quadHeight) = stereo
                ? HudQuad.SizeMetres(width, height, eyes.HudVerticalFovRadians)
                : VirtualScreen.SizeMetres(width, height);
            var quad = new CompositionLayerQuad
            {
                Type = StructureType.CompositionLayerQuad,
                // The HUD is drawn over a transparent clear; the flat frame is opaque.
                LayerFlags = stereo ? CompositionLayerFlags.BlendTextureSourceAlphaBit : 0,
                Space = _space,
                EyeVisibility = EyeVisibility.Both,
                SubImage = new SwapchainSubImage
                {
                    Swapchain = _hud.Handle,
                    ImageRect = new Rect2Di(new Offset2Di(0, 0), new Extent2Di(width, height)),
                    ImageArrayIndex = 0,
                },
                Pose = new Posef { Orientation = new Quaternionf(0, 0, 0, 1), Position = new Vector3f(0, 0, -VirtualScreen.DistanceMetres) },
                Size = new Extent2Df(quadWidth, quadHeight),
            };
            var layers = stackalloc CompositionLayerBaseHeader*[2];
            uint layerCount = 0;
            if (stereo) layers[layerCount++] = (CompositionLayerBaseHeader*)&projection;
            layers[layerCount++] = (CompositionLayerBaseHeader*)&quad;
            EndFrame(layers, layerCount);
        }
        catch (VrUnavailableException)
        {
            // Close the begun frame before the teardown that follows.
            if (_frameOpen) EndFrame(null, 0, check: false);
            throw;
        }
    }

    private Space CreateSpace(XR xr, ReferenceSpaceType type, Posef pose)
    {
        var info = new ReferenceSpaceCreateInfo { Type = StructureType.ReferenceSpaceCreateInfo, ReferenceSpaceType = type, PoseInReferenceSpace = pose };
        Space space;
        Check(xr.CreateReferenceSpace(_session, &info, &space), "xrCreateReferenceSpace");
        return space;
    }

    /// <summary>Makes the head's current position and facing the origin of the
    /// space the frame is placed in, keeping the runtime's gravity: only the turn
    /// about the vertical axis is taken from the head, so the horizon stays level.
    /// Returns false only when the head is not tracked this frame, so the request
    /// waits; a runtime error or a pose that is not finite is logged and ends the
    /// request with the view unchanged, never the VR session.</summary>
    private bool RecenterSpace(XR xr)
    {
        var location = new SpaceLocation { Type = StructureType.SpaceLocation };
        Result located = xr.LocateSpace(_viewSpace, _localSpace, _displayTime, &location);
        if (located < 0)
        {
            _log($"[vr] recenter failed: xrLocateSpace {located}");
            return true;
        }
        if ((location.LocationFlags & SpaceLocationFlags.OrientationValidBit) == 0) return false;
        var head = location.Pose;
        bool positioned = (location.LocationFlags & SpaceLocationFlags.PositionValidBit) != 0;
        var (yaw, origin) = SeatedOrigin(
            new XnaQuaternion(head.Orientation.X, head.Orientation.Y, head.Orientation.Z, head.Orientation.W),
            positioned ? new XnaVector3(head.Position.X, head.Position.Y, head.Position.Z) : XnaVector3.Zero);
        if (!IsFinite(yaw, origin))
        {
            _log("[vr] recenter failed: the runtime gave a head pose that is not finite");
            return true;
        }
        var info = new ReferenceSpaceCreateInfo
        {
            Type = StructureType.ReferenceSpaceCreateInfo,
            ReferenceSpaceType = ReferenceSpaceType.Local,
            PoseInReferenceSpace = new Posef
            {
                Orientation = new Quaternionf(yaw.X, yaw.Y, yaw.Z, yaw.W),
                Position = new Vector3f(origin.X, origin.Y, origin.Z),
            },
        };
        Space recentred;
        Result created = xr.CreateReferenceSpace(_session, &info, &recentred);
        if (created < 0)
        {
            _log($"[vr] recenter failed: xrCreateReferenceSpace {created}");
            return true;
        }
        xr.DestroySpace(_space);
        _space = recentred;
        _log($"[vr] view recentred (turn {MathF.Atan2(2f * yaw.Y * yaw.W, 1f - 2f * yaw.Y * yaw.Y) * 180f / MathF.PI:0} degrees{(positioned ? "" : ", position not tracked")})");
        return true;
    }

    internal static bool IsFinite(XnaQuaternion q, XnaVector3 v) =>
        float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W)
        && float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>The pose, in the runtime's LOCAL space, of a seated origin at the
    /// head: the head's position and only its turn about the vertical (+Y) axis.
    /// OpenXR views look down -Z, so the turn is the one that carries -Z onto the
    /// head's forward direction projected on the floor plane.</summary>
    internal static (XnaQuaternion Yaw, XnaVector3 Position) SeatedOrigin(XnaQuaternion head, XnaVector3 position)
    {
        var forward = XnaVector3.Transform(-XnaVector3.UnitZ, head);
        float yaw = forward.X * forward.X + forward.Z * forward.Z < 1e-8f ? 0f : MathF.Atan2(-forward.X, -forward.Z);
        return (XnaQuaternion.CreateFromAxisAngle(XnaVector3.UnitY, yaw), position);
    }

    private void EndFrame(CompositionLayerBaseHeader** layers, uint layerCount, bool check = true)
    {
        var xr = _xr!;
        _frameOpen = false;
        _viewsGiven = false;
        var endInfo = new FrameEndInfo
        {
            Type = StructureType.FrameEndInfo,
            DisplayTime = _displayTime,
            EnvironmentBlendMode = EnvironmentBlendMode.Opaque,
            LayerCount = layerCount,
            Layers = layerCount > 0 ? layers : null,
        };
        Result ended = xr.EndFrame(_session, &endInfo);
        if (!check) return;
        if (ended == Result.ErrorSessionNotRunning) SkipFrame();
        else Check(ended, "xrEndFrame");
        KeepContextCurrent();
    }

    // The frame call that reported the skip may have switched the GL context too;
    // the game presents right after this returns.
    private void SkipFrame()
    {
        KeepContextCurrent();
        Thread.Sleep(IdleSleepMilliseconds);
    }

    private void EnsureSwapchain(ref SwapchainSet set, string name, int width, int height)
    {
        if (set.Handle.Handle != 0 && width == set.Width && height == set.Height) return;
        var xr = _xr!;
        DestroySwapchain(ref set);
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
        Check(xr.CreateSwapchain(_session, &info, &swapchain), $"xrCreateSwapchain ({name})");
        uint count;
        Check(xr.EnumerateSwapchainImages(swapchain, 0, &count, null), "xrEnumerateSwapchainImages");
        var images = new SwapchainImageOpenGLKHR[count];
        for (int i = 0; i < images.Length; i++) images[i].Type = StructureType.SwapchainImageOpenglKhr;
        fixed (SwapchainImageOpenGLKHR* p = images)
            Check(xr.EnumerateSwapchainImages(swapchain, count, &count, (SwapchainImageBaseHeader*)p), "xrEnumerateSwapchainImages");
        set = new SwapchainSet(swapchain, images.Select(i => i.Image).ToArray(), width, height);
        KeepContextCurrent();
        _log($"[vr] {name} swapchain {width}x{height}, GL format 0x{_swapchainFormat:X4}, {count} images");
    }

    private long PickFormat(XR xr)
    {
        uint count;
        Check(xr.EnumerateSwapchainFormats(_session, 0, &count, null), "xrEnumerateSwapchainFormats");
        var formats = new long[count];
        fixed (long* p = formats) Check(xr.EnumerateSwapchainFormats(_session, count, &count, p), "xrEnumerateSwapchainFormats");
        // The game's images hold display-ready (sRGB-encoded) bytes and the copy
        // does no conversion, so an sRGB swapchain shows them as the window does.
        if (formats.Contains(GlSrgb8Alpha8)) return GlSrgb8Alpha8;
        if (formats.Contains(GlRgba8))
        {
            _log("[vr] runtime offers no GL_SRGB8_ALPHA8 swapchain; using GL_RGBA8 (colours may look washed out)");
            return GlRgba8;
        }
        throw new VrUnavailableException("the runtime offers neither GL_SRGB8_ALPHA8 nor GL_RGBA8 swapchains");
    }

    private void CopyBackBuffer(XR xr, int width, int height) =>
        WithImage(xr, _hud, texture => _blitter!.CopyBackBuffer(texture, width, height));

    private void CopyEye(XR xr, SwapchainSet set, uint source, int width, int height) =>
        WithImage(xr, set, texture => _blitter!.CopyTexture(source, width, height, texture, 0, flipRows: true));

    private void WithImage(XR xr, SwapchainSet set, Action<uint> copy)
    {
        var acquireInfo = new SwapchainImageAcquireInfo { Type = StructureType.SwapchainImageAcquireInfo };
        uint index;
        Check(xr.AcquireSwapchainImage(set.Handle, &acquireInfo, &index), "xrAcquireSwapchainImage");
        var waitInfo = new SwapchainImageWaitInfo { Type = StructureType.SwapchainImageWaitInfo, Timeout = long.MaxValue };
        Check(xr.WaitSwapchainImage(set.Handle, &waitInfo), "xrWaitSwapchainImage");
        try
        {
            // Runtime calls may switch the GL context; the copy must run in the game's.
            KeepContextCurrent();
            copy(set.Images[index]);
        }
        finally
        {
            var releaseInfo = new SwapchainImageReleaseInfo { Type = StructureType.SwapchainImageReleaseInfo };
            Check(xr.ReleaseSwapchainImage(set.Handle, &releaseInfo), "xrReleaseSwapchainImage");
            KeepContextCurrent();
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

    private void DestroySwapchain(ref SwapchainSet set)
    {
        if (set.Handle.Handle == 0) return;
        _xr?.DestroySwapchain(set.Handle);
        set = default;
    }

    private void TearDown()
    {
        var xr = _xr;
        _sessionRunning = false;
        _frameOpen = false;
        _blitter?.Dispose();
        _blitter = null;
        if (xr is null) return;
        DestroySwapchain(ref _hud);
        DestroySwapchain(ref _leftEye);
        DestroySwapchain(ref _rightEye);
        if (_space.Handle != 0) xr.DestroySpace(_space);
        if (_viewSpace.Handle != 0) xr.DestroySpace(_viewSpace);
        if (_localSpace.Handle != 0) xr.DestroySpace(_localSpace);
        if (_session.Handle != 0) xr.DestroySession(_session);
        if (_instance.Handle != 0) xr.DestroyInstance(_instance);
        _space = default;
        _viewSpace = default;
        _localSpace = default;
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

    private readonly record struct SwapchainSet(Swapchain Handle, uint[] Images, int Width, int Height);

    private static class Wgl
    {
        [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentContext")] public static extern nint GetCurrentContext();
        [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentDC")] public static extern nint GetCurrentDC();
        [DllImport("opengl32.dll", EntryPoint = "wglMakeCurrent")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool MakeCurrent(nint hdc, nint hglrc);
    }
}
