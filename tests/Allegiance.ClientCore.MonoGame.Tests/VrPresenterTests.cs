using Allegiance.ClientCore.MonoGame.Vr;
using Allegiance.ClientCore.Vr;
using Silk.NET.OpenXR;
using Xunit;

namespace Allegiance.ClientCore.MonoGame.Tests;

public sealed class VrPresenterTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void Vr_switch_enables_vr_and_is_removed_from_the_arguments()
    {
        string[] args = ["--lobby", "http://x", "--vr", "host"];
        var options = VrOptions.Take(ref args, Env());
        Assert.True(options.Enabled);
        Assert.Equal(["--lobby", "http://x", "host"], args);
    }

    [Fact]
    public void Environment_enables_vr_without_touching_the_arguments()
    {
        string[] args = ["--training"];
        var options = VrOptions.Take(ref args, Env((VrOptions.EnableVariable, "1")));
        Assert.True(options.Enabled);
        Assert.Equal(["--training"], args);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("true")]
    public void Vr_is_off_unless_asked_for(string? value)
    {
        string[] args = ["--debris", "0.5"];
        var options = VrOptions.Take(ref args, name => name == VrOptions.EnableVariable ? value : null);
        Assert.False(options.Enabled);
        Assert.Equal(["--debris", "0.5"], args);
    }

    [Fact]
    public void Configured_loader_is_tried_before_the_default_names()
    {
        string[] args = [];
        var options = VrOptions.Take(ref args, Env((VrOptions.LoaderVariable, "custom/openxr_loader.dll")));
        var candidates = options.LoaderCandidates();
        Assert.Equal("custom/openxr_loader.dll", candidates[0]);
        Assert.True(candidates.Count > 1);
    }

    [Fact]
    public void Blank_loader_variable_means_the_default_search()
    {
        string[] args = [];
        var options = VrOptions.Take(ref args, Env((VrOptions.LoaderVariable, "  ")));
        Assert.Null(options.LoaderPath);
    }

    [Theory]
    [InlineData(SessionState.Idle, VrSessionAction.None)]
    [InlineData(SessionState.Ready, VrSessionAction.Begin)]
    [InlineData(SessionState.Synchronized, VrSessionAction.None)]
    [InlineData(SessionState.Visible, VrSessionAction.None)]
    [InlineData(SessionState.Focused, VrSessionAction.None)]
    [InlineData(SessionState.Stopping, VrSessionAction.End)]
    [InlineData(SessionState.LossPending, VrSessionAction.TearDown)]
    [InlineData(SessionState.Exiting, VrSessionAction.TearDown)]
    public void Session_state_changes_map_to_the_lifecycle_actions(SessionState state, VrSessionAction expected)
    {
        Assert.Equal(expected, VrSessionLifecycle.OnStateChanged(state));
    }

    [Fact]
    public void Virtual_screen_keeps_the_back_buffer_aspect()
    {
        var (width, height) = VirtualScreen.SizeMetres(1920, 1080);
        Assert.Equal(VirtualScreen.WidthMetres, width);
        Assert.Equal(VirtualScreen.WidthMetres * 1080f / 1920f, height, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => VirtualScreen.SizeMetres(0, 1080));
    }

    [Fact]
    public void Missing_loader_fails_closed_to_flat_without_throwing()
    {
        var log = new List<string>();
        string missing = Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N"), "openxr_loader.dll");
        using var presenter = new OpenXrPresenter([missing], log.Add);

        Assert.Null(presenter.BeginFrame());

        Assert.True(presenter.Failed);
        Assert.Contains("no OpenXR loader found", presenter.FailureReason);
        Assert.Contains(VrOptions.LoaderVariable, presenter.FailureReason);
        Assert.Contains(log, line => line.StartsWith("[vr] running flat:", StringComparison.Ordinal));

        int logged = log.Count;
        Assert.Null(presenter.BeginFrame());
        presenter.SubmitFrame(1280, 720, new VrEyeImages(1, 2, 64, 64, 1f));
        Assert.Equal(logged, log.Count);
    }

    [Fact]
    public void A_frame_submitted_before_any_frame_began_starts_nothing()
    {
        var log = new List<string>();
        string missing = Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N"), "openxr_loader.dll");
        using var presenter = new OpenXrPresenter([missing], log.Add);

        presenter.SubmitFrame(1280, 720, default);

        Assert.False(presenter.Failed);
        Assert.Empty(log);
    }

    [Fact]
    public void Hud_quad_covers_the_game_cameras_vertical_field_of_view()
    {
        float fov = 1.2f;
        var (width, height) = HudQuad.SizeMetres(1920, 1080, fov);
        Assert.Equal(2f * VirtualScreen.DistanceMetres * MathF.Tan(fov / 2f), height, 5);
        Assert.Equal(height * 1920f / 1080f, width, 5);
        // Seen from the origin the quad's top edge is half the field of view up.
        Assert.Equal(fov / 2f, MathF.Atan2(height / 2f, VirtualScreen.DistanceMetres), 5);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(3.5f)]
    public void Hud_quad_without_a_usable_field_of_view_is_the_virtual_screen(float fov)
    {
        Assert.Equal(VirtualScreen.SizeMetres(1920, 1080), HudQuad.SizeMetres(1920, 1080, fov));
    }
}
