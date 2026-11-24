# Patch review — round 3 delta: AllegianceCore HyperBench enrollment

**Target:** `ragesaq/AllegianceCore-public` PR #1, branch `port/hyperbench-enrollment`, commits `3ca08ba..1122bce`.
Reviewed HEAD `1122bceda16bbd9657fee16106689dfe32c3d5ec`, tree `76d3e5fcc242e2dea3461c88520bf1d5bac496ee`
(`git write-tree` matched; status shows only the untracked r3 artifact directory).
**Reviewer:** DeepSeek family — `deepseek-v4.1-flash` through OpenCode Go, effort `high`, explicit selection
(reviewer-parity matrix 4.14.0, sha256 `74618f22...b63267`). Authoring family Anthropic/Claude (Port). Cross-family.
Route `codex:5aa43f19840ce08ad126b5b3`, binding `exact`, route lock on. Light delta requested; the wrapper records
tier `full`.
**Change:** 7 files, +139/−3: round 2's record (`fa2b35e`) and the operator's Socket-identity decision (`1122bce`).

## Verdict

**PASS.** Round 1 note 3 is closed and no new defect was found.

## Round 1 note 3 — closed

`AGENTS.md:138` gives Socket's git author in GitHub's documented noreply form,
`<id>+<login>@users.noreply.github.com`, with the login matching the table's GitHub identity. The publish guard's
identity rule (`scripts/publish-guard.sh:13`) rejects only addresses that do not end in `users.noreply.github.com`,
so the address passes where the old row ("its own configured identity") could fail. It uses the same form as Port's
row, is allowed by `GROUND_RULES.md` rule 3 ("other than an agent's noreply commit address"), and sits in the
fork's own section below the upstream author's text, which says nothing about identity. `memory/mem-004` is a
`verified` entry with a resolvable `evidence:` file and an index row at `memory/README.md:31`; it gives the decision
date and the operator's quoted answer, and cites the guard at the exact line (13 is the `BAD_ID` check).

The decision's own provenance is thinner: cited to "the working session", as `mem-001`/`mem-002` do, with no separate durable
record. House style here; noted, not filed.

## Round 2 record

- The committed `summary.md` is byte-identical to the r2 artifact. `git rev-parse HEAD:<path>`, `git hash-object`
  of the working file and of the original all give `1242a066eec91da846768fb37f46b4b4c1d0ff1d`.
- The receipt's launch rows match the r2 `launcher.txt` and `run_state.json`: route id, binding, route lock,
  provider, model, effort, runtime version, start/finish times, `rc=0`, `status complete`, `finishReason stop`
  and assurance `independent_cross_family`. Its head and tree rows match `3ca08ba` and the r2 worktree
  (`fcfe4df`). The "wrapper records tier `full`" parenthetical is accurate (`reviewTier: full`,
  `requestedReviewTier: full`) and records the mismatch with the light-delta prompt.
- The README entry's claims hold: head `3ca08ba`, tree `fcfe4df`, range `f4d61cb..3ca08ba`, PASS, two notes. The
  redacted r1 summary is 98 lines against 98, with exactly one differing line (line 40: the quoted host address
  replaced). The r2 worktree still writes tree `fcfe4df` at `3ca08ba`, and the r1 worktree writes `e7a4e8f` at
  `f4d61cb`; the r2 summary states the latter.
- The README's earlier-attempt row is supported by the r1 first-attempt folder
  (`patch-review-2026-11-24T08-40-48Z-...-r1`), whose `launcher.txt` records `rc=1` at 08:41:24Z.

## New findings

None. Two notes only:

- `memory/mem-004-agent-noreply-identity.md:17` — the decision is cited to "the working session"; no PR, issue or
  knowledge note carries it. Matches `mem-001`/`mem-002`; no action.
- `AGENTS.md:138` — the account id could not be checked offline. No commit in this repository is authored by
  Socket, so history gives no corroboration; the author states it came from GitHub's user search.

## Verified by execution

- `git write-tree` = `76d3e5f...`; delta is exactly the seven files in `git diff --name-status 3ca08ba HEAD`.
- `check_data_boundary.py`, `check_map.py`, `check_memory.py` exit 0, with only the documented warnings (the
  `line-endings` floor module and `recorded-by 'port'` with no `people/` folder).
- Guard rule against the new address: the reject filter prints nothing and exits 1; a control non-noreply address
  is printed and exits 0.
- All changed files are `i/lf`/`w/lf`, `attr/text eol=lf`, no BOM, newline-terminated. No machine path, secret or
  non-agent email in the delta; the only addresses added are the agent noreply addresses in `AGENTS.md` and
  `mem-004`.
- Round 2 summary blob equality (above); r1 summary comparison ignoring CR; r1/r2 worktree head and tree checks.

## Verified by reading

- The identity table and rule text against the guard's regex, GitHub's noreply form, `GROUND_RULES.md` rule 3 and
  the upstream section.
- `mem-004` against `docs/MEMORY_PROTOCOL.md` rules 1–2 and the `memory/README.md` row.
- `DAG.json` node `ragesaq-001` `next` against both review records; it names the round 3 review and the merge.

## Limits

- No network check of the Socket account id; no publish-guard run (it needs a commit range, and no commit carries
  Socket's address yet).
- No build or tests (no code changed). `tools/recall.py` was not run (it writes a cache).
