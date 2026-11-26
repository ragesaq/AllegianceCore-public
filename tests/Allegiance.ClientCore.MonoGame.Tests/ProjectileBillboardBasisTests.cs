using Allegiance.ClientCore.MonoGame.Rendering;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>A billboard round faces the camera whatever the camera's turn: its
/// right and up are the view's own right and up, at right angles to the view's
/// axis. In the headset each eye's view turns with the head, and the rows of the
/// view matrix (the mixed basis read before) left the quad tilted.</summary>
public sealed class ProjectileBillboardBasisTests
{
    public static TheoryData<float, float, float> Turns => new()
    {
        { 0f, 0f, 0f },
        { 45f, 0f, 0f },
        { 30f, 25f, 0f },
        { -60f, 10f, 35f },
        { 120f, -40f, -20f },
    };

    private static Matrix View(float yaw, float pitch, float roll)
    {
        var turn = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(yaw), MathHelper.ToRadians(pitch), MathHelper.ToRadians(roll));
        return Matrix.CreateLookAt(Vector3.Zero, Vector3.TransformNormal(Vector3.Forward, turn), Vector3.TransformNormal(Vector3.Up, turn));
    }

    [Theory]
    [MemberData(nameof(Turns))]
    public void The_quad_lies_square_to_the_view_and_upright_on_the_image(float yaw, float pitch, float roll)
    {
        var view = View(yaw, pitch, roll);
        ProjectileSpriteRenderer.BillboardBasis(view, out var right, out var up);
        // Into view space: the quad's axes are the image's own +X and +Y.
        var rightInView = Vector3.TransformNormal(right, view);
        var upInView = Vector3.TransformNormal(up, view);
        Assert.True(Vector3.Distance(Vector3.UnitX, rightInView) < 1e-5f, $"right {rightInView}");
        Assert.True(Vector3.Distance(Vector3.UnitY, upInView) < 1e-5f, $"up {upInView}");
    }

    [Fact]
    public void The_rows_of_a_turned_view_are_not_square_to_it()
    {
        // The control: the basis read before this fix, for a view turned 45°.
        var view = View(45f, 0f, 0f);
        var rowRight = new Vector3(view.M11, view.M12, view.M13);
        Assert.True(MathF.Abs(Vector3.TransformNormal(rowRight, view).Z) > 0.5f);
    }
}
