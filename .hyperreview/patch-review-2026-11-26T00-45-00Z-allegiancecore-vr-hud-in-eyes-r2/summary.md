# Patch review (delta round 2): VR HUD drawn into the eye images (AllegianceCore PR #15, step 1)

- **Reviewed head:** `d1b87f94741d4d784f3fb00c7ab66fd270886308`
- **Reviewed tree:** `f9b6dea5d796547a7b01f3d5563f2231a721ea14` (`git write-tree` matched; `git status --short` was clean at review start)
- **Range:** `1cf71725..d1b87f94`. Round 1 reviewed `3a3b6e1b..1cf71725`.
- **Reviewer:** DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, effort `high` (reviewer-parity
  matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; standing at
  high; explicit selection, the route the target's `AGENTS.md` "Cross-family review route" declares).
- **Authoring family:** Anthropic (Claude Code, Claude Opus 5.5, agent seat Port). Cross-family review.
- **Verdict:** PASS
- **nextAction:** No blocking action. Optional polish: narrow the new doc comment's 1.4x claim to the
  sideways case, and name the vertical sizing convention in the note. The step-1 loss of the VR radar
  contact list is recorded and is the operator's product call.

## Round 1 dispositions

1. **F1 (High) - closed as a documented limitation.** The code still clears `_drawRadarContacts`
   and `_drawShipTags` after the gather (`Game1.VrHud.cs:110-111`), and the HUD model is built later
   from that list, so no skin's panel radar contacts draw. The author's reason holds against the code:
   Classic and Modern have no radar scope at all; their contacts are screen-space brackets and labels
   drawn at the flat camera's projected pixels by the shared `RadarBracketRenderer`
   (`RadarBracketRenderer.cs:296-303`, installed once for every skin at `Game1.GpuBudget.cs:43`), which
   would duplicate the world markers. No other contact path uses a different list: the shared labels and
   brackets, the off-screen edge pips (`RadarBracketRenderer.Offscreen`) and Jet's scope all read
   `model.Context.Contacts`, so all go empty, and the scope is the only display that is not a screen
   position. `knowledge/vr-hud-design.md:239-244` now states exactly this, and the empty Jet scope is
   listed as a step-1 limit. The functional loss (no behind-you radar in VR) is real but recorded;
   accepting it is a product decision, not a code defect.
2. **F2 (Low) - closed.** `Game1.VrHud.cs:184` and `:221` clamp after scaling with
   `MathF.Min(..., VrHudGeometry.BracketCapRadians)`. A 20 degree contact bracket now yields exactly 20
   degrees, and the 2 degree floor scaled by 1.15/1.3 stays above the floor.
3. **F3 (Low) - closed.** `VrHudGeometry.PixelsForAngleAt` (`VrHudGeometry.cs:139-152`) reconstructs the
   view ray through the marker's own pixel with the off-centre terms, then measures the great-circle span
   in the vertical plane through that ray; this is the exact inverse of `TryProject` for the eye's
   `CreatePerspectiveOffCenter`-form matrix (`VrEyeCamera.cs:47-56`). I reproduced the maths
   independently (reimplementation, not the product code): 0.9997x the centre size at the centre, 1.305x
   at 40 azimuth, 2.00x at 45 elevation, 1.72x at (-30, 35); the new test's cast-back covered angle is
   2.000, 2.000 and 2.0006 degrees for its three cases against a 2 percent tolerance, so it has margin
   and its mutation claim holds (the centre value gives a 23 percent error at 40 azimuth). The fallback
   when a rotated ray crosses the eye plane returns the centre size; at a 100 degree per-eye field it does
   not trigger even for the 20 degree cap.
4. **F4 (Low) - closed.** `ReleaseVrHud` (`Game1.VrHud.cs:440-448`) disposes and nulls the HUD image, the
   panel effect and the pixel texture. Both disposed members are recreated with `??=` at the top of
   `ComposeVrHud` (`:257-258`) before any reader runs, and their only readers (`DrawVrPanel`, `VrLine`)
   are called from `ComposeVrHud` after that point. `ReleaseVrEyeTargets` runs at the start of Draw when
   the sink is not attached and from `DisposeVrSink`, never mid-draw. No null-dereference path found.
5. **F5 (Low) - closed.** `Game1.VrHud.cs:92-94` saves `_radarProofLine` around the wide-frame
   `DrawRadar` and restores it. The game-camera draw runs first (`Game1.Rendering.cs:1767`) and
   `GatherVrHudMarkers` (`:1768`) does the wide run, so the proof keeps the game frame. `DrawRadar`
   clears both contact lists at its start (`:3976-3977`) and writes only `_radarProofLine` under the flag
   (`:4652-4675`), so nothing else leaks.

The added point-behind-the-eye test covers both guards: a point with `w <= 0` and a direction with
`w == 0` are both rejected.

## Findings

None. The delta answers the round 1 findings, and I found no new defect that fails the change's stated
behaviour or breaks the flat path.

## Observations

- **O1 (square bracket, directional stretch).** A deliberately square bracket is sized from the vertical
  span, so it covers the requested angle up-down but not left-right. At a pixel 45 degrees to the side the
  horizontal pixel scale is about 1.41 times the vertical one, so the bracket is about 29 percent narrow
  horizontally (it can sit inside a large contact's silhouette at the far edge); at 45 degrees of
  elevation it is about 41 percent wide. Acceptable for step 1: the marker is a framing aid, the error
  peaks in the periphery, the bracket already carries a 5 percent margin, and the choice matches the
  vertical convention of the text and line floors. If exact framing is wanted later, size by the larger
  of the two spans. The test pins only the vertical direction, which is why this residual is untested.
- **O2 (doc comment scope).** `VrHudGeometry.cs:133-138` says a marker at 45 degrees "needs about 1.4
  times the pixels". That is the sideways (tangential) factor; at 45 degrees of elevation the method
  returns 2.0 times (`sec^2`). The comment should say "about 1.4 times sideways, twice upward" or similar.
- **O3 (note wording).** `knowledge/vr-hud-design.md:241-242` says "the Classic and Modern radars draw them
  as screen-space brackets". The bracket pass is shared by every skin, Jet included; only the scope is
  Jet-specific. The conclusion is unaffected.
- **O4 (note wording).** The note's "drawn 1.15 and 1.3 times the contact bracket so they nest outside
  it, within the same cap" is loose at the cap, where all three brackets are 20 degrees and do not nest.
- **State after `ReleaseVrHud`:** the next `ComposeVrHud` recreates both GPU objects before use, and a
  flat frame cannot reach `VrLine`. Repeated release in flat frames is a no-op after the first.

## Verification

By execution, read-only on the frozen target: `git rev-parse HEAD`, `git write-tree` (expected tree),
`git status --short` (clean at review start; only this r2 artifact directory is untracked afterwards), `git show --stat`, `git diff` numstat against
`git diff --ignore-all-space` numstat (identical), `git ls-files --eol` on every touched file (index LF
throughout; CRLF only in the host worktree for the three C# files), `scripts/publish-guard.sh 3a3b6e1b
HEAD` (OK, 3 commits), `tools/check_data_boundary.py` (OK), a scan of the added lines for machine paths
and email addresses (none), the committed round 1 summary's sha256 against the receipt's claim
(`88ffe149f88be4d2f9a5d345f5cca3ede33fc9baa3d00d60078d0d6a42c18530`, match, no BOM), and an independent
Python reproduction of the new geometry and of the new test's margin.

By reading only: the radar skins and the shared bracket pass, the frame order around the radar gather,
the disposal and lazy-recreation paths, the label/line scaling, the records, and the tests.

Limits: I could not run dotnet, the build, the tests, the game, a replay or a headset. The reported 0
warnings, 78 of 78 VR tests, the mutation result and the stereo smoke run are taken as reported, not
re-observed. My geometry check is a reimplementation, not an execution of the product code. The round 1
summary's byte-identity to the reviewer's original cannot be checked here; only its hash against the
receipt's claim. Per instructions I did not run `tools/recall.py` or `tools/check_map.py`.
