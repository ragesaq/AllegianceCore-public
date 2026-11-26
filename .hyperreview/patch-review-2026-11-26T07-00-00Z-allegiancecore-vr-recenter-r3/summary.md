# Patch review: VR recenter (PR #18), round 3 delta

- Target: `ragesaq/AllegianceCore-public` PR #18, `port/vr-recenter`, stacked on `port/hotas-bindings`
  (PR #14) at `35d8f3f3`.
- Reviewed HEAD `d9522e226730bb94d50ca4df3cca47c8bef14de3`, tree
  `50417d183b39a9376994984a9fd6528a3b3ac1d8`; both matched, worktree clean.
- Delta `7d3b45f0..HEAD`, one commit `d9522e22`: `Cancel()` and its `VrSessionAction.End` call, the class
  comment and one note sentence, `SessionStop_DropsAWaitingPress_AndALaterPressWaitsAgain`, and the round 2
  record.
- Reviewer: DeepSeek, `deepseek-v4.1-flash` via OpenCode Go, `high` (matrix 4.14.0, sha256
  `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; route from `AGENTS.md`). Cross-family
  to Port (Claude Opus 5.5).

**Verdict: PASS.**
**nextAction:** ragesaq's headset test, then the merge decision.

## F4: fixed, and `End` is the right place

The scenario: a press made just before a stop kept its residual 90-frame count through the idle period, so
`Begin` applied it on resume, minutes later. `End` now clears it (`OpenXrPresenter.cs:291-295`); a press made
while idle still arms and applies on the first begun frame after `Begin`.

`Stopping` is the only state mapping to `End` (`VrSessionLifecycle.cs:20`). `Exiting` and `LossPending` map to
`TearDown`, which calls `Fail()`; `TearDown()` clears `_sessionRunning` (`OpenXrPresenter.cs:653-656`), but
`Failed` stays true and `BeginFrame` returns at its first line (`:87`). The instance-loss event fails before
the switch (`:276-280`), and `Dispose` sets `_disposed` (same guard). `End` is the only stop that leaves the
presenter alive.

The tick cannot run while stopped: it is after a successful `xrBeginFrame` (`:330-334`), `PollEvents` drains
the queue first (`:98`), and both `ErrorSessionNotRunning` paths return before it (`:314-315`, `:323-325`). A
queued `Stopping` is applied before a resumed frame can spend one, including the window documented at
`:311-313`.

A press made while stopped is preserved: `Begin` only sets `_sessionRunning = true` (`:286-290`), `Press()`
arms the full wait (`VrRecenterRequest.cs:19`), and nothing clears it before the next begun frame. `Recenter()`
comes from `UpdateFlying` (`Game1.FlightInput.cs:1443`), before Draw's `BeginVrFrame`
(`Game1.Rendering.cs:265`). Only a second `Stopping` event could clear an idle press, and the lifecycle does
not produce one while idle.

## New findings

None.

## Observations (not defects)

- `Cancel()` is byte-identical to `Done()` (`VrRecenterRequest.cs:22,25`); two documented names for one
  transition is defensible. `TearDown()` does not call it, but a failed presenter cannot tick the request.
- In the window where the runtime has stopped but its `Stopping` is not yet polled, that frame's press is
  cancelled. This is F4's conservative choice, and the app still believed the session ran.
- The new test (`VrRecenterTests.cs:94-105`) goes red if `Cancel()` is a no-op (line 101). It does not
  exercise the presenter's `End` wiring or a resume; the author reports that call as read only.
- The round 2 record is accurate: the committed `summary.md` (7666 bytes) hashes to
  `d4dc907342b80f79bcd764185ab7f3dbdbdca154caeca744880bcb27fa7d80de`, equal to the receipt, and the delta
  adds both files with no later edit. The receipt's head
  `7d3b45f0461f4dd23f10463adac5a1f024a0076d`, tree `b93ffbb8adccd5603b319a89ef562893f9856bd3`, range
  `90979ec3..7d3b45f0` (three commits) and PASS verdict match the summary and commits; its round 1 hash
  `5db6f8ece2dfcdb20b0bcc2dfa78154c88fedbab36942866b7336308d7baf1d8` matches the round 1 receipt. README
  dispositions and DAG `next` match. Nuance: the README's "the wait counts only frames of a running session"
  compresses the mechanism; the linked summary states the residual count precisely.

## Verification

Executed, read-only: `rev-parse`, `write-tree`, `status`, `log`, `show`, `diff`, `diff --check` (clean), tree
lookup, `ls-files --eol` (all changed blobs `i/lf`; C# `w/crlf` here only), hashing, `git grep`,
`publish-guard.sh 35d8f3f3 HEAD` (OK, 8 commits), `tools/check_data_boundary.py` (OK). No BOM or CRLF, no
machine path, secret or non-noreply address in the added lines.
By reading only: the build, the tests, the runtime's state sequence, and the round 2 launch state behind the
receipt.

## Limits

The author's evidence (48 VR tests, a 0-warning build, the `Cancel` no-op mutation) is reported, not
reproduced. The argument assumes a spec-conformant runtime that reports `Stopping` and does not repeat it
while idle, as the presenter already assumes (`OpenXrPresenter.cs:311-313`). Headset testing remains the
acceptance step.
