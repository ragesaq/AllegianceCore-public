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
        public bool FailOnBegin;
        public bool ThrowOnBegin;
        public VrStereoFrame? Frame;
        public VrEyeImages LastEyes;
        public int Begun;
        public int Submitted;
        public int Disposed;
        public int Recentered;
        public bool ThrowOnRecenter;
        public bool Failed { get; private set; }

        public void Recenter()
        {
            Recentered++;
            if (ThrowOnRecenter) throw new InvalidOperationException("boom");
        }

        public VrStereoFrame? BeginFrame()
        {
            Begun++;
            if (ThrowOnBegin) throw new InvalidOperationException("boom");
            if (FailOnBegin) Failed = true;
            return Frame;
        }

        public void SubmitFrame(int backBufferWidth, int backBufferHeight, VrEyeImages eyes)
        {
            Submitted++;
            LastEyes = eyes;
            if (ThrowOnSubmit) throw new InvalidOperationException("boom");
            if (FailOnSubmit) Failed = true;
        }

        public void Dispose() => Disposed++;
    }

    private static readonly VrStereoFrame Stereo = new(
        new VrEyeView(Microsoft.Xna.Framework.Quaternion.Identity, new(-0.032f, 0, 0), -0.8f, 0.7f, 0.75f, -0.75f),
        new VrEyeView(Microsoft.Xna.Framework.Quaternion.Identity, new(0.032f, 0, 0), -0.7f, 0.8f, 0.75f, -0.75f),
        1440, 1600);

    private sealed class PlainSink : IVrFrameSink
    {
        public bool Failed => false;
        public VrStereoFrame? BeginFrame() => null;
        public void SubmitFrame(int backBufferWidth, int backBufferHeight, VrEyeImages eyes) { }
        public void Dispose() { }
    }

    [Fact]
    public void Recenter_ReachesTheAttachedSink()
    {
        var (host, _, _) = Make();
        var sink = new Sink();
        host.Attach(sink);
        host.Recenter();
        Assert.Equal(1, sink.Recentered);
        Assert.True(host.Attached);
    }

    [Fact]
    public void Recenter_WithoutASink_OrOnASinkWithoutRecenter_DoesNothing()
    {
        var (host, _, _) = Make();
        host.Recenter();
        host.Attach(new PlainSink());
        host.Recenter();
        Assert.True(host.Attached);
    }

    [Fact]
    public void Recenter_ThatThrows_DropsTheSink()
    {
        var (host, _, log) = Make();
        var sink = new Sink { ThrowOnRecenter = true };
        host.Attach(sink);
        host.Recenter();
        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
        Assert.Contains(log, l => l.Contains("recenter failed"));
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

        host.Submit(1280, 720, default);

        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
        Assert.True(pacing.FixedTimeStep);
        Assert.Equal(TimeSpan.FromMilliseconds(20), pacing.InactiveSleep);
        host.Submit(1280, 720, default);
        Assert.Equal(1, sink.Submitted);
    }

    [Fact]
    public void A_throwing_sink_is_dropped_without_the_exception_reaching_the_frame()
    {
        var (host, pacing, log) = Make();
        var sink = new Sink { ThrowOnSubmit = true };
        host.Attach(sink);

        host.Submit(1280, 720, default);

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

        host.Submit(1280, 720, default);
        Assert.False(pacing.FixedTimeStep);
        // The sleep saved at attach survives: KeepPacing runs while it is zeroed.
        Assert.Equal(TimeSpan.FromMilliseconds(20), pacing.InactiveSleep);
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
        host.Submit(1280, 720, default);
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

    [Fact]
    public void The_sinks_eye_views_and_the_games_eye_images_pass_through()
    {
        var (host, _, _) = Make();
        var sink = new Sink { Frame = Stereo };
        host.Attach(sink);

        Assert.Same(Stereo, host.BeginFrame());
        var eyes = new VrEyeImages(7, 8, 1440, 1600, 1.2f);
        host.Submit(1280, 720, eyes);

        Assert.Equal(eyes, sink.LastEyes);
        Assert.True(host.Attached);
    }

    [Fact]
    public void Without_a_sink_every_frame_is_flat()
    {
        var (host, _, _) = Make();
        Assert.Null(host.BeginFrame());
    }

    [Fact]
    public void A_sink_that_throws_when_the_frame_begins_is_dropped_and_the_frame_is_flat()
    {
        var (host, pacing, log) = Make();
        var sink = new Sink { Frame = Stereo, ThrowOnBegin = true };
        host.Attach(sink);

        Assert.Null(host.BeginFrame());

        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
        Assert.True(pacing.FixedTimeStep);
        Assert.Contains(log, line => line.StartsWith("[vr] frame sink threw, running flat:", StringComparison.Ordinal));
        host.Submit(1280, 720, default);
        Assert.Equal(0, sink.Submitted);
    }

    [Fact]
    public void A_sink_that_fails_when_the_frame_begins_gives_no_eye_views()
    {
        var (host, _, _) = Make();
        var sink = new Sink { Frame = Stereo, FailOnBegin = true };
        host.Attach(sink);

        Assert.Null(host.BeginFrame());
        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
    }

    [Fact]
    public void Abandoning_drops_the_sink_with_the_reason_logged()
    {
        var (host, pacing, log) = Make();
        var sink = new Sink();
        host.Attach(sink);

        host.Abandon("no texture");
        host.Abandon("again");

        Assert.False(host.Attached);
        Assert.Equal(1, sink.Disposed);
        Assert.True(pacing.FixedTimeStep);
        Assert.Equal(["[vr] running flat: no texture"], log);
    }
}
