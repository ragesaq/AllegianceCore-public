using Allegiance.ClientCore.MonoGame.Flight;
using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.Core.Sim;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>VR HUD build step 2 (knowledge/vr-hud-design.md, layer 3): the VR
/// client shows the Classic centre group (gauge ring, speed, own-ship status)
/// at infinity, so in a VR eye frame the Classic and Modern main passes leave it
/// out. A flat frame keeps it.</summary>
public sealed class VrCentreGroupTests
{
    [Fact]
    public void A_flat_frame_draws_the_centre_group_on_the_main_pass()
    {
        Assert.True(HudRenderer.CentreGroupOnMainPass(Model(inWorld: false, centreHud: true)));
    }

    [Fact]
    public void A_vr_eye_frame_leaves_the_centre_group_to_the_block_at_infinity()
    {
        Assert.False(HudRenderer.CentreGroupOnMainPass(Model(inWorld: true, centreHud: true)));
    }

    [Fact]
    public void A_vr_eye_frame_shows_no_roll_dial_and_a_flat_frame_keeps_it()
    {
        Assert.True(HudRenderer.RollOnMainPass(Model(inWorld: false, centreHud: true)));
        Assert.False(HudRenderer.RollOnMainPass(Model(inWorld: true, centreHud: true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_hidden_centre_hud_is_drawn_in_neither_frame(bool inWorld)
    {
        Assert.False(HudRenderer.CentreGroupOnMainPass(Model(inWorld, centreHud: false)));
    }

    private static HudModel Model(bool inWorld, bool centreHud) =>
        HudModelAdapter.Create(Frame(), CameraMode.Cockpit, centreHudVisible: centreHud,
            centreReticleInWorld: inWorld, flightReferenceInWorld: inWorld);

    private static HudFrame Frame() => new(
        .8f, .6f, .4f, .3f, .2f, 1f, 260f, .6f, false, false, true, 0,
        false, 0f, false, false, 0f, 0f, Array.Empty<WeaponStatusRow>(), false, 0);
}
