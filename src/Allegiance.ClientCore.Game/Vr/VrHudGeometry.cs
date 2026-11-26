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

    /// <summary>The flight reference block (layer 3), seen at infinity: the angle
    /// its crop covers across, and how far the gauge ring's centre is below the
    /// nose.</summary>
    public readonly record struct FlightBlock(float WidthRadians, float PitchDownRadians)
    {
        /// <summary>40° across the crop, so the Classic gauge ring (about 215 of
        /// the crop's 400 pixels) is about 21.5° wide: the spec's 30° by 20°
        /// field holds it with its readouts. Centred on the nose, where native
        /// draws the ring around the boresight.</summary>
        public static FlightBlock Default => new(MathHelper.ToRadians(40f), 0f);

        /// <summary>Reads "widthDegrees,pitchDegrees" (for example "40,0"), as set
        /// in ALLEGIANCE_VR_FLIGHT_BLOCK for headset tuning. Anything that does not
        /// parse to a width of 10° to 90° and a pitch of -30° to 45° is the
        /// default.</summary>
        public static FlightBlock Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Default;
            var parts = text.Split(',');
            if (parts.Length != 2) return Default;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            const System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Float;
            if (!float.TryParse(parts[0], style, inv, out float w) || !float.TryParse(parts[1], style, inv, out float p))
                return Default;
            if (!(w >= 10f && w <= 90f) || !(p >= -30f && p <= 45f)) return Default;
            return new FlightBlock(MathHelper.ToRadians(w), MathHelper.ToRadians(p));
        }
    }

    /// <summary>A box in an image, in pixels from the image's centre (x right,
    /// y down): left and top are negative.</summary>
    public readonly record struct PixelBox(float Left, float Top, float Right, float Bottom)
    {
        public float Width => Right - Left;
        public float Height => Bottom - Top;
    }

    /// <summary>The span, in pixels, that <see cref="FlightBlock.WidthRadians"/>
    /// covers: the Classic ring (about 215 pixels, at the fixed native pane
    /// scale) with room around it.</summary>
    public const float FlightBlockReferencePixels = 400f;

    /// <summary>The Classic centre group around the image centre, for an image
    /// this tall: the gauge ring (fixed pixels, about 215 across), the speed
    /// readout <see cref="Rendering.HudRenderer.FlightReferenceSpeedBelowCentre"/>
    /// pixels down, and the own-ship status, whose native points
    /// (<see cref="Rendering.OwnStatusLayout"/>: x from -200 to 122, y from -110
    /// to 185) are spread with the image height. The margins hold the icons and
    /// text drawn from those points; the image is transparent around the group,
    /// so a margin costs nothing.</summary>
    public static PixelBox CentreGroupBox(int imageHeight)
    {
        float s = Rendering.OwnStatusLayout.Spread(imageHeight);
        return new(-(200f * s + 60f), -(110f * s + 90f), 122f * s + 100f,
            MathF.Max(Rendering.HudRenderer.FlightReferenceSpeedBelowCentre + 45f, 185f * s + 60f));
    }

    /// <summary>How far out the flight block's rectangle is placed. Drawn with
    /// <see cref="AtInfinity"/>, this only has to sit inside the seat depth
    /// planes; it sets no disparity.</summary>
    public const float FlightBlockDistanceMetres = 10f;

    /// <summary>The quad that shows <paramref name="box"/> of an image as the
    /// flight block: corners in seat space (top-left, top-right, bottom-left,
    /// bottom-right) at <see cref="FlightBlockDistanceMetres"/>, so
    /// <see cref="FlightBlockReferencePixels"/> about the image centre cover the
    /// block's angle and the image centre lies on the block's axis, and the
    /// matching texture coordinates. The box is clamped to the image.</summary>
    public static (Vector3 TopLeft, Vector3 TopRight, Vector3 BottomLeft, Vector3 BottomRight,
        Vector2 UvTopLeft, Vector2 UvBottomRight) FlightBlockQuad(in FlightBlock block, PixelBox box,
        int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth), "The HUD image has no area.");
        float cx = pixelWidth / 2f, cy = pixelHeight / 2f;
        // Metres per pixel from the fixed reference span, so neither the box nor
        // the image's size changes how large the ring looks.
        float scale = 2f * FlightBlockDistanceMetres * MathF.Tan(block.WidthRadians / 2f) / FlightBlockReferencePixels;
        box = new PixelBox(MathF.Max(box.Left, -cx), MathF.Max(box.Top, -cy),
            MathF.Min(box.Right, cx), MathF.Min(box.Bottom, cy));
        float sin = MathF.Sin(block.PitchDownRadians), cos = MathF.Cos(block.PitchDownRadians);
        Vector3 At(float px, float py)
        {
            // Unpitched: x right, y up, the plane 10 m ahead; then turned down
            // about x by the pitch, as PanelCorners turns a panel.
            float x = px * scale, y = -py * scale, z = -FlightBlockDistanceMetres;
            return new Vector3(x, y * cos + z * sin, -y * sin + z * cos);
        }
        return (At(box.Left, box.Top), At(box.Right, box.Top), At(box.Left, box.Bottom), At(box.Right, box.Bottom),
            new Vector2((cx + box.Left) / pixelWidth, (cy + box.Top) / pixelHeight),
            new Vector2((cx + box.Right) / pixelWidth, (cy + box.Bottom) / pixelHeight));
    }

    /// <summary>A view with its translation removed, so only the eye's turn is
    /// left. Both eyes see what is drawn with it in the same direction: it is at
    /// infinity, with no disparity, and does not move when the head moves.</summary>
    public static Matrix AtInfinity(Matrix view)
    {
        view.Translation = Vector3.Zero;
        return view;
    }

    /// <summary>The flight-path marker's direction (layer 1): the unit velocity,
    /// or null when the rounded speed is zero, the rule the flat velocity marker
    /// follows (native <c>(int)(length + 0.5f)</c>).</summary>
    public static Vector3? FlightPathDirection(Vector3 velocity)
    {
        float speed = velocity.Length();
        if ((int)(speed + 0.5f) == 0) return null;
        return velocity / speed;
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

    /// <summary>The farthest a marker is placed, in metres. Past it the eyes
    /// cannot tell depth from infinity (vergence under 0.04°), and a marker
    /// placed there would be hidden by every object between it and the eye.</summary>
    public const float MarkerDepthClampMetres = 100f;

    /// <summary>Where a marker is placed, relative to the head: on the line to
    /// its object, at the near side of the sphere its bracket frames (so the
    /// object does not hide its own bracket), and never farther than
    /// <paramref name="clampMetres"/>. A marker covering
    /// <paramref name="angleRadians"/> at the object's distance frames a sphere
    /// of radius distance · tan(angle / 2).</summary>
    public static Vector3 MarkerAnchor(Vector3 relative, float angleRadians, float clampMetres = MarkerDepthClampMetres)
    {
        float distance = relative.Length();
        if (!(distance > 1e-4f)) return relative;
        float nearSide = distance * MathF.Max(0.05f, 1f - MathF.Tan(angleRadians / 2f));
        return relative * (MathF.Min(nearSide, clampMetres) / distance);
    }

    /// <summary>The depth a point has in the depth buffer of a view drawn with
    /// <paramref name="projection"/>: 0 at the near plane, 1 at the far plane
    /// (MonoGame's convention on every backend). Below 0 when the point is
    /// nearer than the near plane or behind the eye.</summary>
    public static float BufferDepth(Vector3 relative, Matrix view, Matrix projection)
    {
        var clip = Vector4.Transform(new Vector4(relative, 1f), view * projection);
        return clip.W > 1e-6f ? clip.Z / clip.W : -1f;
    }

    /// <summary>The radius of the sphere a marker's anchor sits in front of, for an
    /// object at <paramref name="relative"/> from the head: the same sphere
    /// <see cref="MarkerAnchor"/> takes the near side of.</summary>
    public static float MarkerSphereRadius(Vector3 relative, float bracketRadians, float wrapRadians, float? radiusMetres) =>
        relative.Length() * MathF.Min(0.95f, MathF.Tan(AnchorAngle(relative, bracketRadians, wrapRadians, radiusMetres) / 2f));

    /// <summary>The depth-buffer value a marker is tested at in one eye. A marker is
    /// drawn flat in the eye's image, so it has one depth along the eye's axis. Its
    /// anchor's depth is not enough: off the eye's axis, the object's sphere
    /// reaches up to radius · (1 − cos θ) nearer than the anchor along that axis,
    /// and its near side then cut through the marker, more the farther the head
    /// turned from it. So the marker is tested at the nearer of its anchor and the
    /// sphere's nearest point along the axis. Its place in the image, and so its
    /// stereo depth, stays at the anchor. Positions are relative to the eye.</summary>
    public static float MarkerTestDepth(Vector3 anchorRelative, Vector3 objectRelative, float sphereRadius, Matrix view,
        Matrix projection)
    {
        float anchorZ = -Vector3.Transform(anchorRelative, view).Z;
        float sphereZ = -Vector3.Transform(objectRelative, view).Z - MathF.Max(0f, sphereRadius);
        float z = MathF.Min(anchorZ, sphereZ);
        if (!(z > 0f)) return -1f;
        var clip = Vector4.Transform(new Vector4(0f, 0f, -z, 1f), projection);
        return clip.W > 1e-6f ? clip.Z / clip.W : -1f;
    }

    /// <summary>The off-view arrow for an object in <paramref name="directionInView"/>
    /// (view space, -Z ahead): a point on a ring <paramref name="ringRadians"/>
    /// off the view's centre at <paramref name="distance"/> metres, on the side
    /// the object lies, and the arrow's direction on the image (+Y down). An
    /// object straight behind points down.</summary>
    public static (Vector3 PointInView, Vector2 ImageDirection) OffViewArrow(Vector3 directionInView, float ringRadians,
        float distance)
    {
        var side = new Vector2(directionInView.X, directionInView.Y);
        side = side.LengthSquared() < 1e-8f ? new Vector2(0f, -1f) : Vector2.Normalize(side);
        float sin = MathF.Sin(ringRadians), cos = MathF.Cos(ringRadians);
        return (distance * new Vector3(sin * side.X, sin * side.Y, -cos), new Vector2(side.X, -side.Y));
    }

    /// <summary>The aim reticle's depth limits, in metres, when a target under
    /// it gives it one. Nearer than 15 m the eyes would cross hard on a fly-by;
    /// beyond 100 m depth looks the same as infinity (under 0.04° of
    /// vergence).</summary>
    public const float ReticleNearestMetres = 15f, ReticleFarthestMetres = 100f;

    /// <summary>How far off the boresight a target's centre may lie and still
    /// give the reticle its depth: 3°, or half its anchor angle
    /// (<see cref="AnchorAngle"/>) when that is wider, so a near object gives it
    /// while its hull is under the reticle.</summary>
    public static readonly float ReticleConeRadians = MathHelper.ToRadians(3f);

    /// <summary>The aim reticle's depth from one target, as an inverse
    /// distance (1/m; 0 is infinity), which stereo disparity is proportional
    /// to. <paramref name="nose"/> is the boresight's direction and
    /// <paramref name="relative"/> the target relative to the head, in one
    /// frame. A target off the boresight, or behind, gives none (0). Otherwise
    /// the reticle takes the target's marker anchor
    /// (<see cref="MarkerAnchor"/>), held between 15 and 100 m.</summary>
    public static float ReticleInverseDistance(Vector3 nose, Vector3 relative, float bracketRadians, float wrapRadians = 0f,
        float? radiusMetres = null)
    {
        float distance = relative.Length();
        if (!(distance > 1e-4f) || nose.LengthSquared() < 1e-8f) return 0f;
        float cosOff = Vector3.Dot(Vector3.Normalize(nose), relative / distance);
        float angle = AnchorAngle(relative, bracketRadians, wrapRadians, radiusMetres);
        float cone = MathF.Max(ReticleConeRadians, angle / 2f);
        if (cosOff < MathF.Cos(cone)) return 0f;
        float depth = MarkerAnchor(relative, angle, ReticleFarthestMetres).Length();
        return 1f / Math.Clamp(depth, ReticleNearestMetres, ReticleFarthestMetres);
    }

    /// <summary>The angle a marker's anchor is set from: the widest of the drawn
    /// bracket, a wrap angle measured where the marker was made, and the wrap of
    /// <paramref name="radiusMetres"/> measured along <paramref name="relative"/>
    /// itself. Pass the radius whenever it is known: the head (the cockpit eye
    /// is 14 m ahead of the ship's centre) sees a near object wider than the
    /// ship's centre does, and a wrap measured from the ship would put the
    /// anchor inside the hull.</summary>
    public static float AnchorAngle(Vector3 relative, float bracketRadians, float wrapRadians, float? radiusMetres) =>
        MathF.Max(MathF.Max(bracketRadians, wrapRadians),
            radiusMetres is { } r && r > 0f ? WrapAngle(r, relative.Length()) : 0f);

    /// <summary>A marker's anchor in the world, for an object at
    /// <paramref name="world"/> seen from <paramref name="head"/> (midway between
    /// the eyes): <see cref="MarkerAnchor"/> along the head-relative vector, with
    /// the angle from <see cref="AnchorAngle"/> measured along that same vector.</summary>
    public static Vector3 MarkerAnchorInWorld(Vector3 world, Vector3 head, float bracketRadians, float wrapRadians,
        float? radiusMetres)
    {
        var relative = world - head;
        return head + MarkerAnchor(relative, AnchorAngle(relative, bracketRadians, wrapRadians, radiusMetres));
    }

    /// <summary>How fast the reticle's depth follows its target: an
    /// exponential approach with this time constant, in seconds.</summary>
    public const float ReticleDepthSeconds = 0.2f;

    /// <summary>One step of the reticle's depth toward <paramref name="target"/>,
    /// both inverse distances, over <paramref name="seconds"/>. Smoothing the
    /// inverse distance moves the stereo disparity at an even rate, and lets the
    /// depth go to and from infinity (0).</summary>
    public static float SmoothInverseDistance(float current, float target, float seconds)
    {
        if (!(seconds > 0f)) return current;
        return target + (current - target) * MathF.Exp(-seconds / ReticleDepthSeconds);
    }

    /// <summary>The angle a bracket covers, across: wide enough to frame the
    /// object (1.05 times its radius, as Jet's designator), never under the
    /// floor, and capped so a near object does not fill the view.</summary>
    public static float BracketAngle(float? worldRadius, float distance) =>
        MathF.Min(WrapAngle(worldRadius, distance), BracketCapRadians);

    /// <summary>The angle that wraps an object of <paramref name="worldRadius"/>
    /// at <paramref name="distance"/> with a 5% margin, at least the bracket
    /// floor and not capped: what <see cref="MarkerAnchor"/> needs to stay in
    /// front of the object when the drawn bracket is capped.</summary>
    public static float WrapAngle(float? worldRadius, float distance)
    {
        if (worldRadius is not { } r || !(r > 0f) || !(distance > 0f)) return BracketFloorRadians;
        return MathF.Max(2f * MathF.Atan(1.05f * r / distance), BracketFloorRadians);
    }

    /// <summary>The bracket angle for an object whose flat-HUD screen radius is
    /// known in pixels of a view of <paramref name="height"/> rows.</summary>
    public static float BracketAngleFromScreenRadius(float? screenRadius, Matrix projection, int height)
    {
        if (screenRadius is not { } r || !(r > 0f) || height <= 0 || !(projection.M22 > 0f)) return BracketFloorRadians;
        float radius = MathF.Atan(r / (projection.M22 * height / 2f));
        return Math.Clamp(2f * 1.05f * radius, BracketFloorRadians, BracketCapRadians);
    }

    /// <summary><see cref="BracketAngleFromScreenRadius"/> without the cap: the
    /// object's own wrap angle, for <see cref="MarkerAnchor"/>.</summary>
    public static float WrapAngleFromScreenRadius(float? screenRadius, Matrix projection, int height)
    {
        if (screenRadius is not { } r || !(r > 0f) || height <= 0 || !(projection.M22 > 0f)) return BracketFloorRadians;
        float radius = MathF.Atan(r / (projection.M22 * height / 2f));
        return MathF.Max(2f * 1.05f * radius, BracketFloorRadians);
    }
}
