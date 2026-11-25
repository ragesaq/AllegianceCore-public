# Patch review: VR milestone 1 (PR #7), round 3 delta

**Reviewed head** `969988dd4ead1b34d25ea5a6f85a8c090d88a17f`, **tree**
`e6931474d36bda9a82f92b65c408f11e714da494`, delta `b3a8ea37..969988dd` (the round 2 record, then the fixes) in
the standalone review clone at the head of `port/vr-m1`. `git write-tree` printed that tree before and after the
run, and `git status --short` showed only this artifact folder.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`, explicit selection
(reviewer-parity matrix 4.14.0, sha256 `74618f22...`). **Author family:** Anthropic (Claude Code, Port).
Cross-family. Light delta tier.

**Verdict: PASS-WITH-CONDITIONS.** Round 2's three findings are closed as the README says, the fixes are correct
on the paths they touch, and two low items remain.

## Round 2 dispositions

1. **Closed.** `KeepContextCurrent()` is now the first statement inside the `try` (`OpenXrQuadPresenter.cs:324`), so
   its `VrUnavailableException` reaches the `catch` (`:327`) and the begun frame is ended empty (`:336`) before the
   rethrow. Ending it needs no GL context: the layer count is zero and no swapchain image is held, so the call is
   valid although the game's context could not be made current, and its result is ignored before the teardown that
   follows. `KeepContextCurrent` throws nothing else (`:387-392`), so WGL load failures still reach `SubmitFrame`'s
   filter (`:59-86`) as they did for the copy path.
2. **Closed, and the author's choice is sound.** Sleeping on the skip is the right pace for the uncapped loop.
   Keeping `_sessionRunning` is better than my round 2 suggestion to clear it: the flag is owned by the event
   stream (`:230`, `:233`), and clearing it on a frame-call result would stick VR idle if a runtime returned a stale
   `NOT_RUNNING` while the session really runs, because no later state event would arrive to restore it. Retrying
   at 11 ms until `STOPPING` arrives is recoverable; the stuck-idle state is not. I withdraw the earlier suggestion.
3. **Closed.** The round 1 entry now cites `Game1.Benchmark.cs:199`, `:452` and `:467`; at this head those three
   statements sit on exactly those lines, `ApplyBenchmarkSettings` still takes the flat steps at `:184-188`, and the
   fixes do not touch that file.

## Findings

### 1. Low: the two skip returns leave the runtime's GL context current

`OpenXrQuadPresenter.cs:303` and `:312`. Both new `{ SkipFrame(); return; }` arms return without
`KeepContextCurrent()`, while every other frame-call exit restores it (`:363`, and `:324` before the copy). The
file's own rule is that an XR call may swap the GL context, which is why the restore exists. `SubmitVrFrame`
(`Game1.GpuBudget.cs:550`) returns just before `base.EndDraw()` presents (`:554`), so a runtime that switched contexts
inside `xrWaitFrame` or `xrBeginFrame` would have the flat window's present issued on the runtime's context: a lost
or wrong present on the desktop mirror while the skips last. It needs a runtime that both swaps and returns that
error, so it is unlikely. Fix: `KeepContextCurrent();` before each `return`, one comparison when nothing changed.

### 2. Low (record): the open frame's retry consequence is not recorded

`OpenXrQuadPresenter.cs:361` and the README's round 2 entry. When `xrEndFrame` returns `NOT_RUNNING` the frame stays
begun and `_sessionRunning` is deliberately left true, so the next `SubmitQuadFrame` calls `xrWaitFrame` with a
frame still open. A runtime enforcing the frame-call order answers `XR_ERROR_CALL_ORDER_INVALID`, `Check` (`:437`)
makes that a `VrUnavailableException`, and `Fail` makes it a permanent flat run for that session. That is the
concrete form of round 2's residual risk, reachable only if `STOPPING` is late or absent. The README instead states
the settled opposite ("the session is not running, so the runtime discards the frame"), which round 2 explicitly
left unestablished. Fix, either way: track a begun-frame flag and send a best-effort empty `xrEndFrame` before the
next wait, or record the retry consequence and accept it, rather than asserting the frame is discarded.

## Notes

- The new assertion (`VrFrameSinkHostTests.cs:101`) is behavioural and holds for the right reason: `Attach` saves
  20 ms, `TakeOverPacing` zeroes it (`VrFrameSinkHost.cs:70-75`), and `KeepPacing` saves only the fixed step
  (`:43`), so a `KeepPacing` that also saved the sleep would make `Drop` (`:77`) restore zero and fail the
  assertion. It pins one asymmetry as intended, that `KeepPacing` honours `IsFixedTimeStep` and ignores
  `InactiveSleepTime`; nothing in the game sets the sleep (`Game1.Vr.cs:36-40`), so that is harmless today and now
  visible. Re-attach after `Dispose` stays untested, as round 2 noted.
- `SkipFrame` (`:366`) duplicates the inline sleep at `:74`; one helper would leave one pacing rule instead of two.
- Records: the README round 2 entry matches the committed summary finding by finding, and the receipt's head and
  tree match this clone (`b3a8ea37` is tree `ad3bbde`; `30c2ae6a` is tree `37638b87`, the round 1 receipt's value).
  The round 1 folder is untouched across `7913415c..969988dd`. The DAG node stays `review`, `updated` on the
  commit's date, and its `next` names this round and then closeout before the headset test, as this fork requires.
- The delta holds no machine path, secret or address beyond agent noreply forms; the record files are LF, no BOM.

## What I verified, and how

**By execution (read-only):** `git rev-parse HEAD` and `git write-tree` (values above, re-read after the run),
`git status --short`, `git log`, `git show`, `git diff` per file, `git show -s --format=%T` for the three recorded
heads, an empty `git diff 7913415c 969988dd` over the round 1 folder, `git ls-files --eol`, `git grep`, a scan of
the diff and record files for drive paths and addresses (none), and a byte check for BOM and CRLF (none).
`bash scripts/publish-guard.sh 47e720be HEAD` -> `[publish-guard] OK (5 commit(s))`; `check_data_boundary.py` -> OK;
`check_memory.py` -> OK (9 entries, only the documented `recorded-by` warnings).

**By reading only:** the OpenXR frame-call order and session lifecycle, the presenter's state machine, the host's
pacing handover and the tests. `check_map.py` and `recall.py` were not run, as the instruction rules both out here.

**Not run:** `dotnet build`, the test tiers, the game and a headset, so the author's 0 warnings, 24 VR tests, the
20 fast-tier failures and the 822-frame harness run are not reproduced. **Limits:** no runtime here returns
`XR_ERROR_SESSION_NOT_RUNNING`, so both findings are argued from the specification and the code, and the committed
round 2 summary's byte-identity with the reviewer's original cannot be checked from this clone.
