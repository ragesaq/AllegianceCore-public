# Patch review: VR HUD zones, round 5 (fourth look)

- Head `2fac59aa79514db83e56ada944ff3320ae57731a`, tree `e43ea39d0a109aa5fc2631e4e494d2bc2ad6e4de` (`git write-tree`). Change `c82dea66..2fac59aa`, one commit; standalone clone detached at `port/vr-hud-zones` (PR #23, stacked on #22 at `601a46ae`).
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection; matrix 4.14.0 (sha256 `74618f22...c8a63267`). Cross-family; author seat Port (Claude Opus 5.5). Route source: the target's `AGENTS.md`, "Cross-family review route".
- Verdict: **WARN**. Two medium findings, two low record findings. The zone arithmetic, gaps and marker changes are sound; the target cut-out boundary is not where the pane's drawn content is.

## Findings

### F1, medium: the target cut-out cuts the top off the 1.5x preview

`src/Allegiance.ClientCore.Game/Vr/VrHudZones.cs:147-161` starts the pane cut-out at `floor(TargetPaneLayout.TopY)`, the pane's name top. In a VR eye frame the model preview is drawn at 1.5x about the native square's bottom (`Rendering/HudRenderer.cs:556-557`, `Rendering/TargetModelPreview.cs:388-395`), so its top is `y0 - 153 s`, above `TopY = y0 - 143 s` by `10 s` px:

| Image | `TopY` | preview top | overhang |
|---|---|---|---|
| 1280x720 | 542 | 531 | 11 px |
| 1280x534 | 382 | 374 | 8 px |
| 1440x740 | 557 | 547 | 10 px |

Failure: with a target selected, those top rows are not in zone C. They fall in the "target notices" cut-out, which ends at `TopY` (`VrHudZones.cs:150-151`) and is shown in zone B at scale 1, while the rest of the preview is grown ~1.9x in C. The radius-1 framing puts the hull's max-radius vertex on the square's edge, so the clipped rows can be opaque: the model is sliced and a fragment floats at the bottom of the notices quad. The record's "from the pane's top" (`knowledge/vr-hud-design.md:617-620`) is not the top of what the pane draws.

Fix: split at `min(floor(TopY), TargetModelPreview.PaneRect(x0, y0, s, 1.5f).Top)` (or add the overhang to `TargetMarginPixels` and subtract it from the notices strip), and test that the target cut-out contains `TopY` and the 1.5x preview rectangle.

### F2, medium: without a target pane the notices are drawn inside the grown cut-out

The cut-outs assume the notices sit above the pane, but `HudRenderer` positions them from `TargetPaneVisible` (`Rendering/HudRenderer.cs:208-209, 383, 392, 516, 525`): without a target pane (no target, target HUD off, external-missile camera) they are drawn at the image foot (`h - 140`, `h - 164`, `h - 90`, `h - 190`), inside the new target cut-out (`TopY..h`), now grown ~1.9x. The leader/commandee lines are usually target-gated, but the mouse-stick indicator and mouse-option notice are not (`HudModel.cs:366-370`).

Failure: mouse flight with no target selected. The joystick circle (radius 38 at `h - 140`) and the sensitivity text appear about twice size inside zone C while the notices quad in B is empty. At 1280x720 the text (`h - 190`, y 530..544) straddles the split at 542, so one line is drawn in two zones at two scales; at 1280x534 the circle straddles `TopY` (356..432 against 382). The zone table's claim that B now holds these lines (`knowledge/vr-hud-design.md:578`) is only true with a target pane.

Fix: in a VR eye frame, position these notices at a fixed band above the pane whether or not a target pane is present (a VR-only context flag), or make the cut-outs depend on the same model flags. Record the no-target case.

### F3, low: the ordinary tips are cut out with the pane and now double

`Rendering/HudRenderer.cs:1287-1300` draws the ordinary tips centred at the foot, always inside the target cut-out. The note records that they are cut out with C (`knowledge/vr-hud-design.md:620`, known gap) but not that growth makes a tip about twice every other HUD line, inside the pane's quad. The zone table says B holds "critical and ordinary tips" (`:578`), contradicting the cut-out table. If the operator's "text log on top of the secondary target screen" meant these tips rather than the port lines, the change enlarges it rather than removing it.

Fix: end the cut-out above the tips' band and cut them separately, or accept and record the size change.

### F4, low (record): the systems cut-out row and comment name the wrong top

`knowledge/vr-hud-design.md:625` says systems runs "down to the target cut-out's top", and `VrHudZones.cs:177-178` says "Below the target cut-out's top it starts right of that cut-out", but `systemsSplit = paneTop - 2` (line 180) uses the *notices* top, now about 144 px above the target top. Behaviour is safe (x ranges do not overlap), but the record and comment describe the old geometry. The "Layer 4 as built" evidence still reports "the wrapped port lines in C" (`:705`), superseded by the fourth look.

## What the change gets right

- B at `-26..26` keeps its 2 degree gap to A and the 42 degree reach. Its height is unchanged; at 760 px high its scale is ~0.953, above the ~0.951 floor. I recomputed the 534..760 height range and the width edges from the pure formulas; they match the unchanged sweep and edge tests.
- C at `-42..-24` keeps 2 degree gaps to D and E. The grown quad is inside C by construction; pane scale is 1.90 to 2.0 at the tested sizes.
- In a VR eye frame the centre gauges, speed and own-ship status are not in the main HUD image (`HudRenderer.CentreGroupOnMainPass`), so they are not caught by the grown cut-out. Only the preview overhang, the no-target notices and the tips are unexpected content there.
- `ClassicLayout` now checks every bound before any overlap; only the reason order changes.
- `VrFlightPathVelocity`: the board check releases the gate before `TryOwnPose` takes it, so no nesting or ordering problem; a boarded gunner keeps the carrier's velocity; `--noPrediction`/no-hull fall back to the server pose; spectating is gated out by `_vrAimReticle`; the camera pose (`_poses[me]`, `Game1.FlightInput.cs:2748-2779`) uses the same carrier/prediction source, so marker and camera agree. No unit test covers it.
- Strokes match the described art (two 36 degree brackets on a 12 px circle, 4 to 2 px taper, mirrored, grey 123,125,123). 32 strokes per eye is 64 sprite draws plus one small list allocation per eye per frame, not a concern; the 1 px width floor holds.
- Publish guard `601a46ae HEAD` OK; data-boundary check OK; no machine paths, host names or private emails in the changed files.

## Tests and records

The new geometry test restates the shape specification and would catch the claimed constant mutants; it does not test drawing or absent art. The zone tests pin the split (`notices.Bottom == pane.Top`, `pane.Top <= TopY`) only against the cut-out geometry, not against what the pane draws; that is the gap F1 and F2 fall in. The fourth-look numbers (1.96 at 1280x720, the split) match the code.

## Verified by execution versus by reading

- Executed (read-only): `git write-tree`, `git status`, `git show`/`git diff`, `git grep`, `git ls-files --eol`, publish guard, data-boundary check.
- By reading: all cited source and test lines; geometry recomputed by mirroring the pure layout formulas, not by running the game.
- Limits: no `dotnet` build, no test run, no headset, no HUD image; `directionbmp` is not in the repository, so the art match is judged from code and note. The author's build, test, mutant and probe results are accepted as reported but not reproduced.

## Next action

Fix F1 and F2 (F3/F4 are record/boundary choices), add the containment test for the pane's drawn content, then re-review the delta. Both are visual regressions the fourth look would likely see, so they should land before the next headset look.