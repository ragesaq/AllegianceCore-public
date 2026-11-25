# Review records

One folder per cross-family review, newest first. Each folder holds the reviewer's `summary.md` and a path-free
`receipt.md`. The reviewer's activity logs and wrapper state stay on the reviewing host (`GROUND_RULES.md` rule 3), and
`.gitignore` keeps them out. The route is set in `AGENTS.md`, "Cross-family review route".

## 2026-11-24: PR #3 publish guard accepts GitHub's merge committer, round 1

- Record: [summary](patch-review-2026-11-24T19-40-00Z-allegiancecore-guard-r1/summary.md), [receipt](patch-review-2026-11-24T19-40-00Z-allegiancecore-guard-r1/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `2ca409f`, tree `ccc2380`, the delta `b15f666..2ca409f`: the one-line identity rule in
  `scripts/publish-guard.sh`, `memory/mem-005` and work map node `ragesaq-003`. Reviewer `deepseek-v4.1-flash` via
  OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. The reviewer ran the author's harness (27 of 27) and 14 cases of its own over CRLF and LF copies.
  Dispositions:
  - Low finding, `mem-005` heading in the present tense contradicted its body: fixed in the next commit (prose only,
    so no re-review).
  - Note 1, the exception also passes a single-parent commit with GitHub as committer (a web edit): accepted, the
    address is GitHub's service identity.
  - Note 2, both `-x` and `-F` are needed: no action, both are present.
  - Note 3, pre-existing: an unresolvable base makes the guard print "no new commits" and pass (`publish-guard.sh:12`).
    Not in this change; reported to the operator as a separate fix.
  - Note 4, pin a head SHA in `ragesaq-003`'s `next`: declined. The head moves with every commit to the PR, so a SHA
    in its own record would be stale at once; the node links PR #3, which shows the current head.

## 2026-11-24: PR #1 HyperBench enrollment, round 3

- Record: [summary](patch-review-2026-11-24T09-17-24Z-allegiancecore-enrollment-r3/summary.md), [receipt](patch-review-2026-11-24T09-17-24Z-allegiancecore-enrollment-r3/receipt.md)
- The committed summary is the reviewer's text, unchanged.
- Reviewed head `1122bce`, tree `76d3e5f`, the delta `3ca08ba..1122bce`: the round 2 record and the operator's
  decision that Socket commits here with its GitHub noreply address. Reviewer `deepseek-v4.1-flash` via OpenCode Go
  at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. Round 1 note 3 is closed: Socket's address is in GitHub's noreply form and passes the publish
  guard's identity rule. The committed round 2 summary is byte-identical to the original. Two informational notes,
  no action taken:
  - `memory/mem-004` cites the working session for the decision, as `mem-001` and `mem-002` do.
  - The account id in Socket's address was read from GitHub's user search; the reviewer had no network check.

## 2026-11-24: PR #1 HyperBench enrollment, round 2

- Record: [summary](patch-review-2026-11-24T09-03-37Z-allegiancecore-enrollment-r2/summary.md), [receipt](patch-review-2026-11-24T09-03-37Z-allegiancecore-enrollment-r2/receipt.md)
- The committed summary is the reviewer's text, unchanged (it holds no machine paths or email addresses).
- Reviewed head `3ca08ba`, tree `fcfe4df`, the delta `f4d61cb..3ca08ba` that answers round 1. Reviewer
  `deepseek-v4.1-flash` via OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS. Both round 1 findings are closed, and the reviewer agrees with the dispute of finding 2
  (`README.md:160` names the x64 downloads). The redacted round 1 summary differs from the original by exactly the
  one stated line. Two informational notes, no action taken:
  - The `AGENTS.md` Git Bash line assumes a CRLF checkout, which is what `core.autocrlf=true` gives; Git Bash parses
    the script either way.
  - The round 1 receipt's earlier-attempt row rests on the first attempt's own folder on the reviewing host, not on
    the files of the run it records.

## 2026-11-24: PR #1 HyperBench enrollment, round 1

- Record: [summary](patch-review-2026-11-24T08-42-33Z-allegiancecore-enrollment-r1/summary.md), [receipt](patch-review-2026-11-24T08-42-33Z-allegiancecore-enrollment-r1/receipt.md)
- The committed summary is the reviewer's text with one email address redacted (`GROUND_RULES.md` rule 3).
- Reviewed head `f4d61cb`, tree `e7a4e8f`, the range `b76d6c0..f4d61cb`. Reviewer `deepseek-v4.1-flash` via
  OpenCode Go at `high`, explicit selection, matrix 4.14.0.
- Verdict: PASS-WITH-CONDITIONS. Findings and dispositions:
  1. Low: `STARTER_MANIFEST.json` did not list `memory/mem-001-hyperbench-scope.md`, which is adapted from the copy
     source. Fixed: listed in `localModifications`.
  2. Low: `knowledge/vr-client-survey.md` said its 64-bit claim was derived from the README, which the reviewer
     found silent on it. Partly disputed: `README.md:160` names the x64 downloads. Fixed anyway: the claim now
     names the package reference, the release targets and the README line.
  3. Note, outside the change: the host's instructions name a non-noreply commit address for Socket, and this fork's
     publish guard rejects any address that is not GitHub noreply. Left for the operator to decide.
  - Review limit acted on: the publish guard is checked out with CRLF on Windows, which WSL's `bash` cannot parse.
    `AGENTS.md` now says to run it from Git Bash.
