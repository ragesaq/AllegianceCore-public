# Receipt: cross-family patch review of PR #14 (joystick bindings for a split HOTAS), round 4

Path-free record of the launch, written by the author (Port) from the launcher's log and the wrapper state, which stay
on the reviewing host because they hold machine paths that `GROUND_RULES.md` keeps out of this public repository. The
reviewer wrote only `summary.md`.

| Field | Value |
|---|---|
| Mode | HyperReview patch-review, delta round 4 |
| Target | ragesaq/AllegianceCore-public PR #14, branch `port/hotas-bindings`, stacked on `port/hotas-plan` (PR #8) |
| Reviewed head | `c6df4d738a1a3387ddb136194805c443296bc7a1` |
| Reviewed tree | `0baf0523c755866f1bfed23cbfd8e48eb33a8b4f` (unchanged after the run) |
| Range | `8e225d2b..c6df4d73`: the round 3 record and fixes, the afterburner button and the throttle direction |
| Author | Port (Claude Code, Claude Opus 5.5), family anthropic |
| Reviewer | `deepseek-v4.1-flash`, provider `opencode-go`, effort `high`, family deepseek |
| Route | `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on |
| Route source | `AGENTS.md`, "Cross-family review route" (operator direction 2026-11-24), explicit selection |
| Matrix | reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267` |
| Runtime | HyperCodex `codex-cli 0.169.0+hc.1`, review run in a standalone clone with no remote, no branches and no tags |
| Effective model | session banner `model: deepseek-v4.1-flash`, `provider: opencode-go`; wrapper route `opencode-go deepseek-v4.1-flash high` |
| Wrapper result | status `complete`, no forced kill, launcher `rc=0` |
| Started / finished | 2026-11-26T00:36:31Z / 2026-11-26T00:46:01Z |
| Clone refs | the review clone held no refs, only a detached head at the reviewed commit; no ref moved in the author clone |
| Summary | sha256 `f73db0478f28a100e5b76d16aa6abc5cb7355c9a03f8df37d55b708f16885418`, committed unchanged |
| Verdict | PASS-WITH-CONDITIONS: R3-1 partly fixed (N1, medium: the targeting keys' frame copy survived the undock disarm), R3-2 and R3-3 fixed; N2 low, a stale class comment |
