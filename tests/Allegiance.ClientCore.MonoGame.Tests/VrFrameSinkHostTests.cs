using Allegiance.ClientCore.MonoGame.Vr;
using Xunit;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The game's side of the VR seam: what the flat game is left with when
/// a sink is attached, fails, throws or is disposed.</summary>
public sealed class VrFrameSinkHostTests
{
    private sealed class Pacing : IVrPacingTarget
    {
        public bool FixedTimeStep { get; set; } = true;
        public TimeSpan InactiveSleep { get; set; } = TimeSpan.FromMilliseconds(20);
        public int VsyncDisabled { get; private set; }
        public void DisableVsync() => VsyncDisabled++;
    }

    private sealed class Sink : IVrFrameSink
    {
        public bool FailOnSubmit;
        public bool ThrowOnSubmit;
        public int Submitted;
        public int Disposed;
        public bool Failed { get; private set; }

        public void SubmitFrame(int backBufferWidth, int backBufferHeight)
        {
            Submitted++;
            if (ThrowOnSubmit) throw new InvalidOperationException("boom");
            if (FailOnSubmit) Failed = true;
        }

        public void Dispose() => Disposed++;
    }

    private static (VrFrameSinkHost Host, Pacing Pacing, List<string> Log) Make()
    {
        var pacing = new Pacing();
        var log = new List<string>();
        return (new VrFrameSinkHost(pacing, log.Add), pacing, log);
    }

    [Fact]
    public void Attaching_hands_pacing_to_the_headset()
    {
        var (host, pacing, _) = Make();
        host.Attach(new Sink());
        Assert.True(host.Attached);
        Assert.False(pacing.FixedTimeStep);
        Assert.Equal(TimeSpan.Zero, pacing.InactiveSleep);
        Assert.Equal(1, pacing.VsyncDisabled);
    }

    [Fact]
    public void A_failed_sink_is_dropped_and_the_flat_pacing_comes_back()
    {
        var (host, pacing, _) = Make();
        var sink = new Sink { FailOnSubmit = true };
        host.Attach(sink);

        host.Submit(1280, 720);

        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
        Assert.True(pacing.FixedTimeStep);
        Assert.Equal(TimeSpan.FromMilliseconds(20), pacing.InactiveSleep);
        host.Submit(1280, 720);
        Assert.Equal(1, sink.Submitted);
    }

    [Fact]
    public void A_throwing_sink_is_dropped_without_the_exception_reaching_the_frame()
    {
        var (host, pacing, log) = Make();
        var sink = new Sink { ThrowOnSubmit = true };
        host.Attach(sink);

        host.Submit(1280, 720);

        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
        Assert.True(pacing.FixedTimeStep);
        Assert.Contains(log, line => line.StartsWith("[vr] frame sink threw, running flat:", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_applied_while_attached_are_what_the_fallback_restores()
    {
        var (host, pacing, _) = Make();
        host.Attach(new Sink { FailOnSubmit = true });

        // A saved benchmark setting turns the frame cap off while the headset paces.
        pacing.FixedTimeStep = false;
        host.KeepPacing();
        Assert.False(pacing.FixedTimeStep);
        Assert.Equal(2, pacing.VsyncDisabled);

        host.Submit(1280, 720);
        Assert.False(pacing.FixedTimeStep);
    }

    [Fact]
    public void Keeping_pacing_without_a_sink_changes_nothing()
    {
        var (host, pacing, _) = Make();
        host.KeepPacing();
        Assert.True(pacing.FixedTimeStep);
        Assert.Equal(0, pacing.VsyncDisabled);
    }

    [Fact]
    public void Dispose_disposes_the_sink_once()
    {
        var (host, _, _) = Make();
        var sink = new Sink();
        host.Attach(sink);
        host.Dispose();
        host.Dispose();
        host.Submit(1280, 720);
        Assert.Equal(1, sink.Disposed);
        Assert.Equal(0, sink.Submitted);
    }

    [Fact]
    public void A_second_sink_is_refused()
    {
        var (host, _, _) = Make();
        host.Attach(new Sink());
        Assert.Throws<InvalidOperationException>(() => host.Attach(new Sink()));
    }
}
