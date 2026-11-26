# Patch review r3: VR HUD bracket depth (PR #17, stacked on PR #15)

- Target: `ragesaq/AllegianceCore-public` PR #17, branch `port/vr-hud-bracket-depth`, reviewed HEAD `78e9be38b0d52de5a367924bbc75a55406e7b261`, tree `2b087f3e42937bbbaa2a7ecdca8db2d6ce669139`, delta `e2825d5f..78e9be38`; round 2 covered `47133cbf..e2825d5f`. Standalone clone at `<review-clone>`; `git status --short` showed only this review's untracked artifact directory.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`; reviewer-parity matrix 4.14.0 (sha256 74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267). Authoring family Anthropic/Claude (Port). Cross-family.
- Verdict: **WARN**. nextAction: give the lock marker the world radius for every lockable type, not only ships; add a production-seam test; correct the record; fix the stale cone comment; re-review the small delta.

## Earlier findings

- **N1 (High, round 2) / round 1 F1 (High): partially open.** The mechanism is right and the ship paths are closed. `VrMarker` carries `RadiusMetres`; `AnchorAngle` returns at least `WrapAngle(radius, |World - head|)`; `MarkerAnchor` places the anchor at `d - 1.05 r` or nearer, in front of the near surface at `d - r`. Compose and the off-view gather call `VrMarkerAnchor` (`Game1.VrHud.cs:321,414`), and the reticle passes `marker.World - head` plus the radius (`:379-380`). Contacts are unchanged. But the lock sets a radius only for ships (N1), so a locked station, asteroid or warp keeps the original defect.
- **F2 (Low, round 1): closed.** The note now bounds the probe's agreement to its depth window (about 0.0001 to 0.0003) and says the contacts vary by run; it no longer claims exact equality.

## Findings

### N1 (Medium): a non-ship lock still carries no radius, so its marker can be hidden

`src/Allegiance.ClientCore.Game/Game1.VrHud.cs:233-235` sets `radius` only when `lockState.TargetType` is `Ship`. The flat lock resolves all four types at `src/Allegiance.ClientCore.Game/Game1.Camera.cs:101-108`. Non-ship locks are supported: `src/Allegiance.Core/Sim/Ship.cs:4833-4848` covers hostile visible damageable targets and warps for warp bombs, and `:4881-4883` explicitly handles a non-ship target. For a station lock, `position` is the station's position (`Game1.VrHud.cs:229-232`) but `radius` is null, so `AnchorAngle` falls back to the drawn bracket.

Concrete failure: lock a 500 m radius Outpost 1.2 km from the head. The bracket is the 2 degree floor times 1.3, about 2.6 degrees, so the anchor is at about 1172.8 m while the near surface is at 700 m; the depth test hides the lock marker. With the radius it would be about 47.2 degrees and an anchor at 675 m, in front. The null radius also keeps the reticle from taking depth from a station hull under it.

Suggested fix: reuse the flat lock's switch (or extract a shared `WorldRadius(IgcObjectType type, short id)` helper) and pass the radius for every lockable type. Add a test for a station lock with a known radius and a null-radius regression. Correct the README and DAG claims that "the selection and lock carry the object's world radius": the selection does for all types, the lock only for ships.

### N2 (Low): the new test does not exercise the production marker path

`tests/Allegiance.ClientCore.MonoGame.Tests/VrHudGeometryTests.cs:438-457` calls `AnchorAngle` with a head-relative vector. It never runs `VrMarkerAnchor` (`src/Allegiance.ClientCore.Game/Game1.VrHud.cs:355-360`) or the marker constructors at `Game1.VrHud.cs:201-202,238-240`. A future edit that drops `RadiusMetres: radius` from either constructor, or passes `marker.World` instead of `marker.World - head` to `AnchorAngle`, passes this test and every other test. That is the round 2 defect class, and round 2 asked for a production-seam test. The author's mutant list names dropping the radius term from `AnchorAngle`, not removing `RadiusMetres` at a call site. The wiring is correct by reading; this is a coverage gap.

Concrete failure: remove `RadiusMetres: radius` from the lock marker at `Game1.VrHud.cs:240`; the new test still passes, and a 30 m hull locked 85 m away is anchored at 58.5 m against a 56 m near surface, inside its hull. Suggested fix: move the anchoring calculation to a public pure helper in `VrHudGeometry` taking `(Vector3 world, Vector3 head, float bracket, float wrap, float? radius)`, have `VrMarkerAnchor` call it, and test that helper with separate own and head positions.

### N3 (Low): the reticle-cone comment still describes the old bracket term

`src/Allegiance.ClientCore.Game/Vr/VrHudGeometry.cs:203-206` says the cone is "3 degrees, or half the target's bracket when that is wider". `ReticleInverseDistance` now uses `max(ReticleConeRadians, AnchorAngle(...) / 2)` (`:221-223`), and `AnchorAngle` can be the radius wrap. Suggested fix: say "half the anchor angle" and link `AnchorAngle`; the design note is already correct.

## Observations

1. The lead marker's anchor angle changes from its drawn 1.2 degrees to the 2 degree `WrapAngle(null, d)` floor, so its anchor sits about 0.7 percent nearer (about 0.7 m at 100 m). It cannot hide the marker, but "lead marker unchanged" is not exact. If strict preservation matters, treat a null radius as 0, not the bracket floor.
2. The wider reticle cone is sound for a spherical hull: half the anchor angle is the angular radius of a 1.05 r disk, so a centre within the cone puts the boresight within about 1.05 r of the centre. The bracket enlargement can over-include by roughly 1 to 2 degrees for mid-size near objects, and the 5 percent margin adds about 0.2 degrees for huge ones; I found no case where an object clearly outside the reticle gets depth. The 5 percent floor changes the anchor depth, not the cone. The 15 m clamp can put the reticle behind a very close hull's near surface, but the clamp is pre-existing and the reticle has no depth test.
3. An anchor cannot land behind the head (floor 0.05 d), but it can fall below the 1 m near plane when the near surface is within about 1 m, i.e. the head is at or inside the bounding sphere. `BufferDepth` then clamps to 0 and the marker may draw over everything. This is a collision edge.
4. The new test arithmetic was recomputed with the code's formulas: 40 m from the ship and 26 m from the head with a 20 m radius gives a 5.0 m anchor against a 6 m near surface; the 30 m hull at 85 m gives 55.25 m against 56 m; the 50 m radius at 60 m and 25 degrees off gives reticle depth with the radius and none without it.
5. Records: the round 2 summary's sha256 matches its receipt; the README, design note and `DAG.json` node `ragesaq-010` agree with the code except the "selection and lock carry the radius" claim (N1) and the "at that seam" claim (N2). No machine path or private email in the commit; the author identity is Port's noreply address. The round 1 README entry now says its fix was incomplete.

## Verified

By execution, read-only in `<review-clone>`: `git rev-parse HEAD` and `git write-tree` matched the frozen head and tree; `git status --short` showed only the artifact directory; `scripts/publish-guard.sh 051f0cc3 HEAD` OK (3 commits); `tools/check_data_boundary.py` OK; an added-line scan found no machine path or email; committed EOL is LF for the text files; the round 2 summary hash matched; the new test arithmetic was recomputed with Python.

By reading: every marker path and the reticle, the cockpit camera's 14 m offset, the flat lock's radius switch, the server lock rules, the new tests, the design note, the README entries, the round 2 receipt and summary, and the DAG node.

## Limits

I could not run `dotnet`, the build, the 97 tests, the 13 mutation breaks, the GPU probe or the headset; those remain the author's evidence. The hull is modelled as a sphere. The author's build, test and mutation numbers are reports, not observations. I did not run `tools/recall.py`, `tools/check_map.py` or any write operation on the target.