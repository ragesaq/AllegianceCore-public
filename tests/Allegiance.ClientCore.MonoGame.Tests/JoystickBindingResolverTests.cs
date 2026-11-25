using Allegiance.ClientCore.MonoGame.Flight;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>
/// Pure-logic tests for <see cref="JoystickBindingResolver"/> and
/// <see cref="JoystickBindingFile"/>: per-device bindings for a split HOTAS, the
/// fallback to the port's earlier single-device layout, and buttons held at start.
/// Device names are the SDL names read out on real hardware
/// (<c>knowledge/joystick-input.md</c>, "Device readout").
/// </summary>
public class JoystickBindingResolverTests
{
    private const string Stick = "VKB-Sim Gunfighter Modern Combat Pro Twist";
    private const string Throttle = "LEFT VPC Throttle MT-50CM2";
    private const string Pedals = "Heusinkveld Sim Pedals Sprint";
    private const string UnknownPad = "Some Other Controller";

    private static JoystickDeviceSample Device(int index, string name, int axisCount, int buttonCount,
        (int Axis, int Value)[]? axes = null, int[]? pressed = null)
    {
        var a = new int[axisCount];
        foreach (var (axis, value) in axes ?? []) a[axis] = value;
        var b = new bool[buttonCount];
        foreach (var i in pressed ?? []) b[i] = true;
        return new JoystickDeviceSample(index, name, a, b);
    }

    private static JoystickDeviceSample StickAt(int index, (int, int)[]? axes = null, int[]? pressed = null) =>
        Device(index, Stick, 8, 128, axes, pressed);

    private static JoystickDeviceSample ThrottleAt(int index, (int, int)[]? axes = null, int[]? pressed = null) =>
        Device(index, Throttle, 6, 79, axes, pressed);

    private static JoystickDeviceSample PedalsAt(int index, (int, int)[]? axes = null) =>
        Device(index, Pedals, 3, 0, axes);

    // ---- profiles ----

    [Fact]
    public void BuiltInProfiles_MatchTheReadOutDevices_AndNotThePedals()
    {
        var r = new JoystickBindingResolver();
        Assert.Equal("VKB-Sim Gunfighter", r.ProfileFor(Stick)?.Match);
        Assert.Equal("VPC Throttle", r.ProfileFor(Throttle)?.Match);
        Assert.Null(r.ProfileFor(Pedals));
        Assert.Null(r.ProfileFor(UnknownPad));
        Assert.Equal("VPC Throttle", r.ProfileFor("left vpc throttle mt-50cm2")?.Match);
    }

    [Fact]
    public void EmptyMatch_NeverMatches()
    {
        Assert.False(JoystickBindingResolver.Legacy.Matches(Stick));
    }

    // ---- split HOTAS ----

    [Fact]
    public void StickAndThrottle_EachFeedTheirOwnRoles()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([
            StickAt(0, [(0, 16384), (1, -32767), (2, 32767), (3, 32767)]),
            ThrottleAt(1, [(0, 32767), (1, 32767), (3, 32767)]),
            PedalsAt(2, [(0, 32767)]),
        ]);

        Assert.True(f.AnyBound);
        Assert.Equal(16384 / 32767f, f.Axes.Yaw, 5);
        Assert.Equal(-1f, f.Axes.Pitch, 5);
        Assert.Equal(1f, f.Axes.Roll, 5);
        // Throttle lever 3 at its rest end (+32767) reads idle after the inversion;
        // the stick's axis 3 and the throttle's own axes 0 and 1 feed nothing.
        Assert.Equal(-1f, f.Axes.Throttle, 5);
    }

    [Fact]
    public void ThrottleLeverFullForward_ReadsFull()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([StickAt(0), ThrottleAt(1, [(3, -32767)])]);
        Assert.Equal(1f, f.Axes.Throttle, 5);
    }

    [Fact]
    public void DeviceOrder_DoesNotMatter()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([
            PedalsAt(0, [(0, 32767)]),
            ThrottleAt(1, [(3, -32767)]),
            StickAt(2, [(0, 32767)]),
        ]);
        Assert.Equal(1f, f.Axes.Yaw, 5);
        Assert.Equal(1f, f.Axes.Throttle, 5);
    }

    [Fact]
    public void UnmatchedDevice_IsIgnored_WhenAnotherDeviceMatches()
    {
        var r = new JoystickBindingResolver();
        // The pedals come first in SDL order and press their axis fully; nothing of
        // theirs may reach a role, and the legacy layout must not be applied to them.
        var f = r.Resolve([PedalsAt(0, [(0, 32767), (1, 32767), (2, 32767)]), StickAt(1)]);
        Assert.True(f.AnyBound);
        Assert.Equal(new JoystickAxes(0f, 0f, 0f, 0f), f.Axes);
    }

    [Fact]
    public void StickAlone_LeavesThrottleAtZero()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([StickAt(0, [(1, 32767)])]);
        Assert.Equal(1f, f.Axes.Pitch, 5);
        Assert.Equal(0f, f.Axes.Throttle, 5);
    }

    // ---- legacy fallback ----

    [Fact]
    public void SingleUnknownDevice_UsesTheEarlierLayout_SameAsGatherRoles()
    {
        int[] raw = [1000, -2000, 3000, -4000, 5000, 6000];
        var r = new JoystickBindingResolver();
        var f = r.Resolve([new JoystickDeviceSample(0, UnknownPad, raw, new bool[4])]);
        Assert.True(f.AnyBound);
        Assert.Equal(JoystickFlightController.GatherRoles(raw, JoystickAxisLayout.DjiFpvRc3), f.Axes);
    }

    [Fact]
    public void TwoUnknownDevices_OnlyTheFirstIsRead()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([
            Device(0, UnknownPad, 4, 4),
            Device(1, "Another Unknown", 4, 4, [(0, 32767), (1, 32767), (2, 32767), (3, 32767)], [1, 2]),
        ]);
        Assert.Equal(new JoystickAxes(0f, 0f, 0f, 0f), f.Axes);
        Assert.False(f.Fire);
        Assert.False(f.Booster);
    }

    [Fact]
    public void NoDevices_NothingBound()
    {
        var f = new JoystickBindingResolver().Resolve([]);
        Assert.False(f.AnyBound);
        Assert.Equal(default, f.Axes);
    }

    [Fact]
    public void Legacy_FireAndBoosterButtons()
    {
        var r = new JoystickBindingResolver();
        r.Resolve([Device(0, UnknownPad, 4, 4)]);
        var f = r.Resolve([Device(0, UnknownPad, 4, 4, pressed: [JoystickFlightController.FireButton, JoystickFlightController.BoosterButton])]);
        Assert.True(f.Fire);
        Assert.True(f.Booster);
        Assert.False(f.FireMissile);
    }

    // ---- buttons ----

    [Fact]
    public void StickTrigger_FirstStageFires_SecondStageIsUnbound()
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0), ThrottleAt(1)]);
        var first = r.Resolve([StickAt(0, pressed: [0]), ThrottleAt(1)]);
        Assert.True(first.Fire);
        Assert.False(first.FireMissile);
        var second = r.Resolve([StickAt(0, pressed: [0, 1]), ThrottleAt(1)]);
        Assert.True(second.Fire);
        Assert.False(second.FireMissile);
        Assert.False(second.Booster);
    }

    [Theory]
    [InlineData(3, JoystickCommand.FireMissile)]
    [InlineData(4, JoystickCommand.DropChaff)]
    [InlineData(5, JoystickCommand.Ripcord)]
    public void StickThumbButtons_LeftToRight_AreMissileChaffRipcord(int button, JoystickCommand expected)
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0), ThrottleAt(1)]);
        var f = r.Resolve([StickAt(0, pressed: [button]), ThrottleAt(1)]);
        Assert.Equal(1 << (int)expected, f.HeldMask);
    }

    [Fact]
    public void UserProfile_CanBindTheSecondStage()
    {
        var (profiles, error) = JoystickBindingFile.Parse("""
            { "devices": [ { "match": "Gunfighter",
                "axes": [ { "role": "Yaw", "axis": 0 }, { "role": "Pitch", "axis": 1 }, { "role": "Roll", "axis": 2 } ],
                "buttons": [ { "command": "Fire", "button": 0 }, { "command": "FireMissile", "button": 1 } ] } ] }
            """);
        Assert.Null(error);
        var r = new JoystickBindingResolver(profiles);
        r.Resolve([StickAt(0)]);
        Assert.True(r.Resolve([StickAt(0, pressed: [0, 1])]).FireMissile);
    }

    [Fact]
    public void ButtonHeldAtStart_DoesNotFire_UntilReleasedAndPressedAgain()
    {
        var r = new JoystickBindingResolver();
        Assert.False(r.Resolve([StickAt(0, pressed: [0])]).Fire);
        Assert.False(r.Resolve([StickAt(0, pressed: [0])]).Fire);
        Assert.False(r.Resolve([StickAt(0)]).Fire);
        Assert.True(r.Resolve([StickAt(0, pressed: [0])]).Fire);
    }

    [Fact]
    public void Reconnect_ForgetsTheArmedState()
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0)]);
        Assert.True(r.Resolve([StickAt(0, pressed: [0])]).Fire);
        r.Resolve([]); // unplugged
        Assert.False(r.Resolve([StickAt(0, pressed: [0])]).Fire);
    }

    [Fact]
    public void LatchedThrottleSwitches_DoNotFireAUserBinding()
    {
        // Throttle buttons 35 and 36 read pressed at rest on real hardware.
        var profiles = new[]
        {
            new JoystickDeviceProfile("VPC Throttle", [], [new(JoystickCommand.Booster, 35)]),
        };
        var r = new JoystickBindingResolver(profiles);
        Assert.False(r.Resolve([ThrottleAt(0, pressed: [35, 36])]).Booster);
    }

    [Fact]
    public void OutOfRangeIndices_AreNeutral()
    {
        var profiles = new[]
        {
            new JoystickDeviceProfile("Small", [new(JoystickRole.Yaw, 9), new(JoystickRole.Pitch, -1)],
                [new(JoystickCommand.Fire, 9)]),
        };
        var r = new JoystickBindingResolver(profiles);
        r.Resolve([Device(0, "Small", 2, 2)]);
        var f = r.Resolve([Device(0, "Small", 2, 2, [(0, 32767), (1, 32767)], [0, 1])]);
        Assert.Equal(new JoystickAxes(0f, 0f, 0f, 0f), f.Axes);
        Assert.False(f.Fire);
    }

    // ---- user profiles ----

    [Fact]
    public void UserProfile_WinsOverBuiltIn()
    {
        var (profiles, error) = JoystickBindingFile.Parse("""
            { "devices": [ { "match": "Gunfighter",
                "axes": [ { "role": "Roll", "axis": 0 }, { "role": "Yaw", "axis": 2, "invert": true } ] } ] }
            """);
        Assert.Null(error);
        var r = new JoystickBindingResolver(profiles);
        Assert.Equal("Gunfighter", r.ProfileFor(Stick)?.Match);
        var f = r.Resolve([StickAt(0, [(0, 32767), (2, 32767)])]);
        Assert.Equal(1f, f.Axes.Roll, 5);
        Assert.Equal(-1f, f.Axes.Yaw, 5);
    }

    [Fact]
    public void UserProfile_MakesPedalsBindable()
    {
        var (profiles, error) = JoystickBindingFile.Parse("""
            // rudder pedals for yaw
            { "Devices": [ { "Match": "Sim Pedals", "Axes": [ { "Role": "yaw", "Axis": 0, }, ], }, ], }
            """);
        Assert.Null(error);
        var r = new JoystickBindingResolver(profiles);
        var f = r.Resolve([StickAt(0), ThrottleAt(1), PedalsAt(2, [(0, 32767)])]);
        // The stick binds yaw too and comes first in SDL order, so it wins the axis.
        Assert.Equal(0f, f.Axes.Yaw, 5);
        var alone = r.Resolve([PedalsAt(0, [(0, 32767)])]);
        Assert.Equal(1f, alone.Axes.Yaw, 5);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "devices": [ { "axes": [] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "  " } ] }""")]
    [InlineData("""{ "devices": [ { "match": "X", "axes": [ { "role": "Sideways", "axis": 0 } ] } ] }""")]
    // Review r1 F2: null entries parsed silently and crashed the first flight update.
    [InlineData("""{ "devices": [ null ] }""")]
    [InlineData("""{ "devices": [ { "match": "Stick", "axes": [ null ] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "Stick", "buttons": [ null ] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "Stick", "axisCommands": [ null ] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "X", "axes": [ { "role": 7, "axis": 0 } ] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "X", "axes": [ { "role": "Yaw", "axis": 99999999999 } ] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "X", "axisCommands": [ { "axis": 0, "negative": "StrafeLeft", "positive": "StrafeRight", "threshold": 0 } ] } ] }""")]
    [InlineData("""{ "devices": [ { "match": "X", "axisCommands": [ { "axis": 0, "negative": "StrafeLeft", "positive": "StrafeRight", "threshold": 1.5 } ] } ] }""")]
    public void BadFile_IsRejectedWithAReason(string json)
    {
        var (profiles, error) = JoystickBindingFile.Parse(json);
        Assert.Empty(profiles);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void AxisCommands_ParseWithTheDefaultThreshold()
    {
        var (profiles, error) = JoystickBindingFile.Parse("""
            { "devices": [ { "match": "Pad",
                "axisCommands": [ { "axis": 2, "negative": "StrafeDown", "positive": "StrafeUp" } ],
                "buttons": [ { "command": "Ripcord", "button": 3 } ] } ] }
            """);
        Assert.Null(error);
        var c = Assert.Single(profiles[0].AxisCommands!);
        Assert.Equal(new JoystickAxisCommandBinding(2, JoystickCommand.StrafeDown, JoystickCommand.StrafeUp, 0.5f), c);
        Assert.Equal(new JoystickButtonBinding(JoystickCommand.Ripcord, 3), Assert.Single(profiles[0].Buttons));
    }

    // ---- side thrusters on the throttle's index-finger stick ----

    [Theory]
    [InlineData(0, -32767, JoystickCommand.StrafeLeft)]
    [InlineData(0, 32767, JoystickCommand.StrafeRight)]
    [InlineData(1, -32767, JoystickCommand.StrafeUp)]
    [InlineData(1, 32767, JoystickCommand.StrafeDown)]
    [InlineData(0, -16384, JoystickCommand.StrafeLeft)]  // just past half
    public void ThrottleThumbStick_DrivesTheSideThrusters(int axis, int value, JoystickCommand expected)
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([StickAt(0), ThrottleAt(1, [(axis, value), (3, 32767)])]);
        Assert.True(f.Holds(expected));
        foreach (var other in new[] { JoystickCommand.StrafeLeft, JoystickCommand.StrafeRight, JoystickCommand.StrafeUp, JoystickCommand.StrafeDown })
            if (other != expected) Assert.False(f.Holds(other));
        // The thumb stick feeds no flight role.
        Assert.Equal(0f, f.Axes.Yaw, 5);
        Assert.Equal(0f, f.Axes.Pitch, 5);
        Assert.Equal(-1f, f.Axes.Throttle, 5);
    }

    [Fact]
    public void ThrottleThumbStick_InsideHalfDeflection_HoldsNothing()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([ThrottleAt(0, [(0, 16000), (1, -16000)])]);
        Assert.Equal(0, f.HeldMask);
    }

    [Fact]
    public void ThrottleThumbStick_Diagonal_HoldsTwoThrusters()
    {
        var r = new JoystickBindingResolver();
        var f = r.Resolve([ThrottleAt(0, [(0, 30000), (1, -30000)])]);
        Assert.True(f.StrafeRight);
        Assert.True(f.StrafeUp);
        Assert.False(f.StrafeLeft);
        Assert.False(f.StrafeDown);
    }

    [Fact]
    public void Ripcord_IsAButtonCommand()
    {
        var profiles = new[] { new JoystickDeviceProfile("VKB-Sim Gunfighter", [], [new(JoystickCommand.Ripcord, 4)]) };
        var r = new JoystickBindingResolver(profiles);
        r.Resolve([StickAt(0)]);
        Assert.True(r.Resolve([StickAt(0, pressed: [4])]).Ripcord);
    }

    // ---- review r1 F1: the throttle device going away ----

    [Fact]
    public void ThrottleBound_FollowsTheThrottleDevice()
    {
        var r = new JoystickBindingResolver();
        Assert.True(r.Resolve([StickAt(0), ThrottleAt(1)]).ThrottleBound);
        var stickOnly = r.Resolve([StickAt(0)]);
        Assert.True(stickOnly.AnyBound);
        Assert.False(stickOnly.ThrottleBound);
    }

    [Fact]
    public void ThrottleUnplugged_ResetGivesTheKeyboardThrottleBack()
    {
        // The call site resets the controller when ThrottleBound goes false; after
        // that a throttle role stuck at 0 must not override the keyboard.
        var r = new JoystickBindingResolver();
        var c = new JoystickFlightController();
        c.Update(r.Resolve([StickAt(0), ThrottleAt(1, [(3, 32767)])]).Axes);
        Assert.NotNull(c.Update(r.Resolve([StickAt(0), ThrottleAt(1, [(3, -32767)])]).Axes).ThrottleOverride);

        var lost = r.Resolve([StickAt(0)]);
        Assert.False(lost.ThrottleBound);
        // Without the reset the latch keeps overriding with the frozen 0.
        var unreset = new JoystickFlightController();
        unreset.Update(new JoystickAxes(0f, 0f, 0f, -1f));
        unreset.Update(new JoystickAxes(0f, 0f, 0f, 1f));
        Assert.Equal(0f, unreset.Update(lost.Axes).ThrottleOverride);

        c.Reset();
        for (int i = 0; i < 3; i++)
            Assert.Null(c.Update(r.Resolve([StickAt(0)]).Axes).ThrottleOverride);
    }

    // ---- review r1 F3: armed state when one device leaves and another arrives ----

    [Fact]
    public void DeviceSwappedInOneUpdate_StillForgetsTheArmedState()
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0)]);
        Assert.True(r.Resolve([StickAt(0, pressed: [0])]).Fire);
        r.Resolve([ThrottleAt(0)]);                      // stick gone, throttle at the same index
        Assert.False(r.Resolve([StickAt(0, pressed: [0])]).Fire);
    }

    [Fact]
    public void Disarm_HeldButtonActsOnlyAfterARelease()
    {
        // Review r2 N1: a thumb button held through launch must not ripcord on the
        // first flight frame.
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0)]);
        Assert.True(r.Resolve([StickAt(0, pressed: [5])]).Ripcord);
        r.Disarm();
        Assert.False(r.Resolve([StickAt(0, pressed: [5])]).Ripcord);
        Assert.False(r.Resolve([StickAt(0)]).Ripcord);
        Assert.True(r.Resolve([StickAt(0, pressed: [5])]).Ripcord);
    }

    [Fact]
    public void ButtonCountChange_StartsFromRestAgain()
    {
        var profiles = new[] { new JoystickDeviceProfile("Pad", [], [new(JoystickCommand.Fire, 0)]) };
        var r = new JoystickBindingResolver(profiles);
        r.Resolve([Device(0, "Pad", 2, 4)]);
        Assert.True(r.Resolve([Device(0, "Pad", 2, 4, pressed: [0])]).Fire);
        Assert.False(r.Resolve([Device(0, "Pad", 2, 8, pressed: [0])]).Fire);
    }

    // ---- review r1 F4: pedals left connected after the HOTAS goes away ----

    [Fact]
    public void AfterAProfileMatched_LeftoverPedalsAreNotReadAsAStick()
    {
        var r = new JoystickBindingResolver();
        Assert.True(r.LegacyAllowed);
        r.Resolve([StickAt(0), ThrottleAt(1), PedalsAt(2)]);
        Assert.False(r.LegacyAllowed);
        var f = r.Resolve([PedalsAt(0, [(0, 32767), (1, 32767), (2, 32767)])]);
        Assert.False(f.AnyBound);
        Assert.Equal(default, f.Axes);
    }

    [Fact]
    public void EmptyFile_GivesNoProfiles()
    {
        var (profiles, error) = JoystickBindingFile.Parse("{}");
        Assert.Null(error);
        Assert.Empty(profiles);
    }

    // ---- targeting: commands named like the key actions they press ----

    [Fact]
    public void Commands_FitTheHeldMask()
        => Assert.True(Enum.GetValues<JoystickCommand>().Length <= 31);

    [Fact]
    public void KeyActionCommands_AreCatalogActionsTheGameDispatches()
    {
        var catalog = Rendering.InputActionCatalog.Definitions.Select(d => d.Name).ToHashSet();
        var dispatched = Game1.RemainderTargetActions.Select(a => a.Id)
            .Concat(["TargetCenter", "TargetNothing", "TargetHostile", "ToggleAutopilot"]).ToHashSet();
        Assert.NotEmpty(JoystickKeyActions.Names);
        foreach (var name in JoystickKeyActions.Names)
        {
            Assert.Contains(name, catalog);
            Assert.Contains(name, dispatched);
        }
    }

    [Fact]
    public void FlightCommands_AreNotKeyActions()
    {
        Assert.False(JoystickKeyActions.TryGet("Fire", out _));
        Assert.False(JoystickKeyActions.TryGet("Ripcord", out _));
        Assert.True(JoystickKeyActions.TryGet("TargetCenter", out var c));
        Assert.Equal(JoystickCommand.TargetCenter, c);
    }

    // Capture 2026-11-25 (knowledge/joystick-input.md): Virpil's diagram controls in press order.
    [Theory]
    [InlineData(32, "TargetNothing")]             // 15
    [InlineData(20, "TargetCenter")]              // 16
    [InlineData(31, "ToggleAutopilot")]           // 21
    [InlineData(29, "TargetEnemy")]               // castle 20 up
    [InlineData(30, "Target")]                    // castle 20 right
    [InlineData(27, "TargetEnemyPrev")]           // castle 20 down
    [InlineData(28, "TargetHostile")]             // castle 20 left
    [InlineData(26, "TargetEnemyNearest")]        // castle 20 centre
    [InlineData(24, "TargetFriendly")]            // castle 19 up
    [InlineData(25, "TargetWarpNearest")]         // castle 19 right
    [InlineData(22, "TargetFriendlyPrev")]        // castle 19 down
    [InlineData(23, "TargetFriendlyBaseNearest")] // castle 19 left
    [InlineData(21, "TargetFriendlyNearest")]     // castle 19 centre
    public void ThrottleGrip_TargetingButtons(int button, string action)
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0), ThrottleAt(1)]);
        var f = r.Resolve([StickAt(0), ThrottleAt(1, pressed: [button])]);
        Assert.True(f.HoldsAction(action));
        Assert.True(JoystickKeyActions.TryGet(action, out var command));
        Assert.Equal(1 << (int)command, f.HeldMask);
    }

    [Fact]
    public void ThrottleRestButtons_AreNotBound()
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0), ThrottleAt(1)]);
        Assert.Equal(0, r.Resolve([StickAt(0), ThrottleAt(1, pressed: [34, 35, 36, 43])]).HeldMask);
    }

    [Fact]
    public void UserProfile_BindsATargetingButton_HeldByActionName()
    {
        var (profiles, error) = JoystickBindingFile.Parse("""
            { "devices": [ { "match": "VPC Throttle", "axes": [],
                "buttons": [ { "command": "TargetCenter", "button": 21 }, { "command": "TargetEnemyNearest", "button": 22 } ] } ] }
            """);
        Assert.Null(error);
        var r = new JoystickBindingResolver(profiles);
        r.Resolve([ThrottleAt(1)]);
        var f = r.Resolve([ThrottleAt(1, pressed: [21])]);
        Assert.True(f.HoldsAction("TargetCenter"));
        Assert.False(f.HoldsAction("TargetEnemyNearest"));
        Assert.False(f.HoldsAction("NotAnAction"));
        Assert.False(r.Resolve([ThrottleAt(1)]).HoldsAction("TargetCenter"));
    }
}
