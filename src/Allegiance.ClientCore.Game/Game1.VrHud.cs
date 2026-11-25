using Allegiance.ClientCore.MonoGame.Flight;
using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.ClientCore.MonoGame.Vr;
using Allegiance.ClientCore.State;
using Allegiance.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Allegiance.ClientCore.MonoGame;

/// <summary>VR HUD, build step 1 (knowledge/vr-hud-design.md): in a stereo
/// frame the HUD is drawn into the eye images, not onto a composition layer of
/// its own. The flat HUD is drawn into an off-screen image and shown on one
/// interim panel fixed to the seat, about 1 m away and below the nose. The
/// targeting markers (selection and lock brackets, the lead marker, contact
/// brackets with a short label) leave that panel and are drawn in each eye at
/// their object's position, so each sits at its object's depth, with a size
/// set as an angle. A flat frame touches none of this.</summary>
public sealed partial class Game1
{
    private enum VrMarkerShape { Contact, Selection, Lock, Lead }

    /// <summary>One targeting marker: where its object is in the world (absolute
    /// XNA coordinates), how wide it is as an angle, and its short label.</summary>
    private readonly record struct VrMarker(Vector3 World, float AngleRadians, Color Colour, VrMarkerShape Shape,
        string? Name, string? Range);

    // The headset sees more than the game camera's 50° frame and the head can
    // turn, so the radar contacts are gathered again for the markers through a
    // wide frame turned with the head. Degrees from the head's forward.
    private const float VrRadarHalfWidthDegrees = 75f;
    private const float VrRadarHalfHeightDegrees = 70f;

    private static readonly VrHudGeometry.Panel VrPanel =
        VrHudGeometry.Panel.Parse(Environment.GetEnvironmentVariable("ALLEGIANCE_VR_HUD_PANEL"));

    private static readonly (float X, float Y)[] VrCorners = { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) };

    private static readonly Color VrMarkerNeutral = new(200, 210, 220);
    private static readonly Color VrSelectionColour = Color.White;
    private static readonly Color VrLockPartialColour = new(255, 210, 40);
    private static readonly Color VrLockFullColour = new(255, 60, 40);
    private static readonly Color VrLeadInRange = new(80, 255, 120);
    private static readonly Color VrLeadOutOfRange = new(150, 150, 150);
    private static readonly Color VrReticleColour = new(120, 255, 140);

    private RenderTarget2D? _vrHudImage;
    private Texture2D? _vrPixel;
    private BasicEffect? _vrPanelEffect;
    private readonly VertexPositionTexture[] _vrPanelVertices = new VertexPositionTexture[4];
    private readonly List<VrMarker> _vrMarkers = new();
    private readonly RadarLabelCache _vrRadarLabels = new();
    private readonly (Matrix View, Matrix Projection, Vector3 Eye)[] _vrEyeCameras = new (Matrix, Matrix, Vector3)[2];
    private bool _vrAimReticle;
    private bool _vrHudLogged;

    /// <summary>Where the HUD is drawn in a stereo frame: an image the size of the
    /// back buffer, so every HUD layout and viewport reads as in the flat game.
    /// Preserve: an excursion to another target that comes back (the SDF font
    /// atlas, for one) must not find it cleared.</summary>
    private RenderTarget2D VrHudImage(int width, int height)
    {
        if (_vrHudImage is null || _vrHudImage.Width != width || _vrHudImage.Height != height)
        {
            _vrHudImage?.Dispose();
            _vrHudImage = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color,
                DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
        }
        return _vrHudImage;
    }

    /// <summary>Called right after the radar gather, with the game's own camera.
    /// In a stereo frame it records the world position of each targeting marker,
    /// then takes the markers off the flat HUD, which in VR is a panel below the
    /// nose where a screen-space bracket would point at nothing. A flat frame
    /// returns at once.</summary>
    private void GatherVrHudMarkers(Matrix view, Matrix proj, Vector3 cameraEye, double timeSeconds)
    {
        if (!_vrEyesDrawn || _vrFrame is not { } frame) return;
        _vrMarkers.Clear();
        bool hudShown = !(_hideHud || _directorActive || _hudHidden || _nativeHangarScreen || LocalSpectating);
        bool flight = hudShown && OwnShipInFlight();
        bool indicators = flight && !CommandCameraActive
            && _cameraMode is CameraMode.Cockpit or CameraMode.Chase or CameraMode.ExternalMissile;
        _vrAimReticle = indicators && _cameraMode == CameraMode.Cockpit;

        if (flight)
        {
            if (!_directorActive && !_nativeHangarScreen)
            {
                var (radarView, radarProj) = VrRadarCamera(view, proj, cameraEye, frame);
                DrawRadar(radarView, radarProj, cameraEye, timeSeconds);
                AddVrContactMarkers(radarView, radarProj, cameraEye);
            }
            AddVrSelectionMarker();
            if (indicators) AddVrLockMarker();
            if (indicators && _cameraMode == CameraMode.Cockpit) AddVrLeadMarker(view, proj, cameraEye);
        }

        // Off the panel: every marker drawn over a projected point. The bearing
        // arrow stays: it is a compass, not a marker.
        _lockBracket = null;
        _leadIndicator = null;
        _targetSelectionBracket = null;
        _velocityMarker = null;
        _chaseAim = null;
        _orderArrows = null;
        _drawRadarContacts.Clear();
        _drawShipTags.Clear();
    }

    private bool OwnShipInFlight()
    {
        lock (_session.Gate)
        {
            var world = _session.World;
            return RenderShipId(world) is { } me && world.Ships.TryGetValue(me, out var entity)
                && entity.Latest is not null && entity.StationId < 0 && !entity.IsDead;
        }
    }

    /// <summary>The radar's camera for the markers: the game camera turned with
    /// the head (the left eye's view without its offset) and a frame wide
    /// enough for the headset's view, with the game projection's depth terms.</summary>
    private static (Matrix View, Matrix Projection) VrRadarCamera(Matrix view, Matrix proj, Vector3 cameraEye, VrStereoFrame frame)
    {
        float halfW = MathHelper.ToRadians(VrRadarHalfWidthDegrees), halfH = MathHelper.ToRadians(VrRadarHalfHeightDegrees);
        var head = frame.Left with { Position = Vector3.Zero, AngleLeft = -halfW, AngleRight = halfW, AngleUp = halfH, AngleDown = -halfH };
        var (headView, wideProj, _) = VrEyeCamera.ForEye(view, proj, cameraEye, head);
        return (headView, wideProj);
    }

    private void AddVrContactMarkers(Matrix radarView, Matrix radarProj, Vector3 cameraEye)
    {
        var vp = GraphicsDevice.Viewport;
        for (int i = 0; i < _drawRadarContacts.Count; i++)
        {
            var contact = _drawRadarContacts[i];
            if (!contact.OnScreen || contact.IsSelf || contact.CameraDistance is not { } distance || !(distance > 0f)) continue;
            var direction = VrHudGeometry.RayThroughPixel(new Vector2(contact.Position.X, contact.Position.Y),
                vp.Width, vp.Height, radarView, radarProj);
            var world = cameraEye + direction * distance;
            float angle = VrHudGeometry.BracketAngleFromScreenRadius(contact.ScreenRadius, radarProj, vp.Height);
            // The native label rules (radar LOD, per-kind tiers) decide the text.
            var label = _vrRadarLabels.Resolve(i, contact, _radarMode, CommandCameraActive);
            var colour = contact.SideColor is { } side ? ToColour(side) : VrMarkerNeutral;
            _vrMarkers.Add(new VrMarker(world, angle, colour, VrMarkerShape.Contact, label.NameLine, label.RangeLine));
        }
    }

    private void AddVrSelectionMarker()
    {
        string? name;
        float? radius = null;
        HudColor? sideColour = null;
        AutopilotTarget target;
        lock (_session.Gate)
        {
            if (_autoTarget is not { } t) return;
            target = t;
            var world = _session.World;
            var kind = target.Kind switch
            {
                TargetKind.Ship => Allegiance.Core.IgcObjectType.Ship,
                TargetKind.Station => Allegiance.Core.IgcObjectType.Station,
                TargetKind.Asteroid => Allegiance.Core.IgcObjectType.Asteroid,
                _ => Allegiance.Core.IgcObjectType.Warp,
            };
            if (SelectionPaneModel.Build(world, kind, target.Id) is not { } info) return;
            name = info.Name;
            short? side = target.Kind == TargetKind.Ship && world.Ships.TryGetValue(target.Id, out var ship) ? ship.SideId
                : target.Kind == TargetKind.Station && world.Stations.TryGetValue(target.Id, out var station) ? station.SideId : null;
            if (side is { } sideId && world.TryGetSideColor(sideId, out float r, out float g, out float b)) sideColour = new(r, g, b);
            radius = TargetWorldRadius(world, target);
        }
        if (!ResolveTargetPosition(target, out var simPos)) return;
        var position = PoseConverter.ToXna(simPos);
        // Range from the own ship, as the flat selection readout measures it.
        bool ownKnown = TryOwnPose(out var ownPos, out _, out _);
        float distance = ownKnown ? (simPos - ownPos).Length() : 0f;
        string? range = ownKnown ? RadarLabelModel.RangeLineText((int)MathF.Round(distance)) : null;
        _vrMarkers.Add(new VrMarker(position, VrHudGeometry.BracketAngle(radius, distance) * 1.15f,
            sideColour is { } c ? ToColour(c) : VrSelectionColour, VrMarkerShape.Selection, name, range));
    }

    private float? TargetWorldRadius(ClientWorld world, AutopilotTarget target) => target.Kind switch
    {
        TargetKind.Ship when world.Ships.TryGetValue(target.Id, out var s) && HullLength(s.HullId) > 0f => HullLength(s.HullId) * 0.5f,
        TargetKind.Station when world.Stations.TryGetValue(target.Id, out var st) && _shopCatalog?.StationTypes is { } types
            && types.TryGetValue(st.StationTypeId, out var type) => type.Radius,
        TargetKind.Asteroid when world.Asteroids.TryGetValue(target.Id, out var a) && a.Radius > 0 => a.Radius,
        TargetKind.Warp when world.Warps.TryGetValue(target.Id, out var w) && w.Radius > 0 => w.Radius,
        _ => null,
    };

    /// <summary>The server's missile-lock target, anchored as
    /// <c>ComputeLockBracket</c> anchors the flat lock box.</summary>
    private void AddVrLockMarker()
    {
        Vector3 position;
        float? radius;
        MissileLockFeedback.LockState state;
        lock (_session.Gate)
        {
            var world = _session.World;
            var lockState = world.MissileLock;
            if (!lockState.HasTarget) return;
            state = MissileLockFeedback.Classify(lockState.LockFraction);
            if (state == MissileLockFeedback.LockState.None) return;
            position = lockState.TargetType == Allegiance.Core.IgcObjectType.Ship
                && ResolveShipAnchor(_poses, world.Ships, lockState.TargetId, out var anchor)
                    ? PoseConverter.ToXna(anchor)
                    : PoseConverter.ToXna(lockState.TargetPosition);
            radius = lockState.TargetType == Allegiance.Core.IgcObjectType.Ship
                && world.Ships.TryGetValue(lockState.TargetId, out var ship) && HullLength(ship.HullId) > 0f
                    ? HullLength(ship.HullId) * 0.5f : null;
        }
        float distance = TryOwnPose(out var own, out _, out _) ? (position - PoseConverter.ToXna(own)).Length() : 0f;
        _vrMarkers.Add(new VrMarker(position, VrHudGeometry.BracketAngle(radius, distance) * 1.3f,
            state == MissileLockFeedback.LockState.Full ? VrLockFullColour : VrLockPartialColour, VrMarkerShape.Lock, null, null));
    }

    /// <summary>The cockpit lead marker: the solved lead direction (the flat
    /// marker's pixel, cast back through the game camera) at the led target's
    /// range, so it sits at the same depth as the target it leads.</summary>
    private void AddVrLeadMarker(Matrix view, Matrix proj, Vector3 cameraEye)
    {
        if (_leadIndicator is not { Granted: true } lead) return;
        Vector3 targetPosition;
        lock (_session.Gate)
        {
            if (_session.World.MyCurrentCommand is not { } command
                || !TryGatherLeadScene(command.TargetType, command.TargetId, out var scene)) return;
            targetPosition = PoseConverter.ToXna(scene.TargetPosition);
        }
        var vp = GraphicsDevice.Viewport;
        var direction = VrHudGeometry.RayThroughPixel(lead.LeadCenter, vp.Width, vp.Height, view, proj);
        float distance = (targetPosition - cameraEye).Length();
        if (!(distance > 0f)) return;
        var colour = Color.Lerp(VrLeadOutOfRange, VrLeadInRange, lead.InRange ? Math.Clamp(lead.Green, 0.35f, 1f) : 0f);
        _vrMarkers.Add(new VrMarker(cameraEye + direction * distance, MathHelper.ToRadians(1.2f), colour,
            VrMarkerShape.Lead, null, null));
    }

    private static Color ToColour(HudColor c) => new(c.R, c.G, c.B, c.A);

    /// <summary>Draws the HUD into both eye images: the HUD image on the seat's
    /// panel, then the markers and the aim reticle. Called once per stereo frame
    /// after the frame is complete, before the eyes are handed to the headset.
    /// The window then shows the left eye.</summary>
    private void ComposeVrHud()
    {
        if (_vrFrame is not { } frame || _vrLeftEye is not { } left || _vrRightEye is not { } right) return;
        var gd = GraphicsDevice;
        _vrPixel ??= CreateVrPixel(gd);
        _vrPanelEffect ??= new BasicEffect(gd) { TextureEnabled = true, LightingEnabled = false, VertexColorEnabled = false };
        for (int pass = 0; pass < 2; pass++)
        {
            var target = pass == 0 ? left : right;
            var eye = pass == 0 ? frame.Left : frame.Right;
            gd.SetRenderTarget(target);
            var seatView = VrHudGeometry.SeatView(eye);
            var (view, proj, eyeWorld) = _vrEyeCameras[pass];
            if (_vrHudImage is { } hud) DrawVrPanel(hud, seatView, VrHudGeometry.SeatProjection(proj));
            if (pass == 0 && !_vrHudLogged) LogVrHudPlacement(eye, seatView, proj, target, _vrHudImage);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            try
            {
                if (_vrAimReticle && VrHudGeometry.TryProjectDirection(Vector3.Forward, seatView, proj,
                        target.Width, target.Height, out var nose))
                    DrawVrReticle(nose, VrHudGeometry.PixelsForAngle(MathHelper.ToRadians(1.5f), proj, target.Height),
                        VrLineWidth(proj, target.Height));
                foreach (var marker in _vrMarkers)
                {
                    if (!VrHudGeometry.TryProject(marker.World - eyeWorld, view, proj, target.Width, target.Height, out var at)) continue;
                    DrawVrMarker(marker, at, proj, target.Height);
                }
            }
            finally { _spriteBatch.End(); }
        }

        gd.SetRenderTarget(null);
        gd.Clear(Color.Black);
        var pp = gd.PresentationParameters;
        float scale = Math.Min((float)pp.BackBufferWidth / left.Width, (float)pp.BackBufferHeight / left.Height);
        var size = new Vector2(left.Width, left.Height) * scale;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        try
        {
            _spriteBatch.Draw(left, new Rectangle((int)((pp.BackBufferWidth - size.X) / 2f), (int)((pp.BackBufferHeight - size.Y) / 2f),
                (int)size.X, (int)size.Y), Color.White);
        }
        finally { _spriteBatch.End(); }
    }

    /// <summary>One line on the first stereo frame: where the head looks in seat
    /// space and where the panel's centre falls in the left eye, so a log shows
    /// whether the panel was in view (a headset not worn points anywhere).</summary>
    private void LogVrHudPlacement(in VrEyeView eye, Matrix seatView, Matrix proj, RenderTarget2D target, RenderTarget2D? hud)
    {
        _vrHudLogged = true;
        var look = Vector3.Transform(Vector3.Forward, Quaternion.Normalize(eye.Orientation));
        string panel = "no HUD image";
        if (hud is not null)
        {
            var (tl, tr, bl, br) = VrHudGeometry.PanelCorners(VrPanel, hud.Width, hud.Height);
            var centre = (tl + tr + bl + br) / 4f;
            panel = VrHudGeometry.TryProject(centre, seatView, proj, target.Width, target.Height, out var px)
                ? $"panel centre at pixel ({px.X:0},{px.Y:0}) of {target.Width}x{target.Height}"
                : "panel centre behind the eye";
        }
        Console.WriteLine($"[vr] HUD in the eyes: head looks ({look.X:0.00},{look.Y:0.00},{look.Z:0.00}) in seat space; {panel}; "
            + $"panel {VrPanel.DistanceMetres:0.##} m, {MathHelper.ToDegrees(VrPanel.PitchDownRadians):0.#} deg below the nose, {VrPanel.WidthMetres:0.##} m wide");
    }

    private static Texture2D CreateVrPixel(GraphicsDevice gd)
    {
        var pixel = new Texture2D(gd, 1, 1);
        pixel.SetData(new[] { Color.White });
        return pixel;
    }

    private void DrawVrPanel(Texture2D hud, Matrix seatView, Matrix proj)
    {
        var (tl, tr, bl, br) = VrHudGeometry.PanelCorners(VrPanel, hud.Width, hud.Height);
        _vrPanelVertices[0] = new VertexPositionTexture(tl, new Vector2(0, 0));
        _vrPanelVertices[1] = new VertexPositionTexture(tr, new Vector2(1, 0));
        _vrPanelVertices[2] = new VertexPositionTexture(bl, new Vector2(0, 1));
        _vrPanelVertices[3] = new VertexPositionTexture(br, new Vector2(1, 1));
        var gd = GraphicsDevice;
        // The HUD image holds premultiplied colour over transparent, as the
        // SpriteBatch HUD wrote it; AlphaBlend is XNA's premultiplied blend.
        gd.BlendState = BlendState.AlphaBlend;
        gd.DepthStencilState = DepthStencilState.None;
        gd.RasterizerState = RasterizerState.CullNone;
        gd.SamplerStates[0] = SamplerState.LinearClamp;
        var effect = _vrPanelEffect!;
        effect.World = Matrix.Identity;
        effect.View = seatView;
        effect.Projection = proj;
        effect.Texture = hud;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, _vrPanelVertices, 0, 2);
        }
    }

    private static float VrLineWidth(Matrix proj, int height) =>
        MathF.Max(2f, VrHudGeometry.PixelsForAngle(MathHelper.ToRadians(0.08f), proj, height));

    private void DrawVrMarker(in VrMarker marker, Vector2 at, Matrix proj, int height)
    {
        float size = VrHudGeometry.PixelsForAngle(marker.AngleRadians, proj, height);
        float half = size / 2f;
        float line = VrLineWidth(proj, height);
        switch (marker.Shape)
        {
            case VrMarkerShape.Contact:
            {
                float arm = half * 0.4f;
                foreach (var (sx, sy) in VrCorners)
                {
                    var corner = at + new Vector2(sx * half, sy * half);
                    VrLine(corner, corner - new Vector2(sx * arm, 0), line, marker.Colour);
                    VrLine(corner, corner - new Vector2(0, sy * arm), line, marker.Colour);
                }
                break;
            }
            case VrMarkerShape.Selection:
                VrPolygon(at, half * MathF.Sqrt(2f), 4, MathHelper.PiOver4, line, marker.Colour);
                break;
            case VrMarkerShape.Lock:
                VrPolygon(at, half, 4, 0f, line * 1.5f, marker.Colour);
                break;
            case VrMarkerShape.Lead:
                VrPolygon(at, half, 12, 0f, line, marker.Colour);
                VrLine(at - new Vector2(line, 0), at + new Vector2(line, 0), line, marker.Colour);
                break;
        }
        DrawVrLabel(marker, at + new Vector2(0, half + line * 2f), proj, height);
    }

    private void DrawVrLabel(in VrMarker marker, Vector2 topCentre, Matrix proj, int height)
    {
        if (marker.Name is null && marker.Range is null) return;
        float glyphPixels = VrHudGeometry.PixelsForAngle(VrHudGeometry.TextFloorRadians, proj, height);
        // A SpriteFont's capital letters fill about three quarters of its line.
        float scale = glyphPixels / (_hudSmall.LineSpacing * 0.75f);
        float y = topCentre.Y;
        for (int line = 0; line < 2; line++)
        {
            string? raw = line == 0 ? marker.Name : marker.Range;
            if (string.IsNullOrEmpty(raw)) continue;
            // Names come from players; DrawString throws on a glyph the font lacks.
            string text = FontText.Safe(_hudSmall, raw);
            var size = _hudSmall.MeasureString(text) * scale;
            var at = new Vector2(topCentre.X - size.X / 2f, y);
            _spriteBatch.DrawString(_hudSmall, text, at + new Vector2(scale, scale), Color.Black * 0.7f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_hudSmall, text, at, marker.Colour, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            y += _hudSmall.LineSpacing * scale;
        }
    }

    private void DrawVrReticle(Vector2 at, float size, float line)
    {
        float half = size / 2f, gap = size * 0.18f;
        VrLine(at + new Vector2(-half, 0), at + new Vector2(-gap, 0), line, VrReticleColour);
        VrLine(at + new Vector2(gap, 0), at + new Vector2(half, 0), line, VrReticleColour);
        VrLine(at + new Vector2(0, -half), at + new Vector2(0, -gap), line, VrReticleColour);
        VrLine(at + new Vector2(0, gap), at + new Vector2(0, half), line, VrReticleColour);
    }

    private void VrPolygon(Vector2 centre, float radius, int sides, float phase, float line, Color colour)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = phase + MathHelper.TwoPi * i / sides, a1 = phase + MathHelper.TwoPi * (i + 1) / sides;
            VrLine(centre + radius * new Vector2(MathF.Cos(a0), MathF.Sin(a0)),
                centre + radius * new Vector2(MathF.Cos(a1), MathF.Sin(a1)), line, colour);
        }
    }

    private void VrLine(Vector2 from, Vector2 to, float width, Color colour)
    {
        var delta = to - from;
        float length = delta.Length();
        if (length < 0.5f) return;
        _spriteBatch.Draw(_vrPixel!, from, null, colour, MathF.Atan2(delta.Y, delta.X), new Vector2(0, 0.5f),
            new Vector2(length, width), SpriteEffects.None, 0f);
    }

    private void ReleaseVrHud()
    {
        _vrHudImage?.Dispose();
        _vrHudImage = null;
    }
}
