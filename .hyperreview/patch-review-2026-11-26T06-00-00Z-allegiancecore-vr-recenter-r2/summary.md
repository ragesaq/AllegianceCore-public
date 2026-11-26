# Patch review: VR seated recenter (PR #18), round 2 delta

- Target: `ragesaq/AllegianceCore-public` PR #18, branch `port/vr-recenter`, stacked on
  `port/hotas-bindings` (PR #14) at `35d8f3f3`.
- Reviewed HEAD: `7d3b45f0461f4dd23f10463adac5a1f024a0076d`. Reviewed tree (`git write-tree`):
  `b93ffbb8adccd5603b319a89ef562893f9856bd3`. Both matched the assigned target; the worktree was
  clean before the review.
- Delta: `90979ec3..HEAD`, three commits: `44299ac4` (the fix), `8695bb2b` (merge of base
  `35d8f3f3`; no source change) and `7d3b45f0` (the round 1 record). The fix is 3 files, +122/-15.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high` (reviewer-parity
  matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; the
  route declared in `AGENTS.md`). Cross-family to the author (Port, Anthropic Claude Opus 5.5).

**Verdict: PASS.**
**nextAction:** ragesaq's headset test, then the merge decision. F4 below is Low and optional; the
change is correct without it.

## Round 1 findings

- **F1 (Low), fixed.** Every failing call in `RecenterSpace` is checked in place and returns before the
  swap, so `_space` stays valid and the session keeps running. The composed yaw and origin are checked
  for finiteness before the only OpenXR call that would receive them (`RecenterSpace`, lines 470-505).
  No throwing call remains on the recenter path. Ending the request on an error is defensible: the
  likely errors (session not running, limit reached) cannot be cured by retrying 90 frames, and the
  player can press again. The non-finite case is unit-tested only through `IsFinite`; no test would
  fail if the call site stopped using it (see Observations).
- **F2 (Low), fixed.** The press now waits in `VrRecenterRequest`: `Press()` arms 90, `Done()` clears,
  `Missed()` counts one untracked frame and reports exhaustion. `WaitAndLocate` calls `Missed()` once per
  begun frame and only on the untracked branch; a successful recenter clears the request. A second press
  restarts the wait. Removing `volatile` is safe: `Recenter()` reaches the presenter from
  `Game1.FlightInput.cs:1443` (Update) and `WaitAndLocate` from Draw, both on the MonoGame game thread.
- **F3 (Low), fixed.** `HeadTurnedLeft90_FacesTheSpaceAlongMinusX` (test line 41) asserts the resulting
  forward vector, not just the implementation's formula, and pins the sign (`forward.X == -1`), so a
  mirrored yaw fails. `PressWhileUntracked_WaitsThenRunsOut` (line 65) fails if the countdown is removed.

## New findings

### F4 (Low): a pending press survives a session stop and can fire much later

`src/Allegiance.ClientCore.Vr/VrRecenterRequest.cs:5` says "a press made long before does not fire
later", but `OpenXrPresenter.cs:100-105` returns before `WaitAndLocate` whenever `!_sessionRunning`, so
`Missed()` does not advance while the headset is idle. The request is not cleared on the
`VrSessionAction.End` transition (`OpenXrPresenter.cs:291-294`).

Scenario: the player presses recenter just before lifting the headset (or as tracking drops). The session
leaves RUNNING (`Stopping` -> `EndSession`, state IDLE) with, say, 80 untracked frames left. Ten minutes
later the headset is worn again; `Ready` -> `Begin` sets `_sessionRunning = true`, and the still-pending
press applies on the resume frame, moving the flat panel to wherever the head is then. The wait is
bounded at 90 untracked running frames, but not in wall time, so "about a second"
(`VrRecenterRequest.cs:9`, `knowledge/joystick-input.md`) holds only while frames keep being begun.

Why it matters: a stale button press becomes a silent recenter at an unrelated moment, contrary to the
class contract. It is Low because the result is a valid recenter, and the user can press again.

Suggested fix: cancel the request on the `Stopping`/`End` transition (a `Cancel()`, or `Done()`, in the
`VrSessionAction.End` case), so presses made before the stop are dropped while presses made during the
idle period still apply at the next `Begin`; correct the class doc and the knowledge note to "90 untracked
frames of a running session"; add a stop-then-resume case to the request's tests. Timestamping the press
and expiring it on wall time is the alternative.

## Observations (read, not defects)

- The in-place checks keep exactly `Check`'s boundary: `Check` itself throws only for `result < 0`
  (`OpenXrPresenter.cs:685-688`). The old space is destroyed only after the replacement exists
  (`:497-505`), so an early return leaks nothing.
- `IsFinite` checks the composed yaw and origin, which are the values sent to OpenXR, rather than the raw
  quaternion. That is the right boundary; a finite quaternion cannot make `atan2` return a non-finite
  yaw.
- `xr.DestroySpace` remains unchecked (`:504`), as before; a failed destroy could leak one handle but
  leaves `_space` valid. This matches the existing `TearDown` pattern.
- Frame counting: `BeginFrame` is called once per game frame (`Game1.Vr.cs:58`); the recenter block
  (`:329-333`) is after `xrBeginFrame` and before the `!_shouldRender` return, so `Missed()` advances
  once per begun frame, including non-rendering idle frames, and not on the `ErrorSessionNotRunning`
  early returns (`:314`, `:323`). This is the pause described in F4.
- Tests: `TrackingReturns_TheWaitingPressIsCarriedOutOnce` (line 81) verifies `Done()`-then-`Press()`
  state, but not that the recenter is applied exactly once; `NaNPose_IsNotFinite` (line 57) covers the
  helper, not the `RecenterSpace` call site. Neither gap is a defect in this change.
- Record: the committed round 1 summary's sha256 is
  `5db6f8ece2dfcdb20b0bcc2dfa78154c88fedbab36942866b7336308d7baf1d8`, equal to the receipt's value;
  it is unchanged. The receipt's head/tree (`90979ec3` / `c4fe7008`) match the commit, and the receipt is path-free. The README
  dispositions match the code. `knowledge/joystick-input.md` matches, except its "about a second" phrasing (F4); DAG node
  `ragesaq-014` matches.
- Merge: `8695bb2b`'s parents are `44299ac4` and `35d8f3f3`, and the merge introduces no source change
  (`git diff 44299ac4 8695bb2b -- src .../VrRecenterTests.cs` is empty).
- Upstream: narrow, seam-shaped diff; every changed index blob is LF (`git ls-files --eol`, `i/lf`; the
  local checkout's CRLF is not committed); `git diff --check` clean; no machine path, device serial,
  secret or non-noreply address in the added lines.

## Verification

Executed: `git rev-parse`, `show`, `log`, `diff`, `cat-file`, `ls-files --eol`, `write-tree`, `status`,
`grep`, `diff --check`; the publish guard over `35d8f3f3..HEAD` (`OK, 7 commits`);
`tools/check_data_boundary.py` (`OK`); `tools/check_memory.py` (`OK`, with the expected `people/`
warnings and the reported missing-baseline limit). All target commands were read-only; at the end, `git status` showed only this review directory untracked, and `HEAD`/`write-tree` were unchanged.

By reading only: the build, the tests and all OpenXR runtime behaviour. I did not run `dotnet`, the game
or a headset; did not run `check_map.py` or `recall.py`; and could not inspect the launcher logs or route
state behind the receipt.

## Limits

The author's execution evidence (97 tests pass, client build with 0 warnings, the two mutation results)
is reported, not reproduced here. Whether the runtime reaches the stop/resume path in F4 depends on the
operator's headset and runtime; F4 is derived from the code's lifecycle handling. Headset testing by
ragesaq remains the acceptance step, and the runtime acceptance of a mid-frame `xrCreateReferenceSpace`
still rests on the author's report.