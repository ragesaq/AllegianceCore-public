using System.Reflection;
using Allegiance.ClientCore.MonoGame.Vr;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Allegiance.ClientCore.MonoGame;

public sealed partial class Game1
{
    private VrFrameSinkHost? _vrHost;

    // VR milestone 2: the headset's views for the frame being drawn (null when
    // the frame is flat only), the two eye images, the eye pass being drawn (-1
    // outside the eye passes) and whether both eyes were drawn this frame.
    private VrStereoFrame? _vrFrame;
    private RenderTarget2D? _vrLeftEye;
    private RenderTarget2D? _vrRightEye;
    private int _vrPass = -1;
    private bool _vrEyesDrawn;
    private float _vrHudFov;

    // Pinned to the DesktopGL package, as HdCompressedUpload's TextureId is.
    private static readonly FieldInfo? VrGlTexture =
        typeof(Texture2D).BaseType?.GetField("glTexture", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Called by the head before <c>Run()</c> when VR was requested.
    /// <see cref="VrFrameSinkHost"/> says what changes while the sink is attached.</summary>
    public void AttachVrFrameSink(IVrFrameSink sink)
    {
        _vrHost ??= new VrFrameSinkHost(new VrPacing(this), Console.WriteLine);
        _vrHost.Attach(sink);
    }

    private void KeepVrPacing() => _vrHost?.KeepPacing();

    /// <summary>True while the scene is being drawn into an eye image.</summary>
    private bool VrEyePass => _vrPass >= 0;

    /// <summary>True for the second eye: the scene's per-frame work (particle
    /// emission, clocks, telemetry) already ran for the first eye and must not
    /// run again; only the drawing repeats.</summary>
    private bool VrRepeatPass => _vrPass > 0;

    /// <summary>How many times <c>Draw</c> runs its 3-D scene this frame: once
    /// flat, or once per eye when the headset is showing the app.</summary>
    private int VrScenePassCount => _vrFrame is null ? 1 : 2;

    /// <summary>Start of <c>Draw</c>: waits for the headset's frame and takes its
    /// eye views. A flat frame leaves every VR field as it was before VR.</summary>
    private void BeginVrFrame()
    {
        _vrPass = -1;
        _vrEyesDrawn = false;
        _vrFrame = _vrHost is { Attached: true } ? _vrHost.BeginFrame() : null;
        // A dropped sink leaves the game flat for the rest of the run; the eye
        // targets (hundreds of MB at headset sizes) go with it.
        if (_vrHost is not { Attached: true }) ReleaseVrEyeTargets();
    }

    /// <summary>Start of one pass of the 3-D scene. On a flat frame it changes
    /// nothing. For an eye it replaces the camera with the eye's and binds the
    /// eye's image, which the scene then draws into exactly as it draws into the
    /// back buffer.</summary>
    private void BeginVrScenePass(int pass, (Matrix View, Matrix Projection, Vector3 Eye) centre,
        ref Matrix view, ref Matrix proj, ref Vector3 cameraEye)
    {
        if (_vrFrame is not { } frame) return;
        _vrPass = pass;
        _vrHudFov = VrEyeCamera.VerticalFieldOfView(centre.Projection);
        (view, proj, cameraEye) = VrEyeCamera.ForEye(centre.View, centre.Projection, centre.Eye,
            pass == 0 ? frame.Left : frame.Right);
        GraphicsDevice.SetRenderTarget(VrEyeTarget(pass, frame.EyeWidth, frame.EyeHeight));
    }

    /// <summary>After the scene passes: the camera goes back to the game's own,
    /// for the radar and HUD drawn after the scene. After eye passes the back
    /// buffer is bound again and cleared to transparent, so it carries only the
    /// HUD, which the headset shows over the eyes.</summary>
    private void EndVrScenePasses((Matrix View, Matrix Projection, Vector3 Eye) centre,
        ref Matrix view, ref Matrix proj, ref Vector3 cameraEye)
    {
        if (_vrPass < 0) return;
        _vrPass = -1;
        _vrEyesDrawn = true;
        (view, proj, cameraEye) = centre;
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Transparent);
    }

    /// <summary>Where the scene finishes: the flat choice (the back buffer or the
    /// capture target), or the eye image during an eye pass.</summary>
    private RenderTarget2D? VrSceneDestination(RenderTarget2D? flat) => _vrPass switch
    {
        0 => _vrLeftEye,
        1 => _vrRightEye,
        _ => flat,
    };

    private RenderTarget2D VrEyeTarget(int pass, int width, int height)
    {
        ref RenderTarget2D? target = ref pass == 0 ? ref _vrLeftEye : ref _vrRightEye;
        if (target is null || target.Width != width || target.Height != height)
        {
            target?.Dispose();
            // Preserve: the scene binds this target once per pass, but a model
            // preview or other excursion that comes back to it must not see it
            // cleared (MonoGame clears a DiscardContents target on every bind).
            target = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color,
                DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
        }
        return target;
    }

    private void SubmitVrFrame()
    {
        if (_vrHost is not { Attached: true }) return;
        var pp = GraphicsDevice.PresentationParameters;
        var eyes = default(VrEyeImages);
        if (_vrEyesDrawn && _vrLeftEye is { } left && _vrRightEye is { } right)
        {
            if (GlTextureName(left) is not { } leftName || GlTextureName(right) is not { } rightName)
            {
                _vrHost.Abandon("the eye images have no OpenGL texture name (MonoGame internals changed)");
                return;
            }
            eyes = new VrEyeImages(leftName, rightName, left.Width, left.Height, _vrHudFov);
        }
        _vrHost.Submit(pp.BackBufferWidth, pp.BackBufferHeight, eyes);
    }

    private static uint? GlTextureName(Texture2D texture) =>
        VrGlTexture?.GetValue(texture) is int name && name > 0 ? (uint)name : null;

    private void DisposeVrSink()
    {
        _vrHost?.Dispose();
        ReleaseVrEyeTargets();
    }

    private void ReleaseVrEyeTargets()
    {
        _vrLeftEye?.Dispose();
        _vrRightEye?.Dispose();
        _vrLeftEye = null;
        _vrRightEye = null;
    }

    private sealed class VrPacing(Game1 game) : IVrPacingTarget
    {
        public bool FixedTimeStep
        {
            get => game.IsFixedTimeStep;
            set => game.IsFixedTimeStep = value;
        }

        public TimeSpan InactiveSleep
        {
            get => game.InactiveSleepTime;
            set => game.InactiveSleepTime = value;
        }

        public void DisableVsync() => game._graphics.SynchronizeWithVerticalRetrace = false;
    }
}
