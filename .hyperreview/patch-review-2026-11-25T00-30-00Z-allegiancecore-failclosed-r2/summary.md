# Patch review: publish guard fail-closed, round 2

- **Target**: ragesaq/AllegianceCore-public PR #4, `port/guard-fail-closed`, stacked on PR #3.
- **Reviewed head** `0b032b7a564bccbe376e28739d2af6a1f07cb200`; **tree** `39dfa270bbcf4319e02feba70f7e325a1bbed2e0`.
- **Delta** `6528bdbe..0b032b7a`: 6 files, +116/-7. `a3e8c4ba` changes the workflow; `0b032b7a` adds the round 1 record.
- **Author** Port (Claude Code, Claude Opus 5.5), family Anthropic.
- **Reviewer** `deepseek-v4.1-flash`, OpenCode Go, effort `high`, explicit selection, family DeepSeek; reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`. Route source: `AGENTS.md`, "Cross-family review route".

**Verdict: PASS-WITH-CONDITIONS.** Round 1's injection finding is closed and its unrelated-history decline is sound; a creation push with a bad non-root commit now fails. The creation path still leaves the root commit unscanned, and the records say otherwise.

## Findings

### Low: `.github/workflows/guard.yml:22-23` and `scripts/publish-guard.sh:8`, the root commit of a created default branch is never scanned

The creation path sets `BASE` to the root commit, and the guard scans `BASE..HEAD`, which excludes `BASE`. A one-commit initial push sets `BASE` = `HEAD`: the guard prints "no new commits" and exits 0. A bad root followed by clean commits scans only the clean ones.

Executed in throwaway repositories: one bad root, first push to `master` -> 0, "no new commits"; bad root plus clean tip -> 0, "OK (1 commit(s))"; clean root plus bad tip -> 1 (control); clean root, bad middle, clean tip -> 1 (control); default name `release/1.0` -> 0.

This is the `root..HEAD` omission the author cites when declining the root fallback for Low 2, now used on the creation path. The guard needs a mode that scans every commit reachable from `HEAD` (empty-base or `--all`); a base equal to the root cannot express an inclusive range. With several roots, `tail -1` also leaves one out.

Reachability: this repository already has `master`, so no push here takes the path; it is a hole for a fresh repository or a recreated default branch.

### Low: the round 1 records overstate the fixes

`.hyperreview/README.md:14-15` says Low 1 is "fixed" and the push is "scanned from its root commit again"; the root is not scanned. `receipt.md:23` says "two fixed, one fixed differently"; Low 2 was declined and the clean orphan still fails red (README:16-18 says so correctly). `DAG.json:93` says "three low findings, now fixed". `memory/mem-007-guard-range.md:29-30` repeats "scanned from the root commit". Reword as: Low 1 scans the commits after the root, the root is still unscanned; Low 2 declined, fails closed.

## Round 1 dispositions

- **Low 1, creation push scanned nothing:** partially closed; a bad commit after the root is caught, the root is not.
- **Low 2, unrelated history fails red:** correctly declined. `root..HEAD` would omit the root, so the suggested fallback would let a one-commit orphan with a bad root pass. It now fails closed with a clear message; the clean-orphan false failure remains, accepted.
- **Low 3, `${{ }}` pasted into the shell:** closed. `REF` and `DEFAULT` arrive through `env:`, every use is quoted; the harness shows the injected command did not run with the new step and did run at `6528bdbe`.
- **Notes 1-6:** dispositions match the round 1 summary. The receipt's launch/outcome fields match `launcher.txt` and `run_state.json` (route, reviewer, tier `full`, complete, rc 0, stop, independent_cross_family, times); its head/tree/range match the frozen commit (`3bb456d1`). The committed summary is byte-identical (`f4634f9b...`). The breach row matches the disclosure; "every remote ref matched" and "reset from the reflog" are author-reported, not in the launcher/state.

## Verified by execution

- `git write-tree` = `39dfa270...` before and after; status shows only the artifact directory; `git diff --check` clean; index LF for all six paths.
- Author harness at this head: `passed=40 unexpected=0`. With `6528bdbe` as old, "push creates the default branch, bad commit" passes and "injected command RAN".
- `scripts/publish-guard.sh ff20f842..HEAD`: `OK (4 commit(s))`.
- `check_data_boundary.py`: OK. `check_memory.py`: OK with the documented warnings. `check_map.py`: its only problem is the node-loss gate's missing `origin/master` base, which this standalone clone lacks; `check_node_loss.py 6528bdbe` passes, so the DAG change drops no node. Its `line-endings` warning is the documented one.

## Limits

- Actions never ran; the step was simulated as `bash -e` on the extracted block, with `${{ }}` pasted as text and env values passed as GitHub does.
- No Linux host; Git Bash was used.
- The root probe covers first-push creation only.