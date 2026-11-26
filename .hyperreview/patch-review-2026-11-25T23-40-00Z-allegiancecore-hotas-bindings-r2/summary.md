# Patch review — PR #14 delta (round 2): joystick bindings for a split HOTAS

- Reviewed head: `a25c734bb78b485a3444360e546ca52cb4d076f7`; tree `7ffb38f39765a8d932adc6262b7bd32b10d5bc5c`
- Range: `b32f0e62..a25c734b`, 8 files, +351/−59 (code: `JoystickBindings.cs` +163, `Game1.FlightInput.cs` +32, `Game1.cs` +1, tests +162)
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high` (reviewer-parity matrix 4.14.0, sha256 `74618f22…`, explicit selection; route source: the target's AGENTS.md "Cross-family review route"). Cross-family against the Anthropic/Claude-authored change.
- Method: read-only inspection of the frozen clone at `<review-clone>`. No dotnet, build, test, replay, game or hardware run.

**Verdict: PASS-WITH-CONDITIONS.**
**nextAction:** fix N1 (or record an accepted deferral); then the HOTAS flight test. Round 1's F1 and F2 are closed.

## Round 1 findings

- **F1 (Medium), throttle freeze: fixed for device loss.** `JoystickBindingFrame.ThrottleBound` (`src/Allegiance.ClientCore.Game/Flight/JoystickBindings.cs:59`, set from `throttle.HasValue` at `:215`) plus `Game1.FlightInput.cs:2098` calling `Reset()` on the true→false edge before the composing gate (flag at `:2100`). `Reset` (`Flight/JoystickFlightController.cs:215`) clears `_joyThrottleEngaged` and `_seenFirstSample`, so the next `Update` re-baselines and the keyboard throttle applies; the all-devices-gone case resets and skips `Update`. Residual: `ThrottleBound` tracks a profile binding, not a readable axis, so a connected device that stopped reporting the axis could still latch at 0; MonoGame's per-device axis count is stable, so narrow.
- **F2 (Medium), malformed `joystick.json` fatal: fixed.** `Parse` rejects null entries (device/axes/buttons/axisCommands), thresholds outside `(0, 1]`, integer enum values (`allowIntegerValues: false`) and out-of-range integers (`JoystickBindings.cs:279-318`), catching `JsonException`, `NotSupportedException`, `ArgumentException` (`:286`). `LoadJoystickProfiles` widens to `ArgumentException`, `NotSupportedException`, `SecurityException` (`Game1.FlightInput.cs:85`). Failure is now a logged reason and no profiles.
- **F3 (Low), armed state surviving a same-update swap: the author's dispute is correct; the finding was wrong.** `Armed` adds each bound key to `_armed` before the prune, and `_armed` is always a superset of `_seenThisFrame`; a swap leaves the departed key plus one new key, so the count is at least one greater than `seen` and the round-1 condition did prune. Equality would need `|old keys| + 1 == |now bound|` with `|old keys| >= |previous bound| == |now bound|`, impossible. The membership rewrite (`:206-210`) is equivalent and simpler; the swap test passes under the old condition, which is the right evidence.
- **F4 (Low), leftover pedals: fixed as proposed.** `_profileEverMatched` (`:170`) gates `LegacyAllowed` (`:160`) and the fallback. Accepted trade-off, documented: after any match, legacy is unavailable until the resolver is recreated, so a single-DJI player who connects a matching device once loses the fallback for the session.
- **F5 (Low), untested paths: mostly fixed.** New tests cover the button-count reset, the swap, throttle loss plus reset, the pedals fallback, the threshold default, diagonals, and trigger stage 2 unbound/user-bindable. Untested (Game1 glue): the reset call site, the strafe OR into the outgoing command, and the ripcord edge/prime; removing the reset line would not fail a test.

## New findings

### N1 — Low — joystick ripcord is primed from the previous flight's cached frame

`src/Allegiance.ClientCore.Game/Game1.FlightInput.cs:877` primes `_lKeyWasDown` from `_joystickFrame.Ripcord`, the field written by `CurrentJoystickFrame` (`:68`). The prime runs on the ReadyCheck→Flying transition (`:781`) and the resolver is only called from `UpdateFlying` (`:1712`, `:1788`, `:2094`), so at prime time the frame is the last flight frame's, or `default` in a fresh process. The keyboard half reads the live key state, so the protection P523 intends covers the key but not the button.

Scenario: second mission in the same process, Ripcord bound to a thumb button released at the last sampled flight frame. The player presses and holds it on the ready screen, then launches. Prime reads stale `false`; the first flight frame (`:1712`) resolves fresh `Ripcord` true with `_lKeyWasDown` false, so `RipcordKey.RequestFor` runs and, with a sector still selected, sends a ripcord on the launch frame (or cancels an active one). The armed gate does not help: the device was known and its button armed by the earlier release. The operator intends to bind this button, so the path is realistic.

Fix: prime from a freshly resolved frame, mirroring the keyboard (pass `gameTime`/the frame to `PrimeInFlightKeyLatches` and use `CurrentJoystickFrame(gameTime).Ripcord`), or clear the device's armed state on flight entry so a held button must be released once. A fresh resolve primes a known armed button true; a newly seen device reads false until release.

## Observations

- Moving the frame read above the composing gate is neutral for the resolver: the missile site (`:1788`) already resolved it unconditionally every round-1 `UpdateFlying` frame, so the 2094 call was a same-tick cache hit. Only the throttle flag/reset are new while composing; axis and command application stays inside `!composing`.
- Strafe mapping matches the keyboard exactly: `FlightInput.Translate` maps the four commands to `Left/Right/Up/DownButton` (`Flight/FlightInput.cs:158-166`), and `Game1.FlightInput.cs:2132-2139` ORs the same bits. `UpButton` is native's thrust up, so Up/Down is right. Threshold is ±0.5, negative first; a value cannot satisfy both, and diagonals OR two commands. Left/right drop the autopilot (`:2141`, `:2393`) like the strafe keys; up/down do not, also like the keys. No training yield is needed: `YieldsToTrainingTarget` resolves a *key* collision on `ThrustUp`/`AcceptCommand`, and the joystick is not that key; `ComposeTrainingFlight` still masks the composed `StateM`. Autopilot replaces `sentStateM` (`:2435`), and the turret reads only `WeaponsMask`.
- JSON: `AxisCommands` is null only for `Legacy`, guarded by `is { }`; user profiles always get a list. On net10.0 (`Directory.Build.props:3`), System.Text.Json honours an optional constructor default when the property is absent, and `AxisCommands_ParseWithTheDefaultThreshold` asserts 0.5; not executed here.
- HeldMask is 8 values in an `int`; no collision. A future command past bit 31 would silently alias; the enum size is an implicit invariant worth a comment if it grows.
- Records agree with the code: `DAG.json` `ragesaq-006` is `review`, `updated` 2026-11-25, `next` names the round-1 outcome and pending steps; `.hyperreview/README.md` matches. Small wording gaps: the parity row's legacy sentence lacks "and none has ever matched"; the note's "Ripcord acts once per press" omits N1; the fork `GLOSSARY.md` "Input" entry does not name `Flight/JoystickBindings.cs` (pre-existing).
- Upstream mergeability is good: narrow hunks, no reformatting. `git diff --check` clean; every touched path is index-LF (`git ls-files --eol`), including the `Game1.cs` blob, so no CRLF entered the commit. No machine path, serial, secret or non-noreply email in the delta.

## Verified by execution

- `git rev-parse HEAD` and `git write-tree` matched the frozen head/tree; `git status --short` clean at entry, and only the untracked round-2 artifact directory after writing this record.
- `bash scripts/publish-guard.sh ff8e8a64 HEAD` → `[publish-guard] OK (3 commit(s))`.
- `python tools/check_data_boundary.py` → OK; `python tools/check_memory.py` → OK with only the expected warnings (no `people/` folder; no deletion baseline).
- `git diff --check b32f0e62 HEAD` clean; `git ls-files --eol` shows `i/lf` for all touched files.

## Verified by reading only

Runtime behaviour, prime/latch ordering, resolver arithmetic, test logic and the author's mutation claims. The build, 84 tests, fast tier and replay were not reproduced.

## Verification limits

No dotnet, game, replay or hardware run, so N1 is derived from call order, not observed, and the STJ default rests on documented .NET 10 behaviour plus the author's test. The flight test remains downstream acceptance.