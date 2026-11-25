using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Vr;

/// <summary>Where the VR HUD goes in each eye image (knowledge/vr-hud-design.md,
/// build step 1). Markers sit on the objects they mark: each eye projects the
/// object's own position, so the two eyes' markers carry the object's disparity
/// and the marker is seen at the object's depth. Their size is an angle, not a
/// pixel count, so it does not change with distance. The rest of the flat HUD
/// hangs on one panel fixed to the seat, below the nose.
///
/// Seat space is the headset's local reference space: metres, +X right, +Y up,
/// -Z the ship's nose (<see cref="VrEyeCamera"/> turns the head in the camera's
/// frame, so the local space's -Z is the camera's forward).</summary>
public static class VrHudGeometry
{
    /// <summary>The smallest angle a bracket covers, across (spec: about 2°).</summary>
    public static readonly float BracketFloorRadians = MathHelper.ToRadians(2f);

    /// <summary>The smallest height of marker text (spec: about 0.6°).</summary>
    public static readonly float TextFloorRadians = MathHelper.ToRadians(0.6f);

    /// <summary>The largest angle a bracket that wraps a near object covers.</summary>
    public static readonly float BracketCapRadians = MathHelper.ToRadians(20f);

    /// <summary>The interim panel for the rest of the flat HUD: about 1 m from
    /// the head's rest position, its centre below the nose, turned to face the
    /// head. Starting values for the headset.</summary>
    public readonly record struct Panel(float DistanceMetres, float PitchDownRadians, float WidthMetres)
    {
        public static Panel Default => new(1.0f, MathHelper.ToRadians(25f), 1.1f);

        /// <summary>Reads "distance,pitchDegrees,width" (for example "1,25,1.1"),
        /// as set in ALLEGIANCE_VR_HUD_PANEL for headset tuning. Anything that does
        /// not parse to a panel at least 0.3 m away and 0.1 m wide is the default.</summary>
        public static Panel Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Default;
            var parts = text.Split(',');
            if (parts.Length != 3) return Default;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            const System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Float;
            if (!float.TryParse(parts[0], style, inv, out float d) || !float.TryParse(parts[1], style, inv, out float p)
                || !float.TryParse(parts[2], style, inv, out float w)) return Default;
            if (!(d >= 0.3f && d <= 10f) || !(p >= -60f && p <= 80f) || !(w >= 0.1f && w <= 5f)) return Default;
            return new Panel(d, MathHelper.ToRadians(p), w);
        }
    }

    /// <summary>The panel's corners in seat space, top-left, top-right,
    /// bottom-left, bottom-right, for an image of the given aspect.</summary>
    public static (Vector3 TopLeft, Vector3 TopRight, Vector3 BottomLeft, Vector3 BottomRight) PanelCorners(
        in Panel panel, int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth), "The HUD image has no area.");
        float sin = MathF.Sin(panel.PitchDownRadians), cos = MathF.Cos(panel.PitchDownRadians);
        var centre = panel.DistanceMetres * new Vector3(0, -sin, -cos);
        // Up within the panel's plane: perpendicular to the line from the head,
        // so the panel faces the head.
        var up = new Vector3(0, cos, -sin);
        float halfWidth = panel.WidthMetres / 2f;
        float halfHeight = halfWidth * pixelHeight / pixelWidth;
        var right = Vector3.UnitX * halfWidth;
        var upHalf = up * halfHeight;
        return (centre - right + upHalf, centre + right + upHalf, centre - right - upHalf, centre + right - upHalf);
    }

    /// <summary>A seat-space view for one eye: the eye's pose in the local
    /// space, with no game camera in it. The panel and the aim reticle are drawn
    /// with it, so they stay fixed to the seat while the head moves.</summary>
    public static Matrix SeatView(in VrEyeView eye) =>
        Matrix.Invert(Matrix.CreateFromQuaternion(Quaternion.Normalize(eye.Orientation)) * Matrix.CreateTranslation(eye.Position));

    /// <summary>Near and far planes, in metres, for things drawn in seat space.</summary>
    public const float SeatNearMetres = 0.05f, SeatFarMetres = 100f;

    /// <summary>The eye's projection with seat-space depth planes: the same
    /// field of view (M11, M22, M31, M32), but a near plane of 5 cm. The eye
    /// projection keeps the game's near plane, which is farther than the panel,
    /// so the GPU would clip the panel away.</summary>
    public static Matrix SeatProjection(Matrix eyeProjection)
    {
        var p = eyeProjection;
        p.M33 = SeatFarMetres / (SeatNearMetres - SeatFarMetres);
        p.M43 = SeatNearMetres * SeatFarMetres / (SeatNearMetres - SeatFarMetres);
        return p;
    }

    /// <summary>The unit direction, in the world's camera-relative frame, of the
    /// ray through a pixel of a view of <paramref name="width"/> by
    /// <paramref name="height"/> pixels (+Y down). The inverse of the game's
    /// <c>ProjectToScreen</c> for a perspective projection (M34 = -1), off-centre
    /// terms included.</summary>
    public static Vector3 RayThroughPixel(Vector2 pixel, float width, float height, Matrix view, Matrix projection)
    {
        float ndcX = pixel.X / width * 2f - 1f;
        float ndcY = 1f - pixel.Y / height * 2f;
        // clip = (M11 x + M31 z, M22 y + M32 z, ..., -z); at z = -1 the w is 1.
        var inView = new Vector3((ndcX + projection.M31) / projection.M11, (ndcY + projection.M32) / projection.M22, -1f);
        var direction = Vector3.TransformNormal(inView, Matrix.Invert(view));
        return Vector3.Normalize(direction);
    }

    /// <summary>Projects a camera-relative point (world position minus the eye)
    /// into an image of the given size, +Y down. False when the point is behind
    /// the eye. A point outside the image still gets its pixel.</summary>
    public static bool TryProject(Vector3 relative, Matrix view, Matrix projection, int width, int height, out Vector2 pixel)
    {
        var clip = Vector4.Transform(new Vector4(relative, 1f), view * projection);
        pixel = default;
        if (clip.W <= 1e-6f) return false;
        pixel = new Vector2((clip.X / clip.W * 0.5f + 0.5f) * width, (0.5f - clip.Y / clip.W * 0.5f) * height);
        return true;
    }

    /// <summary>Projects a direction (a point at infinity) into an image, +Y
    /// down; both eyes give the same direction, so it shows no disparity.</summary>
    public static bool TryProjectDirection(Vector3 direction, Matrix view, Matrix projection, int width, int height, out Vector2 pixel)
    {
        var clip = Vector4.Transform(new Vector4(direction, 0f), view * projection);
        pixel = default;
        if (clip.W <= 1e-6f) return false;
        pixel = new Vector2((clip.X / clip.W * 0.5f + 0.5f) * width, (0.5f - clip.Y / clip.W * 0.5f) * height);
        return true;
    }

    /// <summary>How many pixels an angle covers at the centre of an image of
    /// <paramref name="height"/> rows drawn with <paramref name="projection"/>
    /// (vertical scale M22 = 2 / (tan up - tan down)).</summary>
    public static float PixelsForAngle(float radians, Matrix projection, int height) =>
        MathF.Tan(radians) * projection.M22 * height / 2f;

    /// <summary>How many pixels an angle covers at <paramref name="pixel"/> of a
    /// view-space image of <paramref name="width"/> by <paramref name="height"/>
    /// pixels: the distance between the two rays half the angle either side of
    /// the ray through that pixel, measured up and down. A flat image stretches
    /// off its centre: a marker 45° to the side needs about 1.4 times the pixels
    /// it needs at the centre, and one 45° up about twice.</summary>
    public static float PixelsForAngleAt(float radians, Vector2 pixel, Matrix projection, int width, int height)
    {
        float ndcX = pixel.X / width * 2f - 1f;
        float ndcY = 1f - pixel.Y / height * 2f;
        var ray = Vector3.Normalize(new Vector3((ndcX + projection.M31) / projection.M11,
            (ndcY + projection.M32) / projection.M22, -1f));
        var axis = Vector3.Cross(ray, Vector3.UnitY);
        axis = axis.LengthSquared() < 1e-8f ? Vector3.UnitX : Vector3.Normalize(axis);
        var turn = Quaternion.CreateFromAxisAngle(axis, radians / 2f);
        if (!TryProject(Vector3.Transform(ray, turn), Matrix.Identity, projection, width, height, out var a)
            || !TryProject(Vector3.Transform(ray, Quaternion.Conjugate(turn)), Matrix.Identity, projection, width, height, out var b))
            return PixelsForAngle(radians, projection, height);
        return Vector2.Distance(a, b);
    }

    /// <summary>The angle a bracket covers, across: wide enough to frame the
    /// object (1.05 times its radius, as Jet's designator), never under the
    /// floor, and capped so a near object does not fill the view.</summary>
    public static float BracketAngle(float? worldRadius, float distance)
    {
        if (worldRadius is not { } r || !(r > 0f) || !(distance > 0f)) return BracketFloorRadians;
        float wrap = 2f * MathF.Atan(1.05f * r / distance);
        return Math.Clamp(wrap, BracketFloorRadians, BracketCapRadians);
    }

    /// <summary>The bracket angle for an object whose flat-HUD screen radius is
    /// known in pixels of a view of <paramref name="height"/> rows.</summary>
    public static float BracketAngleFromScreenRadius(float? screenRadius, Matrix projection, int height)
    {
        if (screenRadius is not { } r || !(r > 0f) || height <= 0 || !(projection.M22 > 0f)) return BracketFloorRadians;
        float radius = MathF.Atan(r / (projection.M22 * height / 2f));
        return Math.Clamp(2f * 1.05f * radius, BracketFloorRadians, BracketCapRadians);
    }
}
