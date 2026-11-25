---
id: mem-009
topic: windows-fast-tier
class: witnessed
observed: 2026-11-24
recorded-by: port
seen-in: Port's fast-tier runs on the operator's Windows host for ragesaq-002 (VR milestone 1), 2026-11-24, at master 47e720b and on port/vr-m1; Windows 11, .NET SDK 10.0.404, Python 3.14
---

# The fast test tier does not run cleanly on Windows

`tools/run-tests.sh` runs `tools/test-tiers/runner.py`, which imports `fcntl`.
Windows Python has no `fcntl`, so the runner stops before any test runs. Port
stood in for it by running `dotnet test --no-build` per project with the same
filter the runner builds:
`(Tier=Fast)&(Lane!=LiveTiming)&(Requires!=Artwork&Requires!=NativeSource&Requires!=Gpu&Requires!=MeshyPack&Lane!=LiveTiming)`.

On unchanged `master` (`47e720b`) that run failed 23 tests on Windows. Most
look like Windows-host differences (shader source hashes, paths, timing,
socket permissions), and some are intermittent: across three runs, a CPU-clock
benchmark test, two update tests and four transport tests each passed in one
run and failed in another. The four transport tests failed with a socket
permission error (`WSAEACCES`) in the one run during which SteamVR was
starting; the cause was not shown.

So on Windows, compare a change's failures with a baseline run of its base
commit, test by test, rather than expecting a green run. CI runs the tier on
Linux, where the runner works.
