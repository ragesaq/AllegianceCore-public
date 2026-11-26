# Summary: cross-family patch review of PR #22 (VR HUD step 2), delta round 4

| Field | Value |
|---|---|
| Reviewer | `deepseek-v4.1-flash`, family DeepSeek, provider `opencode-go`, effort `high` |
| Route | explicit selection, the target's `AGENTS.md` "Cross-family review route"; reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Author family | Anthropic (Claude Code, Claude Opus 5.5), seat Port: cross-family |
| Reviewed head | `d0e8b1cf7df78a0122339be9ecdb6284b4e2a0ea` |
| Reviewed tree | `b5ac6525c4473684c4480c0c61a40749f1b085d9` |
| Range | `829025d6..d0e8b1cf`: `829025d6` (round 3 records only) and `d0e8b1cf` (the code and records) |
| Verdict | **PASS-WITH-CONDITIONS**: three low findings, none blocking the headset look |

The target did not move: `git write-tree` printed `b5ac6525...`, `git rev-parse HEAD` printed `d0e8b1cf...`, and
`git status --short` was empty. The review was read-only in `<review-clone>`; only this summary was written.
`scripts/publish-guard.sh f362cdf HEAD` printed OK for 10 commits, and `tools/check_data_boundary.py` printed OK.
`829025d6` adds only `.hyperreview/` records and a `DAG.json` line; the shipped records carry no machine path, host
name or private address, and the guard agrees.

## What was checked, and how

**Seat up (`Game1.VrHud.cs:335-338,443-447`).** `SeatView(eye)` is `Invert(head * T(eye.Position))`, whose rotation
part is `head^-1`; `Invert(_vrEyeCameras[0].View)` is `head * cameraRotation` (`VrEyeCamera.ForEye`,
`Vr/VrEyeCamera.cs:30-33`). `VrSeatUpInWorld` therefore composes to `Vector3.Up * cameraRotation`: the head term
cancels, so the result is the seat basis lifted into the world and does not move when the head rolls. It is in the same
space as `anchorWorld - head` (both world), and `MarkerBillboard` normalises and falls back on a degenerate cross,
so the transform is right. See note N3 for what that up is in a non-cockpit camera.

**10 m block.** `FlightBlockQuad` scales `x` and `y` by `10*tan(20 deg)/400` and puts `z = -10`, so the projected
`x/|z|` ratio, and the angle, are those of the old direction-only quad; the new test's disparity agrees with
`PixelsForAngle(2*atan(0.032/10))`. Both eye passes draw with their own `SeatView(eye)` and the eye's off-centre
projection (`Game1.VrHud.cs:348-355`), so the eye offset now produces the 10 m parallax instead of none. `AtInfinity`
is gone from the code and no caller or test refers to it. Leftovers in prose are finding F2.

**Density.** `_density` is set before the try and reset in its `finally` (`HudRenderer.cs:577,591-594`). Every
`SpriteBatch.Begin` reachable from `DrawFlightReference` carries `DensityTransform` (`BeginBatch` at `:684`, the two
gauge passes at `:616`/`:619`); `DrawCentreIndicatorsAdditive` and `BeginAdditiveSpritePass` are not on that path.
No `ScissorRectangle` is used anywhere in `HudRenderer`, and the passed `Viewport` is layout-sized while the target
is twice that, which is what the transform is for. `DenseText` halves the scale only when it swaps a 1x bake for its
2x bake, so a site that `HudFont` already resolved to the 2x bake (pane scale above 1.25) is not halved again; a
font with no 2x bake keeps its size (blurrier, not bigger). `DrawVrFlightBlock` divides the image by the density, so
the crop, the UVs and the angular size are unchanged.

**Wrap and cargo (`Game1.FlightParts.cs:298-326`, `Game1.InFlightUtility.cs:165-196`).** `WrapLine` returns one empty
line for empty text, gives an over-wide word its own line, keeps runs of spaces inside a line and trims the run at a
break; the measure callback sees the candidate line, which is what `MeasureString` needs. The flat branches keep
`h - FlightPartsLineFromFoot` and `MeasureString(line).Y`, so the flat game is unchanged. `DrawCargoHud` re-wraps the
weapons line to place cargo under it, and its box is sized from the wrapped lines. The 152 px top matches the fixed
native chat foot (126) plus the keys hint, and the 65% width clears the right-hand pane at the 1280-wide
image the note names (and at 1920).

**Throttle.** `_lastSentControls.Throttle` (`ControlData`) and `_throttle` are both -1..+1, and the joystick's
`ThrottleOverride` is the raw, sign-corrected axis in the same range (`Flight/JoystickFlightController.cs:247-259`).
An unbound axis, or a bound axis whose latch has not engaged (so `ThrottleOverride` is null and the sent value is
the keyboard one), shows the keyboard value; autopilot shows the keyboard/cruise value as P445 documents. The bound
flag is refreshed on the same line that stores `_lastSentControls` (`Game1.FlightInput.cs:2130-2135,2537`).

**Missiles.** `BeginFrame`/`Observe`/`EndFrame` run only off the repeat pass; the second eye takes `Shown` with the
live server position as its fallback, which is what a missile that appeared between the two passes needs. `Observe`
returns the server position on a first sample, and drops velocity when a position change comes more than 0.5 s after
the last. The lead is capped at 0.1 s. The same `shown` position feeds the mesh and `TrailMissile`; the trail's
finite difference now reads a smoother series. The clock is the monotonic `Stopwatch`, not the game clock.

## Findings

**F1, low: the missile comment still promises no extrapolation.** `Game1.Rendering.cs:1206-1209` states each missile
"is drawn at the server-authoritative Position ... (no extrapolation ...)", nine lines above `:1220-1222`, which now
says the opposite. A later maintainer could "fix" the smoother away, or trust the old comment when tuning the flat
game. Fix: rewrite the P234 sentence to name `MissileMotionSmoother` and keep the rest.

**F2, low: "at infinity" survives in current-state prose.** `HudRenderer.cs:540` (`CentreGroupOnMainPass`),
`HudModel.cs:236` (`FlightReferenceInWorld`) and `knowledge/vr-hud-design.md:196` (the decision-table row "What the
VR HUD is built from") and `:354` (the "Step 2 as built" heading) all still put the block at infinity. `:194` in the
same table and the revision note at `:117` say 10 m, so the table now contradicts itself. Fix: say 10 m in the seat.

**F3, low (headset judgement): a 10 m block still draws like an overlay.** `DrawVrFlightBlock` goes through
`DrawVrQuad`, which sets `DepthStencilState.None` (`Game1.VrHud.cs:574`), and the quad is painted after the scene into
the eye image. At infinity that was consistent; at 10 m it is not. Scenario: fly within 10 m of a station or hull
with the ring on it; the ring's arcs paint over the nearer hull while the markers beside it (drawn with `DepthRead`)
are hidden by it. Fix: draw the same seat-space corners with the eye's game view and projection, as the markers do,
and depth-test read-only; or record that an always-on-top HUD overlay is intended.

## Notes

N1. `WrapLine`'s doc says each line keeps the original's runs of spaces "less the space it was broken at"; a break
inside a run drops the whole run (`TrimEnd`). No HUD string depends on it, so this is wording only.

N2. The eight fixes have not been seen in a headset: the third look was at `0314c5e`, before them. `VrSeatUpInWorld`,
the density call site, the smoother's Begin/End and the throttle arguments are exactly the parts no unit test covers,
so the fixes rest on reading and the filtered tests. That is residual risk under `AGENTS.md`, not a readiness block.

N3. Because the head term cancels, `VrSeatUpInWorld` returns the game camera's up, not the ship's; in cockpit mode
they are the same, and the existing `VrNoseInWorld` works the same way. It bounds what "upright to the seat" means
outside the cockpit.

## Tests

The new unit tests are behaviour-level: extrapolation amount and cap, gap reset, averaged velocity, the repeat pass,
pruning, wrapping (including a long word and double spaces), the 1.4 degree floor, the 10 m depth and disparity, and
the 1x-to-2x mapping. They do not mirror the wiring. The flat equivalence of the port lines and of `DrawCargoHud` is
established by reading, not by a test, which matches the author's disclosure.

## Verification limits

No `dotnet`, build or test run (not available to this review, and not permitted). Behaviour inside the headset,
the 2x flight image's sharpness and the occlusion in F3 need the operator's next look.