# Receipt: cross-family patch review of PR #17 (VR HUD bracket depth), round 1

Path-free record of the launch, written by the author (Port) from the launcher's log and the wrapper state, which stay
on the reviewing host because they hold machine paths that `GROUND_RULES.md` keeps out of this public repository. The
reviewer wrote only `summary.md`.

| Field | Value |
|---|---|
| Mode | HyperReview patch-review, round 1 |
| Target | ragesaq/AllegianceCore-public PR #17, branch `port/vr-hud-bracket-depth`, base `port/vr-hud-in-eyes` (PR #15) |
| Reviewed head | `47133cbf3a0e16dd44abe5d7fd5d5b437cc82046` |
| Reviewed tree | `189b0790dde88ce83cb19d624471b67366fe1cae` (unchanged after the run) |
| Range | `051f0cc3..47133cbf` |
| Author | Port (Claude Code, Claude Opus 5.5), family anthropic |
| Reviewer | `deepseek-v4.1-flash`, provider `opencode-go`, effort `high`, family deepseek |
| Route | `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on |
| Route source | `AGENTS.md`, "Cross-family review route" (operator direction 2026-11-24), explicit selection |
| Matrix | reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Runtime | HyperCodex `codex-cli 0.169.0+hc.1`, review run in a standalone clone with no remote, branches or tags |
| Effective model | session banner `model: deepseek-v4.1-flash`, `provider: opencode-go`; wrapper route `opencode-go deepseek-v4.1-flash high` |
| Wrapper result | phase `complete`, return code 0, launcher `rc=0` |
| Started / finished | 2026-11-26T01:27:18Z / 2026-11-26T01:40:04Z |
| Clone refs | the review clone held no refs, only a detached head at the reviewed commit |
| Summary | sha256 `e1432c1860480a5d00d3d4e49eaed4aca67aad20fa6274da9d7b71a997ed0583`, committed unchanged |
| Verdict | PASS-WITH-CONDITIONS; one High finding (F1), one Low (F2), six observations |
