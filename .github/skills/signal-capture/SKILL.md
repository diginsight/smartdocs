---
name: signal-capture
description: >
  Captures activities a conversation surfaced that were never in scope for the
  current goal — knowledge belonging to an authority document elsewhere, changes
  needing replication in a peer repository, commitments about other components,
  subjects worth developing, expression defects worth correcting, and prompt-engineering
  artifacts that failed to meet their own declared goal. Defines the
  signal record shape, the relevance/actionability priority derivation, and the
  detection sweep. Use when splitting a conversation into an issue analysis, when
  closing a work item, when an item is being routed away from a plan's park lot,
  or when deciding whether something belongs in the park lot or on a signals page.
domain: "prompt-engineering"
---

# Signal capture

## Purpose

Give conversation-surfaced work that lies **outside the current goal** a durable, self-contained record, so it survives the conversation instead of dying in its payload.

## When to use

- Splitting a conversation into an issue analysis (overview plus case pages)
- Closing a work item, before its plans reach `done`
- Routing an item away from a plan's § Park lot
- Deciding whether something is a park-lot item or a signal

## Park lot or signal?

| | § Park lot | Signal |
|---|---|---|
| Relationship to the goal | in-domain, deliberately excluded | **never** in scope |
| Guarantees | coverage — nothing in-domain was dropped | continuity — nothing out-of-domain was dropped |
| Where it lives | inside the plan file | outside every plan — a page in the work-item folder |
| Lifetime | dies with the plan | outlives every plan in the work item |

MUST NOT park a divergent item. A plan is terminal; a signal outlives it.

## Workflow

### 0. Establish admissible evidence

Before the sweep, state the current work-item goal and collect each candidate's source:

- **Explicit commitment** — the user or conversation explicitly asked for work outside the goal.
- **Verified fact** — a repository, platform, or runtime fact confirmed by a source appropriate to the claim.
- **Fallback or inference** — a default rule, missing declaration, or agent interpretation.

Only explicit commitments and verified facts may become signals. A fallback or inference may constrain the current work, but MUST NOT become a signal until its condition is independently verified.

For repository visibility, use this evidence order: repository metadata, authenticated repository-host result, explicit user statement, then unknown. A missing metadata file invokes the content-classification safety fallback; it does **not** establish that the repository is public and does not by itself justify a metadata, split, or disclosure-audit signal.

Reconcile every candidate against the work item's latest scope before writing: discard it when it is now in scope or completed, and route it when an existing plan owns it.

### 1. Sweep

Ask the conversation all eight questions. NEVER substitute impression for the list:

1. What did the conversation state should happen that is **not** this issue?
2. What knowledge produced here has an **authority document elsewhere** it now contradicts or extends?
3. Which changed artifacts have **path-parallel peers** in another repository?
4. What was decided in conversation and **written to no file**?
5. What references a **path outside this workspace**?
6. What subject did the conversation **open but not develop**?
7. Where did a term, framing or explanation **land wrong and get corrected** — and what formulation would have avoided it?
8. Which prompt, agent, instruction, skill, snippet, template, hook or context file **governed this conversation**, and did it fully meet its own declared goal?

### 2. Classify the kind

| Kind | What it is | Typical landing |
|---|---|---|
| `upstream-feedback` | knowledge produced here belongs to an authority document elsewhere | a vision-amendment plan in the target |
| `propagation-debt` | a change here must be replicated in a peer repository | `pe-align-artifacts-across-repositories` |
| `divergent-commitment` | a decision implying work in another component or repository | an existing plan, or a new one |
| `investigation-lead` | a subject opened but not developed, deserving development rather than a decision | learning-hub content |
| `expression-defect` | a term or framing that landed wrong, with the analysis and better formulation | writing and terminology guidance |
| `artifact-defect` | a governing prompt-engineering artifact of any type underperformed its declared goal, or declared a goal that did not match what was wanted | `pe-review-execution-and-improve-artifacts` |

### 3. Resolve the existing landing FIRST

MUST search for a plan, work item or backlog entry that already covers the goal **before** naming a new landing. A signal that duplicates tracked work is a worse failure than a lost one. When one is found, record it and set `state: routed → <landing>`; the signal then carries no priority, because its ordering lives in the plan that owns it.

MUST NOT convert a generic instruction prerequisite, a conservative safety fallback, or a speculative compliance concern into `upstream-feedback`. It becomes a signal only when the conversation establishes a concrete mismatch and the target authority is known.

MUST NOT convert a hypothetical improvement, a stylistic preference, or an artifact the conversation merely consulted into `artifact-defect`. It becomes a signal only when the conversation shows an **actual execution shortfall** — a correction turn where the developer supplied what the artifact should have produced, or an artifact that reported success over an incomplete outcome. An artifact that met its declared goal is not a defect, however improvable it looks.

### 4. Write the record

| Field | Purpose |
|---|---|
| `kind` | one of the six above |
| `goal` | the one outcome the activity exists to achieve |
| `scope` | what it covers, tight enough to size it |
| `why it matters` | the cost of not doing it |
| `target` | repository and, where known, the artifact or stream |
| `existing landing` | a resolved reference, or an explicit "none found" |
| `state` | `pending` \| `routed → <landing>` \| `closed: <reason>` |
| `relevance` | `high` \| `medium` \| `low` |
| `actionability` | `ready` \| `bounded` \| `open` |
| `actionability strategy` | how the goal becomes executable **where it lands** |

- MUST NOT carry execution steps — steps belong to the context that executes them, and would be authored against a context that no longer holds.
- MUST be **self-contained**. Delivery is manual: a person will read this record in another repository, without the conversation and without this work item. Everything needed to start MUST be in the record.

### 5. Derive the priority

**Relevance** — what it costs to leave undone: `high` outdates or contradicts an authority document, or blocks other work · `medium` improves an artifact or content already in use · `low` opportunistic, nothing degrades while it waits.

**Actionability** — readiness where it lands: `ready` landing resolved and work list derivable without judgement · `bounded` landing known, scope still to be shaped · `open` landing unknown or scope undefined.

MUST order by **relevance descending**, then actionability descending, then identifier ascending. NEVER assign an order by impression. Identifiers are identity, not sequence — order is the listing position.

**`artifact-defect` takes precedence over every other kind** and sorts ahead of the relevance ordering above, so PE-improvement signals lead the primary page. The rationale is compounding: every other signal describes work that is waiting, while an artifact defect describes a generator that keeps producing defective work on every future run until it is repaired. Within `artifact-defect`, the standard relevance-then-actionability ordering applies.

### 6. Place the record

| Page | Holds |
|---|---|
| `<NN>-signals.md` | relevance is `high` **OR** actionability is `ready` or `bounded` |
| `<NN>-other-signals.md` | everything else — the less defined remainder |

Both live in the work-item folder, take the next free ordinals, and carry `publish: false`. Relevance alone earns the primary page: a high-relevance signal is never demoted for being unshaped, and a less relevant one still earns its place there if it is actionable.

### 7. Report

MUST state what was captured and on which page, or explicitly state that the sweep found none. Silence is indistinguishable from not having run.

## Delivery

The capturing repository **identifies and defines only**. It NEVER writes into the target repository. Carrying a signal to its landing is manual, performed by a person working in the target; that person updates `state` in the source record afterwards, which is what stops a later sweep from rediscovering an answered question.

## References

- **📖** [plan-execution.instructions.md](../../instructions/plan-execution.instructions.md) — § Park Lot bars divergent items; Gate check 6 routes them here
- **📖** [05.11-plan-authoring-discipline.md](../../../.copilot/context/00.00-prompt-engineering/05.11-plan-authoring-discipline.md) — the four buckets and the coverage-versus-continuity rationale
- **📖** [signals.template.md](../../templates/01.00-article-writing/signals.template.md) — the page shape
