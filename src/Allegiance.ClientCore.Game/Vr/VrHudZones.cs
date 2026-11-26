using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>The VR HUD's zones (layer 4; knowledge/vr-hud-design.md, "Layout
/// criteria for the VR HUD", accepted by ragesaq on 2026-11-26). The view from
/// the seat is divided into zones, each a region of directions with its own
/// depth, and every HUD element belongs to exactly one. The flat HUD is still
/// drawn into one image in the flat layout; each element is cut out of that
/// image by its rectangle and shown on a quad of its own in its zone, facing
/// the head. The aim and targeting symbols are not zoned: they sit on their
/// objects (layers 1 and 2).
///
/// Angles are in degrees from the seat: azimuth (across) positive to the
/// right, elevation (up and down) positive above the nose.</summary>
public static class VrHudZones
{
    public enum ZoneId { FlightBlock, Comms, Target, Navigation, Systems }

    /// <summary>A region of directions from the seat, in degrees, and the depth
    /// its quads are drawn at.</summary>
    public readonly record struct Zone(ZoneId Id, float Left, float Right, float Bottom, float Top, float DistanceMetres)
    {
        public float Width => Right - Left;
        public float Height => Top - Bottom;
    }

    /// <summary>How far out the zones other than the flight block are drawn
    /// (ragesaq, 2026-11-26: "purple panels should be at 2m, 1m is a little too
    /// close").</summary>
    public const float PanelDistanceMetres = 2f;

    /// <summary>The accepted zone table. A, the flight block, is drawn by
    /// <see cref="VrHudGeometry.FlightBlockQuad"/> at its own 10 m; its row here
    /// is the region the other zones keep clear of.</summary>
    public static readonly IReadOnlyList<Zone> Zones = new[]
    {
        new Zone(ZoneId.FlightBlock, -26f, 26f, -22f, 14f, VrHudGeometry.FlightBlockDistanceMetres),
        new Zone(ZoneId.Comms, -16f, 16f, 16f, 30f, PanelDistanceMetres),
        new Zone(ZoneId.Target, -14f, 14f, -40f, -24f, PanelDistanceMetres),
        new Zone(ZoneId.Navigation, -42f, -16f, -42f, -24f, PanelDistanceMetres),
        new Zone(ZoneId.Systems, 16f, 42f, -42f, -24f, PanelDistanceMetres),
    };

    /// <summary>Where a modal screen (the expanded starmap, the scoreboard, the
    /// menus, the hangar and the like) is shown, whole, while the flight block
    /// is hidden: ahead, 2 m out, as wide as the step 1 panel looked.</summary>
    public static readonly VrHudGeometry.Panel ModalPanel = new(PanelDistanceMetres, 0f, 2.2f);

    /// <summary>Where the whole HUD image hangs for a skin without zones: the
    /// step 1 panel's place, below the nose, at 2 m and as wide as it looked.</summary>
    public static readonly VrHudGeometry.Panel FallbackPanel = new(PanelDistanceMetres, MathHelper.ToRadians(25f), 2.2f);

    public static Zone Get(ZoneId id)
    {
        foreach (var zone in Zones)
            if (zone.Id == id) return zone;
        throw new ArgumentOutOfRangeException(nameof(id), id, "No such zone.");
    }

    /// <summary>The least gap between two zones (rule 2).</summary>
    public const float GapDegrees = 1.5f;

    /// <summary>Nothing to be read lies further out than this, across or down
    /// (rule 4).</summary>
    public const float ReachDegrees = 42f;

    /// <summary>The gap between two zones: the larger of the gap across and the
    /// gap up and down, negative when they overlap.</summary>
    public static float Gap(in Zone a, in Zone b) =>
        MathF.Max(MathF.Max(b.Left - a.Right, a.Left - b.Right), MathF.Max(b.Bottom - a.Top, a.Bottom - b.Top));

    /// <summary>How many degrees one pixel of the HUD image covers on a zone
    /// quad: what it covered on the step 1 panel for a 1280-wide image (1.1 m
    /// across at 1 m, about 57.6°), about 0.045°. It is the same for every image
    /// size, because the Classic HUD draws its text at fixed pixel sizes: the
    /// small font's line (14 px) stays at about 0.63°, above the 0.6° floor
    /// (rule 6), while its zone keeps it unscaled.</summary>
    public static readonly float DegreesPerPixel = MathHelper.ToDegrees(2f * MathF.Atan(0.55f)) / 1280f;

    /// <summary>Where a zone puts its content: across (left, centre, right) and
    /// up and down (top, middle, bottom).</summary>
    public enum Align { Start, Centre, End }

    /// <summary>How a zone lays out its cut-outs: the block of rows is pushed
    /// toward the flight block (comms down, the lower zones up, the side zones
    /// in), each row is aligned across within the block by
    /// <paramref name="RowAcross"/>, and rows are this many pixels apart.</summary>
    public readonly record struct ZoneLayout(ZoneId Zone, Align Across, Align UpDown, int RowGapPixels, Align RowAcross);

    public static readonly IReadOnlyList<ZoneLayout> Layouts = new[]
    {
        // Comms' two parts are one strip of the image cut in two.
        new ZoneLayout(ZoneId.Comms, Align.Centre, Align.End, 0, Align.Centre),
        new ZoneLayout(ZoneId.Target, Align.Centre, Align.Start, 6, Align.Centre),
        new ZoneLayout(ZoneId.Navigation, Align.End, Align.Start, 6, Align.End),
        // Systems' two parts share their right edge and meet in the image, so
        // they are stacked right-aligned with no gap: they join as they were.
        new ZoneLayout(ZoneId.Systems, Align.Start, Align.Start, 0, Align.End),
    };

    /// <summary>Pixels between cut-outs side by side in a row.</summary>
    public const int ColumnGapPixels = 4;

    /// <summary>One HUD element, or a group of elements that sit together, cut
    /// out of the HUD image by <paramref name="Source"/> (pixels) and shown in
    /// <paramref name="Zone"/>, in row <paramref name="Row"/> of that zone.</summary>
    public readonly record struct Cutout(string Name, ZoneId Zone, int Row, Rectangle Source);

    /// <summary>Where a cut-out is shown, and the scale its zone needed to fit
    /// (1 unless the zone's content is larger than the zone).</summary>
    public readonly record struct Placement(Cutout Cutout, AngleRect Where, float Scale);

    /// <summary>The Classic HUD's cut-outs for a HUD image this size, from the
    /// flat layout's own rules (knowledge/vr-hud-design.md, "Layer 4 as
    /// built"). <see cref="ClassicLayout"/> checks that the sources lie in the
    /// image and do not overlap, so no element is shown twice. In VR
    /// the port's text lines are drawn in the image's lower left
    /// (<see cref="VrHudGeometry.PortTextTop"/>) and the keys hint is kept
    /// within the comms strip.</summary>
    public static IReadOnlyList<Cutout> ClassicCutouts(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "The HUD image has no area.");
        int w = imageWidth, h = imageHeight, cx = w / 2;
        var list = new List<Cutout>();

        // B: the chat pane, its filter tag and compose box, the keys hint, the
        // vote banner, tips, warnings, base objectives and "NO X IN SECTOR"
        // (0.40 of the height): the top centre strip.
        int commsBottom = Math.Max(150, (int)MathF.Ceiling(0.40f * h + 22f));
        list.Add(new Cutout("comms", ZoneId.Comms, 0,
            new Rectangle(cx - CommsHalfWidthPixels, 0, 2 * CommsHalfWidthPixels, commsBottom)));

        // C: the target pane with its preview, the leader, commandee and
        // mouse-stick lines above it; and the port's text lines beside it.
        float s = Rendering.TargetPaneLayout.Scale(w, h);
        int paneTop = (int)MathF.Floor(Rendering.TargetPaneLayout.TopY(w, h) - TargetMarginPixels(s));
        int paneHalf = (int)MathF.Ceiling(150f * s);
        list.Add(new Cutout("target", ZoneId.Target, 0, new Rectangle(cx - paneHalf, paneTop, 2 * paneHalf, h - paneTop)));
        int portTop = (int)VrHudGeometry.PortTextTop(h) - 8;
        list.Add(new Cutout("port lines", ZoneId.Target, 0,
            new Rectangle(0, portTop, (int)VrHudGeometry.PortTextWrapPixels + 40, h - CameraStripPixels - portTop)));

        // D: the minimap, the location and turret lines (top right), the
        // orders and the lives and kills badges (top left), the camera label.
        var (mx, my, mw, mh) = Rendering.MinimapGeometry.CornerPanel(w, h);
        var minimap = new Rectangle((int)MathF.Floor(mx), (int)MathF.Floor(my), (int)MathF.Ceiling(mw), (int)MathF.Ceiling(mh));
        list.Add(new Cutout("minimap", ZoneId.Navigation, 0, minimap));
        list.Add(new Cutout("location", ZoneId.Navigation, 0, new Rectangle(w - 200, 30, 200, 76)));
        list.Add(new Cutout("orders and badges", ZoneId.Navigation, 1, new Rectangle(0, 24, 330, 172)));
        list.Add(new Cutout("camera", ZoneId.Navigation, 1, new Rectangle(0, h - CameraStripPixels, 160, CameraStripPixels)));

        // E: the afterburner, power, weapon mode, mining and time dilation
        // lines (the text layout moves them below 126 px, out of the box it
        // keeps for them), the weapons pane or the inventory (F4) in its place.
        // Below the target cut-out's top it starts right of that cut-out.
        int systemsLeft = cx + CommsHalfWidthPixels + 2;
        int systemsSplit = paneTop - 2;
        list.Add(new Cutout("systems", ZoneId.Systems, 0,
            new Rectangle(systemsLeft, SystemsTopPixels, w - systemsLeft, systemsSplit - SystemsTopPixels)));
        int lowLeft = cx + paneHalf + 2;
        list.Add(new Cutout("systems low", ZoneId.Systems, 1,
            new Rectangle(lowLeft, systemsSplit, w - lowLeft, minimap.Top - 4 - systemsSplit)));
        return list;
    }

    /// <summary>How far above the target pane's top its cut-out starts: the
    /// mouse-stick notice is drawn 140 px above it, unscaled, and the leader,
    /// commandee and mouse-stick lines below that.</summary>
    public static float TargetMarginPixels(float paneScale) => MathF.Max(144f, 144f * paneScale);

    /// <summary>Half the comms strip's width: the chat pane (355 px from 160 px
    /// left of the centre) and the keys hint, which VR keeps this narrow, clear
    /// of the weapons pane (from about 882 px in a 1280-wide image).</summary>
    public const int CommsHalfWidthPixels = 240;

    /// <summary>Where the systems cut-out starts down the image: under the
    /// location and turret lines.</summary>
    public const int SystemsTopPixels = 120;

    /// <summary>The camera label's strip at the image's foot.</summary>
    public const int CameraStripPixels = 34;

    /// <summary>Lays the cut-outs out in their zones (<see cref="Layouts"/>):
    /// rows of cut-outs side by side, at <see cref="DegreesPerPixel"/>, scaled
    /// down as a whole when a zone's content is larger than the zone.</summary>
    public static IReadOnlyList<Placement> Place(IReadOnlyList<Cutout> cutouts)
    {
        var placements = new List<Placement>(cutouts.Count);
        foreach (var layout in Layouts)
        {
            var zone = Get(layout.Zone);
            var rows = new SortedDictionary<int, List<Cutout>>();
            foreach (var c in cutouts)
                if (c.Zone == layout.Zone)
                {
                    if (!rows.TryGetValue(c.Row, out var row)) rows[c.Row] = row = new List<Cutout>();
                    row.Add(c);
                }
            if (rows.Count == 0) continue;

            float contentW = 0f, contentH = 0f;
            foreach (var row in rows.Values)
            {
                contentW = MathF.Max(contentW, RowWidth(row));
                contentH += RowHeight(row);
            }
            contentH += layout.RowGapPixels * (rows.Count - 1);
            float zoneW = zone.Width / DegreesPerPixel, zoneH = zone.Height / DegreesPerPixel;
            float scale = MathF.Min(1f, MathF.Min(zoneW / contentW, zoneH / contentH));
            float deg = DegreesPerPixel * scale;

            float blockLeft = zone.Left + Offset(layout.Across, zone.Width, contentW * deg);
            float blockTop = zone.Top - Offset(layout.UpDown, zone.Height, contentH * deg);
            float y = 0f;
            foreach (var row in rows.Values)
            {
                float x = Offset(layout.RowAcross, contentW, RowWidth(row));
                foreach (var c in row)
                {
                    float left = blockLeft + x * deg, top = blockTop - y * deg;
                    placements.Add(new Placement(c,
                        new AngleRect(left, left + c.Source.Width * deg, top - c.Source.Height * deg, top), scale));
                    x += c.Source.Width + ColumnGapPixels;
                }
                y += RowHeight(row) + layout.RowGapPixels;
            }
        }
        return placements;
    }

    /// <summary>The text size floor (rule 6), and the small font's line, the
    /// smallest text the Classic HUD draws in a zone.</summary>
    public const float TextFloorDegrees = 0.6f;
    public const int SmallLinePixels = 14;

    /// <summary>The least scale a zone may show its content at: below it the
    /// small font's line falls under <see cref="TextFloorDegrees"/>.</summary>
    public static float MinimumScale => TextFloorDegrees / (SmallLinePixels * DegreesPerPixel);

    /// <summary>Classic's zones for a HUD image this size, or null, with the
    /// reason, when the zones cannot hold its layout: a cut-out leaves the
    /// image or has no area, two cut-outs share pixels (an element would be
    /// shown twice, or cut in two), or a zone would shrink its text below the
    /// floor (rule 6: "never shrinks it"). The cut-outs follow the flat
    /// layout's anchors, which hold together only near the 1280 by 720 image
    /// they were taken from; for another size the caller shows the whole image
    /// on one panel.</summary>
    public static IReadOnlyList<Placement>? ClassicLayout(int imageWidth, int imageHeight, out string? reason)
    {
        var cutouts = ClassicCutouts(imageWidth, imageHeight);
        var image = new Rectangle(0, 0, imageWidth, imageHeight);
        for (int i = 0; i < cutouts.Count; i++)
        {
            var a = cutouts[i];
            if (a.Source.Width <= 0 || a.Source.Height <= 0 || !image.Contains(a.Source))
            {
                reason = $"'{a.Name}' {a.Source} is not inside the image";
                return null;
            }
            for (int j = i + 1; j < cutouts.Count; j++)
                if (a.Source.Intersects(cutouts[j].Source))
                {
                    reason = $"'{a.Name}' and '{cutouts[j].Name}' share pixels";
                    return null;
                }
        }
        var placements = Place(cutouts);
        foreach (var p in placements)
            if (p.Scale < MinimumScale)
            {
                reason = $"zone {p.Cutout.Zone} would scale its text by {p.Scale:0.00}, under the {TextFloorDegrees}° floor";
                return null;
            }
        reason = null;
        return placements;
    }

    private static float RowWidth(List<Cutout> row)
    {
        float width = ColumnGapPixels * (row.Count - 1);
        foreach (var c in row) width += c.Source.Width;
        return width;
    }

    private static float RowHeight(List<Cutout> row)
    {
        float height = 0f;
        foreach (var c in row) height = MathF.Max(height, c.Source.Height);
        return height;
    }

    /// <summary>How far in from the start an item of <paramref name="size"/>
    /// sits in <paramref name="span"/> under <paramref name="align"/>.</summary>
    private static float Offset(Align align, float span, float size) => align switch
    {
        Align.Start => 0f,
        Align.Centre => (span - size) / 2f,
        _ => span - size,
    };

    /// <summary>A region of directions, in degrees, as <see cref="Zone"/> has.</summary>
    public readonly record struct AngleRect(float Left, float Right, float Bottom, float Top)
    {
        public float Width => Right - Left;
        public float Height => Top - Bottom;
        // A thousandth of a degree of slack for rounding at the zone's edge.
        public bool Within(in Zone zone) =>
            Left >= zone.Left - 1e-3f && Right <= zone.Right + 1e-3f && Bottom >= zone.Bottom - 1e-3f
            && Top <= zone.Top + 1e-3f;
        public bool Overlaps(in AngleRect other) =>
            Left < other.Right && other.Left < Right && Bottom < other.Top && other.Bottom < Top;
    }

    /// <summary>The direction from the seat at this azimuth and elevation, in
    /// seat space (-Z the nose, +Y up, +X right).</summary>
    public static Vector3 Direction(float azimuthDegrees, float elevationDegrees)
    {
        float az = MathHelper.ToRadians(azimuthDegrees), el = MathHelper.ToRadians(elevationDegrees);
        return new Vector3(MathF.Sin(az) * MathF.Cos(el), MathF.Sin(el), -MathF.Cos(az) * MathF.Cos(el));
    }

    /// <summary>A quad in seat space that shows <paramref name="rect"/> at
    /// <paramref name="distanceMetres"/>: flat, centred on the direction of the
    /// rect's centre and facing the head, its edges at the rect's half-angles
    /// from that centre. Corners top-left, top-right, bottom-left,
    /// bottom-right.</summary>
    public static (Vector3 TopLeft, Vector3 TopRight, Vector3 BottomLeft, Vector3 BottomRight) Quad(in AngleRect rect,
        float distanceMetres)
    {
        if (!(distanceMetres > 0f)) throw new ArgumentOutOfRangeException(nameof(distanceMetres), "A quad needs a distance.");
        float azC = (rect.Left + rect.Right) / 2f, elC = (rect.Bottom + rect.Top) / 2f;
        float az = MathHelper.ToRadians(azC), el = MathHelper.ToRadians(elC);
        var forward = Direction(azC, elC);
        // Right: level, at right angles to the azimuth; up: at right angles to
        // both, so the quad faces the head and does not lean.
        var right = new Vector3(MathF.Cos(az), 0f, MathF.Sin(az));
        var up = new Vector3(-MathF.Sin(az) * MathF.Sin(el), MathF.Cos(el), MathF.Cos(az) * MathF.Sin(el));
        var centre = forward * distanceMetres;
        var halfRight = right * (distanceMetres * MathF.Tan(MathHelper.ToRadians(rect.Width / 2f)));
        var halfUp = up * (distanceMetres * MathF.Tan(MathHelper.ToRadians(rect.Height / 2f)));
        return (centre - halfRight + halfUp, centre + halfRight + halfUp, centre - halfRight - halfUp,
            centre + halfRight - halfUp);
    }
}
