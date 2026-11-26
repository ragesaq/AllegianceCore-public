# Patch review r5: VR HUD bracket depth (PR #17, stacked on PR #15)

- Target: `ragesaq/AllegianceCore-public` PR #17, branch `port/vr-hud-bracket-depth`, reviewed HEAD `dec2b473fba2f9b86e1294b2d5e8d6b3cfcf2cb6`, tree `be9f663621f71ce41b3206378709257a1b5fc00c`, delta `1a3adf72..dec2b473`, the answer to round 4. Standalone clone at `<review-clone>`; `git status --short` shows only this review's directory.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`; reviewer-parity matrix 4.14.0 (sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Authoring family Anthropic/Claude (Port). Cross-family.
- Verdict: **PASS**. nextAction: ragesaq's headset look, then merge approval. The lookup test stays deferred as the note records; that deferral is honest and is accepted residual risk, not a blocker.

## Earlier findings

- **R4-F1 (Low): closed in the code; the test is honestly deferred.** `AddVrLockMarker` maps the four lockable `IgcObjectType` values to `TargetKind` and calls the selection's lookup (`src/Allegiance.ClientCore.Game/Game1.VrHud.cs:235-243`). The four arms of `TargetWorldRadius` (`:205-213`) are the same expressions, modulo local names, as the switch this delta removed and as the flat lock's switch (`src/Allegiance.ClientCore.Game/Game1.Camera.cs:101-108`): Ship `HullLength/2` guarded by `> 0`, Station the catalog radius, Asteroid and Warp `Radius` guarded by `> 0`, otherwise null. A non-lockable type maps to `null` (`:241`) and then a null radius. `lockState.TargetId` is `short`, as `AutopilotTarget.Id` is; the empty name is unused. Safety: the lookup takes no lock and touches only the world dictionaries and `_shopCatalog`, and it runs under `_session.Gate` exactly where the removed switch ran (`:222-243`); the selection already calls it in that lock (`:193`). So the radius per type, the null cases and the lock context are unchanged.

  The deferral is stated honestly. `git grep` over `tests/` finds no `TargetWorldRadius`, `AddVrLockMarker`, `AddVrSelectionMarker` or `_vrMarkers`, and tests never construct `Game1`; the lookup is a private instance member that also needs `_hullLengthById` and `_shopCatalog`. The note says no test runs the lookup and that it is checked by reading (`knowledge/vr-hud-design.md:287-288`). That does not overstate coverage; observation 2 covers the reason's wording.
- **R4-F2 (Low, records): closed.** The round 3 README entry now says the radii stand for a ship, a hull and a station and that the test does not run the type lookup; the test's tuples are (40 m, 20 m), (85 m, 15 m), (600 m, 500 m) (`tests/Allegiance.ClientCore.MonoGame.Tests/VrHudGeometryTests.cs:480`). The note now says only a marker with no radius can be hidden, within 100 m (`knowledge/vr-hud-design.md:285-287`). For a known radius the anchor is `d - 1.05 r` against a near side `d - r`; for no radius, hidden implies a near side under 100 m. Both corrections hold.

## Findings

None. No new defect in this delta.

## Observations

1. The new sentence "in front of the near side at any distance" (`knowledge/vr-hud-design.md:285`) is exact while the head is outside the object. `MarkerAnchor`'s 0.05 floor (`src/Allegiance.ClientCore.Game/Vr/VrHudGeometry.cs:169`) puts the anchor behind the near side once the radius exceeds about 0.95 of the distance (r = 0.96 d at d = 100 m: anchor 5.0 m, near side 4.0 m). That is the head inside or at the surface, unchanged since rounds 3 and 4 and visually meaningless; a wording point only ("whenever the head is outside the object" would be exact).
2. The deferral's stated reason is narrower than the real one: a test also needs a `Game1` with the hull-length and station catalogues, or the static helper R4-F1 offered as the alternative. The wording understates the work, not the coverage.
3. "One switch" is true for the VR radius lookup; the flat path keeps its own lock and selection switches (`Game1.Camera.cs:101-108`, `:195-202`), which are screen-space and unchanged. Not a defect.
4. The records agree with the code and the round 4 summary: the README entry, the round 4 receipt, the note and `DAG.json` node `ragesaq-010` (round 4 PASS-WITH-CONDITIONS, answered; round 5 reviews the delta). The round 4 summary's sha256 `13bb2cc665130d1cb310ccf1e00a0082a76f562d0bd0acb6410cc57443a31c50` matches the receipt, and the receipt's reviewed tree `badfd07568d7e9c6c0aa726462a56ac3537f2957` is `1a3adf72`'s tree.

## Verified

By execution, read-only in `<review-clone>`: `git rev-parse HEAD` and `git write-tree` matched the frozen head and tree; `git status --short` shows only this directory; `git diff --check` is clean; file modes are 100644; the diff holds no machine path or email; `scripts/publish-guard.sh 051f0cc3 HEAD` OK (5 commits); `tools/check_data_boundary.py` OK; `git ls-files --eol` shows LF in the index for every touched text file (`Game1.VrHud.cs` has no eol attribute, index LF, working tree CRLF from this clone's checkout and not changed by the commit); recomputed the three anchor cases (5.0, 55.25, 61.0 m against near sides 6, 56, 86 m), the no-radius cases (21.42, 58.48, 100 m) and the 0.95 d floor edge in Python; the round 4 summary hash.

By reading: the lock and selection paths, `TargetWorldRadius`, `MarkerAnchor`, `AnchorAngle`, `WrapAngle`, `BracketAngle`, `MarkerAnchorInWorld`, the flat lock's switch, the new tests, the README entries, the round 4 receipt and note, and the DAG node.

## Limits

I could not run `dotnet`, the build, the 99 VR tests or the headset; those numbers are the author's reports, not observations. The hull is modelled as a sphere. I did not run `recall.py` or `check_map.py`. No repository memory or knowledge entry was written: this clone is a frozen review copy and only this directory is writable.