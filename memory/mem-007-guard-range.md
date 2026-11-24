---
id: mem-007
topic: guard-range
class: verified
observed: 2026-11-24
recorded-by: port
evidence: scripts/publish-guard.sh
---

# The publish guard passed when it could not tell which commits were new

Found by the cross-family review of PR #3 and confirmed with a test harness on
2026-11-24. `scripts/publish-guard.sh` listed the range with `git rev-list`
inside a test. When the base or head commit was not in the clone, `rev-list`
failed, the guard printed "no new commits" and passed. In CI that happens on a
force-push, because `actions/checkout` fetches only commits that some ref still
holds, so the old tip is missing. A force-push carrying a bad commit passed.

A second fault sat in `.github/workflows/guard.yml`. For a new branch (no
"before" commit) it scanned from the first commit of the repository, so any
old commit on `master` that fails the guard failed every new branch. The merge
commit `daaeaf9` (memory/mem-005) is one.

ragesaq-004 fixes both. The guard fails when it cannot list the range. The
workflow scans a new branch, or a force-pushed branch, from its merge base
with the default branch. A force-push that rewrites the default branch from a
tip no ref holds fails closed, and so does a branch that shares no history
with the default branch: a range from its root commit would leave that root
commit out. Only the push that creates the default branch is still scanned
from the root commit. The pre-push hook already used the merge base.

The workflow passes the branch name to the script through `env:`. Pasted in
as `${{ github.ref }}`, a branch named with `$(...)` ran its command (the
review of PR #4 found it, and the test harness confirmed it).
