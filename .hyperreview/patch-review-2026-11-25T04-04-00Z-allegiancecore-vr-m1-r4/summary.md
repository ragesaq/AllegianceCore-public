# Patch review: VR milestone 1 (PR #7), round 4 delta

**Reviewed head** `e71e079e453da2516f0ad586d83b2059d15ba2cb`, **tree**
`283fb410b0852d127e989a1bf67c4c24aabd6a8d`, delta `969988dd..e71e079e` (the round 3 record, then the fix), in the
standalone review clone at the head of `port/vr-m1`. `git write-tree` printed that tree before and after the run, and
`git status --short` shows only this artifact folder.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`, explicit selection
(reviewer-parity matrix 4.14.0, sha256 `74618f22...`). **Author family:** Anthropic (Claude Code, Port).
Cross-family. Light delta tier.

**Verdict: PASS.** The fix closes round 3's finding 1 on every skip path, the record correction answers finding 2,
and I found no defect in the eight changed lines.

## Round 3 dispositions

1. **Closed.** `SkipFrame` (`OpenXrQuadPresenter.cs:368-372`) now calls `KeepContextCurrent()` before
   `Thread.Sleep`, and all three skip arms go through it: `xrWaitFrame` (`:303`), `xrBeginFrame` (`:312`) and
   `xrEndFrame` (`:361`). Every exit from a frame call that can swap the GL context now restores the game's context
   before the game presents (`Game1.GpuBudget.cs:554`).
2. **Closed in the record.** The README's round 2 entry no longer says the runtime discards the frame. It states the
   consequence round 3 named: if a runtime enforces the frame-call order, the next `xrWaitFrame` fails and VR falls
   back to flat for that run, with the reason logged. That matches `Check` (`:437`) -> `VrUnavailableException` ->
   `Fail` (`:400-407`). The alternative round 3 offered, a best-effort empty `xrEndFrame`, was declined with a
   reason. The reason is sound for the state that produces the skip: while the session is not running that call
   returns `XR_ERROR_SESSION_NOT_RUNNING` too, and once `STOPPING` arrives `Present` (`:71-76`) never reaches the
   frame calls. Round 3 allowed recording the consequence instead, so the disposition is compliant. The residual
   case is a runtime returning a stale `NOT_RUNNING` while the session still runs; there an empty `xrEndFrame` could
   have succeeded, but the recorded consequence still describes the outcome.

## Does the new throw leave a frame open?

No. `xrWaitFrame` (`:303`) and `xrBeginFrame` (`:312`) failed, so no frame was begun. On the `xrEndFrame` skip
(`:361`) the end call was issued; if a runtime still held the frame open, the exception path destroys the session
(`Fail` -> `TearDown`, `:417-433`) before any further wait, so no call-order failure can follow. `SkipFrame` is
reached only from inside `Present`'s `try` (`:63-86`); its `VrUnavailableException` is caught at `:80`, `Fail` sets
`Failed`, and the host drops the sink (`VrFrameSinkHost.cs:60`) and restores the flat pacing (`:77-83`). The game's
own present then runs flat.

**The double restore on the `xrEndFrame` skip** is harmless: after `SkipFrame` restores, `:363` compares the current
context with `_hglrc` and returns without a WGL call. This thread's current context cannot change during
`Thread.Sleep`, so the second call is a no-op.

## Findings

None. Scoped to the eight changed lines and the record delta; I could not construct a failing scenario.

## Notes

- `SkipFrame` and the idle path (`:74-76`) still sleep the same interval at two sites; round 3's note stands.
- No test drives a skip: no runtime here returns `XR_ERROR_SESSION_NOT_RUNNING`, so the fix is verified by reasoning
  and the author's evidence, not by a reproduction.

## Records

- The README round 3 entry matches the committed summary finding by finding (verdict, two lows, dispositions, the
  withdrawn round 2 suggestion). Its head `969988d` is tree `e693147`, matching the clone, and the round 2 entry
  carries the corrected sentence.
- The receipt's head, tree, range, author/reviewer tuples and matrix hash match this clone and the task's route
  coordinates; its wrapper-state and clone-ref claims cannot be checked here.
- `DAG.json` node `ragesaq-002` stays `review`, `updated` 2026-11-24, and its `next` names this round, the closeout
  review, then the headset test.
- No machine path, secret or non-noreply address in the delta; record files and the committed source blob are LF
  with no BOM.

## What I verified, and how

**By execution (read-only):** `git rev-parse`, `git write-tree`, `git status --short`, `git log`, `git show`,
`git diff` from `969988dd` to `e71e079e`, `git show -s --format=%T` for the recorded heads, `git ls-files --eol`, byte
checks of the committed blobs for CR and BOM, and `git grep` for addresses and machine paths. `bash scripts/publish-guard.sh 47e720be HEAD` -> OK (7 commits); `check_data_boundary.py` -> OK;
`check_memory.py` -> OK (9 entries, only the documented `recorded-by` warnings).

**By reading only:** the OpenXR frame-call and session-state rules, `Present`/`SubmitQuadFrame`, the host's drop
path, and the flat present.

**Not run:** `dotnet build`, the test tiers, the game, a headset. The author's 0 warnings, 24 VR tests, the
fast-tier comparison and the 824-frame harness run are not reproduced. **Limits:** the skip paths stay unexercised
here, and the round 3 summary's claimed byte-identity with the reviewer's original cannot be checked from this clone
(its internal facts do match).

## Handoff

Nothing in this delta blocks the in-headset test; residual risks are the unexercised skip paths and the accepted
call-order fallback to flat.
