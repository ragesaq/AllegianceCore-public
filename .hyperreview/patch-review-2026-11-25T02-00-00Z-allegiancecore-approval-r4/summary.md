# Patch review r4 (delta): agents merge and change settings on operator approval

**Target:** `ragesaq/AllegianceCore-public` PR #5, base `port/guard-fail-closed`. Head
`326f945eb44e8cd4aa4178b2568313062b947127`, tree `0c76fc1e9ebe8789bd76f102db1b7436fd3bac30`, delta
`309921b6..326f945e` (three commits), in a standalone clone. `git write-tree` matched and `git status --short`
was clean, so the target did not move.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort `high` (matrix 4.14.0, sha256
`74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Author family Anthropic (Port);
cross-family review.

**Verdict: PASS-WITH-CONDITIONS.** Round 3's three findings are closed as `.hyperreview/README.md` says. Two low
record items remain; neither blocks the rule.

## Round 3 dispositions

1. **Low 1, where the checks ran: closed.** The body now says they ran in the author's clone, where `origin/master`
   exists, and explains that the node-loss gate cannot compare without it. Read on GitHub; the author's clone is
   unreachable here, so that claim is unverified.
2. **Low 2, the gate's antecedent and the committed summary: closed.** `AGENTS.md:51-53` now says "each build and
   test gate listed there passes", and the result "is committed and posted as a comment on the pull request".
3. **Low 3, the unevidenced read-only check: closed.** The round 2 receipt now marks the check author-attested and
   says the listing was not kept; the round 3 receipt records sha256 `9feead27...` for both listings (attested,
   not independently checkable).

## New findings

### 1. Low: the PR body still understates the check output

PR #5 body, "Validation": "The only output beyond OK is the expected `line-endings` warning". At this tree
`check_memory.py` also prints the warning `AGENTS.md:175-176` documents as expected (`recorded-by 'port' has no
folder in people/`, eight entries). The sibling PR states it correctly ("the two warnings `memory/mem-001`
documents"). Failure: the operator reads a cleaner validation than ran, and the known memory inconsistency stays
hidden. Fix: use the sibling wording.

### 2. Low: the committed round 3 summary miscounts its range

`summary.md:5` calls `e4e159ab..309921b6` "three commits". It holds four: the merge brought PR #4's `610ef8c2`,
`git rev-list --count` returns 4, and the summary's own Verification line records the publish guard on "4 commits".
The same conflation makes `summary.md:6` call `0b032b7..309921b6` "PR #5's own change", though it now includes
`610ef8c2`. Coverage is unaffected. Fix, since reviewer text stays unchanged: the README's round 3 entry can say
the range is three authored commits plus the merged one.

## Checked, no finding

- Rule 4 read cold (`AGENTS.md:51-63`): "all the review gates below are met, including these" reads as examples,
  not the whole list; the two items match the "Review gates" section; "committed and posted" closes the record
  gap. The new "or in a new Issue ... in a working session" adds a record location, not authority, and conflicts
  with neither the Coordination bullet (durable record) nor the identity contract (attribution still applies). It
  answers the sibling PR's review observation that a session approval had no Issue or PR to be recorded in
  (verified in that PR's round 1 comment).
- Records: the round 3 summary is byte-identical (sha256 `082f8646...`, 4843 bytes); the receipt matches the host
  record on route, binding, lock, model, effort, return code, finish reason, assurance and times, and discloses the
  light/full tier difference; the round 3 clone is still `309921b6`/`8cd1ba78`.
- GitHub: round 3 comment 7148471885 is posted with verdict, findings and dispositions; PR #5's head is
  `326f945e`; the body describes rule 4 at this head and links rounds 1-3.
- No machine path, secret or non-noreply address in the delta; no added line over 120 characters (the long
  identity-table row predates the delta); all six changed files are LF and BOM-free; both new README links
  resolve; `DAG.json` parses; `ragesaq-005` names this round.
- The receipt says "standalone clone"; `AGENTS.md:117` says "review from a separate worktree". A clone is at
  least as isolated (PR #4's own note records a review worktree sharing the author clone's refs), so no action;
  reconciling the wording is separate prose.

## Verification

Executed: `rev-parse`, `write-tree` (matched), `status`, `log`, `diff`, `rev-list --count`, `ls-files --eol`,
sha256/byte comparison, BOM/CRLF scan, JSON load; `publish-guard.sh 309921b6 HEAD` OK (3 commits);
`check_data_boundary.py` OK; `check_memory.py` OK (expected warnings); `check_map.py` reports the known node-loss
problem (no `origin/master`). Read: `AGENTS.md`, `DAG.json`, `.hyperreview/`, GitHub PR #5 and AllegianceVR #3.

Limits: no build or tests (no code changed); `recall.py` not run by instruction; the node-loss gate cannot compare
here; the author clone, the listing digests and the wrapper's state were unreachable, so those claims are attested
or limited, not verified.
