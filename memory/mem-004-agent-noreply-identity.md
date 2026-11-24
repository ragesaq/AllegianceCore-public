---
id: mem-004
topic: agent-noreply-identity
class: verified
observed: 2026-11-24
recorded-by: port
evidence: scripts/publish-guard.sh
---

# Both agents commit here with their GitHub noreply addresses

The upstream publish guard rejects any author or committer address that does
not end in `users.noreply.github.com` (`scripts/publish-guard.sh:13`). The
first cross-family review of the enrollment noted that Socket's host
instructions name a different address for other work. The operator decided on
2026-11-24, in the working session, that Socket uses its GitHub noreply
address in this fork ("Yes, noreply"):
`Socket <323031202+psiclawops-socket@users.noreply.github.com>`, where
323031202 is the account id of `psiclawops-socket`. Port already uses its own
noreply address. `AGENTS.md`, "Agent identity contract", carries both.
