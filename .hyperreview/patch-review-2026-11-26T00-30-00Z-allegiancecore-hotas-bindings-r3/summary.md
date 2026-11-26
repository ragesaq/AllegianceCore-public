# Patch review — PR #14 delta (round 3): joystick targeting, chaff, disarm

- Reviewed head: `8e225d2b03f28f7399ed058dc7dc6295eb14054f`; tree `4136c7e11dad9dcae69a639a14a149c5a8bddb0a`
- Range: `a25c734b..8e225d2b`, five commits, 12 files, +333/−20 (code: `JoystickBindings.cs` +78,
  `Game1.FlightInput.cs` +19, tests +105, records +68)
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high`
  (reviewer-parity matrix 4.14.0, sha256 `74618f22…`, explicit selection; route source: the target's
  AGENTS.md "Cross-family review route"). Cross-family against the Anthropic/Claude-authored change.
- Method: read-only inspection of the frozen clone at `<review-clone>`. No dotnet, build, test, replay,
  game or hardware run.

**Verdict: PASS-WITH-CONDITIONS.**
**nextAction:** bind the disarm to the dock→air transition (or record it as accepted), fix the two comment
defects and the stale parity row; then ragesaq's flight test.

## Round 2 disposition

- **N1 (Low, stale ripcord prime): fixed.** The prime now reads the keys only (`Game1.FlightInput.cs:881`),
  and `PrimeInFlightKeyLatches` calls `JoystickBindingResolver.Disarm()` (`:884`;
  `Flight/JoystickBindings.cs:222`), which clears every device's armed array; the next `Resolve` marks a
  button armed only after it is seen released (`Armed`, `:281-296`). That closes the second-mission path
  for ripcord, chaff, missiles and the targeting commands, and it is consistent with
  `_targetPressEdges.Prime`, which is also keys-only. `Disarm_HeldButtonActsOnlyAfterARelease`
  (`tests/Allegiance.ClientCore.MonoGame.Tests/JoystickBindingResolverTests.cs:417`) exercises the
  resolver, not the call site, so the `Game1` glue remains read-only (round 2 F5 residual). No bad
  interaction with the prune (it iterates an empty table) or a device first seen on the same frame (its
  array starts disarmed).
- **F1–F4: unchanged and still closed. F5:** the new tests cover the resolver and the built-in button
  tables; the dispatch ORs, the frame freshness and the disarm call remain untested.

## Findings

### R3-1 — Low — the disarm covers the ready-screen launch only, not a hangar undock

`Disarm` is reachable only from `PrimeInFlightKeyLatches` (`Game1.FlightInput.cs:884`), which runs on the
ReadyCheck→Flying handoff (`:785`). Undocking keeps `_state == Flying` and does not call it:
`HandleDockedScreen` clears the hangar at `Game1.Panes.cs:365` and returns. The joystick frame is now
resolved before that return (`Game1.FlightInput.cs:1428` is above `:1494`), so the resolver runs and arms
devices on every docked frame, while the chaff, ripcord and autopilot latches are not updated while docked
because their blocks sit below the return.

Scenario: the player presses the ripcord button (5) in the hangar, keeps a thumb on it, then clicks Launch.
The button was armed by an earlier release; on the first undocked frame `lRipcord` is true at `:1720`,
`_lKeyWasDown` is still false, and `RipcordKey.RequestFor` starts (or cancels) a ripcord. The same holds for
chaff (4), missiles (3) and the throttle targeting buttons. A held key has the same pre-existing gap because
the latches are not primed on undock, so this is not a regression, but the new note's "on entering flight
every joystick button is disarmed" (`knowledge/joystick-input.md`) is broader than the code.

Fix: disarm on the dock→air edge before `:1428` (remember `_wasDocked` and call
`_joystickBindings?.Disarm()` at the top of `UpdateFlying`), or at each undock request (shop L/U, the V2
loadout undock, touch). Recording the gap as accepted is also reasonable.

### R3-2 — Low — two comment defects in the new code

- `Game1.FlightInput.cs:33-46`: `JoystickHoldsAction` was inserted between `CurrentJoystickFrame`'s
  `<summary>` and its method, so `JoystickHoldsAction` has two `<summary>` elements (the first describes the
  frame cache) and `CurrentJoystickFrame` has none. No compiler warning (no `GenerateDocumentationFile`), so
  the 0-warning gate is not affected, but the docs are attached to the wrong member.
- `Flight/JoystickBindings.cs:17`: "the others are held" contradicts the next sentence; the 14 `Target`-on
  commands are press-only. Say that the first nine flight commands are held and the targeting commands act
  once per press.

### R3-3 — Low — parity row is stale

`docs/parity/parity-matrix.csv:718` still ends "the bindable commands are Fire, FireMissile, Booster, the
four side thrusters, Ripcord and DropChaff; hats are not". The delta makes the 14 targeting commands
bindable in `joystick.json` and binds the two 5-way castle switches (10 positions) in the built-in throttle
profile. Extend the list and the naming rule, and clarify "hats" (these are button-indexed switch positions,
not hat axes).

## Observations

- **Dispatch coverage:** every name in `JoystickKeyActions` has a real dispatch point — `Target`,
  `TargetEnemy`/`TargetFriendly`/base/warp families through `RemainderTargetActions`
  (`Game1.TargetCommands.cs:11-17,165-166`), `TargetHostile` through `TargetEdgeDown`
  (`Game1.Targeting.cs:1098`), `TargetNothing`/`TargetCenter` at `Game1.FlightInput.cs:2238-2245`,
  `ToggleAutopilot` at `:2199`. No action is dispatched twice through two handlers.
- **Frame freshness:** `_targetJoystickFrame` is set at `:1428` before all three target handlers
  (`:1429-1431`); no read is reachable earlier (the investment-pane early return at `:1424` skips them), and
  the later `CurrentJoystickFrame` calls are same-tick cache hits. Docked, composing, starmap-goto and
  training orderings match the key path.
- **Shared latches:** key and button OR into one `ActionPressEdges` entry or one was-down flag, so holding
  both yields one edge and a press made while the key is held is swallowed. That is the documented
  `ActionPressEdges` contract: no double fire, a deliberate lost simultaneous press.
- **HeldMask:** 23 values, highest bit 22. `Commands_FitTheHeldMask` guards the count, not the maximum
  value; a sparse future enum could still alias, and the guard is one conservative value short (32 values
  would still be bit-safe). Acceptable for an appended enum.
- **Training:** the `ToggleAutopilot` button is ORed outside `AutopilotKeyDown` (`:2197-2199`), so it
  bypasses that method's `ActionFilter` and cloak-key reservation (`Game1.TargetCommands.cs:218-225`). Low
  impact (the joystick is not the native key the lesson records), but it is the one gate the button does not
  share with the bound key.
- **JSON:** `JsonStringEnumConverter(allowIntegerValues: false)` handles the new names; a user profile can
  bind them, and the test proves parse plus `HoldsAction`. No parsing surprise.
- **Throttle map:** buttons 20–32, no duplicates; none of the rest-held 34/35/36/43 and none of the encoder
  pairs 1/2, 13/14, 50/51, 53/54. Each castle position is one button, so no two commands fire together.
- **Capture record:** the throttle capture's per-press button order (21, 15, 16 → 31, 32, 20; …) is
  written down, but the stick capture's is not; the 3/4/5 claim rests on the note. The flight test will settle it.
- **Tests:** `StickThumbButtons_…`, `Disarm_…`, `ThrottleGrip_TargetingButtons` (13 cases),
  `ThrottleRestButtons_AreNotBound` and `UserProfile_…` assert exact masks and action resolution, so they
  fail for the right reason. `KeyActionCommands_…` checks the name tables, not the dispatch ORs.
  `FlightCommands_AreNotKeyActions` does not check `DropChaff` or `FireMissile`.
- **Upstream:** narrow hunks, no reformatting; every touched path is index-LF; no machine path, serial,
  secret or non-noreply address in the delta.

## Verified by execution

`git rev-parse HEAD` and `git write-tree` matched the frozen head/tree; `git status --short` was clean at
entry. `git diff --check` clean. `git ls-files --eol` shows `i/lf` for all touched files.
`bash scripts/publish-guard.sh ff8e8a64 HEAD` → OK (8 commits). `check_data_boundary.py` → OK.
`check_memory.py` → OK with only the expected warnings.

## Verified by reading only

All runtime behaviour, the dispatch wiring and latches, the transition set, and the test logic. The
author's 0-warning build and 164-test run were not reproduced.

## Verification limits

No dotnet, game, replay or hardware run; R3-1 is derived from call order, not observed. The flight test
remains downstream acceptance.