---
id: mem-008
topic: merge-and-settings-approval
class: verified
observed: 2026-11-24
recorded-by: port
evidence: AGENTS.md
---

# Agents merge, and change settings, only with the operator's approval

The operator decided on 2026-11-24, in the working session: "change the
AGENTS.md to indicate that agents merge on operator approval, and changing
repository settings is something that can be done with explicit operator
approval". Until then prime rule 4 said that assistants never merge and never
change repository settings or access; PR #1 was merged by Port as a one-time
exception the operator asked for.

`AGENTS.md`, prime rule 4, now carries the rule. Approval is per pull request
and per settings change, so one approval never covers the next. The agent
checks the approved head before merging and records what it did. The rule
does not cover repositories other than this fork.
