using Allegiance.ClientCore.MonoGame.Flight;
using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.ClientCore.MonoGame.Vr;
using Allegiance.Core.Sim;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>Zone C grows the target pane about two times, so in VR the pane is
/// drawn into its own image at twice the HUD image's pixels and the main pass
/// leaves it out (ragesaq's fifth headset look, 2026-11-27: "the text looks soft
/// and needs to be rendered at a higher resolution, the size looks good").</summary>
public sealed class VrTargetPaneImageTests
{
    [Fact]
    public void The_main_pass_draws_a_shown_target_pane_unless_it_is_drawn_apart()
    {
        var model = Model(withPane: true);
        Assert.True(model.TargetPaneVisible);
        Assert.True(HudRenderer.TargetPaneOnMainPass(model, apart: false));
        Assert.False(HudRenderer.TargetPaneOnMainPass(model, apart: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Without_a_target_the_main_pass_draws_no_pane(bool apart)
    {
        Assert.False(HudRenderer.TargetPaneOnMainPass(Model(withPane: false), apart));
    }

    [Fact]
    public void At_density_one_a_cutout_is_sampled_half_a_texel_inside_its_rectangle()
    {
        var (uv0, uv1) = VrHudZones.SourceUv(new Rectangle(481, 389, 318, 331), 1280, 720);
        Assert.Equal(481.5f / 1280f, uv0.X, 6);
        Assert.Equal(389.5f / 720f, uv0.Y, 6);
        Assert.Equal(798.5f / 1280f, uv1.X, 6);
        Assert.Equal(719.5f / 720f, uv1.Y, 6);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1280, 534)]
    [InlineData(1440, 740)]
    public void At_density_two_zone_c_samples_the_same_rectangle_half_a_dense_texel_inside(int width, int height)
    {
        var placements = VrHudZones.ClassicLayout(width, height, out var reason);
        Assert.True(placements is not null, reason);
        var pane = Assert.Single(placements!, p => p.Cutout.Zone == VrHudZones.ZoneId.Target).Cutout.Source;
        var (uv0, uv1) = VrHudZones.SourceUv(pane, width, height, 2);
        // In the dense image's own pixels: the layout rectangle doubled, half a pixel in.
        Assert.Equal(pane.Left * 2 + 0.5f, uv0.X * width * 2, 3);
        Assert.Equal(pane.Top * 2 + 0.5f, uv0.Y * height * 2, 3);
        Assert.Equal(pane.Right * 2 - 0.5f, uv1.X * width * 2, 3);
        Assert.Equal(pane.Bottom * 2 - 0.5f, uv1.Y * height * 2, 3);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1280, 534)]
    [InlineData(1440, 740)]
    [InlineData(1486, 534)]
    [InlineData(1552, 760)]
    public void Zone_c_holds_all_the_pane_draws_in_vr_and_the_notices_end_where_it_begins(int width, int height)
    {
        // Review round 5, F1: VR draws the model preview 1.5 times its size,
        // grown upward, so its top is above the pane's name line.
        var placements = VrHudZones.ClassicLayout(width, height, out var reason);
        Assert.True(placements is not null, reason);
        var pane = Assert.Single(placements!, p => p.Cutout.Zone == VrHudZones.ZoneId.Target).Cutout.Source;
        var notices = Assert.Single(placements!, p => p.Cutout.Name == "target notices").Cutout.Source;
        float s = TargetPaneLayout.Scale(width, height);
        var preview = TargetModelPreview.PaneRect(TargetPaneLayout.OriginX(width, height),
            TargetPaneLayout.OriginY(width, height), s, HudRenderer.VrTargetPreviewEnlargement);
        Assert.True(preview.Top < TargetPaneLayout.TopY(width, height), "the enlarged preview rises above the name line");
        Assert.True(pane.Contains(preview), $"target cut-out {pane} holds the preview {preview}");
        Assert.True(pane.Top <= TargetPaneLayout.TopY(width, height));
        Assert.Equal(pane.Top, notices.Bottom);
    }

    [Fact]
    public void In_vr_the_notices_stay_above_the_panes_place_without_a_target()
    {
        // Review round 5, F2: at the foot they would be inside zone C's cut-out.
        Assert.True(HudRenderer.NoticesAbovePane(Model(withPane: false)));
        Assert.True(HudRenderer.NoticesAbovePane(Model(withPane: true)));
    }

    [Fact]
    public void In_the_flat_game_the_notices_go_to_the_foot_only_without_a_target()
    {
        Assert.False(HudRenderer.NoticesAbovePane(Model(withPane: false, vr: false)));
        Assert.True(HudRenderer.NoticesAbovePane(Model(withPane: true, vr: false)));
    }

    private static HudModel Model(bool withPane, bool vr = true) =>
        HudModelAdapter.Create(Frame(), CameraMode.Cockpit, centreHudVisible: true, targetHudVisible: true,
            centreReticleInWorld: vr, flightReferenceInWorld: vr,
            targetPane: withPane
                ? new HudTargetPane(Present: true, Name: "Far hull", TypeDescription: "fighter",
                    SideName: "Rixian", SideColor: TargetPaneModel.Neutral, HullFraction: .5f, ShieldFraction: .4f,
                    Distance: 1250f, Speed: 43f, OreFraction: null, KillBonusPercent: null, InRange: false, IsShip: true,
                    ClosingSpeed: 0f)
                : null);

    private static HudFrame Frame() => new(
        .8f, .6f, .4f, .3f, .2f, 1f, 260f, .6f, false, false, true, 0,
        false, 0f, false, false, 0f, 0f, Array.Empty<WeaponStatusRow>(), false, 0);
}
