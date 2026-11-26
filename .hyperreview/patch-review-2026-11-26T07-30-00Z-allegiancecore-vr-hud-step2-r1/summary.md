# Patch review: VR HUD step 2, PR #22 (`port/vr-hud-step2`), round 1

- Target: `ragesaq/AllegianceCore-public` PR #22, branch `port/vr-hud-step2`, base `master` at
  `f362cdf2b6b81b3af30435fe643324cb3a00d9d5`; the whole PR, `f362cdf..2e0a4314` (5 commits, one a merge of master;
  14 files).
- Reviewed HEAD `2e0a43141df0edf1a9857cf6316603854694e4d0`, tree
  `ab9e95514d78e7610e938ca8af580437f73d5a39`; both matched, worktree clean.
- Reviewer: DeepSeek, `deepseek-v4.1-flash` via OpenCode Go, `high` (reviewer-parity matrix 4.14.0, sha256
  `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; route from `AGENTS.md`,
  "Cross-family review route"). Cross-family to Port (Claude Code, Claude Opus 5.5).
- Author evidence read: build 0 warnings; VR HUD tests 94/94; full project 5657 pass, 8 known fails; 24
  single-point mutations each failed a test; flat replay frames byte-identical to master (one skin only); VR
  Classic runs under SteamVR with no exceptions.

**Verdict: PASS-WITH-CONDITIONS.**

**nextAction:** correct or answer the four low-severity record/comment items below in the PR, then ragesaq's
headset test and the merge decision. The code itself is approved by this review; no functional defect was found.

## What I verified by execution

- `git write-tree` prints `ab9e9551...`; HEAD is `2e0a4314`; `git status --short` is clean.
- `scripts/publish-guard.sh f362cdf HEAD` passes for all five commits; `tools/check_data_boundary.py` passes;
  `git diff --check` is clean.
- Not run: no dotnet, build, test, deploy or install. The behaviour below is verified by reading the target and
  its tests, against the author's numbers and headset evidence.

## Findings (all low: records and comments; no code defect)

### F1 (Low) The `FlightBlock` comment calls the crop 40 degrees; the crop is wider

`src/Allegiance.ClientCore.Game/Vr/VrHudGeometry.cs:68-74` describes `WidthRadians` as "the angle its crop
covers across" and says the block is 40 degrees across the crop. The crop is `CentreGroupBox` (`:118-123`),
`322 * spread + 160` px wide: about 530 px at a 720-line image and 836 px at 2160, which is about 52 to 74
degrees across. The 40 degrees is the angle of the 400-px reference span about the centre
(`FlightBlockReferencePixels`, `:105-108`); the 215-px ring is about 21.5 degrees by the note's small-angle
figure (`knowledge/vr-hud-design.md:369`).

Failure scenario: someone tuning `ALLEGIANCE_VR_FLIGHT_BLOCK` to `40,0` expects a 40-degree block; or a later
maintainer "fixes" `CentreGroupBox` to 400 px to match the comment, shrinking the block and clipping the
own-status and speed.

Fix: reword to "the angle the reference 400-px span covers across" and "the reference 400 px are 40 degrees, so
the 215-px ring is about 21.5 degrees".

### F2 (Low) "Both eyes see the same pixels" is not what production draws; the test cannot catch a wrong per-eye projection

`knowledge/vr-hud-design.md:371` and `tests/Allegiance.ClientCore.MonoGame.Tests/VrHudGeometryTests.cs:624`
(`A_view_at_infinity_shows_the_same_pixel_to_both_eyes_and_to_a_moved_head`) say the block shows the same pixels
in both eyes. In production `src/Allegiance.ClientCore.Game/Game1.VrHud.cs:346` passes `SeatProjection(proj)`
with each eye's own projection; those projections are off-centre (`VrEyeCamera.OffCentreProjection`), so the same
view-space corner lands on different pixels in each eye. That is correct infinity stereo (zero vergence), not
identical images. The test uses one shared projection for both eyes, so it proves only that `AtInfinity` removes
the view translation; it would still pass if the right eye were given the left eye's projection, which is a real
stereo error.

Fix: say "the same direction, so no vergence disparity", and add a test with separate left and right off-centre
projections that asserts the block's corners subtend the same world direction in both (or that the pass's own
projection is used).

### F3 (Low) The decision table still says the block "keeps speed and throttle"

`knowledge/vr-hud-design.md:195` (Horizon cues in VR) says "the block keeps speed and throttle". That was
written for the first Jet-tape build; the final block is the Classic centre group (ring, speed, own status). The
next row (`:196`) records the revision and "Step 2 as built" (`:350` onward) matches the code, so the sentence is
stale and can mislead a reader into expecting a throttle tape in the block.

Fix: add "(superseded by Classic's ring, below)" or reword the clause.

### F4 (Low) The row-basis inventory omits two sites

`knowledge/vr-hud-design.md:434-435` says "Eight other renderers read the rows the same way ... they are not
changed here". The same row read also appears at
`src/Allegiance.ClientCore.Game/Rendering/NativeScene.cs:56-57` (camera right/up handed to `debrisGeo.Step`) and
`src/Allegiance.ClientCore.Game/Audio/SoundManager.cs:1329` (the 3D listener right, whose comment at `:1324`
asserts the rows, the opposite of `TargetModelPreview.CameraBasis`). If the sentence is meant as the complete
follow-up set, it is short by two (`ShipRenderScale.cs:71` reads a transform's rows for a uniform scale, which
may be legitimate). Failure scenario: the debris/dust geometry and the stereo-pan basis stay wrong on turned
cameras, and no follow-up names them.

Fix: name the two sites, or scope the sentence to billboard renderers, and correct the `SoundManager` comment. The
code for them can stay out of this PR.

## Observations (not defects)

- **Flat equivalence holds by reading.** With `FlightReferenceInWorld` false, `CentreGroupOnMainPass` equals
  `CentreHudVisible`; `DrawCentreGauges` reproduces master's three branches, `DrawSpeed` and
  `ElementDrawn("Speed")`, and `DrawAfterburner` follows under the same condition, in the same order, with the
  same additive pass and AlphaBlend end/begin. Modern calls `base.Draw`, so its `DrawGauges`/`DrawOwnStatus`
  overrides are gated too. Jet's heading tape, pitch ladder, speed tape, throttle tape and roll pointer are gated
  by `!FlightReferenceInWorld`, and the flat path is unchanged.
- **`DrawFlightReference` state is contained.** It sets `_viewportWidth`/`_viewportHeight` and `Layout`, but
  `Draw` re-initialises them on every pass and nothing after `ComposeVrHud` reads `Layout`. `DrawVrFlightImage`
  (`Game1.VrHud.cs:150-165`) leaves its target bound only until the loop's first `SetRenderTarget` (`:333-346`);
  the per-frame model is kept by `KeepVrHudModel` and cleared at `:401`, so a frame with no HUD pass draws no
  block. The flight image uses `PreserveContents` for the same reason the HUD image does.
- **`MarkerTestDepth` is consistent** with `BufferDepth` (0 near, 1 far) and with the scene's eye projection;
  behind-the-eye anchors are skipped by `TryProject`; the 0.95 cap makes the sphere's near point equal the
  anchor's floor. Trade-off: testing at the marked object's sphere-near depth lets a marker draw over a different
  object that lies between the anchor and that near point (for example a ship at 850 m in front of a marked 1 km
  station of 200 m radius, tested at about 790 m). No test covers an unrelated occluder; it is inherent to a
  one-depth flat marker.
- **Crop coverage is adequate for the real art**, but `VrHudGeometryTests.cs:588` assumes fixed 8/32/40/24-px
  margins around the anchors rather than measuring the MDL canvases or the text; a margin cut that still clears
  those numbers would pass while clipping a large canvas glyph or the speed unit. The smallest tested image is
  720 lines; below about 546 the box is clamped by the image edge.
- **Classic's ROLL dial stays on the panel in VR.** `DrawRoll` (`HudRenderer.cs:483`) is outside the centre gate,
  so the port-only numeric bank dial still shows on the 1 m panel. The decision table's "no horizon cues" names
  the pitch ladder, heading tape and roll pointer, so this may be intended; confirm with ragesaq whether the ROLL
  dial should also be hidden.
- **Author-evidence limits.** The byte-identical replay cannot distinguish skins (disclosed) and, unless it
  contains a turned camera and rounds, cannot show the billboard fix; the Modern and Jet flat passes and the flat
  billboard change are unverified by execution. In the no-classic-art fallback the throttle gauge draws at the
  image's right edge, outside the crop, and the "VECTOR LOCK" text can exceed the crop's right margin; degraded
  mode only.

## Records against the code

The decision table (no horizon cues; Classic's ring, both 2026-11-26), "Step 2 as built", the headset notes and
the timeline match the code apart from F1-F4. No machine path, host name or private email appears in the
committed files (the publish guard and the data-boundary check pass). Tests are behavioural where it matters
(Jet's four elements through its own `Draw`, the billboard basis with a rows control, the marker depth with an
anchor-only control); `VrCentreGroupTests` restates the `CentreGroupOnMainPass` truth table, so treat it as a
regression pin rather than independent evidence.