# LEDGER — the logbook of finished work

One line per completed piece of work, newest first. AI assistants append a
line here whenever a work-map node reaches `done` — in the same PR/commit
that flips the node, so the ledger and the map always agree (the standing
check enforces this). Lines are never edited or removed — this is the
project's permanent record.

Format: `- YYYY-MM-DD — owner — what was finished [node-id] (PR #n)` — or
`(commit <sha>)` while the repo is local-only, before it moves to GitHub.

---

- 2026-11-26 — ragesaq — A joystick button recenters the seated VR view, waiting briefly for tracking [ragesaq-014] (PR #18)
- 2026-11-26 — ragesaq — Target center picks the object nearest the nose by angle, as native does [ragesaq-013] (PR #16)
- 2026-11-26 — ragesaq — Joystick bindings for a split HOTAS, ported from native's input map, with per-device profiles [ragesaq-006] (PR #14)
- 2026-11-25 — ragesaq — VR: the sky (starfield, planets, galaxies) drawn at infinity in each eye, so the eyes converge on it; seen in the headset [ragesaq-009] (commit 4330ed7)
- 2026-11-25 — ragesaq — VR milestone 2: the scene drawn once per eye with the head pose and the HUD on a quad; seen in the headset, follow-ups ragesaq-009 to ragesaq-011 [ragesaq-007] (commit 2572cdd)
- 2026-11-25 — ragesaq — VR milestone 1: the flat frame in an OpenXR headset on one quad layer, opt-in with --vr, flat fallback on any failure [ragesaq-002] (commit 1c521ad)
- 2026-11-24 — ragesaq — Work before adoption is not itemized here; the repository's history through this commit is its record [ragesaq-000] (commit b76d6c0)
- 2026-11-24 — ragesaq — Enrolled the fork in HyperBench (memory, work map, knowledge, recall with vectors) and moved the VR work here [ragesaq-001] (commit daaeaf9)
- 2026-11-24 — ragesaq — Let merges made through GitHub pass the publish guard [ragesaq-003] (commit 10976d0)
- 2026-11-24 — ragesaq — Made the publish guard fail closed when it cannot list the new commits [ragesaq-004] (commit 6da7254)
- 2026-11-24 — ragesaq — Agents merge and change settings only with the operator's approval (AGENTS.md prime rule 4) [ragesaq-005] (commit 47e720b)
