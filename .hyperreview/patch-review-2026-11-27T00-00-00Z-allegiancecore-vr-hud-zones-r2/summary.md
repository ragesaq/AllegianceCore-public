# Patch review - VR HUD layer 4 (Classic zones), round 2

Reviewed head `b4ba3d02e3f7cecca9f7eaf1ff2120778fc1712d`, tree
`d81443d5755f592c8d4becd75f1cab33a86001b9` (checked with `git write-tree`). Change `13614bfa..b4ba3d02`,
one commit, 8 files. The clone root below is `<review-clone>`; `git status --short` was empty before this
review's artifact directory was made.

Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high` (reviewer-parity
matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Cross-family
to the Anthropic/Claude author (Port).

**Verdict: WARN.** nextAction: fix N1 (or record it as a known gap) and add the acceptance test in N2,
then re-review the delta. F1 to F5 are resolved; the checks and the cache are sound.

## Round 1 findings

1. F1 (high): resolved. `ClassicLayout` refuses a zone whose scale would put the 14 px line under 0.6
   degrees (`Vr/VrHudZones.cs:236-274`). Re-implemented arithmetic: at 1280 by 740 the comms zone scales
   0.978 and is accepted, at 1280 by 761 it is 0.951 and refused, at 1280 by 768 it is 0.942 and refused.
   1600x900, 1920x1080 and 2560x1440 are refused for the floor. The whole-image fallback and its log are as
   claimed, and the note says the fallback's text is under the floor (`knowledge/vr-hud-design.md:683-687`).
2. F2 (medium): resolved. Cut-outs are checked for positive area and containment, then pairwise with
   `Rectangle.Intersects` (`VrHudZones.cs:251-276`). Touching edges do not intersect, so the two systems
   parts that meet in the image still join. 800x480 and 640x360 are refused, and 400x300 for a cut-out off
   the image.
3. F3 (medium): resolved. The four flags are bools and are false in ordinary flight; the game-state pane,
   the player pings and the sector map draw only from their own actions, and the investment pane draws full
   screen in flight (`Game1.Rendering.cs:1966`). I checked the other draw sites for in-flight full panels and
   found none missing: `_consolePanes.Nav` is `_minimapExpanded`, `_consolePanes.Team` is
   `_teamPaneVisible`, `_consolePanes.Inventory` is the in-place F4 pane, the teleport pane needs docked,
   and the profile editor needs pilot info. The declined pure-function test is sound: it would restate the
   same flag list, so it could not catch a missing flag.
4. F4 (low): resolved. The log names the zones, the modal panel or the fallback panel, with the panel's
   distance, pitch and width (`Game1.VrHud.cs:545-563`).
5. F5 (low, record): resolved. The note now says which sizes get zones and why, states that the fallback
   text is under the floor, and gives the mutant count (`knowledge/vr-hud-design.md:632-643`, `667-677`).
   N1 finds a different record claim that is false.

## Findings

### N1 (medium) - five readouts are in no cut-out at any size where the zones are used

The afterburner, power, weapon-mode, mining and time-dilation lines are drawn at the top right of the HUD
image. They are centred on `w/2 + 321` (`Rendering/HudRenderer.cs:683`) at y 16, 20, 58 and 82 (`:2011`,
`:2015`, `:2035`, `:2048`, `:2068`), so the text spans y 16 to about 96. The comms cut-out is the 480 px
strip `w/2 - 240 .. w/2 + 240` (`Vr/VrHudZones.cs:134`, `:177`); a label would have to be wider than 162 px
to reach it. The systems cut-outs start at y 120 (`VrHudZones.cs:181`, `:161-165`), chosen to include the
weapon status list at y 150 (`HudRenderer.cs:2224`) and to exclude the location block at y 30 to 106. No
cut-out therefore contains the group, and the zone pass draws only cut-outs: with zones, the five readouts
vanish. The whole-image fallback at the refused sizes still shows them, so the HUD is lossier at 1280 by 720
(and every accepted size) than at 1920 by 1080.

This predates the delta (`13614bfa`), and round 1 did not raise it. It matters now because this commit's
record asserts the opposite: the zone table gives E "afterburner and power, weapon mode, mining, time
dilation" (`knowledge/vr-hud-design.md:579`), the cut-out table says the systems cut-out holds them (`:623`),
the code comment says the text layout moves them below 126 px (`VrHudZones.cs:155-158`, which no draw does),
and the note says "At 1280 by 720 every zone holds its content" (`:629-630`). Rule 9 also expects a test to
fail when an element's rectangle leaves its zone (`:563-564`), but the tests compare cut-out rectangles only,
never the elements' drawn rectangles.

Failure: fly Classic in VR at 1280 by 720. AFTERBURNER/PWR, WEAPONS: MOUNT n, MINING and TIME DILATION are
absent. The flight block's gauges cover power and afterburner only as gauges; weapon mode, mining and time
dilation have no other display.

Fix: give the group its own row in the systems zone. Its reserved box is 320 px wide starting at `w/2 + 161`,
so the box overlaps the comms strip even though the text does not; either move the group right in a VR frame
so the box clears `w/2 + 240`, or clip the row to the measured text. Keep the row out of the location block
(`x >= w - 200`, y 30 to 106) and end it at y 120 where the systems rows start. Then fix the comment and the
two note rows, and add a test that the group's rectangle lies in a cut-out. If the group is accepted as
lost, say so in Known gaps instead.

### N2 (low, tests) - the sweep cannot fail on a size it refuses, and the note's accepted range is untested

`tests/Allegiance.ClientCore.MonoGame.Tests/VrHudZonesTests.cs:290` returns from the test when
`ClassicLayout` returns null, so a regression that refused more sizes would keep the sweep green. Only
1280x720 and 1280x740 are asserted accepted (`:233`, `:270`). The note's "passes from 1140 to 1440 px across
and from 534 to 740 px high" (`knowledge/vr-hud-design.md:641-643`) has no test. Fix: assert `NotNull` for
1140x534, 1366x720 and 1440x740 (the arithmetic says all three are accepted), or count accepted sizes in the
sweep and assert a minimum. The sweep's name also promises "no pixel is shown twice", but it compares source
rectangles, not placed ones; the placement is guaranteed by `Place`, so the name overstates the check.

## Verified

Executed (read-only): HEAD and tree match the frozen target; `git status --short` was empty before my
artifact directory; `scripts/publish-guard.sh 601a46ae HEAD` OK for the two commits; `tools/check_data_boundary.py`
OK; `git ls-files --eol` shows the changed index entries as LF; a diff search found no drive path, home path
or email in the added lines. I re-implemented the comms cut-out and zone-scale arithmetic in double precision
to check the floor boundary: 1280x740 is 0.978 and accepted, 1280x761 is 0.951 and refused, 1280x768 is 0.942
and refused, matching the tests and the note's "about 760 px high".

Read: the check order and `Rectangle.Contains`/`Intersects` touch semantics; `Place`, `Within` and the
offsets; the size-only cache across a change and change back, a skin switch, a modal opening and closing, and
the first frame (`VrZonesFit` runs before the draw, and a refusal clears the quads); the reason log once per
size; the fallback draw and the flight block (a VR HUD image has no centre group,
`HudRenderer.CentreGroupOnMainPass`, so the block is not duplicated); the four flags and every candidate full
pane; the records; the tests.

Limits: I could not run dotnet, the tests or the game. The build, 228 tests, 14 mutants and the 8 known
failures are the author's, not reproduced. N1 comes from the draw coordinates and the cut-out geometry, not
from a run. Element text widths are reasoned from the font sizes, not measured.