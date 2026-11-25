using Allegiance.ClientCore.MonoGame.Vr;

namespace Allegiance.ClientCore.MonoGame;

public sealed partial class Game1
{
    private VrFrameSinkHost? _vrHost;

    /// <summary>Called by the head before <c>Run()</c> when VR was requested.
    /// <see cref="VrFrameSinkHost"/> says what changes while the sink is attached.</summary>
    public void AttachVrFrameSink(IVrFrameSink sink)
    {
        _vrHost ??= new VrFrameSinkHost(new VrPacing(this), Console.WriteLine);
        _vrHost.Attach(sink);
    }

    private void KeepVrPacing() => _vrHost?.KeepPacing();

    private void SubmitVrFrame()
    {
        if (_vrHost is not { Attached: true }) return;
        var pp = GraphicsDevice.PresentationParameters;
        _vrHost.Submit(pp.BackBufferWidth, pp.BackBufferHeight);
    }

    private void DisposeVrSink() => _vrHost?.Dispose();

    private sealed class VrPacing(Game1 game) : IVrPacingTarget
    {
        public bool FixedTimeStep
        {
            get => game.IsFixedTimeStep;
            set => game.IsFixedTimeStep = value;
        }

        public TimeSpan InactiveSleep
        {
            get => game.InactiveSleepTime;
            set => game.InactiveSleepTime = value;
        }

        public void DisableVsync() => game._graphics.SynchronizeWithVerticalRetrace = false;
    }
}
