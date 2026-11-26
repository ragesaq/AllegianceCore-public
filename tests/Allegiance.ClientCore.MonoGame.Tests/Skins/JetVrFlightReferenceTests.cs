using Allegiance.ClientCore.MonoGame.Flight;
using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.ClientCore.MonoGame.Rendering.Jet;
using Allegiance.ClientCore.MonoGame.Rendering.Skins;
using Allegiance.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Allegiance.ClientCore.MonoGame.Tests.Skins;

/// <summary>VR HUD build step 2 (knowledge/vr-hud-design.md, layer 3): in a
/// stereo frame the Jet skin leaves its speed and throttle tapes and its horizon
/// cues (heading tape, pitch ladder, roll pointer, recorded as "hdg" and
/// "ladder") off the main HUD pass, because the VR client shows the Classic
/// centre group at infinity in their place. A flat frame is unchanged.</summary>
public sealed class JetVrFlightReferenceTests
{
    private static readonly string[] FlightElements = { "hdg", "ladder", "spd", "thr" };
    private static readonly HudDrawFrame Screen = new(new Viewport(0, 0, 1920, 1080), null, false);

    [Fact]
    public void A_flat_frame_draws_the_flight_reference_in_the_main_pass()
    {
        var surface = new Recorder();
        Jet(surface).Draw(Model(inWorld: false), Screen);
        foreach (var id in FlightElements) Assert.True(surface.Lines(id) > 0, $"{id} missing from the flat pass");
    }

    [Fact]
    public void In_vr_the_main_pass_leaves_the_flight_reference_out()
    {
        var surface = new Recorder();
        Jet(surface).Draw(Model(inWorld: true), Screen);
        foreach (var id in FlightElements) Assert.Equal(0, surface.Lines(id));
        // The rest of the HUD stays on the panel.
        Assert.True(surface.TotalLines > 0);
    }

    private static HudModel Model(bool inWorld) =>
        HudModelAdapter.Create(Frame(), CameraMode.Cockpit, velocityMarker: new(940, 520),
            flightReference: Reference(5f, 10f, 45f), centreReticleInWorld: inWorld, flightReferenceInWorld: inWorld);

    private static JetHudRenderer Jet(IJetVectorSurface surface)
    {
        var definition = HudSkinResolver.Catalog.TryGet(HudSkinIds.Jet)!;
        return new JetHudRenderer(surface, new HudSettings { Skin = HudSkinIds.Jet }, Resources(), definition);
    }

    private static HudSkinResources Resources() =>
        new(_ => { }, (_, _) => { }, _ => { }, (_, _) => { }, _ => { }, _ => { }, _ => { }) { CommandChrome = _ => { } };

    private static HudFrame Frame() => new(
        .8f, .6f, .4f, .3f, .2f, 1f, 260f, .6f, false, false, true, 0,
        false, 0f, false, false, 0f, 0f, Array.Empty<WeaponStatusRow>(), false, 0);

    private static HudFlightReference Reference(float pitchDegrees, float rollDegrees, float headingDegrees)
    {
        float heading = headingDegrees * MathF.PI / 180f;
        float pitch = pitchDegrees * MathF.PI / 180f;
        float roll = rollDegrees * MathF.PI / 180f;
        var forward = new Allegiance.Core.Collision.Vector3(
            MathF.Cos(heading) * MathF.Cos(pitch),
            MathF.Sin(heading) * MathF.Cos(pitch),
            MathF.Sin(pitch));
        var levelUp = new Allegiance.Core.Collision.Vector3(
            -MathF.Cos(heading) * MathF.Sin(pitch),
            -MathF.Sin(heading) * MathF.Sin(pitch),
            MathF.Cos(pitch));
        var orientation = new Orientation();
        orientation.Set(forward, levelUp);
        if (roll != 0f) orientation.PostRotate(forward, roll);
        return HudFlightReference.FromSim(orientation, forward * 260f, HudFlightReference.SectorUp);
    }

    /// <summary>Counts the strokes drawn under each element id.</summary>
    private sealed class Recorder : IJetVectorSurface, IJetElementAudit
    {
        private readonly Dictionary<string, int> _lines = new(StringComparer.Ordinal);
        private string _element = "";
        public int Begins, Ends, TotalLines;

        public int Lines(string id) => _lines.TryGetValue(id, out int n) ? n : 0;
        public void Clear()
        {
            _lines.Clear();
            Begins = Ends = TotalLines = 0;
            _element = "";
        }

        public void Element(string id) => _element = id;
        public void Drawn(string id) { }
        public void Begin() => Begins++;
        public void End() => Ends++;
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            TotalLines++;
            _lines[_element] = Lines(_element) + 1;
        }
        public void Fill(Vector2 min, Vector2 size, Color color) { }
        public void Text(string text, Vector2 position, bool large, float scale, Color color) { }
        public Vector2 Measure(string text, bool large, float scale) => new(text.Length * 6f * scale, 12f * scale);
    }
}
