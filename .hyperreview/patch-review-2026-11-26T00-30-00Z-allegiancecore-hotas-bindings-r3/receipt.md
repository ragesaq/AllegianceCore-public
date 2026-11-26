# Receipt: cross-family patch review of PR #14 (joystick bindings for a split HOTAS), round 3

Path-free record of the launch, written by the author (Port) from the launcher's log and the wrapper state, which stay
on the reviewing host because they hold machine paths that `GROUND_RULES.md` keeps out of this public repository. The
reviewer wrote only `summary.md`.

| Field | Value |
|---|---|
| Mode | HyperReview patch-review, delta round 3 |
| Target | ragesaq/AllegianceCore-public PR #14, branch `port/hotas-bindings`, stacked on `port/hotas-plan` (PR #8) |
| Reviewed head | `8e225d2b03f28f7399ed058dc7dc6295eb14054f` |
| Reviewed tree | `4136c7e11dad9dcae69a639a14a149c5a8bddb0a` (unchanged after the run) |
| Range | `a25c734b..8e225d2b`: thumb buttons, round 2 record, the N1 fix, the targeting commands, the throttle grip bindings |
| Author | Port (Claude Code, Claude Opus 5.5), family anthropic |
| Reviewer | `deepseek-v4.1-flash`, provider `opencode-go`, effort `high`, family deepseek |
| Route | `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on |
| Route source | `AGENTS.md`, "Cross-family review route" (operator direction 2026-11-24), explicit selection |
| Matrix | reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Runtime | HyperCodex `codex-cli 0.169.0+hc.1`, review run in a standalone clone with no remote, no branches and no tags |
| Effective model | session banner `model: deepseek-v4.1-flash`, `provider: opencode-go`; wrapper route `opencode-go deepseek-v4.1-flash high` |
| Wrapper result | status `complete`, no forced kill, launcher `rc=0` |
| Started / finished | 2026-11-25T23:47:30Z / 2026-11-25T23:58:11Z |
| Clone refs | the review clone held no refs, only a detached head at the reviewed commit; in the author clone only another session's branch moved |
| Summary | sha256 `b64b189312fcaea6092debcfd6ca67be513eff8b7d9c0021b3c1cc71ce8f18b8`, committed unchanged |
| Verdict | PASS-WITH-CONDITIONS: round 2 N1 fixed; three new low findings (R3-1 undock skips the disarm, R3-2 comment defects, R3-3 stale parity row) |
