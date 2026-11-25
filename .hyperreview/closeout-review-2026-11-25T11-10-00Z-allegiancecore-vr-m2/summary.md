# Closeout review: PR #9, VR milestone 2

- Target: `ragesaq/AllegianceCore-public` PR #9, branch `port/vr-m2`, stacked on `port/vr-m1` (PR #7) at `f945af76`
  over `master` `47e720be`.
- Reviewed head `36989f07e6c6e3c788691781b527947462b8d9d4`, tree `81e8dd23a652bcfdf332eb498dce8aa3bdbee61d` (matched
  `git write-tree`). Range `f945af76..36989f07`, four commits.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, `high` (matrix 4.14.0, sha256
  `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; explicit selection, the route declared in the
  target's `AGENTS.md`). Author family Anthropic/Claude (Claude Code, Opus 5.5, seat Port). Cross-family.
- Clone: standalone detached clone, no remote or branches, `<review-clone>`.
- **Verdict: PASS** for the handoff to the in-headset test. Not yet merge-ready: PR #7 first, the Linux
  build-and-test on the final head, and ragesaq's approval (below).
- nextAction: hand to the operator for the headset test; then merge gates.

## Answer

The change is coherent and the ledger is closed. The flat path reduces to the pre-change expression at every
guard, the upstream seam keeps its bytes, and the records hold. What is untested is what the headset measures: row orientation, convergence and scale, HUD edges and placement. That is the acceptance step, not an open finding.

## Finding ledger

| Prior item | Status | Evidence | Owner |
|---|---|---|---|
| R1 F1 (Medium) HUD blend flag | Rejected on specification; accepted in round 2 | R2 re-derived from OpenXR `rendering.adoc`: the flag enables layer alpha; colour is premultiplied unless `UNPREMULTIPLIED` is set. No code change; survey "Traps"; HUD edges stay on the checklist | ragesaq (headset) |
| R1 F2 (Low) debris stepped twice | Closed | `917f29ba`: `GatherDust(..., advance)` (`Rendering/NativeScene.cs:29-56`), `advance: !VrRepeatPass` at `Game1.Rendering.cs:679`; `VrScenePassTests` fails without it (author-run mutation) | - |
| R1 F3 (Low) eye targets outlive sink | Closed | `917f29ba`: `BeginVrFrame` releases them when the host is not attached; `DisposeVrSink` also releases. Read, not executed | - |
| R1 F4 (Low) aleph glimpse on HUD layer | Closed | `917f29ba`: `!_vrEyesDrawn` gate at `Game1.Rendering.cs:1772`; survey records the stereo skip | Port (effect absent later) |
| R1 observation: world-locked HUD, lean drift | Deferred for milestone 2 | Quad pose in LOCAL space (`OpenXrPresenter.cs:417-424`); survey; headset checklist | ragesaq (headset) |
| R1 limit: side-by-side swapchain width | Closed | `917f29ba`: per-eye `EnsureSwapchain` at the recommended size; second 90 s run used 3708x3564 per eye | - |
| R2 L1 (Low) stale `ReadEyeSize` comment | Closed | `36989f07`, comment only (`OpenXrPresenter.cs:244-245`) | - |
| R2 note: call-site wiring untested | Accepted residual | `VrScenePassTests` calls the helper directly; a call-site regression would not fail a test | Port |
| R2 note: swapchain handle leak on enumeration failure | Accepted residual | Milestone-1 code, never observed; one handle, then teardown | Port if seen |
| Post-processing off in eye passes | Deferred | Survey; upscaled/HDR/bloom/antialias/soft gated off per eye; waits on headset frame time | Port |
| HUD over black in the window in stereo | Named limitation | Transparent clear in `EndVrScenePasses`; survey | documented |
| HUD edges (F1 dispute) | Deferred to headset | PR #9 checklist | ragesaq |

No lifecycle-promotion or decision-delta candidates were carried by either round. A draw-pass smoke test is proposed for a later milestone.

## New findings

No High/Medium finding.

**N1 (Low, deferred to Port; not a headset blocker): eye-target creation has no catch.** `Game1.Vr.cs:101-112`
constructs both eye targets inside `Draw`. At 3708x3564 the colour plus depth targets are about 210 MB; if
`new RenderTarget2D` throws, the exception leaves `Draw` and the process exits instead of running flat, breaking the
"flat on any VR failure" promise. Non-throwing failures are contained later by `GlBlitter`'s framebuffer-completeness
check. Fix: catch around creation/binding in `BeginVrScenePass`, call `Abandon(reason)`, draw flat. Unobserved; both
full-size runs succeeded.

## Verified by execution (read-only)

- `git write-tree` matched; `git status --short` empty except this artifact directory.
- Normal and `--ignore-all-space` numstat agree on the three upstream code files (`Game1.Rendering.cs` 35/24,
  `NativeScene.cs` 5/3, `Program.cs` 1/1): no indentation-only change, so the loop body keeps its bytes.
- `git ls-files --eol`: `i/lf` on every upstream index entry; no renormalization committed.
- `git ls-tree 47e720be`: no VR code at master. Browser references neither the game library nor the VR project;
  Android references the game library but not VR; the new MonoGame package is only on `ClientCore.Vr.csproj`.
- Both committed reviewer summaries hash to their receipt values (`c2d5bd62...`, `c48830e1...`).
- The four VR test files hold 46 cases statically (9+2+12+23), matching the author's count.
- `publish-guard.sh f945af76 HEAD` OK (4 commits); `check_data_boundary.py` OK; `check_memory.py` OK with the nine
  expected warnings and its reported deletion-check limit. Added lines contain no machine path or email address.
- `ci.yml` triggers `pull_request` only for `master`, confirming the Linux build-and-test did not run for this PR.

## Verified by reading

- Flat path: no host means one pass, both pass flags false, `BeginVrScenePass`/`EndVrScenePasses` return at once,
  `VrSceneDestination` returns its argument, and every guard reduces to the original expression. The body is wrapped,
  not rewritten.
- Stereo: per-eye `PreserveContents` targets; `dt` is 0 on the repeat pass; emission, trail/emitter updates, effect
  lights, flare begin, preview, telemetry and timers are skipped; dust re-projects without stepping. Radar, HUD and
  2-D screens draw after the camera is restored, over the transparent buffer.
- Presenter: a leftover frame is ended before a new one; `ShouldRender` false ends with no layers; a
  `VrUnavailableException` closes the open frame before teardown. The projection layer carries the same poses and FOVs
  given to the game; the HUD quad is layered second, alpha-blended, sized from the game camera's vertical FOV; each
  swapchain image is released in `finally`.
- Contract: three upstream files, a few dozen lines; the new `GatherDust` parameter is optional, so the Browser call
  site is unchanged; `Program.cs` changes one type name; the eye-view records use only MonoGame math and live in the
  shared library.
- Records: `DAG.json` node `ragesaq-007` is `review`, links the PR and survey, and names the headset test then merge
  approval after PR #7, as the rules require. `GLOSSARY.md` section 16, the survey and `.hyperreview/README.md` agree
  with the code on the eye passes, per-eye swapchains, blend bit, aleph skip, release and `PreserveContents`.

## Limits and residual risk

- Not re-run: build, the 46 tests, the byte-identical replay captures and the two 90 s SteamVR runs are the author's
  measurements; behaviour was read and counted, not executed.
- No headset: the row flip, convergence and scale, HUD edges and the world-locked HUD's drift are the checklist.
- Only SteamVR/OpenXR 2.18.4 was exercised; session-not-running paths and other runtimes are unexercised.
- LOCAL space supplies the eye offset with no in-game recenter, so a runtime recenter moves the eye point and HUD
  anchor; watch it in the headset.
- `check_memory.py` cannot run its deletion check in a clone without a remote; it reports that limit.

## Merge ordering and gates (after the headset test)

1. Land PR #7 first; PR #9 then targets `master`.
2. Run and pass the Linux build-and-test on the final head; it does not run for a PR based on `port/vr-m1`.
3. If a base update moves the head off `36989f07`, review the delta and merge only the head ragesaq approved.
4. Return any failed headset check to Port before the merge decision; the publish guard already passes.