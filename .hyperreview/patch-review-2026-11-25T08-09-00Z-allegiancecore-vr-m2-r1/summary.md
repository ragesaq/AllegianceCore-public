# Patch review: VR milestone 2 (per-eye rendering, HUD quad)

- Target: `ragesaq/AllegianceCore-public` PR #9, branch `port/vr-m2`, stacked on `port/vr-m1` (PR #7).
- Reviewed head: `3677ec6f0b854d3bfb7095c1329a1fe00dfc6a80`; tree `52add9f33a4bbd8c7c829c6379e5c9d61506bfa3` (matched `git write-tree`).
- Range: `f945af76..HEAD`, two commits.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high` (reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; explicit selection, the route declared in AGENTS.md "Cross-family review route").
- Author family: Anthropic/Claude (Claude Code, Claude Opus 5.5, seat Port). Cross-family.
- Clone: standalone detached clone, `<review-clone>`.
- **Verdict: PASS-WITH-CONDITIONS.** The frame seam, eye camera, OpenXR frame balance and GL copy hold up. F1 should be fixed or confirmed in the headset before the headset test; F2 to F4 are cleanups.

## Findings

### F1 (Medium) - the HUD quad's alpha flag premultiplies an already-premultiplied buffer
`src/Allegiance.ClientCore.Vr/OpenXrPresenter.cs:413` sets `CompositionLayerFlags.BlendTextureSourceAlphaBit` on the HUD quad in stereo frames. That flag tells the compositor to multiply the layer's colour by its alpha. The buffer it copies is already premultiplied: `EndVrScenePasses` clears the back buffer to `Color.Transparent` (`src/Allegiance.ClientCore.Game/Game1.Vr.cs:84`), and the HUD draws with `BlendState.AlphaBlend` over premultiplied textures (`src/Allegiance.ClientCore.Game/Rendering/HudRenderer.cs:2749`; the decoder is `MdlTextureDecoder.ToPremultipliedRgba`), which leaves `rgb = rgb x a` in the buffer. The compositor multiplies by alpha a second time.

Scenario: any HUD pixel with `0 < a < 1` (font anti-aliasing, translucent panels, radar fades) renders at roughly `a` times its intended brightness. Text edges go dark and thin; fully transparent and fully opaque pixels are unaffected, so it reads as a soft, dirty HUD rather than an obvious break.

Fix: drop the flag, since premultiplied is what the compositor assumes by default. If the buffer is ever changed to straight alpha, use `UnpremultipliedAlphaBit` instead. Confirm in the headset; nothing here has been seen there yet.

### F2 (Low) - the debris dust field steps twice per frame in VR
`src/Allegiance.ClientCore.Game/Game1.Rendering.cs:679` calls `NativeScene.GatherDust` inside the per-eye loop. With "Subtle dust" off, the `else` branch runs `debrisGeo.Step(...)` (`src/Allegiance.ClientCore.Game/Rendering/NativeScene.cs:51`), which mutates accumulated distance, the speed estimate and the spawn RNG (`src/Allegiance.ClientCore.Game/Rendering/DebrisGeo.cs:186`). Pass 1 adds the ~3 cm inter-eye move to `_accumulated` a second time and computes `_speed` from zero elapsed time, so `_distancePerParticle` differs from the flat frame. The visible impact is small (a few centimetres against a ~7 m spacing), but the invariant the knowledge note claims is broken, and a grain spawned in pass 1 exists for the right eye only.

The default path is benign: `SubtleDust.Step` (`src/Allegiance.ClientCore.Game/Rendering/SubtleDust.cs:30`) refills only on a >100 m jump, so a 3 cm eye move is a no-op and both eyes see the same grains.

Fix: add a `step` flag to `GatherDust` (or split `Step` from the projection) and pass `false` on repeat passes, so pass 1 only re-projects the field for the right eye.

### F3 (Low) - eye render targets are not released when the sink drops
`VrFrameSinkHost.Drop` disposes the sink, but `_vrLeftEye` and `_vrRightEye` are freed only by `DisposeVrSink` (`src/Allegiance.ClientCore.Game/Game1.Vr.cs:131`), which runs from `Dispose(bool)` (`src/Allegiance.ClientCore.Game/Game1.RelativeMouse.cs:46`). After a mid-run VR failure the flat game keeps two eye targets alive: at the runtime's recommended size (3708x3564 per eye) about 105 MB each, so roughly 210 MB of GPU memory held for the rest of the session for nothing.

Fix: release the eye targets when the host is no longer attached, for example from the `Abandon` path in `SubmitVrFrame` and at the top of the next `BeginVrFrame`, still re-creating them on a re-attach.

### F4 (Low) - the aleph glimpse (opt-in) now lands on the HUD quad
`DrawAlephGlimpses` is a 3-D scene effect ("composites AFTER the opaque scene", `src/Allegiance.ClientCore.Game/Game1.Rendering.cs:1769`) but sits after `EndVrScenePasses`, so in stereo it draws into the transparent HUD buffer with the centre camera. The funnels would appear flat on the HUD plane with no parallax instead of inside the eyes' scene. Default off (`--aleph-glimpse`), so Low.

Fix: move the call inside the pass loop (it only reads, so it is safe to repeat per eye) or document the VR limitation.

### Observation (for the headset test, not a defect)
The HUD quad uses `Space = _space` (LOCAL) with a fixed pose (`src/Allegiance.ClientCore.Vr/OpenXrPresenter.cs:422`), so it is world-locked: turning your head moves the HUD out of view. That may be the intended milestone-2 behaviour, but if the HUD is meant to stay readable while looking around, a head-locked space (`XR_REFERENCE_SPACE_TYPE_VIEW`) is a small change. The quad is also sized to the game camera's vertical field of view and centred on the tracking origin, so HUD-to-world alignment drifts if the player leans.

## What I verified

By execution (read-only, on the frozen clone):

- `git write-tree` printed the expected tree; `git status --short` was empty; head equals the reviewed head.
- `git ls-files --eol` on all 16 changed files: the index side is `i/lf` for every file, including the upstream `Game1.Rendering.cs`. No line-ending renormalization was committed; the CRLF in the working tree is the host checkout, not the change.
- `scripts/publish-guard.sh f945af76 HEAD` reported OK for 2 commits.
- `tools/check_data_boundary.py` reported OK. `tools/check_memory.py` reported OK with the nine expected `recorded-by 'port' has no folder in people/` warnings that AGENTS.md says are expected.

By reading, with the call paths traced:

- Flat-path invariance: every guard reduces to the original expression when `_vrFrame` is null (both pass flags false, `VrScenePassCount` 1, `VrSceneDestination` returns its argument for `_vrPass == -1`, `EndVrScenePasses` returns at once). `view`, `proj` and `cameraEye` are untouched on the flat pass. I re-audited the whole ~1,200-line loop body for per-pass state: the gathers clear and refill (idempotent), the LOD counters are reset inside the loop so they are not doubled, `NextFarState` is pure hysteresis and idempotent for the same pixel radius, and `dt` is 0 on the repeat pass so `_trailNowSec`, `_treasureAge` and `_flightEffects.Advance(0)` do not advance. `_spaceMotion.Step` gets `dt = 0` and only bumps a sequence number no consumer reads. F2 is the one stateful call that does not hold.
- Stereo: each eye binds its own `PreserveContents` `Depth24Stencil8` target and clears it; sky, dust, ships, particles, hull-hit sparks and shield flares all draw with the eye's `view`, `proj` and `cameraEye`. Hull hits are drained (`TakeNewHullHits` clears the list), so they are emitted once and drawn from the pool in both eyes. Radar, command grid and HUD are drawn after the camera is restored, into the transparent buffer.
- Eye maths: the XNA row-vector composition `head * cameraRotation` gives the head rotation in the camera's frame; the offset uses `TransformNormal` by the camera rotation; the off-centre projection is `CreatePerspectiveOffCenter` at near = 1 with the game's `M33` and `M43` copied. The tests assert against `Matrix.CreatePerspectiveOffCenter` itself, all four edges mapping to +/-1, and equal depth.
- OpenXR frame balance: every `BeginFrame` path either opens no frame, closes a leftover one first, or fails and tears down; the `xrWaitFrame` and `xrBeginFrame` error paths call `SkipFrame` or `Check`. `SubmitLayers` ends with no layers when `ShouldRender` is false and closes an open frame before rethrowing a `VrUnavailableException`. `EndDraw` reaches `SubmitVrFrame` on every path except the two `Exit()` capture paths. Layer order is projection then quad; the sub-image rects tile a `2 x eyeWidth` swapchain.
- GL: the blit brackets and restores the read/draw bindings, `GL_FRAMEBUFFER_SRGB` and scissor state, checks framebuffer completeness, drains earlier errors, and flips rows only for render-target sources.

## Limits

- No `dotnet`, build, test or game run: the 0-warning build, the 44 VR tests, the byte-identical replay frames, the 21 baseline failures and the 90 s SteamVR log are the author's measurements, not re-run here.
- No headset: the HUD premultiply (F1), the eye-image row-flip direction, the HUD's world-locked placement and the eyes' convergence are reasoned, not observed.
- MonoGame and Silk.NET sources are not in the clone; their blend-state and GL texture behaviour is taken from documented behaviour and the repository's own comments.
- Creating a swapchain inside an open frame, and a swapchain twice the recommended width, both worked on SteamVR. The specification forbids neither, but another runtime could reject the doubled width if it exceeds `maxSwapchainImageWidth`.