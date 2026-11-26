using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Allegiance.ClientCore.MonoGame.Flight;

/// <summary>A flight axis role a joystick axis can feed (native's
/// <c>numericValuesNew</c> outputs 0-3, <c>WinTrek.cpp:10822-10843</c>).</summary>
public enum JoystickRole { Yaw, Pitch, Roll, Throttle }

/// <summary>A flight command a joystick button (or an axis past a threshold) can
/// drive. Native's <c>buttonCommandMap</c> binds joystick buttons to TK commands
/// (<c>trekinput.cpp:675-730</c>): TK_FireWeapon (55), TK_FireMissile (56),
/// TK_FireBooster, the four side thrusters TK_ThrustLeft/Right/Up/Down, TK_Ripcord
/// and TK_DropChaff. Of these flight commands, <see cref="Ripcord"/> and
/// <see cref="DropChaff"/> act once per press, like their keys; the others are held.
/// <para>The commands from <see cref="Target"/> on are named exactly like the key
/// actions they press (<c>InputActionCatalog</c>), act once per press like those keys,
/// and are read through <see cref="JoystickKeyActions"/>.</para></summary>
public enum JoystickCommand
{
    Fire, FireMissile, Booster, StrafeLeft, StrafeRight, StrafeUp, StrafeDown, Ripcord, DropChaff,
    Target, TargetCenter, TargetNothing, TargetHostile, ToggleAutopilot,
    TargetEnemy, TargetEnemyNearest, TargetEnemyPrev,
    TargetFriendly, TargetFriendlyNearest, TargetFriendlyPrev,
    TargetEnemyBaseNearest, TargetFriendlyBaseNearest, TargetWarpNearest,
}

/// <summary>The joystick commands that stand for a key action of the same name.</summary>
public static class JoystickKeyActions
{
    private static readonly Dictionary<string, JoystickCommand> ByAction = Build();

    /// <summary>The key action names a joystick command can press.</summary>
    public static IReadOnlyCollection<string> Names => ByAction.Keys;

    public static bool TryGet(string action, out JoystickCommand command) => ByAction.TryGetValue(action, out command);

    private static Dictionary<string, JoystickCommand> Build()
    {
        var map = new Dictionary<string, JoystickCommand>(StringComparer.Ordinal);
        foreach (var command in Enum.GetValues<JoystickCommand>())
            if (command >= JoystickCommand.Target) map[command.ToString()] = command;
        return map;
    }
}

/// <summary>One axis binding inside a <see cref="JoystickDeviceProfile"/>.</summary>
public sealed record JoystickAxisBinding(JoystickRole Role, int Axis, bool Invert = false);

/// <summary>One button binding inside a <see cref="JoystickDeviceProfile"/>.</summary>
public sealed record JoystickButtonBinding(JoystickCommand Command, int Button);

/// <summary>An axis read as two buttons: <paramref name="Negative"/> is held while the
/// axis is past <paramref name="Threshold"/> (a fraction of full deflection) on its
/// negative side, <paramref name="Positive"/> on its positive side. Allegiance's side
/// thrusters are on/off, so this is how an analog thumb stick drives them.</summary>
public sealed record JoystickAxisCommandBinding(
    int Axis, JoystickCommand Negative, JoystickCommand Positive, float Threshold = JoystickAxisCommandBinding.DefaultThreshold)
{
    public const float DefaultThreshold = 0.5f;
}

/// <summary>
/// The bindings for one physical device, chosen by a case-insensitive substring of
/// the device's name (MonoGame <c>JoystickCapabilities.DisplayName</c>). A device is
/// matched by name, never by its SDL index, because the index can change between
/// runs and a split HOTAS is two or more devices (native bound each axis and command
/// to a (joystick, input) pair, <c>trekinput.cpp:675-800</c>).
/// </summary>
public sealed record JoystickDeviceProfile(
    string Match,
    IReadOnlyList<JoystickAxisBinding> Axes,
    IReadOnlyList<JoystickButtonBinding> Buttons,
    IReadOnlyList<JoystickAxisCommandBinding>? AxisCommands = null)
{
    public bool Matches(string deviceName) =>
        Match.Length > 0 && deviceName.Contains(Match, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One connected device's raw sample for a frame.</summary>
public readonly record struct JoystickDeviceSample(int Index, string Name, IReadOnlyList<int> Axes, IReadOnlyList<bool> Buttons);

/// <summary>The bound result of one frame: the role axes (feeding
/// <see cref="JoystickFlightController.Update"/>), whether any device binds the
/// throttle role, and the held commands.</summary>
public readonly record struct JoystickBindingFrame(bool AnyBound, JoystickAxes Axes, bool ThrottleBound, int HeldMask)
{
    public bool Holds(JoystickCommand command) => (HeldMask & (1 << (int)command)) != 0;
    public bool Fire => Holds(JoystickCommand.Fire);
    public bool FireMissile => Holds(JoystickCommand.FireMissile);
    public bool Booster => Holds(JoystickCommand.Booster);
    public bool StrafeLeft => Holds(JoystickCommand.StrafeLeft);
    public bool StrafeRight => Holds(JoystickCommand.StrafeRight);
    public bool StrafeUp => Holds(JoystickCommand.StrafeUp);
    public bool StrafeDown => Holds(JoystickCommand.StrafeDown);
    public bool Ripcord => Holds(JoystickCommand.Ripcord);
    public bool DropChaff => Holds(JoystickCommand.DropChaff);

    /// <summary>True while a joystick command named <paramref name="action"/> is held.</summary>
    public bool HoldsAction(string action) => JoystickKeyActions.TryGet(action, out var command) && Holds(command);
}

/// <summary>
/// Resolves the connected devices against the device profiles and produces one
/// frame of role axes and commands. Pure (no MonoGame type), so the whole split-HOTAS
/// path is testable with synthetic samples.
/// <para><b>Order.</b> The player's profiles are tried before the built-in ones, and
/// for each device the first matching profile wins. When two devices bind the same
/// role or command, the device with the lower SDL index wins the axis, and a command
/// is held when any device holds it.</para>
/// <para><b>Fallback.</b> When no connected device matches any profile, and none has
/// matched since the resolver was made, the first connected device is read with
/// <see cref="Legacy"/>, the port's layout before per-device bindings
/// (<see cref="JoystickAxisLayout.DjiFpvRc3"/>, fire on button 1, booster on button 2),
/// so a setup that worked before still works. A device that matches no profile while
/// another device does, or after one has, is ignored (racing pedals left connected when
/// the stick is unplugged, a second pad) rather than read as a stick or a throttle.</para>
/// <para><b>Held at start.</b> A button counts as pressed only after it has been seen
/// released since its device appeared: switches latched on at start (throttle mode
/// switches, a hat some sticks report as held while idle) are their rest state, not a
/// press. A device that goes away forgets this state, so a reconnect starts again.</para>
/// </summary>
public sealed class JoystickBindingResolver
{
    /// <summary>The port's layout before per-device bindings, used for the fallback.</summary>
    public static JoystickDeviceProfile Legacy { get; } = new(
        "",
        [
            new(JoystickRole.Yaw, JoystickAxisLayout.DjiFpvRc3.YawAxis, JoystickAxisLayout.DjiFpvRc3.InvertYaw),
            new(JoystickRole.Pitch, JoystickAxisLayout.DjiFpvRc3.PitchAxis, JoystickAxisLayout.DjiFpvRc3.InvertPitch),
            new(JoystickRole.Roll, JoystickAxisLayout.DjiFpvRc3.RollAxis, JoystickAxisLayout.DjiFpvRc3.InvertRoll),
            new(JoystickRole.Throttle, JoystickAxisLayout.DjiFpvRc3.ThrottleAxis, JoystickAxisLayout.DjiFpvRc3.InvertThrottle),
        ],
        [
            new(JoystickCommand.Fire, JoystickFlightController.FireButton),
            new(JoystickCommand.Booster, JoystickFlightController.BoosterButton),
        ]);

    /// <summary>
    /// Built-in profiles for devices read out on real hardware. The stick follows
    /// native's role order (X yaw, Y pitch, the third value, a twist, roll) and fires
    /// on button 0 like native's TK_FireWeapon default (<c>defaultinputmap.mdl</c>
    /// buttonCommandMap). Native's button 1 = TK_FireMissile is not taken: on this
    /// stick button 1 is the trigger's second stage, which ragesaq does not want as
    /// missile fire. The three thumb face buttons, left to right (buttons 3, 4 and 5),
    /// are missiles, chaff and ripcord (ragesaq's choice). The throttle device feeds the throttle role, inverted like native
    /// (<c>WinTrek.cpp:10842</c>), and its index-finger thumb stick drives the side
    /// thrusters (ragesaq's choice; native gives the thrusters keys only).
    /// </summary>
    public static IReadOnlyList<JoystickDeviceProfile> BuiltIn { get; } =
    [
        // VKB-Sim Gunfighter (Modern Combat Pro Twist): axis 0 X, 1 Y, 2 twist; buttons
        // 0 and 1 are the trigger's two stages; the second stage is left unbound.
        // Buttons 3, 4 and 5 are the thumb face buttons, left to right.
        new("VKB-Sim Gunfighter",
            [new(JoystickRole.Yaw, 0), new(JoystickRole.Pitch, 1), new(JoystickRole.Roll, 2)],
            [
                new(JoystickCommand.Fire, 0),
                new(JoystickCommand.FireMissile, 3),
                new(JoystickCommand.DropChaff, 4),
                new(JoystickCommand.Ripcord, 5),
            ]),
        // Virpil MT-50CM2 (SDL name "LEFT VPC Throttle MT-50CM2"): axis 3 is the left
        // lever, set in the Virpil configurator to read -32767 at idle and +32767 at
        // full; it is full already at the afterburner detent, and past the detent the
        // configurator holds logical button 80 (79 here) for the afterburner. Axes 0 and 1
        // are the index-finger thumb stick: left/right and up/down thrust. The grip's
        // targeting controls, by Virpil's diagram numbers: push buttons 15, 16 and 21,
        // and the 5-way castle switches 20 (enemies) and 19 (friendlies and navigation),
        // each read as up, right, down, left and centre.
        new("VPC Throttle",
            [new(JoystickRole.Throttle, 3)],
            [
                new(JoystickCommand.Booster, 79),                   // past the afterburner detent
                new(JoystickCommand.TargetNothing, 32),             // 15
                new(JoystickCommand.TargetCenter, 20),              // 16
                new(JoystickCommand.ToggleAutopilot, 31),           // 21
                new(JoystickCommand.TargetEnemy, 29),               // 20 up
                new(JoystickCommand.Target, 30),                    // 20 right
                new(JoystickCommand.TargetEnemyPrev, 27),           // 20 down
                new(JoystickCommand.TargetHostile, 28),             // 20 left
                new(JoystickCommand.TargetEnemyNearest, 26),        // 20 centre
                new(JoystickCommand.TargetFriendly, 24),            // 19 up
                new(JoystickCommand.TargetWarpNearest, 25),         // 19 right
                new(JoystickCommand.TargetFriendlyPrev, 22),        // 19 down
                new(JoystickCommand.TargetFriendlyBaseNearest, 23), // 19 left
                new(JoystickCommand.TargetFriendlyNearest, 21),     // 19 centre
            ],
            [
                new(0, JoystickCommand.StrafeLeft, JoystickCommand.StrafeRight),
                new(1, JoystickCommand.StrafeUp, JoystickCommand.StrafeDown),
            ]),
    ];

    private readonly IReadOnlyList<JoystickDeviceProfile> _profiles;
    private readonly Dictionary<string, bool[]> _armed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenThisFrame = new(StringComparer.Ordinal);
    private bool _profileEverMatched;

    public JoystickBindingResolver(IReadOnlyList<JoystickDeviceProfile>? userProfiles = null)
    {
        var all = new List<JoystickDeviceProfile>();
        if (userProfiles is not null) all.AddRange(userProfiles);
        all.AddRange(BuiltIn);
        _profiles = all;
    }

    /// <summary>The profile a device name resolves to, or null when none matches.</summary>
    public JoystickDeviceProfile? ProfileFor(string deviceName)
    {
        foreach (var p in _profiles)
            if (p.Matches(deviceName)) return p;
        return null;
    }

    /// <summary>Whether <see cref="Legacy"/> may still be used: no profile has matched
    /// a device since this resolver was made.</summary>
    public bool LegacyAllowed => !_profileEverMatched;

    /// <summary>Forget every device's armed state, so a button held now counts only
    /// after it is released once. Called on entering flight, like the key latches'
    /// prime, so a button held through launch does not act on the first frame.</summary>
    public void Disarm() => _armed.Clear();

    /// <summary>Resolve one frame. <paramref name="devices"/> are the connected devices
    /// in SDL index order.</summary>
    public JoystickBindingFrame Resolve(IReadOnlyList<JoystickDeviceSample> devices)
    {
        var bound = new List<(JoystickDeviceSample Sample, JoystickDeviceProfile Profile)>();
        foreach (var d in devices)
            if (ProfileFor(d.Name) is { } p) bound.Add((d, p));
        if (bound.Count > 0)
            _profileEverMatched = true;
        else if (devices.Count > 0 && !_profileEverMatched)
            bound.Add((devices[0], Legacy));

        _seenThisFrame.Clear();
        float? yaw = null, pitch = null, roll = null, throttle = null;
        int held = 0;
        foreach (var (s, p) in bound)
        {
            foreach (var a in p.Axes)
            {
                float v = Normalize(s.Axes, a.Axis, a.Invert);
                switch (a.Role)
                {
                    case JoystickRole.Yaw: yaw ??= v; break;
                    case JoystickRole.Pitch: pitch ??= v; break;
                    case JoystickRole.Roll: roll ??= v; break;
                    case JoystickRole.Throttle: throttle ??= v; break;
                }
            }

            if (p.AxisCommands is { } axisCommands)
                foreach (var c in axisCommands)
                {
                    float v = Normalize(s.Axes, c.Axis, false);
                    if (v <= -c.Threshold) held |= 1 << (int)c.Negative;
                    else if (v >= c.Threshold) held |= 1 << (int)c.Positive;
                }

            bool[] armed = Armed(s);
            foreach (var b in p.Buttons)
                if (IsPressed(s, b.Button, armed)) held |= 1 << (int)b.Command;
        }

        // A device that has gone away forgets its button state, so a reconnect
        // starts from its own rest state again.
        List<string>? gone = null;
        foreach (var key in _armed.Keys)
            if (!_seenThisFrame.Contains(key)) (gone ??= new()).Add(key);
        if (gone is not null)
            foreach (var key in gone) _armed.Remove(key);

        return new JoystickBindingFrame(
            bound.Count > 0,
            new JoystickAxes(yaw ?? 0f, pitch ?? 0f, roll ?? 0f, throttle ?? 0f),
            throttle.HasValue,
            held);
    }

    private bool[] Armed(in JoystickDeviceSample s)
    {
        string key = s.Index + "|" + s.Name;
        _seenThisFrame.Add(key);
        if (!_armed.TryGetValue(key, out var armed) || armed.Length != s.Buttons.Count)
        {
            armed = new bool[s.Buttons.Count];
            _armed[key] = armed;
        }
        for (int i = 0; i < armed.Length; i++)
            if (!s.Buttons[i]) armed[i] = true;
        return armed;
    }

    private static bool IsPressed(in JoystickDeviceSample s, int button, bool[] armed) =>
        button >= 0 && button < s.Buttons.Count && armed[button] && s.Buttons[button];

    private static float Normalize(IReadOnlyList<int> axes, int index, bool invert)
    {
        if (index < 0 || index >= axes.Count) return 0f;
        float v = axes[index] / JoystickFlightController.AxisNormDivisor;
        if (v > 1f) v = 1f;
        else if (v < -1f) v = -1f;
        return invert ? -v : v;
    }
}

/// <summary>
/// Reads the player's device profiles from <c>joystick.json</c> beside the other
/// client settings. The file is optional: without it the built-in profiles and the
/// fallback apply. A file that cannot be read or parsed is reported and ignored,
/// never fatal.
/// <code>
/// { "devices": [ { "match": "Sim Pedals",
///     "axes": [ { "role": "Yaw", "axis": 0, "invert": false } ],
///     "buttons": [ { "command": "Fire", "button": 0 } ],
///     "axisCommands": [ { "axis": 1, "negative": "StrafeUp", "positive": "StrafeDown", "threshold": 0.5 } ] } ] }
/// </code>
/// </summary>
public static class JoystickBindingFile
{
    public const string FileName = "joystick.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private sealed record FileModel(List<DeviceModel?>? Devices);
    private sealed record DeviceModel(
        string? Match,
        List<JoystickAxisBinding?>? Axes,
        List<JoystickButtonBinding?>? Buttons,
        List<JoystickAxisCommandBinding?>? AxisCommands);

    /// <summary>Parse the file's text. Returns the profiles and null, or an empty list
    /// and the reason the text was rejected.</summary>
    public static (IReadOnlyList<JoystickDeviceProfile> Profiles, string? Error) Parse(string json)
    {
        FileModel? model;
        try
        {
            model = JsonSerializer.Deserialize<FileModel>(json, Options);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException)
        {
            return ([], e.Message);
        }
        var result = new List<JoystickDeviceProfile>();
        foreach (var d in model?.Devices ?? [])
        {
            if (d is null)
                return ([], "a device entry is null");
            if (string.IsNullOrWhiteSpace(d.Match))
                return ([], "a device entry has no \"match\"");
            var axes = new List<JoystickAxisBinding>();
            foreach (var a in d.Axes ?? [])
            {
                if (a is null) return ([], $"device \"{d.Match}\": an axes entry is null");
                axes.Add(a);
            }
            var buttons = new List<JoystickButtonBinding>();
            foreach (var b in d.Buttons ?? [])
            {
                if (b is null) return ([], $"device \"{d.Match}\": a buttons entry is null");
                buttons.Add(b);
            }
            var axisCommands = new List<JoystickAxisCommandBinding>();
            foreach (var c in d.AxisCommands ?? [])
            {
                if (c is null) return ([], $"device \"{d.Match}\": an axisCommands entry is null");
                if (!(c.Threshold > 0f && c.Threshold <= 1f))
                    return ([], $"device \"{d.Match}\": an axisCommands threshold is not in (0, 1]");
                axisCommands.Add(c);
            }
            result.Add(new JoystickDeviceProfile(d.Match, axes, buttons, axisCommands));
        }
        return (result, null);
    }
}
