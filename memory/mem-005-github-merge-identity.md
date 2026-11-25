---
id: mem-005
topic: github-merge-identity
class: verified
observed: 2026-11-24
recorded-by: port
evidence: .github/workflows/guard.yml
---

# A merge made through GitHub fails this fork's publish guard

Observed on 2026-11-24. Port merged PR #1 through the GitHub API at the
operator's direction, with the merge method `merge`. GitHub stamped the merge
commit `daaeaf9` with author `psiclawops-port` at the account's own email
address and committer `GitHub <noreply@github.com>`. Neither address ends in
`users.noreply.github.com`, so `bash scripts/publish-guard.sh b76d6c0 daaeaf9`
fails ("non-noreply author/committer"). `.github/workflows/guard.yml` runs the
guard over each push's range, so a push like that merge fails it whenever
Actions run in this fork. They did not run on PR #1: it showed no checks.

The committer address alone fails the guard, so this holds for any merge,
squash or rebase made in GitHub, whoever does it. The account address is now
in the public history of `master`, which is never rewritten here.

The operator decided on 2026-11-24, in the working session, to keep merging
through GitHub and accept that the guard fails on merge pushes ("Accept it").
The alternatives declined were a merge made locally with noreply identities
and pushed, and "keep my email address private" on the merging account (which
covers the author but not GitHub's committer address). So a red guard run on a
push to `master` that consists only of a GitHub merge commit is expected. The
guard still applies, and must pass, on every pull request's own commits.
