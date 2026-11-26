# Patch review: TK_TargetCenter turret axis and record fixes (PR #16, round 2)

**Reviewed head:** `238432666d45818266d1b4175d26113e4ac95a1f`, tree `1cfcf6e7e8f9cd8d6a762373e5c561276447d2fb`; delta `ece6de19..23843266`; base `3a3b6e1b`. The clone is detached; head and `git write-tree` matched on entry and exit. Nothing was checked out, switched or written outside the artifact directory.
**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high` (reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`); explicit selection, the route declared in the target's "Cross-family review route". Authoring family Anthropic/Claude (Port): cross-family.

**Verdict:** PASS-WITH-CONDITIONS. **nextAction:** exclude the parent ship from the candidate pool (N1), fix the three low record/test items, then re-review the delta.

## Tier
Full: PR/branch target and a durable record, which this repository requires.

## Round 1 disposition
- F1 (record named the T key): core claim fixed. `MiscKeyBindingTests.cs:16-19` and `knowledge/target-center.md:26-29` now name the autopilot implicit pick, `PickNearestTarget` and the G key, which matches `PickNearestTarget`'s own doc ("G-key implicit-pick fallback"). Residual claims remain in N2 and N4.
- F2 (frozen turret axis): the origin and axis now come from `_poses[parent]` and `_turretOrientation` (`Game1.Targeting.cs:1473-1476`). The premise is verified by reading: the own-ship simulation is not stepped while boarded (`Game1.FlightInput.cs:2448-2451`, `:2657-2662`), so `TryOwnPose` returns the unstepped `_ownSim` with prediction on (the default); `_poses[parent]` is the carrier's render pose (`:2889-2893`). The new pose source is right, but the pool now contains the carrier (N1).
- F3 (wording): fixed in the catalog label (`InputActionCatalog.cs:199`), the input comment and the log line. Residual claims remain in N2 and N4.

## Findings

### N1 (medium): in a turret the carrier itself can be the pick
`src/Allegiance.ClientCore.Game/Game1.Targeting.cs:1473-1476`:
```
if (parent != -1 && _poses.TryGetValue(parent, out var carrier))
{
    pos = carrier.Position;
```
`GatherSectorCandidates` excludes only `id == me` (`:175`), and `me` is the rider, so the parent ship stays in `objs` with its raw `Latest` position (`:183`). The origin is the parent's render pose. The two differ by the render lead, and `PickFrontTarget` scores direction only (`:1513`).

Failure: gunner in an aft-facing turret. `_poses[parent]` is the dead-reckoned pose, ahead of `Latest` along the carrier's course (dead reckoning is the default; `LaunchOptions.RemoteShipDeadReckoning = true`). `Latest - origin` then points aft, along the turret aim, so the carrier scores about `+1` and can tie or beat the object being aimed at. If the reckoner has not moved the pose, or `remoteTimeline` (M) renders the carrier late, the offset is zero or forward instead; a zero offset scores `0` (`:1513`), which still outranks every object behind the turret. Either way the first press can select the carrier you are riding, where native's `FindTarget` skips `pship`. A second press steps past it because the current target is skipped, so the command still works, but not as native, and the order/HUD briefly targets your own carrier.
Fix: when `parent != -1`, drop `TargetKind.Ship` with `Id == parent` from `objs` (or give `GatherSectorCandidates` an exclude id), and pin it with a test that the parent is never returned.

### N2 (low): the LIVE TargetCenter proof no longer tests TargetCenter
`tests/Allegiance.ClientCore.MonoGame.Tests/MiscKeyBindingLiveTests.cs:33-37` still describes TargetCenter as "the production `GatherSectorCandidates` + `SelectCandidates` nearest-to-reticle ranking", and `:86` and `:146-151` repeat it. Since this PR, `SelectCentermostTarget` calls `PickFrontTarget`; the live test still calls `SelectCandidates` directly, so it proves the autopilot list, not the shipped command, and would keep passing if TargetCenter regressed. This is a residual of F1 (wrong consumer) and F3 (stale wording), and it is a coverage claim in an artwork-gated live test.
Fix: rank with `PickFrontTarget(objs, ownPos, orient.GetForward(), null)` and assert an in-sector object, or relabel the test as the autopilot implicit-pick proof and state that TargetCenter has unit coverage only.

### N3 (low): the review index says a fixed citation is still stale
`.hyperreview/README.md:19-20` lists among "Observations kept" that "the parity row's older `InputActionCatalog.cs:161` citation is stale", but commit `23843266` changes that row to `:199` (verified at `docs/parity/parity-matrix.csv:87`). The entry contradicts its own commit, so a reader cannot tell whether the disposition is "kept" or "fixed". Reword it to say the citation was fixed.

### N4 (low): the unit test file still names TargetCenter for the tiered list
`tests/Allegiance.ClientCore.MonoGame.Tests/MiscKeyBindingTests.cs:29` ("TargetCenter: nearest to the reticle"), `:34-38`, `:65-66` and `:82-84` describe tests that only call `SelectCandidates` as TargetCenter's core. The class comment directly above now says the opposite. Round 1 asked for this retitle; only the class comment changed. A later reader then edits or trusts `SelectCandidates` as the TargetCenter path, the same failure F1 describes. Retitle the section and the three method docs to the autopilot list, or record why not.

## Observations
- Concurrency: `_poses` is written only on the game thread in the Update pose gather (`Game1.FlightInput.cs:2599`) and read on the same thread here; both callers (`Game1.FlightInput.cs:2179`, `Game1.MousePick.cs:105`) are game-thread. The ungated read is safe, as other callers do. The gather runs later in the same `UpdateFlying`, so a key press uses the previous frame's pose and turret aim: one frame stale, not a defect.
- `IsTurretGunner()` is exactly `ParentId != -1` (`Game1.Camera.cs:1104-1112`), the same predicate as the new check. Because the turret loop runs for every parented ship and seeds on its first frame, `_turretSeeded` is true after that frame. The `carrier.Orientation` fallback is therefore effectively a first-frame path, and native's "rider with no turret" case is not distinctly modelled (pre-existing, not introduced here).
- If `_poses[parent]` is absent (first flying frame, or a frame the carrier fails `CanDrawInSector`), the code silently keeps the frozen `TryOwnPose` result. That is exactly F2's bug and contradicts the note. It is narrow, but cheap to harden by capturing `carrierEnt.Latest` under the gate (already read at `Game1.FlightInput.cs:2657-2661`) and using it as the fallback.
- "Interpolated pose" in `Game1.Targeting.cs:1452` and `knowledge/target-center.md:31-33` is imprecise: the default path is the P448b dead-reckoner; only `remoteTimeline` (M, default off) interpolates. This is why N1's offset changes sign with that flag.
- Scoring equivalence to `CosForward2` (round 1) is unchanged; the delta does not touch `PickFrontTarget`'s arithmetic.
- The note's turret bullet states the pose source correctly but does not record the parent-candidate divergence, and its `Last reviewed` stays 2026-11-25 after a 2026-11-26 edit.
- Parity row: `InputActionCatalog.cs:199` is right. `Game1.Targeting.cs:1448` now lands on a blank doc-comment line (`PickFrontTarget` is at `:1496`), imprecise and pre-existing in this row.

## Verification
Executed (read-only, `<review-clone>`): `rev-parse` and `write-tree` (matched, clean); `git log`, `show`, `diff`, `diff --check`; `git ls-files --eol` (every changed blob `i/lf`; worktree CRLF on `.cs` is checkout conversion); `git status` (clean); SHA-256 of the committed round-1 summary matches the receipt's `2f2843...`; first bytes show no BOM; `scripts/publish-guard.sh 3a3b6e1b HEAD` (OK, 3 commits); `tools/check_data_boundary.py` (OK); `tools/check_memory.py` (OK, with the expected `people/` warnings and the no-baseline limit).
Read, not executed: the delta, `TryOwnPose`, `GatherRenderPoses`, `RemoteShipDrawPose` and its defaults, `IsTurretGunner`, `TurretSeedOrientation`, `GatherSectorCandidates`, `PickFrontTarget`, `CommitTargetForPlayer`, all callers, tests and records. No dotnet build, test, game or headset run was possible. Native sources are absent; the `pship` skip is taken from the review brief.

## Verification limits
The carrier-pick scenario is derived from the render/telemetry offset, not observed in game. The turret branch still has no test and no in-game check, as the author states. Line numbers are at the reviewed head.