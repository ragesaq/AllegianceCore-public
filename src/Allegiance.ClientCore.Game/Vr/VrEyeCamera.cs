using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>Turns the game's camera for a frame into one eye's camera. The game
/// draws camera-relative (P207): its view matrix looks from the origin and every
/// object is placed relative to the camera's world position. An eye therefore
/// gets the camera's rotation followed by the head's, and a world position moved
/// by the head's offset turned into the camera's frame. One game unit is one
/// metre (the radar labels plain world distance in metres, native
/// radarimage.cpp:1209), so the headset's offsets apply unscaled.</summary>
public static class VrEyeCamera
{
    public const float GameUnitsPerMetre = 1f;

    /// <param name="centreView">The game's view matrix for the frame.</param>
    /// <param name="centreProjection">The game's projection; only its depth terms
    /// are kept, so depth maps exactly as in the flat frame.</param>
    /// <param name="centreEye">The game's camera position in world space.</param>
    /// <param name="eye">The eye's pose and field of view from the headset.</param>
    public static (Matrix View, Matrix Projection, Vector3 Eye) ForEye(
        Matrix centreView, Matrix centreProjection, Vector3 centreEye, in VrEyeView eye)
    {
        // The camera's own frame. Every camera-relative view looks from the
        // origin; a view that looks from elsewhere keeps that point in the view.
        Matrix cameraWorld = Matrix.Invert(centreView);
        Matrix cameraRotation = cameraWorld;
        cameraRotation.Translation = Vector3.Zero;

        Matrix head = Matrix.CreateFromQuaternion(Quaternion.Normalize(eye.Orientation));
        Matrix eyeWorld = head * cameraRotation;
        eyeWorld.Translation = cameraWorld.Translation;
        Matrix view = Matrix.Invert(eyeWorld);

        // The head's offset moves the point every draw subtracts from positions.
        Vector3 offset = Vector3.TransformNormal(eye.Position * GameUnitsPerMetre, cameraRotation);
        Vector3 worldEye = centreEye + offset;

        return (view, OffCentreProjection(centreProjection, eye), worldEye);
    }

    /// <summary>An XNA off-centre perspective projection
    /// (Matrix.CreatePerspectiveOffCenter) for the eye's four angles, with the
    /// game projection's depth terms M33 and M43 copied unchanged. Near and far
    /// are not recovered from them: in single precision the far plane comes back
    /// about 0.1% off at the game's 10 000 m.</summary>
    public static Matrix OffCentreProjection(Matrix centreProjection, in VrEyeView eye)
    {
        float left = MathF.Tan(eye.AngleLeft), right = MathF.Tan(eye.AngleRight);
        float down = MathF.Tan(eye.AngleDown), up = MathF.Tan(eye.AngleUp);
        return new Matrix(
            2f / (right - left), 0, 0, 0,
            0, 2f / (up - down), 0, 0,
            (left + right) / (right - left), (up + down) / (up - down), centreProjection.M33, -1,
            0, 0, centreProjection.M43, 0);
    }

    /// <summary>The vertical field of view of an XNA perspective projection
    /// (M22 = 1 / tan(fov / 2)).</summary>
    public static float VerticalFieldOfView(Matrix projection) => 2f * MathF.Atan(1f / projection.M22);
}
