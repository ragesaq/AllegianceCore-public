# Patch review r1: VR HUD bracket depth (PR #17, stacked on PR #15)

- Target: `ragesaq/AllegianceCore-public` PR #17, branch `port/vr-hud-bracket-depth`, HEAD `47133cbf3a0e16dd44abe5d7fd5d5b437cc82046`, tree `189b0790dde88ce83cb19d624471b67366fe1cae`, range `051f0cc3..47133cbf`. Standalone clone at `<review-clone>`; `git status --short` empty.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high`; reviewer-parity matrix 4.14.0 (sha256 74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267). Authoring family Anthropic/Claude (Port). Cross-family.
- Verdict: **PASS-WITH-CONDITIONS**. nextAction: fix F1 (anchor from the object's true angular size, not the capped bracket) and add the capped-case test; address or record F2; then re-review the delta before the headset look.

## Findings

### F1 (High): a bracket capped at 20 degrees is depth-tested inside its own object, so the marker disappears

`Game1.VrHud.cs:348-349` places every marker with `VrHudGeometry.MarkerAnchor(marker.World - head, marker.AngleRadians)` (`VrHudGeometry.cs:165-172`), and `Game1.VrHud.cs:314-316` tests its depth. But `marker.AngleRadians` is the *drawn* angle, which is capped at 20 degrees (`VrHudGeometry.cs:24`; `BracketAngle` and `BracketAngleFromScreenRadius`, `VrHudGeometry.cs:243-256`; the selection multiplies by 1.15 and re-caps, `Game1.VrHud.cs:195`, the lock by 1.3, `Game1.VrHud.cs:232`). At the cap the anchor is at `0.8237 d`; the object's near surface on that ray is at `d - r`. When the cap binds (`d < 5.955 r`), `0.8237 d > d - r` once `d < 5.67 r`, so the hull is nearer than the marker and the read-only depth test hides the marker and its name/range label (`DrawVrLabel` uses the same layerDepth) at every pixel the hull covers.

Failure scenario: select an Outpost (radius 302.5 m, `docs/reports/P699c_assets/corpus.json`) and fly toward it. Inside about 1.7 km the bracket's edges and label are hidden (only the square's corners may remain); inside about 1.2 km nothing of the square remains. A Shipyard (r = 540 m) loses its bracket inside about 3.1 km, a Refinery (r = 121 m) inside about 690 m. The lock diamond is fully hidden for anything inside 5.67 r, so locking a 30 m-hull ship inside about 85 m removes the lock box in a dogfight. Step 1 drew these markers at the object's depth with no test, so this is a regression introduced by this change, and it contradicts the change's own stated invariant ("at the near side of the sphere ... so the object does not hide its own bracket", `knowledge/vr-hud-design.md`, "The layer 2 follow-up as built", first bullet). The committed near-side test uses an uncapped angle (10 degrees at 30 m), so no test covers the cap; the 12 new cases and 9 reported mutation breaks pass with this defect present.

Suggested fix: carry the object's true angular radius (or the near-side distance) on `VrMarker` and use it for the anchor, for example `d - 1.05 r`, floored at 5% of `d` and capped at 100 m. Keep the 20 degree cap for the drawn size and the reticle cone. Add a test with a capped case (40 m away, 20 m radius: true angle about 55 degrees, drawn 20) asserting the anchor is nearer than `d - r`; it fails on the current code.

### F2 (Low): the evidence for the depth-value convention is a single tail probe, and the unit test does not pin it

The change's load-bearing platform claim is that `clip.Z / clip.W` (`BufferDepth`, `VrHudGeometry.cs:177-182`) equals the value the scene left in the eye target's depth buffer, and that SpriteBatch's `layerDepth` enters the same pipeline. The only direct evidence is the uncommitted GPU probe at 0.9999, a far-tail test: it shows a near surface occludes and an unwritten one does not, but it cannot distinguish a constant offset or scale between the sprite path and the scene shader path (an extra 0.5, or a 2x map) which would still pass at 0.9999 while wrongly hiding or showing markers at mid depths. The committed test `A_nearer_point_gets_a_smaller_depth_buffer_value` (`VrHudGeometryTests.cs:336-345`) checks only ordering and signs; a uniform remap passes it.

Suggested action: probe at mid depth too (a sheet at about 0.6 and 0.9 against known surfaces), or read back the depth at one known pixel and compare it with `BufferDepth` for that point, and record the procedure in `knowledge/` so it can be repeated when MonoGame or the backend changes. A unit test pinning `BufferDepth` at the near plane (0), the far plane (1) and one known distance to the analytic `far(d-near)/(d(far-near))` closes the regression gap.

## Observations (no action required)

1. The 100 m clamp also limits occlusion: an object between 100 m and the target never hides a far marker. The marker docstring says so; the knowledge note gives only the vergence reason. One sentence there would keep the record honest.
2. Selection and lock on one object produce two coincident arrows (`Game1.VrHud.cs:394-415`). Cosmetic.
3. An anchor outside one eye but inside the other gives an arrow in both eyes. That matches the stated "outside either eye's image" rule; worth a look in the headset.
4. The panel is drawn last with no depth test. "At 1 m it is nearer than every marker" holds for the default, but `ALLEGIANCE_VR_HUD_PANEL` allows 0.3 to 10 m, so a tuned panel can be farther than a marker yet still draw over it. Defaults are fine.
5. `ReleaseVrHud` leaves `_vrReticleInverseDistance` and `_vrReticleStepTicks`. They are inert while flat (only `ComposeVrHud` steps and reads them); a later attach would ease from the stale value with the 0.1 s cap. No practical effect; zeroing both in `ReleaseVrHud` would remove the doubt.
6. `VrNoseInWorld` is right: seat forward through `SeatView`, back through the inverse of eye 0's view, reduces to the game camera's forward in world, independent of head orientation. Checked against `VrEyeCamera.ForEye`/`SeatView`; it matches the old flat reticle's seat-forward convention, and the reticle's two branches agree in the limit.

## What I verified

By execution, read-only in `<review-clone>`: `git rev-parse HEAD` = `47133cbf...`; `git write-tree` = `189b0790...`; `git status --short` empty; `scripts/publish-guard.sh 051f0cc3 HEAD` OK (1 commit); `tools/check_data_boundary.py` OK; a scan of the added lines found no machine path and no private email.

By reading: the eye pass uses exactly `_vrEyeCameras[pass].Projection` (`Game1.Vr.cs:63-71`), and every path that would move depth off the eye target is disabled on an eye pass (`fsr`, `hdr`, `bloom`, `soft` and AA are all `!VrEyePass`, `Game1.Rendering.cs:550-570`), so the scene's depth in the `Depth24Stencil8` `PreserveContents` target (`Game1.Vr.cs:100-116`) is what the marker test reads; `DepthRead` writes no depth, `_vrMarkerDepth` is reset in the `finally` (`Game1.VrHud.cs:318-324`) and only `VrLine`/`DrawVrLabel` read it, so nothing leaks into the panel (`BasicEffect`) or a flat frame; the off-view test uses both eye cameras and pixel bounds, a point behind the eye fails `TryProject` and yields a down arrow; the ring point is 3 m out at 30 degrees; the reticle is stepped once per frame, starts at infinity on the first frame, caps a pause at 0.1 s, and resets to 0 when the reticle is off; flat frames reach none of the new code.

## Limits

I could not run `dotnet`, the 90 VR tests, the mutation breaks, the GPU probe or the headset; those are the author's reports, not my observations. The depth-convention equivalence is argued from the shared MonoGame 3.8.5.1 DesktopGL shader path plus the author's tail probe (F2). The hull is modelled as a sphere; a concave station model shifts F1's threshold but not the defect.
