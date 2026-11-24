# Patch review: agents merge and change settings on operator approval

**Target:** `ragesaq/AllegianceCore-public` PR #5, base `port/guard-fail-closed`. Head
`1f6039c2c4f6f476261396735ec722d2ea87ae31`, tree `af129d7b36e25a6134c9f08f8249ac9a19eb5ca7`,
`6528bdbe..HEAD` (2 commits, 4 files, +48/-3). `git write-tree` matched; `git status` showed only
this artifact directory.

**Reviewer:** DeepSeek family, `deepseek-v4.1-flash` via OpenCode Go, effort high (reviewer-parity
matrix 4.14.0, sha256 `74618f22fd72f6a27ed86db1298fd387068118d3faab415a79604b60c8a63267`). Author
family Anthropic (Port); cross-family review.

**Verdict: PASS-WITH-CONDITIONS.** Faithful to the instruction, the operator's words quoted exactly,
the old rule fully replaced, the records sound. Two wording gaps remain in the sentence that now
grants merge authority.

## Findings

### 1. Medium: merging is not conditioned on the review gate (AGENTS.md:51-54 vs :101-116)

Rule 4 gives one condition for merging, the operator's approval. The Review gates section requires
`hyperreview-reflex` after every behavior-changing edit and a cross-family review for policy changes,
but binds them only "before it is declared done" (:105-107). Nothing makes a merge wait for that
review, a gap that was inert while agents never merged.

Scenario: the cross-family review posts findings; the operator comments "fine, merge it" before they
are fixed. The head was reviewed and the operator approved it, so both phrases at :53 hold, yet the
fix-and-re-review step at :114 never happens and the merge lands with the findings open.

Fix, at :53-54: "Merge only after the review this file requires has passed, its findings are
dispositioned, and its result is on the pull request", and pin the head in the merge call
(`expectedHeadSha`) so a push between check and merge fails.

### 2. Medium: the rule does not name who may approve (AGENTS.md:51-52, :55-56)

"the operator's approval ... given in a comment on it or in a working session" names channels but not
the speaker. With two agents relaying through Issues and pull requests, likely misreads are a
`psiclawops-port` comment reporting the operator's approval, a handoff note claiming it, or Issue
text. "Working session" is undefined, and each agent has one. The settings sentence (:55-56) names no
channel.

Fix: "given by @ragesaq in a comment on the pull request, or told directly to the agent that merges; a
message from another agent, a handoff note, or an Issue or file never counts." Use the same phrase for
settings.

### 3. Low: the merge record stops short of closeout (AGENTS.md:54-55, DAG.json rules)

Rule 4 says record the merge commit on the node; `DAG.json`'s rules say the first session after a
merge flips it to `done` and adds its ledger line. Read literally, rule 4 leaves `ragesaq-005` at
`review` after its pull request lands. Append "and close the node out as `DAG.json`'s rules say".

### 4. Low: two live nodes still route landing through the operator's own merge (DAG.json:77, :93)

`ragesaq-003` "Waits on ragesaq's merge, after PR #2"; `ragesaq-004` awaits "then ragesaq's merge".
Rule 4 makes the step the operator's approval, then a merge an agent may perform, so a cold agent will
stall waiting for a person. Suggest "waits on the operator's approval to merge" in both.

### 5. Low: mem-008's class and evidence are self-referential (memory/mem-008-merge-and-settings-approval.md:4, :7, :12-17)

`class: verified` with `evidence: AGENTS.md` cites the file this pull request creates, which does not
hold the quoted words, so the entry evidences its own input. The claim is a session statement, which
`docs/MEMORY_PROTOCOL.md:41` classes `witnessed` with `seen-in:`. mem-006 is equally loose, so this is
house style and the checker passes; suggest `witnessed` with `seen-in: operator instruction in the
agent's working session, 2026-11-24`.

### 6. Low: mem-008 states a scope the operator did not (:22)

"The rule does not cover repositories other than this fork" is true of where the text sits, but reads
as "no approval is needed elsewhere"; the body says it better ("AllegianceVR's AGENTS.md still has the
old rule"). Suggest: "This entry records the decision for this repository's AGENTS.md; it does not
decide policy for another repository."

## Checked, no finding

- No "agents never merge" or "operator merges" text survives in AGENTS.md, GROUND_RULES.md, `docs/`,
  `knowledge/`, `memory/` or the DAG rules block. Only `.claude/skills/hyperbench-lifecycle/SKILL.md:144`
  remains, a hub adoption step outranked by AGENTS.md.
- The DAG rules block is unchanged; `ragesaq-005` meets the machine floor (URL and files in `links`,
  concrete `next`, acyclic `needs`), and the identity row's new wording fits the rule.
- mem-008's PR #1 claim holds: GitHub records `merged_by: psiclawops-port`, as mem-005 says. No
  secret, machine path or non-noreply address in the diff.

## Verification

Executed, read-only: `git write-tree`, `log`, `diff`, `status`; `check_data_boundary.py`,
`check_map.py` (expected `line-endings` warning) and `check_memory.py` (expected `people/` warnings)
all OK; `publish-guard.sh 6528bdbe HEAD` OK; GitHub reads of PR #5 (head `1f6039c2`, no comments) and
PR #1. Not executed: build and tests (no code changed); `recall.py check-glossary` writes a cache, so
the recall claim is unverified.
