using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.ClientCore.MonoGame.Vr;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The VR HUD's geometry (knowledge/vr-hud-design.md, build step 1):
/// a marker is anchored at its object's world position, recovered from the
/// radar's pixel; it covers an angle, not a pixel count; the interim panel hangs
/// below the nose facing the head; the aim reticle is a direction, the same in
/// both eyes.</summary>
public sealed class VrHudGeometryTests
{
    private const int Width = 1280, Height = 720;
    private static readonly Matrix GameProjection =
        Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(50f), 16f / 9f, 0.5f, 10000f);
    private static readonly Vector3 CameraEye = new(1200, -40, 3500);

    private static Matrix LookingAlong(Vector3 forward) => Matrix.CreateLookAt(Vector3.Zero, forward, Vector3.Up);

    private static void Near3(Vector3 expected, Vector3 actual, float tolerance)
    {
        float miss = Vector3.Distance(expected, actual);
        Assert.True(miss < tolerance, $"expected {expected}, got {actual} ({miss} off)");
    }

    private static VrEyeView Eye(Quaternion orientation, float x, float half = 0.9f) =>
        new(orientation, new Vector3(x, 0, 0), -half, half * 0.95f, half, -half * 1.05f);

    [Fact]
    public void A_pixel_cast_back_at_its_distance_is_the_point_that_projected_there()
    {
        var view = LookingAlong(new Vector3(0.3f, -0.1f, -1f));
        var world = CameraEye + Vector3.Transform(new Vector3(140f, 60f, -900f), Matrix.Invert(view));

        Assert.True(VrHudGeometry.TryProject(world - CameraEye, view, GameProjection, Width, Height, out var pixel));
        var direction = VrHudGeometry.RayThroughPixel(pixel, Width, Height, view, GameProjection);
        float distance = (world - CameraEye).Length();

        Near3(world, CameraEye + direction * distance, 0.05f);
    }

    [Fact]
    public void An_off_centre_projection_is_inverted_with_its_off_centre_terms()
    {
        // An eye's projection is off-centre (M31, M32 not zero); the radar's wide
        // frame is built the same way.
        var eye = new VrEyeView(Quaternion.Identity, Vector3.Zero, -1.1f, 0.7f, 0.9f, -1.0f);
        var projection = VrEyeCamera.OffCentreProjection(GameProjection, eye);
        Assert.NotEqual(0f, projection.M31);
        var view = LookingAlong(Vector3.Forward);
        var world = CameraEye + new Vector3(-300f, 120f, -700f);

        Assert.True(VrHudGeometry.TryProject(world - CameraEye, view, projection, Width, Height, out var pixel));
        var direction = VrHudGeometry.RayThroughPixel(pixel, Width, Height, view, projection);

        Near3(world, CameraEye + direction * (world - CameraEye).Length(), 0.05f);
    }

    [Fact]
    public void A_contact_beside_the_game_cameras_frame_is_anchored_through_the_radars_head_turned_wide_frame()
    {
        // The head looks 60° left; the contact is there, 800 m off, well outside
        // the game camera's 50° frame but inside the headset's view.
        var gameView = LookingAlong(Vector3.Forward);
        var turned = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(60f));
        float halfW = MathHelper.ToRadians(75f), halfH = MathHelper.ToRadians(70f);
        var head = new VrEyeView(turned, Vector3.Zero, -halfW, halfW, halfH, -halfH);
        var (radarView, radarProjection, radarEye) = VrEyeCamera.ForEye(gameView, GameProjection, CameraEye, head);
        Assert.Equal(CameraEye, radarEye);
        var contact = CameraEye + Vector3.Transform(Vector3.Forward * 800f, Matrix.CreateFromQuaternion(turned));

        // The production radar projection (Game1.DrawRadar) through that frame.
        var site = RadarProjection.Project(contact, CameraEye, radarView, radarProjection, Width, Height,
            RadarBracketLayout.NativeBorderSide);
        Assert.Equal(RadarBracketLayout.RadarPlacement.OnScreen, site.Placement);
        // Through the game camera alone it would be clamped to the edge.
        Assert.Equal(RadarBracketLayout.RadarPlacement.EdgeClamped,
            RadarProjection.Project(contact, CameraEye, gameView, GameProjection, Width, Height, RadarBracketLayout.NativeBorderSide).Placement);

        var direction = VrHudGeometry.RayThroughPixel(site.Point, Width, Height, radarView, radarProjection);
        Near3(contact, CameraEye + direction * 800f, 0.1f);
    }

    [Fact]
    public void A_marker_anchored_at_its_object_lands_on_the_object_in_each_eye_and_so_has_its_disparity()
    {
        var gameView = LookingAlong(Vector3.Forward);
        var target = CameraEye + new Vector3(4f, 1f, -25f);
        foreach (float x in new[] { -0.032f, 0.032f })
        {
            var (view, projection, eyeWorld) = VrEyeCamera.ForEye(gameView, GameProjection, CameraEye, Eye(Quaternion.Identity, x));
            Assert.True(VrHudGeometry.TryProject(target - eyeWorld, view, projection, 3000, 3000, out var marker));
            // The object's own projection, as the scene pass draws it (camera-relative).
            var clip = Vector4.Transform(new Vector4(target - eyeWorld, 1f), view * projection);
            var objectPixel = new Vector2((clip.X / clip.W * 0.5f + 0.5f) * 3000, (0.5f - clip.Y / clip.W * 0.5f) * 3000);
            Assert.True(Vector2.Distance(objectPixel, marker) < 0.01f);
        }
        // The two eyes disagree on the object's pixel: that is its depth cue
        // (64 mm at 25 m is 0.15°, about 3 px here).
        var left = VrEyeCamera.ForEye(gameView, GameProjection, CameraEye, Eye(Quaternion.Identity, -0.032f));
        var right = VrEyeCamera.ForEye(gameView, GameProjection, CameraEye, Eye(Quaternion.Identity, 0.032f));
        VrHudGeometry.TryProject(target - left.Eye, left.View, left.Projection, 3000, 3000, out var l);
        VrHudGeometry.TryProject(target - right.Eye, right.View, right.Projection, 3000, 3000, out var r);
        Assert.True(MathF.Abs(l.X - r.X) > 2f, $"{l.X} against {r.X}");
    }

    [Theory]
    [InlineData(50f)]
    [InlineData(5000f)]
    public void A_far_marker_covers_the_floor_angle_whatever_its_distance(float distance)
    {
        // A 0.5 m object is under 2° across from 50 m on.
        Assert.Equal(VrHudGeometry.BracketFloorRadians, VrHudGeometry.BracketAngle(0.5f, distance), 6);
    }

    [Fact]
    public void A_near_marker_wraps_its_object_up_to_the_cap()
    {
        float wrap = VrHudGeometry.BracketAngle(10f, 100f);
        Assert.Equal(2f * MathF.Atan(1.05f * 10f / 100f), wrap, 5);
        Assert.True(wrap > VrHudGeometry.BracketFloorRadians);
        Assert.Equal(VrHudGeometry.BracketCapRadians, VrHudGeometry.BracketAngle(400f, 100f), 6);
        Assert.Equal(VrHudGeometry.BracketFloorRadians, VrHudGeometry.BracketAngle(null, 100f), 6);
    }

    [Fact]
    public void The_floor_is_about_two_degrees_for_brackets_and_six_tenths_for_text()
    {
        Assert.Equal(2f, MathHelper.ToDegrees(VrHudGeometry.BracketFloorRadians), 4);
        Assert.Equal(0.6f, MathHelper.ToDegrees(VrHudGeometry.TextFloorRadians), 4);
    }

    [Fact]
    public void A_screen_radius_turns_into_the_angle_it_covers()
    {
        // A 100 px radius on the 720-row game frame: atan(100 / (M22 * 360)).
        float radius = MathF.Atan(100f / (GameProjection.M22 * Height / 2f));
        Assert.Equal(2f * 1.05f * radius, VrHudGeometry.BracketAngleFromScreenRadius(100f, GameProjection, Height), 5);
        Assert.Equal(VrHudGeometry.BracketFloorRadians, VrHudGeometry.BracketAngleFromScreenRadius(1f, GameProjection, Height), 6);
        Assert.Equal(VrHudGeometry.BracketFloorRadians, VrHudGeometry.BracketAngleFromScreenRadius(null, GameProjection, Height), 6);
    }

    [Fact]
    public void An_angle_covers_the_pixels_the_projection_gives_it_at_the_centre()
    {
        var eye = new VrEyeView(Quaternion.Identity, Vector3.Zero, -0.9f, 0.9f, 0.9f, -0.9f);
        var projection = VrEyeCamera.OffCentreProjection(GameProjection, eye);
        float angle = MathHelper.ToRadians(2f);
        var view = LookingAlong(Vector3.Forward);
        // A point 2° above the centre direction, at any distance.
        foreach (float d in new[] { 30f, 3000f })
        {
            var up = new Vector3(0, MathF.Tan(angle) * d, -d);
            VrHudGeometry.TryProject(Vector3.Zero + new Vector3(0, 0, -d), view, projection, 2000, 2000, out var centre);
            VrHudGeometry.TryProject(up, view, projection, 2000, 2000, out var above);
            Assert.Equal(VrHudGeometry.PixelsForAngle(angle, projection, 2000), centre.Y - above.Y, 2);
        }
    }

    [Fact]
    public void The_panel_hangs_below_the_nose_at_its_distance_and_faces_the_head()
    {
        var panel = VrHudGeometry.Panel.Default;
        var (tl, tr, bl, br) = VrHudGeometry.PanelCorners(panel, Width, Height);
        var centre = (tl + tr + bl + br) / 4f;

        Assert.Equal(panel.DistanceMetres, centre.Length(), 4);
        Assert.Equal(-panel.PitchDownRadians, MathF.Atan2(centre.Y, -centre.Z), 4);
        Assert.Equal(panel.WidthMetres, Vector3.Distance(tl, tr), 4);
        Assert.Equal(panel.WidthMetres * Height / Width, Vector3.Distance(tl, bl), 4);
        Assert.True(tl.Y > bl.Y);
        Assert.True(tr.X > tl.X);
        // Facing the head: the panel's plane is square to the line from the head.
        var normal = Vector3.Normalize(Vector3.Cross(tr - tl, bl - tl));
        Assert.Equal(1f, MathF.Abs(Vector3.Dot(normal, Vector3.Normalize(centre))), 4);
    }

    [Fact]
    public void The_panel_is_about_a_metre_away_and_below_the_nose_by_default()
    {
        var panel = VrHudGeometry.Panel.Default;
        Assert.InRange(panel.DistanceMetres, 0.8f, 1.2f);
        Assert.InRange(MathHelper.ToDegrees(panel.PitchDownRadians), 10f, 35f);
    }

    [Theory]
    [InlineData("1.2,30,1.4", 1.2f, 30f, 1.4f)]
    [InlineData(" 0.9 , 15 , 1 ", 0.9f, 15f, 1f)]
    public void The_panel_can_be_tuned_from_the_environment(string text, float distance, float pitch, float width)
    {
        var panel = VrHudGeometry.Panel.Parse(text);
        Assert.Equal(distance, panel.DistanceMetres, 4);
        Assert.Equal(pitch, MathHelper.ToDegrees(panel.PitchDownRadians), 3);
        Assert.Equal(width, panel.WidthMetres, 4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,25")]
    [InlineData("0.1,25,1")]
    [InlineData("1,25,abc")]
    [InlineData("1,90,1")]
    public void An_unusable_panel_setting_is_the_default(string? text)
    {
        Assert.Equal(VrHudGeometry.Panel.Default, VrHudGeometry.Panel.Parse(text));
    }

    [Fact]
    public void The_aim_reticle_is_a_direction_so_both_eyes_see_it_at_the_same_pixel()
    {
        var turned = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(10f));
        var projection = VrEyeCamera.OffCentreProjection(GameProjection, Eye(turned, 0f));
        Assert.True(VrHudGeometry.TryProjectDirection(Vector3.Forward, VrHudGeometry.SeatView(Eye(turned, -0.032f)),
            projection, 3000, 3000, out var left));
        Assert.True(VrHudGeometry.TryProjectDirection(Vector3.Forward, VrHudGeometry.SeatView(Eye(turned, 0.032f)),
            projection, 3000, 3000, out var right));
        Assert.True(Vector2.Distance(left, right) < 0.01f);
        // The head turned left, so the nose is right of the image centre.
        Assert.True(left.X > 1500f);
    }

    [Fact]
    public void The_seat_view_holds_the_panel_still_while_the_head_turns()
    {
        var (tl, tr, bl, br) = VrHudGeometry.PanelCorners(VrHudGeometry.Panel.Default, Width, Height);
        var centre = (tl + tr + bl + br) / 4f;
        // Looking straight at the panel: pitch the head down by the panel's angle.
        var down = Quaternion.CreateFromAxisAngle(Vector3.Right, -VrHudGeometry.Panel.Default.PitchDownRadians);
        var view = VrHudGeometry.SeatView(new VrEyeView(down, Vector3.Zero, -1, 1, 1, -1));
        var inView = Vector3.Transform(centre, view);
        Assert.True(MathF.Abs(inView.X) < 1e-4f && MathF.Abs(inView.Y) < 1e-4f && inView.Z < 0f, $"{inView}");
    }

    [Theory]
    [InlineData(1f)]   // the cockpit camera's near plane
    [InlineData(10f)]  // the external cameras' near plane
    public void The_whole_panel_lies_between_the_seat_projections_depth_planes(float gameNearPlane)
    {
        var game = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(50f), 16f / 9f, gameNearPlane, 100000f);
        var (_, eyeProjection, _) = VrEyeCamera.ForEye(Matrix.Identity, game, Vector3.Zero, Eye(Quaternion.Identity, -0.032f));
        var seat = VrHudGeometry.SeatProjection(eyeProjection);
        var view = VrHudGeometry.SeatView(Eye(Quaternion.Identity, -0.032f));
        var (tl, tr, bl, br) = VrHudGeometry.PanelCorners(VrHudGeometry.Panel.Default, Width, Height);
        static float Depth(Vector3 p, Matrix m) { var c = Vector4.Transform(new Vector4(p, 1f), m); return c.Z / c.W; }
        // The game's near plane is farther than the panel's lower edge: the eye
        // projection clips at least that edge away.
        Assert.True(Depth(bl, view * eyeProjection) < 0f && Depth(br, view * eyeProjection) < 0f,
            "the lower edge is unexpectedly inside the game's depth range");
        foreach (var corner in new[] { tl, tr, bl, br })
        {
            float depth = Depth(corner, view * seat);
            Assert.True(depth > 0f && depth < 1f, $"{corner} at clip depth {depth}");
        }
        // The field of view is the eye's own, so the panel lands on the same pixels.
        Assert.True(VrHudGeometry.TryProject(bl, view, eyeProjection, Width, Height, out var a));
        Assert.True(VrHudGeometry.TryProject(bl, view, seat, Width, Height, out var b));
        Assert.True(Vector2.Distance(a, b) < 1e-2f, $"{a} vs {b}");
    }

    [Fact]
    public void In_VR_the_flat_centre_reticle_leaves_the_near_panel_for_the_far_world_reticle()
    {
        var frame = new HudFrame(0.8f, 0.6f, 0.4f, 0.3f, 0.2f, 1f,
            120f, -1f, false, false, true, 0, false, 0f, false, false,
            0f, 0f, System.Array.Empty<Allegiance.ClientCore.MonoGame.Flight.WeaponStatusRow>(), false, 0);
        var flat = HudModelAdapter.Create(frame, CameraMode.Cockpit);
        var vr = HudModelAdapter.Create(frame, CameraMode.Cockpit, centreReticleInWorld: true);
        Assert.True(flat.ReticleVisible);
        Assert.False(vr.ReticleVisible);
        Assert.False(HudRenderer.DrawsAdditiveReticle(vr, turretView: false, hasArtwork: true));
        // The rest of the cockpit indicators stay on the panel.
        Assert.True(vr.IndicatorsVisible);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(40f, 0f)]
    [InlineData(-30f, 35f)]
    public void A_marker_covers_its_angle_at_its_own_pixel_off_the_images_centre(float yawDegrees, float pitchDegrees)
    {
        const int w = 2000, h = 2000;
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(100f), 1f, 0.5f, 1000f);
        var direction = Vector3.Transform(Vector3.Forward,
            Matrix.CreateRotationX(MathHelper.ToRadians(pitchDegrees)) * Matrix.CreateRotationY(MathHelper.ToRadians(-yawDegrees)));
        Assert.True(VrHudGeometry.TryProject(direction * 100f, Matrix.Identity, projection, w, h, out var at));
        float angle = MathHelper.ToRadians(2f);
        float size = VrHudGeometry.PixelsForAngleAt(angle, at, projection, w, h);
        // Cast back the two ends of that span, measured along the pixel's own up-down
        // direction on screen: they are the requested angle apart.
        var ray = VrHudGeometry.RayThroughPixel(at, w, h, Matrix.Identity, projection);
        var up = Vector3.Normalize(Vector3.Cross(Vector3.Cross(ray, Vector3.UnitY), ray));
        Assert.True(VrHudGeometry.TryProject(ray + up * 1e-3f, Matrix.Identity, projection, w, h, out var towardUp));
        var along = Vector2.Normalize(towardUp - at);
        var a = VrHudGeometry.RayThroughPixel(at + along * size / 2f, w, h, Matrix.Identity, projection);
        var b = VrHudGeometry.RayThroughPixel(at - along * size / 2f, w, h, Matrix.Identity, projection);
        float covered = MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f));
        Assert.True(MathF.Abs(covered - angle) < angle * 0.02f, $"covers {MathHelper.ToDegrees(covered)} deg");
        float atCentre = VrHudGeometry.PixelsForAngle(angle, projection, h);
        if (yawDegrees == 0f && pitchDegrees == 0f) Assert.InRange(size / atCentre, 0.99f, 1.01f);
        else Assert.True(size > atCentre * 1.1f, $"{size} px off-centre, {atCentre} px at the centre");
    }

    [Fact]
    public void A_point_behind_the_eye_gets_no_pixel()
    {
        Assert.False(VrHudGeometry.TryProject(Vector3.Backward * 50f, Matrix.Identity, GameProjection, Width, Height, out _));
        Assert.False(VrHudGeometry.TryProjectDirection(Vector3.Backward, Matrix.Identity, GameProjection, Width, Height, out _));
    }

    [Fact]
    public void A_far_marker_is_pulled_in_to_the_clamp_along_the_line_to_its_object()
    {
        var relative = new Vector3(300f, -200f, -2400f);
        var anchor = VrHudGeometry.MarkerAnchor(relative, MathHelper.ToRadians(2f));
        Assert.InRange(anchor.Length(), VrHudGeometry.MarkerDepthClampMetres - 0.01f, VrHudGeometry.MarkerDepthClampMetres + 0.01f);
        Near3(Vector3.Normalize(relative), Vector3.Normalize(anchor), 1e-4f);
    }

    [Fact]
    public void A_near_marker_sits_in_front_of_the_sphere_its_bracket_frames()
    {
        // A ship 30 m away whose bracket covers 10 degrees: its hull reaches
        // about 2.6 m toward the head, so the bracket must be nearer than 27.4 m,
        // and not far nearer.
        var relative = new Vector3(0f, 0f, -30f);
        float angle = MathHelper.ToRadians(10f);
        float hull = 30f * MathF.Tan(angle / 2f);
        var anchor = VrHudGeometry.MarkerAnchor(relative, angle);
        Assert.InRange(anchor.Length(), 30f - hull - 0.01f, 30f - hull + 0.01f);
        Near3(Vector3.Normalize(relative), Vector3.Normalize(anchor), 1e-5f);
    }

    [Fact]
    public void A_nearer_point_gets_a_smaller_depth_buffer_value()
    {
        var view = LookingAlong(new Vector3(0.2f, 0f, -1f));
        float near = VrHudGeometry.BufferDepth(new Vector3(0f, 0f, -20f), view, GameProjection);
        float clamp = VrHudGeometry.BufferDepth(new Vector3(0f, 0f, -100f), view, GameProjection);
        float far = VrHudGeometry.BufferDepth(new Vector3(0f, 0f, -3000f), view, GameProjection);
        Assert.True(0f < near && near < clamp && clamp < far && far < 1f, $"{near} {clamp} {far}");
        Assert.True(VrHudGeometry.BufferDepth(new Vector3(0f, 0f, 40f), view, GameProjection) < 0f);
    }

    [Theory]
    [InlineData(1f, 0f, -1f, 1f, 0f)]
    [InlineData(0f, 1f, 0.5f, 0f, -1f)]
    [InlineData(0f, 0f, 1f, 0f, 1f)]
    public void An_off_view_arrow_sits_on_the_ring_on_the_objects_side_and_points_there(
        float x, float y, float z, float imageX, float imageY)
    {
        float ring = MathHelper.ToRadians(30f);
        var (point, direction) = VrHudGeometry.OffViewArrow(new Vector3(x, y, z), ring, 3f);
        Assert.InRange(point.Length(), 2.999f, 3.001f);
        float offAxis = MathF.Acos(Vector3.Dot(Vector3.Normalize(point), Vector3.Forward));
        Assert.InRange(offAxis, ring - 1e-4f, ring + 1e-4f);
        Assert.InRange(Vector2.Distance(new Vector2(imageX, imageY), direction), 0f, 1e-4f);
        // The ring point lies on the same side of the view's centre the arrow points to.
        Assert.True(Vector2.Dot(new Vector2(point.X, -point.Y), direction) > 0f);
    }

    [Theory]
    [InlineData(40f, 0f, 40f)]      // under the reticle: its near side, about 39.3 m
    [InlineData(8f, 0f, 15f)]       // a fly-by: held at 15 m
    [InlineData(900f, 0f, 100f)]    // far: held at 100 m
    [InlineData(40f, 6f, 0f)]       // 6 degrees off the boresight: no depth (infinity)
    public void A_target_under_the_reticle_gives_it_its_depth_held_between_15_and_100_m(
        float distance, float offDegrees, float expectedMetres)
    {
        var nose = Vector3.Normalize(new Vector3(0.2f, 0.1f, -1f));
        var off = Vector3.Transform(nose, Matrix.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(offDegrees)));
        float bracket = MathHelper.ToRadians(2f);
        float inverse = VrHudGeometry.ReticleInverseDistance(nose, off * distance, bracket);
        if (expectedMetres == 0f) { Assert.Equal(0f, inverse); return; }
        float tolerance = expectedMetres == 40f ? 1f : 0.01f;
        Assert.InRange(1f / inverse, expectedMetres - tolerance, expectedMetres + 0.01f);
    }

    [Fact]
    public void A_near_ships_hull_under_the_reticle_gives_it_depth_though_its_centre_is_off_the_cone()
    {
        // 20 m away, 5 degrees off, with a 16 degree bracket: its hull is under the reticle.
        var nose = Vector3.Forward;
        var off = Vector3.Transform(nose, Matrix.CreateRotationY(MathHelper.ToRadians(5f)));
        Assert.True(VrHudGeometry.ReticleInverseDistance(nose, off * 20f, MathHelper.ToRadians(16f)) > 0f);
        Assert.Equal(0f, VrHudGeometry.ReticleInverseDistance(nose, off * 20f, MathHelper.ToRadians(2f)));
        Assert.Equal(0f, VrHudGeometry.ReticleInverseDistance(nose, -nose * 20f, MathHelper.ToRadians(16f)));
    }

    [Fact]
    public void The_reticle_depth_eases_to_its_target_in_about_a_fifth_of_a_second_and_back_to_infinity()
    {
        float target = 1f / 30f, current = 0f;
        for (int frame = 0; frame < 15; frame++) current = VrHudGeometry.SmoothInverseDistance(current, target, 1f / 75f);
        // After one time constant (0.2 s) it has covered about 63% of the way.
        Assert.InRange(current / target, 0.60f, 0.66f);
        Assert.True(VrHudGeometry.SmoothInverseDistance(current, target, 0f) == current);
        for (int frame = 0; frame < 150; frame++) current = VrHudGeometry.SmoothInverseDistance(current, 0f, 1f / 75f);
        Assert.True(current < target * 1e-4f);
    }

    [Fact]
    public void A_capped_bracket_is_still_anchored_in_front_of_its_object()
    {
        // 40 m away with a 20 m radius: the object wraps about 55 degrees, the
        // bracket is drawn at the 20 degree cap. The anchor must come from the
        // wrap, or the object's own hull hides its marker in the depth test.
        const float distance = 40f, radius = 20f;
        var relative = new Vector3(0f, 0f, -distance);
        float drawn = VrHudGeometry.BracketAngle(radius, distance);
        float wrap = VrHudGeometry.WrapAngle(radius, distance);
        Assert.Equal(VrHudGeometry.BracketCapRadians, drawn);
        Assert.True(wrap > drawn);
        float anchor = VrHudGeometry.MarkerAnchor(relative, MathF.Max(drawn, wrap)).Length();
        Assert.InRange(anchor, distance - 1.05f * radius - 0.01f, distance - radius - 0.5f);
        Assert.True(VrHudGeometry.MarkerAnchor(relative, drawn).Length() > distance - radius,
            "the drawn angle alone would put the anchor inside the object");
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(37f)]
    [InlineData(800f)]
    [InlineData(10000f)]
    public void The_depth_buffer_value_is_the_perspective_depth_for_the_projections_planes(float distance)
    {
        // CreatePerspectiveFieldOfView maps view depth d to far (d - near) / (d (far - near)):
        // 0 at the near plane, 1 at the far plane.
        const float near = 0.5f, far = 10000f;
        float expected = far * (distance - near) / (distance * (far - near));
        var view = LookingAlong(new Vector3(-0.4f, 0.2f, -1f));
        // A point off the view's axis: its depth is set by its view-space z alone.
        var point = Vector3.TransformNormal(new Vector3(0.3f * distance, -0.2f * distance, -distance), Matrix.Invert(view));
        Assert.InRange(VrHudGeometry.BufferDepth(point, view, GameProjection), expected - 1e-5f, expected + 1e-5f);
    }

    [Fact]
    public void A_markers_anchor_uses_its_radius_seen_from_the_head_not_from_the_ships_centre()
    {
        // The cockpit eye is 14 m ahead of the ship's centre. A 20 m radius
        // object 40 m from the ship is 26 m from the head: a wrap angle measured
        // from the ship would put the anchor inside the hull as the head sees it.
        const float fromShip = 40f, fromHead = 26f, radius = 20f;
        var relative = new Vector3(0f, 0f, -fromHead);
        float drawn = VrHudGeometry.BracketAngle(radius, fromShip);
        float angle = VrHudGeometry.AnchorAngle(relative, drawn, 0f, radius);
        float anchor = VrHudGeometry.MarkerAnchor(relative, angle).Length();
        Assert.True(anchor < fromHead - radius, $"anchor {anchor} m, hull from {fromHead - radius} m");
        float fromShipWrap = VrHudGeometry.MarkerAnchor(relative, VrHudGeometry.WrapAngle(radius, fromShip)).Length();
        Assert.True(fromShipWrap > fromHead - radius, "a wrap measured from the ship would be inside the hull");

        // A locked 30 m hull 85 m from the ship, 71 m from the head (the reviewer's case).
        var locked = new Vector3(0f, 0f, -71f);
        float lockAngle = VrHudGeometry.AnchorAngle(locked, VrHudGeometry.BracketAngle(15f, 85f), 0f, 15f);
        Assert.True(VrHudGeometry.MarkerAnchor(locked, lockAngle).Length() < 71f - 15f);
    }

    [Fact]
    public void A_large_objects_hull_under_the_reticle_gives_it_depth_though_its_bracket_is_capped()
    {
        // 60 m from the head, 50 m radius, centre 25 degrees off the boresight:
        // the hull is under the reticle, the drawn bracket is capped at 20 degrees.
        var nose = Vector3.Forward;
        var off = Vector3.Transform(nose, Matrix.CreateRotationY(MathHelper.ToRadians(25f)));
        float drawn = VrHudGeometry.BracketAngle(50f, 60f);
        Assert.Equal(VrHudGeometry.BracketCapRadians, drawn);
        Assert.True(VrHudGeometry.ReticleInverseDistance(nose, off * 60f, drawn, 0f, 50f) > 0f);
        Assert.Equal(0f, VrHudGeometry.ReticleInverseDistance(nose, off * 60f, drawn));
    }
}
