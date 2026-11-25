---
id: mem-006
topic: stays-a-fork
class: verified
observed: 2026-11-24
recorded-by: port
evidence: memory/mem-005-github-merge-identity.md
---

# The repository stays in the fork network

The operator asked on 2026-11-24, in the working session, whether this
repository could stop being a fork. GitHub's "Leave fork network" setting
(Settings, General, Danger Zone) was open to it then: the repository was
public, about 936 MB against a 1 GB limit, with no forks of its own. Leaving is
permanent, drops the repository's pull requests and their comments, and does
not change the merge-identity trap in `mem-005`, which comes from how GitHub
stamps merges. The operator decided to keep it a fork ("Keep it a fork").
Upstream syncs go through the git remote `upstream` either way.
