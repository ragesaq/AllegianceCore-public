# Patch review r2: VR HUD bracket depth (PR #17, stacked on PR #15)

- Target: `ragesaq/AllegianceCore-public` PR #17, branch `port/vr-hud-bracket-depth`, reviewed HEAD `e2825d5f143e59f8e543372a242b25eca969f664`, tree `c9fe1ba6bb6b41493a11f732f0b8bd949a34d0f0`, delta `47133cbf..e2825d5f`; round 1 covered `051f0cc3..47133cbf`. Standalone clone at `<review-clone>`, no remote, branches or tags; `git status --short` showed only this review's untracked artifact directory.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`; reviewer-parity matrix 4.14.0 (sha256 74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267). Authoring family Anthropic/Claude (Port). Cross-family.
- Verdict: **WARN**. nextAction: close F1 by measuring the wrap at the anchor's origin (the head), add a production-seam test, correct the "F1 fixed" record, then re-review the delta. F2 is closed.

## Round 1 dispositions

- **F1 (High): open.** The wrap mechanism is right, but the selection and lock compute it from the own ship's centre and apply it from the head, 14 m ahead in the cockpit camera. The close-range case F1 named is still hidden.
- **F2 (Low): closed.** The new theory pins `BufferDepth` to the perspective formula at the near plane, two mid distances and the far plane, and the recorded probe covers a mid-depth value as well as the far tail. Two record-wording notes below, no code action.

## Findings

### N1 (High, F1 remains): close selection and lock markers are still depth-hidden

`src/Allegiance.ClientCore.Game/Game1.VrHud.cs:197,199-201` (selection) and `:236-239` (lock) compute `WrapAngle(radius, distance)` with `distance` from the own ship's simulation position. `VrMarkerAnchor` (`Game1.VrHud.cs:354-355`) applies that angle to `marker.World - head`, where `head` is the eye midpoint (`Game1.VrHud.cs:279`). The cockpit camera eye is `shipPosition + forward * 14 m` (`Rendering/CockpitCamera.cs:44,73-74`, built with `forwardOffset: 14f` in `Game1.Lifecycle.cs:264-265`), and `VrEyeCamera.ForEye` adds only the headset offset (`Vr/VrEyeCamera.cs:36-37`). For a target ahead, `d_head` is about `d_own - 14`.

`MarkerAnchor` computes `nearSide = d_head * (1 - tan(wrap/2))` (`Vr/VrHudGeometry.cs:169`), so with `tan(wrap/2) = 1.05 r / d_own` the anchor is at `d_head - 1.05 r (d_head/d_own)`. The object's head-relative near surface is at `d_head - r`. The anchor is inside the hull whenever `d_head/d_own < 1/1.05 = 0.952`, which is any target dead ahead inside about 294 m. The 100 m clamp rescues it once its near side passes 100 m, so a 30 m hull fails roughly from 16 m to 128 m dead ahead.

Concrete failure, round 1's own threshold: lock a 30 m hull (radius 15 m, `HullLength * 0.5`) at 85 m dead ahead. The wrap is 21.0 degrees, the drawn box the 20 degree cap, `d_head` 71 m. The anchor is 57.84 m and the near surface 56 m, so it is 1.84 m inside and the read-only depth test hides the lock box; the selection at the same range is identical. The delta moves round 1's 58.48 m to 57.84 m, not out. At the production head distance even the new test's numbers fail: a 20 m radius at 40 m from the ship is 26 m from the head, so the anchor is 12.35 m against a 6 m near surface, 6.35 m inside.

The new test (`tests/Allegiance.ClientCore.MonoGame.Tests/VrHudGeometryTests.cs:403-419`) passes one vector to both the angle and the anchor, so it cannot see the two origins. It passes on the pure geometry and gives false assurance for the production path.

Suggested fix: carry the object's world radius (or its head-relative near-side distance) on `VrMarker`, and have `VrMarkerAnchor` use `|World - head| - 1.05 r` with the 5 percent floor and 100 m clamp; alternatively compute the wrap from `position - head` once the eye cameras exist. Add a test at the production seam with separate own and head positions (own 40 m, head 26 m, radius 20) asserting the anchor is nearer than `d_head - r`. Correct the note and README claims that F1 is fixed.

## F2 notes (no code action)

- The theory pins exactly what F2 asked and fails on a `0.5 z + 0.5` remap. It uses the test projection's 0.5 m near plane rather than the flight camera's 1 m; the identity holds for any near and far, so this is cosmetic.
- The note's "the sprite path and the scene write the same value" is stronger than the probe. The red and green squares bound a constant or scale difference to the red-green depth window, about 1e-4 to 3e-4 at 1.4 to 2.6 km, which excludes a 0.5 remap but not exact equality. The procedure cannot reproduce the same contacts, radii or pixels. Both are Low record-quality issues.

## Observations

1. `BracketAngle` is algebraically unchanged: `min(max(wrap, floor), cap)` equals the old `Clamp(wrap, floor, cap)` for every input, including the invalid guards; existing tests cover the floor, cap and null radius.
2. Wrap angles near 180 degrees: `MarkerAnchor`'s 5 percent floor takes over above about 87 degrees, so a huge close object's anchor stays in front; the float `tan(pi/2)` cliff needs `r/d` above about 1e7 and is unreachable in play.
3. Small objects: the anchor is `d - 1.05 r` (with the 1.15/1.3 drawn enlargement), just in front of the surface. The depth test remains useful and the vergence error is negligible.
4. Contacts are self-consistent: the wrap and the ray come from the same radar projection and distance. The 113 px screen-radius cap can, however, underestimate a very near asteroid's true angular size, so for `r` above about `0.61 d` the contact anchor can fall inside; a near-collision edge case.
5. The reticle's depth inherits N1, so under a very close selection it eases to a too-near anchor; it is drawn without a depth test, so it stays visible. The cone still uses the capped bracket, so a large object whose centre is 10 to 30 degrees off the boresight gives no depth even when its hull is under the reticle. The wrap would be the honest cone term.
6. `ReleaseVrHud` now zeroes `_vrReticleInverseDistance` and `_vrReticleStepTicks` (`Game1.VrHud.cs:583-584`); only the VR compose path reads them. Correct.
7. Records: the round 1 receipt's head and tree (`47133cbf`, `189b079`) and the committed summary's sha256 (`e1432c1...`) match. The README entry, DAG node `ragesaq-010` (`updated` 2026-11-25, PR #17 link) and the note are consistent with the code, except the "F1 fixed" claim. The note's added sentences on the 100 m clamp and a tuned panel are accurate. No machine path or private email in the commit.

## Verified

By execution, read-only in `<review-clone>`: `git rev-parse HEAD` and `git write-tree` matched the frozen targets; `git status --short` showed only the untracked artifact directory; `scripts/publish-guard.sh 051f0cc3 HEAD` OK (2 commits); `tools/check_data_boundary.py` OK; a scan of added lines found no machine path and no email; the changed files' committed EOL is LF. The arithmetic above was recomputed independently with the code's own formulas.

By reading: the cockpit camera offset and the eye/head construction; every marker path and the reticle; the test helpers; the radar screen-radius floor and cap; the round 1 record hashes and the DAG and note changes.

## Limits

I could not run `dotnet`, the 95 VR tests, the mutation breaks, the GPU probe or the headset; those results are the author's reports, not my observations. The hull is modelled as a sphere, so a real hull shifts the thresholds, not the defect. The head/own mismatch is the cockpit camera, the default flight view; in chase and external views the camera is behind the ship, which moves the anchor the safe way.
