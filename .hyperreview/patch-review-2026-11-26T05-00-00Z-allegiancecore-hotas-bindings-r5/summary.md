# Patch review — PR #14 delta (round 5): undock disarm copy reset, afterburner on 75

- Reviewed head: `3f99264631d088129a62de26c0d023d705d4be39`; tree `9223facc8e66fb17228d84d68b68e4bab0e2c271`
- Range: `c6df4d73..3f992646`, three commits, 8 files, +201/−14 (code: `JoystickBindings.cs` +7/−4,
  `Game1.FlightInput.cs` +4/−2, tests +14/−4; records: note +10/−3, `DAG.json` 1/1, review record +165)
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high`
  (reviewer-parity matrix 4.14.0, sha256 `74618f22…`, explicit selection; route source: the target's
  AGENTS.md "Cross-family review route"). Cross-family against the Anthropic/Claude-authored change.
- Method: read-only inspection of the frozen clone at `<review-clone>`. No dotnet, build, test, replay,
  game or hardware run.

**Verdict: PASS-WITH-CONDITIONS.**
**nextAction:** fix the three low items below (test the reset, mark one note sentence historical, bump the
DAG node's `updated`), then ragesaq's flight test.

## Round 4 disposition

- **N1 (Medium, undock disarm missed the targeting frame copy) — fixed, and the fix is complete.**
  `_targetJoystickFrame` is assigned once per update at `Game1.FlightInput.cs:1439` and is read only through
  `JoystickHoldsAction` (`:38`); there is no other reader and no local copy. `DisarmJoystick` now sets it to
  `default` (`:48`), and the undock call at `:1518` runs after the assignment and before every remaining
  reader: `ToggleAutopilot` `:2218`, `TargetNothing` `:2257`, `TargetCenter` `:2262`. All three therefore see
  an empty frame on the undock frame and reset their latches to false.
  - The three handlers at `:1440-1442` run before the docked test (`:1505`) and so still read the fresh
    frame, but the ones that read the copy are edge-based: `TargetEdgeDown` (`Game1.Targeting.cs:1031`) and
    `RemainderTargetEdgeDown` (`Game1.TargetCommands.cs:191`) go through `_targetPressEdges.Observe`, which
    ran on every docked frame and consumed the held edge there. `HandleCommandActionKeys`
    (`Game1.Orders.cs:58`) does not read the copy. Round 4's reasoning holds.
  - The right-mouse target-center path (`Game1.MousePick.cs:104`) reads only mouse state; it never consults
    the joystick frame, so the reset neither helps nor harms it.
  - The other `DisarmJoystick` caller, `PrimeInFlightKeyLatches` (`:895`), runs in the `ReadyCheck` branch
    before `UpdateFlying` and returns (`:804-805`), so the reset there is redundant and harmless; the next
    frame reassigns the field. No legitimate press is lost: the keyboard path is untouched, and a joystick
    press that begins on the undock frame is dropped only until release, which the "after a release"
    contract already states.
  - Residual: the regression test does not cover the assignment (R5-1).
- **N2 (Low, stale class comment) — fixed.** `Flight/JoystickBindings.cs:148-153` now says the throttle role
  is read as the device sends it, that the Virpil configurator inverts the levers, and how a throttle at
  Virpil's own direction is set up. No other comment still calls the named VPC profile inverted. The legacy
  fallback layout (`:136-139`, `JoystickFlightController.NativeHotas`) still inverts by design and says so,
  which is not a contradiction.

## New findings

### R5-1 — Low — the new regression test exercises the struct, not the reset

- `JoystickBindingResolverTests.cs:544-552` (`DefaultFrame_HoldsNoAction`) proves `default(JoystickBindingFrame)`
  holds nothing. That property is necessary, but no test calls `Game1.DisarmJoystick` or reads
  `_targetJoystickFrame` (the only `Disarm` calls are the resolver's own, at `:424` and `:526`).
- Failure scenario: a later edit deletes or moves `_targetJoystickFrame = default;`
  (`Game1.FlightInput.cs:48`) or adds a reader between `:1439` and `:1518`. The whole suite stays green and
  the medium N1 defect returns.
- Fix: use the existing headless harness (`P740FlightInputTests.cs:20-34`: `Headless`, `Set`, `Call`). Set a
  non-empty frame through `Set`, `Call(game, "DisarmJoystick")`, then assert
  `Get<JoystickBindingFrame>(game, "_targetJoystickFrame").HeldMask == 0` (or that
  `Call`-reached `JoystickHoldsAction` is false). Keep `DefaultFrame_HoldsNoAction`.

### R5-2 — Low — the note still states the built-in profile binds button 79

- `knowledge/joystick-input.md:218-219` says, in the present tense, "The built-in throttle profile binds
  button 79 to the booster". That is false against `Flight/JoystickBindings.cs:179` (75) and is superseded
  two lines later by the new bullet (`:220-223`).
- Failure scenario: a reader (round 4 did exactly this) takes 79 as the current binding and probes or
  documents the wrong button.
- Fix: mark the sentence historical ("bound 79 until the 2026-11-26 move") or delete it; `:220-223` carries
  the current fact. `:184` ("reports 79 buttons") and `:227` ("button 79 held") are dated observations of the
  earlier capture, not current claims.

### R5-3 — Low — the DAG node's `updated` was not bumped

- `DAG.json:127` now records 2026-11-26 events, but the same node's `updated` at `:131` is still
  `"2026-11-25"`. The file's own rules block requires updating `status`, `next` and `updated` in the same PR.
  No staleness warning follows (the age is one day), but the record is inaccurate.
- Fix: set `"updated": "2026-11-26"`.

## Observations

- **Booster rebind to 75.** 75 is unique among the VPC profile's bound buttons (20–32 plus 21/31/75), the
  test throttle now has 76 buttons with 75 the highest bound index, and `IsPressed`
  (`Flight/JoystickBindings.cs:301-302`) bounds-checks, so a device with fewer buttons is safe.
  `ThrottleRestButtons_AreNotBound` presses 34/35/36/43, all inside 76. The test arms before pressing and
  checks the release, so it fails if arming or the profile entry regresses.
- **The booster is dropped on the undock frame too.** It is read from `CurrentJoystickFrame` at `:2121`,
  which re-resolves because `DisarmJoystick` sets `_joystickFrameTicks = -1` (`:59-63`). Only the copy is
  reset; the cached frame is invalidated, so the two mechanisms stay consistent.
- **Zeroing the copy does not zero the throttle.** The reset clears `Axes` in the copy as well, but nothing
  reads the copy's `Axes`; `HoldsAction` reads `HeldMask` only.
- **Outside the delta.** `_gKeyWasDown` (`Game1.cs:1311`) is the one autopilot latch that
  `PrimeInFlightKeyLatches` never primes, so a keyboard G held through the ready screen can fire the
  autopilot on the first flight frame. Pre-existing and not introduced here; noted for a separate issue, not
  a condition of this review.
- **Round 4 record.** The summary blob is untouched since `3f7b5805` (`git log -- <path>`); its sha256
  `f73db047…` matches the receipt, and the receipt's head, tree (`0baf0523…`), range (`8e225d2b..c6df4d73`,
  two commits, 10 files, +258/−25) all verify. The README dispositions match the code.
- **Upstream.** Hunks are small and local: one reset plus one summary, one profile comment and one binding,
  three test edits. No reformatting. Every touched index blob is LF (`w/crlf` on the `.cs` files is
  checkout-only). No machine path, serial, secret or non-noreply address appears in the added lines; the
  receipt stays path-free.

## Verified by execution

`git write-tree` = `9223facc…` and `git rev-parse HEAD` = `3f992646…` matched the frozen target;
`git status --short` was clean at entry. `git rev-parse c6df4d73^{tree}` = `0baf0523…`. `git diff --check`
clean. `git ls-files --eol` shows `i/lf` for all touched files. `bash scripts/publish-guard.sh ff8e8a64 HEAD`
→ OK (13 commits). `tools/check_data_boundary.py` → OK. `tools/check_memory.py` → OK, with only the expected
`recorded-by 'port'` warnings and the no-baseline note.

## Verified by reading only

All runtime behaviour: the `UpdateFlying` order, `CurrentJoystickFrame`'s tick cache, `Disarm`'s armed-array
reset, the three latch readers, the edge observers, the mouse path, and the tests' assertions. The author's
build and test run were not reproduced.

## Verification limits

No dotnet, game, replay or hardware run. The N1 conclusion rests on call order and struct-copy semantics,
not on an observed run in the game. In-headset/flight testing by ragesaq remains the downstream acceptance
step.