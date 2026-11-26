# Target center: what the "target what I am aiming at" command picks

**Owner:** ragesaq · **Started:** 2026-11-25 · **Last reviewed:** 2026-11-25 · **Review by:** 2027-05-25

**Status:** in use

## What this answers

Which object the target-center command (native `TK_TargetCenter`; the `'` key, right mouse button and the HOTAS
"select target" button) selects, and why the port changed from its earlier rule.

## Current state

- The command picks the object at the smallest angle off the ship's nose, of any kind (ship, station, warp,
  asteroid, probe, treasure) and any side, in the player's own sector *(derived, from native's code cited below)*.
- A repeat press steps past the current target to the next best object; the current target is kept only when
  nothing else qualifies *(derived, same source)*.
- An object behind the ship is picked only when nothing is ahead *(derived, same source)*.

## How it applies here

- Native `TK_TargetCenter` is `FindTarget(c_ttFront | c_ttAllTypes | c_ttAllSides)` with the ship's orientation
  (`WinTrek.cpp:10127-10151`). `c_ttFront` scores each object with `Orientation::CosForward2`, a signed squared
  cosine of the angle off the nose, and keeps the best score (`common.cpp:509-640`, `orientation.cpp:404-444`)
  *(read, upstream source)*. Nothing ranks one kind of object above another.
- The port used to reuse the autopilot's implicit-pick list (`PickNearestTarget` over `SelectCandidates`, which the
  G key still uses), which offers ships first, then warps, then stations, then asteroids, and only objects inside the
  flat camera's view. Any ship anywhere in view therefore beat a station or rock on the reticle *(read,
  `Game1.Targeting.cs`)*. The T key is a different path (`ChooseTarget`, by distance or list order) *(read,
  `Game1.TargetCommands.cs`; review round 1, finding F1)*.
- In a turret native ranks from the carrier's position along the turret's facing (`WinTrek.cpp:10131-10147`). The
  port uses the carrier's interpolated pose and the turret aim there, because the own-ship simulation is not stepped
  while boarded *(read, `Game1.FlightInput.cs`; review round 1, finding F2)*. Not yet tried in game.
- In ragesaq's headset test the HOTAS select-target button "didnt seem to target only what was under the reticle"
  *(observed 2026-11-25, ragesaq, combined build of PR #15 and PR #14)*. The binding itself was right: ragesaq's
  1-based button 21 is the game's 0-based button 20 *(derived, from the capture table in the HOTAS note on
  PR #14)*.

## What we decided

| Decision | Chosen | Rejected | Why *(derived)* | Decided by | Decided | Last reconciled |
|---|---|---|---|---|---|---|
| Target-center rule | Native's angle-off-the-nose rule, all kinds | Keep the ship-first tiers; a cone limited to the view | Native parity, and it is what the headset test asked for | Port, to restore native behaviour after ragesaq's report | 2026-11 | 2026-11-25 |

## Traps

| Looks like | Actually | How to tell |
|---|---|---|
| The HOTAS button is bound to the wrong command | The button was right; the pick rule was not native | The log line `[monogame] target center:` names what was picked |

## Timeline

- 2026-11-25 — ragesaq reports the select-target button picks things away from the reticle (observed, headset test).
- 2026-11-25 — Port finds the port's tiered rule differs from native and ports `c_ttFront` (read, upstream source).
- 2026-11-26 — cross-family review round 1: PASS-WITH-CONDITIONS; the turret axis, the record of which list the
  port used, and the Controls label fixed after it.

## Sources

- Upstream `src/WinTrek/WinTrek.cpp:10127-10151`, `src/Igc/common.cpp:470-653`, `src/zlib/orientation.cpp:404-444`.
