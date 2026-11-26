# Upstream submission: what the upstream author asks of the VR and HOTAS pull requests

**Owner:** ragesaq · **Started:** 2026-11-26 · **Last reviewed:** 2026-11-26 · **Review by:** 2027-02-26

**Status:** in progress

## What this answers

What the upstream project's author requires before the VR client and the HOTAS bindings can be taken into the
upstream repository, where this fork does not meet it yet, and how the work is split into pull requests there.

## Current state

- ragesaq plans to send this fork's work to the upstream repository, and then to work from it directly without this
  fork. The upstream author wants to test with a Quest 3 *(ragesaq, Port's working session 2026-11-26)*.
- ragesaq wants this fork's open pull requests merged first, before the upstream pull requests are made *(ragesaq,
  same session)*.
- The upstream author's requirements are listed below, as ragesaq relayed them *(the upstream author, relayed by
  ragesaq 2026-11-26)*. Where the fork does not meet one yet is marked "gap".

## Requirements and where the fork stands

### Tests

| Requirement | Fork now | |
|---|---|---|
| VR math and plumbing that does not touch OpenXR (`VrEyeCamera`, `VrFrameSinkHost`, `VrScenePass`, from `src/Allegiance.ClientCore.Game/Vr/`) is tested in `tests/Allegiance.ClientCore.MonoGame.Tests/Vr/` | The VR tests sit at the top of `tests/Allegiance.ClientCore.MonoGame.Tests/` *(read, branch `port/vr-recenter` at `7d3b45f`)* | gap |
| Anything using Silk.NET OpenXR (`OpenXrPresenter`, `VrSessionLifecycle`, `VrOptions`) is tested in a new project `tests/Allegiance.ClientCore.Vr.Tests/` that references `src/Allegiance.ClientCore.Vr` | `VrPresenterTests.cs` and `VrRecenterTests.cs` use `OpenXrPresenter` from the MonoGame test project *(read, same branch)* | gap |
| The MonoGame test project does not reference the `.Vr` project, so that suite does not depend on OpenXR | It does: `Allegiance.ClientCore.MonoGame.Tests.csproj` line 52 *(read, same branch)* | gap |
| Tests that need a real OpenXR runtime or headset skip cleanly without one, through a `Require.OpenXrRuntime()` guard beside `Require.Artwork()` in `tests/_shared/TestRequire.cs`, and carry `[Trait("Tier", "Live")]`; option parsing, the no-loader failure path and the eye and camera math stay in the fast tier | No `Require.OpenXrRuntime()` exists *(read, same branch)* | gap |
| The new test project is added to `AllegianceCore.slnx` | Not created yet | gap |

### VR off by default

- VR stays opt-in. With it off, the desktop client must behave and render exactly as today, with no OpenXR loader
  probing.
- The fork's switch is `--vr` or `ALLEGIANCE_VR=1` (`src/Allegiance.ClientCore.Vr/VrOptions.cs`) *(read, same
  branch)*. Flat replay frames have been byte-identical with VR off in the fork's reviews *(PR #14 and PR #15
  bodies)*. That no loader is probed with VR off is not yet checked: to be verified before the upstream pull request.

### General

- Build with 0 warnings and 0 errors; the upstream CI must pass.
- No machine paths, IP addresses or secrets in committed files. The fork's `GROUND_RULES.md` and
  `tools/check_data_boundary.py` already enforce this here.
- Large media (screenshots, captures) only when really needed; link to them otherwise.
- Smaller pull requests: ideally one per milestone. The upstream author named: M1, the flat frame in the headset;
  M2, the per-eye pose with the HUD on a quad; and the HOTAS separately.

### Known issues to fix or note as follow-ups

Seen by the upstream author in a SteamVR screenshot:

1. The HUD layers are drawn twice.
2. The HUD quad clips into the station geometry.
3. The "Coming home empty" chat message repeats.

Not yet checked against the fork's current branches. The HUD quad of M2 is gone in PR #15, which draws the HUD into
the eye images, so items 1 and 2 may describe the M2 build only *(derived, from the PR #15 body)*.

## How it applies here

- The upstream split does not match the fork's pull requests one to one. The fork has: M1 (merged), M2 (merged),
  the HUD in the eye images (PR #15) and its follow-up (PR #17), the VR recenter button (PR #18), the HOTAS bindings
  (PR #8, PR #14) and the native target-center rule (PR #16, not VR). Mapping these onto the upstream author's
  M1, M2 and HOTAS pull requests is ragesaq's decision.
- ragesaq proposes two upstream collections, one for inputs and one for VR, made after the fork's pull requests
  merge *(proposed 2026-11-26 by ragesaq, Port's working session: "group our changes into two large collections. One
  for inputs, and one for VR")*. Not decided. Inputs would hold the HOTAS bindings (PR #14) and the native
  target-center rule (PR #16); VR would hold M1, M2, the HUD in the eyes (PR #15, PR #17) and the recenter (PR #18).
  The recenter's joystick command joins the command list the HOTAS bindings add, so the VR collection builds on the
  inputs one, or that command moves to inputs *(derived, PR #18 is stacked on PR #14)*. The upstream author asked for
  smaller pull requests, one per milestone; a VR collection kept as one commit series per milestone would fit both
  *(derived)*.
- HyperBench files (`AGENTS.md`, `DAG.json`, `LEDGER.md`, `memory/`, `knowledge/`, `.hyperreview/`, `tools/`
  checks) stay out of any upstream pull request *(`AGENTS.md`, prime rule 3)*.
- Upstream pull requests go out under ragesaq's GitHub identity, not an agent's *(operator rule for public
  upstream repositories)*.

## Timeline

- 2026-11-25 — ragesaq raises sending the VR client upstream (DAG node `ragesaq-012`).
- 2026-11-26 — ragesaq relays the upstream author's requirements; this note records them and the gaps.

## Sources

- The upstream author's message, relayed by ragesaq in Port's working session, 2026-11-26.
- Fork code at branch `port/vr-recenter`, commit `7d3b45f`.
