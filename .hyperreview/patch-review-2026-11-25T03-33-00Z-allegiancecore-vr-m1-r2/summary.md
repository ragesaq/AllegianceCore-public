# Patch review: VR milestone 1 (PR #7), round 2 delta

**Reviewed head** `b3a8ea376cc136a3322e422107075dc3f0a5c159`, **tree**
`ad3bbdebc6f6c2f8e43bafc8c0f96751cf8a295e`, delta `30c2ae6a..b3a8ea37` (the round 1 record, then the fixes) in
the standalone clone at the head of `port/vr-m1`. `git status --short` was empty at the start and now shows
only the untracked round 2 folder.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`, explicit selection
(reviewer-parity matrix 4.14.0, sha256 `74618f22...`). **Author family:** Anthropic (Claude Code, Port).
Cross-family. Light delta tier.

**Verdict: PASS-WITH-CONDITIONS.** Round 1's findings are answered; two low-severity gaps remain, both cheap to close or to record as residual.

## Round 1 dispositions

1. **Closed.** `GlBlitter.DrainEarlierErrors` (`GlBlitter.cs:122-136`) logs five drained batches in detail plus one line saying it stops (`:131-135`). The game's
   `HdCompressedUpload` guard still sees a cleared error, but the codes are no longer lost silently.
2. **Closed.** `KeepContextCurrent()` runs before `GlBlitter.Create` (`OpenXrQuadPresenter.cs:135`) and before every
   copy (`:321`), covering the swapchain creation that precedes it.
3. **Closed for the exception the copy raises.** `CopyFrameIntoSwapchain` only throws `VrUnavailableException` (`:368-379`, `Check` at
   `:433-436`), and the new `catch` ends a `LayerCount 0` frame before rethrowing (`:326-337`). Finding 1 covers a
   path the fix misses.
4. **Closed.** `XR_ERROR_SESSION_NOT_RUNNING` from `xrWaitFrame` (`:302`), `xrBeginFrame` (`:311`) and `xrEndFrame`
   (`:360`) now skips the frame. Finding 2 covers that path's pacing.
5. **Disposition right.** `ApplyBenchmarkSettings` sets the flat choice, then calls `KeepVrPacing` and
   `ApplyChanges` (`Game1.Benchmark.cs:184-188`), so the saved value is the one the flat game has at that moment;
   the end and failure paths re-apply the original the same way. The narrower case is real: `BeginBenchmark` clears
   the fixed step directly (`:199`), so a VR failure before the next `ApplyBenchmarkSettings` restores the
   pre-benchmark step for that run. Bounded by the run, so residual risk is fair. The cited line numbers are off by one (finding 3).
6. **Closed.** Seven behavioural tests; two (`VrFrameSinkHostTests.cs:65,82`) are exactly those that fail when `Drop` stops
   restoring the fixed step, so the red-test claim holds.
7. **Closed.** `DAG.json` has `status: review`, the PR #7 URL in `links` alongside an existing repo file, and a
   `next` naming the round 1 head, which meets the rules block's review-node floor.
8. **Closed.** The survey note names both log lines; `mem-009`'s `seen-in` carries the environment versions.

## Findings

### 1. Low: the new context check sits outside the copy's `try`

`OpenXrQuadPresenter.cs:321`. `xrBeginFrame` has succeeded (`:312`); `KeepContextCurrent()` then throws
`VrUnavailableException` if `wglMakeCurrent` fails (lost or reset context). Control leaves before the `catch` at
`:326`, so no empty `xrEndFrame` is sent and the session is torn down with the frame open. That is the state
finding 3's fix exists to prevent; the fix covers only throws from `CopyFrameIntoSwapchain`. Round 1 established
that destroying a session with an open frame is legal, so this is a consistency gap. Fix: move the call to the
first statement inside the `try` (`:323`), or wrap the whole `ShouldRender` block in the same handler.

### 2. Low: the `NOT_RUNNING` skips bypass the idle sleep

`OpenXrQuadPresenter.cs:301-312`. The early returns leave `_sessionRunning` true, so the 11 ms sleep at `:74` is
skipped. With VR attached the loop is uncapped (vsync off, `IsFixedTimeStep=false`), so if the runtime returns
`XR_ERROR_SESSION_NOT_RUNNING` before posting `STOPPING`, or never posts it, the game spins at full frame rate
instead of using the path that exists for that state. Normally this lasts a frame or two until `STOPPING`. Fix:
set `_sessionRunning = false` before returning (the `:71-75` idle path then sleeps and the `READY` event's Begin
action resumes), or sleep in the early return.

Related, residual risk rather than a defect: `xrEndFrame` returning that error at `:360` leaves a begun frame never
ended. The session is not running, so the frame is discarded and a later `xrBeginSession` starts a fresh frame
loop; neither the spec text nor a runtime here establishes that a restart is always clean. The same one-line fix
makes the intent explicit.

### 3. Low (record): finding 5's line numbers are off by one

The README entry cites `Game1.Benchmark.cs:198` for the direct fixed-step set and `:451,466` for the re-applies;
at this head they are `:199`, `:452` and `:467`. The file is unchanged by the fixes, so the numbers were already
off at `30c2ae6a`. The substance is right.

## Notes

- **The empty frame on a copy failure is legal.** The copy releases the acquired image in its `finally`
  (`:372-379`), so none is held at the `catch`; `LayerCount 0` after acquire/release is valid, and `DisplayTime`
  and blend mode match the normal empty frame (`:351-358`). Ignoring the result is safe: the throw ends VR.
- **Tests** are behavioural (pacing values, dispose and submit counts, the log line) against fake pacing and sink.
  Gap: nothing asserts `InactiveSleep` after `KeepPacing()` then a fallback, and re-attach after `Dispose` is
  untested. Notes, not defects.
- **`VrFrameSinkHost` keeps the old behaviour.** Attach saves both flat values and takes over; `KeepPacing` saves
  only the fixed step (`:43`), so the sleep is not clobbered; `Drop` restores both and disposes once; `Dispose` is
  idempotent; a second sink is refused. `Attach` now refuses null, which no caller relies on: `Program.cs:745` is
  the only attach, unconditional, before `Run()` (`:765`).
- **Records.** The GL log is bounded to six lines per session. `mem-009` and the survey note are accurate. The DAG `next` pins
  the round 1 head but not the current head; minor.
- New files are committed LF with a CRLF checkout, like existing upstream source.

## What I verified, and how

**By execution (read-only):** `git rev-parse HEAD` and `git write-tree` (values above, unchanged after the run),
`git status --short` (only the untracked artifact folder), `git log`, `git show`, per-file `git diff 30c2ae6a b3a8ea37`, `git ls-files --eol`,
`git grep`, `git rev-parse 30c2ae6a^{tree}` (matches the round 1 receipt), and the round 1 folder unchanged across
`7913415c..b3a8ea37`.
`bash scripts/publish-guard.sh 47e720be HEAD` -> `[publish-guard] OK (3 commit(s))`; `check_data_boundary.py` ->
OK; `check_memory.py` -> OK (9 entries, only the documented `recorded-by` warnings).

**By reading only:** OpenXR frame-call semantics, the GL drain, the host's state machine and the tests. I did not
run `check_map.py` or `recall.py` (the instruction rules both out here).

**Not run:** `dotnet build`, the test tiers, the game and a headset, so the author's 0 warnings, 24 VR tests and
821 frames are not reproduced. **Limits:** the `NOT_RUNNING`-after-`xrBeginFrame` window is unexercised by anyone;
context restoration is still argued from one runtime (SteamVR 2.18.4).
