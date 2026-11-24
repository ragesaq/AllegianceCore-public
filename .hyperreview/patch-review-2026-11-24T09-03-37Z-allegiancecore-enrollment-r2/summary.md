# Patch review — round 2 delta: AllegianceCore HyperBench enrollment

**Target:** `ragesaq/AllegianceCore-public` PR #1, branch `port/hyperbench-enrollment`, commits `f4d61cb..3ca08ba`.
Reviewed HEAD `3ca08bacc03938d3e3eb1bf7265095992efc2519`, tree `fcfe4df013cfdc5cc06b1bfb7e4ce26c8eae3097`
(`git write-tree` matched before and after; `git status --short` clean).
**Reviewer:** DeepSeek family — `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high`, explicit selection
(reviewer-parity matrix 4.14.0, sha256 `74618f22...b63267`; standing at `high`). Authoring family Anthropic/Claude
(Port). Cross-family.
**Change:** 7 files, +148/−3; answers round 1. Delta tier.

## Verdict

**PASS.** Both round 1 findings are closed and the new text is accurate. No new defect found.

## Round 1 dispositions

**F1 — closed.** `STARTER_MANIFEST.json:39` now lists `memory/mem-001-hyperbench-scope.md`. Blob comparison against
the copy source (`AllegianceVR` at `90ba914`) shows every copied file the manifest leaves unlisted is byte-identical:
the lifecycle skill, `knowledge/TEMPLATE.md`, `memory/CANARY.md`, `tools/check_memory.py`, `check_tracking.py`,
`hb_modules.py`, `recall.py` and `recall_vectors.py`. `mem-001` is the only copied file that differs and is now
named. The three listed files that are identical to the copy source are explained by the manifest's own `about`:
they differ from the hub starter or hub root (`memory/CANARY.md` from `templates/repo-starter`, `tools/check_map.py`
and `check_node_loss.py` by `origin/main` → `origin/master`). Nothing unadapted is listed.

**F2 — closed; the author's dispute is correct.** The rewritten sentence
(`knowledge/vr-client-survey.md:20-24`) names three sources, and each is exact at HEAD:
`Allegiance.ClientCore.MonoGame.csproj:25` is the `MonoGame.Framework.DesktopGL` 3.8.5.1 reference;
`.github/workflows/release.yml:38` is `rid: [linux-x64, win-x64, osx-arm64]`; `README.md:160` is
"Download the Windows x64 setup `.exe`, Linux x64 AppImage, or a portable bundle". Round 1's premise that the README
says nothing about 64-bit was inaccurate: line 160 names x64 twice. The new citation is stronger than the old
one regardless. The copied round 1 summary retains that inaccurate sentence, but `.hyperreview/README.md:16-18`
records the dispute, so the committed record is coherent.

**F3 — unchanged, correctly left for the operator.** Not part of this delta.

## New findings

None. Two notes for the record:

- **NOTE — `AGENTS.md:160`.** "The script is checked out with CRLF there" holds on this host
  (`core.autocrlf=true`, `scripts/publish-guard.sh` is `w/crlf`; `.gitattributes` has no `scripts/*.sh` rule). On a
  Windows clone with `core.autocrlf=false` the checkout is LF, so the clause is conditional. The instruction is
  still safe either way: Git Bash parses both forms, and WSL `bash` fails only on the CRLF form (measured below).
  Could be tightened to "a typical Windows checkout is CRLF"; not needed.
- **NOTE — receipt.** The "Earlier attempt" row cannot be reconstructed from `launcher.txt` or `run_state.json`:
  both record only the successful 08:42:34Z launch. It rests on host-side wrapper logs. No contradiction observed.

## Verified by execution

- `git write-tree` printed `fcfe4df...` before and after; `git status --short` was clean; the delta is the seven
  named files.
- `check_data_boundary.py`, `check_map.py` and `check_memory.py` each exit 0 with only the two documented warnings
  (`line-endings` floor module; `recorded-by 'port'` has no `people/` folder).
- The committed round 1 `summary.md` matches the original r1 artifact line for line, ignoring CR: 98 lines each,
  exactly one differing line, the host-policy email address replaced by "non-noreply address redacted". The receipt
  matches the r1 `launcher.txt`/`run_state.json` on route id, binding `exact`, route lock, provider/model/effort,
  runtime version, start/finish times, `rc=0`, `status complete`, `finishReason stop` and assurance
  `independent_cross_family`; the r1 worktree still writes tree `e7a4e8f` at head `f4d61cb`.
- Every changed file is `i/lf` + `w/lf` and `attr/text eol=lf` (`.hyperreview/**` is covered); no BOM.
- WSL `bash -n scripts/publish-guard.sh` exits 2 (`syntax error: unexpected end of file`) on the CRLF worktree copy;
  Git Bash `bash -n` exits 0. The `AGENTS.md` line is empirically right on this host.
- No email address in the changed files except the pre-existing GitHub noreply identity in `AGENTS.md`; no
  drive-letter or POSIX home-directory path in the delta. The two relative links in `.hyperreview/README.md` resolve to committed
  files.

## Verified by reading

- Manifest `localModifications` against copy-source and template/root blob hashes.
- The three line citations above, at HEAD.
- `DAG.json` node `ragesaq-001`'s updated `next` matches the review record and the pending round 2.

## Limits

- No `dotnet` build or tests (no C# changed); no publish guard run in this tier (the delta is Markdown and JSON,
  and round 1 ran the guard on the underlying enrollment).
- `tools/recall.py` was not run (it writes a cache); the read-only checks above stand in.
- The r1 "Earlier attempt" row is not independently verifiable from the two permitted state files.