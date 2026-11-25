# Patch review: VR HUD drawn into the eye images (AllegianceCore PR #15, step 1)

- **Reviewed head:** `1cf7172515747d362299e7b10a8c671f96c144c0`
- **Reviewed tree:** `15311120b7af41a6ea362bb007f88b01d664be6d` (`git write-tree` matched; `git status` clean)
- **Range:** `3a3b6e1b..1cf71725`; the second commit only names PR #15 in `DAG.json`.
- **Reviewer:** DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high`
  (reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`;
  standing at high; explicit selection, the route the target's `AGENTS.md` "Cross-family review route" declares).
- **Authoring family:** Anthropic (Claude Code, agent seat Port). Cross-family review.
- **Verdict:** PASS-WITH-CONDITIONS
- **nextAction:** Fix F1 (restore the panel's contact list, or correct the note if an empty scope is
  intended), then re-run the VR tests and a left-eye dump that shows radar blips on the panel, push, and
  re-review the delta. F2 and F3 are cheap and worth taking with it.

## Findings

### F1 (High): in VR the panel's radar draws no contacts at all

`Game1.VrHud.cs:108-109` clears `_drawRadarContacts` and `_drawShipTags` after gathering the markers.
Every radar draw path reads `model.Context.Contacts`, which the HUD model builds from that same list
(`Game1.Rendering.cs:3347`): Classic (`HudRenderer.cs:798`), Modern and Jet
(`JetHudRenderer.Radar.cs:189-190`), the label blocks and the brackets (`RadarBracketRenderer.cs:302`).
With the list empty, a stereo frame renders an empty radar scope with no blips, no labels and no
brackets on the interim panel.

Failure scenario: fly with contacts around you in VR; the panel radar is a bare scope, so the only
instrument that shows what is behind you is gone, for every skin. The change's own note says the
opposite (`knowledge/vr-hud-design.md`, "Step 1 as built": "the Jet skin's radar scope is not re-run in
the wide frame, so in VR it shows only contacts inside the game camera's frame"), so either the code
over-clears or the note is wrong.

Suggested fix: keep the wide-frame gather for the markers, then rebuild the flat list for the panel,
e.g. call `DrawRadar(view, proj, cameraEye, timeSeconds)` once more with the game camera before
returning (`DrawRadar` only fills lists), or hold a copy of the game-camera contacts and restore both
lists instead of clearing. The panel is a small screen showing the flat HUD, so the flat contact set is
the right content for it. If an empty scope is the intent, say so in the note and in the step's limits.

### F2 (Low): the selection and lock brackets can break the stated floor and cap

`Game1.VrHud.cs:182` (`* 1.15f`) and `:219` (`* 1.3f`) scale the angle *after* `BracketAngle` has
clamped it to 2°..20° (`VrHudGeometry.cs:138-140`). A 20° lock bracket becomes 26°, a floor bracket
2.6°. The note states brackets are "never under 2° across (capped at 20°)". Suggested fix: scale the
radius or the distance before the clamp, or clamp again after scaling.

### F3 (Low): marker size uses the centre-of-image angular scale at every pixel

`VrHudGeometry.PixelsForAngle` is documented for "the centre of an image"
(`VrHudGeometry.cs:127-131`) and is used at `Game1.VrHud.cs:357` for markers anywhere in the eye
image. A rectilinear image's angular scale grows as `1/cos^2` from the centre: at 45° off-axis it is
about twice the centre value, so a marker there covers about half its intended angle. With a ~100°
per-eye field of view, peripheral brackets and the 0.6° text floor are effectively halved. Suggested
fix: convert the angle at the marker's own pixel, or record the approximation in the note.

### F4 (Low): `ReleaseVrHud` releases one of three GPU resources

`Game1.VrHud.cs:436-440` disposes `_vrHudImage` only. `_vrPixel` (`:319`) and `_vrPanelEffect`
(`:256`) are created once and never disposed; `ReleaseVrEyeTargets` calls `ReleaseVrHud` when the
sink is dropped, so a dropped-and-recreated sink would keep a stale `BasicEffect`/texture for a device
that may have been reset; the leak is bounded by the device lifetime.

### F5 (Low): the stereo frame runs `DrawRadar` twice and the proof line reports the wide frame

`Game1.VrHud.cs:92` re-runs `DrawRadar` with the wide head-turned camera. `DrawRadar` uses no time
input, so this is a pure gather, but it also re-runs `RefreshInvestState()` (`Game1.Panes.cs:1072`,
idempotent) and overwrites `_radarProofLine` (`Game1.Rendering.cs:4673`) with the wide-frame contact
set and counts. Only diagnostics change (the line exists behind `_radarProofEnabled`), but a later
HUD-5b proof run in VR would read the wide frame as if it were the game frame.

## Observations

- **Geometry matches the game's convention.** `RayThroughPixel` inverts exactly what `ProjectToScreen`
  does (`Game1.Camera.cs:765-777`): pixel to NDC with +Y down, `x = (ndcX + M31)/M11` at `z = -1`,
  `w = -z` for `M34 = -1`; `TryProject`/`TryProjectDirection` reproduce the same pixel mapping (and
  `:791-805`). Off-centre `M31`/`M32` are used in both directions, and `VrEyeCamera.OffCentreProjection`
  writes `M31`/`M32` in that same convention.
- **Contact recovery is exact for on-screen contacts.** `RadarBracketLayout.Resolve` returns the true
  NDC pixel when `OnScreen` (`RadarBracketLayout.cs:113-117`), and `CameraDistance` is measured from
  the same `cameraEye` the direction is scaled from (`Game1.Rendering.cs:4052, 4060`), so
  `cameraEye + direction * distance` is the object's position. Off-screen contacts are filtered
  (`contact.OnScreen`), which is the documented no-edge-arrow limit.
- **The wide frame is eye-independent.** `VrRadarCamera` zeroes the eye offset and keeps `frame.Left`'s
  orientation, so the reconstructed point does not depend on which eye gathered.
- **Per-eye markers use the real eye cameras.** `_vrEyeCameras[pass]` is captured in
  `BeginVrScenePass` (`Game1.Vr.cs:70`), so each eye projects with its own offset and FOV and the
  marker carries the object's disparity. The reticle uses `TryProjectDirection` (w = 0), so both eyes
  give the same pixel.
- **The flat path cannot reach the new code.** `GatherVrHudMarkers` returns at once unless
  `_vrEyesDrawn` (`Game1.VrHud.cs:79`), `CentreReticleInWorld` defaults to false and is fed
  `_vrEyesDrawn`, and `EndVrScenePasses` returns before binding anything when `_vrPass < 0`.
- **Nulling the marker fields changes drawing only.** The six fields are read nowhere except the HUD
  model build (`Game1.Rendering.cs:3327-3329`); no input, audio, targeting or network path reads them.
  `_drawRadarContacts` has one other reader, the P818 chat proof (`Game1.ChatSubjectProof.cs:293`),
  which is diagnostic.
- **OpenXR layers are consistent.** A stereo frame submits one projection layer with two views and
  never touches the `_screen` swapchain; a flat frame submits one opaque quad. `_screen` is created
  lazily, so a session that only ever sees stereo frames never allocates it, and `DestroySwapchain`
  tolerates that. The stereo path returns after `EndFrame`, inside the same try/catch that closes the
  frame on failure.
- **Compose ordering is safe.** The eye target is unbound before it is sampled for the window mirror,
  and `BlendState.AlphaBlend` is XNA's premultiplied blend, which matches the premultiplied HUD image
  (the panel comment is correct).
- **State after `ComposeVrHud`:** back buffer bound, `Opaque`/`None`/`CullNone`/`LinearClamp` left
  behind. The flat path already leaves sprite-batch states (AlphaBlend/None/CullNone/LinearClamp) at
  the end of every frame, so this is the same class of carry-over. `Abandon` on a `ComposeVrHud`
  failure rebinds the back buffer and drops the sink, so the game runs flat.
- **Upstream mergeability is clean.** `git diff --ignore-all-space --numstat` equals `--numstat` for
  every file, so nothing was reformatted or renormalised; the upstream hot file
  (`Game1.Rendering.cs`) gained two lines, `HudModel.cs`/`HudModelAdapter.cs` five and three. Every
  touched file is LF in the index.
- **Records:** `DAG.json` parses; nodes `ragesaq-010`/`ragesaq-011` move to `review` with the PR link
  and a `next` that matches the built state, including the `ALLEGIANCE_VR_HUD_PANEL` knob and the
  awaited gates; the `rules` block is untouched. `knowledge/vr-client-survey.md`'s new near-plane
  paragraph matches `SeatProjection`. The one note claim that does not match the code is the
  radar-scope bullet (F1).

## Tests

The new tests exercise behaviour, not the implementation: a round trip through the real
`RadarProjection` and `RadarBracketLayout`, an off-centre inversion, the wide-frame recovery (including
that the game camera alone would clamp), per-eye disparity against the object's own projection, the
panel's corners/normal/depth range against the game's 1 m and 10 m near planes, environment parsing and
its fallbacks, the reticle's identical pixel in both eyes, the seat view holding the panel still, and
the `HudModel` flag with the additive-reticle gate. `VrHudGeometryTests` holds 25 cases (13 facts, 12
inline data), which matches the author's count.

Gaps worth naming:

- no test that a stereo frame keeps the panel's contact list; such a test would have caught F1;
- no test for a marker behind the eye (`TryProject` false) or for a marker outside the image;
- no test that `GatherVrHudMarkers` leaves every flat field untouched (needs a game harness; the code
  path is short enough to read);
- no test of the bracket scaling (F2) or of the off-centre pixel-size error (F3).

## Verification

By execution, read-only on the frozen target: `git write-tree` (expected tree), `git status --short`
(clean), `git diff --stat`, `git diff --numstat` against `git diff --ignore-all-space --numstat`,
`git ls-files --eol` on every touched file, `scripts/publish-guard.sh 3a3b6e1b HEAD` (OK, 2 commits),
`tools/check_data_boundary.py` (OK), `tools/check_memory.py` (OK, 10 entries; only the expected
`recorded-by` warnings), a JSON parse of `DAG.json`, and a count of the new test cases.

By reading only: all rendering, frame-loop, OpenXR and state behaviour above. I did not run dotnet,
the tests, the game, a replay or a headset.

Limits: the build (0 warnings), VR tests (74 of 74), mutation checks, byte-identical flat replay
frames and the left-eye dump are taken as reported, not re-observed. I could not measure the cost of
the second `DrawRadar`. `check_memory.py`'s entry-deletion baseline could not run in this clone (no
remote or branches); the tool reported that as a limit rather than a pass.