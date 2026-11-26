using Allegiance.ClientCore.MonoGame.Vr;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The VR HUD's zones (layer 4, knowledge/vr-hud-design.md "Layout
/// criteria for the VR HUD"): the zone table keeps its gaps and reach, and a
/// zone quad shows its angles at its depth.</summary>
public sealed class VrHudZonesTests
{
    [Fact]
    public void No_two_zones_overlap_and_each_pair_keeps_the_gap()
    {
        var zones = VrHudZones.Zones;
        for (int i = 0; i < zones.Count; i++)
            for (int j = i + 1; j < zones.Count; j++)
            {
                float gap = VrHudZones.Gap(zones[i], zones[j]);
                Assert.True(gap >= VrHudZones.GapDegrees, $"{zones[i].Id} and {zones[j].Id}: gap {gap}°");
            }
    }

    [Fact]
    public void Gap_is_negative_for_overlapping_zones()
    {
        var a = new VrHudZones.Zone(VrHudZones.ZoneId.Comms, -10f, 10f, -5f, 5f, 2f);
        var b = new VrHudZones.Zone(VrHudZones.ZoneId.Target, 5f, 20f, 0f, 10f, 2f);
        Assert.True(VrHudZones.Gap(a, b) < 0f);
        var c = new VrHudZones.Zone(VrHudZones.ZoneId.Target, 12f, 20f, -30f, 30f, 2f);
        Assert.Equal(2f, VrHudZones.Gap(a, c), 3);
    }

    [Fact]
    public void Every_zone_lies_within_the_reach()
    {
        foreach (var zone in VrHudZones.Zones)
        {
            Assert.True(zone.Left >= -VrHudZones.ReachDegrees && zone.Right <= VrHudZones.ReachDegrees, zone.Id.ToString());
            Assert.True(zone.Bottom >= -VrHudZones.ReachDegrees, zone.Id.ToString());
            Assert.True(zone.Left < zone.Right && zone.Bottom < zone.Top, zone.Id.ToString());
        }
    }

    [Fact]
    public void Comms_sits_above_the_flight_block_and_the_panels_are_at_two_metres()
    {
        var a = VrHudZones.Get(VrHudZones.ZoneId.FlightBlock);
        var b = VrHudZones.Get(VrHudZones.ZoneId.Comms);
        Assert.True(b.Bottom > a.Top);
        Assert.Equal(VrHudGeometry.FlightBlockDistanceMetres, a.DistanceMetres);
        foreach (var zone in VrHudZones.Zones)
            if (zone.Id != VrHudZones.ZoneId.FlightBlock) Assert.Equal(2f, zone.DistanceMetres);
    }

    [Fact]
    public void Direction_points_where_the_angles_say()
    {
        AssertNear(new Vector3(0, 0, -1), VrHudZones.Direction(0f, 0f));
        AssertNear(new Vector3(1, 0, 0), VrHudZones.Direction(90f, 0f));
        AssertNear(new Vector3(0, 1, 0), VrHudZones.Direction(0f, 90f));
        var d = VrHudZones.Direction(30f, -20f);
        Assert.Equal(30f, MathHelper.ToDegrees(MathF.Atan2(d.X, -d.Z)), 3);
        Assert.Equal(-20f, MathHelper.ToDegrees(MathF.Asin(d.Y)), 3);
    }

    [Theory]
    [InlineData(-14f, 14f, -40f, -24f)]
    [InlineData(16f, 42f, -42f, -24f)]
    [InlineData(-16f, 16f, 16f, 30f)]
    public void A_quad_faces_the_head_at_its_distance_and_spans_its_angles(float left, float right, float bottom, float top)
    {
        var rect = new VrHudZones.AngleRect(left, right, bottom, top);
        var (tl, tr, bl, br) = VrHudZones.Quad(rect, 2f);
        var centre = (tl + tr + bl + br) / 4f;
        Assert.Equal(2f, centre.Length(), 3);
        // Faces the head: both edges at right angles to the line to the centre.
        var toCentre = Vector3.Normalize(centre);
        Assert.Equal(0f, Vector3.Dot(Vector3.Normalize(tr - tl), toCentre), 3);
        Assert.Equal(0f, Vector3.Dot(Vector3.Normalize(tl - bl), toCentre), 3);
        // Upright: the top edge is level and the up edge leans no way across.
        Assert.Equal(0f, (tr - tl).Y, 4);
        Assert.True((tl - bl).Y > 0f);
        // The edge midpoints are seen at the rect's half-angles from the centre.
        float across = MathHelper.ToDegrees(MathF.Acos(Vector3.Dot(Vector3.Normalize((tr + br) / 2f), toCentre)));
        float upDown = MathHelper.ToDegrees(MathF.Acos(Vector3.Dot(Vector3.Normalize((tl + tr) / 2f), toCentre)));
        Assert.Equal(rect.Width / 2f, across, 2);
        Assert.Equal(rect.Height / 2f, upDown, 2);
        // Right is right and top is up.
        Assert.True(Vector3.Dot(tr - tl, VrHudZones.Direction((left + right) / 2f + 90f, 0f)) > 0f);
        Assert.True(Vector3.Dot(tl - bl, Vector3.Up) > 0f);
    }

    [Fact]
    public void A_pixel_covers_the_step_one_panel_angle_and_small_text_stays_above_the_floor()
    {
        // The step 1 panel: 1.1 m across at 1 m for a 1280-wide image.
        float panelDegrees = MathHelper.ToDegrees(2f * MathF.Atan(0.55f));
        Assert.Equal(panelDegrees / 1280f, VrHudZones.DegreesPerPixel, 5);
        // The small font's 14 px line, at the scale every zone gets at 1280 by 720.
        Assert.True(14f * VrHudZones.DegreesPerPixel >= 0.6f);
    }

    public static TheoryData<int, int> ImageSizes => new() { { 1280, 720 }, { 1920, 1080 }, { 1600, 900 }, { 2560, 1440 } };

    [Theory]
    [MemberData(nameof(ImageSizes))]
    public void Cutouts_lie_in_the_image_and_no_two_overlap(int width, int height)
    {
        var cutouts = VrHudZones.ClassicCutouts(width, height);
        var image = new Rectangle(0, 0, width, height);
        foreach (var c in cutouts)
        {
            Assert.True(c.Source.Width > 0 && c.Source.Height > 0, c.Name);
            Assert.True(image.Contains(c.Source), $"{c.Name} {c.Source} leaves {image}");
        }
        for (int i = 0; i < cutouts.Count; i++)
            for (int j = i + 1; j < cutouts.Count; j++)
                Assert.False(cutouts[i].Source.Intersects(cutouts[j].Source),
                    $"{cutouts[i].Name} {cutouts[i].Source} overlaps {cutouts[j].Name} {cutouts[j].Source}");
    }

    [Theory]
    [MemberData(nameof(ImageSizes))]
    public void Every_cutout_is_placed_once_inside_its_zone_and_no_two_overlap(int width, int height)
    {
        var cutouts = VrHudZones.ClassicCutouts(width, height);
        var placements = VrHudZones.Place(cutouts);
        Assert.Equal(cutouts.Count, placements.Count);
        foreach (var p in placements)
        {
            var zone = VrHudZones.Get(p.Cutout.Zone);
            Assert.NotEqual(VrHudZones.ZoneId.FlightBlock, zone.Id);
            Assert.True(p.Where.Within(zone), $"{p.Cutout.Name} at {p.Where} leaves {zone}");
            Assert.True(p.Scale > 0f && p.Scale <= 1f, p.Cutout.Name);
            // Shown at its own aspect.
            Assert.Equal((float)p.Cutout.Source.Width / p.Cutout.Source.Height, p.Where.Width / p.Where.Height, 2);
        }
        for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
                Assert.False(placements[i].Where.Overlaps(placements[j].Where),
                    $"{placements[i].Cutout.Name} overlaps {placements[j].Cutout.Name}");
    }

    [Fact]
    public void At_1280_by_720_every_zone_fits_its_content_unscaled()
    {
        foreach (var p in VrHudZones.Place(VrHudZones.ClassicCutouts(1280, 720)))
            Assert.True(p.Scale == 1f, $"{p.Cutout.Name} needed scale {p.Scale}");
    }

    [Fact]
    public void The_zones_hold_the_elements_the_accepted_table_gives_them()
    {
        var byName = new Dictionary<string, VrHudZones.ZoneId>();
        foreach (var c in VrHudZones.ClassicCutouts(1280, 720)) byName[c.Name] = c.Zone;
        Assert.Equal(VrHudZones.ZoneId.Comms, byName["comms"]);
        Assert.Equal(VrHudZones.ZoneId.Target, byName["target"]);
        Assert.Equal(VrHudZones.ZoneId.Target, byName["port lines"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["minimap"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["location"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["orders and badges"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["camera"]);
        Assert.Equal(VrHudZones.ZoneId.Systems, byName["systems"]);
        Assert.Equal(VrHudZones.ZoneId.Systems, byName["systems low"]);
    }

    [Fact]
    public void Each_zone_pushes_its_content_toward_the_flight_block()
    {
        var placements = VrHudZones.Place(VrHudZones.ClassicCutouts(1280, 720));
        foreach (var layout in VrHudZones.Layouts)
        {
            var zone = VrHudZones.Get(layout.Zone);
            float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
            foreach (var p in placements)
                if (p.Cutout.Zone == layout.Zone)
                {
                    left = MathF.Min(left, p.Where.Left); right = MathF.Max(right, p.Where.Right);
                    bottom = MathF.Min(bottom, p.Where.Bottom); top = MathF.Max(top, p.Where.Top);
                }
            switch (layout.Zone)
            {
                case VrHudZones.ZoneId.Comms: Assert.Equal(zone.Bottom, bottom, 3); break;
                case VrHudZones.ZoneId.Navigation: Assert.Equal(zone.Right, right, 3); Assert.Equal(zone.Top, top, 3); break;
                case VrHudZones.ZoneId.Systems: Assert.Equal(zone.Left, left, 3); Assert.Equal(zone.Top, top, 3); break;
                default: Assert.Equal(zone.Top, top, 3); Assert.Equal(0f, (left + right) / 2f, 3); break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(ImageSizes))]
    public void The_two_systems_parts_join_in_the_zone_as_they_meet_in_the_image(int width, int height)
    {
        VrHudZones.Placement upper = default, lower = default;
        foreach (var p in VrHudZones.Place(VrHudZones.ClassicCutouts(width, height)))
            if (p.Cutout.Name == "systems") upper = p;
            else if (p.Cutout.Name == "systems low") lower = p;
        Assert.Equal(upper.Cutout.Source.Bottom, lower.Cutout.Source.Top);
        Assert.Equal(upper.Cutout.Source.Right, lower.Cutout.Source.Right);
        Assert.Equal(upper.Where.Bottom, lower.Where.Top, 3);
        Assert.Equal(upper.Where.Right, lower.Where.Right, 3);
    }

    [Theory]
    [MemberData(nameof(ImageSizes))]
    [InlineData(1280, 600)]
    public void The_target_cutout_holds_the_mouse_stick_notice_above_the_pane(int width, int height)
    {
        // HudRenderer draws the notice 140 px above the pane's top, unscaled.
        float paneTop = Rendering.TargetPaneLayout.TopY(width, height);
        foreach (var c in VrHudZones.ClassicCutouts(width, height))
            if (c.Name == "target") Assert.True(c.Source.Top <= paneTop - 142f, $"top {c.Source.Top}, pane {paneTop}");
        Assert.True(VrHudZones.TargetMarginPixels(0.9f) >= 142f);
    }

    [Fact]
    public void A_zone_too_small_for_its_content_scales_it_down_to_fit()
    {
        var big = new[]
        {
            new VrHudZones.Cutout("wide", VrHudZones.ZoneId.Comms, 0, new Rectangle(0, 0, 2000, 100)),
            new VrHudZones.Cutout("tall", VrHudZones.ZoneId.Systems, 0, new Rectangle(0, 0, 100, 2000)),
        };
        foreach (var p in VrHudZones.Place(big))
        {
            Assert.True(p.Scale < 1f);
            Assert.True(p.Where.Within(VrHudZones.Get(p.Cutout.Zone)));
        }
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"expected {expected}, got {actual}");
    }
}
