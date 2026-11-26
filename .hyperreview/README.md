# Review records

One folder per cross-family review, newest first. Each folder holds the reviewer's `summary.md` and a path-free
`receipt.md`. The reviewer's activity logs and wrapper state stay on the reviewing host (`GROUND_RULES.md` rule 3), and
`.gitignore` keeps them out. The route is set in `AGENTS.md`, "Cross-family review route".

## 2026-11-26: PR #22 VR HUD step 2, round 3

- Record: [summary](patch-review-2026-11-26T14-00-00Z-allegiancecore-vr-hud-step2-r3/summary.md), [receipt](patch-review-2026-11-26T14-00-00Z-allegiancecore-vr-hud-step2-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `6737eaa..0314c5e`, head `0314c5e`, tree `bb3df52`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS, no findings. The fixes from ragesaq's second headset look: markers as flat shapes facing the head,
  the original velocity marker art, and in VR the port's text lines off the target pane and a larger target preview.
  The reviewer found the flat game unchanged. Three notes, no code change:
  1. N1: the canted-eye test cannot fail on what its name says, because any fixed point triangulates to itself. The
     plane's orientation is carried by the other billboard test and the mutants; that `DrawVrMarker` uses the plane
     is shown by the VR run and the headset, not by a test. Left as stated.
  2. N2: the preview's enlargement is cached in the renderer during `Draw`, so a harness that paints the preview
     outside `Draw` after a VR frame would get 1.5 times. No shipped path does. Left as stated.
  3. N3: a marker is now skipped when an edge of its plane falls behind the eye. That needs the anchor about 80° off
     the eye's forward, far outside the image. Left as stated.

## 2026-11-26: PR #22 VR HUD step 2, round 2

- Record: [summary](patch-review-2026-11-26T08-10-00Z-allegiancecore-vr-hud-step2-r2/summary.md), [receipt](patch-review-2026-11-26T08-10-00Z-allegiancecore-vr-hud-step2-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `2e0a431..6737eaa`, head `6737eaa`, tree `93d8f6b`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. F1 to F4 closed and the F2 decline judged sound. The ROLL gate leaves the flat game unchanged.
  Fixed after round 2, prose only:
  1. N1, cosmetic: two note lines past the paragraph's wrap, rewrapped.
  2. Observation: the DAG node now says one suggested test was declined.
  3. Observation: the note's aim reticle line says "the same direction in both eyes", as F2 did for the block.
- Left as stated: the ROLL test checks the gate's predicate, not its call site in `HudRenderer.Draw`. Whether bank
  stays readable without the ROLL dial is for the headset pass.

## 2026-11-26: PR #22 VR HUD step 2, round 1

- Record: [summary](patch-review-2026-11-26T07-30-00Z-allegiancecore-vr-hud-step2-r1/summary.md), [receipt](patch-review-2026-11-26T07-30-00Z-allegiancecore-vr-hud-step2-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of the whole pull request, `f362cdf..2e0a431`, head `2e0a431`, tree `ab9e955`, in a standalone clone
  with no remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS, no code defect. The reviewer found the flat game's main pass equivalent to `master`
  by reading. Fixed after round 1, all low:
  1. F1: the `FlightBlock` comments said the block's angle covers the crop. It covers the 400 reference pixels; the
     crop is wider.
  2. F2: "both eyes see the same pixels" became "the same direction: no vergence disparity", in the note and in the
     test's name. Declined: a new test with separate off-centre projections. Each eye's pass uses its own projection,
     as the panel did before this pull request, and the existing test checks the direction.
  3. F3: the decision row "Horizon cues in VR" no longer says the block keeps speed and throttle.
  4. F4: the note names the two further row-basis sites, the debris field (`NativeScene`) and the sound listener
     (`SoundManager`). Their code and the listener's comment are left for the follow-up.
  5. Observation: Classic's ROLL dial stayed on the VR panel. It is now hidden in VR with the other horizon cues
     (`HudRenderer.RollOnMainPass`; test `A_vr_eye_frame_shows_no_roll_dial_and_a_flat_frame_keeps_it` fails when
     the gate is removed).

## 2026-11-26: PR #18 VR recenter button, round 3

- Record: [summary](patch-review-2026-11-26T07-00-00Z-allegiancecore-vr-recenter-r3/summary.md), [receipt](patch-review-2026-11-26T07-00-00Z-allegiancecore-vr-recenter-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Light delta patch review of `7d3b45f..d9522e2`, head `d9522e2`, tree `50417d1`, in a standalone clone with no
  remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS, no new findings. F4 fixed: the session's end is the only stop that leaves the presenter alive, and a
  press made while stopped is kept until the next begun frame.

## 2026-11-26: PR #18 VR recenter button, round 2

- Record: [summary](patch-review-2026-11-26T06-00-00Z-allegiancecore-vr-recenter-r2/summary.md), [receipt](patch-review-2026-11-26T06-00-00Z-allegiancecore-vr-recenter-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `90979ec..7d3b45f`, head `7d3b45f`, tree `b93ffbb`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. F1 to F3 fixed. Fixed after round 2:
  1. F4, low: the wait counts only frames of a running session, so a press made just before the session stopped
     could fire when it resumed, much later. The session's end now cancels a waiting press; a press made while it
     is stopped still applies when it begins. Test `SessionStop_DropsAWaitingPress_AndALaterPressWaitsAgain` fails
     when `Cancel` does nothing; the call at the session's end is read only. The class comment and the note say
     "frames of a running session".

## 2026-11-26: PR #18 VR recenter button, round 1

- Record: [summary](patch-review-2026-11-26T04-00-00Z-allegiancecore-vr-recenter-r1/summary.md), [receipt](patch-review-2026-11-26T04-00-00Z-allegiancecore-vr-recenter-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `7c6632b..90979ec`, head `90979ec`, tree `c4fe700`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. The reviewer found the space swap valid within the frame, the yaw-only math right and both calls on
  the game thread. Fixed after round 1, all low:
  1. F1: a runtime error while recentring, or a head pose that is not finite, is logged and leaves the view as it
     was; it no longer ends the VR session.
  2. F2: a press made while the head is not tracked waits up to 90 headset frames (about a second) for tracking
     instead of being lost (`VrRecenterRequest`, tests `PressWhileUntracked_WaitsThenRunsOut` and
     `TrackingReturns_TheWaitingPressIsCarriedOutOnce`).
  3. F3: test `HeadTurnedLeft90_FacesTheSpaceAlongMinusX` states the expected turn without the implementation's
     formula. A mirrored yaw fails it and the older tests; removing the countdown fails the wait test.
  - Observation kept for the headset test: a recenter also re-places the flat panel in front of the head's facing at
    the press.

## 2026-11-26: PR #16 target center by angle off the nose, round 3

- Record: [summary](patch-review-2026-11-26T04-00-00Z-allegiancecore-target-center-r3/summary.md), [receipt](patch-review-2026-11-26T04-00-00Z-allegiancecore-target-center-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `2384326..2165561`, head `2165561`, tree `68ee129`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. N1 to N4 fixed. Fixed after round 3:
  1. L1, low: the candidate pool's comment names both consumers and says `PickFrontTarget` skips the carrier, which
     stays in the pool.
  2. L2, low: tests `ExcludingTheCarrier_KeepsAnotherKindWithTheSameId` and `CarrierAsTheCurrentTarget_IsNotKept`.
     Each fails when its guard is broken (the kind check removed; the exclusion moved after the current-target test).

## 2026-11-26: PR #16 target center by angle off the nose, round 2

- Record: [summary](patch-review-2026-11-26T03-00-00Z-allegiancecore-target-center-r2/summary.md), [receipt](patch-review-2026-11-26T03-00-00Z-allegiancecore-target-center-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `ece6de1..2384326`, head `2384326`, tree `1cfcf6e`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. F1 to F3 fixed, with residuals in N2 and N4. Fixed after round 2 in `3c03315`:
  1. N1, medium: in a turret the carrier could be the first pick, while native's `FindTarget` never returns it.
     `PickFrontTarget` takes the ship to leave out; test `GunnersCarrier_IsNeverPicked` fails without it. The carrier's
     last update is the fallback when it has no render pose.
  2. N2, low: the artwork-gated live test now ranks with `PickFrontTarget` from the own ship's real pose; it passes.
  3. N3, low: the round 1 entry now says the parity citation was fixed.
  4. N4, low: the unit tests of `SelectCandidates` are retitled as the autopilot implicit-pick list.
  - Observations kept: a key press uses the previous frame's pose (one frame stale); native's rider without a turret
    is not modelled apart from a gunner (existing behaviour).

## 2026-11-26: PR #16 target center by angle off the nose, round 1

- Record: [summary](patch-review-2026-11-26T02-00-00Z-allegiancecore-target-center-r1/summary.md), [receipt](patch-review-2026-11-26T02-00-00Z-allegiancecore-target-center-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `3a3b6e1..ece6de1`, head `ece6de1`, tree `21598bb`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. All three findings fixed in `efebff1`:
  1. F1, medium: the record said the port reused the T key's list; it was the autopilot implicit pick's. The note and
     the test comment now name it.
  2. F2, medium: in a turret the pick used a frozen pilot pose. It now uses the carrier's interpolated pose and the
     turret aim, as native ranks from the carrier along the turret's facing. Checked by reading only.
  3. F3, low: the Controls label, a comment and the no-pick log line no longer say "reticle" or "in view".
  - Observations kept: the scoring matches `CosForward2` by construction; probes and treasures are newly offered and
    are valid order targets. The parity row's older `InputActionCatalog.cs:161` citation was fixed to `:199` in
    `2384326`.

## 2026-11-26: PR #14 joystick bindings for a split HOTAS, round 5

- Record: [summary](patch-review-2026-11-26T05-00-00Z-allegiancecore-hotas-bindings-r5/summary.md), [receipt](patch-review-2026-11-26T05-00-00Z-allegiancecore-hotas-bindings-r5/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `c6df4d7..3f99264`, head `3f99264`, tree `9223fac`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. N1 and N2 fixed; the reviewer found every reader of the targeting copy after the
  undock disarm sees an empty frame. Fixed after round 5, all low:
  1. R5-1: test `DisarmJoystick_EmptiesTheTargetingCopy` calls the game's own reset with every command held; it
     fails when the reset line is removed.
  2. R5-2: the note's sentence about button 79 is marked as the binding before the move.
  3. R5-3: `DAG.json` node `ragesaq-006` `updated` is 2026-11-26.
  - Observation kept for a separate issue: a keyboard G held through the ready screen can toggle the autopilot on
    the first flight frame (`_gKeyWasDown` is not primed). Pre-existing, outside this pull request.

## 2026-11-26: PR #14 joystick bindings for a split HOTAS, round 4

- Record: [summary](patch-review-2026-11-26T01-00-00Z-allegiancecore-hotas-bindings-r4/summary.md), [receipt](patch-review-2026-11-26T01-00-00Z-allegiancecore-hotas-bindings-r4/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `8e225d2..c6df4d7`, head `c6df4d7`, tree `0baf052`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. R3-2 and R3-3 fixed. R3-1 only partly: round 3's entry below says the undock
  disarm "drops the frame already resolved for that update", which held for the cached frame but not for the copy
  the targeting keys read. Fixed after round 4:
  1. N1, medium: the targeting keys' copy of the joystick frame is taken before the docked check, so on the undock
     frame a held autopilot, clear-target or target-center button could still act. `DisarmJoystick` now also resets
     that copy to an empty frame. Test `DefaultFrame_HoldsNoAction`; the call site is read only.
  2. N2, low: the resolver's class comment still said the throttle is inverted like native. It now says the lever
     is read as the device sends it, and how a throttle at Virpil's own direction is set up.
  - Observations kept for the flight test: a lever left at the detent takes the throttle role only after it moves
    (native's latch works the same way); the action filter branch and the `Game1` glue are covered by reading only.

## 2026-11-25: PR #14 joystick bindings for a split HOTAS, round 3

- Record: [summary](patch-review-2026-11-26T00-30-00Z-allegiancecore-hotas-bindings-r3/summary.md), [receipt](patch-review-2026-11-26T00-30-00Z-allegiancecore-hotas-bindings-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `a25c734..8e225d2`, head `8e225d2`, tree `4136c7e`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Round 2's N1 is fixed. New, all low, all fixed after round 3:
  1. R3-1: only the launch from the ready screen disarmed the joystick, not an undock from the hangar, so a
     button held through the undock could ripcord, drop chaff or toggle autopilot on the first flight frame. The
     first frame after a docked one now disarms the joystick and drops the frame already resolved for that update.
     Test `DisarmThenResolveAgain_SameSamples_DropsTheHeldButton` covers the resolver half; the call site is read
     only.
  2. R3-2: two comment defects (a doc comment attached to the wrong member, and "the others are held" read as
     covering the targeting commands). Both corrected.
  3. R3-3: the parity row's list of bindable commands was stale. It now names the targeting commands and says the
     5-way switches are bound as buttons.
  - Observations acted on: a joystick targeting press now respects the action filter a training lesson sets on the
    keys (the reviewer noted the autopilot button bypassed it); the held-mask test checks each value's bit instead
    of the count; the flight-commands test also covers `DropChaff` and `FireMissile`.
  - Left as is: the autopilot key's cloak-key reservation in training concerns the native G key, not a joystick
    button.

## 2026-11-25: PR #14 joystick bindings for a split HOTAS, round 2

- Record: [summary](patch-review-2026-11-25T23-40-00Z-allegiancecore-hotas-bindings-r2/summary.md), [receipt](patch-review-2026-11-25T23-40-00Z-allegiancecore-hotas-bindings-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `b32f0e6..a25c734`, head `a25c734`, tree `7ffb38f`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Round 1: F1, F2 and F4 fixed; the reviewer agrees with the F3 dispute; F5 mostly
  fixed, with the `Game1` glue (the reset call, the strafe bits, the ripcord edge) covered by reading only. New:
  1. N1, low: the ripcord latch was primed from the last flight's cached joystick frame, so a thumb button held
     through launch could ripcord on the first flight frame. Fixed after round 2: the prime no longer reads the
     frame, and entering flight calls `JoystickBindingResolver.Disarm`, so a held button acts only after a release.
     The chaff button, added after round 2, had the same path and gets the same fix. Test
     `Disarm_HeldButtonActsOnlyAfterARelease`.
  - Wording gaps acted on: the parity row's fallback sentence and the note now say the fallback applies only while
    no profile has ever matched, and the note describes the ripcord and chaff buttons with the disarm on launch.

## 2026-11-25: PR #14 joystick bindings for a split HOTAS, round 1

- Record: [summary](patch-review-2026-11-25T22-50-00Z-allegiancecore-hotas-bindings-r1/summary.md), [receipt](patch-review-2026-11-25T22-50-00Z-allegiancecore-hotas-bindings-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `ff8e8a6..b32f0e6`, head `b32f0e6`, tree `83727f8`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Findings and dispositions:
  1. Medium: losing the throttle device while the stick stays froze the throttle at 0 and shut out the keyboard
     throttle. Fixed: the frame reports `ThrottleBound`, and the flight path resets the throttle latch when it goes
     false. Tests: `ThrottleBound_FollowsTheThrottleDevice`, `ThrottleUnplugged_ResetGivesTheKeyboardThrottleBack`.
  2. Medium: `null` entries in `joystick.json` parsed silently and crashed the first flight update. Fixed: `Parse`
     rejects them with a reason, rejects numeric enum values and bad thresholds, and the file read catches the
     other read failures. `BadFile_IsRejectedWithAReason` gains the cases; with the null check removed, the case
     fails.
  3. Low: armed state could survive one device leaving while another arrives in the same update. Disputed with
     evidence: the arriving device's key enters the table before the prune, so a departed key always makes the
     table larger than the set seen. With the round 1 condition restored, the new test
     `DeviceSwappedInOneUpdate_StillForgetsTheArmedState` passes. The prune is now written by membership anyway,
     which is simpler, and the test stays.
  4. Low: after the HOTAS goes away, the fallback could read leftover pedals as a stick. Fixed: the fallback is used
     only while no profile has ever matched. Test `AfterAProfileMatched_LeftoverPedalsAreNotReadAsAStick` fails
     without the fix.
  5. Low: untested paths. Fixed: tests for the button-count change, the swap, the throttle loss and the pedals.
## 2026-11-25: PR #17 VR HUD bracket depth, round 5

- Record: [summary](patch-review-2026-11-26T04-30-00Z-allegiancecore-vr-hud-bracket-depth-r5/summary.md), [receipt](patch-review-2026-11-26T04-30-00Z-allegiancecore-vr-hud-bracket-depth-r5/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `1a3adf7..dec2b47`, head `dec2b47`, tree `be9f663`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS, no findings. R4-F1 is closed: the lock's lookup gives the same radius per type as the removed switch,
  in the same lock. Its test stays deferred, which the reviewer accepted as honestly stated residual risk. R4-F2 is
  closed. Observations 1 and 2 were wording points about the design note, taken in the commit that records this
  review (prose only): "in front of the near side" holds while the head is outside the object, and a test of the
  lookup also needs a game instance with its hull and station catalogues.
- Across five rounds the findings went from one High and one Low, to one High, to one Medium and two Low, to two Low,
  to none.

## 2026-11-25: PR #17 VR HUD bracket depth, round 4

- Record: [summary](patch-review-2026-11-26T04-00-00Z-allegiancecore-vr-hud-bracket-depth-r4/summary.md), [receipt](patch-review-2026-11-26T04-00-00Z-allegiancecore-vr-hud-bracket-depth-r4/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `78e9be3..1a3adf7`, head `1a3adf7`, tree `badfd07`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS, no source defect. Round 3's N1, N2 and N3 are closed, and the reviewer confirmed the
  author's correction to round 3's example (the 100 m clamp). Findings:
  1. Low (R4-F1): the lock repeated the selection's radius switch, and no test reaches the lookup from a lock or
     selection to the marker's radius. Partly taken: the lock now uses the selection's lookup (`TargetWorldRadius`), so
     there is one switch. The test is deferred, by reading only: it needs a game world holding one object of each
     type, and the design note records the gap.
  2. Low (R4-F2, records): corrected. The round 3 entry now says the test uses radii standing for a ship, a hull and a
     station, not the type lookup; the note says only a marker with no radius can be hidden, within 100 m.

## 2026-11-25: PR #17 VR HUD bracket depth, round 3

- Record: [summary](patch-review-2026-11-26T03-30-00Z-allegiancecore-vr-hud-bracket-depth-r3/summary.md), [receipt](patch-review-2026-11-26T03-30-00Z-allegiancecore-vr-hud-bracket-depth-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `e2825d5..78e9be3`, head `78e9be3`, tree `2b087f3`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: WARN. The round 2 fix closed the defect for ships. Findings, all accepted and fixed:
  1. Medium (N1): the lock passed a radius only for ships, so a locked station, asteroid or warp kept the defect.
     Fixed: the lock takes the radius for every lockable type, as the flat lock flower does. One correction to the
     reviewer's example: an Outpost locked 1.2 km away is not hidden, because the anchor's 100 m clamp keeps it in
     front of a near surface 700 m away. The defect needs the near surface within 100 m, such as a 500 m radius
     station about 590 m from the head; the new test uses that case.
  2. Low (N2): the round 2 test did not run the world-space path. Fixed: `VrHudGeometry.MarkerAnchorInWorld` is the
     whole anchor calculation, `VrMarkerAnchor` only calls it, and a new test runs it with the ship's centre and a head
     14 m ahead for radii of 20, 15 and 500 m (sizes standing for a ship, a locked hull and a station; the test does not
     run the lock's type lookup), and checks that without the radius each would be inside.
  3. Low (N3): the reticle cone's comment named the bracket. Fixed: it names the anchor angle.
  - Observation 1 taken: a marker with no radius is anchored from its drawn angle alone, so the lead marker is exactly
    as before (new test).

## 2026-11-25: PR #17 VR HUD bracket depth, round 2

- Record: [summary](patch-review-2026-11-26T03-00-00Z-allegiancecore-vr-hud-bracket-depth-r2/summary.md), [receipt](patch-review-2026-11-26T03-00-00Z-allegiancecore-vr-hud-bracket-depth-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `47133cb..e2825d5`, head `e2825d5`, tree `c9fe1ba`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: WARN. F2 closed. Findings:
  1. High (N1, round 1 F1 still open): the selection and lock measured their wrap angle from the ship's centre and the
     anchor applied it from the head, which the cockpit camera puts 14 m ahead. A target dead ahead inside about
     300 m was still anchored inside its hull (a 30 m hull locked 85 m away: 1.8 m inside), and the round 1 test used
     one vector for both, so it could not see it. Accepted and fixed for ships (round 3 found the lock passed a radius
     only for ships): the selection and lock carry the object's world radius, and `VrHudGeometry.AnchorAngle` measures its wrap along the head-relative vector the anchor uses. New
     test at that seam, with the reviewer's numbers (40 m from the ship, 26 m from the head, 20 m radius; and the
     locked 30 m hull); it fails when the radius term is dropped.
  - F2 wording: the note now says the probe bounds any difference between the sprite path's and the scene's depth to
    the probe's window, not that they are equal, and that the contacts it lands on vary from run to run.
  - Observations taken: the reticle's cone now uses the same anchor angle, so a large object's hull under the reticle
    gives it depth even when its bracket is capped (new test); the contact anchor's limit near very close asteroids
    (the radar caps the screen radius) is recorded in the note.

## 2026-11-25: PR #17 VR HUD bracket depth, round 1

- Record: [summary](patch-review-2026-11-26T02-30-00Z-allegiancecore-vr-hud-bracket-depth-r1/summary.md), [receipt](patch-review-2026-11-26T02-30-00Z-allegiancecore-vr-hud-bracket-depth-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `051f0cc..47133cb`, head `47133cb`, tree `189b079`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. The reviewer confirmed by reading that the eye pass keeps the scene's depth in the eye
  target, that the marker depth never leaks into the panel or a flat frame, the off-view test and ring, and the
  reticle's boresight, easing and reset. Findings:
  1. High (F1): a bracket capped at 20 degrees was anchored from the capped angle, so near a large object (an Outpost
     inside about 1.7 km, a 30 m ship locked inside about 85 m) the anchor fell inside the hull and the depth test hid
     the marker. Accepted; this first fix was incomplete (round 2, N1): each marker carries its object's uncapped wrap angle
     (`VrHudGeometry.WrapAngle`, `WrapAngleFromScreenRadius`), and the anchor and the reticle's depth use it; the drawn
     size and the reticle cone keep the cap. New test with the reviewer's capped case (40 m away, 20 m radius); it fails
     when the wrap is capped again.
  2. Low (F2): the depth convention rested on one far-tail probe, and no test pinned `BufferDepth`. Accepted: a test
     pins it to the perspective formula at the near plane, two mid distances and the far plane (it fails under a
     `0.5 z + 0.5` remap), and a second GPU probe tested depth at each radar contact (`knowledge/vr-hud-design.md`,
     "The layer 2 follow-up as built").
  - Observations: the 100 m clamp also limits occlusion (now said in the note); a tuned panel farther than a marker
    still draws over it (noted); the reticle state is zeroed in `ReleaseVrHud`; two coincident arrows for a selection
    that is also locked, and an arrow in both eyes when only one eye loses the marker, are left for the headset look.

## 2026-11-25: PR #15 VR HUD step 1, round 2

- Record: [summary](patch-review-2026-11-26T00-45-00Z-allegiancecore-vr-hud-in-eyes-r2/summary.md), [receipt](patch-review-2026-11-26T00-45-00Z-allegiancecore-vr-hud-in-eyes-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta patch review of `1cf7172..d1b87f9`, head `d1b87f9`, tree `f9b6dea`, in a standalone clone with no remote.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS, no findings. All five round 1 findings closed; F1 as a documented limit, the empty VR radar being
  ragesaq's product call. The reviewer reproduced the off-centre geometry independently. Observations:
  1. O1: a square bracket is sized up and down, so 45° to the side it is about 29% narrow. Accepted for step 1 and
     named in the note.
  2. O2: the doc comment's "1.4 times at 45°" holds sideways only (twice upward). Reworded.
  3. O3: the contact brackets are shared by every skin, not only Classic and Modern. Note reworded.
  4. O4: at the 20° cap the three brackets are the same size and do not nest. Note says so.

## 2026-11-25: PR #15 VR HUD step 1, round 1

- Record: [summary](patch-review-2026-11-26T00-30-00Z-allegiancecore-vr-hud-in-eyes-r1/summary.md), [receipt](patch-review-2026-11-26T00-30-00Z-allegiancecore-vr-hud-in-eyes-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `3a3b6e1..1cf7172`, head `1cf7172`, tree `1531112`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. The reviewer confirmed the marker geometry against the game's projection convention,
  the per-eye disparity, that no flat path reaches the new code, that the cleared marker fields are read only by the
  HUD draw, the OpenXR layer handling, and that no upstream file was reformatted. Findings:
  1. High (F1): in VR the panel's radar draws no contacts, and the note said the Jet scope kept the in-frame ones.
     Partly accepted: the note was wrong and is corrected. The panel stays clear, because the Classic and Modern
     radars draw contacts as screen-space brackets that would double the world markers (the doubled elements of the
     milestone 2 headset test). The empty Jet scope is a step-1 limit for the cockpit panels (layer 4).
  2. Low (F2): the selection and lock brackets could exceed the 20 degree cap. Fixed: clamped after scaling.
  3. Low (F3): marker size used the image centre's scale everywhere. Fixed: `VrHudGeometry.PixelsForAngleAt` takes
     the size at the marker's own pixel; lines and labels follow. New test checks the covered angle at the centre and
     off it, and fails when the size falls back to the centre's.
  4. Low (F4): `ReleaseVrHud` kept the panel effect and the pixel texture. Fixed: both are disposed.
  5. Low (F5): the wide-frame radar run overwrote the radar proof line. Fixed: saved and restored around it.
  - Test gap named by the reviewer, a point behind the eye: added.

## 2026-11-25: PR #11 sky at infinity in each eye, round 1

- Record: [summary](patch-review-2026-11-25T22-00-00Z-allegiancecore-sky-stereo-r1/summary.md), [receipt](patch-review-2026-11-25T22-00-00Z-allegiancecore-sky-stereo-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `8ccacfa..9db16a4`, head `9db16a4`, tree `72c2a09`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS, no findings. The reviewer checked the projection algebra, the depth range of every sky element, that
  no sky code assumes a symmetric projection, and that no flat path can reach the new branch. Observations:
  1. A future flat projection jitter through `M31`/`M32` would reach the sky; desirable, worth a comment then. No
     action now.
  2. The off-centre test covers one eye; the copy is sign-symmetric, so the mirrored eye follows. Answered, no
     change: the reviewer's own reading covers it, and a second eye's matrix adds no new path through the code.
  3. The symmetric test checks values, not the early return; the two branches agree for symmetric input. No action.
  4. Node `ragesaq-009` should name PR #11 and the reviewed head. Done in the commit that records this review.

## 2026-11-25: PR #9 VR milestone 2, closeout

- Record: [summary](closeout-review-2026-11-25T11-10-00Z-allegiancecore-vr-m2/summary.md), [receipt](closeout-review-2026-11-25T11-10-00Z-allegiancecore-vr-m2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Closeout-verification of the whole pull request, `f945af7..36989f0`, head `36989f0`, tree `81e8dd2`, in a standalone
  clone with no remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS for the headset test. The ledger of rounds 1 and 2 is closed, deferred with an owner, or answered. The
  reviewer re-checked that the upstream seam keeps its bytes (`--ignore-all-space` numstat agrees) and that no
  upstream file was renormalized. New:
  1. Low (N1): creating the eye render targets inside `Draw` (`Game1.Vr.cs`) has no catch, so a throw there ends the
     game instead of running flat. Deferred: node `ragesaq-008` in `DAG.json` (Issues are turned off on this fork).
     Opt-in `--vr` only, never observed.
  - Its merge gates: PR #7 first (merged as `1c521ad`), then a green Linux `build-and-test` on the final head, which
    did not run while this PR was based on `port/vr-m1`.

## 2026-11-25: PR #9 VR milestone 2, round 2

- Record: [summary](patch-review-2026-11-25T10-50-00Z-allegiancecore-vr-m2-r2/summary.md), [receipt](patch-review-2026-11-25T10-50-00Z-allegiancecore-vr-m2-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch,
  including a first attempt that failed on the provider connection before the review began.
- Delta review of `3677ec6..917f29b`, head `917f29b`, tree `a928ba9`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. Round 1's F2 to F4 and the swapchain limit are closed; the F1 dispute is accepted on the OpenXR
  specification. New:
  1. Low: the `ReadEyeSize` comment in `OpenXrPresenter.cs` still described one side-by-side swapchain. Fixed
     (comment only).
  - Notes, no action: the new test covers `GatherDust`'s `advance` flag but not the `advance: !VrRepeatPass` call
    site; `EnsureSwapchain` would leak a swapchain handle if image enumeration failed after a successful create
    (milestone 1 code, never observed).

## 2026-11-25: PR #9 VR milestone 2, round 1

- Record: [summary](patch-review-2026-11-25T08-09-00Z-allegiancecore-vr-m2-r1/summary.md), [receipt](patch-review-2026-11-25T08-09-00Z-allegiancecore-vr-m2-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Patch review of `f945af7..3677ec6`, head `3677ec6`, tree `52add9f`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Findings and dispositions:
  1. Medium: the HUD quad's `BLEND_TEXTURE_SOURCE_ALPHA_BIT` would premultiply an already-premultiplied buffer.
     Disputed: the OpenXR specification says the bit only enables the layer's alpha (unset, the alpha is one), and
     colour is taken as premultiplied unless `XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT` is set. No change; the
     HUD's edges are on the headset checklist.
  2. Low: the native debris field stepped twice per frame in stereo. Fixed: `NativeScene.GatherDust` takes
     `advance`, false on the second eye; `VrScenePassTests` covers it and was seen to fail without the fix.
  3. Low: the eye render targets outlived a dropped VR sink. Fixed: released when the host is no longer attached.
  4. Low: the opt-in aleph glimpse would land on the HUD layer. Fixed by skipping it in stereo frames, documented.
  - Observation: the HUD quad is fixed in the local space, so it does not follow the head. Kept for milestone 2 and on
    the headset checklist.
  - Limit acted on: the side-by-side eye swapchain was twice the recommended width. Each eye now has its own
    swapchain at the recommended size.

## 2026-11-25: PR #7 VR milestone 1, closeout

- Record: [summary](closeout-review-2026-11-25T04-14-00Z-allegiancecore-vr-m1/summary.md), [receipt](closeout-review-2026-11-25T04-14-00Z-allegiancecore-vr-m1/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Closeout-verification of the whole pull request, `47e720b..f36ee31`, head `f36ee31`, tree `4f34e85`, in a standalone
  clone with no remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
  The wrapper state shows status complete and assurance `independent_cross_family`; its served-model field was empty
  this time, and the session banner and the locked route name `deepseek-v4.1-flash`.
- Verdict: PASS-WITH-CONDITIONS, ready for the in-headset test. The ledger shows every round 1 to 4 finding closed,
  except round 1 finding 5, rejected with its narrower benchmark case kept as residual risk. Four low items.
  Dispositions (in the commit with this record):
  - Low 1, an acquired swapchain image is not released when `xrWaitSwapchainImage` fails: not taken, with evidence.
    The OpenXR 1.0 page for `xrReleaseSwapchainImage` says the runtime must return `XR_ERROR_CALL_ORDER_INVALID` if
    no image has been waited on, so a release after a failed wait would itself fail and would replace the original
    error. The page for `xrDestroySwapchain` sets no rule about acquired images; its only requirement is that GPU
    work referring to the swapchain has completed. The code therefore releases only after a successful wait, which
    is what round 1's summary meant by the release in the `finally`.
  - Low 2, `GLOSSARY.md` had no VR entry, which the fork's `AGENTS.md` asks for when a system is added: fixed. A
    layout row and a section 16 name the options, the seam, the presenter and the tests;
    `tools/check-glossary-links.py` passes.
  - Low 3, the survey note did not say what the flat game keeps after a fallback: fixed. It now says the fixed step
    and the sleep come back but vsync stays off until restart, so the flat game can run uncapped. Its status line
    now names the review and closeout.
  - Low 4, the gate's "new tests 3x alone" was not reported: done. The 24 VR tests ran three times alone at
    `e71e079` (the code is unchanged since), passing 24 of 24 each time. The same gate's `tools/build-tools.sh`,
    which the closeout did not name, was also run: all 155 tool projects built at e71e079 (the code is unchanged since), with no warnings or errors.
  - The two other `AGENTS.md` rules for new systems do not apply here: `docker-compose.yml` lists services, and the
    existing desktop-client variable `ALLEGIANCE_UPDATE_FEED` is not in it either; Dockerfiles belong to server apps,
    and the VR project is a client library like `Allegiance.ClientCore.Game`.
  - The closeout's merge conditions stand: the Linux `build-and-test` job green on the head, and ragesaq's approval
    at the reviewed head. These fixes are prose only, so the record is re-recorded with them and no delta review
    is needed.

## 2026-11-25: PR #7 VR milestone 1, round 4

- Record: [summary](patch-review-2026-11-25T04-04-00Z-allegiancecore-vr-m1-r4/summary.md), [receipt](patch-review-2026-11-25T04-04-00Z-allegiancecore-vr-m1-r4/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta review of `969988d..e71e079` (the round 3 record and fix), head `e71e079`, tree `283fb41`, in a standalone
  clone with no remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
  The wrapper state shows the served model `deepseek-v4.1-flash`, effort `high`, status complete, assurance
  `independent_cross_family`. The clone's refs were the same before and after the run.
- Verdict: PASS, no findings. Round 3 findings 1 and 2 closed, and the reason for not sending an empty `xrEndFrame`
  judged sound. The reviewer found that a context failure inside `SkipFrame` leaves no frame open and reaches the
  flat fallback, and that nothing in the delta blocks the in-headset test. Residual risks it names: the skip paths
  are unexercised, and the accepted call-order fallback to flat. The summary calls the presenter's `SubmitFrame`
  method `Present`; the line numbers it cites are `SubmitFrame`'s.

## 2026-11-25: PR #7 VR milestone 1, round 3

- Record: [summary](patch-review-2026-11-25T03-50-00Z-allegiancecore-vr-m1-r3/summary.md), [receipt](patch-review-2026-11-25T03-50-00Z-allegiancecore-vr-m1-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta review of `b3a8ea3..969988d` (the round 2 record and fixes), head `969988d`, tree `e693147`, in a standalone
  clone with no remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
  The wrapper state shows the served model `deepseek-v4.1-flash`, effort `high`, status complete, assurance
  `independent_cross_family`. The clone's refs were the same before and after the run.
- Verdict: PASS-WITH-CONDITIONS. Round 2 findings 1 to 3 closed. The reviewer agreed with keeping the session flag
  for round 2's finding 2 and withdrew its suggestion to clear it. Two low findings. Dispositions (the fix in the
  commit after this record, re-reviewed in round 4):
  - Low 1, the two skip returns after `xrWaitFrame` and `xrBeginFrame` did not restore the game's GL context before
    the game presents: fixed. `SkipFrame` restores the context, then sleeps, so every skip path restores it.
  - Low 2, the round 2 entry said the runtime discards a frame left begun when `xrEndFrame` reports the session not
    running, which is not established: fixed in the record. The round 2 entry now states the consequence the
    reviewer names: if a runtime enforces the frame-call order, the next `xrWaitFrame` fails and VR falls back to
    flat for the rest of that run, with the reason logged. That is a safe failure, it needs the STOPPING event to
    be late or absent, and it is kept as residual risk. The reviewer's other fix, a best-effort empty `xrEndFrame`
    before the next wait, is not taken: the session is not running, so that call would return the same error.
  - Note, `SkipFrame` and the idle path both sleep the idle interval: kept as two call sites of one constant, since
    `SkipFrame` now also restores the context.

## 2026-11-25: PR #7 VR milestone 1, round 2

- Record: [summary](patch-review-2026-11-25T03-33-00Z-allegiancecore-vr-m1-r2/summary.md), [receipt](patch-review-2026-11-25T03-33-00Z-allegiancecore-vr-m1-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged. The receipt is Port's path-free record of the launch.
- Delta review of `30c2ae6..b3a8ea3` (the round 1 record and fixes), head `b3a8ea3`, tree `ad3bbde`, in a standalone
  clone with no remote. Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
  The wrapper state shows the served model `deepseek-v4.1-flash`, effort `high`, status complete, assurance
  `independent_cross_family`. The clone's refs were the same before and after the run.
- Verdict: PASS-WITH-CONDITIONS. Round 1 findings 1 to 4 and 6 to 8 closed, finding 5's disposition right. Three low
  findings. Dispositions (fixes in the commit after this record, re-reviewed in round 3):
  - Low 1, the context check before the copy sat outside the `try` that ends an empty frame: fixed. The check is now
    the first statement inside it.
  - Low 2, the `XR_ERROR_SESSION_NOT_RUNNING` skips bypassed the idle sleep, so the uncapped loop could spin: fixed.
    Each skip (after `xrWaitFrame`, `xrBeginFrame` or `xrEndFrame`) sleeps the idle interval. The session flag is
    left alone on purpose: the application ends a session itself, on the runtime's STOPPING event, so clearing the
    flag without that event could leave VR idle for the rest of the run, while a sleep keeps retrying at the idle
    rate. The reviewer's related residual risk (a frame begun and not ended when `xrEndFrame` returns that error) is
    kept. Round 3 corrected this line, which first said the runtime discards the frame: that is not established. If
    a runtime enforces the frame-call order instead, the next `xrWaitFrame` fails and VR falls back to flat for the
    rest of that run, with the reason logged.
  - Low 3, round 1's line numbers for finding 5 were off by one: fixed in the round 1 entry (`Game1.Benchmark.cs:199`,
    `:452`, `:467`).
  - Note, nothing asserted the inactive-window sleep after `KeepPacing` and a fallback: the test
    `Settings_applied_while_attached_are_what_the_fallback_restores` now does, and fails when `KeepPacing` overwrites
    the saved sleep. Note, re-attach after `Dispose` is untested: kept, `Dispose` runs only at shutdown. Note, the DAG
    node named only the round 1 head: it now names the round 2 head.

## 2026-11-25: PR #7 VR milestone 1, round 1

- Record: [summary](patch-review-2026-11-25T03-13-00Z-allegiancecore-vr-m1-r1/summary.md), [receipt](patch-review-2026-11-25T03-13-00Z-allegiancecore-vr-m1-r1/receipt.md)
- The committed summary and receipt are the reviewer's text, unchanged.
- Reviewed head `30c2ae6`, tree `37638b8`, the range `47e720b..30c2ae6`, in a standalone clone with no remote. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0. The wrapper state shows the
  served model `deepseek-v4.1-flash`, effort `high`, status complete, assurance `independent_cross_family`. The clone's
  refs were the same before and after the run (listing sha256 `8c513e17bfd61405eaa851e5379727c9e43f2b1a6fae68170a5f07038f4ec04b`).
- Verdict: PASS-WITH-CONDITIONS, one low-medium and seven low findings. Dispositions (fixes in the commit after this
  record, re-reviewed in round 2):
  - Low-Medium 1, the copy cleared GL errors the game's own check watches for: fixed. `GlBlitter` logs the codes it
    clears (the first five batches, then one line saying it stops).
  - Low 2, the GL context was not checked before the first copy after swapchain creation: fixed. The presenter checks
    it before every copy and before creating the blitter.
  - Low 3, a begun frame was left open when the copy failed: fixed. An empty frame is ended before the teardown.
  - Low 4, `XR_ERROR_SESSION_NOT_RUNNING` from the frame calls ended VR: fixed. That result now skips the frame; the
    runtime's STOPPING event follows.
  - Low 5, the saved flat pacing has several writers: not a defect in the scenario given. A benchmark suggestion with
    no frame cap is applied through `ApplyBenchmarkSettings`, which calls `KeepVrPacing`, so the value saved is the
    one the flat game has at that moment; the benchmark's end re-applies the original settings the same way
    (`Game1.Benchmark.cs:452,467`; round 2 corrected these and the next line number). The finding does hold for a narrower case: `BeginBenchmark` sets the fixed step
    directly (`Game1.Benchmark.cs:199`), so a VR failure between that line and the next `ApplyBenchmarkSettings`
    restores the pre-benchmark step for the rest of that run. Benchmark-only, it needs a VR failure mid-run, and it
    ends at the run's next settings change, so it is left as residual risk.
  - Low 6, no test covered what the flat game is left with: fixed. The attach, submit, drop and pacing logic moved
    from `Game1.Vr.cs` into `Vr/VrFrameSinkHost.cs` behind a small pacing interface, with 7 tests
    (`VrFrameSinkHostTests`). Two of them fail when the fallback stops restoring the fixed step.
  - Low 7, DAG `ragesaq-002` named no pull request or head: fixed. Status `review`, PR #7 linked, heads named.
  - Low 8, the survey note said any failure logs `[vr] running flat:`: fixed; it now names both log lines.
    `memory/mem-009` now names the environment it was seen in.
  - Note, `IVrFrameSink` said "must not throw" while the game catches what a sink throws: the doc comment now says the
    game drops a sink that throws. Note, `GlBlitter` frees the SDL handle it resolved from: kept, it matches
    `HdCompressedUpload` and MonoGame keeps SDL2 loaded.

## 2026-11-25: PR #5 agents merge and change settings on operator approval, round 4

- Record: [summary](patch-review-2026-11-25T02-00-00Z-allegiancecore-approval-r4/summary.md), [receipt](patch-review-2026-11-25T02-00-00Z-allegiancecore-approval-r4/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `326f945`, tree `0c76fc1`, the delta `309921b..326f945`, in a standalone clone. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. All three round 3 findings are closed. Rule 4 read cold: "including these" reads as
  examples, and the new Issue sentence adds a place to record, not authority. Dispositions:
  - Low 1, the PR body named only the `line-endings` warning: fixed in the body, which now names both warnings
    `memory/mem-001` documents.
  - Low 2, the round 3 summary calls its range three commits: it holds four, with PR #4's merged `610ef8c`. Fixed in
    the round 3 entry above; the summary stays the reviewer's text.
- Both fixes are records only, made in this record commit, so no round 5 runs: `AGENTS.md`'s gates let prose-only
  changes skip review.

## 2026-11-25: PR #5 agents merge and change settings on operator approval, round 3

- Record: [summary](patch-review-2026-11-25T01-30-00Z-allegiancecore-approval-r3/summary.md), [receipt](patch-review-2026-11-25T01-30-00Z-allegiancecore-approval-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `309921b`, tree `8cd1ba7`, the delta `e4e159a..309921b`, in a standalone clone: three authored
  commits plus PR #4's `610ef8c`, which the merge brought in (the summary says three; round 4 below). Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. All five round 2 findings are closed (New 5 in substance). Dispositions:
  - Low 1, the PR body said `check_map.py` passes without saying where: fixed in the body. The checks ran in the
    author's clone, where `origin/master` exists; in a clone without it, the node-loss gate cannot compare.
  - Low 2, "any build and test gate they name" had no clear antecedent and left out committing the summary: fixed.
    Rule 4 now says "each build and test gate listed there passes" and that the review result "is committed and
    posted as a comment on the pull request".
  - Low 3, the round 2 receipt's read-only check had no evidence: fixed. That receipt now says the check was
    author-attested and the listing was not kept; this round's receipt records the listing's digest.

## 2026-11-25: PR #5 agents merge and change settings on operator approval, round 2

- Record: [summary](patch-review-2026-11-25T00-45-00Z-allegiancecore-approval-r2/summary.md), [receipt](patch-review-2026-11-25T00-45-00Z-allegiancecore-approval-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `e4e159a`, tree `1241018`, PR #5's own delta `0b032b7..e4e159a`, in a standalone clone. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. All six round 1 findings are closed. Dispositions:
  - New 1 (medium), "recorded on the pull request" was ambiguous and PR #5 had no review comment: fixed. Rule 4 now
    says the result is posted as a comment on the pull request. The round 1 comment was posted after this review's
    snapshot was taken, and this round's is posted too.
  - New 2, the PR body described the round 1 text: fixed, the body now describes the rule as it stands.
  - New 3, `mem-004` was said to belong to PR #2: fixed, it merged with PR #1.
  - New 4, `mem-008`'s `seen-in:` named no runtime: fixed, it names Port (Claude Code, Claude Opus 5.5).
  - New 5, the gloss of "the review gates" left out the build gate: fixed, rule 4 now says any build and test gate
    the section names must pass as well.

## 2026-11-25: PR #4 publish guard fails closed, round 2

- Record: [summary](patch-review-2026-11-25T00-30-00Z-allegiancecore-failclosed-r2/summary.md), [receipt](patch-review-2026-11-25T00-30-00Z-allegiancecore-failclosed-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `0b032b7`, tree `39dfa27`, the delta `6528bdb..0b032b7` that answers round 1, in a standalone clone.
  Reviewer `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Round 1's Low 3 is closed and its Low 2 decline is sound. Dispositions:
  - Low 1, the root commit of a push that creates the default branch is not scanned: declined, recorded. The guard
    scans `base..head`, which cannot include a root commit; the same was true before PR #4. No push in this fork takes
    that path (`master` exists and GitHub does not let a default branch be deleted). Covering it needs a scan-all
    mode in the upstream author's script, which is outside this change.
  - Low 2, the round 1 records overstated the fixes: fixed. The round 1 entry, its receipt, `mem-007` and
    `ragesaq-004` now say Low 1 was fixed in part and Low 2 declined.

## 2026-11-24: PR #5 agents merge and change settings on operator approval, round 1

- Record: [summary](patch-review-2026-11-24T23-50-00Z-allegiancecore-approval-r1/summary.md), [receipt](patch-review-2026-11-24T23-50-00Z-allegiancecore-approval-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `1f6039c`, tree `af129d7`, the delta `6528bdb..1f6039c`. Reviewer `deepseek-v4.1-flash` via OpenCode
  Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Dispositions:
  - Medium 1, a merge did not wait for the review gates: fixed. Rule 4 now requires the required review recorded on
    the pull request and each finding fixed or answered there.
  - Medium 2, the rule did not say who may approve: fixed. Only ragesaq, in a comment on the pull request or directly
    to the merging agent; a relayed message, handoff note, Issue or file never counts. Settings approval comes the same
    way.
  - Low 3, closeout after a merge: fixed, the rule points at `DAG.json`'s closeout rule.
  - Low 4, live nodes waited on "ragesaq's merge": fixed for `ragesaq-003`; `ragesaq-004` already said "the operator's
    approval to merge" after PR #4's round 1 record, merged into this branch.
  - Low 5, `mem-008` class: fixed, now `witnessed` with `seen-in:`. `mem-006` (PR #2) and `mem-004` (merged with
    PR #1) carry the same looseness; they are left for a later change.
  - Low 6, `mem-008` scope sentence: reworded as suggested. The entry also records the operator's confirmation that
    approval covers only the one merge or settings change.

## 2026-11-24: PR #4 publish guard fails closed, round 1

- Record: [summary](patch-review-2026-11-24T20-40-00Z-allegiancecore-failclosed-r1/summary.md), [receipt](patch-review-2026-11-24T20-40-00Z-allegiancecore-failclosed-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `6528bdb`, tree `3bb456d`, the delta `ff20f84..6528bdb`. Reviewer `deepseek-v4.1-flash` via OpenCode Go
  at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. Dispositions, each checked in the author's harness against `6528bdb` and against the fix:
  - Low 1, a push that creates the default branch scanned nothing: fixed in part. The commits after the root commit
    are scanned again, as before PR #4; the root commit itself is not (round 2 below).
  - Low 2, a branch with no history in common with the default branch failed: kept failing closed, now with a clear
    message. The suggested root-commit fallback was not taken: `root..HEAD` leaves out the root commit, so a one-commit
    orphan with a bad commit would pass unscanned (the harness shows the old step doing exactly that).
  - Low 3, the branch name and default branch name were pasted into the shell: fixed. Both now reach the script through
    `env:`. A branch named `inj$(touch${IFS}INJECTED)` ran its command at `6528bdb` and does not after the fix.
  - Notes 1, 2, 4 and 6: confirmations, no action. Note 3 (a pull request whose base commit is missing from the clone
    over-scans from the default branch): no action. `base.sha` is the base branch's tip when the event fires, so the
    clone holds it, and when it does not, scanning more commits is the safe direction. Note 5 (`.githooks/pre-push`
    names `origin/master`): no action, it is the upstream author's hook and matches this fork's default branch.
- The reviewer's probes broke the read-only rule once: they committed two throwaway commits onto the author clone's
  local `master`, which the review worktree shares. Nothing reached GitHub; local `master` was reset from the reflog.

## 2026-11-24: PR #3 publish guard accepts GitHub's merge committer, round 1

- Record: [summary](patch-review-2026-11-24T19-40-00Z-allegiancecore-guard-r1/summary.md), [receipt](patch-review-2026-11-24T19-40-00Z-allegiancecore-guard-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `2ca409f`, tree `ccc2380`, the delta `b15f666..2ca409f`: the one-line identity rule in
  `scripts/publish-guard.sh`, `memory/mem-005` and work map node `ragesaq-003`. Reviewer `deepseek-v4.1-flash` via
  OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. The reviewer ran the author's harness (27 of 27) and 14 cases of its own over CRLF and LF copies.
  Dispositions:
  - Low finding, `mem-005` heading in the present tense contradicted its body: fixed in the next commit (prose only,
    so no re-review).
  - Note 1, the exception also passes a single-parent commit with GitHub as committer (a web edit): accepted, the
    address is GitHub's service identity.
  - Note 2, both `-x` and `-F` are needed: no action, both are present.
  - Note 3, pre-existing: an unresolvable base makes the guard print "no new commits" and pass (`publish-guard.sh:12`).
    Not in this change; reported to the operator as a separate fix.
  - Note 4, pin a head SHA in `ragesaq-003`'s `next`: declined. The head moves with every commit to the PR, so a SHA
    in its own record would be stale at once; the node links PR #3, which shows the current head.

## 2026-11-24: PR #1 HyperBench enrollment, round 3

- Record: [summary](patch-review-2026-11-24T09-17-24Z-allegiancecore-enrollment-r3/summary.md), [receipt](patch-review-2026-11-24T09-17-24Z-allegiancecore-enrollment-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `1122bce`, tree `76d3e5f`, the delta `3ca08ba..1122bce`: the round 2 record and the operator's
  decision that Socket commits here with its GitHub noreply address. Reviewer `deepseek-v4.1-flash` via OpenCode Go
  at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. Round 1 note 3 is closed: Socket's address is in GitHub's noreply form and passes the publish
  guard's identity rule. The committed round 2 summary is byte-identical to the original. Two informational notes,
  no action taken:
  - `memory/mem-004` cites the working session for the decision, as `mem-001` and `mem-002` do.
  - The account id in Socket's address was read from GitHub's user search; the reviewer had no network check.

## 2026-11-24: PR #1 HyperBench enrollment, round 2

- Record: [summary](patch-review-2026-11-24T09-03-37Z-allegiancecore-enrollment-r2/summary.md), [receipt](patch-review-2026-11-24T09-03-37Z-allegiancecore-enrollment-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged (it holds no machine paths or email addresses).
- Reviewed head `3ca08ba`, tree `fcfe4df`, the delta `f4d61cb..3ca08ba` that answers round 1. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. Both round 1 findings are closed, and the reviewer agrees with the dispute of finding 2
  (`README.md:160` names the x64 downloads). The redacted round 1 summary differs from the original by exactly the
  one stated line. Two informational notes, no action taken:
  - The `AGENTS.md` Git Bash line assumes a CRLF checkout, which is what `core.autocrlf=true` gives; Git Bash parses
    the script either way.
  - The round 1 receipt's earlier-attempt row rests on the first attempt's own folder on the reviewing host, not on
    the files of the run it records.

## 2026-11-24: PR #1 HyperBench enrollment, round 1

- Record: [summary](patch-review-2026-11-24T08-42-33Z-allegiancecore-enrollment-r1/summary.md), [receipt](patch-review-2026-11-24T08-42-33Z-allegiancecore-enrollment-r1/receipt.md)
- The committed summary is the reviewer's text with one email address redacted (`GROUND_RULES.md` rule 3).
- Reviewed head `f4d61cb`, tree `e7a4e8f`, the range `b76d6c0..f4d61cb`. Reviewer `deepseek-v4.1-flash` via
  OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Findings and dispositions:
  1. Low: `STARTER_MANIFEST.json` did not list `memory/mem-001-hyperbench-scope.md`, which is adapted from the copy
     source. Fixed: listed in `localModifications`.
  2. Low: `knowledge/vr-client-survey.md` said its 64-bit claim was derived from the README, which the reviewer
     found silent on it. Partly disputed: `README.md:160` names the x64 downloads. Fixed anyway: the claim now
     names the package reference, the release targets and the README line.
  3. Note, outside the change: the host's instructions name a non-noreply commit address for Socket, and this fork's
     publish guard rejects any address that is not GitHub noreply. Left for the operator to decide.
  - Review limit acted on: the publish guard is checked out with CRLF on Windows, which WSL's `bash` cannot parse.
    `AGENTS.md` now says to run it from Git Bash.
