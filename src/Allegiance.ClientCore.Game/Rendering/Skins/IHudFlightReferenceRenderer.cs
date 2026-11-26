namespace Allegiance.ClientCore.MonoGame.Rendering.Skins;

/// <summary>A HUD skin that can draw its flight reference (speed and throttle)
/// on its own, into the frame's viewport. The VR client shows that image at
/// infinity, as layer 3 of <c>knowledge/vr-hud-design.md</c>, while
/// <see cref="HudContext.FlightReferenceInWorld"/> keeps those readouts, and the
/// horizon cues VR leaves out (heading, pitch ladder, roll), off the skin's main
/// pass. Only the Jet skin has one.</summary>
public interface IHudFlightReferenceRenderer
{
    void DrawFlightReference(HudModel model, in HudDrawFrame frame);
}
