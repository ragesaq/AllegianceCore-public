namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>Receives each finished frame for a head-mounted display. The game
/// library knows only this seam; the OpenXR code lives in the desktop head's
/// Allegiance.ClientCore.Vr project, so the shared library and the browser and
/// Android heads carry no VR dependency.</summary>
public interface IVrFrameSink : IDisposable
{
    /// <summary>Called once per frame on the render thread at the start of the
    /// frame's drawing, with the graphics context current. Waits for the headset
    /// and returns the two eye views the game should draw this frame, or null
    /// when the frame is drawn flat only (the headset is not showing the app, or
    /// the session is not running).</summary>
    VrStereoFrame? BeginFrame();

    /// <summary>Called once per frame on the render thread, after the frame is
    /// complete in the back buffer and before it is presented to the window.
    /// The graphics context is current. <paramref name="eyes"/> holds the eye
    /// images, with the HUD drawn into them, when the game drew the views
    /// <see cref="BeginFrame"/> returned; the back buffer then holds a mirror of
    /// the left eye for the window. Otherwise the back buffer holds the whole
    /// flat frame. A sink that cannot continue sets <see cref="Failed"/>; the
    /// game drops it, and drops one that throws the same way.</summary>
    void SubmitFrame(int backBufferWidth, int backBufferHeight, VrEyeImages eyes);

    /// <summary>True once the sink has given up; the game then runs flat.</summary>
    bool Failed { get; }
}
