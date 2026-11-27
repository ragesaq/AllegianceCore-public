# VR HUD design: what each HUD element is fixed to, and how far away it is

**Owner:** ragesaq · **Started:** 2026-11-25 · **Last reviewed:** 2026-11-25 · **Review by:** 2027-02-24

**Status:** in progress (design spec accepted by ragesaq 2026-11-25; build step 1 built on `port/vr-hud-in-eyes`, waiting for review and the headset; node `ragesaq-010`)

## What this answers

Where each part of the flight HUD goes in the VR client: what it is fixed to (the world, the ship, or the
direction of flight), how far away it is drawn, how large it is, and in what order to build it. It replaces the
milestone 2 HUD, one flat quad 2 m ahead covering about 80° by 50° of view, which ragesaq found too close, too
large and hard to use (`knowledge/vr-client-survey.md`, "Milestone 2 in the headset").

## Current state

- Milestone 2 draws the whole flat HUD into the back buffer and shows it on one OpenXR quad layer 2 m ahead
  *(read: `OpenXrPresenter.cs`, `VirtualScreen.cs` on `master` `e73b573`)*.
- The HUD moves into the eye images, not onto its own layer *(decided 2026-11 by ragesaq, recorded in
  `knowledge/vr-client-survey.md`)*.
- ragesaq accepted this spec as it stands on 2026-11-25, including layer 3 and building on the Jet skin
  *(Port's working session: "Accept, merge #12", in answer to whether the rest of the spec was ready to accept)*. Choices first
  proposed by Port say so where they appear. The numbers (distances, sizes, the fade) remain starting values for
  the headset.
- The game has three HUD skins, cycled with Shift+F12: Classic, Modern and Jet. Jet is a fighter-style vector HUD
  that the upstream author is still building. A visual flag gates it and is off by default; turn it on with
  `--visuals hud.jetSkin=on` or the "Jet HUD" toggle in the visual options *(read: `Rendering/VisualFlags.cs`,
  `Game1.GpuBudget.cs`, `Rendering/InputActionCatalog.cs` on `master`)*. The upstream author pointed ragesaq at it
  for VR (ragesaq, Port's working session 2026-11-25). See "The Jet skin and the cockpit study" below.

## Constraints

- **No motion controllers.** Input is a joystick and keyboard, or a HOTAS with some keyboard. There is no wrist or
  hand UI and no pointing at panels; every HUD element is read, not touched, and is switched with keys or stick
  buttons *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25: "this game wont have motion controls,
  so no wrist UI or anything like that")*. HOTAS support itself is deferred (node `ragesaq-006`).
- **Drawn in each eye pass.** Every element is drawn into both eye images with that eye's view and projection, so
  each has a real depth (decided above).
- **The flat game keeps its bytes.** The flat HUD is unchanged with `--vr` absent or on any VR failure (prime rule 3
  and the VR seam rules in `knowledge/vr-client-survey.md`).
- **Nothing is fixed to the head.** A HUD that turns with the head is a known comfort problem
  *(read: [Microsoft, comfort](https://learn.microsoft.com/en-us/windows/mixed-reality/design/comfort))*, and the
  head is free to look around the cockpit. Elements are fixed to the world, to the ship, or to the ship's
  direction of flight.

## The four layers

| Layer | Elements (current HUD code) | Fixed to | Depth | Size | Source of the choice |
|---|---|---|---|---|---|
| 1. Aim | crosshair (`DrawCrosshair`), velocity marker (`DrawVelocityMarker`), lead indicator and aim predictor (`DrawLeadIndicator`, `DrawAimPredictor`) | the ship's nose direction | infinity: the same direction in both eyes, no disparity | constant angle, about 2° to 4° across | *(decided 2026-11 by ragesaq: "the aiming reticle should be cast into infinity")*; matches fighter HUDs, which collimate symbology to infinity *(read: [F-35 JPO](https://www.jsf.mil/hmds))* |
| 2. Targeting | lock bracket and target selection bracket (`DrawLockBracket`, `DrawTargetSelectionBracket`), radar contact brackets and labels (`DrawRadarLabels`), target bearing arrow | the world: each marked object | at the marked object's distance, so the eyes converge on the object and its marker together | scaled with distance so the angle stays constant | *(decided 2026-11 by ragesaq: the targeting HUD "should be depth rendered … to match the focusing distance of the object targeted")*; matches VR guidance that a marker must sit on its object *(read: [Meta, display](https://developers.meta.com/horizon/design/display/))* |
| 3. Flight reference | speed (`DrawSpeed`), throttle, roll (`DrawRoll`), heading tape, afterburner, the six status gauges (`DrawGauges`), weapon mode | the ship (cockpit), in a central field of about 30° to 40° | infinity, like a fighter's head-up display | about 30° by 20° in total | Port's proposal *(derived: fighter symbology fields are 20° to 40° and collimated to infinity, [Collins JHMCS](https://www.collinsaerospace.com/what-we-do/Military-And-Defense/Displays-And-Controls/Airborne/Helmet-Mounted-Displays/Joint-Helmet-Mounted-Cueing-System), [BAE Striker II](https://www.baesystems.com/en/product/striker-ii-digital-helmet-mounted-display); and the readouts a pilot glances at mid-fight should not need the eyes to refocus)* |
| 4. Cockpit displays | target pane (`DrawTargetPane`), weapon status list (`DrawWeaponStatusList`), own status and command summary, console and chat text (`DrawConsoleText`), minimap and starmap, training callouts, warnings, money and kills | the ship (cockpit) | about 0.8 m to 1.2 m, never under 0.5 m | text at least 0.6° to 0.75° tall | *(decided 2026-11 by ragesaq: informational elements "at a relatively close distance … somewhat angled like we are in a cockpit and the information is projected on displays")*; distances from VR guidance *(read: [Meta](https://developers.meta.com/horizon/design/display/): at least 0.5 m, about 1 m comfortable; [Microsoft](https://learn.microsoft.com/en-us/windows/mixed-reality/design/comfort): never under 0.4 m; text sizes from [Microsoft, typography](https://learn.microsoft.com/en-us/windows/mixed-reality/design/typography))* |

Layer 3 is Port's addition, not in ragesaq's description. Without it the flight readouts that are now near the
crosshair (speed, gauges, roll) would move to close panels, and every glance at them would need the eyes to
refocus from the fight. ragesaq accepted it on 2026-11-25.

### Layer 1: aim symbols at infinity

- Draw each symbol along a direction fixed to the ship, at a large distance, with depth testing off, so it shows the
  same direction in both eyes *(derived)*.
- A ship or base nearer than the symbol's apparent depth that passes behind it gives conflicting cues: it hides
  nothing, yet stereo says the symbol is behind it *(read: Meta, display, above)*. In this game fights are hundreds
  of metres apart, so this happens mostly near bases and in close formation *(derived from the screenshots'
  ranges: a base at 711 m; vergence at 63 mm eye spacing is 0.005° at 711 m, effectively infinity)*. Proposal:
  fade the crosshair while the object under it is nearer than about 30 m. To tune in the headset.

### Layer 2: targeting markers at the target's depth

- Each marker is placed at its object's position in each eye pass, a little in front of the object's near side so
  the object does not cut through it, and scaled with distance so its angular size does not change
  *(read: [Unity XRI reticle](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.6/api/UnityEngine.XR.Interaction.Toolkit.XRInteractorReticleVisual.html),
  [Meta raycast](https://developers.meta.com/horizon/design/raycasting_specs/))*.
- Size from the eye and the angle, not from screen pixels: a screen-size billboard shrinks in VR
  *(read, forum report: [Unreal forums](https://forums.unrealengine.com/t/billboard-crosshair-changing-relative-size-in-vr/367992))*.
- Labels (name, range) sit with their bracket at the same depth and the same angular text size.
- **Depth and readability are separate settings.** How far away a marker looks depends on the disparity between
  the eyes. How readable it is depends on the angle it covers. A marker placed at its target's depth and scaled with
  distance covers the same angle at 50 m and at 5 km, so distance alone never makes it unreadable *(derived)*. What
  does make it unreadable is a fixed-size marker, which shrinks with distance, or a long block of small text
  *(read: Unreal forum report above;
  [EVE Valkyrie design lessons](https://gamedeveloper.com/design/-text-looks-rubbish-and-other-vr-design-lessons-learned-on-i-eve-valkyrie-i-))*.
  Beyond about 20 m the depth is hard to tell from infinity anyway (vergence 0.18° at 20 m, under 0.01° at 500 m),
  so a far target's data gains no depth cue by sitting at the target *(derived)*.
- **Split the target information by job** *(decided 2026-11 by ragesaq, in Port's working session 2026-11-25:
  "Accept the split", after raising that part of the targeting information should be closer, or it may be too far
  away to see; the split itself was Port's proposal)*. The 2° and 0.6° floors below are still starting values:
  - *At the target's depth:* what says where the target is and whether to shoot. That is the bracket, the lock, the
    lead marker, and a short label (name and range). It has an angular floor: the bracket is never under about 2°
    across, the text is never under about 0.6° tall, and when the target is off-view an edge arrow shows the
    direction instead.
  - *On a close target panel (layer 4, about 1 m):* what is read rather than aimed at. That is type, side, hull and
    shield, speed, time to intercept, in weapon range, ore and kill bonus, and the target model preview.
  - The same split appears twice upstream. Jet draws a designator on the target and the target's data as separate
    readouts *(read: `CoverageIds` in `Rendering/Jet/JetHudRenderer.cs`: "Selected target bracket" against "Target
    name", "Target hull" and others)*. The cockpit study puts the bracket, lock and lead on "HUD glass" and target
    name, hull, shield, range, speed and preview on "MFD: target" *(read: P1044 map, rows 18 to 38)*.
  - The 2° and 0.6° floors are starting values: 0.6° is the comfortable text height in
    [Microsoft, typography](https://learn.microsoft.com/en-us/windows/mixed-reality/design/typography), with 0.35°
    to 0.4° as its minimum. The headset decides them.
  - No source found in this research gives a rule for which target data a VR game puts on the target and which in
    the cockpit *(read: none found)*. The split rests on the two upstream designs and the readability argument
    above.
- Several contacts: each bracket at its own object's depth. No source covers this; it follows from the rule above
  *(derived)*.
- No source gives a clamp distance or a smoothing time *(read: none found)*. Beyond a few tens of metres a marker at
  the object's depth and one at infinity look nearly the same *(derived: vergence 2·atan(0.0315/d) is 0.18° at
  20 m)*, so a clamp is needed only for depth-buffer range, not for comfort. A target that jumps in depth (a new
  lock) can move its marker at once; a marker sliding in depth is the case to smooth *(derived from
  [Microsoft, comfort](https://learn.microsoft.com/en-us/windows/mixed-reality/design/comfort): content moving in
  depth is the main discomfort source)*.

### Layer 3: the flight reference at infinity

- One block of readouts in a field of about 30° by 20°, centred a little below the nose, fixed to the ship, drawn at
  infinity with depth testing off. It is the fighter's head-up display: it stays put in the cockpit when the head
  turns. **Revised 2026-11-26 by ragesaq:** 10 m out instead of at infinity (decision table).
- Its content is today's central gauges and readouts, laid out again for a smaller field rather than scaled down:
  the flat layout spreads across the whole 1280x720 screen.
- **Revised 2026-11-26 by ragesaq:** no horizon cues. Space has no horizon, so the pitch ladder, the heading tape and
  the roll pointer are left out; the block is the speed and throttle readouts, in the manner of Elite Dangerous and
  Star Citizen and of original Allegiance (decision table).
- **Revised again 2026-11-26 by ragesaq:** the block is the Classic skin's centre group, the ring gauge of original
  Allegiance (thrust, fuel, armour, shield, ammunition and energy around the own ship), with the speed and the
  own-ship status (signature) below it, whatever skin is active (decision table).

### Layer 4: cockpit displays

- Panels fixed to the cockpit, each a flat rectangle about 1 m from the head's rest position, turned to face it, so
  together they form an angled set like cockpit screens rather than one sheet.
- Placement: the main panels 10° to 35° below the nose, where the resting gaze falls; secondary ones (chat, minimap)
  to the sides, within about 45° of the nose; nothing more than about 10° above it *(read: Microsoft, comfort,
  above)*.
- Each panel's content is drawn with the existing HUD code into its own off-screen image, then shown as a textured
  rectangle in each eye pass *(derived: reuses `HudRenderer` layout and fonts; the panel images are drawn once per
  frame, not per eye)*.
- Panels can be switched or cycled with keys, since there is no pointing.
- There are no cockpit-model screens to reuse. The game draws no cockpit interior, and the native artwork has no
  cockpit or interior model *(read: `docs/reports/COCKPIT_3D_P1044.md`, "Native findings")*. So the doubled elements
  in the milestone 2 screenshot are not 3-D cockpit screens; they are most likely overlapping parts of the flat HUD
  *(derived, not checked)*.
- Panels must not hide contacts. The upstream cockpit study requires that any cockpit geometry leave every visible
  ship and its marker readable, and drop the cockpit for a frame when it cannot *(read: P1044, "Gameplay parity and
  fairness")*. The same rule applies to layer 4 panels, and it is a reason to keep them below the nose.

## The Jet skin and the cockpit study

**The Jet skin is already split the way the four layers need.** It is a third `IHudSkinRenderer`, in about 9,400
lines under `Rendering/Jet/`, with green and amber palettes. Every value it shows comes from the shared `HudModel`,
the same data the Classic and Modern skins draw from. Its geometry is in pure functions with no graphics device
(`JetTargetGeometry`, `JetFlightGeometry`), which tests can run headless *(read: `Rendering/Jet/JetHudRenderer.cs`
and the two geometry files on `master`)*. Its elements map onto the layers like this:

| Jet element | Layer |
|---|---|
| broken gun cross; round flight-path marker | 1, aim at infinity |
| target designator (corner brackets at 1.05 times the target's radius), missile lock, lead marker, order arrows | 2, at the target's depth |
| speed and throttle tapes | 3, flight reference at infinity |
| pitch ladder on the sector plane, heading tape, roll pointer | not shown in VR (decided 2026-11-26: no horizon in space) |
| stores block (hull, shield, energy, fuel, ammunition), master caution, console and chat, inventory pane, radar | 4, cockpit displays |

Layer 3 is the part a fighter HUD is built around. In Jet, the pitch ladder is drawn against the sector plane,
not against the screen. A ladder like that only lines up with the world when it is drawn at infinity, which supports
layer 3 *(derived)*. ragesaq decided on 2026-11-26 that VR shows no horizon cues at all (decision table), so this
argument no longer applies.

**Build the VR HUD by placing Jet's elements, not by laying out Classic again** *(proposed by Port, accepted by ragesaq 2026-11-25)*. The work becomes a VR
placement for each Jet element group, chosen by layer. Classic and Modern keep their flat layouts in VR, on one
panel, as a fallback. The flat Jet skin keeps its bytes. One cost: Jet is behind a default-off flag and still in
development upstream, so the VR placement follows its changes. That argues for doing this work with the upstream
author, not as a fork-only branch (node `ragesaq-012`). **Superseded 2026-11-26 by ragesaq for the centre block:**
the VR HUD's flight block is Classic's ring gauge, not Jet's tapes (decision table).

**The upstream cockpit study (P1044) proposes the same split for the flat game.** It is a report, not built code.
It keeps aim, targeting and urgent symbols as a sharp overlay ("HUD glass", which matches layers 1 to 3). It moves
systems and target summaries onto cockpit displays drawn into small off-screen images ("MFD", which matches layer 4).
It parents the interior to the seat, not the head, and gives the interior its own depth range of 0.05 m to 5 m,
because a 0.05 m world near plane would make depth steps at 10 km about 20 times coarser *(read:
`docs/reports/COCKPIT_3D_P1044.md`, "Complete HUD destination map", "Rendering design")*. It also maps all 112 HUD
rows in the parity matrix to a destination. Layer 4 can use that map instead of the element list in the table
above, and its planned display service (P1044 slice 3) is the off-screen panel image layer 4 needs. Build one
service with upstream, not two.

## What we decided

| Item | Choice | Rejected | Who and when decided | Last reconciled |
|---|---|---|---|---|
| Where the HUD is composited | into each eye image | its own composition layer (milestone 2), fixing capture in the capture tool | ragesaq, 2026-11 | 2026-11-25 |
| Aiming reticle depth | infinity, except that a selected or locked target under the reticle (within 3°, or half its bracket) gives it that target's depth, held between 15 and 100 m and eased over 0.2 s ("your recommendation for the smaller version sounds like a plan to me") | always at infinity (the step 1 build; ragesaq kept it after the first headset test); snapping to the target's distance wherever it is, with a 25 m rest distance (a prompt from another assistant: the reticle shows where the guns point, so an off-axis target must not set its depth, and a 25 m rest brings it back toward the near HUD); collimated 15 to 30 m ahead, as Elite Dangerous does | ragesaq, 2026-11; revised 2026-11-25 | 2026-11-25 |
| Target marker depth | the target's distance | infinity; a fixed HUD plane | ragesaq, 2026-11 | 2026-11-25 |
| What zone C shows | the target pane alone, grown to fill the zone ("showing the secondary target screen is higher priority and it should get scaled up"); the port's lines and the target notices go beside the chat in zone B, widened to the flight block's width | the pane at the flat game's size with the port's lines beside it (the layer 4 build); the port's lines in zone D or E, which have no room for them | ragesaq, 2026-11-27 (the move to zone B proposed by Port) | 2026-11-27 |
| Information panels | 2 m out, angled like cockpit displays ("1m is a little too close") | one flat sheet (milestone 2); panels fixed to the head; about 1 m (the first choice, 2026-11) | ragesaq, 2026-11; distance revised 2026-11-26 | 2026-11-26 |
| Input | joystick and keyboard, or HOTAS and keyboard; no motion controllers, no wrist UI | motion-controller UI | ragesaq, 2026-11 | 2026-11-25 |
| Target information split | marker, lock, lead and a short label at the target's depth with an angular floor; the target's details on a close panel | all target information at the target's depth | ragesaq, 2026-11 (proposed by Port, accepted 2026-11-25) | 2026-11-25 |
| Flight reference block (layer 3) | accepted, 10 m out in the seat ("the classic hud circular lines shouldnt be drawn to infinity, it feels a little fake that way, maybe closer like 10m") | all non-marker elements on close panels; the block at infinity (built first, 2026-11-25 to 26) | ragesaq, 2026-11 (proposed by Port, accepted 2026-11-25); depth revised 2026-11-26 | 2026-11-26 |
| Horizon cues in VR | none: the pitch ladder, the heading tape (a 360° compass) and the roll pointer are left out of the VR HUD, and Classic's ROLL dial with them; the flight-path marker stays (the block itself is Classic's ring, next row). Follow space-game HUDs (Elite Dangerous, Star Citizen) and stay close to original Allegiance, which has none of these; the flat Jet skin keeps them ("i dont know that a ladder makes a lot of sense in space, we dont have a horizon to worry about, the same with a 360' compass, so we should remove those elements") | Jet's full fighter-jet flight reference at infinity (the first step 2 build) | ragesaq, 2026-11-26 | 2026-11-26 |
| What the VR HUD is built from | the flight block (10 m out in the seat since 2026-11-26) is the Classic centre group (the ring gauge, the speed, the own-ship status), drawn by the Classic renderer whatever skin is active; the other skins stay in the game, and each leaves its own centre instruments off its VR panel. ragesaq showed original Allegiance's ring ("i think we can have it be simple like that"), chose "Use Classic's ring", and added "these other UIs can be in i just want to use the classic one" | the Jet skin's speed and throttle tapes as the block (accepted 2026-11-25 and built first in step 2); laying out the whole Classic HUD again for VR | ragesaq, 2026-11-26 (revisits the 2026-11-25 choice of Jet) | 2026-11-26 |

## Disagreements between sources

| Question | Figure | Source and form | Date |
|---|---|---|---|
| Where the aiming reticle goes | at infinity | fighter HUD and HMD practice, *(read: [F-35 JPO](https://www.jsf.mil/hmds))*; DCS players expect it *(read, forum: [DCS](https://forum.dcs.world/topic/224385-cannot-reproduce-hud-too-close-in-vr))* | read 2026-11-25 |
| Where the aiming reticle goes | on the object behind it | *(read: [Meta, display](https://developers.meta.com/horizon/design/display/))*; Star Wars Squadrons snaps it to the nearest object *(read, a search snippet of EA Motive's blog, page now gone)*; [Damaged Core](https://www.gamedeveloper.com/design/in-vr-even-something-as-simple-as-a-crosshair-is-i-not-i-easy-to-do-) | read 2026-11-25 |
| Minimum text height | 0.35° to 0.4° minimum, 0.6° to 0.75° comfortable at 2 m | *(read: [Microsoft, typography](https://learn.microsoft.com/en-us/windows/mixed-reality/design/typography))* | read 2026-11-25 |
| Minimum text height | at least 1.5° | *(read: [Google Daydream, archived 2019](https://developers.google.com/vr/distribute/daydream/design-requirements))*, from low-resolution phone headsets | read 2026-11-25 |

The reticle row is settled for this game by ragesaq's decision, not by evidence; the conflict it warns about (a
near object behind a far reticle) is handled by the fade in layer 1. The text-size row is settled in the headset.

## Build order (proposal)

1. **Infrastructure and layer 2 first.** Draw HUD content in the eye passes and stop submitting the quad layer;
   targeting brackets and labels at their objects' depth. This alone fixes capture (`ragesaq-011`) and the
   misplaced markers.
2. **Layers 1 and 3.** The aim symbols and the flight reference block at infinity.
3. **Layer 4.** The cockpit panels, drawn with the display service from the upstream cockpit study if that has
   landed, and placed by its 112-row map.
4. **Headset tuning by ragesaq** of distances, sizes, the crosshair fade and panel placement, recorded here.

Each step keeps the flat HUD's bytes (replay frames) and goes through the review gates.

## Step 1 as built (branch `port/vr-hud-in-eyes`)

- **The HUD is drawn into each eye image.** The OpenXR quad layer is gone from stereo frames: a stereo frame
  submits only the projection layer, so a capture of the eyes holds the HUD (`ragesaq-011`). A frame that falls
  back to flat still shows the back buffer on an opaque screen quad *(read: `OpenXrPresenter.cs`, `Game1.VrHud.cs`
  on the branch)*.
- **Targeting markers at their objects' depth.** Each frame the flat game's contact brackets, the selection
  bracket, the missile-lock bracket and the lead marker are turned into world positions, and each eye projects
  them itself, so each marker carries its object's disparity. Contacts come from the radar's pixels, cast back
  through the view at the contact's known distance; in a stereo frame the radar is re-run through a wide,
  head-turned frame (±75° by ±70°) so contacts beside the game camera's frame are kept. Sizes are angles: brackets
  never under 2° across (capped at 20°), text never under 0.6° tall, line width at least 0.08° *(read:
  `VrHudGeometry.cs`; tested in `VrHudGeometryTests`)*. The selection and lock brackets are drawn 1.15 and 1.3 times
  the contact bracket so they nest outside it; at the 20° cap all three are the same size. A flat eye image stretches
  off its centre, so each marker's pixel size is taken at its own pixel, measured up and down; off to the side a
  square bracket is then a little narrower than its angle. Labels are name and range from the radar's own label
  rules.
- **The aim reticle is a direction** (the ship's nose), the same direction in both eyes, so it is seen at infinity. The
  flat game's centre crosshair is left off the panel in VR (`HudContext.CentreReticleInWorld`). ragesaq looked in the
  headset on 2026-11-25 and asked that the targeting reticles be drawn farther than 1 m, to tell them apart from the
  near HUD *(ragesaq, Port's working session 2026-11-25)*; the flat crosshair on the 1 m panel was the one that was
  not.
- **The rest of the flat HUD is on one interim panel fixed to the seat**: 1 m from the head's rest position, its
  centre 25° below the nose, 1.1 m wide, facing the head. For headset tuning, set `ALLEGIANCE_VR_HUD_PANEL` to
  "distance m,pitch down degrees,width m" (for example `1,25,1.1`); values outside 0.3 to 10 m, -60 to 80° or 0.1 to
  5 m fall back to the default. It is drawn with its own projection whose near plane is 5 cm: the eye projection keeps
  the game's near plane (1 m in the cockpit, 10 m outside), which clipped the panel's lower part away *(observed
  2026-11-25 in a full-resolution dump of the left eye; test `The_whole_panel_lies_between_the_seat_projections_depth_planes`)*.
- **The desktop window mirrors the left eye**, letterboxed.
- **Limits of step 1**, for the next steps: no edge arrows for off-view targets yet; the last-base objective
  brackets, the velocity marker, the chase aim point and the order arrows are not drawn in VR yet (they were
  screen-space and are dropped from the panel); the panel shows no radar contacts, because every skin draws them as
  screen-space brackets and labels that would double the world markers, so the Jet skin's radar scope is
  empty in VR until the cockpit panels (layer 4) give it its own contact list (found by the cross-family review of
  PR #15, round 1, F1); the FIRING tag left the panel with the flat crosshair.
- The flat game is unchanged: replay frames 300, 1500, 3600, 6000 and 9000 of `battle.alr` hash the same as
  before *(observed 2026-11-25)*.

## Step 1 in the headset (2026-11-25)

ragesaq tested PR #15 together with the HOTAS bindings (PR #14) *(ragesaq, relayed by Port's other working session
and answered in this one, 2026-11-25)*:

- "i noticed the HUD was still very 2d, but it worked". Expected: the flat panel is step 1's stopgap, which layer 4
  replaces.
- "the depth of rendering on the targeting window is a little confusing, i think the bore sight is accurate". The
  "targeting window" is the brackets around objects *(ragesaq, Port's working session 2026-11-25)*. Likely cause, not
  yet confirmed in the headset: the brackets draw over everything with no depth test, so a nearer object that passes
  in front of a far target sits "behind" a bracket that stereo places farther away *(derived from
  `Game1.VrHud.cs`)*.
- ragesaq pointed at how Elite Dangerous places its targeting, from a note by another assistant, and said "it makes a
  lot of sense to me". Its points against this spec *(read: that note only, not checked against the game)*:
  - Brackets and the lead pip at the target's world position with a constant angular size: built in step 1.
  - Their depth clamped at about 50 to 100 m, so far objects do not shimmer: matters only with a depth test, so it
    goes with that follow-up. Beyond 100 m vergence is under 0.04°, the same as infinity.
  - The boresight collimated 15 to 30 m ahead: rejected for now; the reticle stays at infinity (table above).
  - The cockpit depth-tests over world brackets: no cockpit model exists yet; the scene's own depth is the
    follow-up's test.
  - Off-screen target arrows on a ring 2 to 5 m out near the canopy edge: the edge arrows step 1 lacks.
- Next (a layer 2 follow-up): test bracket depth against the scene's depth buffer, with the near clamp; off-screen arrows
  on a ring a few metres out. Then a headset look to confirm the bracket confusion is gone. Built: see below.

## The layer 2 follow-up as built (branch `port/vr-hud-bracket-depth`)

Stacked on step 1 (PR #15). What it changes, all in the eye images and none of it in a flat frame:

- **Brackets are tested against the scene's depth.** Each marker is placed on the line from the head (midway between
  the eyes) to its object, at the near side of the sphere its bracket frames, so the object does not hide its own
  bracket, and never farther than 100 m. The sphere comes from the object's own size, not the drawn bracket, which is
  capped at 20°: anchored from the capped angle, a marker near a large object fell inside its hull and was hidden
  (review round 1, F1). For the selection and the lock the size is the object's world radius, for every type that
  can be selected or locked, measured from the head (`VrHudGeometry.AnchorAngle`, `MarkerAnchorInWorld`): the cockpit
  eye is 14 m ahead of the ship's centre, and a wrap angle measured from the ship still put close targets inside their
  hulls (review rounds 2 and 3). With a radius, the anchor is in front of the near side whenever the head is outside
  the object. A marker with
  no radius (a failed lookup) can be hidden by its own object only when the object's near side is within 100 m,
  because of the clamp. No test runs the lookup from a lock or selection to the marker's radius: it is a private
  member of the game, so a test needs a game instance with its hull and station catalogues and a world with one object
  of each type, or the lookup moved to a static helper. It is checked by reading (review rounds 4 and 5). A radar contact's size comes from its
  screen radius, which the radar caps, so an asteroid nearer than about 1.6 radii can still hide its own contact
  bracket *(derived: review round 2, observation 4)*. It is drawn with a read-only depth test at that point's depth, so a nearer object that
  passes in front of it hides it. Beyond 100 m the clamp changes nothing that can be seen: vergence is under 0.04°
  *(derived: 64 mm / 100 m)*. The clamp also limits occlusion: an object between 100 m and a far target does not hide
  its marker. `VrHudGeometry.MarkerAnchor`, `WrapAngle`, `BufferDepth`.
- **The scene's depth is still in the eye images when the HUD is drawn.** Checked on the GPU *(measured: a temporary
  probe, removed before commit, drew a half-clear sheet over the whole left eye at depth 0.9999 with the same read-only
  depth test, in the battle replay with SteamVR)*: asteroids and ships stayed unpainted, while the sky and a planet,
  which are drawn without writing depth, were painted. So the depth the scene wrote is intact at compose time.
  A second probe tested the value at mid distances *(measured, same setup)*: at each radar contact, a red square at
  the depth of a point 2 radii behind the object's centre and a green one 2 radii in front. Asteroids 1.4 and 2.6 km
  away hid the red square and showed the green one. That bounds any difference between the depth the sprite path
  tests and the depth the scene wrote to the probe's window there, about 0.0001 to 0.0003; a remap such as
  `0.5 z + 0.5` would have hidden the green square. Which contacts it lands on varies from run to run. Warp swirls showed
  both, as they write no depth. To repeat it after a MonoGame or backend change: draw those two squares per contact in
  the left eye before the markers, and look at the window mirror. A unit test pins `BufferDepth` to
  far (d - near) / (d (far - near)).
- **Off-view arrows.** When the selection's or the lock's marker falls outside either eye's image, an arrow in its
  colour sits on a ring 30° off the view's centre, 3 m out, on the side the object lies, and points that way. An
  object straight behind gives an arrow pointing down. `VrHudGeometry.OffViewArrow`.
- **The aim reticle's depth** follows the decision table: infinity, unless a selected or locked target is under it.
  "Under it" uses the same angle as the anchor, so a large object whose hull covers the reticle counts even when its
  drawn bracket is capped.
  Its depth is eased as an inverse distance, which stereo disparity is proportional to, so it moves at an even rate
  and can return to infinity. `VrHudGeometry.ReticleInverseDistance`, `SmoothInverseDistance`.
- **Draw order in each eye:** the aim reticle and the arrows over the scene, then the markers with the depth test,
  then the panel, which at 1 m is nearer than every marker. A panel tuned farther than a marker with
  `ALLEGIANCE_VR_HUD_PANEL` still draws over it.
- **Why the reticle stays at infinity in most cases** *(Port, 2026-11-25, answering a prompt from another assistant
  that ragesaq passed on)*: the stereo offset between a point at distance d and infinity is about 64 mm / d. That is
  0.01° at 300 m and 0.04° at 100 m. Only at about 20 m and nearer (0.18°) does it reach roughly where the eyes stop
  fusing two images at the centre of view *(read: Panum's fusional area, about 10 to 20 arcminutes at the fovea;
  textbook figure, not measured here)*. So infinity only fails for a close target under the reticle, which is the
  case the change covers.
- Tests: `VrHudGeometryTests`, 99 VR tests. Each of 15 deliberate breaks of the new geometry made a test fail.
- Cross-family review: round 1 PASS-WITH-CONDITIONS; round 2 WARN, because the round 1 fix measured the wrap from the
  ship's centre; round 3 WARN, because the lock passed a radius only for ships; round 4 PASS-WITH-CONDITIONS with two
  Low items; round 5 PASS with no findings (`.hyperreview/README.md`).
- Final VR smoke run of the reviewed code in the battle replay under SteamVR: renders at about 6.7 ms a frame, no
  exceptions.
- Not yet checked in the headset: whether the bracket confusion is gone, and how the reticle's depth change feels.
  *(Checked: see below.)*
- Merged in PR #17 (merge commit `de0c1ce`, 2026-11-26).

## The layer 2 follow-up in the headset (2026-11-26)

ragesaq looked at PR #17 in the headset: "i did the PR17 headset review, it looked good" *(ragesaq, Port's working
session 2026-11-26)*. No problem was reported with the bracket depth, the off-view arrows or the reticle's depth.

## Step 2 as built (branch `port/vr-hud-step2`)

Layers 1 and 3, for every skin. What it changes, all in the eye images and none of it in a flat frame:

- **The flight block at infinity is Classic's centre group (layer 3).** In a stereo frame
  (`HudContext.FlightReferenceInWorld`) the Classic renderer draws, once a frame, only its centre group into an
  image the size of the HUD image (`HudRenderer.DrawFlightReference`): the ring gauge (thrust, fuel, armour, shield,
  ammunition, energy), the own-ship status (signature, status icons, kill bonus) and the speed readout, which it
  puts 195 pixels below the ring's centre as native does (`HudRenderer.FlightReferenceSpeedBelowCentre`; the flat
  port draws it at the screen's foot). It does this whatever skin is active, because Modern and Jet are separate
  renderer instances and ragesaq wants the Classic ring in VR (decision table).
- **Each skin leaves its own centre instruments off the 1 m panel in VR.** Classic and Modern leave out the centre
  group (`HudRenderer.CentreGroupOnMainPass`) and the port's ROLL dial (`HudRenderer.RollOnMainPass`); the
  afterburner readout stays on the panel. Jet leaves out its speed
  and throttle tapes and its horizon cues: the heading tape, the pitch ladder (with its `PITCH` readout) and the roll
  pointer are drawn in VR nowhere (ragesaq, 2026-11-26, decision table). The flat game is unchanged in every skin.
- **The crop and its size.** Classic draws the ring at fixed pixels around the image's centre (pane scale 0.72), but
  places the own-ship status from native points spread with the image height (`OwnStatusLayout.Spread`: height/680,
  held between 1.15 and 2.1). So the box cut out of the image is sized from that spread
  (`VrHudGeometry.CentreGroupBox`), and shown on a quad 10 m out (`VrHudGeometry.FlightBlockQuad`). The angle per
  pixel is fixed: 400 pixels about the centre are 40° by default, so the ring (about 215 pixels) is about 21.5° wide
  whatever the window size, and the image's centre, the ring's, is on the nose. Each eye draws the quad with the
  seat view's translation zeroed (`VrHudGeometry.AtInfinity`), so both eyes see each point of it in the same
  direction: no vergence disparity, and no movement when the head moves. It is fixed to the ship: turning the head
  looks away from it. For headset tuning, set `ALLEGIANCE_VR_FLIGHT_BLOCK` to "width degrees,pitch down degrees"
  (for example `40,0`); values outside 10° to 90° or -30° to 45° fall back to the default
  (`VrHudGeometry.FlightBlock.Parse`).
- **The flight-path marker is at infinity (layer 1).** A circle (0.6° radius) with wings and a tail, in the
  reticle's colour, drawn along the ship's velocity in each eye as a direction. It is the space cue that replaces
  the horizon cues: original Allegiance shows the velocity direction too, and Star Citizen has the same marker. It
  is drawn with the same gate as the aim reticle and only when the speed rounds to at least 1 m/s
  (`VrHudGeometry.FlightPathDirection`).
- **Draw order in each eye:** the flight block first, then the aim reticle, the flight-path marker and the arrows,
  then the markers with the depth test, then the panel.
- **History:** the first step 2 build drew Jet's speed and throttle tapes (and, before the no-horizon decision, its
  ladder, heading tape and roll pointer) as the block, for Jet only. ragesaq chose Classic's ring instead; that
  build and its `IHudFlightReferenceRenderer` interface were removed.
- **Not built:** the crosshair fade under 30 m (layer 1); the stores block and the other layer 4 displays stay on the
  panel.
- Tests: `VrCentreGroupTests` (the Classic and Modern main pass keeps the centre group in a flat frame and leaves it
  out in a VR eye frame), `JetVrFlightReferenceTests` (Jet's flat pass draws its flight elements; its VR main pass
  leaves them out) and `VrHudGeometryTests` (the crop quad: the ring's centre on the axis at each pitch, 40° across,
  square pixels, upright; the crop's texture coordinates; the same angle in a bigger image; a small image cropped,
  not enlarged; the box holds the ring, the speed and every own-status point at 720 to 2160 lines, and fits the
  image; the ring the same size whatever the window; `AtInfinity`, `Parse`, the flight-path rule). Each deliberate break of the new code made a test fail.
- Checked: with the VR flag off, the main pass makes the same calls in the same order and under the same condition
  as `master` (gauges, speed, afterburner, then the own-ship status), by reading the diff. Flat replay frames 300,
  1500, 3600, 6000 and 9000 of `battle.alr` are byte-identical to `master`'s *(observed 2026-11-26)*. **Limit:**
  those frames show the same HUD whether `hud.json` names Classic, Modern or Jet *(observed 2026-11-26: the three
  sets are byte-identical to each other)*, so they check one skin's flat pass, not each skin. An earlier record here
  that the flat Jet frames were byte-identical was wrong in the same way: they were not Jet frames. Why the replay
  capture ignores the skin setting is not yet found.
- Checked in VR with Classic selected *(observed 2026-11-26)*: 60-second runs of the replay and of training under
  SteamVR raised no exceptions (one known audio warning, also on `master`). The window, a mirror of the left eye,
  shows the ring with SIG, the detection eye and the speed readout below it at the nose, none of it cut by the crop.
  Not checked: how it looks in the headset, and the other skins in VR.

## Headset notes of 2026-11-26 and what they found

ragesaq's notes from a session in the headset *(Port's working session, 2026-11-26)*, each with its cause and what
was done on `port/vr-hud-step2`:

- "the target box surrounding your selected target seem to have an axial shift depending on how you are looking at
  it, which makes part of the targeting box go behind the object and causes a focal shift". **Cause** *(derived
  from `Game1.VrHud.cs`; confirmed by a test)*: a marker is drawn flat in each eye's image, so the depth test gives it
  one depth along the eye's axis, and that depth was its anchor's. Off the eye's axis, the object's sphere reaches
  up to radius · (1 − cos θ) nearer along that axis than the anchor, so the near side of a large or close object cut
  through the bracket, more the farther the head turned from it. **Fix:** each marker is tested at the nearer of its
  anchor and its sphere's nearest point along the eye's axis (`VrHudGeometry.MarkerTestDepth`). Its place in the
  image, and so its stereo depth, is unchanged. This also likely explains part of the flicker: a bracket tested at
  about the depth of its hull's surface is hidden and shown again as the hull moves *(derived, not checked in the
  headset)*.
- "i also didnt see an aim lead indicator on enemy ships". **Causes:** the lead is drawn only when native
  Allegiance grants it (the hull's lead capability, or a friendly relay that sees the target: `LeadGranted`), and
  the flat game hides it then too; that is kept. Separately, in VR the lead marker carried no radius, so when it fell
  on its target the target's own hull, nearer than its centre, hid it *(derived)*. **Fix:** the lead marker carries
  the target's radius.
- "the directional indicator as to where we were aiming": the aim reticle and the flight-path marker are drawn only
  in the cockpit view with the own ship in flight (`GatherVrHudMarkers`). Neither is visible in the window shots of
  the replay smoke runs, and whether the replay meets that gate was not checked. **Not yet explained**; asked of
  ragesaq which view and mode the session used.
- "bullets are sprites and they seem to be rotated". **Cause** *(read: `ProjectileSpriteRenderer.DrawBillboard`;
  confirmed by a test)*: the round's quad took the camera's right and up from the view matrix's rows, but a
  `CreateLookAt` view keeps them in its columns (the same mistake HUD-21b fixed for the target preview). The rows are
  a mixed basis, so the quad was tilted toward the camera's axis. In the headset each eye's view turns with the head,
  so the tilt changed with every head turn and showed in stereo. **Fix:** the columns. This changes the flat game
  too, where billboard rounds are on screen: native draws them facing the camera. Eight other renderers read the
  rows the same way (explosions, smoke, chaff, mines, dust, the particle batcher and two sizing helpers), and so do
  two other sites: the camera right and up handed to the debris field (`NativeScene`) and the 3D sound listener's right
  (`SoundManager.SetListenerCamera`, whose comment says the rows hold the basis). None of them is changed here
  (found by the cross-family review of PR #22, round 1).
- "a lo fo the UI elements flicker a little bit". Not yet found beyond the bracket depth above; asked of ragesaq which
  elements.

## Step 2 in the headset (2026-11-26, second look)

ragesaq's look at the build at `6737eaa` *(Port's working session, 2026-11-26)*, with its cause and what was done on
`port/vr-hud-step2`:

- "the classic ring looks good, no roll dial needed" and "flickering is gone". Native Allegiance has no lasting roll
  instrument, only a training text *(read: the `DrawRoll` comment in `HudRenderer.cs`, citing
  `WinTrek/indicator.cpp:634-641`)*; the ROLL dial is the port's own.
- No lead marker on enemy ships: the hull flown did not have the lead capability, which native requires
  (`c_habmLeadIndicator`, or a friendly relay that sees the target: `LeadGranted`). Not a VR defect.
- "the flight path marker ... its green, its supposed to be grey". **Cause:** VR drew its own green shape. **Fix:**
  VR draws the original game's velocity marker (`directionbmp`, 32 by 32 pixels, untinted as in the flat game) at
  the flight block's angle per pixel, about 3.2° across. A grey shape stands in when the art is missing.
- "the left hand side of targeting boxes seems to not be in focus ... and the boxes dont seem to be billboarding
  towards the camera orientation". **Cause** *(derived from `Game1.VrHud.cs`, not measured in the headset)*: each eye
  drew the box as pixel offsets around its anchor's pixel, sized by that eye's stretch at that pixel. The two eyes'
  images stretch differently there (they are turned and off-centre, and this headset's are wide), so a box's edges
  got a disparity other than its anchor's, and one side sat at another depth. **Fix:** a marker is a flat shape in
  the world at its anchor, at right angles to the line from the head and upright to the head
  (`VrHudGeometry.MarkerBillboard`); each eye projects its corners. On the flat mirror window a box off the centre
  now shows in perspective, as such a shape does on a flat image; the headset's lenses undo that.
- "parts of a log are overlapping the target indicator that shows a PIP of the object, and the PIP needs to be a
  little higher and increased in size by 150%". **Cause** *(observed in a dump of the VR panel image, 1280 by 720,
  with a target selected)*: the port's own weapons and cargo text lines (`DrawFlightPartsHud`, `DrawCargoHud`; not
  in native) run across the target pane at the image's foot. **Fix, VR only:** the two lines move up to the image's
  middle, which VR leaves empty since the centre group went to infinity (`VrHudGeometry.PortTextLift`, replaced
  in the third look); the target
  preview is drawn 1.5 times its native size with its bottom edge kept, so it also sits higher
  (`HudRenderer.TargetPreviewEnlargement`, `TargetModelPreview.PaneRect`). "Increased by 150%" was read as 1.5
  times the size; the flat game is unchanged.
- dominusbelial's review notes of a recording (`dominusbelial/AllegianceCore-public`,
  `docs/reports/VR_TEST1_REVIEW_2026-11-26.md`) describe the Classic panel of an earlier build, with the ROLL dial and
  the centre group still on it: doubled ARMOR and SHIELD labels and KB lines. Step 2 takes the centre group and the
  dial off the panel. A cyan separator still runs through one "NOT READY" weapons row in the panel dump; not
  changed here.

## Step 2 in the headset (2026-11-26, third look)

ragesaq's look at the build at `0314c5e` *(Port's working session, 2026-11-26)*, with its cause and what was done on
`port/vr-hud-step2`:

- "target boxes look good" and the flight-path marker "is grey, so that part is good".
- Box sizes: "we need to have a scaling on how big they should be, a maximum and minimum size ... the minimum size im
  seeing right now is close, lets reduce its size by 30%". **Cause:** sizes already scale with the object's size and
  distance between a floor of 2° and a cap of 20°, but most objects at fighting range sit at the floor, so most boxes
  look the same. **Fix:** the floor is 1.4°, 30% less; the cap stays 20° (`VrHudGeometry.BracketFloorRadians`).
- "the targeting boxes rotate if you rotate your head ... they should stay static". **Cause:** a box's plane was
  upright to the head, so it rolled with the head. **Fix:** upright to the seat (the ship); it still faces the head,
  so its edges keep the anchor's depth (`VrSeatUpInWorld`, `VrHudGeometry.MarkerBillboard`).
- "the weapon and cargo area is kind of overlapping other elements ... like the chat, so this needs to move up
  higher". **Fix, VR only:** the two lines start under the chat pane and the keys hint, at the panel's left (152 of
  720 pixels down), and wrap at 65% of its width, short of the weapons pane (`VrHudGeometry.PortTextTopPixels`,
  `PortTextWrapWidth`, `WrapLine`). The flat game is unchanged.
- "the classic hud circular lines shouldnt be drawn to infinity, it feels a little fake that way, maybe closer like
  10m". **Fix:** the flight block is drawn 10 m out in the seat, at the same angle as before. Its quad was already
  placed 10 m out; the view no longer drops the eye's offset. The decision table row is changed.
- "the thrust bar isnt animating with thrust amount, just when the afterburner is on". **Cause:** the gauge showed
  the keyboard throttle (P445), which a joystick throttle axis never moves: the axis goes straight into the controls
  sent. **Fix:** with a joystick throttle bound and no autopilot, the gauge shows the throttle sent, as native shows
  the applied controls (`HudThrottle.Gauge`). Keyboard flight and autopilot keep P445's value. This changes the flat
  game too, on purpose.
- "the text inside the classic hud circle area is pretty low resolution". **Cause:** the block's image had the HUD
  image's pixels (1280 by 720), and 40° across 400 of them is 0.1° a pixel, coarser than the headset's eye images.
  **Fix:** the block's image has twice the pixels each way; it is laid out as before and drawn scaled up, and its
  text takes the 2x font bakes at half the scale (`HudRenderer.DrawFlightReference` with a density,
  `NativeFonts.TryDenser`, `HudRenderer.DenseText`). The gauge art is the same bitmaps, sampled finer. The flat
  game draws at density 1, unchanged.
- "when missiles launch i see them either at really low framerate or kind of double vision". **Cause** *(derived
  from `Game1.Rendering.cs`, not measured in the headset)*: a missile was drawn at its last server position, which
  changes about 20 times a second, so at the headset's frame rate it stood still and jumped; an update that landed
  between the two eyes' passes put it in two places. **Fix:** a missile is drawn moved on from its last position
  along the velocity measured between updates, at most 0.1 s, and the second eye uses the first eye's positions
  (`MissileMotionSmoother`). The exhaust trail follows the same positions. This changes the flat game too.

## Layout criteria for the VR HUD (accepted 2026-11-26)

*(decided 2026-11 by ragesaq: proposed by Port, approved in Port's working session on 2026-11-26 as "Approve,
new PR": the zones below, built as layer 4 in its own pull request stacked on PR #22.)*

ragesaq's third look at step 2 *(Port's working session, 2026-11-26)*: "a lot of the text is on top of the circle hud,
we need to have primary hud UI non overlapping. the text chat needs to go above the circle hud. we need a more
comprehensive UI design criteria that accounts for all of the elements and their position."

**Cause** *(derived from `VrHudGeometry.PanelCorners`, `FlightBlockQuad` and a dump of the panel image; script-free
arithmetic, not measured in the headset)*: the flight block is centred on the nose and its group reaches 20.6° below
it (the own-ship status and the speed readout under the ring). The panel is one sheet 1 m out, 25° below the nose,
whose top edge is 6.9° below the nose. So the panel's top third lands inside the flight block, at 1 m against 10 m:

| Element | Across (az) | Up and down (el) | Overlaps |
|---|---|---|---|
| Ring gauge | -11.0° .. 11.0° | -10.8° .. 10.8° | |
| Own-ship status and speed | -22.7° .. 14.3° | -20.6° .. 12.0° | |
| Chat pane | -7.6° .. 9.6° | -13.4° .. -8.1° | ring, status |
| Keys hint (transient) | -12.6° .. 12.6° | -14.2° .. -13.5° | status |
| Port weapons and cargo lines | -28.5° .. 10.9° | -18.8° .. -13.1° | status, speed |
| Afterburner and weapon mode | 11.9° .. 17.9° | -15.6° .. -13.6° | status |
| Weapons pane | 12.0° .. 30.1° | -20.2° .. -14.5° | status |
| Location | 22.2° .. 26.8° | -9.5° .. -8.8° | |
| Target pane | -8.1° .. 8.5° | -39.5° .. -31.7° | |
| Minimap | 16.3° .. 34.5° | -36.1° .. -29.8° | |

### The rules

1. **One owner per place.** The view is divided into zones, and every HUD element belongs to exactly one zone. A
   zone is a region of directions from the seat (degrees across and up), with its depth.
2. **No two zones overlap, with a gap of at least 1.5°** between them. The aim and targeting symbols (layers 1 and 2:
   reticle, flight-path marker, brackets, lock, lead, arrows) are exempt: they sit on their objects in the world.
3. **No depth conflict.** Two things at different depths never share a direction, because the eyes cannot focus on
   both: the 10 m flight block and a 1 m panel must not overlap even by a margin.
4. **Placement by how often it is read.** The flight block keeps the centre. What is read in a fight (target, weapons,
   energy) sits nearest the centre, below it. What is read now and then (chat, location, map, inventory) sits
   further out. What is read in a fight stays within 26° across and 40° down; nothing to be read goes beyond 42°,
   where reading needs a head turn.
5. **The forward view stays clear** above the ring, except the chat zone ragesaq asked for there.
6. **Text size floor:** text is at least 0.6° tall where it is read; a zone that cannot fit its text at that size
   wraps or drops it, never shrinks it.
7. **Transient messages have a reserved zone** (keys hint, training callouts, warnings), so they never land on
   another element.
8. **The flat game keeps its layout.** Zones apply to the VR eye frame only.
9. **Checked by a test.** The zone table lives in code; a test fails when two zones overlap or when an element's
   rectangle leaves its zone.

### The zones

Degrees from the seat: across is positive to the right, up is positive above the nose. Each zone's quad is sized so
1° covers about 20 pixels of the HUD image's layout (1280 by 720), whatever its distance, so the small font's line
is about 0.64° tall. The panels are 2 m out (ragesaq, 2026-11-26: "i think purple panels should be at 2m, 1m is a
little too close"), so each quad is twice the size it would be at 1 m.

| Zone | Across | Up and down | Depth | What it holds |
|---|---|---|---|---|
| A, flight block | -26° .. 26° | -22° .. 14° | 10 m | the ring gauge, the own-ship status, the speed (as now) |
| B, comms | -26° .. 26° (-16° .. 16° until 2026-11-27) | 16° .. 30° | 2 m, facing the head | chat history and compose box, vote banner, critical and ordinary tips, warnings, base objectives, "NO X IN SECTOR", the keys hint, training callouts, the skin-switch toast; since 2026-11-27 also the commandee and leader lines, the mouse-stick indicator and its notice, and the port's weapons and cargo lines |
| C, target | -14° .. 14° | -24° .. -42° (-40° until 2026-11-27) | 2 m | the target pane with its preview, grown to fill the zone (since 2026-11-27; before, also the lines now in B) |
| D, navigation | -42° .. -16° | -24° .. -42° | 2 m | the minimap, the location and turret lines, current orders, the lives and kills badges, the camera label, the command ship summary |
| E, ship systems | 16° .. 42° | -24° .. -42° | 2 m | afterburner and power, weapon mode, mining, time dilation, the weapon status list or the inventory pane (F4) in its place |
| Modal | centre | centre | 2 m | the expanded starmap (Tab), the death and lifepod overlays, the ripcord countdown, the command bar and command view, the scoreboard: shown alone, and the flight block is hidden while one is open |
| Tint | whole view | whole view | none | the damage and warp flashes, as a tint over the eye image, not a panel |

Gaps: 2° between A and B, A and C, A and D or E, and between C and D or E. Reading reach: what is read in a
fight is within 26° across and 40° down (A, C, the inner half of E); what is read now and then may reach 42°.

The aim and targeting symbols are not zoned: the reticle, the flight-path marker, the selection, lock and lead
markers, the contact brackets and tags, the off-view arrows and the order arrows sit on their objects. The target
bearing arrow (bottom right of the flat HUD) is redundant with the off-view arrows in VR and is left out.

### How it would be built (layer 4)

The single panel is replaced. In a VR eye frame the HUD pass lays each element out in its zone's own region of the
HUD image (an anchor per element, taken from the zone table instead of the flat screen), and each zone's region is
drawn on its own quad at the zone's direction and depth, as the flight block is now. The flat layout stays as it is.
A geometry test checks the zone table (no overlaps, the gaps, the reach) and each element's rectangle against its
zone at 1280 by 720.

## Layer 4 as built (branch `port/vr-hud-zones`)

Built on 2026-11-26, stacked on PR #22. It builds the accepted zones for the Classic skin, the skin the VR HUD is built
from. The flat game does not change.

**How it is built** *(decided 2026-11 by Port while building, within the accepted zones; ragesaq to confirm in the
headset)*. The plan above said each element would be laid out again in its zone's own region of the HUD image.
Port took a smaller route that keeps almost all of the flat layout: the flat HUD is still drawn into one image,
each element or group of elements is cut out of that image by its rectangle, and each cut-out is shown on a quad of
its own in its zone (`Vr/VrHudZones.cs`). The quad is 2 m out and faces the head. Its rectangle comes from the
flat layout's own rules (`TargetPaneLayout`, `MinimapGeometry` and fixed anchors). The rejected route was to move the
anchor of each element in the HUD code. That touches dozens of draw sites in three skins, and every one of them
would have to be kept in step with the flat game. Only three elements needed to move for the cut-outs to work.

The cut-outs, for a HUD image of width w and height h (pixels from the top left):

| Cut-out | Zone | Source rectangle | Holds |
|---|---|---|---|
| target notices | B, left of comms | 300 s centred (s the target pane's scale), from 144 s above the pane's top (at least 144 px, for the unscaled mouse-stick notice 140 px above it) down to the pane's top | leader and commandee lines, mouse stick and its notice |
| comms | B | 480 px centred, from the top to 0.40 h + 22 | chat pane and compose box, keys hint, vote banner, tips at the top, warnings, base objectives, "NO X IN SECTOR" |
| port lines | B, right of comms | the lower left, from h - 302 to h - 34 | the port's weapons and cargo lines, which VR draws there, wrapped to 216 px |
| target | C | 300 s centred, from the pane's top to the foot, grown up to 2 times to fill the zone | target pane and preview; the ordinary tips at the foot too |
| minimap | D | the minimap's own rectangle | the minimap |
| location | D | 200 px at the top right, from 30 to 106 | location and turret lines |
| orders and badges | D | 330 px at the top left, from 24 to 196 | current orders, lives and kills badges |
| camera | D | the foot's left 160 px | the camera label |
| systems | E | from the comms strip's right edge, or the weapon status list's left edge if that is further left, from 120 down to the target cut-out's top | afterburner and power, weapon mode, mining, time dilation, the weapon status list down to the minimap's top (see the known gaps) |
| systems low | E | right of the target cut-out, down to the minimap's top | the rest of the weapons pane, or the inventory (F4) |

A zone lays out its cut-outs in rows, pushed toward the flight block: comms down, the target zone up and centred,
navigation up and right, systems up and left. Zone C grows the target pane to fill it, up to 2 times
(`VrHudZones.TargetMaxScale`): at 1280 by 720 about 1.96 times, which makes it as wide as the zone. The two systems parts share their right edge and meet in the image,
so they are stacked right-aligned with no gap and join as they were. The afterburner, power, weapon mode, mining
and time dilation lines are drawn through the HUD's text layout, which keeps a 320 by 120 px box for them at the
image's top and then moves them out of it, to about y 130 to 220, inside the systems cut-out. *(Review round 2
read them at their nominal place, y 16 to 96, and Port added a cut-out there; round 3 showed, from the layout
code and the committed capture `docs/reports/P1075_assets/desktop-afterburner-classic.png`, that nothing is drawn
there, and the cut-out was withdrawn. A test now places the lines through the layout.)* A pixel covers about 0.045°, what it covered on
the step 1 panel for a 1280-wide image, so the small font's 14 px line is about 0.63°. At 1280 by 720 every zone
holds its content at that scale, and zone C grows the target pane.

**Which image sizes get zones** *(review round 1 finding, 2026-11-26)*. The cut-outs follow the flat layout's
anchors: some are fixed pixels (the 480 px comms strip, the 330 px orders block, the 200 px location box), some follow
the image (comms down to 0.40 h, the target pane and the minimap). They hold together only near the 1280 by 720 image
they were taken from. For a larger image, comms and systems grow past their zones, and fitting them would shrink the
text under the 0.6° floor (at 1920 by 1080 a 14 px line would be about 0.36°), which rule 6 forbids. For a smaller one
the cut-outs overlap, so an element would show in two zones. So before it uses the zones, the game checks the layout
for the image's size (`VrHudZones.ClassicLayout`): every cut-out lies in the image, no two share pixels, and no zone
scales its 14 px line under 0.6°. When the check fails, the whole HUD image is shown on one panel below the nose, as
for another skin, and the game logs the reason once for that size (`[vr] HUD zones do not hold`). The default window,
1280 by 720, gets zones. The check passes from 534 to 760 px high, and from 1275 px across up to between 1486 and
1552 px (the limit grows with the height: 1486 px at 534 and 600 high, 1534 at 720, 1552 at 760). Below 1275 px the systems cut-out, which starts no further right than
the weapon status list so as not to cut off its left side, meets the comms strip. Above the limit the systems zone would
shrink its text. Under 534 px high comms meets the cut-outs below it, and over 760 px comms would shrink its
text.

**What moved in VR.** The port's text lines go to the image's lower left, which the flat layout leaves empty
(`VrHudGeometry.PortTextTop`), so they can be cut out alone, and are shown in zone B. The keys hint is kept to the 480 px comms strip, so it
stays clear of the weapons pane: a long hint is drawn smaller, as the flat game does for a narrow window. The target
bearing arrow is left out, as the zone table says.

**Modal screens.** While the expanded starmap, the scoreboard, pilot info, the team pane, the game-state pane (F7),
the player pings, the spectator's sector map, the investment pane, help, credits, the main menu, the key bindings, the settings panel, the quick-chat menu, command view, the hangar, death or the lifepod, the
ripcord countdown, the briefing or the game-over panel is up, or when the frame has no in-flight HUD, the whole HUD
image is shown on one panel ahead, 2 m out and 2.2 m wide, and the flight block is hidden
(`Game1.VrHudModalOpen`). A skin other than Classic keeps the whole image on one panel below the nose at 2 m
(`VrHudZones.FallbackPanel`); its text can still land on the ring, as on the step 2 panel.

**Evidence.**
- Build: 0 warnings.
- Zone tests (`VrHudZonesTests`):
  - The zone table keeps its gaps and reach.
  - A quad faces the head at its depth and spans its angles.
  - At four image sizes, the cut-outs lie in the image and no two overlap, and each is placed once, inside its
    zone, at its own aspect, with no two placements overlapping.
  - At 1280 by 720 nothing is scaled except the target pane, which zone C holds alone and grows by at least 1.5
    times until it meets the zone's width or height (also at 1280 by 534 and 1440 by 740).
  - The notices' cut-out ends where the pane's begins, so together they keep the strip the target cut-out had.
  - The two systems parts join.
  - A zone too small for its content scales it to fit.
  - `ClassicLayout` gives zones at the range's edges: 1275 by 720, 1280 by 534, 1280 by 760, 1440 by 740,
    1486 by 534, 1534 by 720 and 1552 by 760. It refuses 1487 by 534, 1535 by 720, 1553 by 760, 1600 by 900,
    1920 by 1080, 2560 by 1440, 1280 by 761 and 1280 by 768 for the text floor. It refuses
    800 by 480, 640 by 360, 1274 by 720 and 1280 by 533 for overlapping cut-outs, and 400 by 300 for a cut-out off
    the image.
  - Of 100 sizes from 640 by 360 to 1920 by 1080, the zones are used for exactly the 12 from 1280 to 1440 px
    across and 534 to 720 px high. Wherever they are used, no two cut-outs share pixels and a 14 px line keeps
    0.6°.
  - The afterburner, power, weapon mode, mining and time dilation lines, placed through the HUD's text layout with
    its reserved boxes, land in the systems cut-out. The test reserves only the two boxes next to the lines, the
    chat pane's slot and the 320 by 120 box; the layout's other reservations are not modelled.
  - The systems cut-out starts no further right than the weapon status list, or the zones are refused. No test
    checks the list's height.
  - There is no "status" cut-out (round 2's, withdrawn).
  - The flight-path marker's strokes are the art's two brackets: their radius, span, taper and grey.
- The zone and geometry tests: 241 of 241, after the fourth look. The whole suite: 8 failures, in shader hashes, the voice catalog,
  the mouse wheel, profile storage and the screenshot path, none in the VR HUD code; the same 8 as before
  layer 4.
- 26 mutants of the zone code, the text layout and the flight-path marker's shape, each caught by a test (one added in round 4 lowers the floor to 0.595°; the nine
  added for the fourth look: the target pane not grown, the notices or the port's lines put back in zone C, the
  pane's cut-out given the notices' strip, zone B narrowed again, and four that change the marker's radius, span,
  taper or colour). Three remove one check of
  `ClassicLayout` each, one lowers its floor, one lets the text layout ignore its reserved boxes, and one drops
  the systems cut-out's limit at the weapon status list. The first run had 11 mutants: 10 were caught, and the one left, a larger target margin, was a
  valid layout, so the margin's lower bound got a test of its own.
- A 60 s training run in VR under SteamVR, with nobody wearing the headset. The mirror window showed the minimap and
  location in D at the left, the joined systems pane in E at the right, and the wrapped port lines in C. Zone B
  was above the view of the headset, which lay pitched 14° down.

**Known gaps.**
- The target pane is grown by magnifying its pixels from the HUD image, about 2 times, so its text and preview
  are softer than the rest of the HUD. Drawing the pane larger in the HUD image instead would make it sharp.
- Zones only for an image near 1280 by 720: 1275 px across up to about 1490 to 1550, and 534 to 760 px high
  (above). A larger window, such as
  1920 by 1080 through `--window` or a resize, gets the whole image on one panel. There the text is smaller than
  0.6°, as it was on the step 2 panel. Zones for every size would need cut-outs taken from each element's own drawn
  rectangle at that size, rather than from fixed anchors.
- The training callouts are drawn near the image's centre, which no cut-out holds, so VR does not show them.
- In the flat game too, the weapon status list's panel is drawn after the power, weapon mode, mining and time
  dilation lines that the text layout moves into its area, and its rows overlap them *(review round 3, read from
  the draw order, not seen in a capture)*. The systems cut-out shows that as it is.
- The weapon status list's panel starts at y 150 and grows by three lines, about 51 px, for each mounted system.
  The joined systems cut-outs end at the minimap's top, y 486 at 1280 by 720 and y 355 at 1280 by 534. So with
  about seven or more systems at 720 px high, or four or more at 534, the lower rows are not shown in zone E
  *(review round 4, read from `HudRenderer.DrawWeaponStatusList` with a 17 px line, not seen in a capture)*.
- The ordinary tips at the foot of the image are cut out with the target zone, not with comms.
- The objectives' bearing arrows fall in the systems cut-out.
- A vote banner or critical tip wider than 480 px is clipped at the strip's edges.
- The keys hint, drawn smaller to fit, can fall just under the 0.6° floor.
- The zones are fixed to the seat, as the flight block is. When the head moves off the seat's origin, the 2 m zones
  shift against the 10 m flight block (by about 3° for 0.1 m). The smoke run's headset lay off the origin, so its
  port lines sat beside the speed readout. The gap between the flight block and the lower zones is 2°, so a lean
  toward them could bring them onto the ring's readouts. That is for ragesaq's headset look.

## Layer 4 in the headset (2026-11-27, fourth look)

ragesaq's notes at `c82dea6`, and what the same branch changed:

- "panel distance looks good", "comms look good": no change.
- "there is a text log on top of the secondary target screen below. showing hte secondary target screen is higher
  priority and it should get scaled up". The text was the port's weapons and cargo lines. Zone C placed them
  right beside the target pane, 0.2° apart, against its RANGE readout *(Port's printout of the placements at
  1280 by 720: the pane's cut-out ended at 1.3°, the lines began at 1.5°)*. The pane itself filled only 284 by
  118 px of its 318 by 331 px cut-out; the rest is the strip kept above it for the leader, commandee and
  mouse-stick notices *(a dump of the VR HUD image, made with a local probe that was not committed)*. Zone C now
  holds the pane alone and grows it to fill the zone, about 1.96 times at 1280 by 720. The notices' strip and the
  port's lines go beside the chat in zone B, which is widened to -26° .. 26°, as wide as the flight block below it.
  The decision table records the choice. In a 60 s run's mirror window the pane fills zone C and the lines sit at
  the right of comms; the chat keeps its size.
- "the update rate of the grey vector directional heading is a bit low, it looks choppy and low res". The
  flight-path marker read the server's latest velocity, which changes only when an update arrives, about 20 times
  a second, while the view it is drawn in follows the local prediction every frame. It now reads the prediction's
  velocity, as the camera does (a boarded gunner still reads the carrier's) *(a local probe, not committed,
  counted over 5 s: the marker's direction changed in 376 of 376 frames, the server velocity in 100)*. The flat
  game's marker is unchanged. The marker was the 32 px `directionbmp` bitmap magnified about 3 times in the eye
  image; it is now the same two grey brackets drawn as strokes at the eye image's resolution
  (`VrHudGeometry.FlightPathMarkerStrokes`), at the same size and colour. Its sharpness is for the next look.

## Open questions

- Whether ragesaq accepts the layout criteria and zones above, and where layer 4 builds them. *(Answered
  2026-11-26: accepted, in its own pull request stacked on PR #22.)*
- Which skin the VR HUD is built from. *(Answered 2026-11-26 by ragesaq: Classic's ring, decision table.)*
- Why a replay frame capture (`ALLEGIANCE_BENCHMARK_FRAME` with `--benchmark replay:...`) draws the same HUD
  whatever skin `hud.json` names, though the game reads and saves that file *(observed 2026-11-26)*. Until it is
  found, a flat byte comparison checks only the skin the capture shows ("Step 2 as built").
- Whether Jet must leave its default-off flag before the VR HUD depends on it, and how that is coordinated with
  the upstream author (node `ragesaq-012`).
- What the doubled elements in the milestone 2 screenshot are. They are not cockpit-model screens (none exist); a
  headset look with Classic, then Modern, would tell. *(Partly answered 2026-11-26: on the Classic panel they were
  the centre group and the target pane over each other, "Step 2 in the headset". Modern in VR is not looked at.)*
- Whether the lower zones, 2° below the flight block and 8 m nearer, stay clear of the ring's readouts when the
  head leans, or need a larger gap ("Layer 4 as built").
- What the desktop window shows in VR mode. *(Answered in step 1: a mirror of the left eye.)*
- How HUD pixels should be written to depth, if depth is ever submitted for reprojection *(read: SteamVR names
  `XR_KHR_composition_layer_depth`, [Steam partner docs](https://partner.steamgames.com/doc/steamframe/compat/perf_criteria);
  no source found on HUD pixels)*.

## Timeline

- 2026-11-25: ragesaq's headset test of milestone 2 found the HUD too close and large; ragesaq decided the HUD goes
  into the eye images, set the layer depths and the input constraint; Port gathered sources (fighter HMDs, VR
  platform guidance, VR games) and wrote this spec.
- 2026-11-25: the upstream author pointed ragesaq at the Jet skin. Port read it and the upstream cockpit study
  (P1044): Jet's elements fall into the four layers, and P1044 splits the flat game the same way.
- 2026-11-25: ragesaq accepted the target information split: the marker, lock, lead and short label at the target's
  depth, with an angular floor; the target's details on a close panel.
- 2026-11-25: ragesaq accepted the spec as it stands, including layer 3 and building on the Jet skin.
- 2026-11-26: ragesaq decided the VR HUD shows no horizon cues (pitch ladder, heading tape, roll pointer) and
  follows space-game HUDs and original Allegiance; step 2 was changed to match.
- 2026-11-26: Port built step 2 (layers 1 and 3, Jet only) on `port/vr-hud-step2` (above).
- 2026-11-26: ragesaq chose Classic's ring gauge for the flight block, whatever skin is active; step 2 was rebuilt on
  it and the Jet-based block removed.
- 2026-11-27: ragesaq's fourth headset look (layer 4): the target pane gets zone C to itself and is grown, the
  port's lines and the target notices move to zone B, and the flight-path marker follows the prediction every
  frame and is drawn at the eye's resolution (above).
- 2026-11-26: review round 4 of layer 4 (PASS-WITH-CONDITIONS): the upper width edge and the status cut-out's
  removal are pinned by tests, and the note says how far down the weapon status list is shown (above).
- 2026-11-26: review round 3 of layer 4 (WARN): round 2's finding was wrong, as the text layout moves those lines
  into the systems cut-out; the status cut-out is withdrawn, and the systems cut-out keeps the weapon status
  list whole (above).
- 2026-11-26: review round 2 of layer 4 (WARN): the afterburner, weapon mode, mining and time dilation lines were
  read as in no cut-out, and got one of their own in the systems zone (withdrawn in round 3).
- 2026-11-26: review round 1 of layer 4 (WARN): the zones are used only for the image sizes whose layout they hold,
  and four more in-flight full panes are shown whole (above).
- 2026-11-26: Port built layer 4, the zones, for the Classic skin on `port/vr-hud-zones` (above), stacked on
  PR #22.
- 2026-11-26: ragesaq found the panel's text on the ring and asked for layout criteria covering every element;
  ragesaq accepted Port's zones with the panels at 2 m, to be built as layer 4 in its own pull request.
- 2026-11-26: ragesaq's third look at step 2 (above): the box floor lowered, boxes upright to the ship, the
  ring 10 m out and sharper, the port's lines under the chat pane, the thrust gauge and missiles fixed.
- 2026-11-26: ragesaq's headset look at step 2 (above): the ring kept; the flight-path marker, the targeting
  boxes, the port's text lines and the target preview changed for VR.
- 2026-11-26: PR #17 merged; ragesaq's headset look at it "looked good".
- 2026-11-25: Port built the layer 2 follow-up on `port/vr-hud-bracket-depth` (above). ragesaq accepted Port's
  smaller version of a reticle-depth prompt from another assistant: a target under the reticle gives it depth.
- 2026-11-25: ragesaq tested step 1 in the headset (above): the brackets' depth confused, the boresight was right;
  the aim reticle stays at infinity.
- 2026-11-25: Port built step 1 on `port/vr-hud-in-eyes` (above). From a first look in the headset, ragesaq asked
  that targeting reticles be farther than 1 m; the flat crosshair left the near panel.

## Sources

Fighter HUDs and HMDs: [F-35 JPO, HMDS](https://www.jsf.mil/hmds) ·
[Collins, JHMCS](https://www.collinsaerospace.com/what-we-do/Military-And-Defense/Displays-And-Controls/Airborne/Helmet-Mounted-Displays/Joint-Helmet-Mounted-Cueing-System) ·
[BAE, Striker II](https://www.baesystems.com/en/product/striker-ii-digital-helmet-mounted-display) ·
[DTIC, HUD design guide (Newman)](https://apps.dtic.mil/sti/html/tr/ADA331767/index.html).
VR guidance: [Meta, display](https://developers.meta.com/horizon/design/display/) ·
[Microsoft, comfort](https://learn.microsoft.com/en-us/windows/mixed-reality/design/comfort) ·
[Microsoft, typography](https://learn.microsoft.com/en-us/windows/mixed-reality/design/typography) ·
[Google Daydream, archived](https://developers.google.com/vr/distribute/daydream/design-requirements).
VR games: [Damaged Core crosshair](https://www.gamedeveloper.com/design/in-vr-even-something-as-simple-as-a-crosshair-is-i-not-i-easy-to-do-) ·
[EVE Valkyrie design lessons](https://gamedeveloper.com/design/-text-looks-rubbish-and-other-vr-design-lessons-learned-on-i-eve-valkyrie-i-).
Composition layers: [Khronos forum, mirror misses quad layers](https://community.khronos.org/t/mirror-openxr-to-monitor/110950) ·
[Unity, composition layer order](https://docs.unity3d.com/Packages/com.unity.xr.compositionlayers@2.4/manual/overview.html).
