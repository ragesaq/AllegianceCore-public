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
    /// images when the game drew the views <see cref="BeginFrame"/> returned;
    /// the back buffer then holds only the HUD and 2-D screens over a transparent
    /// background. A sink that cannot continue sets <see cref="Failed"/>; the
    /// game drops it, and drops one that throws the same way.</summary>
    void SubmitFrame(int backBufferWidth, int backBufferHeight, VrEyeImages eyes);

    /// <summary>True once the sink has given up; the game then runs flat.</summary>
    bool Failed { get; }

    /// <summary>Asks the sink to make the head's current position and facing the
    /// seated origin of the view, from the next headset frame on. Called on the
    /// game thread; a sink with no tracked head ignores it.</summary>
    void Recenter() { }
}
