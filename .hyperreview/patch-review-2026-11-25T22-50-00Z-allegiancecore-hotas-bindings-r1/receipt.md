# Receipt: cross-family patch review of PR #14 (joystick bindings for a split HOTAS), round 1

Path-free record of the launch, written by the author (Port) from the launcher's log and the wrapper state, which stay
on the reviewing host because they hold machine paths that `GROUND_RULES.md` keeps out of this public repository. The
reviewer wrote only `summary.md`.

| Field | Value |
|---|---|
| Mode | HyperReview patch-review |
| Target | ragesaq/AllegianceCore-public PR #14, branch `port/hotas-bindings`, stacked on `port/hotas-plan` (PR #8) |
| Reviewed head | `b32f0e6255813b3e1eabc7737b4bfee02de3ca55` |
| Reviewed tree | `83727f80ea1345096204f251c163d1a3e3e6f61f` (unchanged after the run) |
| Range | `ff8e8a64..b32f0e62`, the whole pull request |
| Author | Port (Claude Code, Claude Opus 5.5), family anthropic |
| Reviewer | `deepseek-v4.1-flash`, provider `opencode-go`, effort `high`, family deepseek |
| Route | `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on |
| Route source | `AGENTS.md`, "Cross-family review route" (operator direction 2026-11-24), explicit selection |
| Matrix | reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Runtime | HyperCodex `codex-cli 0.169.0+hc.1`, review run in a standalone clone with no remote and no branches |
| Effective model | session banner `model: deepseek-v4.1-flash`, `provider: opencode-go`; wrapper route `opencode-go deepseek-v4.1-flash high` |
| Wrapper result | status `complete`, no forced kill, launcher `rc=0` |
| Started / finished | 2026-11-25T22:51:18Z / 2026-11-25T22:58:59Z |
| Clone refs | the review clone held no refs, only a detached head at the reviewed commit; the author clone's refs were unchanged |
| Summary | sha256 `460613d2b972e297bb439595f944a24843ec9a8cfadde2cdf765d03022a17d59`, committed unchanged |
| Verdict | PASS-WITH-CONDITIONS, two medium and three low findings |
