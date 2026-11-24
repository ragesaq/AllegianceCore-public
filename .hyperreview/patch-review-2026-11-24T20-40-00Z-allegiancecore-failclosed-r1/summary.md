# Patch review: the publish guard fails closed when it cannot list its range

- **Target**: ragesaq/AllegianceCore-public PR #4, `port/guard-fail-closed`, stacked on `port/guard-github-merges` (PR #3).
- **Reviewed head** `6528bdbe491d7aacb0c3ad4478206a96baf0ff3c`; **tree** `3bb456d1ba5053c0e28cfdf021adf37105d3a009`.
- **Delta** `ff20f842..6528bdbe`: 5 files, +55/-2; two lines added in `scripts/publish-guard.sh`, eight in `.github/workflows/guard.yml`, the rest records.
- **Author** Port (Claude Code, Claude Opus 5.5), family Anthropic.
- **Reviewer** `deepseek-v4.1-flash`, OpenCode Go, effort `high`, explicit selection, family DeepSeek; reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`. Route source: `AGENTS.md`, "Cross-family review route".

**Verdict: PASS.** The stated fail-open is closed for every push this repository can receive, the identity and content scans are untouched, and the records match. Three low findings, none reachable here, and notes.

## Findings

### Low: `.github/workflows/guard.yml:20-21`, a push that creates the default branch scans nothing

When `before` is all zeros and the pushed ref is `refs/heads/<default>`, checkout runs after the push, so `origin/<default>` already equals `HEAD`. `git merge-base` returns `HEAD`, the range is empty, and the guard exits 0 with "no new commits"; the old code scanned from the root commit.

Executed: a fresh bare remote with two commits, the second stamped `someone@example.com`, pushed to `master` for the first time. Old 1, new 0.

This repository cannot reach it: GitHub does not let the default branch be deleted, and a push to an existing one never carries `before` = 0. It matters if the workflow is reused on a fresh repository or the default branch is recreated.

Fix: inside the zero-`before` branch, when `github.ref` is the default branch, use `git rev-list --max-parents=0 "$HEAD" | tail -1`; other new branches keep the merge base.

### Low: `.github/workflows/guard.yml:21,25`, an unrelated-history branch fails red though clean

`git merge-base` exits 1 when the branch shares no commit with the default branch, and under `bash -e` the assignment fails the job, although every pushed commit is new and scannable. Executed: old 0, new 1 for a clean orphan branch. An orphan branch (vendor import, pages-style) therefore cannot be pushed; failing closed is the safe direction, but this is a new false failure. Fall back to the orphan's own root with `git rev-list --max-parents=0 "$HEAD" | tail -1`, which is exactly the set new to this repository.

### Low: `.github/workflows/guard.yml:19,24`, `${{ }}` values are pasted into the shell

`github.ref` (the pushed branch name) and `github.event.repository.default_branch` are interpolated into the script; a branch named `x$(cmd)` yields `[ "refs/heads/x$(cmd)" != ... ]` and the substitution runs inside the quotes. Executed: the extracted run block created the marker file. It grants nothing a push-capable user lacks, and pull-request refs (`refs/pull/N/merge`) are not attacker-named, but pass both values through `env:` as `REF` and `DEFAULT` and quote them.

## Notes

1. `git cat-file -e "$BASE^{commit}"` is right for a full 40-hex payload SHA: `-e` tests presence, `^{commit}` rejects a non-commit.
2. A renamed default branch works; `origin/$DEFAULT` follows the payload and the ref test is string equality, including a name with a slash (executed for a new branch).
3. PR runs are unchanged when `base.sha` exists. If it were missing, the new `elif` re-bases on the default branch and over-scans for a PR aimed at a non-default branch. Narrow.
4. The empty-range "no new commits" pass is preserved (`scripts/publish-guard.sh:13`); identity and content scans are untouched, and the guard on its own change returns `OK (2 commit(s))`.
5. `.githooks/pre-push` still hardcodes `origin/master` with a root fallback; unchanged, outside the delta.
6. Records: `mem-007` is a `verified` entry with resolvable evidence, indexed, and matches the diff; `DAG.json` `ragesaq-004` meets the rules block including the link floor. Optional: `next` names PR #4 without a SHA.

## Verified by execution

- `git write-tree` = `3bb456d1...` at start and after each probe; status shows only the artifact directory; `git diff --check` clean.
- `git ls-files --eol`: index LF for all five files; the two scripts CRLF only in the working tree, as before. Committed blobs have zero CR bytes.
- Guard on `ff20f842..HEAD`, CRLF copy and LF blob: both `OK (2 commit(s))`; `bash -n` passes.
- The author's harness reproduced 28/28.
- Own scratch probes: clean orphan (old 0, new 1), first push creating the default branch with a bad commit (old 1, new 0), new branch off `release/1.0` (0), `github.ref` injection (ran).
- `check_data_boundary.py`, `check_map.py`, `check_memory.py`: OK with the documented warnings.

## Limits

- Actions never ran; the workflow was simulated as `bash -e` on extracted blocks, and checkout's `fetch-depth: 0` all-branch fetch is documented behavior, not observed.
- No Linux host: the LF copy ran under Git Bash.
- Two drafts of my probe harness ran repository commands in the review worktree by mistake; each was stopped and the worktree restored to the frozen commit. The final `git write-tree` and a status naming only the artifact directory put the reviewed state at exactly `6528bdbe` / `3bb456d1`; the probes above ran in their own scratch repositories.