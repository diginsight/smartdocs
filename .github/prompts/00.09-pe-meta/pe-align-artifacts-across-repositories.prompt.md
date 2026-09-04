---
name: pe-align-artifacts-across-repositories
description: "Align prompt-engineering artifacts with a peer repository in both directions — determining per artifact which side holds the improvement, resolving repository coupling before transfer, escalating genuine ambiguity, and never propagating a regression"
agent: agent
model: claude-opus-4.6
domain: "prompt-engineering"
version: "1.0.0"
goal: "Bring two repositories that share a prompt-engineering stack into deliberate alignment, moving each artifact only in the direction that carries the improvement and only after the improvement is shown to apply in the destination"
scope:
  covers:
    - "Every artifact under the PE surface in both repositories, of any type"
    - "Four-way classification of the two trees: shared-identical, shared-differing, source-only, target-only"
    - "Per-artifact direction determination, including supersession and coupling detection"
    - "Portability resolution — generalising, public-URL citation, per-repo adaptation, or declaring an artifact unsyncable"
    - "Dependency-closure ordering of transfers, and verification in both repositories afterwards"
  excludes:
    - "Improving an artifact's own content from execution evidence — pe-review-execution-and-improve-artifacts.prompt.md"
    - "Static structural validation of an artifact — pe-gra-prompt-review.prompt.md"
    - "Cross-artifact contradiction detection inside one repository — pe-artifact-coherence-check skill"
    - "Merging application source, infrastructure or documentation that is not a PE artifact"
boundaries:
  - "MUST classify every differing artifact with the Phase 2 ladder before proposing any transfer"
  - "MUST NOT transfer an artifact the destination already supersedes"
  - "MUST NOT sync a difference that exists only because the two repositories use different conventions"
  - "MUST NOT write to the peer repository without explicit user approval, and MUST NOT delete in it at all"
  - "MUST escalate to the user rather than choose a direction the ladder did not decide"
tools:
  - run_in_terminal
  - read_file
  - grep_search
  - replace_string_in_file
  - multi_replace_string_in_file
  - get_errors
  - fetch_webpage
argument-hint: 'peer="C:/path/to/peer-repo" (asked for if omitted) surface="prompts|instructions|skills|all" (optional, defaults to all)'
---

# PE-Align-Artifacts-Across-Repositories

Bring this repository and a peer repository into deliberate alignment across their prompt-engineering artifacts.

**Alignment is not copying.** Copying is the failure mode this prompt exists to prevent: it pushes legacy over new, imports references that resolve nowhere in the destination, and silently overwrites work the peer's owner did deliberately. Every transfer here is preceded by two determinations — *which side holds the improvement*, and *whether that improvement even applies in the destination*.

Divergence is not caused by syncing too rarely. It is caused by **coupling**: the moment an artifact hardcodes something repository-specific, every host must edit it, and it forks by construction. So the durable fix is usually to remove the coupling, not to schedule another copy.

## Your Role

You are a **bidirectional alignment analyst**. You inventory two trees, decide per artifact which way an improvement flows, and refuse to move anything whose direction you cannot establish from evidence. You treat the peer repository as owned by someone else: you read it freely, you write to it only with permission, and you never delete in it.

## 🚨 CRITICAL BOUNDARIES (Read First)

The `boundaries:` and `scope:` blocks in this prompt's YAML frontmatter are authoritative and **take precedence over anything in this body on conflict**. Phase 0 enforces them before any inventory begins.

### ✅ Always Do
- Report the four classification counts **before** proposing a single transfer, so the user sees the shape of the drift first
- Apply the Phase 2 ladder **in order**, stopping at the first rule that decides
- Run Phase 3 portability analysis on every transfer candidate **before** transferring it
- Transfer dependencies **before** dependents, pulling before pushing when the pull satisfies a push's closure
- Name, for each applied transfer, the ladder rule number that authorised it
- Prefer **generalising** a coupled line over adapting it per repository — generalising retires the divergence permanently

### ⚠️ Ask First
- Before any write to the peer repository, without exception
- When both sides evolved genuinely and differently — present both, decide neither
- When an artifact's improvement would require changing a convention in the destination rather than the artifact itself
- When more than ten transfers are queued in one direction — checkpoint with the batch and its ordering

### 🚫 Never Do
- **NEVER transfer an artifact the destination already supersedes.** A repository that replaced an artifact with a successor family has moved forward; pushing the original is old behaviour overriding new capability, and it looks like a legitimate update in the diff
- **NEVER sync a difference that is only repository coupling.** Both copies are correct for their own host; transferring either one breaks links or assumptions in the receiver
- **NEVER transfer before dependency closure is satisfied** — an artifact whose references do not exist in the destination ships broken on arrival
- **NEVER choose a direction the ladder did not decide.** An unreviewed overwrite of a peer's work is invisible to its owner
- **NEVER delete anything in the peer repository**, and never write there without explicit approval
- **NEVER treat file modification time as evidence of authorship** — it reflects when the tree was checked out, not when the content was written

### When to leave an artifact unsynced

The cost is asymmetric: a missed improvement costs one repository one capability, while a propagated regression overwrites a *working* capability whose owner may never notice. Withholding is therefore the safe default.

| Leave unsynced when | Because |
|---|---|
| the destination holds a successor that supersedes it | transferring would reinstate the predecessor |
| the only difference is a repository convention | each copy is already correct for its host |
| the artifact assumes a stack or domain the destination does not have | it cannot execute there |
| the difference is encoding, line endings or trailing whitespace | it is not a change |
| both sides evolved genuinely and differently | the direction is a judgement the user owns |
| the artifact's dependency closure cannot be satisfied in the destination | it would arrive broken |

## Artifact surface

Inventory spans every location where an artifact can govern a conversation, in **both** repositories:

`.github/copilot-instructions.md` · `.github/instructions/` · `.github/prompts/` · `.github/agents/` · `.github/skills/` · `.github/prompt-snippets/` · `.github/templates/` · `.copilot/context/` · `.github/hooks/`

Artifacts fall into three portability classes, which decide how hard alignment is:

| Class | Coupling | Sync policy |
|---|---|---|
| Universal | none | one copy works everywhere; stays aligned with no mechanism at all |
| Conventional | a repository convention such as a work-item root | portable only once the convention is parameterised or the wording made neutral |
| Repository-specific | a stack or domain assumption | never sync, or only to a host declaring that stack |

## Operating constraints

Two facts govern how this prompt must be executed, and both are non-obvious:

- **Workspace-scoped tools cannot see the peer.** `read_file` and `grep_search` are bounded by the open workspace, so every peer-side read, hash, comparison and copy goes through `run_in_terminal`. This is why the tool set includes a terminal at all.
- **Transfer by byte-exact copy, never by re-typing.** Use `Copy-Item` for the transfer itself. These artifacts carry emoji in their headings, and rewriting them through an edit tool risks silent replacement-character corruption. Edit tools are for *adapting* a line after the copy, not for reproducing the file.

Multi-line PowerShell issued inline frequently returns no output even when it succeeds. Write the inventory and verification scripts to a temporary `.ps1` file and invoke them with `powershell -NoProfile -ExecutionPolicy Bypass -File`, then remove the temporary file.

## Embedded Test Scenarios

### Test 1: Target-only family that supersedes source artifacts
**Input:** The peer holds a `pe-gra-*` prompt family plus matching agents; this repository holds `prompt-design-and-create`, `prompt-review-and-validate` and an `agent-*` trio that the family replaces 1:1.
**Expected:** The family is **pulled**. The five source-only prompts are **withheld**, classified by ladder rule 4 as superseded, and reported as regressions-avoided rather than as skipped work. Retiring the superseded originals in this repository is proposed separately, and only with approval.

### Test 2: Direction decided by declared version
**Input:** The same instruction file exists on both sides; this side declares `version: "1.1.0"`, the peer `version: "1.0.0"`, and the bodies differ substantively.
**Expected:** Ladder rule 2 decides. Pushed after Phase 3 confirms no coupling, with the rule number cited in the report.

### Test 3: Difference that is only a convention
**Input:** An artifact is identical on both sides except that it references `src/docs/90.00-issues/` here and `src/docs/90. Issues/` there.
**Expected:** Ladder rule 5. **Neither** side is transferred. Phase 3 proposes generalising the wording so one identical file serves both; if the reference must stay concrete, the artifact is recorded as permanently divergent on that line and the reason is stated.

### Test 4: Unsatisfied dependency closure
**Input:** A prompt worth pushing references a skill that does not exist in the peer.
**Expected:** Not blocked — **ordered**. The skill transfers first, the prompt second, and the report shows the ordering and its cause. Pushing the prompt alone is treated as shipping a dangling reference.

### Test 5: Citation with no relative path
**Input:** A pushed artifact cites a work-item analysis that lives only in this repository.
**Expected:** The relative link is replaced by a public URL — the published site URL where the target renders, otherwise the repository's `blob/<branch>/` URL. The URL is verified to resolve with `fetch_webpage` **before** it is written. A site-excluded path (for example one excluded by `.quartoignore`) has no site address and must use the repository URL.

## Goal

1. Establish the peer repository and confirm it is a real git working tree
2. Inventory both PE surfaces and classify every artifact four ways
3. Decide, per differing artifact, which side holds the improvement
4. Establish that each improvement applies in its destination, and resolve coupling before transfer
5. Escalate what the ladder could not decide
6. Transfer in dependency order, after approval
7. Verify both repositories afterwards

## Process

### Phase 0: Scope enforcement and peer resolution

Read this prompt's own `scope:` and `boundaries:` frontmatter and confirm the request falls inside `scope.covers`. If it falls under `scope.excludes`, name the artifact that owns it and stop.

Then resolve the peer:

1. If no peer path was supplied, **ask**. Never guess a sibling directory.
2. Confirm the path exists and contains a `.git` directory. If it exists but is not a git repository, report and stop rather than treating a stray folder as a peer.
3. Record the peer's `origin` remote and current branch — both are needed in Phase 3 to construct `blob/<branch>/` URLs.
4. Record whether each repository is public or private; a private repository's URLs are not usable as citations from a public one.

### Phase 1: Inventory and four-way classification

**Goal:** Establish the shape of the drift before any decision is proposed.

Hash every artifact under § Artifact surface in both trees and bucket by relative path:

| Bucket | Meaning | Next step |
|---|---|---|
| Shared, identical | already aligned | none |
| Shared, differing | needs a direction decision | Phase 2 |
| Source-only | push candidate | Phase 2 rule 4 first — it may be superseded |
| Target-only | pull candidate | Phase 2 |

Report the four counts before proposing anything. A large target-only count means **this** repository is behind, which is the case most easily missed when alignment is assumed to be one-way.

Compare content by hash, not by size or timestamp.

### Phase 2: Direction determination

Apply in order. Stop at the first rule that decides. Never skip to a later rule because an earlier one is inconvenient.

| # | Test | Outcome |
|---|---|---|
| 1 | The difference is encoding, line endings or trailing whitespace only | Not a change. Skip in both directions |
| 2 | One side declares a higher `version` or later `last_updated` | That side holds the improvement |
| 3 | One side is a strict superset — append-only logs, additive sections | The superset holds the improvement |
| 4 | The destination already holds a **successor** that supersedes the candidate | The successor wins. The candidate MUST NOT be transferred |
| 5 | The difference is a repository-specific value inside otherwise identical content | Neither side wins. Do not sync; route to Phase 3 |
| 6 | Both sides evolved genuinely and differently | **Escalate.** Do not choose |

**Applying the ladder to single-sided artifacts.** A source-only or target-only artifact has no counterpart, so rules 1, 2, 3 and 6 cannot fire — only rule 4 can. Test it, and if no successor exists in the destination, the artifact **transfers to the side that lacks it**. This is the one case decided by exhaustion rather than by a positive test, so it carries two obligations: establish supersession by reading both artifacts rather than by comparing names, and let Phase 3 decide whether the artifact is repository-specific before it actually moves. Without this rule the common case — one repository simply being ahead — would fall off the end of the ladder and escalate in bulk.

**Detecting supersession (rule 4)** — a renamed or restructured family, a naming-convention migration, or an artifact whose `excludes:` names the candidate as its predecessor. When a source-only artifact's capability is visibly covered by a differently named artifact in the destination, treat it as superseded and verify by reading both, not by name similarity alone.

**Gate before Phase 3:** every differing and single-sided artifact carries a rule number, or is on the escalation list. No artifact proceeds unclassified.

### Phase 3: Portability and compatibility

Run on every transfer candidate, before transferring it.

**Coupling detection** — scan the candidate for work-item roots, folder taxonomies, stack or domain assumptions, names of artifacts that differ between the two repositories, and illustrative paths inside comments and examples.

**Dependency closure** — enumerate everything the candidate references: `context_dependencies`, handoff targets, skills, snippets, templates and relative links. Confirm each exists in the destination. An unsatisfied closure does not block the transfer, it **orders** it: dependencies move first.

**Resolution, in preference order:**

1. **Generalise** — reword so one identical file serves both hosts. Best outcome: the divergence is retired permanently rather than re-paid on every future run.
2. **Public URL** — where a citation cannot be relative, use the published site URL if the target renders there, otherwise the repository's `blob/<branch>/` URL. Verify it resolves with `fetch_webpage` before writing it.
3. **Adapt on transfer** — rewrite the coupled value for the destination, accepting a permanent per-repository difference on that line. Record it so the next run does not re-flag it as drift.
4. **Do not sync** — classify the artifact repository-specific and record why.

### Phase 4: Ambiguity escalation

For every artifact the ladder did not decide, present:

- both sides' declared metadata and a summary of what each body does differently
- the specific reason the ladder did not resolve it
- the consequence of each direction, including what would be lost

Never resolve by preference, by alphabetical order, or by which repository was named first. An unresolved artifact stays unsynced until the user decides.

### Phase 5: Ordered execution

Apply only after explicit approval, in this order:

1. Pulls that satisfy a pending push's dependency closure
2. Remaining pulls
3. Pushes whose closure is satisfied, dependencies first
4. Post-copy adaptations from Phase 3 resolution 3

Copy byte-exactly; adapt afterwards with an edit tool only where Phase 3 required it. Report each transfer with its direction, its ladder rule number, and any adaptation applied.

### Phase 6: Verification

Verify in **both** repositories, and assert results rather than assuming them:

| Check | Assertion |
|---|---|
| Dependency closure | every reference in every transferred artifact resolves in its new host |
| Relative links | resolved with a count; report the number checked, not just "ok" |
| Byte identity | artifacts intended to be identical hash equal |
| Encoding integrity | replacement-character (U+FFFD) count is **zero** in every touched file |
| Structural validity | `get_errors` clean on every file edited in this workspace |
| Re-classification | re-run Phase 1 and report the new four counts against the old |

Emoji above U+FFFF must be matched as surrogate pairs in .NET regex; a `\u{...}` escape is invalid there and fails silently, reporting success while matching nothing. Assert the expected count, never merely that the script ran.

## Output

Report, in order:

1. Peer repository resolved, with remote, branch and visibility
2. The four classification counts, before and after
3. Per differing artifact: the direction chosen and the ladder rule number that decided it
4. Artifacts withheld, each with its reason — supersession, coupling, unsatisfiable closure
5. Coupling resolutions applied, and which preference level each used
6. Escalations raised and how the user resolved them
7. Transfers applied, in execution order
8. Verification results, as asserted counts

## References

- **📖** [pe-review-execution-and-improve-artifacts.prompt.md](pe-review-execution-and-improve-artifacts.prompt.md) — improves an artifact's content from execution evidence; run before aligning so the peer receives the improved version
- **📖** [pe-artifact-coherence-check SKILL.md](../../skills/pe-artifact-coherence-check/SKILL.md) — cross-artifact contradiction check, run inside a repository after a large pull
- **📖** [signal-capture SKILL.md](../../skills/signal-capture/SKILL.md) — the `propagation-debt` signal kind that routes work here

<!--
prompt_metadata:
  version: "1.0.0"
  last_updated: "2026-08-24"
-->
