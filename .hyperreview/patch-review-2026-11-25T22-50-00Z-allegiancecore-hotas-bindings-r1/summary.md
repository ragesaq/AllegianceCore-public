# Patch review — PR #14: joystick bindings for a split HOTAS

- Reviewed head: `b32f0e6255813b3e1eabc7737b4bfee02de3ca55`
- Reviewed tree: `83727f80ea1345096204f251c163d1a3e3e6f61f`
- Range: `ff8e8a64..b32f0e62`, 7 files, +636/-35
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high` (reviewer-parity
  matrix 4.14.0, sha256 `74618f22...`, explicit selection; route source: the target's AGENTS.md "Cross-family review
  route"). Cross-family against the Anthropic/Claude-authored change.
- Method: read-only inspection of the frozen clone at `<review-clone>`. No dotnet, build, test, replay or game run.

**Verdict: PASS-WITH-CONDITIONS.**
**nextAction:** fix F1 and F2 (or record an accepted deferral) before merge; F3-F5 can be follow-ups by the
operator's decision. Then the HOTAS flight test.

## Findings

### F1 — Medium — losing the throttle device freezes throttle at neutral and disables the keyboard throttle

`src/Allegiance.ClientCore.Game/Game1.FlightInput.cs:2094` calls `_joystickFlight.Update(jsFrame.Axes)` whenever
`AnyBound`; `src/Allegiance.ClientCore.Game/Flight/JoystickBindings.cs:135-173` leaves an unbound role at `0f`.

Scenario: the split HOTAS is flying with the VPC throttle engaged. The throttle's USB link drops. The stick still
matches, so `AnyBound` stays true and only `Axes.Throttle` becomes `0`. `JoystickFlightController.Update` sees
`|0 - old| > 0.1`, keeps `_joyThrottleEngaged = true`, sets `_oldJoyThrottle = 0` and returns
`ThrottleOverride = 0` every later frame. `Controls.Throttle` is pinned at 0 (mid) and the keyboard throttle keys
stop working, because the override wins. Recovery needs a throttle preset key that calls `Reset()` (33%/66%) or a
re-plug.

Before this change, losing the only joystick made `AnyBound` false, `Update` was not called at all, and the keyboard
throttle applied; this is new behaviour on the split path and neither the note nor the parity rows mention it.

Fix: add `bool ThrottleBound` to `JoystickBindingFrame`, and when a frame has no throttle binding after one had it,
call `_joystickFlight.Reset()` (that restores keyboard authority). Alternatively make the throttle parameter of
`Update` a `float?` and pass null when no device binds it. Cover it with a resolver test and a controller test that
the latch disengages.

### F2 — Medium — a malformed player `joystick.json` can crash the client

`src/Allegiance.ClientCore.Game/Flight/JoystickBindings.cs:225-247` accepts `null` elements in `devices`, `axes`
and `buttons`; `:139-161` dereferences every element; `src/Allegiance.ClientCore.Game/Game1.FlightInput.cs:74-90`
catches only `IOException` and `UnauthorizedAccessException`.

Scenario: `{"devices":[{"match":"Stick","axes":[null]}]}` parses without error (System.Text.Json does not enforce
nullability here and `RespectNullableAnnotations` is not set). The first flight update then throws
`NullReferenceException` at `a.Axis` inside `UpdateFlying`; nothing catches it and the game crashes. The same
happens for `buttons:[null]` and `devices:[null]`. The type doc promises "A file that cannot be read is reported and
ignored, never fatal", and the note says the same.

Fix: reject null entries in `Parse` with a reason (or make the element types nullable and skip nulls), and widen the
call-site catch so other read failures (`ArgumentException`, `NotSupportedException`) cannot be fatal either.

### F3 — Low — "forgotten when the device disappears" is not always true

`src/Allegiance.ClientCore.Game/Flight/JoystickBindings.cs:166-168` prunes stale armed state only when
`_armed.Count > _seenThisFrame.Count`. If one bound device disappears and another bound device appears in the same
update, the counts are equal and the old key survives. If the first device returns at the same index and name with a
button already held, it fires without a release, which contradicts the documented and tested rule.

Fix: prune by key membership whenever the connected set changes (track every sample, not only bound ones), and add
the drop-plus-add test.

### F4 — Low — the legacy fallback can let leftover pedals fly the ship

`src/Allegiance.ClientCore.Game/Flight/JoystickBindings.cs:131-132`. If both HOTAS devices unplug mid-flight and
only the pedals remain, no device matches and `devices[0]` (the pedals) is read with `DjiFpvRc3`, so pedal axis 0
becomes yaw. This is the documented fallback and matches the old first-device rule, but for a player who never used
the legacy path it produces full-deflection input from a device they chose to leave unbound.

Fix: remember that a profile has matched since the resolver was created and refuse `Legacy` afterwards, or allow the
fallback only when exactly one device is connected and none has ever matched.

### F5 — Low — untested paths

`tests/Allegiance.ClientCore.MonoGame.Tests/JoystickBindingResolverTests.cs` covers the main resolver contract well
(23 cases). Not covered: a device whose button count changes (the `armed.Length != s.Buttons.Count` reset), F3's
drop-plus-add, F1's throttle loss, and the pedals-only fallback (F4). The author reports 3 of the tests failing when
the armed gate is removed; that was not reproduced here.

## Observations

- Single-device parity holds. `Legacy` uses exactly `DjiFpvRc3`'s indices and invert flags, and `Normalize`
  (`JoystickBindings.cs:193-200`) does the same divide, clamp order and invert as `GatherRoles`
  (`JoystickFlightController.cs:188-208`). Fire 1 and booster 2 are the old constants. The only behaviour delta is
  the held-at-start gate, which the note names as the one change for a setup that worked before. No `Reset()` call
  was removed: none existed on unplug, before or after.
- The per-update cache key is sound as used. Both call sites (`Game1.FlightInput.cs:1787`, `:2094`) are inside one
  `UpdateFlying`, called once per update (`:792`), with the same `GameTime`. MonoGame advances `TotalGameTime` per
  update in both fixed and variable timestep, so equal ticks cannot mean two different updates there; ticks 0 on the
  first frame work because the sentinel is `-1`. Residual risk only if a host ever reuses a `GameTime`; the symptom
  would be one stale frame.
- The reused bool buffers do not alias across frames. `JoystickBindingFrame` holds only floats and bools, `_armed`
  copies into its own arrays, and the buffers are fully rewritten before each sample is added.
- Native fidelity is right. `defaultinputmap.mdl:117-121` gives joystick 0 buttons 0/1 to TK commands 55/56;
  `WinTrek.cpp:10822-10843` reads yaw from value 0, pitch from value 1, roll from `GetAxis(2)` (numeric map entry 2 ->
  DirectInput value 3) and throttle from `-GetAxis(3)` (entry 3 -> value 2). So X yaw, Y pitch, twist roll and a
  negated throttle are the native convention, and the note is honest that the operator's capture labels axis 0 as the
  stick's left/right and axis 2 as the twist, with the flight test to decide. `trekinput.cpp:717-725` ORs button
  bindings per TK command and `numericValuesNew` binds per (joystick, value) pair, which is the shape this change
  ports.
- A numeric enum value (`"role": 7`) is accepted silently and the role is ignored, as are unknown properties, so a
  typo gives no diagnostic. The file is read once, on the first flight update, and cached for the process lifetime,
  so edits need a restart. The hot-plug scan is a few `GetCapabilities`/`GetState` calls per update, unchanged in
  shape from the old rescan.
- Records are accurate. Both parity rows cite real native lines and match the code; `DAG.json` `ragesaq-006` moved to
  `review` with a concrete `next`, `updated` 2026-11-25 and its link in the same commit, per the rules block. No
  machine path, device serial, secret or non-noreply email appears in the diff.
- Upstream mergeability is good: 7 files, narrow hunks (`Game1.cs` +8/-1, `Game1.FlightInput.cs` +70/-32), the rest
  new files, no reformatting; `git diff --check` is clean and the new `.cs` uses the repo's existing index-LF
  convention. At 636 changed lines this correctly needs the cross-family review it is getting.

## Verified by execution

- `git rev-parse HEAD` and `git write-tree` matched the frozen target; `git status --short` showed no tracked change.
- `bash scripts/publish-guard.sh ff8e8a64 HEAD` -> `[publish-guard] OK (1 commit(s))`.
- `python tools/check_data_boundary.py` -> OK.
- `python tools/check_memory.py` -> OK, with only the two expected warnings (the `recorded-by` name has no
  `people/` folder, and the entry-deletion check has no git baseline).

## Verified by reading only

- Runtime behaviour, the throttle latch, the cache key, System.Text.Json null/enum handling and the native
  comparison. The author's build, test and replay claims were not reproduced.

## Verification limits

No dotnet, game, replay or hardware run; Linux CI and the operator's HOTAS flight test are still open. The
throttle-latch finding is derived from the controller code and its call site, not observed on a device.