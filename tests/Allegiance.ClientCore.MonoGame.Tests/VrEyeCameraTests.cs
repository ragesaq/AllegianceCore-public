using Allegiance.ClientCore.MonoGame.Vr;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>How the game's camera becomes an eye's camera: the head's turn
/// applies in the camera's frame, the eye's offset moves the point the scene is
/// drawn relative to, and the headset's field of view replaces the game's while
/// its depth mapping stays.</summary>
public sealed class VrEyeCameraTests
{
    private const float Near = 0.5f;
    private const float Far = 10000f;
    private static readonly Matrix Projection = Matrix.CreatePerspectiveFieldOfView(1.2f, 16f / 9f, Near, Far);
    private static readonly Vector3 ShipPosition = new(1200, -40, 3500);

    private static VrEyeView Eye(Quaternion orientation, Vector3 position, float half = 0.7f) =>
        new(orientation, position, -half, half, half, -half);

    // A camera-relative view (P207): it looks from the origin along a direction.
    private static Matrix LookingAlong(Vector3 forward) => Matrix.CreateLookAt(Vector3.Zero, forward, Vector3.Up);

    private static Vector3 Forward(Matrix view) => Vector3.TransformNormal(Vector3.Forward, Matrix.Invert(view));

    private static void Near3(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
        Assert.True(Vector3.Distance(expected, actual) < tolerance, $"expected {expected}, got {actual}");

    [Fact]
    public void An_untracked_head_at_the_origin_sees_what_the_game_camera_sees()
    {
        var view = LookingAlong(new Vector3(1, 0.2f, -0.5f));
        var (eyeView, _, eye) = VrEyeCamera.ForEye(view, Projection, ShipPosition, Eye(Quaternion.Identity, Vector3.Zero));

        for (int i = 0; i < 16; i++)
            Assert.Equal(view[i], eyeView[i], 4);
        Assert.Equal(ShipPosition, eye);
    }

    [Fact]
    public void The_left_eye_offset_is_turned_into_the_camera_frame()
    {
        // Looking along +X with +Y up, the camera's right is +Z.
        var view = LookingAlong(Vector3.UnitX);
        var (_, _, eye) = VrEyeCamera.ForEye(view, Projection, ShipPosition, Eye(Quaternion.Identity, new Vector3(-0.032f, 0, 0)));

        Near3(ShipPosition + new Vector3(0, 0, -0.032f), eye);
    }

    [Fact]
    public void Turning_the_head_left_turns_the_view_left_of_the_ship()
    {
        var left = Quaternion.CreateFromAxisAngle(Vector3.Up, MathF.PI / 2);

        var (ahead, _, _) = VrEyeCamera.ForEye(LookingAlong(Vector3.Forward), Projection, ShipPosition, Eye(left, Vector3.Zero));
        Near3(Vector3.Left, Forward(ahead));

        // The same turn on a ship facing +X looks along the ship's left, -Z.
        var (turned, _, _) = VrEyeCamera.ForEye(LookingAlong(Vector3.UnitX), Projection, ShipPosition, Eye(left, Vector3.Zero));
        Near3(-Vector3.UnitZ, Forward(turned));
    }

    [Fact]
    public void Looking_up_in_the_headset_looks_up_from_the_ship()
    {
        var up = Quaternion.CreateFromAxisAngle(Vector3.Right, MathF.PI / 2);
        var (view, _, _) = VrEyeCamera.ForEye(LookingAlong(Vector3.UnitX), Projection, ShipPosition, Eye(up, Vector3.Zero));
        Near3(Vector3.Up, Forward(view));
    }

    [Fact]
    public void A_view_that_does_not_look_from_the_origin_keeps_its_own_eye_point()
    {
        var view = Matrix.CreateLookAt(new Vector3(5, 0, 0), new Vector3(5, 0, -1), Vector3.Up);
        var (eyeView, _, eye) = VrEyeCamera.ForEye(view, Projection, ShipPosition, Eye(Quaternion.Identity, Vector3.Zero));

        for (int i = 0; i < 16; i++)
            Assert.Equal(view[i], eyeView[i], 4);
        Assert.Equal(ShipPosition, eye);
    }

    [Fact]
    public void The_games_vertical_field_of_view_comes_back_from_its_projection()
    {
        Assert.Equal(1.2f, VrEyeCamera.VerticalFieldOfView(Projection), 5);
    }

    [Fact]
    public void The_eye_projection_is_xnas_off_centre_projection()
    {
        var fov = new VrEyeView(Quaternion.Identity, Vector3.Zero, -0.9f, 0.7f, 0.8f, -0.85f);
        var expected = Matrix.CreatePerspectiveOffCenter(
            Near * MathF.Tan(fov.AngleLeft), Near * MathF.Tan(fov.AngleRight),
            Near * MathF.Tan(fov.AngleDown), Near * MathF.Tan(fov.AngleUp), Near, Far);
        var actual = VrEyeCamera.OffCentreProjection(Projection, fov);

        for (int i = 0; i < 16; i++)
            Assert.Equal(expected[i], actual[i], 4);
        // The depth terms are the game's own, bit for bit.
        Assert.Equal(Projection.M33, actual.M33);
        Assert.Equal(Projection.M43, actual.M43);
    }

    [Fact]
    public void Each_edge_of_an_asymmetric_eye_field_of_view_lands_on_the_image_edge()
    {
        var fov = new VrEyeView(Quaternion.Identity, Vector3.Zero, -0.9f, 0.7f, 0.8f, -0.85f);
        var (view, projection, _) = VrEyeCamera.ForEye(LookingAlong(Vector3.Forward), Projection, ShipPosition, fov);
        var viewProjection = view * projection;

        Vector2 Clip(float x, float y)
        {
            var clip = Vector4.Transform(new Vector4(x * 100, y * 100, -100, 1), viewProjection);
            return new Vector2(clip.X / clip.W, clip.Y / clip.W);
        }

        Assert.Equal(-1f, Clip(MathF.Tan(fov.AngleLeft), 0).X, 4);
        Assert.Equal(1f, Clip(MathF.Tan(fov.AngleRight), 0).X, 4);
        Assert.Equal(1f, Clip(0, MathF.Tan(fov.AngleUp)).Y, 4);
        Assert.Equal(-1f, Clip(0, MathF.Tan(fov.AngleDown)).Y, 4);
    }

    [Fact]
    public void A_point_maps_to_the_same_depth_in_the_eye_as_in_the_flat_frame()
    {
        var fov = new VrEyeView(Quaternion.Identity, Vector3.Zero, -0.9f, 0.7f, 0.8f, -0.85f);
        var (_, projection, _) = VrEyeCamera.ForEye(LookingAlong(Vector3.Forward), Projection, ShipPosition, fov);
        foreach (float distance in new[] { Near, 3f, 250f, 4000f, Far })
        {
            var point = new Vector4(0, 0, -distance, 1);
            var flat = Vector4.Transform(point, Projection);
            var eye = Vector4.Transform(point, projection);
            Assert.Equal(flat.Z / flat.W, eye.Z / eye.W);
        }
    }
}
