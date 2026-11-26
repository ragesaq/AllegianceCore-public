# Receipt: cross-family patch review of PR #16 (target center by angle off the nose), round 1

Path-free record of the launch, written by the author (Port) from the launcher's log and the wrapper state, which stay
on the reviewing host because they hold machine paths that `GROUND_RULES.md` keeps out of this public repository. The
reviewer wrote only `summary.md`.

| Field | Value |
|---|---|
| Mode | HyperReview patch-review, round 1 |
| Target | ragesaq/AllegianceCore-public PR #16, branch `port/target-center-native`, base `master` `3a3b6e1b` |
| Reviewed head | `ece6de19562f39c5fbe75d88bb4380911bbcdcd8` |
| Reviewed tree | `21598bba6aa2e2d523325bbc19c1cd08dcb93ab0` (unchanged after the run) |
| Author | Port (Claude Code, Claude Opus 5.5), family anthropic |
| Reviewer | `deepseek-v4.1-flash`, provider `opencode-go`, effort `high`, family deepseek |
| Route | `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on |
| Route source | `AGENTS.md`, "Cross-family review route" (operator direction 2026-11-24), explicit selection |
| Matrix | reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Runtime | HyperCodex `codex-cli 0.169.0+hc.1`, review run in a standalone clone with no remote, no branches and no tags |
| Effective model | session banner `model: deepseek-v4.1-flash`, `provider: opencode-go`; wrapper route `opencode-go deepseek-v4.1-flash high` |
| Wrapper result | launcher `rc=0`, no forced kill |
| Started / finished | 2026-11-26T01:03:10Z / 2026-11-26T01:15:54Z |
| Clone refs | the review clone held no refs, only a detached head at the reviewed commit |
| Summary | sha256 `2f284334650673c94d3147eda38a3d9b41996a7d6793f9d6712a83fdd8c9379b`, committed unchanged |
| Verdict | PASS-WITH-CONDITIONS: F1 medium (record names the wrong consumer of the tiered list), F2 medium (turret axis), F3 low (stale wording) |
