using Allegiance.ClientCore.MonoGame.Rendering;
using Xunit;
using SimVec = Allegiance.Core.Collision.Vector3;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>Missiles drawn between the server's 20 Hz position updates
/// (ragesaq, 2026-11-26: missiles looked low frame rate or doubled in VR).</summary>
public sealed class MissileMotionSmootherTests
{
    private const short Id = 7;
    private static SimVec X(float x) => new(x, 0f, 0f);

    // Frames at 90 Hz; the server moves the missile 15 m every 50 ms (300 m/s).
    private static (MissileMotionSmoother M, double Now) Flying()
    {
        var m = new MissileMotionSmoother();
        double now = 0;
        m.BeginFrame(now); m.Observe(Id, X(0)); m.EndFrame();
        now = 0.05; m.BeginFrame(now); m.Observe(Id, X(15)); m.EndFrame();
        return (m, now);
    }

    [Fact]
    public void A_first_update_is_drawn_where_the_server_put_it()
    {
        var m = new MissileMotionSmoother();
        m.BeginFrame(1.0);
        Assert.Equal(X(100), m.Observe(Id, X(100)));
        m.EndFrame();
        m.BeginFrame(1.02);
        Assert.Equal(X(100), m.Observe(Id, X(100)));
    }

    [Fact]
    public void Between_updates_a_missile_moves_on_along_its_velocity()
    {
        var (m, now) = Flying();
        m.BeginFrame(now + 0.02);
        var a = m.Observe(Id, X(15));
        m.EndFrame();
        m.BeginFrame(now + 0.04);
        var b = m.Observe(Id, X(15));
        Assert.Equal(21f, a.X, 3);   // 15 + 300 * 0.02
        Assert.Equal(27f, b.X, 3);
    }

    [Fact]
    public void A_missile_is_moved_on_at_most_a_tenth_of_a_second()
    {
        var (m, now) = Flying();
        m.BeginFrame(now + 0.5);
        Assert.Equal(15f + 300f * (float)MissileMotionSmoother.MaxLeadSeconds, m.Observe(Id, X(15)).X, 3);
    }

    [Fact]
    public void Updates_far_apart_give_no_velocity()
    {
        var m = new MissileMotionSmoother();
        m.BeginFrame(0); m.Observe(Id, X(0)); m.EndFrame();
        m.BeginFrame(2.0); m.Observe(Id, X(15)); m.EndFrame();
        m.BeginFrame(2.05);
        Assert.Equal(X(15), m.Observe(Id, X(15)));
    }

    [Fact]
    public void The_velocity_averages_two_intervals_so_a_late_frame_errs_less()
    {
        var (m, now) = Flying();
        // The next update is seen a frame late: 15 m over 61 ms looks like 246 m/s.
        now += 0.061; m.BeginFrame(now); m.Observe(Id, X(30)); m.EndFrame();
        m.BeginFrame(now + 0.02);
        float x = m.Observe(Id, X(30)).X;
        float averaged = 30f + 0.5f * (300f + 15f / 0.061f) * 0.02f;
        Assert.Equal(averaged, x, 2);
    }

    [Fact]
    public void The_second_view_of_a_frame_gets_the_first_views_position()
    {
        var (m, now) = Flying();
        m.BeginFrame(now + 0.02);
        var left = m.Observe(Id, X(15));
        // An update lands between the two eyes' passes: the right eye still
        // draws the left eye's position.
        Assert.Equal(left, m.Shown(Id, X(30)));
        Assert.Equal(X(99), m.Shown(8, X(99)));
    }

    [Fact]
    public void A_missile_not_seen_in_a_frame_is_forgotten()
    {
        var (m, now) = Flying();
        Assert.Equal(1, m.Count);
        m.BeginFrame(now + 0.01);
        m.EndFrame();
        Assert.Equal(0, m.Count);
        m.BeginFrame(now + 0.02);
        Assert.Equal(X(15), m.Observe(Id, X(15)));
    }
}
