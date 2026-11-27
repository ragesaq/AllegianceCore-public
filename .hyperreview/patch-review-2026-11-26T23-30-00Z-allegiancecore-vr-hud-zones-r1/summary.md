# Patch review - VR HUD layer 4 (Classic zones), round 1

Reviewed head `13614bfa93d57a4c8021784aa0391af9aefcc598`, tree
`c89b94f7b8a1c4a10abd397edcbffa1d8769f51b` (checked with `git write-tree`). Change `601a46ae..13614bfa`,
one commit, 10 files. The clone root below is `<review-clone>`.

Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high` (reviewer-parity
matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Cross-family
to the Anthropic/Claude author (Port).

**Verdict: WARN.** nextAction: fix F1-F3, extend the size matrix and add a testable modal predicate,
then re-review the delta. The default 1280x720 image is sound; the defects are in the size-dependent
layout and in the modal list.

## Findings

### F1 (high) - the fit scale drops HUD text below the accepted 0.6 degree floor above 720 px

`src/Allegiance.ClientCore.Game/Vr/VrHudZones.cs:80` fixes `DegreesPerPixel` at 57.62/1280 for every
image, while the Classic cut-out heights come from viewport-dependent anchors
(`VrHudZones.cs:131-164`); `Place` then shrinks a zone's whole content to fit
(`VrHudZones.cs:210-212`). Classic text does not scale with the window (`HudRenderer.TextScale => 1`,
`src/Allegiance.ClientCore.Game/Rendering/HudRenderer.cs:647`), so the zone scale is its final angular
size. Modelled from the code:

| image | comms | systems | 14 px line, comms / systems |
|---|---|---|---|
| 1600x900 (tested) | 0.814 | 0.766 | 0.51 / 0.48 degrees |
| 1920x1080 (tested) | 0.685 | 0.570 | 0.43 / 0.36 degrees |
| 2560x1440 (tested) | 0.520 | 0.377 | 0.33 / 0.24 degrees |

The target zone also shrinks (0.861 / 0.717 / 0.542) and carries the fixed-size port lines with it.
This contradicts rule 6: "a zone that cannot fit its text at that size wraps or drops it, never
shrinks it" (`knowledge/vr-hud-design.md:558-559`).

Failure: start with `--window 1920x1080` (`src/Allegiance.ClientCore.Game/Game1.Session.cs:101-105`;
the window is also resizable, `src/Allegiance.ClientCore.Game/Game1.cs:1905`), Classic skin, in VR.
Chat and ship-system text render at 0.36-0.45 degrees and the `[vr] HUD zone` warning fires on every
size change. The tests miss it: the only unscaled assertion is at 1280x720
(`tests/Allegiance.ClientCore.MonoGame.Tests/VrHudZonesTests.cs:144-149`); the other matrix sizes
(`VrHudZonesTests.cs:103`) are checked for containment, not scale.

Fix: make the mapping follow the image (`panelDegrees / imageWidth`), or render into a fixed 1280x720
layout and sample that. Per rule 6, wrap or drop rather than shrink. Assert in the test that each
matrix size keeps the 14 px line at 0.6 degrees or more; it fails today at 1600x900.

### F2 (medium) - cut-out source rectangles overlap at window sizes the game accepts

`VrHudZones.cs:131-164` mixes fixed pixel anchors (240 px comms half-strip, 330 px order block,
200 px location box) with width-dependent ones (minimap, target pane). Modelled over the code: below
1140 px of width comms overlaps orders/badges; below 880 px comms also overlaps location; at 1280 px
of width overlaps begin below 534 px of height, and below 498 px port lines overlap orders/badges; at
640x360 the systems rectangle becomes `78 x -58`.

At `--window 800x480` (a size this repository's proof tools use, `tools/p1006-ui-proof/Program.cs:44`)
the comms cut-out (x 160-640, y 0-214) contains the top of the target cut-out (y 184-480), including the
mouse-stick notice near y 189, and the right end of the first port-text line (text starts at y 186,
x 20-236). Those pixels are drawn in zone B and again in zone C, and the target cut-out shows the
notice clipped at its top. At 1024x768 the only overlap band (x 272-330) is empty unless the order
line exceeds 272 px. The tests use only 1280x720, 1600x900, 1920x1080 and 2560x1440, and
`ClassicCutouts` never validates its rectangles.

Fix: derive each cut-out from the element's drawn rectangle at that size and clamp it to the image, or
letterbox at a fixed layout size. At minimum reject intersecting rectangles at layout time and fall
back to the whole panel with a log. Add 800x480, 1024x768 and 640x360 to the matrix.

### F3 (medium) - `VrHudModalOpen` misses in-flight full panels

`src/Allegiance.ClientCore.Game/Game1.VrHud.cs:570-575` omits three full-screen states drawn while
flying: `_consolePanes.GameState` (F7 or `/`, `Rendering/InputActionCatalog.cs:303`) and
`_playerPingsVisible`, both of which draw a centred `TeamPaneFrame` up to 98.5% by 94% of the image
(`Game1.ConsoleModes.cs:263-296`, `Rendering/Screens/PanelScreenFrames.cs:387-427`, called from
`Game1.Rendering.cs:2032`); and `_spectatorMapOpen`, a centred 60% by 62% sector map
(`Game1.SpectatorSectors.cs:77-101`, called from `Game1.Rendering.cs:2212`).

Failure: in VR in flight, press F7 or open player pings. `zoned` stays true, so the panel is cut into
zones B/C/D/E; its middle band (for example y 310-389 at 1280x720) is in no cut-out and is not drawn,
and the rest appears as unrelated fragments. Before this PR the screen was whole on one panel. The
docstring at `Game1.VrHud.cs:566-569` claims "the other full panes", so the omission contradicts the
stated contract as well.

Fix: add the three flags, or one shared full-screen-UI predicate, and extract that predicate from
`Game1` into a pure function so a test can enumerate the states instead of relying on inspection.

### F4 (low) - the placement log reports the unused fallback panel

`Game1.VrHud.cs:547-554` logs `VrPanel` (distance, pitch, width, projected centre) on the first stereo
frame, but with zones or a modal that is not the drawn surface. Log the active branch; the cut-out
count is already logged at `Game1.VrHud.cs:596-599`.

### F5 (low, record) - the record does not disclose F1

`knowledge/vr-hud-design.md:626-631` records the 0.63 degree line and the uniform downscale but not
that at 1600x900 and above comms, target and systems text falls below the accepted floor; Known gaps
mentions only the keys hint. Add the size-dependent consequence and the remedy. Also
`knowledge/vr-hud-design.md:656` says 10 of 11 mutants are caught while the execution handoff says 11
of 11; state which is right, since one mutant survives by design.

## Verified by execution and by reading

Executed (read-only): `git write-tree` matches the required tree and HEAD is the required commit;
`scripts/publish-guard.sh 601a46ae HEAD` reports OK for the one commit; `tools/check_data_boundary.py`
reports OK; `git ls-files --eol` shows LF blobs. I re-implemented the cut-out and `Place` math in Python
from the source and scanned 20 window sizes plus the width and height boundaries; the F1 table and F2
thresholds come from that model. No added line contains a drive path, home path or email address.

Read: the quad math is correct - `right` and `up` are orthogonal to `forward`, the top edge is level,
`up` has positive Y for normal elevations, the tangent half-sizing gives the rect half-angles at the
edge midpoints, corner order and UVs match XNA's top-left origin, and the azimuth and elevation signs
match the seat axes used by the flight block. Rows, alignment, gaps and the systems join are as
described, and the two systems parts meet exactly. The flat game is preserved: every changed
`HudRenderer` branch is gated on `FlightReferenceInWorld`, which is only `_vrEyesDrawn`
(`Game1.Rendering.cs:3351`; flat frames clear it, `Game1.Vr.cs:56`), and the port-text changes sit in
the VR-only branch. The flight block uses its own image, not the cut-up HUD image
(`Game1.VrHud.cs:164-179`), so the ring is not duplicated at 1280x720. The size-only cache is sound,
and the block is skipped on modal frames and redrawn on close.

## Limits

I could not run dotnet, the tests or the game; build, test, mutation and smoke-run claims are the
author's, not reproduced. The smoke run had zone B outside its view, so comms has no visual
confirmation. The model uses double precision; C# float rounding cannot change the results above.
Target-pane text widths were reasoned from the font tables, not measured, so the 300 s wide cut-out
is tight but not raised. F1 and F2 apply to any non-720p back buffer.
