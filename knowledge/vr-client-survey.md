# VR client survey: where OpenXR attaches to the AllegianceCore client

**Owner:** ragesaq · **Started:** 2026-11-24 · **Last reviewed:** 2026-11-25 · **Review by:** 2027-02-24

**Status:** in progress (milestone 1 built, reviewed and seen in the headset as a floating screen, as designed;
milestone 2 merged and seen in the headset: the 3-D scene in stereo; the sky and the HUD need follow-up work)

## What this answers

Why the VR client is built on AllegianceCore, how its MonoGame client draws a frame, where an OpenXR session and
per-eye rendering can attach, and what the first milestone should be.

## Decision: build the VR client here

- **Build the OpenXR VR client on AllegianceCore, in this fork.** *(decided 2026-11 by ragesaq, in the working
  session of 2026-11-24: "lets start by going allegiancecore, i'll make a fork", then "Enroll the fork")*
- Rejected: **VR in the C++ fork ragesaq/AllegianceVR.** Its renderer is fixed-function Direct3D 9 in a 32-bit build,
  OpenXR has no Direct3D 9 binding, and a modified C++ client can play on the public FreeAllegiance lobby only once
  upstream accepts the change. The full survey, the lobby checks and the community sources are in
  [AllegianceVR's survey note](https://github.com/ragesaq/AllegianceVR/blob/port/allegiancecore-decision/knowledge/openxr-port-survey.md).
- What informed it *(derived from the `MonoGame.Framework.DesktopGL` package in
  `src/Allegiance.ClientCore.MonoGame/Allegiance.ClientCore.MonoGame.csproj:25`, the release targets
  `linux-x64`, `win-x64` and `osx-arm64` in `.github/workflows/release.yml:38`, the README's x64 downloads
  (`README.md:160`) and the code read below)*: this client renders through OpenGL with shaders and builds for
  64-bit, and OpenXR has an OpenGL binding. Risks kept with the decision: one upstream author
  and a fast-moving codebase; MonoGame does not expose its GL context or textures publicly; and AllegianceCore runs
  its own server and lobby, so VR play here does not reach the FreeAllegiance lobby.

## Current state (read 2026-11 at upstream `b76d6c0`)

- The desktop client is the `src/Allegiance.ClientCore.MonoGame` head over the shared `src/Allegiance.ClientCore.Game`
  library, on `MonoGame.Framework.DesktopGL` 3.8.5.1 (its `.csproj`). Other heads: Browser, Android, Explorer.
- One `Game1` class, split across many `Game1.*.cs` files, owns the frame. `Draw` is at
  `src/Allegiance.ClientCore.Game/Game1.Rendering.cs:262`.
- The flight scene is drawn inline in `Draw`, about 1,400 lines from the camera choice to the post-processing
  composite. The camera comes from one call, `ComputeFlightCamera` (`Game1.Rendering.cs:426`, defined at
  `Game1.Director.cs:95`), which returns `view`, `proj` and the eye position. Every scene pass draws relative to that
  eye.
- The scene renders into its own target, then bloom, HDR output and FSR composite it (around
  `Game1.Rendering.cs:1727`). The HUD is a later SpriteBatch pass (`Game1.Rendering.cs:1776`).
- Capture mode already redirects the whole frame into an offscreen render target (`Game1.Rendering.cs:295`).
- The client already reaches below MonoGame: it reads `Texture2D`'s private `glTexture` field by reflection
  (`src/Allegiance.ClientCore.Game/Rendering/HdCompressedUpload.cs:25`) and loads GL functions through
  `SDL_GL_GetProcAddress` (`HdCompressedUpload.cs:48`).
- Vsync is on by default; `ALLEGIANCE_NOVSYNC=1` turns it off and frees the time step
  (`src/Allegiance.ClientCore.Game/Game1.cs:1682`).

## How it applies here (derived from the rows above)

| Need | Seam | Notes |
|---|---|---|
| OpenXR session | A new head project beside `Allegiance.ClientCore.MonoGame` | The OpenGL binding on Windows needs the GL device context and rendering context, read on the render thread while MonoGame's context is current |
| Swapchain images | Copy from a render target into the OpenXR image | The `glTexture` reflection precedent gives the source texture id |
| Frame pacing | `xrWaitFrame` paces the loop | Vsync off and a free time step in VR mode, as `ALLEGIANCE_NOVSYNC` already does |
| Per-eye rendering | Pull the inline scene pass out of `Draw` into a method that takes view, projection, eye and target | The camera-relative convention makes an eye offset a change of eye position. A refactor of upstream code, so it is milestone 2 |
| HUD | Draw the SpriteBatch HUD into its own target and show it on a quad layer | Both HUD styles must keep working (upstream rule) |

## Proposed first milestone

A virtual screen in the headset. Start an OpenXR session from a new head, render the existing flat frame into an
offscreen target the way capture mode does, copy it into the swapchain, and submit it as a quad layer. If no OpenXR
runtime is present, the game runs flat. This proves the binding, the texture access, the frame pacing and the
fallback before any stereo work. Milestone 2 is per-eye rendering of the scene with the head pose, and the HUD on its
own quad.

## Milestone 1 as built (branch `port/vr-m1`)

- **Binding: Silk.NET.OpenXR 2.23.0 with Silk.NET.OpenXR.Extensions.KHR 2.23.0.** *(decided 2026-11 by ragesaq,
  approving milestone 1 as proposed in the working session of 2026-11-24: "restore the NuGet packages, including
  Silk.NET.OpenXR")* Rejected: hand-written P/Invoke over the loader (more code to review for the same calls), and
  Khronos' `OpenXR.Loader` NuGet package as the native loader *(observed 2026-11: unlisted, last version 1.0.10)*.
- **No native loader ships with the game.** Silk.NET carries managed bindings only *(observed 2026-11)*. The loader is
  found through `ALLEGIANCE_OPENXR_LOADER` (a path to `openxr_loader.dll`, for example the one SteamVR installs in its
  `bin\win64` folder), then the default library search (`src/Allegiance.ClientCore.Vr/VrOptions.cs`).
- **Where it attaches** *(derived from the code)*: the OpenXR code is a library, `src/Allegiance.ClientCore.Vr`,
  referenced only by the desktop head. The shared game library holds just the `IVrFrameSink` seam
  (`src/Allegiance.ClientCore.Game/Vr/IVrFrameSink.cs`), the host that attaches, feeds and drops a sink
  (`Vr/VrFrameSinkHost.cs`) and `Game1.Vr.cs`. `EndDraw` hands the finished back buffer to
  the sink just before the window present (`Game1.GpuBudget.cs`). This differs from the proposal above: it copies
  framebuffer 0 rather than redirecting `Draw` into an offscreen target, so `Draw` is unchanged.
- **Opt-in and fallback** *(derived from the code)*: `--vr` or `ALLEGIANCE_VR=1`. OpenXR starts on the first frame, on
  the render thread with MonoGame's context current. A loader, runtime, headset, OpenXR or GL failure, or the session
  exiting or being lost, logs `[vr] running flat: <reason>` and tears OpenXR down. Any other exception from the
  presenter is caught by the game, logged as `[vr] frame sink threw, running flat: ...` and handled the same way.
  Either way the game runs flat: the fixed time step and the inactive-window sleep come back, but vsync stays off
  until restart, so the flat game can run uncapped. A unit test proves a missing loader fails closed; it goes red when the presenter's catch is removed
  *(observed 2026-11)*.
- **The copy** *(observed 2026-11, harness readback)*: `glBlitFramebuffer` from framebuffer 0 into a
  `GL_SRGB8_ALPHA8` swapchain image, with `GL_FRAMEBUFFER_SRGB` and the scissor test off, keeps every byte (the clear
  colour read back unchanged) and keeps the orientation with no flip (each corner colour where OpenGL's bottom-up
  layout puts it, which is OpenXR's OpenGL convention). Whether the colours look right in the headset is for the
  headset test.
- **Live runtime** *(observed 2026-11, SteamVR/OpenXR 2.18.4, a Lighthouse headset, not worn)*: the runtime offers
  `XR_KHR_opengl_enable` and asks for OpenGL 4.3 or newer. A bare MonoGame DesktopGL window with the presenter went
  Idle, Ready, Synchronized, Visible; it submitted every frame for 12 s without a failure and recreated the swapchain
  when the back buffer was resized. The display period was 13.33 ms, and the loop ran at 74.7 frames per second once
  started.
- **Full game** *(observed 2026-11, the same runtime and headset, not worn; FreeAllegiance Artwork at `4a60aa6` and the
  AllegianceCore sky pack at `5272ded`; `--training --vr`, 45 s)*: the session went Idle, Ready, Synchronized,
  Visible with a 1280x720 swapchain and no fall to flat. The training mission ran (weapons fire, a warp transit). In
  flight the loop ran at about 75 frames per second against the 13.33 ms period, with 90 % of frames under 13.8 ms.
  The same build without `--vr` ran at 60 frames per second on a 60 Hz monitor with vsync. The warning
  `ASR sound unavailable` appears with and without `--vr`, so VR does not cause it.
- **In the headset** *(observed 2026-11-25 by ragesaq)*: the game shows as a floating screen, not a VR view. That is
  milestone 1's design: one flat image on a quad layer, with no stereo and no head tracking. The image was upright
  and not mirrored, its colours matched the flat game, and motion looked smooth *(observed 2026-11-25 by ragesaq)*.
  After closing SteamVR, ragesaq saw the desktop window unchanged and the game still running. The run's log shows
  the session Focused to the end and the headset's pacing in every window up to the game's own shutdown, with no
  Stopping state and no `[vr] running flat` line *(observed)*. So the fall to flat did not happen in that run; SteamVR
  likely stayed running while the game held its session *(inferred, not checked)*. The live fall to flat is still
  unobserved.
- **Frame cost with vsync off** *(observed 2026-11-25, flat, `ALLEGIANCE_NOVSYNC=1`, 1280x720 scene, 35 s)*: about
  3.6 ms per real frame in steady windows (the `[frameprof]` means halved, see Traps). It was noisy: two windows had a
  p95 of 23 ms. A stereo frame at headset resolution has several times as many pixels, so this does not yet show
  whether the post-processing fits at the headset rate *(derived)*.

## Milestone 2 (approved 2026-11-25)

Per-eye rendering of the scene with the head pose *(decided 2026-11 by ragesaq: "yes, start milestone 2", in
Port's working session; approving the plan below as proposed)*. Rejected: stopping at the floating screen. Tracked
as `DAG.json` node `ragesaq-007`.

- Each frame, `xrLocateViews` gives the two eye poses and fields of view. The scene is drawn once per eye into a
  per-eye swapchain and submitted as a projection layer instead of the quad.
- `Draw` advances state each time it runs: the HUD frame stamp, camera smoothing, the sound listener and telemetry
  (`Game1.Rendering.cs:262-440`) *(read)*. So the camera is resolved once per frame as now, and only the scene pass
  repeats per eye *(derived)*. That needs the inline scene pass (`Game1.Rendering.cs`, from the camera to the
  composite) moved into a method that takes view, projection, eye and target, with the flat path unchanged.
- The head pose adds to the ship's camera in the cockpit view; the eye offset needs the size of a game unit in metres
  (an open question below).
- The HUD goes on its own quad layer in front of the player.
- Checks: the flat frame must stay the same before and after the move, compared frame by frame with the deterministic
  headless replay and capture mode; the per-eye frame time measured in the headset before the post-processing is
  kept or cut.

## Milestone 2 as built (branch `port/vr-m2`)

Built on `port/vr-m2`, stacked on `port/vr-m1`; merged as `2572cdd`. Seen in the headset: see the next section.

- **The scene runs in a loop, not a new method.** The plan above said to move the scene pass into a method. As built,
  `Draw` resolves the camera once and wraps the scene, from the bloom redirect to the end of post-processing, in
  `for (int vrPass = 0; vrPass < VrScenePassCount; vrPass++)` with the body not re-indented *(decided 2026-11 by Port,
  for upstream mergeability: about 1,200 upstream lines keep their bytes, and upstream changes to the scene still
  merge; rejected: extracting a method, which would move every line)*. A flat frame runs it once with every VR field
  at its flat value.
- **The second eye repeats only the drawing.** `dt` is 0 for it, and it skips particle emission (each `Emit` adds at
  least one particle), the trail update, the effect-lights gather, flare occlusion, the target-model preview and the
  GPU timer and telemetry *(read: each call site in `Game1.Rendering.cs`)*. Hull hits are drained by
  `TakeNewHullHits`, and the asteroid spin takes an absolute clock, so both are safe to repeat *(read)*. The dust
  field is re-projected for the second eye without being stepped (`NativeScene.GatherDust(..., advance: false)`):
  the native debris field, used when "Subtle dust" is off, accumulates distance and draws from its spawn RNG on each
  step *(found by the PR #9 round 1 review; tested: `VrScenePassTests`)*.
- **One game unit is one metre** *(derived: the radar labels plain world distance in metres, native
  `radarimage.cpp:1209`, `docs/reports/HUD1_HUD_PARITY.md:107`)*. So the headset's eye offsets apply unscaled
  (`VrEyeCamera.GameUnitsPerMetre`). This answers the first open question below; how the cockpit scale feels is for
  the headset test.
- **Eye camera.** The head rotation applies in the camera's frame (`head * cameraRotation`), and the eye offset turned
  into that frame moves `cameraEye`, which every draw subtracts from positions (P207) *(tested: `VrEyeCameraTests`)*.
  The eye projection is XNA's off-centre projection for the headset's four angles, with the game projection's depth
  terms copied unchanged.
- **Post-processing is off in the eye passes** for now: upscaling, HDR, bloom, antialiasing and soft particles. Their
  targets are sized for the window. Whether to bring them back per eye waits on the headset frame time.
- **The HUD.** After the eye passes the back buffer is cleared to transparent and the HUD and radar are drawn into it
  with the game's own camera. The presenter shows it on a quad 2 m ahead, sized so it covers the game camera's
  vertical field of view, with `XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT`. The desktop window then shows
  the HUD over black in stereo frames, a known limitation. The HUD buffer holds premultiplied colour, which is what
  OpenXR assumes when `XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT` is unset; without the blend bit the layer's
  alpha would be one and the HUD an opaque sheet *(read: OpenXR specification, "Composition Layer Blending")*. The
  quad is fixed in the local space, so it stays ahead of the seat rather than following the head.
- **Not in stereo yet:** the opt-in aleph destination glimpse (`--aleph-glimpse`) is drawn after the eye passes, so
  it is skipped in stereo frames rather than landing on the HUD layer.
- **Known gap:** the eye render targets are created inside `Draw` without a catch, so a failure to create them (about
  210 MB at 3708x3564) would end the game rather than run it flat *(found by the PR #9 closeout review; not
  observed)*. Node `ragesaq-008` holds the fix.
- **Flat output unchanged** *(observed 2026-11-25: replay frames 300, 1500, 3600, 6000 and 9000 of
  `data/benchmarks/battle.alr` captured before the change, after it, and after the round 1 review fixes are
  byte-identical; see the PR)*.
- A frame with no tracked orientation, or one the game did not draw in stereo, is shown as milestone 1's opaque quad.
- Each eye has its own swapchain at the runtime's recommended size, so no runtime can refuse an image wider than its
  `maxSwapchainImageWidth`. The eye render targets are released when the VR sink is dropped.
- **Stereo runs at the headset rate in the harness** *(observed 2026-11-25: SteamVR/OpenXR 2.18.4, lighthouse
  headset not worn, `--training --vr` for 90 s, run twice)*. The runtime recommended 3708x3564 per eye. The first
  run used one side-by-side swapchain of 7416x3564; the second, after the round 1 fixes, one 3708x3564 swapchain per
  eye. The HUD swapchain is 1280x720 and the back buffer has 8 alpha bits. In both runs `[frameprof]` counted 750
  frames per 5 s, 75 per second after halving (the trap below), against a 13.33 ms display period, with no `[vr]`
  error. Whether the picture is upright, the eyes converge and the HUD sits right is for the headset.

## Milestone 2 in the headset (2026-11-25)

ragesaq wore the headset with a build whose source matches `master` at `e73b573` (`--training --vr`, SteamVR/OpenXR
2.18.4, lighthouse headset). The session reached Focused with two 3708x3564 eye swapchains *(observed 2026-11-25 by
ragesaq, reported in Port's working session with a screenshot of SteamVR's VR View, which is not committed)*.

- **Upright:** yes. The row flip is right.
- **3-D objects:** correct, with good distance and scale. One game unit as one metre feels right.
- **Background:** the starfield, planets and galaxies show double vision; the eyes do not converge on them. Cause
  *(derived from the code, not yet confirmed by a fix)*: `NativeSkyPlacement.SkyProjection` rebuilds the sky's
  projection from the scene projection's `M11` and `M22` only, which gives a symmetric frustum. An eye's projection
  is off-centre (`M31` and `M32` are not zero), so each eye's sky is shifted sideways, the two in opposite
  directions. All three sky tiers call it. Node `ragesaq-009`.
- **HUD:** shown, and fixed ahead of the seat as designed, but close and large. At 2 m and sized to the game
  camera's default 50° vertical field of view it is about 3.3 m by 1.9 m, about 80° by 50° of view. Some elements
  appear twice, overlapping, at different slants. The flat HUD at 1280x720 does not overlap *(observed in a flat
  replay frame)*. Whether the second copies are 3-D cockpit screens drawn in the eye passes is not checked yet.
  Node `ragesaq-010`.
- **Capture:** ragesaq's OpenXR capture tool showed the stereo view but not the HUD, which is on its own quad
  layer *(observed by ragesaq)*. Node `ragesaq-011`.
- **The HUD moves into the eye images** *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25: "the
  game should send the hud into the main view, other games have done HUDs on separately layers and it causes
  problems"; rejected: keeping the HUD on its own composition layer, as milestone 2 built it, and fixing capture in
  the capture tool)*. Each eye pass draws the HUD into its own image, so the projection layer is the whole picture
  and any tool that records it gets the HUD. Node `ragesaq-010` carries the work; it also closes `ragesaq-011`.

## Traps

- **Replay frames are byte-identical only when the capture has the GPU to itself.** A recapture run beside a stereo
  smoke run drew each frame in about 23 ms instead of about 2 ms, and frames 1500 and 3600 differed: ship textures
  stream in on a background loader, so a slow run reaches a frame with a different set loaded. Run alone, the same
  build matched all five *(observed 2026-11-25)*. Capture with no other game client running before reading a
  mismatch as a change.
- **MonoGame sleeps 20 ms per frame while its window is not active** (`Game.InactiveSleepTime`). In VR that halves the
  headset frame rate whenever another window has focus *(observed 2026-11: 45 frames per second against a 75 Hz
  period, 74.7 with the sleep off)*. VR mode sets it to zero.
- **Saved benchmark settings turn vsync back on at startup** (`InitializeBenchmark`, then `ApplyBenchmarkSettings`).
  Vsync and the headset's frame wait would then both pace the loop. `ApplyBenchmarkSettings` calls `KeepVrPacing()`,
  which keeps the flat choice for a fallback and leaves the headset in charge *(derived from the code)*.
- **Creating an OpenXR instance starts SteamVR** when it is not running *(observed 2026-11)*. A probe run is not free
  of side effects on the desktop.
- **A projection rebuilt from its field of view loses an eye's off-centre terms.** `NativeSkyPlacement.SkyProjection`
  keeps only `M11` and `M22` of the scene projection, so in an eye pass the sky is drawn with a symmetric frustum
  and does not converge *(observed in the headset 2026-11-25; cause derived from the code)*. In a pass that runs per
  eye, keep `M31` and `M32` and replace only the depth terms.
- **A far plane recovered from a float projection is about 0.1% off.** `M43 / (M33 + 1)` at near 0.5 m and far
  10 000 m gives 10 010.8 *(observed 2026-11 in a unit test)*: `M33 + 1` cancels to a few ulps. Copy `M33` and `M43`
  instead of rebuilding a projection from recovered planes.
- **MonoGame stores a render target's rows top first** (its OpenGL backend flips clip space when a target is bound),
  the reverse of the back buffer, so eye images are copied into the swapchain with rows flipped *(derived from
  MonoGame's OpenGL backend; to be confirmed in the headset)*.
- **`XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT` does not premultiply.** It enables the layer's alpha;
  unset, the alpha is one. Colour is taken as premultiplied unless `XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT`
  is set *(read: OpenXR specification source, `rendering.adoc`)*. A review that read the blend bit as a second
  premultiply was answered with this.
- **MonoGame clears a `DiscardContents` render target each time it is bound**, so the eye targets use
  `PreserveContents`: the target-model preview binds another target and comes back to the scene's *(read)*.
- **The `[frameprof]` line counts each flight frame twice.** `UpdateSessionPerformance` runs from `UpdateBenchmark`
  (`Game1.Benchmark.cs:285` on `master`) and again from `UpdateFrame` (`Game1.FlightInput.cs:221`) *(read)*. Once
  the player is in flight, the line reports twice the real frame count and half the real mean frame time. The
  second interval is near zero, so its p95 is about the 90th percentile of real frames *(derived, and matched by
  a flat run: 600 counted frames per 5 s with vsync at 60 Hz)*. Halve the count before comparing it with the headset
  rate. The fix belongs in its own PR, not in VR work.

## Open questions

- What one game unit is in metres. It sets the eye separation and the cockpit scale. *(Answered above: one metre,
  derived; the scale felt right in the headset, 2026-11-25.)*
- Whether bloom, HDR output and FSR can run per eye at headset frame rates, or must be cut in VR mode.
- Whether upstream would accept a VR head if it stays behind a clear seam.

## Timeline

- 2026-11-24: read the client at upstream `b76d6c0`. ragesaq decided to build the VR client here and enroll the
  fork. No code changed.
- 2026-11-24: ragesaq approved milestone 1 as proposed. Built on `port/vr-m1` from `master` at `47e720b`; probed
  against SteamVR with a harness. The headset test by ragesaq is still to come.
- 2026-11-24: ragesaq approved cloning the FreeAllegiance Artwork and the AllegianceCore sky pack. The full game ran
  in the SteamVR session at the headset rate (above); the headset is still to be worn.
- 2026-11-25: ragesaq wore the headset and saw a floating screen rather than a VR view. Port proposed milestone 2
  (above), and ragesaq approved it as proposed.
- 2026-11-25: built milestone 2 on `port/vr-m2`: eye passes, the projection layer and the HUD quad. The flat replay
  frames are unchanged. The headset test by ragesaq is still to come.
- 2026-11-25: the cross-family review of PR #9, round 1, passed with conditions. Three low findings were fixed and
  each eye got its own swapchain; the HUD blend finding was answered from the OpenXR specification
  (`.hyperreview/README.md`). A delta review of the fixes is next.
- 2026-11-25: the delta review, round 2, passed. It accepted the HUD blend dispute on the specification text and
  found one stale comment, fixed. The headset test by ragesaq is next.
- 2026-11-25: the closeout review passed; its one low item is node `ragesaq-008`. ragesaq approved merging PR #7 and
  then PR #9 once each is ready. PR #7 merged as `1c521ad`, and PR #9 was retargeted to `master`.
- 2026-11-25: PR #9's first `build-and-test` against `master` failed in a step the public log does not show; the
  rerun on the records-only head `c19e5ce` passed, and PR #9 merged as `2572cdd`. ragesaq's headset test of
  milestone 2 is next.
- 2026-11-25: ragesaq's headset test of milestone 2: upright, and the 3-D scene converges at the right scale. The
  sky does not converge, the HUD is close and large with doubled elements, and capture misses the HUD: nodes
  `ragesaq-009` to `ragesaq-011`.
