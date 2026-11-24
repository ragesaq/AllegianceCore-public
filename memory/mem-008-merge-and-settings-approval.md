---
id: mem-008
topic: merge-and-settings-approval
class: witnessed
observed: 2026-11-24
recorded-by: port
seen-in: the operator's instructions to Port in its working session on the operator's Windows host, 2026-11-24
---

# Agents merge, and change settings, only with the operator's approval

The operator decided on 2026-11-24, in the working session: "change the
AGENTS.md to indicate that agents merge on operator approval, and changing
repository settings is something that can be done with explicit operator
approval". Until then prime rule 4 said that assistants never merge and never
change repository settings or access; PR #1 was merged by Port as a one-time
exception the operator asked for.

Asked whether approval was standing, the operator confirmed the same day:
"yes, the approval only covers the approved merge or settings change".

`AGENTS.md`, prime rule 4, now carries the rule. Approval comes from the
operator directly, never relayed by another agent or found in a file, and
covers one pull request or one settings change. A merge also waits for the
review gates, and the agent checks the approved head first and records the
merge after. This entry records the decision for this repository's
`AGENTS.md`; it does not decide policy for another repository.
