# Patch review: TK_TargetCenter picks by angle off the nose (PR #16, round 1)

**Reviewed head:** `ece6de19562f39c5fbe75d88bb4380911bbcdcd8`, tree `21598bba6aa2e2d523325bbc19c1cd08dcb93ab0`;
base `3a3b6e1b`, tree `c182891cd16abfb74294c528980ab16e1041aada`. Never checked out, switched or written.
**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go at effort `high` (reviewer-parity matrix
4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`, standing at high); explicit
selection, the route declared in the target's "Cross-family review route". Authoring family Anthropic/Claude (Claude
Code, Claude Opus 5.5, seat Port): cross-family.
**Verdict:** PASS-WITH-CONDITIONS. **nextAction:** fix F1 and F3, answer or record F2, then re-review the delta.

## Findings

### F1 (medium): the tier list's remaining consumer is not the T key
`tests/Allegiance.ClientCore.MonoGame.Tests/MiscKeyBindingTests.cs:17` now states "The T key cycles this list."
That is false in this tree. The T key (`Target`, `InputActionCatalog.cs:192`) is dispatched by the remainder-target loop
(`Game1.TargetCommands.cs:165-166`) into `SelectRemainderTarget("Target", Next)`, which chooses through `ChooseTarget`
(`Game1.TargetCommands.cs:21-31`) over `GatherSectorCandidates`: distance for *Nearest*, list order for *Next/Prev*.
(The touch Autopilot button shares that path, `Game1.FlightInput.cs:109`.) `SelectCandidates` is reached only by
`CollectTargetCandidates` (`Game1.Targeting.cs:110`), whose remaining production callers are `PickNearestTarget`'s
two autopilot implicit-pick sites (`Game1.FlightInput.cs:131,138`); `CycleTarget` (`Game1.Targeting.cs:330`) has no
callers at all. The same misattribution is in the durable record, `knowledge/target-center.md:26` ("The port used to
reuse the T key's candidate list..."). Because the class comment was rewritten by this PR, this is a claim the change
introduces, and this repository treats record accuracy as a gate.
Concrete failure: a later reader "fixes" the T key by editing `SelectCandidates`, changing nothing the player sees, or
concludes the T key was the old target-center path. Fix: say the autopilot implicit pick (G key / touch Autopilot) is
the remaining consumer, and retitle the section header (`:28`) and the three method docs (`:33-37`, `:63-65`,
`:81-84`), which still say "TargetCenter" for tests that only exercise `SelectCandidates`.

### F2 (medium): in a turret seat the axis is a frozen pilot pose
`SelectCentermostTarget` takes its origin and axis from `TryOwnPose` (`Game1.Targeting.cs:1450`). For a boarded
gunner, `TryOwnPose` returns `_ownSim` whenever prediction is on and a hull was resolved (`Game1.Camera.cs:55-59`),
but the own-ship sim is neither stepped nor reseeded while boarded (`Game1.FlightInput.cs:2448-2451` steps only when
`sendArm`, i.e. not a gunner; `:2657-2663` skips `UpdateOwnShipSimulation` for `ParentId >= 0`). So the pose freezes at
the carrier's last piloted pose from the moment of boarding.
Concrete failure: board a turret, swing it about 90 degrees off that frozen heading, press `'` or the HOTAS
target-center button. The command ranks candidates against the old pilot nose, so it can pick a different object than
native, whose TK_TargetCenter uses the turret orientation while the player is in a turret (the review brief states it). Nothing
in this diff causes this: the old frustum pick read the same `TryOwnPose`. But the new rule's claim "as native" does
not hold for a gunner seat, and the operator's reported use is a HOTAS in VR.
Suggested fix: when `ownShip.ParentId >= 0`, use the carrier's current pose for the origin and
`_turretOrientation.GetForward()` for the axis, or record the divergence (note plus DAG `next`) with the native
evidence and a headset check. Do not silently leave the claim as is.

### F3 (low): stale player-facing wording
`InputActionCatalog.cs:199` still labels the F6 Controls entry "Target center (nearest to reticle)"; after this change
it is nearest the nose, not the reticle. `Game1.FlightInput.cs:2163` repeats the same wording in a comment, and the
no-pick log `Game1.Targeting.cs:1469` says "nothing in view" although no view is involved. Fix the label first (it is
user-visible); the other two are one-line corrections.

## Observations (not merge conditions)

- Scoring matches the described native rule, by construction. `PickFrontTarget` maximises
  `sign(dot)*dot^2/|d|^2` over `d = object - position`, which is exactly `-Orientation.CosForward2(d)`:
  `GetForward` is `-m_r[2]`, `CosX2` carries the sign (`Orientation.cs:303-308`), and `CosForward2` negates the
  backward row (`Orientation.cs:317-319`). Native keeps the lowest score over `pos - model`; both the sign and the
  ordering invert together, so the picked object is identical. Ties: first candidate wins (`>`), which matches a
  lowest-wins scan; zero length scores 0 as `CosX2` does. The current target is skipped and returned only when nothing
  else qualifies (fresh `AutopilotTarget`, `UseDefault` true), so a repeat press steps to the next object and
  `CommitTargetForPlayer` sees an unchanged kind/id for the keep case. I found no arithmetic or ordering defect.
- Candidate pool: `GatherSectorCandidates` excludes only `id == me`, `IsNotVisible`, missing `Latest`, and other
  sectors; probes and treasures are the only kinds this change newly offers. Both map to real order targets
  (`Game1.Orders.cs:143`), so no unsupported-target path. Own-side probes, child/turret ships of other players, and
  visible dead ships are not filtered, but the ship pool is unchanged by this PR; native `FindableModel` parity for
  those cannot be checked in this clone.
- Sector and identity: the pool uses `ownShip.SectorId`, while the pointer pick uses `RenderSector(own.SectorId)`.
  They are identical in normal flight; they differ only for benchmark views, local spectating, and command-camera
  while docked. Native's `FindTarget` is ship-centred, so the raw sector is defensible. In local spectating
  `World.Reset` clears `MyShipId` (`ClientWorld.cs:2308`), so the key still no-ops exactly as it did before.
- Callers: only `'` (`Game1.FlightInput.cs:2177-2179`) and the virtual-joystick right mouse
  (`Game1.MousePick.cs:103-105`) call the method in this tree. No other caller depended on the frustum or the ship
  tier. The HOTAS button path is not in this clone, so its routing is unverified here.
- Tests: hand-evaluating all six confirms each passes for the intended reason (0.999997 vs 0.64; 0.9994 vs 0.8; +1e-4
  vs -1; 1 vs 0.990; keep-with-`UseDefault`; empty). A strict ship-first rule flips `StationOnTheNose...` and
  `ObjectBehind...`, as the author says. Untested: zero-length vector, exact tie/first-wins, kind-scoped current
  matching, and the `SelectCentermostTarget` glue (read-only, acknowledged). Consider one test pinning equivalence to
  `CosForward2`, or call `CosForward2` so the formula cannot drift.
- Records: the note's current-state bullets match the code; the DAG node `ragesaq-013` has a valid status, a concrete
  `next`, and a followable link. The edited parity row leaves the pre-existing stale `InputActionCatalog.cs:161`
  citation (the row is `:199`); the `Game1.Targeting.cs:1448` citation is correct.
- Hygiene: `git diff --check` clean; all seven changed blobs are LF in the index (worktree CRLF on `.cs` is checkout
  conversion only); no absolute paths or non-noreply addresses appear in the diff.

## Verification
Executed (read-only, `<review-clone>`): target identity via `rev-parse`/`write-tree` (clean, matches), `git status`
(clean), `git diff --check`, `git ls-files --eol`, `scripts/publish-guard.sh 3a3b6e1b HEAD` (OK, 1 commit),
`tools/check_data_boundary.py` (OK), `tools/check_memory.py` (OK; only the expected `people/` warnings plus the
documented "no git baseline reachable" limit). `check_map.py` and `recall.py` were not run, per the task.
Read, not executed: the full diff, the scoring math against `Orientation.cs`, every caller and dependent path, the
tests, and the records. No dotnet build, test, game or headset run was possible.

## Limits
Native sources are absent, so parity is judged only against the author's reading. The turret finding needs native
confirmation and a headset check. The HOTAS binding is outside this clone. Line numbers are at the reviewed head.