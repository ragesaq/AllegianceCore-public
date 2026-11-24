# Patch review — AllegianceCore HyperBench enrollment

**Target:** `ragesaq/AllegianceCore-public` PR #1, branch `port/hyperbench-enrollment`, commits `b76d6c0..f4d61cb5`.
Reviewed HEAD `f4d61cb54a98b99fe075fb56aa8e1832cbcc70a3`, tree `e7a4e8f0254c2440a96374e1312bbe5a8e5fefe0`
(`git write-tree` matched before and after; `git status --short` shows only the review artifact directory).
**Reviewer:** DeepSeek family — `deepseek-v4.1-flash` through OpenCode Go, reasoning effort `high`, explicit selection
(reviewer-parity matrix 4.14.0, sha256 `74618f22...b63267`; standing at `high`). Authoring family Anthropic/Claude
(Port). Cross-family.
**Change:** 29 files, +8,247 lines, no C#; the only copied-tool edit is the token-pattern spelling `github_pa[t]_`.

## Verdict

**PASS-WITH-CONDITIONS.** The enrollment is structurally sound and every applicable gate passes. Two low-severity
accuracy defects should be fixed (both one-line edits); nothing blocks the operator's merge decision.

## Findings

### F1 — LOW — the manifest omits one file it adapted from the copy source
`STARTER_MANIFEST.json:27-42` (`localModifications`). `memory/mem-001-hyperbench-scope.md` exists at the copy source
(`ragesaq/AllegianceVR` at `90ba914`, blob `48535328`) and differs here (blob `9e4a4392`), yet it is not listed.
Every other file copied from that source is byte-identical, named, or is this manifest itself; mem-001 is the only unexplained divergence.
**Failure:** an auditor reproducing the manifest's provenance claim hashes each path and finds one silent change, so
"what was copied is host-owned" cannot be reproduced exactly for mem-001. `mem-002`, `mem-003` and
`knowledge/vr-client-survey.md` are new paths and are correctly unlisted.
**Fix:** add `"memory/mem-001-hyperbench-scope.md"` to `localModifications` (or state in `about` that memory entries
are repo-authored).

### F2 — LOW — the knowledge note cites a source that does not contain the claim
`knowledge/vr-client-survey.md:20-22` labels "this client renders through OpenGL with shaders and builds for 64-bit"
as *(derived from the README and the code read below)*. `README.md` at `b76d6c0` says nothing about 64-bit, and none
of the cited code lines establish it. The fact itself is true: `release.yml` publishes `linux-x64`, `win-x64` and
`osx-arm64`.
**Failure:** a later reader trusts a provenance pointer that does not support the sentence, which is the exact defect
`knowledge/README.md` rule 1 warns about (a derived claim must name what it rests on).
**Fix:** cite `.github/workflows/release.yml` (the `rid:` list) instead of the README.

### F3 — NOTE (outside the changed lines) — Socket's author address will fail this repo's gate
`AGENTS.md:137-138` and `:140-143` require a GitHub noreply author address; `scripts/publish-guard.sh:12-13` rejects
any identity not ending in `users.noreply.github.com`. The host-level AGENTS.md for this machine names
`Socket <non-noreply address redacted>` for internal fleet contributions. If that identity is used for this public fork, the
fork's own required publish guard fails the push. The repo rule is the closer authority and already resolves it
(use a noreply address here), but the operator should say so explicitly, since the host policy names a non-noreply
one. This is a cross-source tension, not a defect in the changed text.

## Verified by execution

- `check_data_boundary.py`, `check_map.py` and `check_memory.py` all exit 0. `check_map` invokes
  `check_node_loss.py`, so that tool ran too. Only the two documented warnings appear.
- `bash scripts/publish-guard.sh b76d6c0 HEAD` prints `OK (2 commit(s))`.
- `github_pa[t]_` is semantically identical to the original pattern (six samples, including word-boundary,
  `x`-prefixed and short-token edges); it still matches a synthetic fine-grained token, and the publish guard's
  pattern still matches that token but not the added source line.
- Line-ending measurement over all 16,271 blobs at `b76d6c0`: exactly 33 CRLF-only and 1 mixed
  (`docs/reports/BROWSER_CLIENT_P1045_assets/spike-render.patch`, 49 CRLF / 781 bare LF). The manifest's claim is exact.
- `git check-attr` on all 29 added files: every one is `text: set, eol: lf`. `.gitignore` (modified upstream file)
  has no rule and stays `i/lf`. No upstream file existed under `.claude/`, `knowledge/`, `memory/` or `.hyperreview/`,
  so no folder rule can touch upstream content.
- All nine knowledge citations are exact at `b76d6c0`: `Game1.Rendering.cs:262` (Draw), `:426` (ComputeFlightCamera
  call), `Game1.Director.cs:95` (its definition), `:1727` (bloom/HDR/FSR composite), `:1776` (DrawHud), `:295`
  (capture target), `HdCompressedUpload.cs:25` and `:48`, `Game1.cs:1682` (NOVSYNC).
- `mem-003`'s citations are exact: `release.yml:273` is the publish job's repository `if:`; `gitlab-mirror.yml:18`
  is the mirror job's guard; `ci.yml` runs on push/PR to `master`; `guard.yml` runs the guard on every push and PR.
- `check_map.py` and `check_node_loss.py` differ from the hub only `origin/main` → `origin/master`;
  `check_data_boundary.py` differs from the VR copy in exactly one line.

## Verified by reading

- Upstream safety: only `AGENTS.md` (+153/−0, `@@ -22,0 +23,153 @@`, appended after the upstream file's last line)
  and `.gitignore` (+9/−0, `@@ -65,0 +66,9 @@`) change. No upstream line is removed or edited; the new ignore
  patterns are anchored to `.hyperbench-recall/` and `.hyperreview/`, which no upstream path matches.
- `/AGENTS.md` is the only upstream path with an attribute. It changes no index bytes (both LF) but does change
  checkout on an `autocrlf=true` host from CRLF to LF (observed `w/lf`; `.gitignore`, with no rule, is `w/crlf`).
  That is intended and does not affect mergeability.
- Starter files still differ from the hub templates where `localModifications` says so; `knowledge/TEMPLATE.md` is
  identical to the template and correctly unlisted; `memory/CANARY.md` differs from the template and is listed.
- `DAG.json` satisfies its own rules block: `<owner>-<number>` ids, acyclic `needs` (000 → 001 → 002), the `review`
  node carries a URL plus repo files, and every live node has a concrete `next`. `LEDGER.md` has the `ragesaq-000`
  line in the documented format.
- Memory entries carry `id/topic/class/observed/recorded-by/evidence`; each evidence path exists; the index matches;
  `MEMORY-CANARY-7F3A` is present. The protocol is satisfied (checker agrees).
- Knowledge claims that hold: DesktopGL 3.8.5.1; heads Browser/Android/Explorer; the inline scene is about 1,400
  lines (262→1776 is 1,514; 426→1727 is 1,301); vsync default with the documented off-switch; scene → bloom → HDR →
  FSR chain; the linked VR survey exists on branch `port/allegiancecore-decision`.
- Public safety: no secrets, no personal email, no host names, no machine paths. The only drive-letter string is a
  synthetic two-character example in a `check_map.py:205` comment, byte-identical to the reviewed copy. Author and committer are
  the Port noreply address; the co-author trailer is a vendor noreply and matches the reviewed VR baseline commits.

## Limits

- `tools/recall.py check-glossary` was not run (it writes a cache into the worktree). Substituted a read-only check
  that every glossary target term occurs in a searchable source.
- No `dotnet` build or tests: the change contains no C#.
- The publish guard ran from an LF copy under Git Bash. The worktree copy is CRLF, so WSL `bash` (what `bash`
  resolves to from PowerShell on this host) fails to parse it. Git Bash and the Git hook path work. This is
  pre-existing (`scripts/*.sh` has no attribute), not introduced here, but the documented command is shell-dependent.
- The knowledge note's two claims about the C++ fork (fixed-function D3D9, 32-bit) were not re-verified here; they are
  established in the linked VR survey.
- Read-only review: no source file was modified; only this artifact directory was written.