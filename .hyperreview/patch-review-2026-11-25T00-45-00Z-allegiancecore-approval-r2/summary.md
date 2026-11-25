# Patch review r2 (delta): agents merge and change settings on operator approval

**Target:** `ragesaq/AllegianceCore-public` PR #5, base `port/guard-fail-closed`. Head
`e4e159ab0126f59f78d430d62ebddcf02e31775b`, tree `1241018cd9818c8f24e407c919c86ac8b367b047`, delta
`0b032b7a..e4e159ab` (7 files, +190/-4). Round 1 reviewed `1f6039c2`, tree `af129d7b`. `git write-tree` matched;
`git status` clean apart from the artifact directory.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort high (reviewer-parity matrix 4.14.0,
sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Author family Anthropic (Port);
cross-family review.

**Verdict: PASS-WITH-CONDITIONS.** Round 1's two Medium findings are closed. Conditions: New 1, New 2 and the low
record fixes.

## Round 1 dispositions

1. **Medium 1, merge before the gates: closed.** `AGENTS.md:51-53` conditions the merge on the gates, the review
   recorded and findings fixed or answered (residual: New 1, New 5).
2. **Medium 2, who may approve: closed.** `:53-55` names ragesaq, a PR comment or the merging agent's own session,
   excludes relays, handoffs, Issues and files, and pins the head (`:55-57`); settings use the same channel (`:59-60`).
3. **Low 3, closeout: closed.** `AGENTS.md:57-58` points at `DAG.json`'s closeout rule.
4. **Low 4, live nodes: closed.** `DAG.json:77` now says "the operator's approval to merge"; `:93` already did.
5. **Low 5, mem-008 class: closed.** `witnessed` with `seen-in:` (`memory/mem-008-...md:4,7`); the index row matches
   (`memory/README.md:35`). Residuals: New 3, New 4.
6. **Low 6, mem-008 scope: closed.** `memory/mem-008-...md:26-27`; the per-change confirmation is at `:19-20`.

## New findings

### 1. Medium: the review is committed, not recorded on the pull request

`AGENTS.md:52` requires the review "recorded on the pull request"; `AGENTS.md:116-120` requires a PR comment.
`e4e159ab` commits round 1 under `.hyperreview/`, but PR #5 has zero comments and zero reviews (GitHub read).

Failure: the operator approves; a merging agent finds no on-PR record and cannot tell whether "recorded" means a
comment or a commit. It merges on the commit, while the gates section requires a comment. Fix: post both review
rounds as PR comments (with the gates' fields) before asking for approval; or write "committed under
`.hyperreview/` in the pull request" and keep the gates bullet explicit.

### 2. Low: the PR body describes the round-1 text

The body still says approval is "given in a comment on it or in a working session", omits the review-gate
precondition and the direct-to-merging-agent narrowing, stops the record at the work-map node, and says the review
"will be posted here"; it does not reflect `c3a9eb17`. Failure: the operator approves the body's rule, not the diff's.
Fix: update Change and Review, and link the posted review.

### 3. Low: `mem-004` is misattributed to PR #2

`.hyperreview/README.md:22-23` says "`mem-004` and `mem-006` ... belong to PR #2". `mem-004` was added by `1122bced`,
an ancestor of PR #1's merge `daaeaf9f`; only `mem-006` (`6aa89dea`) is in PR #2. Failure: a later agent treats a
merged entry as pending in PR #2. Fix: name `mem-006` only; `mem-004` arrived with PR #1.

### 4. Low: `mem-008`'s `seen-in` omits versions

`docs/MEMORY_PROTOCOL.md:41,273` requires the action, environment and versions; `:55` requires the machine by role.
`memory/mem-008-...md:7` gives the action and "the operator's Windows host" (role, no hostname or path) but no
versions. Failure: a reader cannot tell which runtime produced the words, so the record is weaker than the protocol
asks. Fix: name the runtime, e.g. Port (Claude Code, Claude Opus 5.5).

### 5. Low: the gloss drops the build gate

`AGENTS.md:52-53` defines "gates met" as the review recorded plus findings dispositioned; the section's first bullet
(`AGENTS.md:107-108`) is the build and fast-test gate for code. Failure: a cold agent reads the gloss as the complete
merge checklist and merges code whose build has not run. (Prose-only changes have no required review, which is
consistent with the gates section.) Fix: "only after the gates below are met (for code, the
build and fast tests; where a review is required, its result posted on the pull request with findings dispositioned)".

## Checked, no finding

- `592e0eb7` resolved cleanly: `git diff 0b032b7a 592e0eb7` shows only PR #5's four files; no `.github/`, `scripts/`
  or `tools/` change.
- No machine path, secret or non-noreply address in the delta; both operator quotes are exact; all changed files are
  LF in the index; the two new README links resolve.
- `ragesaq-005` meets the work-map floor; the DAG rules block is unchanged. Round 1's summary is byte-identical
  (sha256 `0DAFEFD2...`); its receipt matches `launcher.txt` and `run_state.json` on route, model, effort, lock,
  result and times, and discloses the light/full tier difference.

## Verification

Executed: `git rev-parse`, `write-tree`, `status`, `log`, `show`, `diff`, `ls-files --eol`; hashes;
`publish-guard.sh 1f6039c2 HEAD` OK (5 commits); `check_data_boundary.py` OK; `check_memory.py` OK (expected
warnings); GitHub PR #5 (zero comments, zero reviews). Read: `AGENTS.md`, `memory/`, `DAG.json`, `.hyperreview/`,
`docs/MEMORY_PROTOCOL.md`, round 1 launcher state.

Limits: `check_map.py` reports one problem, the node-loss gate cannot compare (no `origin` base in this standalone
clone), a clone artifact, not the diff. No build or tests (no code changed). The receipt's "branches and tags
unchanged" is not evidenced in the retained state.