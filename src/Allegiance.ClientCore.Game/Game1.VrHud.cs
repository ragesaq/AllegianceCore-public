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
    /// XNA coordinates), how wide it is drawn as an angle, and its short label.
    /// <paramref name="WrapRadians"/> is the object's own wrap angle, not
    /// capped, as seen where the marker was made (0 when unknown), and
    /// <paramref name="RadiusMetres"/> its world radius when known: the
    /// marker's anchor stays in front of the object with them
    /// (<see cref="VrHudGeometry.AnchorAngle"/>).</summary>
    private readonly record struct VrMarker(Vector3 World, float AngleRadians, Color Colour, VrMarkerShape Shape,
        string? Name, string? Range, float WrapRadians = 0f, float? RadiusMetres = null);

    // The headset sees more than the game camera's 50° frame and the head can
    // turn, so the radar contacts are gathered again for the markers through a
    // wide frame turned with the head. Degrees from the head's forward.
    private const float VrRadarHalfWidthDegrees = 75f;
    private const float VrRadarHalfHeightDegrees = 70f;

    private static readonly VrHudGeometry.Panel VrPanel =
        VrHudGeometry.Panel.Parse(Environment.GetEnvironmentVariable("ALLEGIANCE_VR_HUD_PANEL"));
    private static readonly VrHudGeometry.FlightBlock VrFlightBlock =
        VrHudGeometry.FlightBlock.Parse(Environment.GetEnvironmentVariable("ALLEGIANCE_VR_FLIGHT_BLOCK"));

    private static readonly (float X, float Y)[] VrCorners = { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) };

    private static readonly Color VrMarkerNeutral = new(200, 210, 220);
    private static readonly Color VrSelectionColour = Color.White;
    private static readonly Color VrLockPartialColour = new(255, 210, 40);
    private static readonly Color VrLockFullColour = new(255, 60, 40);
    private static readonly Color VrLeadInRange = new(80, 255, 120);
    private static readonly Color VrLeadOutOfRange = new(150, 150, 150);
    private static readonly Color VrReticleColour = new(120, 255, 140);

    private RenderTarget2D? _vrHudImage;
    // The flight reference block (layer 3) and the HUD model of the stereo
    // frame it is drawn from; the flight-path marker's world direction (layer 1).
    private RenderTarget2D? _vrFlightImage;
    private HudModel? _vrHudModel;
    private Vector3? _vrFlightPath;
    private Texture2D? _vrPixel;
    private BasicEffect? _vrPanelEffect;
    private readonly VertexPositionTexture[] _vrPanelVertices = new VertexPositionTexture[4];
    private readonly List<VrMarker> _vrMarkers = new();
    private readonly RadarLabelCache _vrRadarLabels = new();
    private readonly (Matrix View, Matrix Projection, Vector3 Eye)[] _vrEyeCameras = new (Matrix, Matrix, Vector3)[2];
    private bool _vrAimReticle;
    private bool _vrHudLogged;
    // The aim reticle's depth as an inverse distance (1/m, 0 is infinity),
    // smoothed toward a target under it, and when it was last stepped.
    private float _vrReticleInverseDistance;
    private long _vrReticleStepTicks;
    // The depth-buffer value the marker being drawn is tested at (VrLine and
    // the label text pass it to SpriteBatch as the layer depth).
    private float _vrMarkerDepth;

    // Off-view arrows: on a ring this far off the view's centre, this far away.
    private static readonly float VrArrowRingRadians = MathHelper.ToRadians(30f);
    private const float VrArrowRingMetres = 3f;

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
        // The flight-path marker follows the flat velocity marker's gate: the
        // cockpit, and a rounded source speed that is not zero.
        _vrFlightPath = _vrAimReticle && TrySourcePose(out _, out _, out var velocity)
            ? VrHudGeometry.FlightPathDirection(PoseConverter.ToXna(velocity)) : null;

        if (flight)
        {
            if (!_directorActive && !_nativeHangarScreen)
            {
                var (radarView, radarProj) = VrRadarCamera(view, proj, cameraEye, frame);
                string? proofLine = _radarProofLine;   // the radar proof reports the game camera's frame
                DrawRadar(radarView, radarProj, cameraEye, timeSeconds);
                _radarProofLine = proofLine;
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

    /// <summary>Called right after the skin's HUD pass. In a stereo frame it keeps
    /// the model, so <see cref="ComposeVrHud"/> can draw the flight reference
    /// block from the same data; a flat frame keeps none.</summary>
    private void KeepVrHudModel(HudModel model) => _vrHudModel = _vrEyesDrawn ? model : null;

    /// <summary>The flight reference block (layer 3) drawn into its own image,
    /// the size of the HUD image: the Classic centre group (gauge ring, speed,
    /// own-ship status), drawn by the Classic renderer whatever skin is active,
    /// laid out as in the flat game. Null when the instruments are hidden.</summary>
    private RenderTarget2D? DrawVrFlightImage(GraphicsDevice gd, int width, int height)
    {
        if (_vrHudModel is not { InstrumentsVisible: true } model) return null;
        if (_vrFlightImage is null || _vrFlightImage.Width != width || _vrFlightImage.Height != height)
        {
            _vrFlightImage?.Dispose();
            // Preserve, as the HUD image: an excursion to another target that
            // comes back must not find it cleared.
            _vrFlightImage = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color,
                DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
        }
        gd.SetRenderTarget(_vrFlightImage);
        gd.Clear(Color.Transparent);
        _hudRenderer.DrawFlightReference(model, gd.Viewport);
        return _vrFlightImage;
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
            float wrap = VrHudGeometry.WrapAngleFromScreenRadius(contact.ScreenRadius, radarProj, vp.Height);
            // The native label rules (radar LOD, per-kind tiers) decide the text.
            var label = _vrRadarLabels.Resolve(i, contact, _radarMode, CommandCameraActive);
            var colour = contact.SideColor is { } side ? ToColour(side) : VrMarkerNeutral;
            _vrMarkers.Add(new VrMarker(world, angle, colour, VrMarkerShape.Contact, label.NameLine, label.RangeLine, wrap));
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
        _vrMarkers.Add(new VrMarker(position, MathF.Min(VrHudGeometry.BracketAngle(radius, distance) * 1.15f, VrHudGeometry.BracketCapRadians),
            sideColour is { } c ? ToColour(c) : VrSelectionColour, VrMarkerShape.Selection, name, range, RadiusMetres: radius));
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
            // Every lockable type, through the selection's lookup (the flat lock
            // flower in Game1.Camera.cs takes the same four).
            TargetKind? kind = lockState.TargetType switch
            {
                Allegiance.Core.IgcObjectType.Ship => TargetKind.Ship,
                Allegiance.Core.IgcObjectType.Station => TargetKind.Station,
                Allegiance.Core.IgcObjectType.Asteroid => TargetKind.Asteroid,
                Allegiance.Core.IgcObjectType.Warp => TargetKind.Warp,
                _ => null,
            };
            radius = kind is { } k ? TargetWorldRadius(world, new AutopilotTarget(k, lockState.TargetId, "")) : null;
        }
        float distance = TryOwnPose(out var own, out _, out _) ? (position - PoseConverter.ToXna(own)).Length() : 0f;
        _vrMarkers.Add(new VrMarker(position, MathF.Min(VrHudGeometry.BracketAngle(radius, distance) * 1.3f, VrHudGeometry.BracketCapRadians),
            state == MissileLockFeedback.LockState.Full ? VrLockFullColour : VrLockPartialColour, VrMarkerShape.Lock, null, null,
            RadiusMetres: radius));
    }

    /// <summary>The cockpit lead marker: the solved lead direction (the flat
    /// marker's pixel, cast back through the game camera) at the led target's
    /// range, so it sits at the same depth as the target it leads. It carries the
    /// target's radius: the lead falls on or beside the target, and without it the
    /// target's own hull, nearer than its centre, hid the marker.</summary>
    private void AddVrLeadMarker(Matrix view, Matrix proj, Vector3 cameraEye)
    {
        if (_leadIndicator is not { Granted: true } lead) return;
        Vector3 targetPosition;
        float targetRadius;
        lock (_session.Gate)
        {
            if (_session.World.MyCurrentCommand is not { } command
                || !TryGatherLeadScene(command.TargetType, command.TargetId, out var scene)) return;
            targetPosition = PoseConverter.ToXna(scene.TargetPosition);
            targetRadius = scene.TargetRadius;
        }
        var vp = GraphicsDevice.Viewport;
        var direction = VrHudGeometry.RayThroughPixel(lead.LeadCenter, vp.Width, vp.Height, view, proj);
        float distance = (targetPosition - cameraEye).Length();
        if (!(distance > 0f)) return;
        var colour = Color.Lerp(VrLeadOutOfRange, VrLeadInRange, lead.InRange ? Math.Clamp(lead.Green, 0.35f, 1f) : 0f);
        _vrMarkers.Add(new VrMarker(cameraEye + direction * distance, MathHelper.ToRadians(1.2f), colour,
            VrMarkerShape.Lead, null, null, RadiusMetres: targetRadius > 0f ? targetRadius : null));
    }

    private static Color ToColour(HudColor c) => new(c.R, c.G, c.B, c.A);

    /// <summary>Draws the HUD into both eye images: the aim reticle and the
    /// off-view arrows, then the markers tested against the scene's depth, then
    /// the HUD image on the seat's panel. Called once per stereo frame
    /// after the frame is complete, before the eyes are handed to the headset.
    /// The window then shows the left eye.</summary>
    private void ComposeVrHud()
    {
        if (_vrFrame is not { } frame || _vrLeftEye is not { } left || _vrRightEye is not { } right) return;
        var gd = GraphicsDevice;
        _vrPixel ??= CreateVrPixel(gd);
        _vrPanelEffect ??= new BasicEffect(gd) { TextureEnabled = true, LightingEnabled = false, VertexColorEnabled = false };
        // The head: midway between the eyes. Markers and arrows are placed from
        // it, so both eyes see one point and its disparity.
        var head = (_vrEyeCameras[0].Eye + _vrEyeCameras[1].Eye) / 2f;
        // The head's up in the world, for the markers' planes.
        var headUp = Vector3.Normalize(Matrix.Invert(_vrEyeCameras[0].View).Up + Matrix.Invert(_vrEyeCameras[1].View).Up);
        GatherVrArrows(head, left.Width, left.Height);
        var nose = VrNoseInWorld(frame.Left);
        StepVrReticleDepth(nose, head);
        var flightImage = _vrHudImage is { } hudSize ? DrawVrFlightImage(gd, hudSize.Width, hudSize.Height) : null;
        for (int pass = 0; pass < 2; pass++)
        {
            var target = pass == 0 ? left : right;
            var eye = pass == 0 ? frame.Left : frame.Right;
            gd.SetRenderTarget(target);
            var seatView = VrHudGeometry.SeatView(eye);
            var (view, proj, eyeWorld) = _vrEyeCameras[pass];
            if (pass == 0 && !_vrHudLogged) LogVrHudPlacement(eye, seatView, proj, target, _vrHudImage);

            // The flight reference block first: it is at infinity, so everything
            // else in the HUD is nearer and draws over it.
            if (flightImage is not null)
                DrawVrFlightBlock(flightImage, VrHudGeometry.AtInfinity(seatView), VrHudGeometry.SeatProjection(proj));

            // The aim reticle (at infinity, or at the depth of a target under it)
            // and the off-view arrows (on a ring 3 m out) draw over the scene.
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            try
            {
                if (_vrAimReticle && VrReticlePixel(nose, head, seatView, view, proj, eyeWorld, target.Width, target.Height,
                        out var aim))
                    DrawVrReticle(aim, VrHudGeometry.PixelsForAngle(MathHelper.ToRadians(1.5f), proj, target.Height),
                        VrLineWidth(proj, target.Height));
                // The flight-path marker, at infinity along the velocity.
                if (_vrFlightPath is { } path
                    && VrHudGeometry.TryProjectDirection(path, view, proj, target.Width, target.Height, out var fpm))
                    DrawVrFlightPathMarker(fpm, proj, target.Width, target.Height);
                foreach (var arrow in _vrArrows)
                {
                    if (!VrHudGeometry.TryProject(arrow.World - eyeWorld, view, proj, target.Width, target.Height, out var at)) continue;
                    DrawVrArrow(at, arrow.ImageDirection, VrHudGeometry.PixelsForAngleAt(MathHelper.ToRadians(2.5f), at, proj,
                        target.Width, target.Height), VrLineWidth(proj, target.Height), arrow.Colour);
                }
            }
            finally { _spriteBatch.End(); }

            // Markers are tested against the scene's depth, read only: a nearer
            // object that passes in front of a marker hides it, as it would hide
            // anything behind it. Each is drawn at its anchor's depth.
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.DepthRead, RasterizerState.CullNone);
            try
            {
                foreach (var marker in _vrMarkers)
                {
                    var anchorWorld = VrMarkerAnchor(marker, head);
                    var anchor = anchorWorld - eyeWorld;
                    if (!VrHudGeometry.TryProject(anchor, view, proj, target.Width, target.Height, out _)) continue;
                    float sphere = VrHudGeometry.MarkerSphereRadius(marker.World - head, marker.AngleRadians, marker.WrapRadians,
                        marker.RadiusMetres);
                    _vrMarkerDepth = Math.Clamp(VrHudGeometry.MarkerTestDepth(anchor, marker.World - eyeWorld, sphere, view, proj),
                        0f, 1f);
                    var (planeRight, planeUp) = VrHudGeometry.MarkerBillboard(anchorWorld - head, headUp, marker.AngleRadians);
                    DrawVrMarker(marker, new VrMarkerPlane(anchor, planeRight, planeUp, view, proj, target.Width, target.Height));
                }
            }
            finally
            {
                _spriteBatch.End();
                _vrMarkerDepth = 0f;
            }

            // The panel last: at 1 m it is nearer than every marker.
            if (_vrHudImage is { } hud) DrawVrPanel(hud, VrPanel, seatView, VrHudGeometry.SeatProjection(proj));
        }

        // Each frame keeps its own model; one that skips the HUD pass draws no block.
        _vrHudModel = null;
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

    /// <summary>A marker's anchor in the world: on the line from the head to its
    /// object, in front of the object and at most 100 m away
    /// (<see cref="VrHudGeometry.MarkerAnchor"/>).</summary>
    private static Vector3 VrMarkerAnchor(in VrMarker marker, Vector3 head) =>
        VrHudGeometry.MarkerAnchorInWorld(marker.World, head, marker.AngleRadians, marker.WrapRadians, marker.RadiusMetres);

    /// <summary>The boresight's direction in the world: the seat's forward,
    /// taken through the eye's seat view and back out of its world view.</summary>
    private Vector3 VrNoseInWorld(in VrEyeView eye)
    {
        var inEye = Vector3.TransformNormal(Vector3.Forward, VrHudGeometry.SeatView(eye));
        return Vector3.Normalize(Vector3.TransformNormal(inEye, Matrix.Invert(_vrEyeCameras[0].View)));
    }

    /// <summary>Moves the reticle's depth toward the nearest selection or lock
    /// whose object is under it (<see cref="VrHudGeometry.ReticleInverseDistance"/>),
    /// or toward infinity when none is.</summary>
    private void StepVrReticleDepth(Vector3 nose, Vector3 head)
    {
        float target = 0f;
        if (_vrAimReticle)
            foreach (var marker in _vrMarkers)
                if (marker.Shape is VrMarkerShape.Selection or VrMarkerShape.Lock)
                    target = MathF.Max(target, VrHudGeometry.ReticleInverseDistance(nose, marker.World - head, marker.AngleRadians,
                        marker.WrapRadians, marker.RadiusMetres));
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        float seconds = _vrReticleStepTicks == 0 ? 0f
            : MathF.Min(0.1f, (float)((now - _vrReticleStepTicks) / (double)System.Diagnostics.Stopwatch.Frequency));
        _vrReticleStepTicks = now;
        _vrReticleInverseDistance = _vrAimReticle
            ? VrHudGeometry.SmoothInverseDistance(_vrReticleInverseDistance, target, seconds)
            : 0f;
    }

    /// <summary>Where the aim reticle falls in one eye: along the boresight at
    /// its smoothed depth, or the boresight's own direction at infinity.</summary>
    private bool VrReticlePixel(Vector3 nose, Vector3 head, Matrix seatView, Matrix view, Matrix proj, Vector3 eyeWorld,
        int width, int height, out Vector2 at)
    {
        if (_vrReticleInverseDistance > 1e-4f)
            return VrHudGeometry.TryProject(head + nose / _vrReticleInverseDistance - eyeWorld, view, proj, width, height, out at);
        return VrHudGeometry.TryProjectDirection(Vector3.Forward, seatView, proj, width, height, out at);
    }

    private readonly record struct VrArrow(Vector3 World, Vector2 ImageDirection, Color Colour);
    private readonly List<VrArrow> _vrArrows = new();

    /// <summary>For the selection and the lock: when the marker falls outside
    /// either eye's image, an arrow on a ring off the view's centre, 3 m out,
    /// on the side the object lies.</summary>
    private void GatherVrArrows(Vector3 head, int width, int height)
    {
        _vrArrows.Clear();
        var (view0, _, _) = _vrEyeCameras[0];
        var toWorld = Matrix.Invert(view0);
        foreach (var marker in _vrMarkers)
        {
            if (marker.Shape is not (VrMarkerShape.Selection or VrMarkerShape.Lock)) continue;
            var anchor = VrMarkerAnchor(marker, head);
            bool inView = true;
            for (int pass = 0; pass < 2 && inView; pass++)
            {
                var (view, proj, eyeWorld) = _vrEyeCameras[pass];
                inView = VrHudGeometry.TryProject(anchor - eyeWorld, view, proj, width, height, out var at)
                    && at.X >= 0f && at.X <= width && at.Y >= 0f && at.Y <= height;
            }
            if (inView) continue;
            var inViewSpace = Vector3.TransformNormal(marker.World - head, view0);
            var (point, direction) = VrHudGeometry.OffViewArrow(inViewSpace, VrArrowRingRadians, VrArrowRingMetres);
            _vrArrows.Add(new VrArrow(head + Vector3.TransformNormal(point, toWorld), direction, marker.Colour));
        }
    }

    private void DrawVrArrow(Vector2 at, Vector2 direction, float size, float line, Color colour)
    {
        var across = new Vector2(-direction.Y, direction.X);
        var tip = at + direction * size * 0.6f;
        var left = at - direction * size * 0.4f + across * size * 0.45f;
        var right = at - direction * size * 0.4f - across * size * 0.45f;
        VrLine(tip, left, line, colour);
        VrLine(left, right, line, colour);
        VrLine(right, tip, line, colour);
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

    private void DrawVrPanel(Texture2D hud, in VrHudGeometry.Panel panel, Matrix seatView, Matrix proj)
    {
        var (tl, tr, bl, br) = VrHudGeometry.PanelCorners(panel, hud.Width, hud.Height);
        DrawVrQuad(hud, tl, tr, bl, br, Vector2.Zero, Vector2.One, seatView, proj);
    }

    /// <summary>The Classic centre group cropped out of its image and shown as
    /// the flight block.</summary>
    private void DrawVrFlightBlock(Texture2D image, Matrix seatView, Matrix proj)
    {
        var (tl, tr, bl, br, uv0, uv1) = VrHudGeometry.FlightBlockQuad(VrFlightBlock, VrHudGeometry.CentreGroupBox(image.Height),
            image.Width, image.Height);
        DrawVrQuad(image, tl, tr, bl, br, uv0, uv1, seatView, proj);
    }

    private void DrawVrQuad(Texture2D hud, Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br, Vector2 uv0, Vector2 uv1,
        Matrix seatView, Matrix proj)
    {
        _vrPanelVertices[0] = new VertexPositionTexture(tl, uv0);
        _vrPanelVertices[1] = new VertexPositionTexture(tr, new Vector2(uv1.X, uv0.Y));
        _vrPanelVertices[2] = new VertexPositionTexture(bl, new Vector2(uv0.X, uv1.Y));
        _vrPanelVertices[3] = new VertexPositionTexture(br, uv1);
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

    /// <summary>One eye's view of a marker's plane (<see cref="VrHudGeometry.MarkerBillboard"/>):
    /// the anchor relative to the eye and the plane's two half-width vectors.
    /// A point x right, y down (±1 at the marker's edges) is projected from the
    /// plane, so both eyes see each edge at the anchor's depth.</summary>
    private readonly record struct VrMarkerPlane(Vector3 Anchor, Vector3 Right, Vector3 Up, Matrix View, Matrix Projection,
        int Width, int Height)
    {
        public bool TryPoint(float x, float y, out Vector2 pixel) =>
            VrHudGeometry.TryProject(Anchor + Right * x - Up * y, View, Projection, Width, Height, out pixel);
    }

    private void DrawVrMarker(in VrMarker marker, in VrMarkerPlane plane)
    {
        if (!plane.TryPoint(0f, 0f, out var at)
            || !plane.TryPoint(-1f, 0f, out var leftEdge) || !plane.TryPoint(1f, 0f, out var rightEdge)) return;
        float size = Vector2.Distance(leftEdge, rightEdge);
        if (!(size > 0.5f)) return;
        // Line and text keep their angle off the image's centre too.
        float stretch = size / MathF.Max(1e-3f, VrHudGeometry.PixelsForAngle(marker.AngleRadians, plane.Projection, plane.Height));
        float line = VrLineWidth(plane.Projection, plane.Height) * stretch;
        switch (marker.Shape)
        {
            case VrMarkerShape.Contact:
            {
                const float arm = 0.4f;
                foreach (var (sx, sy) in VrCorners)
                {
                    VrPlaneLine(plane, sx, sy, sx - sx * arm, sy, line, marker.Colour);
                    VrPlaneLine(plane, sx, sy, sx, sy - sy * arm, line, marker.Colour);
                }
                break;
            }
            case VrMarkerShape.Selection:
                VrPlanePolygon(plane, MathF.Sqrt(2f), 4, MathHelper.PiOver4, line, marker.Colour);
                break;
            case VrMarkerShape.Lock:
                VrPlanePolygon(plane, 1f, 4, 0f, line * 1.5f, marker.Colour);
                break;
            case VrMarkerShape.Lead:
                VrPlanePolygon(plane, 1f, 12, 0f, line, marker.Colour);
                VrLine(at - new Vector2(line, 0), at + new Vector2(line, 0), line, marker.Colour);
                break;
        }
        // The label hangs below the marker's foot, two line widths down.
        if (plane.TryPoint(0f, 1f + 4f * line / size, out var labelTop))
            DrawVrLabel(marker, labelTop, plane.Projection, plane.Height, stretch);
    }

    private void VrPlaneLine(in VrMarkerPlane plane, float x0, float y0, float x1, float y1, float width, Color colour)
    {
        if (plane.TryPoint(x0, y0, out var from) && plane.TryPoint(x1, y1, out var to)) VrLine(from, to, width, colour);
    }

    private void VrPlanePolygon(in VrMarkerPlane plane, float radius, int sides, float phase, float width, Color colour)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = phase + MathHelper.TwoPi * i / sides, a1 = phase + MathHelper.TwoPi * (i + 1) / sides;
            VrPlaneLine(plane, radius * MathF.Cos(a0), radius * MathF.Sin(a0), radius * MathF.Cos(a1), radius * MathF.Sin(a1),
                width, colour);
        }
    }

    private void DrawVrLabel(in VrMarker marker, Vector2 topCentre, Matrix proj, int height, float stretch)
    {
        if (marker.Name is null && marker.Range is null) return;
        float glyphPixels = VrHudGeometry.PixelsForAngle(VrHudGeometry.TextFloorRadians, proj, height) * stretch;
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
            _spriteBatch.DrawString(_hudSmall, text, at + new Vector2(scale, scale), Color.Black * 0.7f, 0f, Vector2.Zero, scale,
                SpriteEffects.None, _vrMarkerDepth);
            _spriteBatch.DrawString(_hudSmall, text, at, marker.Colour, 0f, Vector2.Zero, scale, SpriteEffects.None, _vrMarkerDepth);
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

    // The flight-path marker's circle when the native art is missing: about
    // the size of Jet's (7 px at scale 1).
    private static readonly float VrFlightPathRadiusRadians = MathHelper.ToRadians(0.6f);
    private static readonly Color VrFlightPathFallbackColour = new(160, 160, 160);

    /// <summary>The flight-path marker: the original game's velocity marker
    /// (<c>directionbmp</c>, untinted, as the flat game draws it), at the flight
    /// block's angle per pixel, so it keeps its size beside the ring. Without
    /// the art, a grey circle with two side wings and a tail above.</summary>
    private void DrawVrFlightPathMarker(Vector2 at, Matrix proj, int width, int height)
    {
        if (_indicatorTextures?.VelocityMarker is { } art)
        {
            float radiansPerPixel = VrFlightBlock.WidthRadians / VrHudGeometry.FlightBlockReferencePixels;
            float pixels = VrHudGeometry.PixelsForAngleAt(art.Width * radiansPerPixel, at, proj, width, height);
            _spriteBatch.Draw(art, at, null, Color.White, 0f, new Vector2(art.Width / 2f, art.Height / 2f), pixels / art.Width,
                SpriteEffects.None, 0f);
            return;
        }
        float radius = VrHudGeometry.PixelsForAngleAt(VrFlightPathRadiusRadians, at, proj, width, height);
        float line = VrLineWidth(proj, height);
        float wing = radius * 12f / 7f, tail = radius * 9f / 7f;
        VrPolygon(at, radius, 16, 0f, line, VrFlightPathFallbackColour);
        VrLine(at - new Vector2(radius + wing, 0), at - new Vector2(radius, 0), line, VrFlightPathFallbackColour);
        VrLine(at + new Vector2(radius, 0), at + new Vector2(radius + wing, 0), line, VrFlightPathFallbackColour);
        VrLine(at - new Vector2(0, radius + tail), at - new Vector2(0, radius), line, VrFlightPathFallbackColour);
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
            new Vector2(length, width), SpriteEffects.None, _vrMarkerDepth);
    }

    private void ReleaseVrHud()
    {
        _vrHudImage?.Dispose();
        _vrHudImage = null;
        _vrFlightImage?.Dispose();
        _vrFlightImage = null;
        _vrHudModel = null;
        _vrFlightPath = null;
        _vrPanelEffect?.Dispose();
        _vrPanelEffect = null;
        _vrPixel?.Dispose();
        _vrPixel = null;
        _vrReticleInverseDistance = 0f;
        _vrReticleStepTicks = 0;
    }
}
