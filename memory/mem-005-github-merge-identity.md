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
in the public history of `master`, which is never rewritten here. Ways to
avoid it next time are an operator decision: a merge made locally with noreply
identities and pushed, or "keep my email address private" on the merging
account (which covers the author but not GitHub's committer address).
