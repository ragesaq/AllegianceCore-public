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
- ragesaq's devices are read out below *(observed 2026-11-25)*. The pedals are not used for this game *(decided
  2026-11 by ragesaq, in Port's working session 2026-11-25: "i dont plan on using the pedals, those are for
  racing")*.

## Device readout (2026-11-25)

Read with a probe on the game's own input stack (MonoGame DesktopGL 3.8.5.1 on SDL2), `Joystick.GetCapabilities`
and `Joystick.GetState` for indexes 0 to 15, with all devices plugged in *(observed)*:

| SDL index | Device (SDL name) | Axes | Buttons | Hats | Role |
|---|---|---|---|---|---|
| 0 | VKB-Sim Gunfighter Modern Combat Pro Twist | 8 | 128 | 2 | stick |
| 1 | LEFT VPC Throttle MT-50CM2 | 6 | 79 | 0 | throttle |
| 2 | Heusinkveld Sim Pedals Sprint | 3 | 0 | 0 | not used (ragesaq) |

- With this order the game today reads the stick and ignores the throttle *(derived: `CurrentJoystickState`
  reads index 0 only, above)*. The order is SDL's and can change between runs, so bindings follow the device name
  and identifier, not the index (proposed first PR, step 1).
- At rest the stick reports axis 0 at about -6,300 and axis 7 at -32,768, and the pedals all three axes at -32,768
  *(observed, one sample)*. Which physical control each axis and button is has not been mapped yet; that needs a
  capture while ragesaq moves each control.
- A device that is not bound, such as the pedals, must be ignored, not read as a stick or a throttle *(derived)*.

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

## Open questions

- Which physical control is each axis, button and hat on the stick and the throttle. Answered by a capture while
  ragesaq moves each control.
- Whether native's `defaultinputmap.mdl` binds joystick axes and buttons by default, and which.

## Timeline

- 2026-11-24 — ragesaq asked whether the game can bind their HOTAS and said the stick and throttle are separate
  devices; Port read the client and native input code (findings above) and found no joystick connected to the host at
  that time (observed, a MonoGame joystick enumeration).
- 2026-11-24 — ragesaq deferred the work until after the VR milestone 1 headset test (decided).
- 2026-11-25 — with the HOTAS plugged in, Port read out the three devices the game sees (table above); ragesaq
  decided the pedals are not used for this game.

## Boundary

No device serial numbers, host names or machine paths. A future hardware readout records device names and counts
only.

## Sources

- Native source: <https://github.com/FreeAllegiance/Allegiance> (`src/engine/dinput.cpp`, `src/WinTrek/trekinput.cpp`,
  `src/WinTrek/trekinput.h`)
- Artwork set with `defaultinputmap.mdl`: <https://github.com/FreeAllegiance/Artwork>
- MonoGame joystick API: <https://docs.monogame.net/api/Microsoft.Xna.Framework.Input.Joystick.html>
