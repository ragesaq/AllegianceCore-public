# AGENTS.md — notes for coding agents working in AllegianceCore

- **Start with [GLOSSARY.md](GLOSSARY.md)**: the per-domain index of terms, key files and the native source each
  system ports. **Update it** when you add a system, a recurring term or an architectural pattern
  (`tools/check-glossary-links.py` checks every path it names; CI runs it).
- **Behaviour parity with native Allegiance** (`FreeAllegiance/Allegiance`): match native behaviour exactly; visuals may
  differ. Cite native `file:line` for every rule you port, and track it in `docs/parity/*.csv`
  (`scripts/parity-scorecard.py` validates them).
- **HUD work covers both styles** (`HudStyle.Classic` and `HudStyle.Modern`): put behaviour in the shared model, test
  both, capture both.
- **New apps** go under `src/Allegiance.<Name>/` (csproj + own `Dockerfile`, registered in `AllegianceCore.slnx` and
  `docker-compose.yml`), tests under `tests/Allegiance.<Name>.Tests/`; one-off tools under `tools/`.
- **Any new service, store, port or env var** is reflected in `docker-compose.yml` (+ `docker/`), and
  `docker compose config` must validate.
- **Public repo hygiene:** no secrets, personal emails or absolute home paths in code; new media is reviewed before a
  push. `scripts/publish-guard.sh` (pre-push hook `.githooks/pre-push`, and CI) enforces this.
- **Gate:** `dotnet build AllegianceCore.slnx` with 0 warnings, `tools/build-tools.sh`, new tests 3× alone, one
  `tools/run-tests.sh --tier fast`; known flakes are ledgered in `docs/TEST_FLAKES.md`.
- **Training changes also gate Live lessons:** run `tools/run-training-tests.sh --no-build --repeat 3` with artwork.
  It runs both client test projects serially across Fast and Live, including Nanite completion.
- Generated maps log `seed=N`; `--map-seed N` rebuilds the same map. `scripts/dev-stack.sh up|down|status` runs a local
  server + lobby (+ bots).

# AllegianceCore VR fork: rules for Port and Socket

Everything above this heading is the upstream author's `AGENTS.md`, kept unchanged; it still applies. This section is
the fork's own and stays at the end of the file, so upstream syncs merge cleanly.

This repository is the operator's fork of [dominusbelial/AllegianceCore-public](https://github.com/dominusbelial/AllegianceCore-public),
the C#/.NET rewrite of Allegiance. The goal of the fork is an OpenXR VR client. The fork belongs to the operator,
**ragesaq**. Two agents work here: Port (Claude Code) and Socket (Codex). Both read this file as their rules. The
earlier C++ fork, [ragesaq/AllegianceVR](https://github.com/ragesaq/AllegianceVR), keeps the survey of the C++ engine;
`knowledge/vr-client-survey.md` here says why the VR work moved.

Work is tracked with HyperBench's memory, decision and orientation system (hub: `PsiClawOps/hyperbench`, taken at
commit `e98c3185`, HyperBench 0.43.0). [`STARTER_MANIFEST.json`](STARTER_MANIFEST.json) records what was adopted.

## Prime rules

1. **No org-restricted data (as defined by your org policy) in the repo or the coding session.** This is a public
   repository: [`GROUND_RULES.md`](GROUND_RULES.md) says what may never be committed. No secrets, personal data,
   host names or machine paths. The upstream publish guard (`scripts/publish-guard.sh`) must pass before every push.
2. **Never push a `v*` tag.** `.github/workflows/release.yml` builds release packages on `v*` tags. Its publish job
   and the GitLab mirror (`.github/workflows/gitlab-mirror.yml`) run only in the upstream repository, but the build
   jobs would still run here. Never dispatch either workflow.
3. **Keep upstream mergeable.** Remote `upstream` is dominusbelial's repository, fetch only. Do not reformat,
   re-indent or renormalize upstream files. Follow the upstream rules above for anything that could go upstream:
   native `file:line` citations, both HUD styles, a build with 0 warnings, failing-first tests. Put VR work behind
   clear seams (new projects and files where possible, small hooks into existing code). HyperBench files stay out of
   any PR to upstream.
4. **Changes land by pull request. An agent merges a pull request only with the operator's approval for that
   pull request**, given in a comment on it or in a working session. Approval of one pull request does not carry
   over to another. Before merging, check that the head is the one that was reviewed and approved, and stop if it
   has moved. After merging, record the merge commit on the work map node (`memory/mem-005` says how GitHub
   stamps it). **Repository settings and access change only with the operator's explicit approval of that
   change**, such as turning off one workflow. Make only the approved change and record it in the Issue or pull
   request where it was asked for. Without that approval, never change settings, access, branch protection or
   visibility (`memory/mem-008`).

## Start of a session

- Run `git status --short --branch`. If the worktree is clean, `git fetch origin` and `git pull --rebase` before
  trusting local records. If it is dirty, do not discard anything: inspect and work around it.
- Read [`GLOSSARY.md`](GLOSSARY.md) for the code, then [`memory/README.md`](memory/README.md), [`DAG.json`](DAG.json)
  and [`knowledge/README.md`](knowledge/README.md), then the records relevant to the task. Follow the
  [hyperbench-lifecycle](.claude/skills/hyperbench-lifecycle/SKILL.md) skill at task start, when something durable
  is learned, and at handoff. A session title or an old handoff never picks the task.
- Search the notes with `python tools/recall.py search "<words>"` (see Recall below). A hit is a lead: open the file
  and cite it.

## Where things are kept

- **Work in flight:** `DAG.json`, one node per piece of work, ids `<owner>-<number>`. Its `rules` block is the
  contract. Update a node's `status`, `next` and `updated` in the same PR as the work. Before an action that could end
  the session or run long, add the node's `attempt` record first.
  The rules block is the hub's canonical text, which `check_map.py` requires verbatim: where it says `main`, read
  this fork's default branch, `master`.
- **Finished work:** [`LEDGER.md`](LEDGER.md), one append-only line per node that reaches `done`. Node `ragesaq-000`
  stands for all history before adoption (2026-11-24).
- **Short facts:** `memory/`, following [`docs/MEMORY_PROTOCOL.md`](docs/MEMORY_PROTOCOL.md). Each entry carries its
  trust class; what the repository shows now beats a memory entry. No entry is `canonical` here, because the `people`
  module is skipped. Record an operator decision as an ordinary entry that cites where it was made. Before saying
  "there is no record of X", find `MEMORY-CANARY-7F3A` by the same search and name the surface you searched.
- **Findings and decisions:** `knowledge/`, following [`docs/KNOWLEDGE_NOTES.md`](docs/KNOWLEDGE_NOTES.md). A decision
  is a `(decided YYYY-MM by <who>)` claim in a note, with the rejected options beside it. Write the note when the
  finding lands, in the same PR as the work.
- **Coordination:** GitHub Issues for investigations, status and durable handoffs; the pull request for
  change-specific discussion, review and validation evidence. Link the Issue from the PR.

## Workspaces and branches

- **Each agent has its own workspace on this host.** Port works under its Claude Code tree and Socket under its Codex
  tree. An agent may read the other's tree but never writes into it.
- **One writer per branch and worktree.** Port uses `port/<topic>`, Socket uses `socket/<topic>`. Never commit to,
  rebase or push another agent's branch; ask through the Issue or PR instead.
- PRs target `master`, or the branch they build on when stacked. Name the base in the PR. Prefer `git pull --rebase`
  over merge commits for routine sync. Keep commits scoped to one topic.
- **Upstream sync:** fetch `upstream`, merge `upstream/master` into a `<agent>/upstream-sync` branch, build, and open
  a PR. Never rebase or force-push `master`.

## Review gates

- **The upstream gate applies to code.** A change to C# code passes `dotnet build AllegianceCore.slnx` with 0
  warnings and the fast test tier, as the upstream rules above say, before it is declared done.
- **After every behavior-changing edit, run `hyperreview-reflex`** on the concrete change before declaring it done,
  a one-line fix included. It decides mechanically whether an inline review is enough or an independent
  cross-family review is required.
- **Escalation is mechanical.** Contract, serialization or shared-memory changes, concurrency, trust boundaries,
  review/gate/policy/build changes, more than 400 reviewable lines or 20 files, and high-blast-radius paths (the
  graphics device, the frame loop, input, the wire protocol) go to an independent cross-family review. Only pure
  prose, formatting, generated-only and lockfile-only changes skip the gate.
- **Record every review where others can see it.** Freeze the reviewed commit, review from a separate worktree,
  commit the summary under `.hyperreview/patch-review-<date>-<topic>-r<N>/`, and post the result as a PR comment:
  reviewed head, reviewer and effort, verdict, findings and their disposition. Fix findings in a new commit and
  re-review the delta; a disputed finding needs evidence. Only `summary.md` and a path-free `receipt.md` are
  committed; the wrapper's logs and state stay on the reviewing host.
- Use the other HyperReview modes where they fit: `hyperreview-readiness` for a plan before coding,
  `hyperreview-closeout` before handing a build to ragesaq for headset testing, `hyperreview-architecture` for
  cross-module design.
- In-headset testing by ragesaq is the downstream acceptance step. Reviews verify source, diffs, logs, tests and
  builds up to that point; missing headset validation is residual risk, not a readiness blocker.

### Cross-family review route

- **Default reviewer: `deepseek-v4.1-flash` at `high`, through OpenCode Go, explicit selection** (ragesaq,
  2026-11-24, for the VR project: "deepseek v4.1 flash, which should be the default reviewer for this project"; the
  same route is declared in AllegianceVR's `AGENTS.md`). `high` is the only effort at which the reviewer-parity matrix
  gives it standing. It is a non-Anthropic family, so it reviews Port's work; for a change Socket authors, check that
  the installed matrix still puts it outside Socket's family.
- **Socket/Codex:** use the package-owned `hyperreview-xfam-route` skill and its selector with this reviewer named
  explicitly, the declared author tuple and a refreshed host registry. **Port:** use its host's cross-family review
  launcher with these coordinates.
- An OpenCode Go 429 ("at capacity") is retried once after a pause. If the route is still unavailable, or a launch
  fails or its effective provenance does not match, record `escalate-unavailable` and ask ragesaq. Do not fall
  through to another reviewer without that answer. Never substitute a same-family review and call it independent.
- Each posted review names this section as the route's source, with the matrix version and hash.

## Agent identity contract

| Actor | GitHub identity | Git author | Role |
|---|---|---|---|
| **ragesaq** | [@ragesaq](https://github.com/ragesaq) | `ragesaq` | Operator. Owns the fork. Decisions, approvals (of merges and of settings changes), headset testing |
| **Port** | `psiclawops-port` | `Port <323790917+psiclawops-port@users.noreply.github.com>` | Claude Code agent on the operator's Windows host |
| **Socket** | `psiclawops-socket` | `Socket <323031202+psiclawops-socket@users.noreply.github.com>` | Codex agent on the same host |

1. **Commit under your own identity**, set per clone or per worktree, never `--global` and never in a checkout shared
   with the other agent. Before pushing, `git log -1 --format='%an <%ae>'` must show your own identity, and the
   address must be a GitHub noreply address (the publish guard rejects any other). This holds here even where an
   agent's host instructions name another address for other work (`memory/mem-004`). Push only with your own
   credentials.
2. **Post under your own GitHub account, and say who you are.** Issues, PRs, comments and reviews start with an
   attribution line: `> _Posted by <Agent> (<runtime, model>) on behalf of @ragesaq._`
3. **One concern per post**, on the right surface: a new Issue for a bug or feature, a comment on the existing Issue
   or PR for status, a PR review or comment for review results.

## Checks

Before committing a change to the stores they guard, from the repository root, in this order:

```powershell
python tools\check_data_boundary.py
python tools\check_map.py
python tools\check_memory.py
python tools\recall.py check-glossary
```

Before pushing, run the upstream publish guard on the new commits: `bash scripts/publish-guard.sh origin/master HEAD`.
On Windows, run it from Git Bash: the script is checked out with CRLF there, and WSL's `bash` cannot parse it.

Expected warnings, which do not fail the checks: `check_map.py` reports `line-endings` as a floor module that this
repo skips, and `check_memory.py` reports that a `recorded-by` name has no `people/` folder. `memory/mem-001` explains
both.

## Recall

`tools/recall.py` searches `AGENTS.md`, `memory/`, `knowledge/` and `docs/` by words, glossary aliases
([`RECALL_GLOSSARY.json`](RECALL_GLOSSARY.json)) and, where switched on, vectors. Full contract:
[`docs/RECALL.md`](docs/RECALL.md). Here `docs/` also holds the upstream author's documentation, so recall covers it
too. Word search needs only Python and git. Vectors are switched on per working tree with `enable-vectors`, need
Python 3.10+ with `numpy` and `model2vec` in a virtual environment outside the repo, and the pinned `potion-base-8M`
model files. The cache lives in `.hyperbench-recall/`, which is never committed. After enabling,
`python tools/recall.py check-vectors` must pass. Add a glossary row when a search misses because people ask in
different words from the notes.
