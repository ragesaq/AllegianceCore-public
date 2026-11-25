namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>Receives each finished frame for a head-mounted display. The game
/// library knows only this seam; the OpenXR code lives in the desktop head's
/// Allegiance.ClientCore.Vr project, so the shared library and the browser and
/// Android heads carry no VR dependency.</summary>
public interface IVrFrameSink : IDisposable
{
    /// <summary>Called once per frame on the render thread, after the frame is
    /// complete in the back buffer and before it is presented to the window.
    /// The graphics context is current. A sink that cannot continue sets
    /// <see cref="Failed"/>; the game drops it, and drops one that throws the
    /// same way.</summary>
    void SubmitFrame(int backBufferWidth, int backBufferHeight);

    /// <summary>True once the sink has given up; the game then runs flat.</summary>
    bool Failed { get; }
}
