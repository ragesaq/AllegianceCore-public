# Patch review - VR HUD layer 4 (Classic zones), round 3

Reviewed head `dafc2b38608cd9a0a05c3106e1df9b57adef1e60`, tree
`0cab8c5b351cf2e573859ee47098fa635756d66e` (`git write-tree`); change `b4ba3d02..dafc2b38`, one
commit, 7 files. Standalone clone with no remote, `<review-clone>`; `git status --short` was empty
before this round's artifact directory.

Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high` (reviewer-parity
matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`).
Cross-family to the Anthropic/Claude author (Port).

**Verdict: WARN.** nextAction: N1 is not fixed. Keep the five status lines in their slot (below), so
the new cut-out holds them, then correct the records and re-review the delta. N2 is resolved.

## Round 2 findings

### N1 (medium): not resolved - the cut-out is right, but the lines are not drawn there

Round 2 read the draw sites (`HudRenderer.cs:2004-2069`) and concluded the five lines sit at y 16 to
about 96. They do not. Before the draw, `PrepareLayout` reserves the cluster's 320 by 120 slot at
`HudRenderer.cs:777-779`. The lines go through `DrawText` -> `DrawMeasuredText` -> `HudTextLayout.Place`
(`HudRenderer.cs:867`, `1122-1146`), and `Place` rejects every candidate that meets a reserved
rectangle when `instrument` is false (`HudTextLayout.cs:43`; the call sites pass neither `instrument`
nor `fixedAnchor`). The engine therefore moves all five lines out of the slot, to y about 132 to 220.

Reproduced two ways. (1) The port's own capture `docs/reports/P1075_assets/desktop-afterburner-classic.png`
(1280 by 800) shows "AFTERBURNER" at y 134-141, "PWR 0%" at 148-155 and "WEAPONS: ALL" at 172-179,
x 920-1001; the Modern capture shows the same block at y 153-167. (2) Re-implementing `Place` with
the real reservations gives, at 1280 by 720: AB `(896,132)`, PWR `(930,146)`, WEAPONS `(898,170)`,
MINING `(940,194)`, TIME DILATION `(903,208)` - all 12 px lines.

Consequences for this commit:

- `StatusLines` (`VrHudZones.cs:194-195`, cut-out added at `:160`) covers the empty reserved slot
  (`x` cx+242..cx+400, y 12..120). Nothing is drawn there, so the new quad is blank.
- All five lines are inside the existing **systems** cut-out (`x >= cx+242`, `y >= 120`) at every
  accepted size, so they were already in a cut-out and shown in zone E. Round 2's "no cut-out holds
  them" and "VR did not show them" are contradicted by the capture. The new parenthetical at
  `knowledge/vr-hud-design.md:631`, the README entry and the DAG node repeat that claim, and the
  note's row for `status` (`:623`) and the test `The_status_cutout_holds_...` assert a position that
  is never drawn.
- The lines are not invisible today: with weapon systems mounted, PWR, WEAPONS, MINING and TIME
  DILATION are painted over by the weapon list's 0.55-alpha background drawn later
  (`HudRenderer.cs:16`, `:2222-2225`), and its rows overlap them; AB sits just above the panel.
- The commit still changes zone E for the worse: row 0 gains 158 px plus a 4 px gap, so at 1280 by
  720 the systems content's right edge moves from 33.9 to 41.2 degrees (the reach limit is 42), at
  1366 by 720 the pane is shrunk to 0.96 (it was 1), and the accepted width range narrows from
  1140..1440 to 1200..1372 - all for a blank panel.

Why the tests pass: `The_status_cutout_holds_...` (`VrHudZonesTests.cs:307-322`) checks a
hand-written rectangle (cx+321 +/- 65, y 16..96) against the cut-out source; the centre test
(`:299-305`) restates `TopStatusCenterX`'s formula instead of calling it. Both are nominal-coordinate
tests and stay green with the cluster displaced; the two new mutants are caught only by them.

Fix: draw the four sites as fixed anchors (`fixedAnchor: true`, the pattern the console text already
uses at `HudRenderer.cs:1196-1316`), so the lines stay at `TopStatusCenterX` and the weapon panel no
longer covers them. The new cut-out then holds them as written. Add one test that places the cluster
through `HudTextLayout` with the real reservations (or asserts the drawn bounds); it fails on this
head and passes after the fix.

### N2 (low, tests): resolved

`Of_the_sweep_the_zones_are_used_for_exactly_the_sizes_the_note_gives`
(`VrHudZonesTests.cs:281-296`) now fails on a size the code refuses, asserts 1200x740 and 1366x740
accepted, 1140x720 refused with `'status'`, and 1440x720 refused for the floor; the sweep's name no
longer overstates its check. I re-derived the accepted sweep set and both reasons from the code's own
arithmetic (float32, including `TargetPaneLayout.Scale`, `MinimapGeometry`, the cut-outs, the floor)
and got exactly the test's list. The 130 px text estimate is sound: DejaVu Sans at the NativeR8 bake
(Size 8, spacing 0) measures "AFTERBURNER ACTIVE" at 129.6 px with integer advances.

## New findings

1. **Low - the lower bound clips the weapon status list it claims to hold.** At 1200 px across
   `systemsLeft = cx + 242 = 842` (`VrHudZones.cs:161`), while `DrawWeaponStatusList` starts its panel
   at `w - 380 - 18 = 802` (`HudRenderer.cs:2222-2223`). The cut-out omits the panel's left 40 px and
   the first ~32 px of every row's text; at 1280 the two coincide exactly (882). This predates the
   delta, but the new test and record now certify 1200 as accepted while `knowledge/vr-hud-design.md:624`
   says the systems cut-out holds the weapon status list. Clamp `systemsLeft` to the panel's left edge
   (or refuse widths below 1280), and test the panel rectangle against the cut-out at each accepted size.
2. **Low - the join test never reaches the new tight case.** `The_two_systems_parts_join_...`
   (`VrHudZonesTests.cs:192-204`) runs only over `ImageSizes` (1280x720, 1920x1080, 1600x900,
   2560x1440). The delta makes row 0's height `max(status 108, systems height)`; at the lowest
   accepted height (534) the margin is 8 px, so a taller status cut-out would break the join with
   every test green. Add 1200x534 (it passes today) to pin it.

Record note: the note says the zones fail "at 480 px high or less" (`knowledge/vr-hud-design.md:645-647`),
but 481..529 also fail (the comms cut-out meets the target); the lowest accepted height is 530. The
wording is scoped by "of the sizes tried", so this is an imprecision, not a false claim.

## Verified

Executed (read-only): HEAD and tree match the frozen target; `git status --short` empty before the
artifact directory; `scripts/publish-guard.sh 601a46ae HEAD` OK; `tools/check_data_boundary.py` OK;
changed index entries are LF; the round 2 summary's sha256 is `02e10231...`, matching its receipt, so
"committed unchanged" holds; no drive path, home path or email in the added lines; the two P1075
captures were measured pixel-wise; the zone arithmetic, the accepted/refused sets and the cluster's
post-layout boxes were re-implemented from the code in float32.

Read: the draw path (`ReserveConsoleAnchors`, `DrawAfterburner`/`DrawWeaponMode`/`DrawMining`/
`DrawTimeDilation`, `DrawMeasuredText`, `HudTextLayout.Place`), the cut-outs and `Place`, the floor,
the weapon panel, and the note/README/DAG text.

Limits: I could not run dotnet, the tests or the game. The build, 233 tests, 16 mutants and the 8
known failures are the author's. The cluster's displacement is proved from the placement code plus
the port's committed captures, not from a run of this head; the captured y depends on the 12 px
measured line, which the captures confirm. The author also did not run the game in VR after the fix.
