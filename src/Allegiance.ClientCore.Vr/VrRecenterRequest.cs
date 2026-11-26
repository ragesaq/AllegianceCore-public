namespace Allegiance.ClientCore.Vr;

/// <summary>A recenter press waits a short time for the head to be tracked: a press
/// made as tracking drops (the headset lifted, an overlay taking over) still applies
/// when tracking returns. The wait counts headset frames of a running session, and the
/// session stopping cancels it, so a press made before a stop does not fire when the
/// session resumes; a press made while it is stopped applies when it begins again.
/// Used on the game thread only.</summary>
internal sealed class VrRecenterRequest
{
    /// <summary>Headset frames a press waits for tracking, about one second of a
    /// running session.</summary>
    internal const int WaitFrames = 90;

    private int _framesLeft;

    public bool Pending => _framesLeft > 0;

    public void Press() => _framesLeft = WaitFrames;

    /// <summary>The request was carried out, or failed for good.</summary>
    public void Done() => _framesLeft = 0;

    /// <summary>The session stopped: a press made before it is dropped.</summary>
    public void Cancel() => _framesLeft = 0;

    /// <summary>The head was not tracked this frame. True when the request has just
    /// run out of frames.</summary>
    public bool Missed()
    {
        if (_framesLeft <= 0) return false;
        _framesLeft--;
        return _framesLeft == 0;
    }
}
