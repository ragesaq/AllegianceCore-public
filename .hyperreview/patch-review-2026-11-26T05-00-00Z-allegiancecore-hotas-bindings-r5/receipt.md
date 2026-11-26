# Receipt: cross-family patch review of PR #14 (joystick bindings for a split HOTAS), round 5

Path-free record of the launch, written by the author (Port) from the launcher's log and the wrapper state, which stay
on the reviewing host because they hold machine paths that `GROUND_RULES.md` keeps out of this public repository. The
reviewer wrote only `summary.md`.

| Field | Value |
|---|---|
| Mode | HyperReview patch-review, delta round 5 |
| Target | ragesaq/AllegianceCore-public PR #14, branch `port/hotas-bindings`, stacked on `port/hotas-plan` (PR #8) |
| Reviewed head | `3f99264631d088129a62de26c0d023d705d4be39` |
| Reviewed tree | `9223facc8e66fb17228d84d68b68e4bab0e2c271` (unchanged after the run) |
| Range | `c6df4d73..3f992646`: the round 4 record and fixes, the afterburner moved to button 75 |
| Author | Port (Claude Code, Claude Opus 5.5), family anthropic |
| Reviewer | `deepseek-v4.1-flash`, provider `opencode-go`, effort `high`, family deepseek |
| Route | `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on |
| Route source | `AGENTS.md`, "Cross-family review route" (operator direction 2026-11-24), explicit selection |
| Matrix | reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Runtime | HyperCodex `codex-cli 0.169.0+hc.1`, review run in a standalone clone with no remote, no branches and no tags |
| Effective model | session banner `model: deepseek-v4.1-flash`, `provider: opencode-go`; wrapper route `opencode-go deepseek-v4.1-flash high` |
| Wrapper result | status `complete`, no forced kill, launcher `rc=0` |
| Started / finished | 2026-11-26T01:58:02Z / 2026-11-26T02:05:08Z |
| Clone refs | the review clone held no refs, only a detached head at the reviewed commit; no ref moved in the author clone |
| Summary | sha256 `695b88fe13d3b884e1d00f62970602d980e5de173a47f52f3f81d97faf3a1ebe`, committed unchanged |
| Verdict | PASS-WITH-CONDITIONS: N1 and N2 fixed; R5-1 to R5-3 low (the reset untested, a stale note sentence, the node's date) |
