# Patch review: VR milestone 2, round 2 (delta)

- Target: `ragesaq/AllegianceCore-public` PR #9, branch `port/vr-m2`, stacked on `port/vr-m1` (PR #7).
- Reviewed head: `917f29baa447d17b78daad8e20d0634e53ffafdc`; tree `a928ba9768dc32087493de35aacbe2b8ef63e204` (matched `git write-tree`).
- Range: `3677ec6f..917f29ba`, one commit answering round 1 (`f945af76..3677ec6f`).
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high` (reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; explicit selection, the route declared in `AGENTS.md` "Cross-family review route").
- Author family: Anthropic/Claude (Claude Code, Claude Opus 5.5, seat Port). Cross-family.
- Clone: standalone detached clone with no remote, `<review-clone>`.
- **Verdict: PASS.** All four round 1 findings are closed or correctly disputed, and the eye-swapchain limit is resolved. One Low documentation finding remains; it is comment text only and does not block the headset test or merge, but the gate asks for each finding to be fixed or answered.

## Round 1 dispositions

| Round 1 item | Status |
|---|---|
| F1 (Medium): `BLEND_TEXTURE_SOURCE_ALPHA_BIT` premultiplies the HUD twice | **Dispute accepted.** The flag does not multiply colour; the round 1 reading was wrong. |
| F2 (Low): debris field stepped twice per frame | **Closed.** The guard is complete and the flat path is unchanged. |
| F3 (Low): eye targets outlive a dropped sink | **Closed.** The release point is safe; no pending submit, no double dispose. |
| F4 (Low): aleph glimpse lands on the HUD layer | **Closed.** Skipped when the eyes were drawn; documented as a limitation. |
| Limit: side-by-side eye swapchain twice the recommended width | **Resolved.** One swapchain per eye at the recommended size, with correct per-view sub-images. |

## F1: the dispute is correct

The OpenXR specification, "Composition Layer Blending" (rendering chapter), says:

- If `XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT` is unset, "the layer alpha is initialized to one".
- `XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT` is the bit that multiplies colour: "LayerColor.RGB *= LayerColor.A".
- The blend is `CompositeColor = LayerColor + CompositeColor * (1 - LayerColor.A)`.

Source-alpha selection and premultiplication are separate. The blend bit only lets the texture alpha reach the blend equation; it never multiplies colour. Round 1's claim that the flag "tells the compositor to multiply the layer's colour by its alpha" was a misreading, and its proposed fix (drop the flag) would have made `LayerColor.A` one and the HUD an opaque sheet hiding the eyes.

The other half of the premise also holds: the HUD buffer is premultiplied. `HudRenderer.PremultipliedTint` documents "the HUD's premultiplied `BlendState.AlphaBlend` batch", HUD textures go through `MdlTextureDecoder.ToPremultipliedRgba`, and MonoGame's `BlendState.AlphaBlend` is `One`/`InverseSourceAlpha`, which preserves premultiplied colour over the transparent clear. The current pairing (blend bit set, unpremultiplied bit unset) is correct. If the buffer is ever changed to straight alpha, the pairing must flip to `UNPREMULTIPLIED`; the headset remains the final check on HUD edges.

## New findings

### L1 (Low, documentation): `ReadEyeSize` still describes the old one-swapchain layout

`src/Allegiance.ClientCore.Vr/OpenXrPresenter.cs:244` says "both eyes share one size so one side-by-side swapchain holds them", but the delta moved to one swapchain per eye (`OpenXrPresenter.cs:379-380`, `392`) and updated the class summary and `GLOSSARY.md` only.

Failure scenario: a future editor trusts the comment, concludes the per-eye split is redundant, and puts both eyes back on one doubled swapchain or sizes one from `maxSwapchainImageWidth`. That reintroduces the width problem round 1 flagged, from a comment the code now contradicts.

Fix: reword to "the runtime's recommended image size per eye, shared by both eye targets and by each eye's swapchain". Comment only.

## What I verified

By execution (read-only, on the frozen clone):

- `git rev-parse HEAD` and `git write-tree` matched the stated head and tree; `git status --short` was empty.
- `git ls-files --eol` on the nine changed files: the index side is `i/lf` for every file, including the upstream `Game1.Rendering.cs`. No line-ending renormalization was committed; the CRLF in the working tree is the host checkout.
- `scripts/publish-guard.sh 3677ec6f HEAD` reported OK for one commit.
- `tools/check_data_boundary.py` reported OK. `tools/check_memory.py` reported OK with the nine expected `recorded-by 'port'` warnings; its entry-deletion check could not run in this detached clone (no baseline), which the tool reports as a limit.
- The delta's added lines contain no absolute or machine path and no email address.

By reading, with call paths traced:

- F2: `advance: false` skips `SubtleDust.Step` and `DebrisGeo.Step`, so `_accumulated`, `_speed`, `_timeLast`, `_posL*`, `_distancePerParticle` and the spawn RNG are untouched. What remains is `subtleDust.Reset()` (sets `_ready = false`, idempotent, and `SubtleDust` is unused in that branch), `output.Clear()` and a re-projection that reads only `Points`, `Brightness`, the plain `Density` getter and the eye camera. The other call site (`Game1.Scene.cs:157`) takes the default `advance: true`.
- F2 test: the first call leaves the field empty (zero-length first step), so the "unchanged" assertion is not vacuous: without the guard the second call (50 m, 1 s) spawns about 71 grains and fails. The author reports seeing that failure. Both assertions use xUnit's enumerable overloads (item-by-item), and `DebrisPoint` is a record struct.
- F3: `VrFrameSinkHost.Submit` and `Drop` run synchronously on the render thread, so no submit is pending at the next `BeginVrFrame`; `EndVrScenePasses` and present leave the back buffer bound; `VrEyeTarget` re-creates on demand after a re-attach; nulling makes a later `DisposeVrSink` a no-op. Release also runs when `BeginFrame` itself drops the sink in the same frame, before the flat draw.
- Per-eye swapchains: each view names its own swapchain with rect `(0,0,eyeWidth,eyeHeight)` and array index 0; `EnsureSwapchain` destroys and re-creates on a size change; `WithImage` acquires, waits, copies and releases per swapchain with release in `finally`; tear-down destroys HUD, left and right before space/session/instance; a failed second create becomes `Fail`/`TearDown` with the first swapchain cleaned up.
- Flat path: with no host, `advance` is `true`, `_vrEyesDrawn` is reset false before the glimpse gate, and `ReleaseVrEyeTargets` is a no-op. `Game1.Rendering.cs:679` is inside the pass loop, so pass 0 steps and pass 1 only re-projects.
- Records: `DAG.json` node `ragesaq-007`, `GLOSSARY.md`, `knowledge/vr-client-survey.md` and `.hyperreview/README.md` match the code on the per-eye swapchains, the blend bit, the aleph limitation and the release-on-drop. No machine paths or non-agent addresses.

## Limits

- No `dotnet`, build, test or game run: the 0-warning build, 46 VR tests, the byte-identical replay frames, the baseline failure sets and the 90 s SteamVR log are the author's measurements.
- No headset: HUD edges, eye convergence, row-flip direction and the world-locked HUD remain for ragesaq's headset test.
- The specification text above was read from the public Khronos `rendering.adoc`; no runtime other than the author's SteamVR was exercised.
- Notes, not findings: the new test covers the helper flag, not the `advance: !VrRepeatPass` wiring, so a call-site regression would not fail a test; `EnsureSwapchain` would leak a handle if image enumeration failed after a successful create (pre-existing, none observed).