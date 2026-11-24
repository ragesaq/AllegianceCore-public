# Patch review: the publish guard accepts GitHub's merge committer

- **Target**: ragesaq/AllegianceCore-public PR #3, `port/guard-github-merges`, stacked on `port/enrollment-done` (PR #2).
- **Reviewed head** `2ca409f673d448ed7af5535b975b0fd8ac9a5148`; **tree** `ccc2380c889d9894c69df2049903dc62a1c71112` (`git write-tree` matched before and after).
- **Delta** `b15f666b..2ca409f6`: 4 files, +32/-8; a one-line local difference in `scripts/publish-guard.sh`.
- **Author** Port (Claude Code, Claude Opus 5.5), family Anthropic.
- **Reviewer** `deepseek-v4.1-flash`, OpenCode Go, effort `high`, explicit selection, family DeepSeek; reviewer-parity matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`. Route source: `AGENTS.md`, "Cross-family review route".

**Verdict: PASS.** The change is correct under `set -eu`, behaves the same with CRLF and LF copies, and does not weaken the guard's stated purpose. The records match it. One low record finding, four notes.

## Findings

### Low (non-blocking): `memory/mem-005-github-merge-identity.md:10`

The heading still asserts in the present tense that "A merge made through GitHub fails this fork's publish guard". The body this PR adds says that from `ragesaq-003` on, a merge with email privacy on passes, and the index row was changed to the past tense ("rejected"). After this PR merges, the heading is false on its own terms.

Scenario: a later session recalls the entry by its heading, concludes GitHub merges still fail, and proposes a workaround the guard already contains.

Fix: retitle in observation form, for example "GitHub merges stamped addresses the publish guard rejected", or append "(fixed by ragesaq-003)".

## Notes (no action required)

1. **The exception is wider than merges.** A single-parent commit with committer `noreply@github.com` passes (verified). This is safe for a guard about keeping personal addresses out of public history: the address is GitHub's service identity, not a person. It is also simpler than a `--merges`-only form. The load-bearing detail is filtering the committer stream *before* `sort -u`; after the union it would also hide an author equal to that address. I confirmed that an author and committer both equal to it still fails.
2. **Both filter flags matter.** Without `-x`, `evil-noreply@github.com` would pass; without `-F`, `noreplyXgithub.com` would pass. Both are rejected.
3. **Pre-existing fail-open, `scripts/publish-guard.sh:12`.** An unresolvable base (verified with an unknown SHA) makes `git rev-list` fail, the guard print "no new commits", and exit 0. This predates the delta; `guard.yml` normalises only the all-zero SHA. It deserves a separate change, not this one.
4. **DAG `ragesaq-003` (`DAG.json:69-84`).** Status, `next`, `needs` and `updated` are present, and the enforced link floor passes. Rule 2's prose also asks to pin heads and hashes; `next` (line 77) names PR #3 and PR #2 but no SHA. Optional: add "head 2ca409f6, base b15f666b".

## Verified by execution

- `git write-tree`, `git status`, `git diff --check`: tree unchanged, only this review's artifact directory untracked, one-line diff, no renormalisation.
- `bash scripts/publish-guard.sh b15f666b HEAD` on the CRLF worktree copy and the LF blob (`git show | bash -s`): both `OK (2 commit(s))`; `bash -n` passes on both.
- Harness: 27 of 27 as claimed, against a copy whose LF sha256 matches the reviewed blob.
- Own probe, 14 scenarios over LF and CRLF: GitHub committer passes; the same address as author fails; `NoReply@GitHub.com` fails; the harness's prefix, suffix and fixed-string lookalikes fail; a clean two-parent merge passes and one whose history holds a non-noreply author fails; an empty range exits 0 "no new commits". A trailing space or tab in a committer address is stripped by git before storage, so that lookalike cannot reach the guard.
- `check_data_boundary.py`, `check_map.py`, `check_memory.py`: OK with the documented warnings. `git ls-files --eol`: LF in the index for all four files; the script is CRLF only in the working tree (`core.autocrlf=true`, no eol attribute), as `AGENTS.md` says.
- PR #3 through the GitHub API: head `2ca409f6`, base `b15f666b`, 4 files, +32/-8, matching the frozen target.

## Verified by reading only

- `mem-005` against `docs/MEMORY_PROTOCOL.md`: `verified` class with resolvable `evidence`; the unobserved account setting is honestly scoped ("the operator said", "Port has not seen that yet"); the index row matches.

## Limits

- No Linux host: the LF copy ran under Git Bash, not GNU bash on CI. The constructs used are standard in both.
- GitHub Actions do not run in this fork, so the workflow path is unexecuted.
- The accounts' email-privacy setting is unverified; the first merge after it is the observation, as `mem-005` says.