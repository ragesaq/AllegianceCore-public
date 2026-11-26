namespace Allegiance.ClientCore.MonoGame.Rendering;

/// <summary>Which throttle the HUD's thrust gauge shows. P445 made it the
/// persistent keyboard throttle, exact and lag-free, and during autopilot the
/// cruise setting the ship returns to. A joystick throttle axis does not move
/// that value: it is applied straight to the controls sent, so with one bound
/// the gauge only moved with the afterburner (ragesaq's headset test,
/// 2026-11-26, a HOTAS throttle). With a bound axis and no autopilot the gauge
/// shows the throttle sent, as native shows the applied controls'
/// (<c>jsValues[c_axisThrottle]</c>).</summary>
public static class HudThrottle
{
    public static float Gauge(float keyboardThrottle, float sentThrottle, bool joystickThrottleBound, bool autopilotEngaged) =>
        joystickThrottleBound && !autopilotEngaged ? sentThrottle : keyboardThrottle;
}
