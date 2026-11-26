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
    public void HeadTurnedLeft90_FacesTheSpaceAlongMinusX()
    {
        // Stated without the implementation's formula: a quarter turn left about +Y
        // carries the OpenXR forward (-Z) onto -X.
        var head = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2)
                 * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(25f));
        var (yaw, _) = OpenXrPresenter.SeatedOrigin(head, Vector3.Zero);
        float s = MathF.Sqrt(0.5f);
        Assert.Equal(1f, MathF.Abs(Quaternion.Dot(yaw, new Quaternion(0f, s, 0f, s))), 4);
        var forward = Vector3.Transform(-Vector3.UnitZ, yaw);
        Assert.Equal(-1f, forward.X, 4);
        Assert.Equal(0f, forward.Y, 4);
        Assert.Equal(0f, forward.Z, 4);
    }

    [Fact]
    public void NaNPose_IsNotFinite()
    {
        Assert.True(OpenXrPresenter.IsFinite(Quaternion.Identity, Vector3.Zero));
        Assert.False(OpenXrPresenter.IsFinite(new Quaternion(float.NaN, 0f, 0f, 1f), Vector3.Zero));
        Assert.False(OpenXrPresenter.IsFinite(Quaternion.Identity, new Vector3(0f, float.PositiveInfinity, 0f)));
    }

    [Fact]
    public void PressWhileUntracked_WaitsThenRunsOut()
    {
        var request = new VrRecenterRequest();
        Assert.False(request.Pending);
        request.Press();
        for (int i = 1; i < VrRecenterRequest.WaitFrames; i++)
        {
            Assert.False(request.Missed());
            Assert.True(request.Pending);
        }
        Assert.True(request.Missed());
        Assert.False(request.Pending);
        Assert.False(request.Missed());
    }

    [Fact]
    public void TrackingReturns_TheWaitingPressIsCarriedOutOnce()
    {
        var request = new VrRecenterRequest();
        request.Press();
        request.Missed();
        request.Missed();
        Assert.True(request.Pending);
        request.Done();
        Assert.False(request.Pending);
        request.Press();
        Assert.True(request.Pending);
    }

    [Fact]
    public void HeadLookingStraightUp_GivesNoTurn()
    {
        var head = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver2);
        var (yaw, _) = OpenXrPresenter.SeatedOrigin(head, Vector3.Zero);
        Assert.Equal(Quaternion.Identity, yaw);
    }
}
