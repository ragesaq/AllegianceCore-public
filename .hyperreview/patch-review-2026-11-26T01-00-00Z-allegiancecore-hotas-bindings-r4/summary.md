# Patch review — PR #14 delta (round 4): undock disarm, lesson filter, throttle detent

- Reviewed head: `c6df4d738a1a3387ddb136194805c443296bc7a1`; tree `0baf0523c755866f1bfed23cbfd8e48eb33a8b4f`
- Range: `8e225d2b..c6df4d73`, two commits, 10 files, +258/−25 (code: `JoystickBindings.cs` +7/−4,
  `Game1.FlightInput.cs` +24/−5, `Game1.cs` +1, tests +30/−6, records +180)
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high`
  (reviewer-parity matrix 4.14.0, sha256 `74618f22…`, explicit selection; route source: the target's
  AGENTS.md "Cross-family review route"). Cross-family against the Anthropic/Claude-authored change.
- Method: read-only inspection of the frozen clone at `<review-clone>`. No dotnet, build, test, replay,
  game or hardware run.

**Verdict: PASS-WITH-CONDITIONS.**
**nextAction:** move the docked-to-undocked disarm before `_targetJoystickFrame` is captured, correct
the stale class comment, then ragesaq's flight test.

## Round 3 disposition

- **R3-1 (Low, undock disarm) — partly fixed; the `Game1` half is incomplete.** The resolver half is
  right: `DisarmJoystick()` (`Game1.FlightInput.cs:43`) clears the armed state and invalidates the frame
  cache, and `DisarmThenResolveAgain_SameSamples_DropsTheHeldButton` proves the same samples then hold
  nothing. The call site misses three readers (N1).
- **R3-2 (Low, comment defects) — fixed.** `JoystickHoldsAction` and `CurrentJoystickFrame` each have
  their own summary, and `JoystickBindings.cs:15-19` separates the held flight commands from the
  press-only targeting commands. One new stale comment of the same kind remains (N2).
- **R3-3 (Low, stale parity row) — fixed.** `docs/parity/parity-matrix.csv:718` names the 14 targeting
  commands and says the 5-way switches are bound as buttons.

## New findings

### N1 — Medium — the undock disarm misses three readers of the captured targeting frame

- `Game1.FlightInput.cs:1437` copies `_targetJoystickFrame = CurrentJoystickFrame(gameTime)` before the
  docked test at `:1503`. On the first undocked frame the new block at `:1513-1517` calls
  `DisarmJoystick()`, which sets `_joystickFrameTicks = -1`.
- That is enough for every later `CurrentJoystickFrame` reader (chaff `:1691`, ripcord `:1737`, missile
  `:1813`, axes and held buttons `:2119`): they re-resolve against the disarmed state.
- It is not enough for `JoystickHoldsAction` (`:37-39`), which reads the copied
  `_targetJoystickFrame`. `JoystickBindingFrame` is a `readonly record struct`
  (`Flight/JoystickBindings.cs:88`), so invalidating the cache does not touch that copy, and the copy is
  assigned only at `:1437`.
- The readers that use it after the disarm block are `ToggleAutopilot` `:2216`, `TargetNothing` `:2255`
  and `TargetCenter` `:2260`. Their edge latches (`_gKeyWasDown`, `_semicolonNothingKeyWasDown`,
  `_apostropheCenterKeyWasDown`) are never updated on docked frames because the docked branch returns at
  `:1509`.
- Failure scenario: in the hangar, rest a thumb on the throttle's autopilot push button (31) and press
  L or U to launch. The resolver arms the button during the docked frames; the undock frame's copy at
  `:1437` still holds it; `:1516` disarms the resolver but not the copy; at `:2216` `gPress` is true and
  `_gKeyWasDown` is false, so `ToggleAutopilotForTarget()` fires. `TargetNothing` clears the target and
  disengages autopilot the same way, and `TargetCenter` selects the centermost target.
- The `RemainderTargetActions` and `TargetHostile` paths are not affected: their handlers run at
  `:1438-1440` before the docked return, so `_targetPressEdges.Observe` consumes the held edge in the
  hangar and produces none on undock. Only the three latch-style readers fire.
- The comment at `:1511-1512` ("a joystick button held through the undock acts only after a release")
  and the round-3 README claim that the fix "drops the frame already resolved for that update" are
  therefore untrue for those readers.
- Fix: detect the transition before `:1437` — move the `_joystickDockedLastFrame` check to the top of
  `UpdateFlying`, or disarm from `ClearUndockedHangar()` (`Game1.Panes.cs:372`), which `Update` already
  calls at `:390` — or clear `_targetJoystickFrame` inside `DisarmJoystick()`. Moving the check is
  complete; clearing the copy also makes that one frame safe.

### N2 — Low — the resolver's class doc still says the built-in throttle is inverted like native

`Flight/JoystickBindings.cs:148-150` says "The throttle device feeds the throttle role, inverted like
native (`WinTrek.cpp:10842`)". The delta removed `Invert: true` from the built-in VPC binding (`:174`)
because the operator inverted the device, and the note and parity row were updated to match. The
sentence now contradicts the code. Failure: a maintainer trusting the comment re-adds `Invert: true`,
and the throttle then reads backwards against the configured direction. Fix: say the role is read as
the device sends it, and that a throttle at Virpil's own direction needs `"invert": true` in
`joystick.json`.

## Observations

- **Flag lifecycle.** `_joystickDockedLastFrame` is set on every frame `HandleDockedScreen` consumes
  (`:1508`), including launch-pending frames, and cleared only on the first frame past it. Leaving
  `Flying` while docked (death, quit, the ready screen at `:367-369`) leaves it true, so the next flight
  frame disarms: conservative, and a duplicate of `PrimeInFlightKeyLatches`. `HandleDockedScreen` returns
  true only for a docked, non-ghost ship (`Game1.Panes.cs:86-89,127,362`).
- **Double resolve.** One extra `Resolve` per undock frame, with no side effect found: `ThrottleBound`
  is profile-derived both times, so `_joystickThrottleWasBound` (`:2123-2125`) cannot spuriously reset;
  armed arrays are rebuilt from the same samples; the device log and legacy fallback state are stable.
- **ActionFilter.** `JoystickHoldsAction` applies the same filter `ActionMap.IsDownMostSpecific` applies
  (`Rendering/InputBindings.cs:251`), with "null means allowed". Only a training lesson installs it
  (`Game1.Training.cs:184`) and exit clears it (`Game1.TrainingIntegration.cs:58`), so nothing outside
  training is blocked.
- **Throttle sign.** `Normalize` divides by 32767 and clamps, so idle is −1 and full is +1, the
  `ControlData` convention the old inversion produced. The latch (`JoystickFlightController.cs:241-260`)
  compares `|raw − old|`, so it is sign-independent; the unplug hand-back reads `ThrottleBound` only.
- **Booster.** Held, merged as `AfterburnerButton` (`:2154-2155`), inside the `!composing` block and
  after the docked return, matching the Shift key; in training `ComposeTrainingFlight` (`:2509-2525`)
  masks `StateM` before sending. Button 79 collides with nothing (20–32, 34/35/36/43 and the encoder
  pairs are untouched), and a device with fewer buttons is safe (`JoystickBindings.cs:298-299`).
- **Latch at launch.** The detent is +1 and the latch needs movement from its first sample, so a lever
  left at the detent does not take the throttle role until it moves: launch starts at keyboard/idle, then
  jumps. Native's latch works the same way; worth watching in the flight test beside the held booster.
- **Tests.** `ThrottlePastTheAfterburnerDetent_HoldsTheBooster` arms first, presses 79 at full, checks
  the release; `DisarmThenResolveAgain…` fails if `Disarm` stops clearing armed state. Untested: the
  `Game1` glue (read only), the `ActionFilter` branch of `JoystickHoldsAction`, and a 79-button device.
- **Records.** The note, parity row and DAG match the code apart from N2; the note's "undocking disarms
  them the same way" is qualified by N1. No machine path, serial, secret or non-noreply address in the
  delta.
- **Upstream.** Hunks stay small and local (`Game1.cs` one field; `JoystickBindings.cs` one profile
  entry and one comment); no reformatting. All touched paths are index-LF; the CRLF on some
  working-tree copies is checkout-only.

## Verified by execution

`git rev-parse HEAD` and `git write-tree` matched the frozen head and tree; `git status --short` was
clean at entry; `git diff --check` clean; `git ls-files --eol` shows `i/lf` for all touched files.
`bash scripts/publish-guard.sh ff8e8a64 HEAD` → OK (10 commits). `python tools/check_data_boundary.py`
→ OK. `python tools/check_memory.py` → OK, with only the expected `recorded-by 'port'` warnings and the
no-baseline note (this clone has no remote).

## Verified by reading only

All runtime behaviour: the call order in `UpdateFlying`, the latch readers, the resolver, the throttle
controller, the training mask and the tests' assertions. The author's 0-warning build and 166-test run
were not reproduced.

## Verification limits

No dotnet, game, replay or hardware run. N1 is derived from call order and from `JoystickBindingFrame`
being a struct copy, not observed in a running game. The flight test remains the downstream acceptance
step.