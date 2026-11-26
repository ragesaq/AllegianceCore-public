using Allegiance.ClientCore.MonoGame.Flight;
using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.ClientCore.MonoGame.Rendering.Jet;
using Allegiance.ClientCore.MonoGame.Rendering.Skins;
using Allegiance.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Allegiance.ClientCore.MonoGame.Tests.Skins;

/// <summary>VR HUD build step 2 (knowledge/vr-hud-design.md, layer 3): in a
/// stereo frame the Jet skin's flight reference (heading tape, pitch ladder,
/// roll pointer, speed and throttle tapes) leaves the main HUD pass, which the
/// VR client shows on the near panel, and is drawn on its own for the block at
/// infinity. A flat frame is unchanged.</summary>
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

    [Fact]
    public void In_vr_the_flight_reference_is_drawn_on_its_own()
    {
        var surface = new Recorder();
        var jet = Jet(surface);
        var model = Model(inWorld: true);
        jet.Draw(model, Screen);
        surface.Clear();
        jet.DrawFlightReference(model, Screen);
        foreach (var id in FlightElements) Assert.True(surface.Lines(id) > 0, $"{id} missing from the flight block");
        Assert.Equal(1, surface.Begins);
        Assert.Equal(1, surface.Ends);
    }

    [Fact]
    public void The_flight_block_draws_nothing_while_the_instruments_are_hidden()
    {
        var surface = new Recorder();
        var model = HudModelAdapter.Create(Frame(), CameraMode.Cockpit, docked: true,
            flightReference: Reference(5f, 10f, 45f), centreReticleInWorld: true, flightReferenceInWorld: true);
        Jet(surface).DrawFlightReference(model, Screen);
        Assert.Equal(0, surface.TotalLines);
        Assert.Equal(0, surface.Begins);
    }

    [Fact]
    public void The_palette_wrapper_passes_the_flight_block_to_the_active_palette()
    {
        var surface = new Recorder();
        var definition = HudSkinResolver.Catalog.TryGet(HudSkinIds.Jet)!;
        var settings = new HudSettings { Skin = HudSkinIds.Jet };
        var amberDefinition = HudSkinResolver.Catalog.TryGetVariant(HudSkinIds.Jet, JetPaletteIds.Amber)!;
        var green = new JetHudRenderer(surface, settings, Resources(), definition);
        var amber = new JetHudRenderer(surface, settings, Resources(), amberDefinition);
        var palettes = new JetPaletteSwitcher(settings,
            new ResolvedJetPalette(definition, green), new ResolvedJetPalette(amberDefinition, amber));
        var wrapper = new JetPaletteRenderer(palettes);
        var model = Model(inWorld: true);
        // As in the game: the main pass first, which resolves the attitude.
        wrapper.Draw(model, Screen);
        surface.Clear();
        ((IHudFlightReferenceRenderer)wrapper).DrawFlightReference(model, Screen);
        Assert.True(surface.Lines("ladder") > 0);
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
