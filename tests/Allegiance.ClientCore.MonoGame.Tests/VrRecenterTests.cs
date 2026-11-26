using Allegiance.ClientCore.Vr;
using Microsoft.Xna.Framework;
using Xunit;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The seated origin a recenter makes: the head's position and only its
/// turn about the vertical axis, so the horizon stays level.</summary>
public sealed class VrRecenterTests
{
    private static float YawDegrees(Quaternion q)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, q);
        return MathHelper.ToDegrees(MathF.Atan2(-forward.X, -forward.Z));
    }

    [Fact]
    public void HeadLookingAhead_GivesNoTurn()
    {
        var (yaw, position) = OpenXrPresenter.SeatedOrigin(Quaternion.Identity, new Vector3(0.1f, 1.2f, -0.3f));
        Assert.Equal(0f, YawDegrees(yaw), 3);
        Assert.Equal(new Vector3(0.1f, 1.2f, -0.3f), position);
    }

    [Theory]
    [InlineData(30f, 0f)]
    [InlineData(-75f, 20f)]
    [InlineData(120f, -40f)]
    public void HeadTurnedAndTilted_KeepsOnlyTheTurn(float turnDegrees, float pitchDegrees)
    {
        // The tilt is about the head's own sideways axis: turn, then tilt in the turned frame.
        var head = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(turnDegrees))
                 * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(pitchDegrees));
        var (yaw, _) = OpenXrPresenter.SeatedOrigin(head, Vector3.Zero);
        Assert.Equal(turnDegrees, YawDegrees(yaw), 2);
        var up = Vector3.Transform(Vector3.UnitY, yaw);
        Assert.Equal(1f, up.Y, 4);
    }

    [Fact]
    public void HeadLookingStraightUp_GivesNoTurn()
    {
        var head = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver2);
        var (yaw, _) = OpenXrPresenter.SeatedOrigin(head, Vector3.Zero);
        Assert.Equal(Quaternion.Identity, yaw);
    }
}
