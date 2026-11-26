# Patch review: VR seated recenter (PR #18)

- Target: `ragesaq/AllegianceCore-public` PR #18, branch `port/vr-recenter`, stacked on
  `port/hotas-bindings` (PR #14) at `7c6632bb`.
- Reviewed HEAD: `90979ec3b6212c5b8c7908e63d9871a8d9d7e50f`. Reviewed tree (`git write-tree`):
  `c4fe70083afa59696fa98b3e8e4ed3fbd03b1aa0`. Both matched the assigned target; worktree clean.
- Diff: `7c6632bb..HEAD`, 4 commits (two authorship, one base merge `f11ebe12`, one doc-comment move
  `90979ec3`), 12 files, +243/−11.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high` (reviewer-parity matrix
  4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; the route declared in
  the repository `AGENTS.md`). Cross-family to the author (Port, Anthropic Claude Opus 5.5).

**Verdict: PASS.**
**nextAction:** ragesaq's headset test, then the merge decision. The findings below are Low and optional;
F1 is the one worth folding in first, because it decides what a bad press costs.

## Findings

### F1 (Low): a failed recenter takes down the whole VR session

`src/Allegiance.ClientCore.Vr/OpenXrPresenter.cs:468` (`Check(xr.LocateSpace(...))`) and `:484`
(`CreateSpace`, which checks `xrCreateReferenceSpace`) sit inside `WaitAndLocate`, whose caller catches
`VrUnavailableException` and calls `Fail` (`:108-111`), setting `Failed` and tearing the session down. The
game then runs flat for the rest of the run.

Scenario: the operator presses recenter on a frame where either call returns an error (a session transition
racing the call, `XR_ERROR_RUNTIME_FAILURE`, or `XR_ERROR_LIMIT_REACHED` on a runtime that caps reference
spaces). One convenience press ends VR until restart. It is the only place in this change where a
non-essential action can fail the session; `xrLocateViews` is different because the frame cannot be drawn
without it.

Fix: call `LocateSpace`/`CreateReferenceSpace` directly, test `Result`, log and return on failure, leaving
`_space` alone. The ordering already supports that: the old `_space` is destroyed only after the new one
exists, so an early return leaks nothing. Optional in the same edit: skip the swap when the located pose is
not finite, so a degenerate runtime quaternion cannot reach `xrCreateReferenceSpace` as a NaN pose.

### F2 (Low): a recenter pressed while the head is untracked is consumed and lost

`:329-333` clears `_recenterRequested` before `RecenterSpace` decides whether it can act; `:469-473` then
logs "recenter skipped" and returns. The press is gone.

Scenario: the player presses just as the headset loses tracking (doff, overlay handover). Nothing happens,
the flag is already cleared, and the button looks dead until pressed again.

Fix: clear the flag in the success path only, and rate-limit the skip log. This answers the "can a recenter
be lost" question: it cannot be applied twice, but it can be dropped this way.

### F3 (Low): the yaw tests share the implementation's forward-axis premise; the swap is untested

`tests/Allegiance.ClientCore.MonoGame.Tests/VrRecenterTests.cs:12-16`: `YawDegrees` recomputes
`Atan2(-forward.X, -forward.Z)`, the same expression as `SeatedOrigin`. A sign error is still caught (the
expected value comes from the test's own `InlineData` turn), but "-Z is forward" is identical on both sides
and cannot be caught there. Nothing tests `RecenterSpace`: flag consumption, swap order, failure paths.

Fix: add one assertion stated independently of the helper, e.g. a head turned +90° about +Y gives a space
pose near (0, −sin45°, 0, cos45°) that maps `-Z` onto `+X`. A seam over the space create/destroy calls would
make the swap and F1/F2 paths testable, but that is more than this PR needs.

## Observations (read, not defects)

- Swap validity: `_space` is read only at `xrLocateViews` (`:346`) and the two layer spaces (`:408`, `:428`),
  all after the swap at `:329` and all rebuilt each frame. The previous frame's layers were consumed by
  `xrEndFrame`, and a frame left open is closed at `:97`, so nothing refers to the destroyed space. Creating
  and destroying a space between `xrBeginFrame` and `xrEndFrame` is allowed.
- Math: `-Z` is OpenXR forward, so `Atan2(-f.X, -f.Z)` is the yaw carrying `-Z` onto the head's floor-projected
  forward. The quaternion is about `+Y` only, so the runtime's gravity keeps the horizon level, and the origin
  at the head pose puts the recentred head at the origin facing `-Z`. Pitch and roll stay in the head pose,
  which is the right split for a seated sim.
- Runtime LOCAL recentre moves the offset space with LOCAL; the next press recomputes from `_viewSpace` in
  `_localSpace`, so offsets never accumulate. Same exposure as before the change.
- Position not tracked: the origin falls back to zero, so the yaw turns about the LOCAL origin rather than the
  head and the head offset stays in the eye position. Logged, and only reachable with `PositionValidBit`
  clear while `OrientationValidBit` is set.
- Visible side effect to expect in the headset test: the HUD/flat quad pose is `(0,0,-D)` in `_space` (`:436`),
  so a recenter re-places that panel in front of the head's facing at the press instant. Pressing while
  looking sideways swings the panel there.
- Threading: `Recenter()` (from `UpdateFlying`) and `WaitAndLocate` (from `Draw` → `BeginVrFrame`) both run on
  the MonoGame game thread; nothing else touches the presenter. `volatile` is unnecessary but harmless, and
  the check-then-clear at `:329-331` has no reachable race today.
- No leak or double free: create-before-destroy, nothing destroyed after the swap, and `TearDown` (`:639-646`)
  guards each handle, so a partial `Initialize` failure is clean too.
- Input: the edge is correct and composes with the resolver's arming (`JoystickBindings.cs:288-303`), which
  forgets a device's state when it disappears and re-arms only after a release, so a button held across a
  reconnect cannot fire. `ViewRecenter` precedes `Target`, so `JoystickKeyActions` (`:44`) still excludes it.
  It shifts the target commands' ordinals, but nothing persists `JoystickCommand` by number:
  `JoystickBindingFile` uses `JsonStringEnumConverter(allowIntegerValues: false)` (`:336`) and no other module
  reads the enum. Button 42 collides with no VPC binding. The latch is reachable docked and in flight: the
  block at `Game1.FlightInput.cs:1439-1444` runs before `HandleDockedScreen` (`:1510`) and outside the
  `composing` gate, as the comment says.
- Tests that do hold: `VrFrameSinkHostTests` covers pass-through, the default no-op, and the throwing sink
  being dropped and disposed; `JoystickBindingResolverTests` binds button 42 and keeps the command out of the
  key-action map.
- Records: `DAG.json` node `ragesaq-014` matches the code (42, logical 43), needs an existing node, and uses
  the repository's owner/status vocabulary; `knowledge/joystick-input.md` describes the mechanism accurately.
- Upstream mergeability: narrow, seam-shaped diff; every changed blob is LF (`i/lf` in `git ls-files --eol`);
  `git diff --check` clean; no machine path, serial or non-noreply address in the added lines.

## Verification

Executed: `git rev-parse`, `write-tree`, `status`, `log`, `diff`, `show`, `grep`, `ls-files --eol`,
`diff --check`, `cat-file`; the publish guard over `7c6632bb..HEAD` (`OK, 4 commits`);
`tools/check_data_boundary.py` (`OK`); `tools/check_memory.py` (`OK`, expected `people/` warnings plus the
reported missing-baseline limit).

By reading only: the build, the tests, and all OpenXR behaviour. I did not run `dotnet`, the game or a
headset, and did not run `tools/check_map.py` or `tools/recall.py` as instructed.

## Limits

No runtime evidence for the recenter itself: that the composed image stays glued to the world across the
swap, that this runtime accepts a mid-frame `xrCreateReferenceSpace`, and that button 42 is the button the
operator means all rest on the author's report, the recorded capture and the operator's choice. Headset
testing by ragesaq remains the acceptance step.
