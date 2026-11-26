# Patch review: VR HUD step 2, PR #22, round 2 (delta `2e0a4314..6737eaaf`)

- Target: `ragesaq/AllegianceCore-public` PR #22, branch `port/vr-hud-step2`, base `master` at `f362cdf2`.
  Standalone review clone, no remote, branches or tags.
- Reviewed HEAD `6737eaaf69df1f644e85b1e63f14a106a1d6deb4`, tree `93d8f6be043d50fd30b1844239f1d07c6c8b09a1`.
- Reviewer: DeepSeek, `deepseek-v4.1-flash` via OpenCode Go, `high` (matrix 4.14.0, sha256
  `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; route from `AGENTS.md`,
  "Cross-family review route"). Cross-family to Port (Claude Code, Claude Opus 5.5).

**Verdict: PASS.** Round 1's four findings are closed and the F2 decline is sound; the ROLL gate is correct in
VR and unchanged in the flat game. One cosmetic note-line issue is the only new item.

**nextAction:** ragesaq's headset test and the merge decision. The wrap fix below is optional.

## Round 1 findings, status

- **F1 closed.** `Vr/VrHudGeometry.cs:68-76` now says the width angle covers `FlightBlockReferencePixels` (400 px)
  and that `CentreGroupBox` is wider. `FlightBlockQuad` (:141-153) scales metres per pixel from that 400-px
  reference, so the comment matches the code; 215 px at 0.1 degrees per pixel is the 21.5 degrees claimed.
- **F2 closed for the record; the decline is sound.** The note (:370-373) and the test name
  (`VrHudGeometryTests.cs:624`) say "the same direction: no vergence disparity". Production already passes each
  eye's own projection (`Game1.VrHud.cs:346`, the panel at `:397`), and that predates this pull request, so the
  record no longer overclaims. The declined per-eye test is new coverage, not a repair; the same gap already
  existed for the panel.
- **F3 closed.** The decision row (:195) drops "keeps speed and throttle". The Jet element table at :158 still
  maps the tapes to layer 3, but the note marks that whole approach superseded in place (:171-172, "the VR HUD's
  flight block is Classic's ring gauge, not Jet's tapes"), so the row is not stale in context.
- **F4 closed.** The note (:434-437) names the debris-field and sound-listener row-basis sites; both exist as
  described (`NativeScene.cs:53-58`, `SoundManager.cs:1324-1329`, whose comment says the rows hold the basis).
  Their code is unchanged in this delta, as stated.
- **ROLL dial observation addressed.** `HudRenderer.RollOnMainPass` (`HudRenderer.cs:546`) is
  `!FlightReferenceInWorld`; the call site (`:483`) is gated. Flat equivalence holds by construction: the only
  production writer of that flag is `Game1.Rendering.cs:3341` (`_vrEyesDrawn`), which `BeginVrFrame` clears each
  frame and only an eye pass sets; a dropped VR frame stays flat and keeps the dial. Modern draws via `base.Draw`
  (`ModernHudRenderer.cs:111-123`) and has no roll of its own, so the gate covers both skins.

## New finding

### N1 (Low, cosmetic) Two note lines exceed the paragraph's wrap

`knowledge/vr-hud-design.md:372-373` are 122 and 138 columns where the paragraph wraps near 116, and "It is fixed
to the ship..." now sits mid-line. No functional effect; it makes later diffs noisier and breaks the file's wrap
convention. Fix: rewrap the sentence. Not a merge condition.

## Observations

- `DAG.json:179` says "four low record findings, all fixed". That is true of the record findings, but F2's
  suggested test was declined; the same sentence links `.hyperreview/README.md`, which records the decline, so a
  careful reader is not misled. Optional: add "one test suggestion declined".
- `knowledge/vr-hud-design.md:239` still calls the aim reticle "the same pixel in both eyes", the imprecision F2
  corrected for the block; the reticle is also drawn at infinity with each eye's projection. Pre-existing and
  outside F2's named scope; an optional one-word alignment.
- The new test (`VrCentreGroupTests.cs:25-30`) asserts the predicate only: deleting the call-site guard at
  `HudRenderer.cs:483` would not fail it, while making `RollOnMainPass` always true would. README item 5 is
  accurate for the mutant the author describes, but the call site is not pinned, as with round 1's note on
  `VrCentreGroupTests`.
- Hiding the ROLL dial removes the VR HUD's only numeric bank readout. Roll has no native instrument
  (`HudRenderer.cs:2116-2124`), the 3D scene shows attitude directly, and the flat skins keep the dial, so this
  follows ragesaq's 2026-11-26 decision; the headset pass should still confirm bank stays readable without it.
- No other horizon-like element of Classic or Modern was found on the VR panel in the paths I read: the
  `HudRenderer.Draw` element list, the Modern overrides and the existing Jet gates. That is "not found where I
  looked", not proof over the whole renderer.

## Records, policy and verification

- The committed round 1 summary is the reviewer's text, unchanged: its sha256 equals the receipt's value, and it
  is byte-identical (`cmp`) to the retained round 1 reviewer artifact, so this holds beyond the author-written
  receipt. The receipt's launch fields match the retained launcher log: route `codex:5aa43f19...`, binding
  `exact`, route lock on, `opencode-go`/`deepseek-v4.1-flash`/`high`, rc 0, and the stated times.
- No machine path, host name or private email in the files this delta adds or changes; the publish guard and
  data-boundary check pass. New text files are UTF-8 without BOM and LF.
- Executed: `git write-tree` prints `93d8f6be...`; HEAD and tree match the brief; status clean;
  `publish-guard.sh f362cdf HEAD` passes (6 commits); `check_data_boundary.py` passes; `git diff --check` clean;
  the hash, `cmp`, launcher-log and BOM/line-ending checks above. Read: code, tests, note, README, DAG and round
  1 records, against each other. Not run: no dotnet, build, test, deploy or install, so the author's 95-of-95 and
  mutant claims are not reproduced.

## Verification limits

Flat-frame equivalence and VR suppression are established by construction and reading, not by running the game.
Headset behaviour, bank readability without the ROLL dial, and the Modern/Jet VR panels remain residual risk for
ragesaq's headset pass and the merge decision.
