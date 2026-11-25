# VR client survey: where OpenXR attaches to the AllegianceCore client

**Owner:** ragesaq · **Started:** 2026-11-24 · **Last reviewed:** 2026-11-24 · **Review by:** 2027-02-24

**Status:** in progress (milestone 1 built, awaiting review and the headset test)

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
  Either way the game runs flat. A unit test proves a missing loader fails closed; it goes red when the presenter's catch is removed
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
  started. The full game was not launched: this host has no artwork corpus.

## Traps

- **MonoGame sleeps 20 ms per frame while its window is not active** (`Game.InactiveSleepTime`). In VR that halves the
  headset frame rate whenever another window has focus *(observed 2026-11: 45 frames per second against a 75 Hz
  period, 74.7 with the sleep off)*. VR mode sets it to zero.
- **Saved benchmark settings turn vsync back on at startup** (`InitializeBenchmark`, then `ApplyBenchmarkSettings`).
  Vsync and the headset's frame wait would then both pace the loop. `ApplyBenchmarkSettings` calls `KeepVrPacing()`,
  which keeps the flat choice for a fallback and leaves the headset in charge *(derived from the code)*.
- **Creating an OpenXR instance starts SteamVR** when it is not running *(observed 2026-11)*. A probe run is not free
  of side effects on the desktop.

## Open questions

- What one game unit is in metres. It sets the eye separation and the cockpit scale.
- Whether bloom, HDR output and FSR can run per eye at headset frame rates, or must be cut in VR mode.
- Whether upstream would accept a VR head if it stays behind a clear seam.

## Timeline

- 2026-11-24: read the client at upstream `b76d6c0`. ragesaq decided to build the VR client here and enroll the
  fork. No code changed.
- 2026-11-24: ragesaq approved milestone 1 as proposed. Built on `port/vr-m1` from `master` at `47e720b`; probed
  against SteamVR with a harness. The headset test by ragesaq is still to come.
