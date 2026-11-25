using Allegiance.ClientCore.MonoGame.Vr;

namespace Allegiance.ClientCore.MonoGame;

public sealed partial class Game1
{
    private IVrFrameSink? _vrSink;
    private bool _vrSavedFixedTimeStep;
    private TimeSpan _vrSavedInactiveSleep;

    /// <summary>Set by the head before <c>Run()</c> when VR was requested. While a
    /// sink is attached the headset's frame wait paces the loop, so vsync, the
    /// fixed time step and the inactive-window sleep are turned off (the headset
    /// keeps its rate when another window has focus). If the sink fails the game
    /// keeps running flat: those come back, except vsync, which stays off until
    /// restart.</summary>
    public IVrFrameSink? VrFrameSink
    {
        get => _vrSink;
        set
        {
            if (_vrSink is not null) throw new InvalidOperationException("A VR frame sink is already attached.");
            _vrSink = value;
            if (value is null) return;
            _vrSavedFixedTimeStep = IsFixedTimeStep;
            _vrSavedInactiveSleep = InactiveSleepTime;
            _graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
        }
    }

    /// <summary>Called where the game sets its own vsync or frame cap (saved
    /// benchmark settings are applied at startup). The flat choice is kept for a
    /// fallback; while a headset is attached it still paces the loop.</summary>
    private void KeepVrPacing()
    {
        if (_vrSink is null) return;
        _vrSavedFixedTimeStep = IsFixedTimeStep;
        _graphics.SynchronizeWithVerticalRetrace = false;
        IsFixedTimeStep = false;
    }

    private void SubmitVrFrame()
    {
        if (_vrSink is null) return;
        var pp = GraphicsDevice.PresentationParameters;
        try
        {
            _vrSink.SubmitFrame(pp.BackBufferWidth, pp.BackBufferHeight);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[vr] frame sink threw, running flat: {ex.GetType().Name}: {ex.Message}");
            DropVrSink();
            return;
        }
        if (_vrSink.Failed) DropVrSink();
    }

    private void DropVrSink()
    {
        var sink = _vrSink;
        _vrSink = null;
        IsFixedTimeStep = _vrSavedFixedTimeStep;
        InactiveSleepTime = _vrSavedInactiveSleep;
        try { sink?.Dispose(); }
        catch (Exception ex) { Console.WriteLine($"[vr] frame sink dispose failed: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void DisposeVrSink()
    {
        var sink = _vrSink;
        _vrSink = null;
        sink?.Dispose();
    }
}
