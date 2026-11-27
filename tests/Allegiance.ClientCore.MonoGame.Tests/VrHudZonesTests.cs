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
            float most = zone.Id == VrHudZones.ZoneId.Target ? VrHudZones.TargetMaxScale : 1f;
            Assert.True(p.Scale > 0f && p.Scale <= most, $"{p.Cutout.Name} at scale {p.Scale}");
            // Shown at its own aspect.
            Assert.Equal((float)p.Cutout.Source.Width / p.Cutout.Source.Height, p.Where.Width / p.Where.Height, 2);
        }
        for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
                Assert.False(placements[i].Where.Overlaps(placements[j].Where),
                    $"{placements[i].Cutout.Name} overlaps {placements[j].Cutout.Name}");
    }

    [Fact]
    public void At_1280_by_720_every_zone_but_the_target_fits_its_content_unscaled()
    {
        foreach (var p in VrHudZones.Place(VrHudZones.ClassicCutouts(1280, 720)))
            if (p.Cutout.Zone != VrHudZones.ZoneId.Target)
                Assert.True(p.Scale == 1f, $"{p.Cutout.Name} needed scale {p.Scale}");
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1280, 534)]
    [InlineData(1440, 740)]
    public void The_target_pane_alone_fills_its_zone_grown_to_at_least_one_and_a_half(int width, int height)
    {
        // ragesaq, 2026-11-27: the target pane has priority and is scaled up.
        var placements = VrHudZones.ClassicLayout(width, height, out var reason);
        Assert.True(placements is not null, reason);
        var inTarget = placements!.Where(p => p.Cutout.Zone == VrHudZones.ZoneId.Target).ToList();
        var pane = Assert.Single(inTarget);
        Assert.Equal("target", pane.Cutout.Name);
        Assert.True(pane.Scale >= 1.5f, $"scale {pane.Scale}");
        var zone = VrHudZones.Get(VrHudZones.ZoneId.Target);
        // Grown until it meets the zone across or up and down, or reaches the cap.
        Assert.True(pane.Scale == VrHudZones.TargetMaxScale || Math.Abs(pane.Where.Width - zone.Width) < 1e-2f
            || Math.Abs(pane.Where.Height - zone.Height) < 1e-2f, $"{pane.Where} in {zone}");
    }

    [Fact]
    public void The_zones_hold_the_elements_the_accepted_table_gives_them()
    {
        var byName = new Dictionary<string, VrHudZones.ZoneId>();
        foreach (var c in VrHudZones.ClassicCutouts(1280, 720)) byName[c.Name] = c.Zone;
        Assert.Equal(VrHudZones.ZoneId.Comms, byName["comms"]);
        Assert.Equal(VrHudZones.ZoneId.Target, byName["target"]);
        Assert.Equal(VrHudZones.ZoneId.Comms, byName["port lines"]);
        Assert.Equal(VrHudZones.ZoneId.Comms, byName["target notices"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["minimap"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["location"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["orders and badges"]);
        Assert.Equal(VrHudZones.ZoneId.Navigation, byName["camera"]);
        Assert.Equal(VrHudZones.ZoneId.Systems, byName["systems"]);
        Assert.Equal(VrHudZones.ZoneId.Systems, byName["systems low"]);
        // Review round 3: the five status lines are in the systems cut-out, so
        // a cut-out of their own at their nominal place would be empty.
        Assert.DoesNotContain("status", byName.Keys);
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
    [InlineData(1280, 534)]
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
        // The notices' cut-out ends where the pane's begins.
        float paneTop = Rendering.TargetPaneLayout.TopY(width, height);
        var cutouts = VrHudZones.ClassicCutouts(width, height);
        var notices = cutouts.Single(c => c.Name == "target notices").Source;
        var pane = cutouts.Single(c => c.Name == "target").Source;
        Assert.True(notices.Top <= paneTop - 142f, $"top {notices.Top}, pane {paneTop}");
        Assert.Equal(notices.Bottom, pane.Top);
        Assert.True(pane.Top <= paneTop, $"pane cut-out {pane.Top}, pane {paneTop}");
        Assert.Equal(height, pane.Bottom);
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

    [Fact]
    public void The_zones_hold_the_default_1280_by_720_image()
    {
        var placements = VrHudZones.ClassicLayout(1280, 720, out var reason);
        Assert.True(placements is not null, reason);
        Assert.Equal(VrHudZones.ClassicCutouts(1280, 720).Count, placements!.Count);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1600, 900)]
    [InlineData(2560, 1440)]
    public void A_larger_image_whose_zones_would_shrink_the_text_is_refused(int width, int height)
    {
        // Rule 6: a zone never shrinks its text under the floor.
        Assert.Null(VrHudZones.ClassicLayout(width, height, out var reason));
        Assert.Contains("floor", reason);
    }

    [Theory]
    [InlineData(800, 480)]
    [InlineData(640, 360)]
    public void A_smaller_image_whose_cutouts_overlap_or_leave_it_is_refused(int width, int height)
    {
        Assert.Null(VrHudZones.ClassicLayout(width, height, out var reason));
        Assert.True(reason!.Contains("share pixels") || reason.Contains("not inside"), reason);
    }

    [Fact]
    public void A_cutout_off_the_image_is_refused_before_it_is_sampled()
    {
        // 400 px across: the 480 px comms strip starts left of the image.
        Assert.Null(VrHudZones.ClassicLayout(400, 300, out var reason));
        Assert.Contains("'comms'", reason);
        Assert.Contains("not inside", reason);
    }

    [Fact]
    public void The_floor_is_the_line_between_zones_and_the_panel()
    {
        // Comms runs to 0.40 h + 22: at 740 px high its 14 px line keeps 0.6°,
        // at 768 it would fall to about 0.59°.
        Assert.NotNull(VrHudZones.ClassicLayout(1280, 740, out var reason));
        Assert.Null(VrHudZones.ClassicLayout(1280, 768, out reason));
        Assert.Contains("floor", reason);
    }

    [Fact]
    public void Of_the_sweep_the_zones_are_used_for_exactly_the_sizes_the_note_gives()
    {
        // knowledge/vr-hud-design.md, "Which image sizes get zones".
        var used = new List<string>();
        foreach (var row in SweepSizes())
            if (VrHudZones.ClassicLayout((int)row[0], (int)row[1], out _) is not null) used.Add($"{row[0]}x{row[1]}");
        Assert.Equal(new[] { "1280x534", "1280x600", "1280x680", "1280x720", "1366x534", "1366x600", "1366x680", "1366x720",
            "1440x534", "1440x600", "1440x680", "1440x720" }, used);
        // The edges: 1275 px across (the systems cut-out, kept left of the
        // weapon status list, meets comms below it), 534 to 760 px high, and
        // the widest image whose systems zone keeps the text floor, which
        // grows with the height.
        foreach (var (w, h) in new[] { (1275, 720), (1280, 760), (1440, 740), (1486, 534), (1534, 720), (1552, 760) })
            Assert.True(VrHudZones.ClassicLayout(w, h, out var reason) is not null, $"{w}x{h}: {reason}");
        foreach (var (w, h) in new[] { (1487, 534), (1535, 720), (1553, 760) })
        {
            Assert.Null(VrHudZones.ClassicLayout(w, h, out var wide));
            Assert.Contains("floor", wide);
        }
        Assert.Null(VrHudZones.ClassicLayout(1274, 720, out var narrow));
        Assert.Contains("share pixels", narrow);
        Assert.Null(VrHudZones.ClassicLayout(1280, 533, out var low));
        Assert.Contains("share pixels", low);
        Assert.Null(VrHudZones.ClassicLayout(1280, 761, out var tall));
        Assert.Contains("floor", tall);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1280, 534)]
    [InlineData(1440, 740)]
    public void The_text_layout_puts_the_status_lines_in_the_systems_cutout(int width, int height)
    {
        // HudRenderer.PrepareLayout keeps the chat pane's slot and a 320 by
        // 120 px box at the top for the afterburner, power, weapon mode,
        // mining and time dilation lines; the lines are then placed through
        // the layout (not as fixed anchors), so it moves them out of that box.
        var layout = new Rendering.HudTextLayout();
        layout.Reset(width, height);
        int lines = Rendering.ChatPaneLayout.DefaultLines;
        layout.Reserve(new Rectangle(Rendering.ChatPaneLayout.Left(width), Rendering.ChatPaneLayout.Top(lines),
            Rendering.ChatPaneLayout.Width, Rendering.ChatPaneLayout.Height(lines)));
        float centre = width / 2f + Rendering.ChatPaneLayout.Width - Rendering.ChatPaneLayout.LeftFromCentre + 16f + 110f;
        layout.Reserve(new Rectangle((int)(centre - 160f), 6, 320, 120));
        var systems = VrHudZones.ClassicCutouts(width, height).Single(c => c.Name == "systems").Source;
        // The widest of each, in the medium Verdana (12 px lines).
        foreach (var (text, wide, y) in new[] { ("AFTERBURNER ACTIVE", 130f, 20f), ("PWR 100%", 60f, 34f),
                     ("WEAPONS: MOUNT 4", 115f, 58f), ("MINING", 48f, 82f), ("TIME DILATION 100%", 130f, 16f) })
        {
            var box = layout.Place(text, new Vector2(wide, 12f), new Vector2(centre - wide / 2f, y));
            Assert.True(systems.Contains(box), $"{text} at {box} is not in the systems cut-out {systems}");
        }
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1366, 720)]
    [InlineData(1440, 740)]
    [InlineData(1180, 720)]
    public void The_systems_cutout_starts_no_further_right_than_the_weapon_status_list_or_the_zones_are_refused(int width, int height)
    {
        // The flat game draws the list's panel from here at 1280 across.
        Assert.Equal(1280 - 380 - 18, VrHudZones.WeaponListLeft(1280));
        var placements = VrHudZones.ClassicLayout(width, height, out var reason);
        if (placements is null)
        {
            Assert.Contains("share pixels", reason);
            return;
        }
        var systems = placements.Single(p => p.Cutout.Name == "systems").Cutout.Source;
        Assert.True(systems.Left <= VrHudZones.WeaponListLeft(width), $"{systems} starts right of the list");
    }

    public static TheoryData<int, int> SweepSizes()
    {
        var sizes = new TheoryData<int, int>();
        foreach (int w in new[] { 640, 800, 1024, 1140, 1200, 1280, 1366, 1440, 1600, 1920 })
            foreach (int h in new[] { 360, 480, 534, 600, 680, 720, 768, 800, 900, 1080 })
                sizes.Add(w, h);
        return sizes;
    }

    [Theory]
    [MemberData(nameof(SweepSizes))]
    public void Wherever_the_zones_are_used_the_cutouts_are_disjoint_and_text_keeps_the_floor(int width, int height)
    {
        var placements = VrHudZones.ClassicLayout(width, height, out _);
        if (placements is null) return;
        var image = new Rectangle(0, 0, width, height);
        foreach (var p in placements)
        {
            Assert.True(image.Contains(p.Cutout.Source) && p.Cutout.Source.Width > 0 && p.Cutout.Source.Height > 0, p.Cutout.Name);
            Assert.True(p.Where.Within(VrHudZones.Get(p.Cutout.Zone)), p.Cutout.Name);
            float line = VrHudZones.SmallLinePixels * p.Where.Height / p.Cutout.Source.Height;
            Assert.True(line >= VrHudZones.TextFloorDegrees - 1e-4f, $"{p.Cutout.Name}: a 14 px line is {line}°");
        }
        for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
                Assert.False(placements[i].Cutout.Source.Intersects(placements[j].Cutout.Source),
                    $"{placements[i].Cutout.Name} and {placements[j].Cutout.Name} share pixels");
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"expected {expected}, got {actual}");
    }
}
