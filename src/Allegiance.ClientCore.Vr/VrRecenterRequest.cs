namespace Allegiance.ClientCore.Vr;

/// <summary>A recenter press waits a short time for the head to be tracked: a press
/// made as tracking drops (the headset lifted, an overlay taking over) still applies
/// when tracking returns, and a press made long before does not fire later. Used on
/// the game thread only.</summary>
internal sealed class VrRecenterRequest
{
    /// <summary>Headset frames a press waits for tracking, about one second.</summary>
    internal const int WaitFrames = 90;

    private int _framesLeft;

    public bool Pending => _framesLeft > 0;

    public void Press() => _framesLeft = WaitFrames;

    /// <summary>The request was carried out, or failed for good.</summary>
    public void Done() => _framesLeft = 0;

    /// <summary>The head was not tracked this frame. True when the request has just
    /// run out of frames.</summary>
    public bool Missed()
    {
        if (_framesLeft <= 0) return false;
        _framesLeft--;
        return _framesLeft == 0;
    }
}
