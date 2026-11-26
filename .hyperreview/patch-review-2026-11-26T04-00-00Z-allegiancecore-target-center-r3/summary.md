# Patch review: a gunner's carrier is never picked (PR #16, round 3)

**Reviewed head:** `216556139a03c9308dd3fb6e7f93d5dda2459347`, tree `68ee129dd54b123679c092c31a4f70cba9190940`; delta `23843266..21655613` (two commits); base `3a3b6e1b`. The clone is detached, with no remote, branches or tags; head and `git write-tree` matched on entry, and `git status --short` was empty. Nothing was checked out, switched or written outside the artifact directory.
**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high` (reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`); explicit selection, the route declared in the target's "Cross-family review route". Authoring family Anthropic/Claude (Port): cross-family.

**Verdict:** PASS. **nextAction:** two Low findings, a stale pool comment and two cases the new test does not pin; fix or answer them before the merge decision. No medium or high issue remains.

## Tier
Full: PR/branch target with a durable review record, which this repository requires.

## Round 2 disposition
- **N1 (medium): fixed.** `PickFrontTarget` takes `excludeShipId` and skips `Kind == Ship` with that id (`Game1.Targeting.cs:1523`); `SelectCentermostTarget` passes `ownShip.ParentId` (`:1494`), the same `short` id type with `-1` as the sentinel, which is also the value outside a turret, so nothing is excluded there. The carrier is gone from the pick pool, which removes the round 2 failure (the carrier scored about `+1` from the dead-reckoned origin against its own raw `Latest` position).
- **N1 pose fallback: fixed.** With no `_poses[parent]` entry the origin is now the carrier's last update, captured under `_session.Gate` (`:1476-1477`). `ServerShipUpdate.Orientation` allocates a fresh `Orientation` per access and `Position` is a struct, and every `Latest` assignment replaces the object, so the captured pair is a consistent immutable snapshot and the later `GetForward()` outside the lock cannot read torn state (`ServerShipUpdate.cs:56-62`). `_poses` itself is still read outside the gate, as before: it is written only on the game thread.
- **N2 (low): fixed.** The artwork-gated live test now ranks with `PickFrontTarget` over the real gathered candidates from the own ship's `Latest` pose (`MiscKeyBindingLiveTests.cs:136-155`), and its doc comment names that picker.
- **N3 (low): fixed.** `.hyperreview/README.md:36-37` now says the parity citation was fixed to `:199` in `2384326`.
- **N4 (low): fixed.** `MiscKeyBindingTests.cs:16-19,29,34-37,63-65,81-83` retitle the tests as the autopilot implicit-pick list and say TargetCenter no longer uses it.

## Round 2 record
- The committed round 2 `summary.md` is byte-identical to the reviewer's file as the receipt records it: sha256 `7204aa7db8675ffc80106d3a5cb2b06139ddc3bb1e2498d5bc3cfff43abfe65d`, first bytes `# P` so no BOM.
- Receipt values check out: reviewed head `23843266`, tree `1cfcf6e7` (confirmed by `rev-parse 23843266^{tree}`), range `ece6de19..23843266` (the range's first commit is the round 1 head), and the verdict line matches the summary. `21655613` replaced the copied round 1 values (head `ece6de19`, tree `21598bb`, summary `2f2843`, times `01:03/01:15`), and the round 1 receipt still carries exactly those, so the fix corrected a verbatim copy.
- The README round 2 entry states the same head, tree, reviewer, matrix and verdict as the receipt, and its dispositions match the summary's findings.

## Findings

### L1 (low): the pool's doc comment still names only the old chooser
`src/Allegiance.ClientCore.Game/Game1.Targeting.cs:154-155` says "how a candidate is CHOSEN from within that pool (`SelectCandidates`) is untouched". Since this PR the `'` command chooses with `PickFrontTarget`, and that picker now drops a ship. Failure: a reader auditing the turret path concludes the carrier is filtered when the pool is built, or that `SelectCandidates` is still the consumer; the next edit then moves or removes the exclusion and the round 2 defect returns. Fix: name `PickFrontTarget` and say the carrier is skipped by the picker (or pass the exclude id into `GatherSectorCandidates` so every consumer is immunized). The same wording appears at `:1455` ("left out of the candidates") even though the carrier stays in `objs`.

### L2 (low): the new test does not pin the kind check or the keep-rule interaction
`tests/Allegiance.ClientCore.MonoGame.Tests/TargetCenterFrontTests.cs:75-86`. Two behaviours the fix relies on are unasserted: (a) the `Kind == Ship` guard, since the station's id is `6` and not `5`; (b) that an excluded ship which is also the *current* target is not retained, because the exclusion sits before the `currentSeen` test (`Game1.Targeting.cs:1523-1527`). Failure: a later edit to `o.Id == excludeShipId`, or a move of the check after the current-target test, keeps this test green while a station sharing the carrier's numeric id is silently dropped or the carrier is kept. `AutopilotTarget` identity is the `(Kind, Id)` pair, so cross-kind id collisions are expected. Fix: add a station with id `5`, and a case with `current = (Ship, 5)`, one candidate, `excludeShipId: 5`, expecting `null`.

## Observations
- The red/green claim holds arithmetically: without the exclusion the ship at `z=-10` scores `1.0` against the station's `0.862`, so both `Assert.Equal(Station, 6)` and `Assert.Null` would fail; with it, both pass. I did not run dotnet.
- The keep rule is consistent with the exclusion: a repeat press with the carrier as the current target steps to the next object, and with nothing else returns `null`, leaving the previous target untouched, which matches native never returning `pship` and never clearing on a miss. The carrier remaining as a stale target in that corner is unreachable through `FindTarget`, so it is not a divergence.
- The fallback pose is the raw last update, not the dead-reckoned render pose. It applies only when the carrier is not drawable (first frame, or a frame it fails `CanDrawInSector`), so it is a stale-but-correct origin rather than a ranking change. `knowledge/target-center.md:31-35` now states this correctly.
- The live test's final `Assert.Contains(objs, ...)` is implied by `PickFrontTarget` returning a member of its input, so it is a smoke assertion; its value is the non-null and drone-presence checks.
- `DAG.json`'s `ragesaq-013` still carries `updated: 2026-11-25`, and `status`/`next` still describe a branch awaiting review and merge, which remains accurate; the two completed rounds are recorded in `.hyperreview/README.md` and the note's timeline.
- Upstream shape is narrow: one production file, three test files and records; the new parameter is optional, so the live test and the existing unit tests call it unchanged.

## Verification
Executed read-only in `<review-clone>`: `rev-parse`, `write-tree` (matched), `status` (clean), `log`, `show`, `diff`, `diff --check` (clean); `git ls-files --eol` (all eight changed blobs `i/lf`; `.cs` shows `w/crlf` from checkout conversion only); `git cat-file` plus `sha256sum` for both committed summaries; `scripts/publish-guard.sh 3a3b6e1b HEAD` (OK, 5 commits); `tools/check_data_boundary.py` (OK); `tools/check_memory.py` (OK, with the expected `people/` warnings and the missing-baseline limit); a scan of every added line for machine paths, emails and secret words (none).
Read, not executed: the delta, `PickFrontTarget`, `SelectCentermostTarget`, `GatherSectorCandidates` and its callers, `TryOwnPose`, `RenderShipId`, `ServerShipUpdate`, `PredictedPose`, the turret pose gather, the tests and the records.

## Verification limits
Native sources are absent from the clone, so "native `FindTarget` never returns the ship passed to it" is the author's reading of `WinTrek.cpp:10131-10147`, not re-verified here; the fix is harmless even if that premise is wrong, since the carrier is the gunner's own ship. The receipt's route, runtime version and start/finish times come from host logs that are not in the clone and could not be checked. The turret branch still has no in-game test: the author's turret check is by reading, and the headset run is the downstream acceptance step. No build, test, game or headset run was possible.