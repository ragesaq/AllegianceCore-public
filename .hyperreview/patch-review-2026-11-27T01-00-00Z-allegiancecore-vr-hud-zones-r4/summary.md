# Patch review — PR #23 VR HUD layer 4 (the zones), round 4 (delta)

Reviewed head `dadc03010c37ab6f5e87cfba37104ec5a0cc38a7`, tree
`3e978b958c81ecda91337073798d803dfcf4f81e` (`git write-tree`); change `dafc2b38..dadc0301`,
one commit, 7 files. Standalone clone with no remote at `<review-clone>`; `git status --short`
showed only this round's untracked artifact directory.

Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high`
(reviewer-parity matrix 4.14.0, sha256 `74618f22...b63267`). Authoring family Anthropic/Claude
(Port). Cross-family.

**Verdict: PASS-WITH-CONDITIONS.** Round 3's findings are resolved, and the code change is right
for its purpose. Four low items remain: the weapon-list claim and test overstate containment
(vertical), and three are record/test precision. Nothing blocks the headset look.

nextAction: correct the "keeps the list whole" wording or extend that test to the panel's full
rectangle; fix "each pinned by a test"; optionally drive the layout test from `PrepareLayout`.
No further code change is needed for the delta's own purpose.

## Round 3 dispositions

1. **Round 2's N1 (the blank status cut-out): resolved.** `StatusLines` and its constants are
   gone (`VrHudZones.cs:155-161`), no reference to them or to a "status" cut-out remains in
   `src/` or `tests/`, the `byName["status"]` assert is removed, and the systems comment now says
   the text layout keeps a 320 by 120 box and moves the five lines to about y 130 to 220. The
   records (`knowledge/vr-hud-design.md:628-635`, `.hyperreview/README.md`, `DAG.json`) say the
   same and record the withdrawal.
2. **Round 3's low 1 (the left edge clipped the weapon list): resolved.** `systemsLeft` is
   `Math.Min(cx + 242, WeaponListLeft(w))` (`VrHudZones.cs:162`, `:185`); `WeaponListLeft` matches
   `DrawWeaponStatusList`'s `w - 380 - 18` for the Classic skin (TextScale 1; `0.34w` exceeds 380
   at every accepted width). Reproduced: 1274 refuses for `'comms' and 'systems'`, 1275 and 1280
   accept with the cut-out's left edge equal to the panel's.
3. **Round 3's low 2 (the join test at no small height): resolved.** `The_two_systems_parts_join_
   ...` now also runs at 1280 by 534 (`VrHudZonesTests.cs:191-204`); the parts meet at
   `paneTop - 2` with equal right edges.
4. **Round 3's record note (the lowest accepted height): resolved.** The note now gives 534 to
   760 high (`knowledge/vr-hud-design.md:646-650`) and 1280 by 533 is refused, 534 accepted. I
   re-derived it: at 533 the `comms` cut-out's bottom (236) meets `systems low`'s top (235); at
   534 they are adjacent. The old "530" was wrong because it ignored `systems low`.
5. **Round 3's noted item (the weapon panel drawn over the moved lines): recorded, not fixed.**
   The known-gaps bullet at `knowledge/vr-hud-design.md:702-704` says so and that the systems
   cut-out shows it as it is. Declining fixed anchors for the flat game is acceptable; the gap is
   visible and recorded.

## New findings

### 1. Low: the weapon-list test and records claim more than they check (vertical extent)

`VrHudZonesTests.cs:343-344` asserts only `systems.Left <= WeaponListLeft(width)`; it never places
the list's rectangle. `knowledge/vr-hud-design.md:623` and `:683`, and the DAG node, say the
systems cut-out "keeps the weapon status list whole". The panel starts at y 150 and each row is
`LineSpacing + 5` = 17 px (`HudRenderer.cs:2217-2225`; the measured line that puts the five boxes at y 132..220 is 12 px (round 3 read it from the capture), so 17). The joined systems parts end at `minimap.Top - 4`: 486 at 1280x720
and 355 at 1280x534. Failure: a loadout with 7 or more mounted weapon systems at 1280x720 (or 4
or more at 1280x534) puts the panel's lower rows outside both parts, so those systems are not
shown in zone E (8 rows reach y 575 at 720). The cut-out bottoms are unchanged by this delta, so
this is a claim/test gap, not a regression. Fix: assert the panel rectangle against the union of
`systems` and `systems low`, or state the row limit where the table says "the weapon status
list".

### 2. Low: "each pinned by a test" is not true for the upper width edge

`.hyperreview/README.md:29` says the range's edges (534-760 high, 1275 across up to 1490-1550)
are each pinned. Heights and the 1275 edge are pinned (`VrHudZonesTests.cs:293-298`). The upper
width is not: the sweep's only wide refusal is 1600 (`:350`), while the real edge is about 1536 at
720 high and 1552 at 760 high (I reproduced both). A regression moving the edge to 1590 would
still pass. Fix: pin a pair near 1534/1536 at 720, or drop "each pinned".

### 3. Low: the layout test copies two reservations instead of driving `PrepareLayout`

`VrHudZonesTests.cs:311-323` reserves the chat pane and the 320 by 120 box by hand and hard-codes
the five measured widths and 12 px heights. That reproduces the real frame closely: the order is
the draw order, Classic's `TextScale` is 1 (only `ModernHudRenderer` overrides it), and the
committed capture places the lines within 2 px of the test's boxes; at 1280x534 the last box is
208..220, inside 120..236. But the other `PrepareLayout` reservations (target pane, minimap,
gauges, command summary, sector/order anchors) are not modelled, and a radar label placed earlier
in the band could push a line into `systems low`. Fix (optional): extract a prepare-layout seam
that the test can call, or add the static reservations at the tested sizes.

### 4. Note: no test pins the status cut-out's withdrawal

Re-adding a 158 px status cut-out would still pass the sweep and the "unscaled at 1280 by 720"
test (row 0's content would be 560 px, scale 1). An `Assert.DoesNotContain("status", byName.Keys)`
would pin the removal. No action needed if the removal is considered a one-off.

## Verified by execution

- `git write-tree` = `3e978b95...`, HEAD `dadc0301`; status clean apart from this artifact
  directory. `scripts/publish-guard.sh 601a46ae HEAD` OK (4 commits); `tools/check_data_boundary.py`
  OK; `git diff --check` clean; changed index entries are LF; no drive path, home path or
  non-agent email in the added lines.
- The round 3 summary's sha256 is `ce226e7d...`, matching its receipt, so "committed unchanged"
  holds.
- Re-implemented the cut-outs, `Place`, the zone scales and the floor in float32 from the code:
  the accepted sweep is exactly the 12 sizes asserted, with 1274/1275, 533/534 and 760/761 as
  claimed, and the width limits 1486 at 534 high and about 1552 at 760.
- Re-implemented `HudTextLayout.Place` with the test's reservations: boxes `(896,132)`,
  `(931,146)`, `(904,170)`, `(937,194)`, `(896,208)`, all inside the systems cut-out at 1280x720,
  1280x534, 1275x720 and 1440x740.
- Computed the weapon panel's bounds against the joined systems parts (finding 1), and the F4
  inventory rectangle: it is drawn inside the HUD image (not on the modal list) and is covered by
  the joined systems parts at the accepted sizes.

## Verified by reading

`HudTextLayout.Place`, `DrawMeasuredText`, `PrepareLayout`/`ReserveConsoleAnchors`,
`DrawWeaponStatusList`, `TopStatusCenterX`, `ChatPaneLayout`, `TargetPaneLayout`,
`MinimapGeometry`, `InventoryPaneLayout`, the zone table and `ClassicLayout`, and the
README/note/DAG/receipt text.

## Limits

No dotnet, build, tests or game: the author's 237 of 237, 16 mutants and 8 known failures are
theirs, and I could not run a hull to count `WeaponSystems` rows. The placement result is a
re-implementation, corroborated by the port's committed capture; the measured widths and 12 px
line height are taken from that capture, not measured from the font here.