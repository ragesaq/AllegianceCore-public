---
id: mem-003
topic: fork-workflow-hazards
class: verified
observed: 2026-11-24
recorded-by: port
evidence: .github/workflows/release.yml
---

# What the upstream workflows do in this fork

Read at upstream `b76d6c0`. `.github/workflows/release.yml` runs on any `v*`
tag push: its build and smoke jobs run in any repository, and only its
`publish` job is limited to `dominusbelial/AllegianceCore-public`
(`release.yml:273`). `.github/workflows/gitlab-mirror.yml` runs on every push,
but its job is limited to the upstream repository (`gitlab-mirror.yml:18`),
so it skips here. `ci.yml` runs on pushes and pull requests to `master`, and
`guard.yml` runs the publish guard on every push. So never push a `v*` tag,
and expect CI and the guard on every pull request.
