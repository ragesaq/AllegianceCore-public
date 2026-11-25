namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>The loop settings a VR frame sink takes over. Game1 maps them onto
/// its GraphicsDeviceManager and Game properties; tests supply a fake.</summary>
internal interface IVrPacingTarget
{
    bool FixedTimeStep { get; set; }
    TimeSpan InactiveSleep { get; set; }
    void DisableVsync();
}

/// <summary>The game's side of the VR seam: attaching a sink, handing it each
/// frame, and dropping it to run flat. While a sink is attached the headset's
/// frame wait paces the loop, so vsync, the fixed time step and the
/// inactive-window sleep are off (the headset keeps its rate when another window
/// has focus). When the sink fails or throws the game keeps running flat: the
/// fixed time step and the sleep come back; vsync stays off until restart,
/// because turning it on needs a device reset in the middle of a frame.</summary>
internal sealed class VrFrameSinkHost(IVrPacingTarget pacing, Action<string> log)
{
    private IVrFrameSink? _sink;
    private bool _flatFixedTimeStep;
    private TimeSpan _flatInactiveSleep;

    public bool Attached => _sink is not null;

    public void Attach(IVrFrameSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (_sink is not null) throw new InvalidOperationException("A VR frame sink is already attached.");
        _sink = sink;
        _flatFixedTimeStep = pacing.FixedTimeStep;
        _flatInactiveSleep = pacing.InactiveSleep;
        TakeOverPacing();
    }

    /// <summary>Called after the game sets its own vsync or frame cap (saved
    /// benchmark settings are applied at startup). The flat choice is kept for a
    /// fallback; while a sink is attached the headset still paces the loop.</summary>
    public void KeepPacing()
    {
        if (_sink is null) return;
        _flatFixedTimeStep = pacing.FixedTimeStep;
        TakeOverPacing();
    }

    public void Submit(int backBufferWidth, int backBufferHeight)
    {
        if (_sink is null) return;
        try
        {
            _sink.SubmitFrame(backBufferWidth, backBufferHeight);
        }
        catch (Exception ex)
        {
            log($"[vr] frame sink threw, running flat: {ex.GetType().Name}: {ex.Message}");
            Drop();
            return;
        }
        if (_sink.Failed) Drop();
    }

    public void Dispose()
    {
        var sink = _sink;
        _sink = null;
        sink?.Dispose();
    }

    private void TakeOverPacing()
    {
        pacing.DisableVsync();
        pacing.FixedTimeStep = false;
        pacing.InactiveSleep = TimeSpan.Zero;
    }

    private void Drop()
    {
        var sink = _sink;
        _sink = null;
        pacing.FixedTimeStep = _flatFixedTimeStep;
        pacing.InactiveSleep = _flatInactiveSleep;
        try { sink?.Dispose(); }
        catch (Exception ex) { log($"[vr] frame sink dispose failed: {ex.GetType().Name}: {ex.Message}"); }
    }
}
