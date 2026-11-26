# Summary: cross-family patch review of PR #22 (VR HUD step 2), delta round 3

| Field | Value |
|---|---|
| Reviewer | `deepseek-v4.1-flash`, family DeepSeek, provider `opencode-go`, effort `high` |
| Route | explicit selection, the target's `AGENTS.md` "Cross-family review route"; reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Author family | Anthropic (Claude Code, Claude Opus 5.5), seat Port: cross-family |
| Reviewed head | `0314c5e40108cc3446db66306ed5ac7174d4ba02` |
| Reviewed tree | `bb3df527f2170ddc25368dbc20d38a68eb450cb3` |
| Range | `6737eaaf..0314c5e4`: `d04562fe` (records only) and `0314c5e4` (the code) |
| Verdict | **PASS**: no findings; three notes, none blocking |

The target did not move: `git write-tree` printed `bb3df527...`, `git rev-parse HEAD` printed `0314c5e4...`, and
`git status --short` was empty. The review was read-only in `<review-clone>`; only this summary was written.

## Claim 1: targeting markers are flat shapes in the world

`VrHudGeometry.MarkerBillboard` (`src/Allegiance.ClientCore.Game/Vr/VrHudGeometry.cs:187`) returns
`right = normalize(cross(forward, headUp))`, `up = cross(right, forward)`, each times `distance*tan(angle/2)`, with
`forward` the head-to-anchor direction. Checked against the canonical case (`forward = -Z`, `headUp = +Y`): the result
is `right = +X`, `up = +Y`, and `cross(right, up) = -forward`, so the frame is right-handed seen from the head and
`+x` is image-right. `VrMarkerPlane.TryPoint` (`Game1.VrHud.cs:582`) then maps `+x` right and `-Up*y` to image-down,
which matches the screen convention `TryProject` produces. The degenerate case (anchor along the head's up) is guarded
at `VrHudGeometry.cs:194` and the `UnitX`/`UnitZ` fallback keeps the same handedness, checked straight up and straight
down.

All four shapes keep their old proportions, compared against `6737eaaf:572-608`: selection corners at plane `+/-1` (an
axis-aligned square, as before), lock at `+/-1` with phase 0 (diamond), lead a 12-gon of radius 1, contact bracket
corners at `+/-1` with 0.4 arms. Size is now `|rightEdge - leftEdge|` instead of `PixelsForAngleAt(angle, ...)`; both
measure the image width of an angular width `angle`, so the on-screen size and the `stretch`/line-width formula are
unchanged. The label moved from `half + 2*line` pixels below the anchor to plane `y = 1 + 4*line/size`, which is the
same two line widths below the foot, and it now follows the projected centre column rather than the anchor's pixel
column.

The depth test and the anchor are unchanged (`Game1.VrHud.cs:386-388`); one depth per eye for the whole shape is now
correct rather than an approximation, because the shape is planar. Building the plane from the head (`headUp`,
`Game1.VrHud.cs:331`) and projecting the corners eye-relative is sound: adding translation-free world directions to an
eye-relative anchor survives the affine view, and both eyes receive the same world corners, which is the fix ragesaq
asked for.

## Claim 2: the flight-path marker art

Premultiplication is right: `IndicatorTextures` decodes through `ToPremultipliedRgba`
(`Rendering/IndicatorTextures.cs:124`), so the batch's `BlendState.AlphaBlend` is correct, and `Color.White` matches
the flat game's untinted `DrawCentered(marker, position, Color.White)` (`Rendering/HudRenderer.cs:2969`). Size is
`art.Width * WidthRadians/FlightBlockReferencePixels` = 3.2 degrees at the default 40 degrees over 400 px, which is
32/215 of the Classic ring: the same proportion the flat game draws. Layer depth `0f` matches that batch (depth test
off, `_vrMarkerDepth` zero there). The grey fallback and its colour match the operator's note.

## Claim 3: flat equivalence and the lifted port lines

Exact. With `FlightReferenceInWorld` false, `PortTextLift` (`VrHudGeometry.cs:166`) returns 0, so the weapons line is
at `h - 158` and cargo at `h - 132` as before (`Game1.FlightParts.cs:298-306`, `Game1.InFlightUtility.cs:169-170`). In
VR the upper line's top lands on `h/2` and cargo keeps the 26 px gap. `TargetPreviewEnlargement`
(`HudRenderer.cs:553`) is 1.5/1, and `PaneRect(..., 1)` returns the native rect by early return
(`Rendering/TargetModelPreview.cs:388-391`), so the flat rect is byte-identical. The field is set at the top of `Draw`
(`HudRenderer.cs:191`) before the early return, and both shipped call sites (`HudRenderer.cs:2740`,
`ModernHudRenderer.cs:359`) are inside `Draw`; `DrawFlightReference` draws no preview, so the VR flight-block pass
cannot read it stale. See note N2 for the harness path.

## Claim 4: records

The new section `knowledge/vr-hud-design.md:444-477` matches the code: 32 by 32, about 3.2 degrees across, 1.5 times
with the bottom edge kept, the two lines to the image's middle, the flat game unchanged. The "increased by 150%"
reading is recorded as an interpretation rather than presented as the operator's words, which is honest. `DAG.json`
node `ragesaq-010` `next` matches this state. `d04562fe` touches records only.

## Policy

`scripts/publish-guard.sh f362cdf HEAD` printed `OK (8 commit(s))` and `tools/check_data_boundary.py` printed `OK`.
No machine path, host name or non-noreply email appears in the changed files.

## Notes (no action required)

- **N1, test strength.** `tests/Allegiance.ClientCore.MonoGame.Tests/VrHudGeometryTests.cs:795` cannot fail on the
  property it names. It builds a fixed world corner from `MarkerBillboard`, projects it through both eyes and
  re-intersects the rays; any fixed world point triangulates to itself, so a plane that did not face the head (say
  `Right = UnitX*half`, `Up = UnitY*half`) would still pass. The orientation claim is actually carried by the test at
  `:764` (right exactly perpendicular to `headUp`, `up.headUp > 0.7`, handedness) plus the listed mutants, and no test
  reaches `DrawVrMarker`, so a regression to per-eye pixel offsets is caught only in the headset (disclosed by the
  author). A GPU-free improvement: assert that the corner's disparity equals the anchor's within epsilon, and that the
  old per-eye-offset corner does not.
- **N2, cached enlargement.** `_targetPreviewEnlargement` (`HudRenderer.cs:556`) is instance state set in `Draw` and
  read by the `protected virtual DrawTargetModelPreview`. The test harness exposes a direct call outside `Draw`
  (`tests/.../Hud21TargetModelPreviewTests.cs:444`, `PaintPreview`), so a harness or subclass painting a preview after
  a VR-model `Draw` silently gets 1.5 times. No shipped path is wrong. Passing the enlargement down the pane call would
  remove the trap.
- **N3, edge behind the eye.** `DrawVrMarker` now returns when either horizontal edge fails to project
  (`Game1.VrHud.cs:588-589`), where before only the anchor had to. This needs the plane's edge to cross the eye plane,
  which needs the anchor past about 80 degrees off that eye's forward for the capped 20 degree bracket; the radar
  gathers to 75 degrees off the head plus up to about 10 degrees of cant, so it is reachable in principle but far
  outside any eye image, so nothing visible is dropped. Recorded so a wider field or a bigger bracket does not lose
  markers silently.

## Verification

Executed, read-only: `git write-tree`, `rev-parse`, `status`, `log`, `show --stat`, `diff` (per file and per range),
`ls-files --eol`, `grep`/`rg`, `scripts/publish-guard.sh`, `tools/check_data_boundary.py`. Everything else is by
reading: I cannot run `dotnet`, the game or the headset, so the author's 0-warning build, 139/139 tests and ten
mutants are taken as reported and not re-run. `directionbmp` is loaded from the external artwork tree and is not in the
clone, so "32 by 32" and therefore "about 3.2 degrees" rest on the stated asset size. The enlarged preview and the
lifted lines are seen only in the author's 1280 by 720 panel dump and ragesaq's headset look; I verified no headset
appearance. The marker plane's visual result on the flat mirror (perspective on off-centre boxes) is by geometry only.

## Next action

No code change required. Post this as the round 3 PR comment, then ragesaq's headset look and approval at this head
`0314c5e4` (only then merge, per `AGENTS.md`).