using System.Runtime.InteropServices;

namespace Allegiance.ClientCore.Vr;

/// <summary>Copies the window's back buffer (framebuffer 0, where MonoGame
/// DesktopGL finishes each frame) into an OpenXR swapchain texture with
/// glBlitFramebuffer. GL entry points come from SDL_GL_GetProcAddress, as in
/// HdCompressedUpload. Every piece of GL state it touches is put back.</summary>
internal sealed unsafe class GlBlitter : IDisposable
{
    private const uint ReadFramebuffer = 0x8CA8;
    private const uint DrawFramebuffer = 0x8CA9;
    private const uint ReadFramebufferBinding = 0x8CAA;
    private const uint DrawFramebufferBinding = 0x8CA6;
    private const uint ColorAttachment0 = 0x8CE0;
    private const uint Texture2D = 0x0DE1;
    private const uint FramebufferComplete = 0x8CD5;
    private const uint FramebufferSrgb = 0x8DB9;
    private const uint ScissorTest = 0x0C11;
    private const uint ColorBufferBit = 0x4000;
    private const uint Nearest = 0x2600;

    private readonly delegate* unmanaged<uint, int*, void> _getIntegerv;
    private readonly delegate* unmanaged<int, uint*, void> _genFramebuffers;
    private readonly delegate* unmanaged<int, uint*, void> _deleteFramebuffers;
    private readonly delegate* unmanaged<uint, uint, void> _bindFramebuffer;
    private readonly delegate* unmanaged<uint, uint, uint, uint, int, void> _framebufferTexture2D;
    private readonly delegate* unmanaged<uint, uint> _checkFramebufferStatus;
    private readonly delegate* unmanaged<int, int, int, int, int, int, int, int, uint, uint, void> _blitFramebuffer;
    private readonly delegate* unmanaged<uint, byte> _isEnabled;
    private readonly delegate* unmanaged<uint, void> _enable;
    private readonly delegate* unmanaged<uint, void> _disable;
    private readonly delegate* unmanaged<uint> _getError;
    private const int LoggedErrorBatches = 5;

    private readonly Action<string> _log;
    private uint _framebuffer;
    private int _errorBatches;

    private GlBlitter(delegate* unmanaged[Cdecl]<byte*, nint> proc, Action<string> log)
    {
        _log = log;
        nint Load(string name)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(name + "\0");
            fixed (byte* p = bytes)
            {
                nint address = proc(p);
                return address != 0 ? address : throw new VrUnavailableException($"GL entry point {name} is missing");
            }
        }
        _getIntegerv = (delegate* unmanaged<uint, int*, void>)Load("glGetIntegerv");
        _genFramebuffers = (delegate* unmanaged<int, uint*, void>)Load("glGenFramebuffers");
        _deleteFramebuffers = (delegate* unmanaged<int, uint*, void>)Load("glDeleteFramebuffers");
        _bindFramebuffer = (delegate* unmanaged<uint, uint, void>)Load("glBindFramebuffer");
        _framebufferTexture2D = (delegate* unmanaged<uint, uint, uint, uint, int, void>)Load("glFramebufferTexture2D");
        _checkFramebufferStatus = (delegate* unmanaged<uint, uint>)Load("glCheckFramebufferStatus");
        _blitFramebuffer = (delegate* unmanaged<int, int, int, int, int, int, int, int, uint, uint, void>)Load("glBlitFramebuffer");
        _isEnabled = (delegate* unmanaged<uint, byte>)Load("glIsEnabled");
        _enable = (delegate* unmanaged<uint, void>)Load("glEnable");
        _disable = (delegate* unmanaged<uint, void>)Load("glDisable");
        _getError = (delegate* unmanaged<uint>)Load("glGetError");
        uint framebuffer;
        _genFramebuffers(1, &framebuffer);
        _framebuffer = framebuffer;
    }

    /// <summary>Must be called on the render thread with MonoGame's context current.</summary>
    public static GlBlitter Create(Action<string> log)
    {
        foreach (string lib in new[] { "SDL2.dll", "libSDL2-2.0.so.0", "libSDL2.dylib" })
        {
            if (!NativeLibrary.TryLoad(lib, out var handle)) continue;
            try
            {
                if (NativeLibrary.TryGetExport(handle, "SDL_GL_GetProcAddress", out var address))
                    return new GlBlitter((delegate* unmanaged[Cdecl]<byte*, nint>)address, log);
            }
            finally { NativeLibrary.Free(handle); }
        }
        throw new VrUnavailableException("SDL_GL_GetProcAddress is not available");
    }

    /// <summary>Copies the bottom-left <paramref name="width"/> by
    /// <paramref name="height"/> pixels of the back buffer into
    /// <paramref name="texture"/> unchanged: no sRGB conversion, no flip.</summary>
    public void CopyBackBuffer(uint texture, int width, int height)
    {
        int readBinding, drawBinding;
        _getIntegerv(ReadFramebufferBinding, &readBinding);
        _getIntegerv(DrawFramebufferBinding, &drawBinding);
        bool srgb = _isEnabled(FramebufferSrgb) != 0;
        bool scissor = _isEnabled(ScissorTest) != 0;
        DrainEarlierErrors();
        try
        {
            _bindFramebuffer(ReadFramebuffer, 0);
            _bindFramebuffer(DrawFramebuffer, _framebuffer);
            _framebufferTexture2D(DrawFramebuffer, ColorAttachment0, Texture2D, texture, 0);
            uint status = _checkFramebufferStatus(DrawFramebuffer);
            if (status != FramebufferComplete) throw new VrUnavailableException($"swapchain framebuffer incomplete (0x{status:X4})");
            if (srgb) _disable(FramebufferSrgb);
            if (scissor) _disable(ScissorTest);
            _blitFramebuffer(0, 0, width, height, 0, 0, width, height, ColorBufferBit, Nearest);
            uint error = _getError();
            if (error != 0) throw new VrUnavailableException($"glBlitFramebuffer into the swapchain failed (GL error 0x{error:X4})");
            _framebufferTexture2D(DrawFramebuffer, ColorAttachment0, Texture2D, 0, 0);
        }
        finally
        {
            if (srgb) _enable(FramebufferSrgb);
            if (scissor) _enable(ScissorTest);
            _bindFramebuffer(ReadFramebuffer, (uint)readBinding);
            _bindFramebuffer(DrawFramebuffer, (uint)drawBinding);
        }
    }

    /// <summary>Errors left by the frame's own drawing are not this copy's; clear
    /// them so the check after the blit sees only what it raised. Clearing them
    /// hides them from the game's own checks (HdCompressedUpload's "GL error before
    /// BC upload"), so they are logged here instead.</summary>
    private void DrainEarlierErrors()
    {
        List<string>? codes = null;
        for (int i = 0; i < 16; i++)
        {
            uint error = _getError();
            if (error == 0) break;
            (codes ??= []).Add($"0x{error:X4}");
        }
        if (codes is null || _errorBatches > LoggedErrorBatches) return;
        _errorBatches++;
        _log(_errorBatches <= LoggedErrorBatches
            ? $"[vr] GL errors from the frame before the copy: {string.Join(", ", codes)}"
            : "[vr] more GL errors from the frame before the copy; no longer logged");
    }

    public void Dispose()
    {
        if (_framebuffer == 0) return;
        uint framebuffer = _framebuffer;
        _framebuffer = 0;
        _deleteFramebuffers(1, &framebuffer);
    }
}
