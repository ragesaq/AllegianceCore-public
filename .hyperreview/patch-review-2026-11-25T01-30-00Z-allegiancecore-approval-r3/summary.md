# Patch review r3 (delta): agents merge and change settings on operator approval

**Target:** `ragesaq/AllegianceCore-public` PR #5, base `port/guard-fail-closed`. Head
`309921b6e9eba985a06f98dc021b58326e70d11b`, tree `8cd1ba78efff155f16f9fdc41eab73aeb6ee2156`, delta
`e4e159ab..309921b6` (three commits), in a standalone clone. PR #5's own change to this head is
`0b032b7..309921b6`.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort high (matrix 4.14.0,
sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Author family Anthropic (Port);
cross-family review.

**Verdict: PASS-WITH-CONDITIONS.** Round 2's five findings are closed or closed in substance; three low record and
wording items remain. None blocks the rule's substance.

## Round 2 dispositions

1. **New 1, the review must be on the pull request: closed.** `AGENTS.md:52-53` says the result "is posted as a
   comment on the pull request"; PR #5 now carries both round comments (round 1 at 00:02:29Z, round 2 at 00:14:04Z).
2. **New 2, the PR body described round 1: closed.** The body now states rule 4 as it stands and links both rounds.
3. **New 3, `mem-004` attribution: closed.** `.hyperreview/README.md:52-53`; `1122bced` is an ancestor of PR #1's
   merge `daaeaf9f`.
4. **New 4, `mem-008` `seen-in`: closed** (`memory/mem-008-merge-and-settings-approval.md:7`).
5. **New 5, the gloss dropped the build gate: closed in substance, wording still loose** (finding 2).

## New findings

### 1. Low: the PR body says `check_map.py` passes; here it reports one problem

The body's "Validation" says all four checks pass "with only the expected warnings". From the clone root
`check_map.py` exits with one problem: the node-loss gate cannot compare because no `origin/master` base is readable
in a standalone clone. `AGENTS.md` names only `line-endings` as expected, and round 2 recorded this problem as a
limit. Failure: the operator reads "pass" as evidence the work map is sound when the one blocking gate did not run.
Fix: name where it ran and what it reported.

### 2. Low: "any build and test gate they name passes" has no clear antecedent

`AGENTS.md:52`. The nearest plural noun is "the review gates below", which cannot name gates; the nearest party is
the operator. A cold agent can read the clause as "gates the operator names", while the section's first bullet
(`AGENTS.md:108-109`) makes the build and fast tests apply to code whether the approval names them or not. The gloss
also omits the section's commit-the-summary requirement (`:117-121`). Failure: code merges with no build because the
approval named none. Fix: "any build and test gate the gates below name passes".

### 3. Low: the receipt asserts a read-only check with no evidence

`.hyperreview/patch-review-2026-11-25T00-45-00Z-allegiancecore-approval-r2/receipt.md:24` says the clone's branches
and tags were unchanged, "checked by the author with `git for-each-ref`". Round 2's summary says that claim "is not
evidenced in the retained state", and nothing attaches the output. Fix: mark it author-attested, or record the
output digest.

## Checked, no finding

- `a3382822` resolved cleanly: `git diff 610ef8c2 309921b6` touches only PR #5's paths; no line of `610ef8c2`'s
  README is lost, and the only `bad84365` lines absent are PR #4's round 1 words that `610ef8c2` itself corrected.
  Headings are newest first: PR #5 r2, PR #4 r2, PR #5 r1, PR #4 r1.
- Rule 4 read cold: approval counts only from ragesaq, on the PR or directly to the merging agent in its own
  session; relays, handoffs, Issues and files never count; approval covers one pull request; the head must be the
  reviewed and approved one. No reading merges before the review comment exists.
- The committed round 2 summary is byte-identical to the reviewer's original (sha256 `21d9556c...`).
- The receipt matches `launcher.txt` and `run_state.json` on route, binding, lock, model, effort, return code,
  finish reason, assurance and times, and discloses the light/full tier difference.
- No machine path, secret or non-noreply address in the delta; all changed files are LF in the index; both new
  README links resolve; `ragesaq-005` names this round as its next step.

## Verification

Executed: `rev-parse`, `write-tree` (matched), `status`, `log`, `show`, `diff`, `ls-files --eol`, `sha256sum`;
`publish-guard.sh e4e159ab HEAD` OK (4 commits); `check_data_boundary.py` OK; `check_memory.py` OK;
`check_map.py` as in finding 1; GitHub PR #5 comments and body (read). Read: `AGENTS.md`, `memory/`, `DAG.json`,
`.hyperreview/`, round 2 `launcher.txt` and `run_state.json`.

Limits: no build or tests (no code changed); `recall.py` not run by instruction; the node-loss gate cannot compare
in this clone; GitHub shows comments and body only, not review approvals or settings.
