using System.Collections.Generic;
using SimVec = Allegiance.Core.Collision.Vector3;
using Xunit;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>TK_TargetCenter picks by angle off the nose, as native's
/// <c>FindTarget(c_ttFront | c_ttAllTypes | c_ttAllSides)</c> does
/// (WinTrek.cpp:10127-10151, common.cpp:509-640): any kind of object, no
/// ship-first tier.</summary>
public class TargetCenterFrontTests
{
    private static readonly SimVec Origin = new(0, 0, 0);
    private static readonly SimVec Forward = new(0, 0, -1);

    private static Game1.CandidateObject Obj(Game1.TargetKind kind, short id, float x, float y, float z) =>
        new(new SimVec(x, y, z), kind, id, $"{kind}{id}");

    [Fact]
    public void StationOnTheNose_BeatsAShipOffToTheSide()
    {
        var objs = new List<Game1.CandidateObject>
        {
            Obj(Game1.TargetKind.Ship, 1, 300, 0, -400),
            Obj(Game1.TargetKind.Station, 2, 5, 0, -3000),
        };
        var picked = Game1.PickFrontTarget(objs, Origin, Forward, null);
        Assert.Equal((Game1.TargetKind.Station, (short)2), (picked!.Value.Kind, picked.Value.Id));
    }

    [Fact]
    public void AngleDecides_NotDistance()
    {
        var objs = new List<Game1.CandidateObject>
        {
            Obj(Game1.TargetKind.Asteroid, 1, 50, 0, -100),
            Obj(Game1.TargetKind.Asteroid, 2, 50, 0, -2000),
        };
        Assert.Equal((short)2, Game1.PickFrontTarget(objs, Origin, Forward, null)!.Value.Id);
    }

    [Fact]
    public void ObjectBehind_IsPickedOnlyWhenNothingIsAhead()
    {
        var behind = Obj(Game1.TargetKind.Ship, 1, 0, 0, 500);
        var wide = Obj(Game1.TargetKind.Warp, 2, 1000, 0, -10);
        Assert.Equal((short)2, Game1.PickFrontTarget(new[] { behind, wide }, Origin, Forward, null)!.Value.Id);
        Assert.Equal((short)1, Game1.PickFrontTarget(new[] { behind }, Origin, Forward, null)!.Value.Id);
    }

    [Fact]
    public void RepeatPress_StepsPastTheCurrentTarget()
    {
        var objs = new List<Game1.CandidateObject>
        {
            Obj(Game1.TargetKind.Ship, 1, 0, 0, -1000),
            Obj(Game1.TargetKind.Ship, 2, 100, 0, -1000),
        };
        var first = Game1.PickFrontTarget(objs, Origin, Forward, null);
        Assert.Equal((short)1, first!.Value.Id);
        Assert.Equal((short)2, Game1.PickFrontTarget(objs, Origin, Forward, first)!.Value.Id);
    }

    [Fact]
    public void CurrentTargetAlone_IsKeptWithTheDefaultCommand()
    {
        var objs = new[] { Obj(Game1.TargetKind.Ship, 7, 0, 0, -100) };
        var current = new Game1.AutopilotTarget(Game1.TargetKind.Ship, 7, "Ship7", UseDefault: false);
        var picked = Game1.PickFrontTarget(objs, Origin, Forward, current);
        Assert.Equal((short)7, picked!.Value.Id);
        Assert.True(picked.Value.UseDefault);
    }

    [Fact]
    public void GunnersCarrier_IsNeverPicked()
    {
        var objs = new List<Game1.CandidateObject>
        {
            Obj(Game1.TargetKind.Ship, 5, 0, 0, -10),
            Obj(Game1.TargetKind.Station, 6, 400, 0, -1000),
        };
        Assert.Equal((Game1.TargetKind.Station, (short)6),
            (Game1.PickFrontTarget(objs, Origin, Forward, null, excludeShipId: 5)!.Value.Kind,
             Game1.PickFrontTarget(objs, Origin, Forward, null, excludeShipId: 5)!.Value.Id));
        Assert.Null(Game1.PickFrontTarget(new[] { objs[0] }, Origin, Forward, null, excludeShipId: 5));
    }

    [Fact]
    public void ExcludingTheCarrier_KeepsAnotherKindWithTheSameId()
    {
        var objs = new[] { Obj(Game1.TargetKind.Ship, 5, 0, 0, -10), Obj(Game1.TargetKind.Station, 5, 400, 0, -1000) };
        var picked = Game1.PickFrontTarget(objs, Origin, Forward, null, excludeShipId: 5);
        Assert.Equal((Game1.TargetKind.Station, (short)5), (picked!.Value.Kind, picked.Value.Id));
    }

    [Fact]
    public void CarrierAsTheCurrentTarget_IsNotKept()
    {
        var current = new Game1.AutopilotTarget(Game1.TargetKind.Ship, 5, "Ship5");
        Assert.Null(Game1.PickFrontTarget(new[] { Obj(Game1.TargetKind.Ship, 5, 0, 0, -10) }, Origin, Forward, current, excludeShipId: 5));
    }

    [Fact]
    public void NoObjects_PicksNothing() =>
        Assert.Null(Game1.PickFrontTarget(new List<Game1.CandidateObject>(), Origin, Forward, null));
}
