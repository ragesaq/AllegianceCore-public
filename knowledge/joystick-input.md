# Joystick input and a split HOTAS

**Owner:** ragesaq · **Started:** 2026-11-24 · **Last reviewed:** 2026-11-25 · **Review by:** 2027-02-24

**Status:** in progress (findings and a device readout recorded; the VR milestone 1 headset test it waited on is
done; `DAG.json` node `ragesaq-006`)

## What this answers

Whether a player can fly AllegianceCore with a HOTAS whose stick and throttle are separate USB devices, what native
Allegiance did for that setup, and what porting it involves.

## Current state

- A split HOTAS does not work: the client reads one joystick, maps its axes for a drone remote controller, and has
  fixed fire and booster buttons *(read from the code at `master` `47e720b`, see below)*.
- Native Allegiance bound each axis and command to a chosen device through its input map *(read from the native
  source, see below)*.
- The port is proposed and deferred until after the VR milestone 1 headset test *(decided 2026-11 by ragesaq)*.
- ragesaq's devices are read out below *(observed 2026-11-25)*. ragesaq will not bind their own pedals, which are
  racing pedals without a useful range of motion here; pedals must still be bindable for any player who wants
  them *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25: "i dont want to say that pedals shouldnt be viable if anyone wants to use them, i just dont plan on using mine as they dont have the range of motion that makes sense")*.

## Device readout (2026-11-25)

Read with a probe on the game's own input stack (MonoGame DesktopGL 3.8.5.1 on SDL2), `Joystick.GetCapabilities`
and `Joystick.GetState` for indexes 0 to 15, with all devices plugged in *(observed)*:

| SDL index | Device (SDL name) | Axes | Buttons | Hats | Role |
|---|---|---|---|---|---|
| 0 | VKB-Sim Gunfighter Modern Combat Pro Twist | 8 | 128 | 2 | stick |
| 1 | LEFT VPC Throttle MT-50CM2 | 6 | 79 (80 after the afterburner mapping, 76 from 2026-11-26) | 0 | throttle |
| 2 | Heusinkveld Sim Pedals Sprint | 3 | 0 | 0 | not bound by ragesaq (bindable for others) |

- With this order the game today reads the stick and ignores the throttle *(derived: `CurrentJoystickState`
  reads index 0 only, above)*. The order is SDL's and can change between runs, so bindings follow the device name
  and identifier, not the index (proposed first PR, step 1).
- At rest the stick reports axis 0 at about -6,300 and axis 7 at -32,768, and the pedals all three axes at -32,768
  *(observed, one sample)*. Which physical control each axis and button is has not been mapped yet; that needs a
  capture while ragesaq moves each control.
- Every device and axis is bindable, pedals included. A device with nothing bound, such as ragesaq's pedals, is
  ignored: never read as a stick or a throttle just because of its index *(derived from ragesaq's decision above)*.

### Control capture (2026-11-25)

ragesaq moved the controls in a set order while the probe logged the first time each axis left its rest value (by
more than 6,000 of 32,768) or each button or hat was pressed, and each axis's range over 90 s *(observed)*. The
roles below are read from that order and are *(derived)* where noted:

| Device | Input | Range seen | What it is |
|---|---|---|---|
| stick | axis 0 | full | roll, stick left and right (moved first) |
| stick | axis 1 | full | pitch, stick forward and back (second) |
| stick | axis 2 | full | yaw, twist (third) |
| stick | axes 5 and 6 | full | a two-axis thumb control: both moved at the same instant *(derived)* |
| stick | axis 7 | full, rest at the minimum | a lever or slider, likely the brake lever *(derived)* |
| stick | axes 3 and 4 | none | not used by this stick |
| stick | buttons 0 and 1 | | the trigger's two stages: pressed at the same instant *(derived)* |
| stick | hat 0 | up, right, down, left | the main hat |
| stick | buttons 2 to 9, 11, 13, 15, 20, 21, 23 | | other stick buttons ragesaq pressed |
| throttle | axis 3 | full | left throttle lever (second capture) |
| throttle | axis 4 | full | right throttle lever (second capture) |
| throttle | axis 5 | full | flaps lever (second capture) |
| throttle | axis 2 | full | middle-finger slider (second capture) |
| throttle | axes 0 and 1 | full | index-finger joystick (second capture) |
| throttle | button pairs 1 and 2, 13 and 14, 50 and 51, 53 and 54 | | the left lever's rotary encoder and the right lever's thumb rotary encoder, which send button pulses; which pair is which encoder is not known |
| throttle | buttons 0 to 8, 10, 12 to 28, 31 to 33, 43 to 46, 48 to 54, 73 to 78 | | throttle buttons ragesaq pressed |
| pedals | axis 0 | full | pedal axis, rest at the minimum (pressed once to check) |

A second capture, 60 s, started with the hands off and moved one throttle control at a time. ragesaq gave the
order afterwards *(Port's working session 2026-11-25: "left throttle, right throttle, flaps, middle finger slider, index finger joystick, left throttle rotary encoder, right throttle thumb rotary encoder")*. The probe logged axis 0 at 22.1 s, then axes 3,
4, 5, 2 and 1 at 24.2, 26.5, 29.4, 35.0 and 36.5 s, then the button pairs between 41 and 46.5 s *(observed)*. The
throttle rows above are read from that order *(derived)*. Axis 0 moving first, before the left lever, is most likely
the hand catching the index-finger joystick on the grip *(derived, not checked)*.

Settled with the hands off *(observed, second capture)*:
- Stick axis 0 rests at about 1,000, so the -6,300 of the first capture was a hand on the stick.
- Stick hat 1 reads "up" with the hands off: the stick reports it when idle. A binding must treat a hat or button
  already held when the game starts as its rest state, not as a press.
- Throttle buttons 35 and 36 read pressed at rest in both captures; button 34 was held in the first and not in the
  second, when the levers rested at the other end. 34 is likely a switch tied to the lever position, 35 and 36
  latched switches *(derived, not checked)*.
- The throttle levers' rest value is wherever they were left, so the throttle role reads the lever's position, with
  no centre.

The rotary encoders are buttons that fire in sequence as the encoder turns *(ragesaq, Port's working session
2026-11-25: "the rotary encoders are probably buttons that just fire in sequence"; consistent with the pairs
pressed in quick succession above)*. A binding treats each pulse as one step, acting on the press edge, never as a
held button.

Still open: which button pair is which rotary encoder, and which button of a pair is which turning direction.
- Both throttle levers moved from the maximum (+32,767) towards the minimum, so the throttle role was inverted, as
  native's `NativeHotas` layout already marks *(derived)*. Later the same day ragesaq inverted both levers in the
  Virpil configurator; see "First PR as built".

## How it applies here

- `CurrentJoystickState` reads only the first connected joystick (`src/Allegiance.ClientCore.Game/Game1.FlightInput.cs:53-62`)
  *(read)*. A throttle on a second device is therefore ignored *(derived, from that line range)*.
- The read path hard-codes `JoystickAxisLayout.DjiFpvRc3`, a drone remote controller's "Mode 2" layout
  (`Game1.FlightInput.cs:2058`) *(read)*. `JoystickAxisLayout.NativeHotas` (yaw 0, pitch 1, roll 2, throttle 3,
  throttle inverted) exists but nothing uses it (`src/Allegiance.ClientCore.Game/Flight/JoystickFlightController.cs:81`)
  *(read)*. On a HOTAS the axes would likely land on the wrong roles *(derived, from the two layouts)*.
- Fire and booster are fixed button constants, `FireButton = 1` and `BoosterButton = 2`
  (`JoystickFlightController.cs:156-158`) *(read)*. Joystick buttons are not part of the key map, and there is no
  binding screen *(read, a search of `src/` for an input-map port found only comments citing `defaultinputmap.mdl`)*.
- Native Allegiance (`FreeAllegiance/Allegiance`) *(read)*:
  - `engine/dinput.cpp:1549-1700` enumerates every joystick device (`GetJoystickCount`, `GetJoystick(index)`).
  - `WinTrek/trekinput.cpp:675-800` binds each command to a (joystick index, button index) pair, negative indexes
    being hat directions, and each axis role to a (joystick index, axis index) pair.
  - The map loads from `inputmap1` and falls back to `defaultinputmap` (`WinTrek/trekinput.h:48-49`);
    `defaultinputmap.mdl` is in the FreeAllegiance Artwork set. `InputMapPopup` (`trekinput.cpp:1631`) is the in-game
    binding screen.
- In VR the player cannot see the keyboard, so joystick bindings matter more there *(derived)*.

## What we decided

| Decision | Chosen | Rejected | Why *(derived — what it rests on)* | Decided by | Decided | Last reconciled |
|---|---|---|---|---|---|---|
| When to do the HOTAS work | After the VR milestone 1 headset test | Start it now, beside PR #7 | The headset test is the pending acceptance step for PR #7 | ragesaq (Port's working session) | 2026-11 | 2026-11-24 |

## Proposed first PR

Not decided; a proposal for ragesaq to accept or change when the work starts.

1. Read every connected joystick. Tie each binding to the device's identity (MonoGame `JoystickCapabilities.Identifier`
   and `DisplayName`) rather than its index, because the SDL index can change between runs *(derived)*.
2. Bind each axis role (yaw, pitch, roll, throttle) to a device, an axis and an invert flag; bind joystick buttons and
   hat directions to commands, at least fire and booster, preferably what native's default map binds.
3. Defaults from native's `defaultinputmap.mdl` where it gives them, else `NativeHotas`; keep `DjiFpvRc3` selectable;
   a user map file the player can edit.
4. Unit tests with synthetic states for two devices (stick on one, throttle on the other) and a device that is
   missing or unplugged; the deadzone and throttle-latch behaviour unchanged; parity rows in `docs/parity/`.
5. A readout of ragesaq's devices (names, axis, button and hat counts; no serial numbers) to set the defaults;
   ragesaq's flight test as the acceptance step.

The in-game binding screen is a later step, scoped with ragesaq.

## First PR as built

ragesaq said to start the first PR *(Port's working session 2026-11-25: "Start it")*. What it does, and where it
departs from the proposal above *(observed in the code and its tests, `JoystickBindingResolverTests`)*:

- Every connected joystick is read each update. Each one is matched to a device profile by a case-insensitive part
  of its name (`DisplayName`), not by its SDL index. The proposal named `Identifier` as well; the name alone is used
  because a player can read it and write it in a file, and the name is enough to tell ragesaq's three devices apart
  *(derived)*.
- A profile binds the yaw, pitch, roll and throttle roles to an axis and an invert flag, and the Fire, FireMissile
  and Booster commands to buttons. Hats are not bound yet.
- Built-in profiles: the VKB Gunfighter stick (X yaw, Y pitch, twist roll, as native's role order gives; the
  trigger's first stage, button 0, fires, as `defaultinputmap.mdl` gives) and the VPC throttle (left lever, axis 3,
  read as the device sends it, see below). Native's default puts missile fire on button 1, which on this stick is the trigger's second stage;
  the second stage is left unbound *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25: "i dont want trigger stage 2 for missiles")*. A player profile can still bind it. Stick X as yaw follows native; whether ragesaq wants X as
  roll is for the flight test.
- The game's Controls screen (F6) rebinds keyboard keys only; mouse buttons are fixed and joystick inputs are not
  on it *(observed in `Rendering/KeyBindingController.cs`, `Rendering/InputLayouts.cs`)*. Until a joystick
  binding screen exists, `joystick.json` is the only way to change a joystick binding.
- A player file `joystick.json` beside `player.json` adds profiles that are tried before the built-in ones. A file
  that cannot be parsed is logged and ignored.
- When two bound devices give the same role, the lower SDL index wins; a command is held when any device holds it.
- The throttle's index-finger thumb stick drives the side thrusters: axis 0 left and right, axis 1 up and down, each
  held while the stick is past half its travel, because Allegiance's thrusters are on or off *(decided 2026-11 by
  ragesaq, in Port's working session 2026-11-25: "there are also lateral and vertical thrusters useful for maneuvering in dogfighting, i want that bound to the index finger joystick on my throttle")*. Which way of axis 1 is up is assumed (pushing
  the stick up reads negative) and is for the flight test. Left and right thrust count as manual input and drop
  the autopilot, as the strafe keys do.
- The stick's three thumb face buttons are buttons 3, 4 and 5 from left to right *(observed: a 30 s capture
  2026-11-25, hands off, then one press each at 21.2, 22.3 and 23.2 s)*. They are bound to missiles, chaff and
  ripcord in that order *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25: "lets run the capture for the right joystick face thumb buttons, i'll start from left and go to right. the left one i want to be missiles, the next right one should be chaff (if there is one? some kind of deployable?) and the next right one should be ripcord")*.
  Missiles fire while held, like the missile key; chaff drops once per press. Ripcord takes a few seconds to jump
  *(ragesaq, Port's working session 2026-11-25: "ripcord is either a hold or a start and cancel, it takes a few
  seconds to jump")*. The port's ripcord key is start and cancel: a press starts the countdown to the sector
  selected on the starmap, a press during the countdown cancels it, and a press with no sector selected only
  shows a hint (`Flight/RipcordKey.cs` `RequestFor`, `Game1.FlightInput.cs` TK_Ripcord block). The joystick
  button feeds the same press detection, so it behaves the same *(observed in the code)*. On entering flight every
  joystick button is disarmed, so a button held through launch acts only after it is released once *(review round
  2, finding N1)*. Undocking from the hangar disarms them the same way *(review round 3, finding R3-1; completed after review
  round 4, finding N1, which found the targeting keys' copy of the frame still held the button)*.
- A profile can bind any command to an axis past a threshold (`axisCommands` in `joystick.json`), and the commands
  now include the four side thrusters, ripcord and chaff.
- Targeting goes on the throttle grip *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25: "i want
  targeting to be on the left throttle")*. ragesaq picked five grip controls by their numbers in Virpil's diagram
  of the MT-50CM2: push buttons 15, 16 and 21 and the 5-way castle switches 19 and 20 *(Port's working session
  2026-11-25: "i think button 15, 16, hat 19, hat 20, and button 21 are the ones to target for now")*. Those are
  Virpil's labels, not the button numbers the game reads; the throttle reports 79 buttons and no hats (device
  readout), so each castle switch is five buttons, four directions and a centre press *(derived)*.
- A 180 s capture 2026-11-25 mapped them *(observed)*. ragesaq pressed 21, 15, 16, then castle 20 up, right, down,
  left and centre, then castle 19 the same way, as they feel the directions in the hand. Each press logged exactly
  one new throttle button, in order: 31, 32, 20; 29, 30, 27, 28, 26; 24, 25, 22, 23, 21. Throttle buttons 35, 36 and
  43 read pressed at rest this time.

  | Control (Virpil diagram) | Button | Command |
  |---|---|---|
  | 15 push | 32 | `TargetNothing` (clear target) |
  | 16 push | 20 | `TargetCenter` (the target under the reticle) |
  | 21 push | 31 | `ToggleAutopilot` (autopilot to the target) |
  | 20 up, right, down, left, centre | 29, 30, 27, 28, 26 | next enemy, cycle any target, previous enemy, hostile, nearest enemy |
  | 19 up, right, down, left, centre | 24, 25, 22, 23, 21 | next friendly, nearest aleph, previous friendly, nearest friendly base, nearest friendly |

  The three push buttons follow ragesaq's choice *(decided 2026-11 by ragesaq, in Port's working session
  2026-11-25: "i want 15 to clear target, its not a natural press, i want 16 to select target, that is a natural
  high frequency press button so the action should match, and then 21 can be to autopilot to the target")*. The
  castle switches follow Port's proposal, which ragesaq accepted as a place to start (same message: "this is a good
  place to start"). Port read "select target" as `TargetCenter`, the press that picks what the pilot is looking at.
- The joystick targeting commands are named exactly like the key actions they press: `Target`, `TargetCenter`,
  `TargetNothing`, `TargetHostile`, `ToggleAutopilot`, next, nearest and previous for `TargetEnemy` and
  `TargetFriendly`, and nearest for `TargetEnemyBase`, `TargetFriendlyBase` and `TargetWarp`. Each acts once per
  press through the same press detection as its key, so a joystick press and the key behave the same
  (`Game1.TargetCommands.cs` `RemainderTargetEdgeDown`, `Game1.Targeting.cs` `TargetEdgeDown`, and the
  TargetNothing, TargetCenter and autopilot checks in `Game1.FlightInput.cs`) *(observed in the code)*. A test
  checks every such command is a catalog action the game dispatches. A joystick targeting press respects the
  same action filter a training lesson puts on the keys *(review round 3)*.
- The afterburner is the booster command (`FireBooster`, "Afterburner boost (hold)"). ragesaq set it on the left
  lever's afterburner detent in the Virpil configurator *(ragesaq, Port's working session 2026-11-25: "when i push
  it past the throttle level of the detent it will hold down button 73 representing hte afterburner")*. The
  configurator's physical button 73 had no logical button until ragesaq mapped it to logical button 80 (ragesaq,
  same session: "the logical mapping doesnt have button physical button 73 bound. i just bound it. it shows up as
  logical button 80"); before that the game never saw it. After the mapping the throttle reports 80 buttons,
  and with the lever past the detent it holds button 79 (0-based) *(observed: probe, 2026-11-25)*. The built-in
  throttle profile bound button 79 to the booster until the move below.
- ragesaq later moved the afterburner to logical button 76, after adjusting buttons "with overlapping features"
  *(ragesaq, Port's working session 2026-11-26)*. With the lever past the detent the throttle now holds button 75
  (0-based) and reports 76 buttons *(observed: probe, 2026-11-26)*. The built-in profile binds button 75 to the
  booster. No other bound button moved *(ragesaq, same session: "Only the afterburner")*.
- ragesaq also inverted the left and right levers in the configurator *(ragesaq, same session: "i also inverted
  the axis of left/right throttle (bound together) so the numerical values make more sense")*. Lever axis 3 now
  reads +32,767 at 100% (ragesaq: "the throttle is also at 100% right now", with the probe reading +32,767 and
  button 79 held) and spans -32,768 to +32,767 *(observed)*, so idle is -32,768 *(derived)*. The built-in
  profile no longer inverts axis 3. The lever reads 100% already at the detent (ragesaq: "at the max of being
  below the detent its still at 100%"), so only the button tells the afterburner zone apart. That is how Virpil's
  detent mapping is meant to work *(ragesaq, same session: "all axis positions past the detent continue to read
  at 100% but with a virtual button pressed, so this is as expected")*. A throttle left at
  Virpil's own direction needs a `joystick.json` profile with `"invert": true` on axis 3.
- A desktop app that shows which physical control is which button on both devices, and rebinds them, is a later
  goal *(ragesaq, Port's working session 2026-11-25: "an app is a future goal, for now we should just map out
  some bindings")*. ragesaq's HOTAS is on a sim rig with no monitor, viewed through VR, which is why the app
  would help. Adapting an existing tool is an option to look at.
- If the device that feeds the throttle goes away while another stays, the throttle latch is reset so the keyboard
  throttle works again *(review round 1, finding 1)*. After a profile has matched once, the fallback is not used, so
  leftover pedals never fly the ship *(finding 4)*.
- A device that matches no profile while another does is ignored, so pedals feed nothing unless a profile binds them.
  When no device matches, the first joystick is read with the port's earlier layout (`DjiFpvRc3`, fire button 1,
  booster button 2), as before this change.
- A button already held when its device appears counts only after it is released once, so latched switches and a hat
  that reads held while idle do not fire. This also applies to the earlier layout, which is the one change in
  behaviour for a setup that worked before.
- The log names each device and whether it uses a profile, the earlier layout, or is ignored.

## Open questions


- Which button pair is which rotary encoder on the throttle.
- Whether native's `defaultinputmap.mdl` binds joystick axes and buttons by default, and which.

## Timeline

- 2026-11-24 — ragesaq asked whether the game can bind their HOTAS and said the stick and throttle are separate
  devices; Port read the client and native input code (findings above) and found no joystick connected to the host at
  that time (observed, a MonoGame joystick enumeration).
- 2026-11-24 — ragesaq deferred the work until after the VR milestone 1 headset test (decided).
- 2026-11-25 — with the HOTAS plugged in, Port read out the three devices the game sees (table above). ragesaq
  will not bind their own racing pedals, but pedals stay bindable for other players (decided).
- 2026-11-25 — a 90 s control capture mapped the stick's axes, trigger and hat and the throttle's levers and buttons
  (table above); a few throttle axes and rest states remain open.
- 2026-11-25 — a second capture, one throttle control at a time with ragesaq naming the order, mapped every throttle
  axis and settled the rest states; only the encoders' button pairs remain open.
- 2026-11-25 — the first binding PR: per-device profiles by name, built-in stick and throttle profiles, a player
  file, the earlier layout as the fallback, held buttons ignored until released.

## Boundary

No device serial numbers, host names or machine paths. A future hardware readout records device names and counts
only.

## Sources

- Native source: <https://github.com/FreeAllegiance/Allegiance> (`src/engine/dinput.cpp`, `src/WinTrek/trekinput.cpp`,
  `src/WinTrek/trekinput.h`)
- Artwork set with `defaultinputmap.mdl`: <https://github.com/FreeAllegiance/Artwork>
- MonoGame joystick API: <https://docs.monogame.net/api/Microsoft.Xna.Framework.Input.Joystick.html>
