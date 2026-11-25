# Closeout review: VR milestone 1 (PR #7)

**Reviewed head** `f36ee317b2aeee3e81c214d80c615ffcabc4cee5`, **tree** `4f34e859d766f0ff4b217c1ec23b5039fc221cfe`
(`git write-tree`), range `47e720be..f36ee317`, 8 commits, in a standalone detached clone at the head of `port/vr-m1`.
The PR's GitHub head was the same SHA at review time; `git status --short` showed only this artifact folder.

**Reviewer:** DeepSeek, `deepseek-v4.1-flash` via OpenCode Go, effort `high`, explicit selection (matrix 4.14.0,
sha256 `74618f22...`), the route in `AGENTS.md`, 'Cross-family review route'. **Author family:** Anthropic (Port).
Cross-family. Closeout-verification mode.

**Verdict: PASS-WITH-CONDITIONS.** Ready for the in-headset test. Four Low items, three of them records; none blocks
the headset test or corrupts the flat game.

**nextAction:** `ragesaq` runs the headset test. Before the merge decision the Linux CI `build-and-test` job on
`f36ee317` must finish green, and the Low record items should be fixed in a prose-only commit or waived explicitly.

## Finding ledger

| # | Finding | Status | Evidence |
|---|---|---|---|
| R1-1 | Low-Med: copy drained GL errors the game watched | Closed | `GlBlitter.cs:122-136` logs them |
| R1-2 | Low: context unchecked after swapchain creation | Closed | `OpenXrQuadPresenter.cs:135`, `:324` |
| R1-3 | Low: begun frame left open on copy failure | Closed | empty frame at `:327-338` |
| R1-4 | Low: `NOT_RUNNING` ended VR | Closed | skips at `:303`, `:312`, `:361` |
| R1-5 | Low: pacing saved in one slot | Rejected; narrower case residual | `Game1.Benchmark.cs:184-188`; `:199` remains |
| R1-6 | Low: flat game's state untested | Closed | 7 host tests; two fail without the restore |
| R1-7 | Low: DAG named no PR or head | Closed | `review`, PR #7 linked, next names closeout then headset |
| R1-8 | Low: survey said one log line | Closed | survey `:80-85`; `mem-009` has versions |
| R1 notes | SDL handle; 'must not throw' | Kept; doc fixed | `IVrFrameSink.cs:9-13` |
| R2-1 | Low: context check outside the `try` | Closed | `:324` is first inside it |
| R2-2 | Low: skips bypassed the idle sleep | Closed | `SkipFrame` sleeps (`:368-372`) |
| R2-3 | Low: line numbers off by one | Closed | `:199`, `:452`, `:467` correct |
| R3-1 | Low: skips left the runtime context current | Closed | `SkipFrame` restores (`:370`) |
| R3-2 | Low: retry consequence unrecorded | Closed in the record | README round 2 states the fallback |
| R4 | PASS, none | | eight changed lines |

Residuals and containment: `Game1.Benchmark.cs:199` (run-bounded, benchmark-only); the accepted call-order fallback
to flat (recorded, logged); unexercised skip paths (specification, fail-closed); no full game with `--vr` (the
operator's test).

## New findings

### 1. Low: an acquired swapchain image is not released when the image wait fails

`OpenXrQuadPresenter.cs:378-389`. Acquire and wait sit outside the `try/finally`; only the copy is inside. **Scenario:**
a runtime errors in `xrWaitSwapchainImage`; `Check` throws before the `try`, no release is sent, and `TearDown`
destroys the swapchain (`:424`) with an image still acquired, which the swapchain rules expect to be released first.
The session is already falling back, so the impact is a validation message at teardown. **Fix:** put acquire and wait
inside the `try` behind an `acquired` flag. This also corrects the round 1 summary, which says release runs in
`finally`; it runs only after a successful wait.

### 2. Low (record): `GLOSSARY.md` has no VR entry

`AGENTS.md` asks for a `GLOSSARY.md` update when a system, term or pattern is added; the link check passes because it
only validates paths the file names. This change adds a system (`src/Allegiance.ClientCore.Vr`), a seam (`Game/Vr`) and
three terms, so the index is incomplete. **Fix:** add a VR row or section pointing at the survey and both folders.

### 3. Low (record): the survey omits what the flat game keeps after a fallback

`knowledge/vr-client-survey.md:80-85` says 'Either way the game runs flat.' The code restores the fixed step and sleep
but leaves vsync off until restart (`VrFrameSinkHost.cs:12-18`, `:70-85`), so the flat game can run uncapped. The PR
body says this; the note should too. **Fix:** one sentence in the fallback bullet.

### 4. Low (evidence): the 'new tests 3x alone' gate is not reported

`AGENTS.md` lists it beside the build and fast tier. The PR reports the build, 24 tests, mutation checks and the
Windows baseline comparison, not the repetition; CI's fast tier runs once. **Fix:** run the two VR classes three times
alone and report it, or record why not.

## Test evidence

Host tests cover what can change the flat game: attach, submit, fail, throw, settings while attached, dispose, a
refused second sink. Milestone tests cover option parsing, the lifecycle map, geometry and the missing-loader
fallback. The fallback is covered end to end: drop, restore the flat step and sleep, present flat again. Untested: the
skip paths, empty-frame recovery and context restoration; they are unreachable through the concrete `XR` object and end
in flat, so this is a residual, not a blocker. Not reproduced: the author's build, tests and mutation results; CI's
Linux job was `in_progress` (both `guard` checks passed).

## Contract correctness

Five small upstream edits: `EndDraw` (`Game1.GpuBudget.cs:549-550`), `ApplyBenchmarkSettings`
(`Game1.Benchmark.cs:187`), the dispose hook (`Game1.RelativeMouse.cs:48-52`), 7 lines in `Program.cs`, and the
solution/project references. With `--vr` absent, `SubmitVrFrame` returns on a null host before reading
`PresentationParameters` (`Game1.Vr.cs:21`), `KeepVrPacing` is null-conditional (`:17`), and `VrOptions.Take` only
filters its argument list. Every touched file is `i/lf` in the index and the diffs are single additions, not
reformatting. Only the MonoGame head and the tests reference the VR project, so the browser and Android heads gain
nothing. No new service, port or env var beyond three opt-in constants.

## Records

`DAG.json` `ragesaq-002` has `status: review`, PR #7 in `links`, and a `next` naming the rounds, this closeout, the
headset test and then the merge approval: it meets the rules block. The survey matches the code except finding 3. The
README entries match the summaries finding by finding; heads and trees match `git show -s --format=%T`; each round
folder is unchanged since its record commit. `publish-guard` OK (8 commits);
`check_data_boundary.py` OK; `check_memory.py` OK (9 entries, documented warnings only). No machine path, secret or
non-noreply address in the diff; committed blobs are LF with no BOM.

## What I verified, and how

**By execution (read-only):** HEAD, tree, status, log, per-file diffs, `%T` for every recorded head, `ls-files --eol`,
per-round diffs, BOM/CR checks, an address and path scan of the diff, `publish-guard` OK (8 commits), both checkers
OK, and the PR's description and check runs.

**By reading only:** the presenter's frame and session state machine, the GL copy and state restoration, the host's
pacing handover and drop path, the tests and the records.

**Not run:** `dotnet`, build, tests, the game, a headset; `check_map.py` and `recall.py` (excluded). **Limits:** the
author's numbers and mutation results are not reproduced; CI is still running; the skip paths and the in-headset look
are unverified; the summaries' byte-identity with the reviewer's originals cannot be checked here.

## Judge

**Ready for the in-headset test.** The opt-in path is isolated, every failure ends in a logged flat run, and the flat
state is restored at the seam. Blocking the merge decision after the headset test passes: the Linux `build-and-test`
job green on `f36ee317`; the Low record items fixed or waived; and `ragesaq`'s approval at the reviewed head. A
prose-only fix moves the head and should be re-recorded; any code change needs a delta re-review.
