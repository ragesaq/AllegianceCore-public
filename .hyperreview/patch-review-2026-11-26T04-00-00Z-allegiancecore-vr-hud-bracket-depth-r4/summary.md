# Patch review r4: VR HUD bracket depth (PR #17, stacked on PR #15)

- Target: `ragesaq/AllegianceCore-public` PR #17, branch `port/vr-hud-bracket-depth`, reviewed HEAD `1a3adf72a501220234c6b2dd60570f77c37d861d`, tree `badfd07568d7e9c6c0aa726462a56ac3537f2957`, delta `78e9be38..1a3adf72`, the answer to round 3. Standalone clone at `<review-clone>`; `git status --short` showed nothing but this review's artifact directory.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`; reviewer-parity matrix 4.14.0 (sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Authoring family Anthropic/Claude (Port). Cross-family.
- Verdict: **PASS-WITH-CONDITIONS**. nextAction: close or explicitly defer the two Low items below, then ragesaq's headset look. No source defect found in this delta.

## Earlier findings

- **N1 (Medium, r3): closed in the code.** `AddVrLockMarker` now resolves a radius for Ship, Station, Asteroid and Warp (`src/Allegiance.ClientCore.Game/Game1.VrHud.cs:233-245`), arm for arm the flat lock's switch (`src/Allegiance.ClientCore.Game/Game1.Camera.cs:101-108`). The radius reaches the marker (`Game1.VrHud.cs:250`), `VrMarkerAnchor` (`:365-366`) and the reticle (`:385-386`).
- **N1's example: the author's correction holds and round 3's example was wrong.** `MarkerAnchor` clamps to 100 m (`src/Allegiance.ClientCore.Game/Vr/VrHudGeometry.cs:165-171`), so a 500 m radius Outpost locked 1.2 km from the ship (1186 m from the head) is anchored at 100 m, in front of its near side at 686 m. Round 3's 1172.8 m figure omitted the clamp. The defect needs the near side within 100 m: with a null radius the anchor is min(0.977 d, 100 m), so a 15 m radius object 110 m from the ship (96 m from the head) is anchored at 93.8 m against a near side at 81 m and hidden. Recomputed with the committed formulas.
- **N2 (Low, r3): closed as scoped.** `VrHudGeometry.MarkerAnchorInWorld` holds the whole calculation (`VrHudGeometry.cs:240-249`); `VrMarkerAnchor` only forwards (`Game1.VrHud.cs:365-366`). The new test passes separate world and head points, so passing the world vector instead of the head-relative one now fails it. The constructor relay stays read-only (R4-F1).
- **N3 (Low, r3): closed.** The cone comment names half the anchor angle (`VrHudGeometry.cs:203-206`) and the code agrees (`:222-223`).
- **Round 3 observation 1: handled.** `AnchorAngle` treats a null or non-positive radius as 0 (`VrHudGeometry.cs:236-238`), so the lead marker is anchored from its drawn 1.2 degrees again, with a test (`tests/Allegiance.ClientCore.MonoGame.Tests/VrHudGeometryTests.cs:493-500`). Contacts and selection are unchanged: their bracket or wrap is never below the floor.

## Findings

### R4-F1 (Low): the production wiring into MarkerAnchorInWorld is still read-only

`Game1.VrHud.cs:233-245` (the four type arms) and `:202,250` (`RadiusMetres: radius` at the two constructors). No test reaches any of it: `git grep` over `tests/` finds no `AddVrLockMarker`, `TargetWorldRadius`, `VrMarkerAnchor` or `_vrMarkers`, so the suite can pass with the radius dropped anywhere between the world and the helper. Failure: delete the Station arm; a 500 m station 600 m from the ship (586 m from the head) is then anchored at 100 m against a near side at 86 m and hidden. The same happens if `RadiusMetres: radius` is removed from the lock marker: the capped 20 degree bracket alone gives the same 100 m anchor. Suggested fix: reuse `TargetWorldRadius` for the lock (one switch, not two) or extract a static `WorldRadius(world, type, id, catalog)` helper, and test it with a world holding one object of each type; that also removes the duplicated switch.

### R4-F2 (Low, records): two record sentences overstate the tests and the clamp

- `.hyperreview/README.md:21` says the new test runs the world-space path "for a ship, a locked hull and a station". The test (`VrHudGeometryTests.cs:472-491`) runs the pure helper with three radii (20, 15, 500 m). It never exercises a station lock or the Station arm; the sizes stand for those objects, the type path does not.
- `knowledge/vr-hud-design.md:285-286` says "an object can hide its own marker only when its near side is within 100 m". With a known radius the anchor is d - 1.05 r before the clamp, always in front of the near side at d - r, so such a marker never hides. The residual case is a marker with no radius (a failed lookup), where hiding needs its near side within 100 m. Suggested fix: say "a marker with no radius".

## Observations

1. The new test's arithmetic was recomputed: 40 m/20 m gives 5.0 m against a 6.0 m near side; 85 m/15 m gives 55.25 m against 56.0 m; 600 m/500 m gives 61.0 m against 86.0 m; with a null radius the same brackets give 21.4, 58.5 and 100 m, each inside. The direction check holds because `MarkerAnchor` scales the relative vector.
2. The test compares the same radius-sized bracket with and without the radius, which is the constructor-drop scenario. It is a fair geometry test, but it is not a call-site test (R4-F1).
3. The 100 m clamp still lets a large hull whose near side is under 100 m hide a no-radius marker; the note records the clamp, with the wording point above. The round 3 clamp edge (head inside the sphere, anchor under the 1 m near plane) is unchanged.
4. Records otherwise agree with the code: the round 3 summary hashes to the receipt's `90a93e08...97`; the README, note and `DAG.json` node `ragesaq-010` say round 3 WARN and round 4 reviews the answer. `DAG.json` `updated` is 2026-11-25, the commit date. No machine path or email in the commit; author and co-author use noreply addresses; `git diff --check` is clean.

## Verified

By execution, read-only in `<review-clone>`: `git rev-parse HEAD` and `git write-tree` matched the frozen head and tree; `git status --short` was empty before this artifact directory; `scripts/publish-guard.sh 051f0cc3 HEAD` OK (4 commits); `tools/check_data_boundary.py` OK; `git ls-files --eol` shows LF in the index for every touched text file; the round 3 summary hash; the test arithmetic and the N1 correction in Python; `git grep` showed no test reaches the marker or lock code.

By reading: the lock and selection paths, `VrMarkerAnchor`, `MarkerAnchorInWorld`, `AnchorAngle`, `MarkerAnchor`, `WrapAngle`, the reticle path, the off-view gather, the flat lock's switch, both new tests, the README entries, the round 3 receipt, the note and the DAG node.

## Limits

I could not run `dotnet`, the build, the 99 tests, the 15 mutation breaks or the headset; those numbers are the author's reports, not observations. The hull is modelled as a sphere. I did not run `recall.py`, `check_map.py` or any write on the target; the only write is this directory.