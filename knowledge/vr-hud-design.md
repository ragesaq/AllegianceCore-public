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
  turns.
- Its content is today's central gauges and readouts, laid out again for a smaller field rather than scaled down:
  the flat layout spreads across the whole 1280x720 screen.

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
| pitch ladder on the sector plane, heading tape, speed and throttle tapes | 3, flight reference at infinity |
| stores block (hull, shield, energy, fuel, ammunition), master caution, console and chat, inventory pane, radar | 4, cockpit displays |

Layer 3 is the part a fighter HUD is built around. In Jet, the pitch ladder is drawn against the sector plane,
not against the screen. A ladder like that only lines up with the world when it is drawn at infinity, which supports
layer 3 *(derived)*.

**Build the VR HUD by placing Jet's elements, not by laying out Classic again** *(proposed by Port, accepted by ragesaq 2026-11-25)*. The work becomes a VR
placement for each Jet element group, chosen by layer. Classic and Modern keep their flat layouts in VR, on one
panel, as a fallback. The flat Jet skin keeps its bytes. One cost: Jet is behind a default-off flag and still in
development upstream, so the VR placement follows its changes. That argues for doing this work with the upstream
author, not as a fork-only branch (node `ragesaq-012`).

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
| Aiming reticle depth | infinity | at the depth of the object behind it (Meta guidance, Star Wars Squadrons) | ragesaq, 2026-11 | 2026-11-25 |
| Target marker depth | the target's distance | infinity; a fixed HUD plane | ragesaq, 2026-11 | 2026-11-25 |
| Information panels | close (about 1 m), angled like cockpit displays | one flat sheet (milestone 2); panels fixed to the head | ragesaq, 2026-11 | 2026-11-25 |
| Input | joystick and keyboard, or HOTAS and keyboard; no motion controllers, no wrist UI | motion-controller UI | ragesaq, 2026-11 | 2026-11-25 |
| Target information split | marker, lock, lead and a short label at the target's depth with an angular floor; the target's details on a close panel | all target information at the target's depth | ragesaq, 2026-11 (proposed by Port, accepted 2026-11-25) | 2026-11-25 |
| Flight reference block at infinity (layer 3) | accepted | all non-marker elements on close panels | ragesaq, 2026-11 (proposed by Port, accepted 2026-11-25) | 2026-11-25 |
| What the VR HUD is built from | the Jet skin's elements, placed by layer; Classic and Modern on one fallback panel | laying out the Classic HUD again for VR | ragesaq, 2026-11 (proposed by Port, accepted 2026-11-25) | 2026-11-25 |

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
- **The aim reticle is a direction** (the ship's nose), the same pixel in both eyes, so it is seen at infinity. The
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

## Open questions

- Whether Jet must leave its default-off flag before the VR HUD depends on it, and how that is coordinated with
  the upstream author (node `ragesaq-012`).
- What the doubled elements in the milestone 2 screenshot are. They are not cockpit-model screens (none exist); a
  headset look with Classic, then Modern, would tell.
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
