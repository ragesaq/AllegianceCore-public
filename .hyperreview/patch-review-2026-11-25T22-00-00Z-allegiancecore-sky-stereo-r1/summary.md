# HyperReview patch review — VR sky at infinity in each eye

- Repository: AllegianceCore VR fork (ragesaq/AllegianceCore-public), pull request #11.
- Reviewed head: `9db16a45b0a9c2e246f1ddbd20dc767d9d47630c`
- Reviewed tree: `72c2a09ca361cd5fb20da7a2aed9fcdc7cd19915`
- Change: one commit, `8ccacfa9..9db16a45`; parent `8ccacfa9` is the head of PR #10.
- Reviewer: DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, reasoning effort `high`; reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`; explicit selection per the target's `AGENTS.md` "Cross-family review route".
- Authoring family: Anthropic/Claude (agent seat Port). Relation: cross-family.
- Verdict: **PASS**
- nextAction: ragesaq's in-headset check that the starfield, planets and galaxies converge; then merge approval after PR #10.

## What the change does

`NativeSkyPlacement.SkyProjection` rebuilt the sky's projection from the scene projection's
`M11` and `M22` only. A VR eye's projection is off-centre (`M31`, `M32` from
`VrEyeCamera.OffCentreProjection`), so the rebuild centred each eye's sky and the two eyes
diverged. The change returns the old rebuild when both terms are zero, and otherwise copies
the scene projection and replaces only `M33` and `M43` with the rebuild's near-1/far-80000
depth terms. All three sky tiers call it (`BlackSkyRenderer.cs:151`,
`NativeSkyRenderer.cs:169`, `HdSkyRenderer.cs:161`). A unit test and records were added.

## Findings

None. No critical, major or minor defect found in the change. Items 1 to 4 below are
observations; none changes the verdict.

## Verification performed

By execution, read-only, against the frozen tree:

- `git rev-parse HEAD` and `git write-tree` match the frozen head and tree; `git status
  --short` showed nothing before the review wrote its artifact directory.
- `git diff --stat 8ccacfa9 HEAD`: 4 files, +59/-6 — `DAG.json` 2/2,
  `knowledge/vr-client-survey.md` 11/3, `NativeSkyPlacement.cs` 12/1, the test 34/0.
- `git diff --check` clean. `git ls-files --eol`: both `.cs` files are `i/lf` (committed
  bytes LF), same as untouched `.cs` files; the `w/crlf` is this clone's
  `core.autocrlf=true`, not the change.
- `scripts/publish-guard.sh 8ccacfa9 HEAD`: OK (1 commit).
- `tools/check_data_boundary.py`: OK. `tools/check_memory.py`: OK (10 entries; only the
  documented `people/` warnings, and the entry-deletion baseline is unavailable in a clone
  with no git baseline).
- `git grep` for writes to `M31`/`M32` in `src`, `tests`, `tools`: none. `git grep` for
  `CreatePerspectiveOffCenter`: no production call; `VrEyeCamera.cs:51-55` builds the only
  off-centre projection by hand. No projection jitter in the tree.

By reading the code and the projection algebra:

- XNA/MonoGame right-handed convention confirmed: `M33 = far/(near-far)`, `M34 = -1`,
  `M43 = near*far/(near-far)`, so NDC z is 0 at near and 1 at far (as the existing
  `SkyProjection_EnclosesTheDomeRadius` test already pins).
- The eye branch changes only `M31`/`M32`. The rebuild's `M11`/`M22` are the round-trip of
  the scene's (`vFov = 2 atan(1/M22)`, `aspect = M22/M11`), which equals the eye's
  `2/(r-l)` and `2/(u-d)` within float round-trip; the eye's other terms are zero, as in the
  rebuild; `M33`/`M43` become the same 1..80000 terms the old eye path already used. So the
  sole behavioural delta in an eye pass is keeping the off-centre terms — exactly the
  diagnosed cause.
- Depth range holds every sky element: dome 7500 (z_ndc 0.99988), star shells 7000
  (`NativeSkyRenderer.cs:41`), HD star dome 7110 (`HdSkyRenderer.cs:44`), planet 6650/6960
  (`BlackSkyRenderer.cs:39`), objects 7800 (`BlackSkyRenderer.cs:44`), giant patch radius
  6600 (`GiantObjectRenderer.cs:147`). All three tiers draw the sky with
  `DepthStencilState.None` (`BlackSkyRenderer.cs:161`, `NativeSkyRenderer.cs:179`,
  `HdSkyRenderer.cs:170`), so these depth terms only clip; they do not occlude.
- Nothing in the sky path assumes a symmetric projection. `SimStarMath.Project`
  (`SpaceVisualMotion.cs:74-82`) transforms with the full projection, so star points and
  tails now land at the eye-correct pixel and stay attached (`SpaceStarRenderer.cs:22-88`).
  `LensFlareRenderer.cs:104` projects with `view*projection` and uses only x/y;
  `DiscHalfPixels` uses `M22`, unchanged. Billboards, domes, the giant patch and the galaxy
  field draw world-space geometry through the projection and derive no FOV/aspect/focus
  from it. No sky code builds a frustum for culling.
- Flat path cannot reach the new branch today: no production code writes `M31`/`M32`;
  `RadialVisibility.WithFarPlane` (`RadialVisibility.cs:87-95`) and the camera helpers touch
  only `M33`/`M43`. So flat calls return the old rebuild unchanged and replay frames are
  bit-identical.
- FOCUS 3 sweep inside `Game1.Rendering.cs:543-1754`: only `proj.M22` reads (line 672
  comment, line 799 sizing); no projection is constructed there. The centre projection is
  captured at line 540, before the loop, and each eye's projection is built in
  `BeginVrScenePass` (`Game1.Vr.cs:64-73`). The remaining `CreatePerspectiveFieldOfView`
  sites in the client feed the centre camera (ChaseCamera, CockpitCamera, CommandCamera,
  MissileCamera, Director, `CameraEasing.cs:161`) or run outside the eye pass:
  `AlephGlimpseRenderer.cs:481` at `Game1.Rendering.cs:1777` (after `EndVrScenePasses`,
  gated by `!_vrEyesDrawn`) and the docked hull previews at `Game1.Rendering.cs:5165` and
  `5190`. No same-class defect inside an eye pass.
- Tests fail for the right reason on old code: with `M31 = M32 = 0`, direction `(0,0,-1)`
  gives x/w `-M31` (about 0.1067) against 0, far outside the 1e-5 bound; the other ten tests still pass on the old code, as the author reports,
  are untouched. The new test builds its eye matrix with `Matrix.CreatePerspectiveOffCenter`,
  the same constructor that `VrEyeCameraTests.cs:87-101` pins
  `VrEyeCamera.OffCentreProjection` against, so the chain is tied even though this test does
  not call `VrEyeCamera` directly.
- Records: `DAG.json` `ragesaq-009` is `review`, `updated` 2026-11-25, and links
  `knowledge/vr-client-survey.md`, a repo-relative regular file, which satisfies the rules
  block's blocking link floor. The note's sky entry, trap and timeline agree with the code.
  The diff contains no machine path, secret or email address other than the agent noreply
  in the commit trailer.

## Observations

1. `NativeSkyPlacement.cs:77`: the exact float test is the right discriminator today. If a
   later flat path ever adds sub-pixel projection jitter through `M31`/`M32`, the sky will
   inherit it. That is the desirable coupling, but it deserves a comment at that time.
2. The off-centre test exercises one eye's angles. The copy is sign-symmetric, so the
   mirrored eye follows, but a both-eyes parameterisation would pin that.
3. `SkyProjection_SymmetricSceneProjection_IsTheFieldOfViewRebuild` (test lines 138-146)
   checks the flat output numerically. For a standard symmetric perspective the copy branch
   would produce the same matrix, so the test does not pin the early return. That is
   acceptable: the two are numerically equivalent for such inputs.
4. `DAG.json` `ragesaq-009` names the branch and PR #10 but not PR #11 or the head. The rules
   block's prose also asks to pin exact heads and ids; add the PR number and head when known,
   or at merge.

## Verification limits

No `dotnet` build, test run, game run or headset was available, so the author's runs (11/11
unit tests, the 7-failure MonoGame baseline before and after, the 0-warning build, the flat
replay hashes, the 60 s stereo run) were not reproduced here. In particular the stereo run
was made without the headset worn and cannot show convergence; that remains the next
acceptance step and residual risk, not a defect in this change.