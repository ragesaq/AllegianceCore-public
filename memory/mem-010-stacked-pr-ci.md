---
id: mem-010
topic: stacked-pr-ci
class: witnessed
observed: 2026-11-25
recorded-by: port
seen-in: PR #9 (VR milestone 2), stacked on PR #7, after PR #7 merged on 2026-11-25; .github/workflows/ci.yml at master 1c521ad
---

# A stacked PR gets no build-and-test until it targets master, and a base change does not start one

`.github/workflows/ci.yml` runs `build-and-test` on `pull_request` for branch
`master` only, with the default event types (opened, synchronize, reopened).
A PR whose base is another PR's branch gets only the `guard` jobs, so its code
has not been built and tested on Linux while it is stacked.

When the lower PR merged and PR #9 was retargeted to `master`, no run started:
a base change is an `edited` event. Closing and reopening the PR started one on
the same head; so does a new push. GitHub shows the job log and the
`test-tiers` artifact only to a signed-in user, so an agent without a browser
sign-in sees which run failed but not which step or test.

So for a stacked PR, expect the first `build-and-test` only after the
retarget, and count it as a merge gate of its own.
