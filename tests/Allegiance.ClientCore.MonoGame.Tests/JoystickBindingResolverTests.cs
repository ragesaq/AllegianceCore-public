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
    public void StickTriggerStages_FireThenMissile()
    {
        var r = new JoystickBindingResolver();
        r.Resolve([StickAt(0), ThrottleAt(1)]);
        var first = r.Resolve([StickAt(0, pressed: [0]), ThrottleAt(1)]);
        Assert.True(first.Fire);
        Assert.False(first.FireMissile);
        var second = r.Resolve([StickAt(0, pressed: [0, 1]), ThrottleAt(1)]);
        Assert.True(second.Fire);
        Assert.True(second.FireMissile);
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
    public void BadFile_IsRejectedWithAReason(string json)
    {
        var (profiles, error) = JoystickBindingFile.Parse(json);
        Assert.Empty(profiles);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void EmptyFile_GivesNoProfiles()
    {
        var (profiles, error) = JoystickBindingFile.Parse("{}");
        Assert.Null(error);
        Assert.Empty(profiles);
    }
}
