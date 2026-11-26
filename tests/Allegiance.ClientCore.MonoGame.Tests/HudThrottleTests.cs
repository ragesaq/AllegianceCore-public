using Allegiance.ClientCore.MonoGame.Rendering;
using Xunit;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The thrust gauge follows a HOTAS throttle (ragesaq, 2026-11-26:
/// it moved only with the afterburner).</summary>
public sealed class HudThrottleTests
{
    [Fact]
    public void A_bound_joystick_throttle_shows_the_throttle_sent()
        => Assert.Equal(0.4f, HudThrottle.Gauge(-1f, 0.4f, joystickThrottleBound: true, autopilotEngaged: false));

    [Fact]
    public void Without_a_joystick_throttle_the_keyboard_throttle_shows()
        => Assert.Equal(0.2f, HudThrottle.Gauge(0.2f, 0.9f, joystickThrottleBound: false, autopilotEngaged: false));

    [Fact]
    public void Under_autopilot_the_cruise_setting_shows_as_P445_has_it()
        => Assert.Equal(0.2f, HudThrottle.Gauge(0.2f, 0.9f, joystickThrottleBound: true, autopilotEngaged: true));
}
