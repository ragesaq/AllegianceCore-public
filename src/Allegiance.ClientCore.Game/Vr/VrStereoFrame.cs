using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>One eye's view for the coming headset frame, in the headset's local
/// reference space: metres, +X right, +Y up, -Z forward (the same handedness as
/// XNA). The four angles are the eye's field of view in radians; left and down
/// are normally negative.</summary>
public readonly record struct VrEyeView(
    Quaternion Orientation, Vector3 Position,
    float AngleLeft, float AngleRight, float AngleUp, float AngleDown);

/// <summary>What the headset asks the game to draw this frame: both eyes, each
/// into an image of <see cref="EyeWidth"/> by <see cref="EyeHeight"/> pixels.</summary>
public sealed record VrStereoFrame(VrEyeView Left, VrEyeView Right, int EyeWidth, int EyeHeight);

/// <summary>The finished eye images for <see cref="IVrFrameSink.SubmitFrame"/>:
/// OpenGL texture names, both <see cref="Width"/> by <see cref="Height"/>, rows
/// stored top first (MonoGame render-target order, the reverse of the back
/// buffer). <c>default</c> means no eye was drawn this frame.
/// <see cref="HudVerticalFovRadians"/> is the game camera's vertical field of
/// view, so the HUD layer can cover the same angle; 0 when unknown.</summary>
public readonly record struct VrEyeImages(uint Left, uint Right, int Width, int Height, float HudVerticalFovRadians)
{
    public bool Present => Left != 0 && Right != 0 && Width > 0 && Height > 0;
}
