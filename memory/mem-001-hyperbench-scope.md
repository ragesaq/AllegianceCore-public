---
id: mem-001
topic: hyperbench-scope
class: verified
observed: 2026-11-24
recorded-by: port
evidence: STARTER_MANIFEST.json
---

# HyperBench scope here, and the two-agent rules

The operator directed on 2026-11-24, in the working session, that this fork be
enrolled in HyperBench and that the VR work records move here ("Enroll the
fork"). The scope is the one the operator chose for ragesaq/AllegianceVR the
same day: HyperBench's memory, decision and orientation system with vector
recall, not its wider rule set, plus the collaboration rules Port and Socket
use on the operator's other projects. The HyperBench files were copied from
AllegianceVR's reviewed enrollment head `90ba914`. `STARTER_MANIFEST.json`
records what was adopted and skipped, and the fork's section at the end of
`AGENTS.md` carries the rules; the upstream author's rules above it still
apply.

Line endings were skipped because the hub checker wants every tracked file
LF, and this fork keeps upstream files byte-identical. `check_map.py` warns
that `line-endings` is a floor module, and `check_memory.py` warns that a
`recorded-by` name has no `people/` folder (the `people` module is skipped).
Those warnings are expected here and do not fail the checks.
