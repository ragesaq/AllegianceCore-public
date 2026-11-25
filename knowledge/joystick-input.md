# Joystick input and a split HOTAS

**Owner:** ragesaq · **Started:** 2026-11-24 · **Last reviewed:** 2026-11-24 · **Review by:** 2027-02-24

**Status:** in progress (findings recorded; the work waits on the VR milestone 1 headset test, `DAG.json` node
`ragesaq-006`)

## What this answers

Whether a player can fly AllegianceCore with a HOTAS whose stick and throttle are separate USB devices, what native
Allegiance did for that setup, and what porting it involves.

## Current state

- A split HOTAS does not work: the client reads one joystick, maps its axes for a drone remote controller, and has
  fixed fire and booster buttons *(read from the code at `master` `47e720b`, see below)*.
- Native Allegiance bound each axis and command to a chosen device through its input map *(read from the native
  source, see below)*.
- The port is proposed and deferred until after the VR milestone 1 headset test *(decided 2026-11 by ragesaq)*.
- No hardware readout of ragesaq's devices exists yet: they were unplugged when this was written *(observed 2026-11)*.

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

- Which HOTAS ragesaq has, and how its axes and buttons enumerate under SDL2. Answered by a readout with the devices
  plugged in.
- Whether native's `defaultinputmap.mdl` binds joystick axes and buttons by default, and which.

## Timeline

- 2026-11-24 — ragesaq asked whether the game can bind their HOTAS and said the stick and throttle are separate
  devices; Port read the client and native input code (findings above) and found no joystick connected to the host at
  that time (observed, a MonoGame joystick enumeration).
- 2026-11-24 — ragesaq deferred the work until after the VR milestone 1 headset test (decided).

## Boundary

No device serial numbers, host names or machine paths. A future hardware readout records device names and counts
only.

## Sources

- Native source: <https://github.com/FreeAllegiance/Allegiance> (`src/engine/dinput.cpp`, `src/WinTrek/trekinput.cpp`,
  `src/WinTrek/trekinput.h`)
- Artwork set with `defaultinputmap.mdl`: <https://github.com/FreeAllegiance/Artwork>
- MonoGame joystick API: <https://docs.monogame.net/api/Microsoft.Xna.Framework.Input.Joystick.html>
