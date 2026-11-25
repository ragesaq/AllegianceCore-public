# Patch review: VR milestone 1 (PR #7), round 1

**Reviewed head** `30c2ae6a304e878c159cfbefdf779669ae3ed34c`, **tree** `37638b87f60bbf5590695b982747f2a1e093a7c7`
(`git write-tree`), delta `47e720be..30c2ae6a`, one commit, from a standalone detached clone at the head of
`port/vr-m1`. `git status --short` empty.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`, explicit selection (reviewer-parity
matrix 4.14.0, sha256 `74618f22...`). **Author family:** Anthropic (Claude Code, Port). Cross-family.

**Verdict: PASS-WITH-CONDITIONS.** Two cheap robustness fixes before the headset test (findings 1 and 2); the rest may
be deferred to milestone 2 if the DAG node names them.

## What holds

- The session lifecycle mapping is the canonical one, not a plausible variant: `Ready` -> `xrBeginSession`,
  `Stopping` -> `xrEndSession`, `Exiting`/`LossPending` -> tear down, `Idle` -> nothing
  (`VrSessionLifecycle.cs:17-23`). `xrBeginSession` in `Idle` would return `XR_ERROR_SESSION_NOT_READY`; beginning on
  `Ready` is what the specification asks for. `Stopping` then `Ready` again recovers, and the swapchain is kept across
  the pause, which is allowed.
- Frame-loop balance is right on every normal path, including `ShouldRender` false: `xrBeginFrame`/`xrEndFrame` are
  always paired and the empty frame is submitted with `LayerCount 0` (`OpenXrQuadPresenter.cs:304-334`). Swapchain
  acquire/wait/release are paired, with release in `finally` (`:338-354`). `EventDataBuffer` is rebuilt inside the poll
  loop, so `Type` is reset each iteration (`:212`). Structure `Type` fields and the single `Next` chain
  (`SessionCreateInfo.Next = &binding`) are correct.
- Teardown is children-first and in the right order: blitter (private FBO) before swapchain, then space, session,
  instance (`:381-398`). I found no leak, double free or use-after-destroy on the paths I traced, including an
  `Initialize` that throws after the session exists.
- Nothing changes when VR is not requested. The hooks into upstream files early-return on a null sink
  (`Game1.Vr.cs:38,46,63,73`), and `SubmitVrFrame` tests the sink before touching `PresentationParameters`
  (`Game1.Vr.cs:46-47`), so the flat path gains one predictable branch per frame and no property read. The edits are
  four one-line hooks plus 7 lines in `Program.cs`, and the new project is referenced only by the desktop head.
- Pacing order is correct: `ALLEGIANCE_NOVSYNC` and `ALLEGIANCE_FRAME_CAP` are applied in the `Game1` constructor
  (`Game1.cs:1682-1698`), before `Program.cs` attaches the sink, so the attach wins and saves the right flat value;
  `ApplyBenchmarkSettings` calls `KeepVrPacing()` before its own `ApplyChanges()` (`Game1.Benchmark.cs:187-188`).
- The index is clean: every new file is `i/lf` with `w/crlf` in the checkout, exactly like existing upstream source
  (`git ls-files --eol`). The new tests are hermetic: the internal constructor receives one non-existent path, so no
  host runtime can be loaded (`VrMilestone1Tests.cs:84-100`).
- The copy's central assumption, that framebuffer 0 holds the finished frame at `EndDraw`, is the same one the existing
  replay-frame capture already relies on one statement away (`Game1.GpuBudget.cs:520-521`), and capture mode is in
  use. That is the strongest available evidence short of running the game.

## Findings, most severe first

### 1. Low-Medium: the copy discards GL errors the game was watching for

`GlBlitter.cs:89-91` drains up to 16 pending `glGetError` results before the blit so its own check is attributable.
That also clears errors raised by the frame's own drawing. Concretely, with `--vr` attached, an error the game
produced is swallowed at `EndDraw`, so the deliberate guard at
`src/Allegiance.ClientCore.Game/Rendering/HdCompressedUpload.cs:67` ("GL error before BC upload") stops firing for the
rest of the session. VR off is unaffected, so this is a diagnostic loss in one mode rather than a behaviour break, but
it removes an existing in-repo guard silently. Fix: keep the drained codes and log them (`[vr] GL errors before copy:
0x...`) instead of dropping them.

### 2. Low: the GL context is re-checked after `xrEndFrame`, not after swapchain creation

`KeepContextCurrent()` runs only at `OpenXrQuadPresenter.cs:335`. `EnsureSwapchain` (`:242-274`) calls
`xrCreateSwapchain` and `xrEnumerateSwapchainImages` and then goes straight to the blit. A runtime that leaves its own
context current after creating a swapchain makes `CopyBackBuffer` bind the private FBO name in the wrong namespace:
`glCheckFramebufferStatus` returns 0, the code raises "swapchain framebuffer incomplete (0x0000)" and VR is permanently
flat on the first frame. It fails closed, so nothing is corrupted, and SteamVR 2.18.4, which the author's harness used,
restores the context (the resize case passed), which is why this is Low. Fix: one `KeepContextCurrent()` after
`xrEnumerateSwapchainImages`, or immediately before the blit.

### 3. Low: a begun frame is never ended when the copy fails

After a successful `xrBeginFrame` (`:305`), a throw from `CopyFrameIntoSwapchain` (`:338-354`) skips `xrEndFrame`
(`:334`); `SubmitFrame`'s handler (`:80`) then tears the session down with that frame open. OpenXR requires
`xrEndFrame` after every successful `xrBeginFrame`, and `xrDestroySession` is legal at any time, so this is not a
violation, but the frame is abandoned and one transient GL error becomes a permanent fallback. Fix: on a copy failure,
submit an empty frame (`LayerCount 0`) before failing.

### 4. Low: a recoverable pause can become a permanent fallback

`xrWaitFrame`/`xrBeginFrame` returning `XR_ERROR_SESSION_NOT_RUNNING` (the runtime moves to `Stopping` between
`PollEvents` and the wait) goes through `Check` into `Fail` (`:298,305`), while the event path treats the same
transition as recoverable. A one-frame race therefore costs the whole session instead of one frame. Fix: treat that one
result as "no frame this iteration" (return without failing).

### 5. Low: pacing restore depends on one slot written from several places

`_vrSavedFixedTimeStep` is written by the attach setter (`Game1.Vr.cs:25`) and by `KeepVrPacing` (`:39`), while
`Game1.Benchmark.cs:199,420,428` set `IsFixedTimeStep` directly without saving. With saved frame-cap settings plus an
autotune run whose suggestion has `FrameCap 0`, `KeepVrPacing` stores false; a later VR drop then restores
`IsFixedTimeStep=false` and the flat game loses its cap (`TargetElapsedTime` is not touched). Benchmark-only and it
needs a VR failure after an autotune, hence Low; the `ALLEGIANCE_FRAME_CAP` start-up path itself is correct.

### 6. Low: the tests do not cover the part that affects the flat game

`VrMilestone1Tests.cs` covers option parsing, the state mapping, geometry and the missing-loader path, all of which are
pure. Nothing covers `DropVrSink`'s restore, `SubmitVrFrame`'s catch or the idle sleep, which are the only VR code that
can leave the flat game in a different state. Suggested fix, matching the repository's existing pattern
(`BenchmarkHdPbr`, `Game1.Benchmark.cs:164`): move the save/restore arithmetic into a static helper and test it.

### 7. Low (record): the DAG node does not pin what the next reader needs

`DAG.json:63` names the review and the headset test but not the pull request or the head under review, and the status
stays `active` (`:59`) although the branch is in review. The same file's rules 2 and 6 ask for the authoritative thread
link, exact state and the status update. Fix: add PR #7 and the head, and set `review`.

### 8. Low (record): one note claim is broader than the code

`knowledge/vr-client-survey.md:81` says any failure logs `[vr] running flat:`. Only the four filtered exception types
do (`OpenXrQuadPresenter.cs:80`); anything else is logged by the game as `[vr] frame sink threw, running flat:`
(`Game1.Vr.cs:54`). The rest of the section is accurate, and the "(observed)"/"(derived)" labels are used honestly,
including the explicit "the full game was not launched". Separately, `memory/mem-009`'s `seen-in` names the action and
the host by role but not the environment versions `docs/MEMORY_PROTOCOL.md` asks a `witnessed` entry to carry.

### Notes, no action

- `GlBlitter.cs:66-76` frees the module handle it just resolved the entry point from. Safe today because MonoGame keeps
  SDL2 loaded, so the refcount stays above zero; it would dangle if this were the only load.
- `IVrFrameSink.cs:12` says `SubmitFrame` "Must not throw", while the filter at `OpenXrQuadPresenter.cs:80` lets other
  exception types out. `Game1`'s backstop makes that safe, so the contract text and the implementation merely disagree.

## What I verified, and how

**By execution (read-only) on the frozen target:** `git rev-parse HEAD` and `git write-tree` (head and tree above),
`git status --short` (empty), `git log`/`git show --stat`, `git ls-files --eol`, `git grep` for `fcntl`,
`IsFixedTimeStep`, `SynchronizeWithVerticalRetrace`, `_scaledThisFrame` and `glGetError`;
`bash scripts/publish-guard.sh 47e720be HEAD` -> `[publish-guard] OK (1 commit(s))`;
`check_data_boundary.py` -> OK; `check_memory.py` -> OK, 9 entries consistent and indexed, with only the documented
`recorded-by` warnings.

**By reading only:** all OpenXR semantics above (against the 1.0 lifecycle section and the canonical READY/STOPPING
mapping), all GL reasoning, the MonoGame integration, the tests and the records. I did not run `check_map.py` (the
instruction rules it out in a clone without a remote), did not run `recall.py`, and did not run the durability
recorders.

**Not run:** `dotnet build`, the test tiers, the game, and a headset. The author's "0 warnings", "17 tests pass" and
"the fallback test goes red when the catch is removed" were therefore not reproduced. I did check the red-test claim
structurally: with the catch removed the exception escapes `SubmitFrame` before the log line, so the test does fail.

**Limits.** The author's harness measured SteamVR/OpenXR 2.18.4 with a bare window, not the game; the full-game frame
contents at `EndDraw`, the scaled/FSR path and the headset's actual look are unverified by both of us. Runtime-specific
behaviour (context restoration, blend-mode support) is argued from one runtime's observation plus the reasoning above,
not from a second runtime.