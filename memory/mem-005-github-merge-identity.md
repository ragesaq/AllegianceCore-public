---
id: mem-005
topic: github-merge-identity
class: verified
observed: 2026-11-24
recorded-by: port
evidence: .github/workflows/guard.yml
---

# GitHub merges stamped addresses the publish guard rejected (fixed by ragesaq-003)

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
through GitHub ("Accept it"); a merge made locally with noreply identities and
pushed was declined. Later the same day the operator said they had turned on
"Keep my email addresses private" for `ragesaq`, `psiclawops-port` and
`psiclawops-socket`. GitHub then stamps each account's noreply address as the
author of a merge. Port has not seen that yet: its API token cannot read the
setting, and no merge has happened since. The first GitHub merge after the
change will show it.

GitHub's committer address stays `noreply@github.com` whatever the setting.
`scripts/publish-guard.sh` accepts that one address as a committer (never as an
author) from ragesaq-003 on. With both in place a GitHub merge passes the
guard. Until ragesaq-003 is merged, a red guard on a push to `master` that is
only a GitHub merge commit is expected. The guard still applies, and must pass,
on every pull request's own commits.
