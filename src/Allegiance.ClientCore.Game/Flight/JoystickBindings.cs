using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Allegiance.ClientCore.MonoGame.Flight;

/// <summary>A flight axis role a joystick axis can feed (native's
/// <c>numericValuesNew</c> outputs 0-3, <c>WinTrek.cpp:10822-10843</c>).</summary>
public enum JoystickRole { Yaw, Pitch, Roll, Throttle }

/// <summary>A held flight command a joystick button can drive. Native's
/// <c>buttonCommandMap</c> binds joystick buttons to TK commands
/// (<c>trekinput.cpp:675-730</c>); these are the three the port's flight path reads
/// from a joystick: TK_FireWeapon (55), TK_FireMissile (56) and TK_FireBooster.</summary>
public enum JoystickCommand { Fire, FireMissile, Booster }

/// <summary>One axis binding inside a <see cref="JoystickDeviceProfile"/>.</summary>
public sealed record JoystickAxisBinding(JoystickRole Role, int Axis, bool Invert = false);

/// <summary>One button binding inside a <see cref="JoystickDeviceProfile"/>.</summary>
public sealed record JoystickButtonBinding(JoystickCommand Command, int Button);

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
    IReadOnlyList<JoystickButtonBinding> Buttons)
{
    public bool Matches(string deviceName) =>
        Match.Length > 0 && deviceName.Contains(Match, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One connected device's raw sample for a frame.</summary>
public readonly record struct JoystickDeviceSample(int Index, string Name, IReadOnlyList<int> Axes, IReadOnlyList<bool> Buttons);

/// <summary>The bound result of one frame: the role axes (feeding
/// <see cref="JoystickFlightController.Update"/>) and the held commands.</summary>
public readonly record struct JoystickBindingFrame(
    bool AnyBound, JoystickAxes Axes, bool Fire, bool FireMissile, bool Booster);

/// <summary>
/// Resolves the connected devices against the device profiles and produces one
/// frame of role axes and commands. Pure (no MonoGame type), so the whole split-HOTAS
/// path is testable with synthetic samples.
/// <para><b>Order.</b> The player's profiles are tried before the built-in ones, and
/// for each device the first matching profile wins. When two devices bind the same
/// role or command, the device with the lower SDL index wins the axis, and a command
/// is held when any device holds it.</para>
/// <para><b>Fallback.</b> When no connected device matches any profile, the first
/// connected device is read with <see cref="Legacy"/>, the port's layout before
/// per-device bindings (<see cref="JoystickAxisLayout.DjiFpvRc3"/>, fire on button 1,
/// booster on button 2), so a setup that worked before still works. A device that
/// matches no profile while another device does is ignored (racing pedals, a second
/// pad) rather than read as a stick or a throttle.</para>
/// <para><b>Held at start.</b> A button counts as pressed only after it has been seen
/// released since its device appeared: switches latched on at start (throttle mode
/// switches, a hat some sticks report as held while idle) are their rest state, not a
/// press.</para>
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
    /// native's role order (X yaw, Y pitch, the third value, a twist, roll; native
    /// default buttons 0 = TK_FireWeapon, 1 = TK_FireMissile, <c>defaultinputmap.mdl</c>
    /// buttonCommandMap); the throttle device feeds only the throttle role, inverted
    /// like native (<c>WinTrek.cpp:10842</c>).
    /// </summary>
    public static IReadOnlyList<JoystickDeviceProfile> BuiltIn { get; } =
    [
        // VKB-Sim Gunfighter (Modern Combat Pro Twist): axis 0 X, 1 Y, 2 twist; buttons
        // 0 and 1 are the trigger's two stages.
        new("VKB-Sim Gunfighter",
            [new(JoystickRole.Yaw, 0), new(JoystickRole.Pitch, 1), new(JoystickRole.Roll, 2)],
            [new(JoystickCommand.Fire, 0), new(JoystickCommand.FireMissile, 1)]),
        // Virpil MT-50CM2 (SDL name "LEFT VPC Throttle MT-50CM2"): axis 3 is the left
        // lever; it reads +32767 at idle and falls towards full forward.
        new("VPC Throttle",
            [new(JoystickRole.Throttle, 3, Invert: true)],
            []),
    ];

    private readonly IReadOnlyList<JoystickDeviceProfile> _profiles;
    private readonly Dictionary<string, bool[]> _armed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenThisFrame = new(StringComparer.Ordinal);

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

    /// <summary>Resolve one frame. <paramref name="devices"/> are the connected devices
    /// in SDL index order.</summary>
    public JoystickBindingFrame Resolve(IReadOnlyList<JoystickDeviceSample> devices)
    {
        var bound = new List<(JoystickDeviceSample Sample, JoystickDeviceProfile Profile)>();
        foreach (var d in devices)
            if (ProfileFor(d.Name) is { } p) bound.Add((d, p));
        if (bound.Count == 0 && devices.Count > 0)
            bound.Add((devices[0], Legacy));

        _seenThisFrame.Clear();
        float? yaw = null, pitch = null, roll = null, throttle = null;
        bool fire = false, missile = false, booster = false;
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

            bool[] armed = Armed(s);
            foreach (var b in p.Buttons)
            {
                if (!IsPressed(s, b.Button, armed)) continue;
                switch (b.Command)
                {
                    case JoystickCommand.Fire: fire = true; break;
                    case JoystickCommand.FireMissile: missile = true; break;
                    case JoystickCommand.Booster: booster = true; break;
                }
            }
        }

        // A device that has gone away forgets its button state, so a reconnect
        // starts from its own rest state again.
        if (_armed.Count > _seenThisFrame.Count)
            foreach (var key in new List<string>(_armed.Keys))
                if (!_seenThisFrame.Contains(key)) _armed.Remove(key);

        return new JoystickBindingFrame(
            bound.Count > 0,
            new JoystickAxes(yaw ?? 0f, pitch ?? 0f, roll ?? 0f, throttle ?? 0f),
            fire, missile, booster);
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
/// fallback apply. A file that cannot be read is reported and ignored, never fatal.
/// <code>
/// { "devices": [ { "match": "Sim Pedals",
///     "axes": [ { "role": "Yaw", "axis": 0, "invert": false } ],
///     "buttons": [ { "command": "Fire", "button": 0 } ] } ] }
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
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record FileModel(List<DeviceModel>? Devices);
    private sealed record DeviceModel(string? Match, List<JoystickAxisBinding>? Axes, List<JoystickButtonBinding>? Buttons);

    /// <summary>Parse the file's text. Returns the profiles and null, or an empty list
    /// and the reason the text was rejected.</summary>
    public static (IReadOnlyList<JoystickDeviceProfile> Profiles, string? Error) Parse(string json)
    {
        FileModel? model;
        try
        {
            model = JsonSerializer.Deserialize<FileModel>(json, Options);
        }
        catch (JsonException e)
        {
            return ([], e.Message);
        }
        var result = new List<JoystickDeviceProfile>();
        foreach (var d in model?.Devices ?? [])
        {
            if (string.IsNullOrWhiteSpace(d.Match))
                return ([], "a device entry has no \"match\"");
            result.Add(new JoystickDeviceProfile(d.Match, d.Axes ?? [], d.Buttons ?? []));
        }
        return (result, null);
    }
}
