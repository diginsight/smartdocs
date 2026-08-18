---
title: "Signal capture — giving divergent activities a durable, routable record"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [prompt-engineering, issue-analysis, governance, cross-repository]
description: "Adds a signal-capture mechanism to issue analysis and plan authoring, so activities that lie outside the current goal — divergent work, investigation leads and expression defects — survive the conversation instead of dying in its payload."
domain: "prompt-engineering"
goal: "Give conversation-surfaced signals — related or divergent activities outside the current goal, often targeting another repository — a durable record carrying goal, scope, target landing, actionability strategy and a derived priority, so they survive the conversation that produced them and are recorded in a defensible order"
scope:
  covers:
    - "The distinction between § Park lot (goal coverage) and signals (divergent continuity)"
    - "Where a signal is captured when a conversation is split into overview plus individual cases"
    - "Where a signal record lives within the work item that observed it"
    - "The signal taxonomy, including investigation leads and expression defects"
    - "The required shape of a signal record"
    - "Priority ordering derived from relevance and actionability, within a signals page"
    - "The sweep procedure that detects signals in a conversation"
    - "Recording a signal whose landing is another repository, with the context needed to carry it there by hand"
    - "Which prompt engineering artifacts must change"
  excludes:
    - "Executing the five captured signals themselves — each routes to its own landing"
    - "Automating delivery into a target repository — carrying a signal across is performed by hand"
    - "Sequencing signals across work items — ordering is derived per signals page"
    - "Content classification and the split procedure (plan 01)"
    - "Metadata governance for the plan artifact type (parked in plan 01)"
    - "Implementing the self-updating engine's Detect/Assess/Propose loop"
boundaries:
  - "MUST NOT overload § Park lot with divergent items — the park lot guarantees coverage of the current goal, and diluting it destroys that guarantee"
  - "MUST NOT let a signal record carry execution steps — steps belong to the context that executes them, not the one that observed them"
  - "MUST NOT record a signal without either a named landing or an explicit unknown-landing state"
  - "MUST derive priority from the declared relevance and actionability axes — never assign it by impression"
  - "MUST check for an existing landing before creating a new one — a captured signal that duplicates tracked work is worse than a lost one"
  - "MUST NOT place a signal record inside a plan file — plans are terminal and signals outlive them"
  - "MUST NOT write into the target repository at capture time — the capturing repository identifies and defines; carrying the signal across is a manual act performed in the target"
  - "MUST make a cross-repository record self-contained — it MUST carry enough context to be acted on without the conversation or the source work item"
  - "NEVER silently drop a conversation statement that implies work outside the current goal"
rationales:
  - "A conversation is the only place some knowledge exists; if none of the durable outputs has a slot for it, it is lost when the payload rolls over"
  - "The park lot is bounded by the plan's goal, so it structurally cannot hold items that were never in scope"
  - "Invariant intent (goal, scope, why) is durable; steps are context-specific and expire — the same split dual metadata applies to artifacts, applied here to work items"
  - "An unordered signal list defers the question of what to do first to whoever reads it last, which is how a capture mechanism decays into a backlog nobody drains"
  - "The record stays where it was observed, so there is exactly one authoritative copy; copying it into the target would create a second one and no way to tell which is current"
  - "A record carried across repositories by hand is only as good as its self-containment, so completeness of context is the capturing repository's whole responsibility"
  - "Propagation debt between path-parallel repositories is mechanically detectable, so it should never depend on someone remembering"
status: in-progress
---

# Signal capture — giving divergent activities a durable, routable record

## 🎯 Goal

Conversations surface work that is **not** the issue being analysed and **not** an expansion of it — knowledge that belongs upstream, changes that must be replicated in a peer repository, decisions about entirely different components, subjects worth developing rather than deciding, and defects in how something was expressed. Today none of the durable outputs has a slot for these, so they are lost with the conversation payload.

This plan gives them one: a **signal** record carrying goal, scope, why it matters, its target landing, the strategy that makes it actionable, and a **priority derived from relevance and actionability** — captured at the moment the conversation is split into an overview plus individual cases.

## 🧭 Why this plan exists

Three signals emerged in the originating conversation and none had a home:

1. New prompt engineering rules and rationales — public/private information handling, the metadata contracts, skills as a reach mechanism — were **not integrated back** into the learning hub's article-writing and content-management content.
2. Changes made to prompt engineering artifacts here were **not reported** to the peer artifacts in the learning repository.
3. `Learn.Web` **should be dismissed** — stated in an earlier interaction, unconnected to this work item.

Each was stated with enough substance to act on. Each was lost, because the only durable outputs of an issue conversation are the analysis pages and the plans they spawn, and neither has a place for work that was never in scope.

Examining the concept surfaced two further kinds that behave the same way and are lost by the same mechanism: **subjects worth developing rather than deciding** — a topic the conversation opened that deserves integration into the learning hub — and **expression defects** — a term, framing or explanation that landed wrong and was corrected, where the correction itself is the durable value.

## 🔍 Evidence gathered

### Finding 1 — the park lot is bounded by the goal, by definition

[plan-execution.instructions.md](../../../../../.github/instructions/plan-execution.instructions.md) defines § Park lot as *"edge cases surfaced during authoring or execution that are **out of scope for the current plan**"*, and Actionability Gate check 6 feeds it: *"no item exceeds the verbatim trigger; **expansions** → § Park lot"*.

Both phrasings presuppose the item is an **expansion of this goal** — something in-domain, deliberately excluded to protect scope. That is precisely what makes the park lot a **coverage guarantee**: it proves nothing in-domain was dropped silently.

A divergent item was never in scope, so parking it does not protect coverage — it dilutes the guarantee by mixing "excluded from this goal" with "unrelated to this goal". The two need separate sections because they answer different questions.

### Finding 2 — the disposition vocabulary cannot express a cross-repository landing

Park lot dispositions are `→ <sibling-plan-id>.md`, `→ defer`, `→ closed: <reason>`. All three assume the item remains in **this repository's plan lineage**. Two of the three captured signals land in `Learn.01`, which no disposition can name.

### Finding 3 — the conversation-to-analysis split has no slot for divergent material

[issue-generate-analysis-from-current-conversation.prompt.md](../../../../../.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md) step 3 produces `01-overview.md` plus one page per incident or theme, then a standing reference page. Step 4 fills `RESOLUTION STATUS → follow-up actions` and `LESSONS LEARNED`.

Every one of those slots is **about the incident**. The prompt asks the conversation what happened; it never asks what else the conversation revealed. The split is lossy by construction, and nothing reports the loss.

### Finding 4 — a captured signal can duplicate work that is already tracked

Signal 3 is **not** actually lost. [WS-J-retirement](../20260815.01-smartdocs-firstimpl/01-smartdocs-web-convergence.plan.md) is `🟡 todo` in the convergence plan and already removes `src/Learn.Web/`, `src/Learn.Web.Client/` and `src/Learn.Web.Shared/` from `Learn.01`, gated on the validation exit criterion.

It was invisible **from this work item**, not untracked. So the signal record must resolve an existing landing before proposing a new one — otherwise capture manufactures duplicate work, which is a worse failure than losing the signal.

### Finding 5 — one signal class is mechanically detectable

Propagation debt (signal 2) does not need human recall. Both repositories carry path-parallel `.github/instructions/`, `.github/prompts/` and `.copilot/context/` trees. Any artifact changed here that has a peer there is detectable by comparison. This is the strongest automation candidate and should not be left to a judgement sweep.

### Finding 6 — the analysis folder's own overview was never written

`overview.md` in this work item is **empty**, and it is named `overview.md` rather than the `01-overview.md` the prompt specifies. The mechanism that was supposed to hold the connective tissue between cases did not run at all — which is why the split never had an opportunity to surface signals.

## 📡 Signals captured from the originating conversation

The five signals that motivated this plan now live in the shape it proposes, on the pages it defines — [03-signals.md](03-signals.md) for `SIG-2`, `SIG-1`, `SIG-5` and `SIG-3`, and [04-other-signals.md](04-other-signals.md) for `SIG-4`.

They are deliberately **not** duplicated here. A plan is terminal and a signal outlives it; a copy in this file would be a second authority with no way to tell which is current. (✅ done)

## 🧱 Proposed target architecture

### The two buckets, and what each guarantees

| | § Park lot | Signals |
|---|---|---|
| Relationship to the goal | in-domain, deliberately excluded | never in scope |
| What it guarantees | **coverage** — nothing in-domain was dropped | **continuity** — nothing out-of-domain was dropped |
| Produced by | Gate check 6, scope discipline | a sweep of the conversation, not of the plan |
| Where it lives | inside the plan file | **outside every plan** — a page in the work-item folder |
| Lifetime | dies with the plan or migrates into it | outlives every plan in the work item |
| Disposition | `→ sibling-plan` \| `→ defer` \| `→ closed` | a `target` **and** a `state` — `pending`, `routed → <landing>`, or `closed: <reason>` |

Keeping signals out of plan files resolves the terminal-state conflict directly: a `done` plan must not gain or lose items, and a signal captured after that plan closed would force exactly that.

### The kinds

The kind determines where a signal routes, so it is declared, not inferred:

| Kind | What it is | Typical landing |
|---|---|---|
| `upstream-feedback` | knowledge produced here belongs to an authority document elsewhere | a vision-amendment plan in the target repository |
| `propagation-debt` | a change made here must be replicated in a peer repository | a comparison-derived work list |
| `divergent-commitment` | a decision implying work in another component or repository | an existing plan, or a new one |
| `investigation-lead` | a subject the conversation opened that deserves development rather than a decision | learning hub content |
| `expression-defect` | a term, framing or explanation that landed wrong, with the analysis and the better formulation | writing and terminology guidance |

`expression-defect` is the one kind whose value is carried **in** the record: the analysis and the suggested formulation are the deliverable. Whether such a record is published follows the classification rules in plan 01 like any other content.

### The signal record

Every signal carries the **invariant** part of a work item and none of the mutable part:

| Field | Purpose |
|---|---|
| `kind` | one of the five above — determines routing |
| `goal` | the one outcome the activity exists to achieve |
| `scope` | what it covers, stated tightly enough to size it |
| `why it matters` | the cost of not doing it |
| `target` | repository and, where known, the artifact or stream |
| `existing landing` | a resolved reference, or an explicit "none found" |
| `state` | `pending` \| `routed → <landing>` \| `closed: <reason>` — updated by whoever carries the signal to its landing |
| `relevance` | `high` \| `medium` \| `low` — see below |
| `actionability` | `ready` \| `bounded` \| `open` — see below |
| `actionability strategy` | how the goal becomes executable **where it lands** — never steps authored here |

Steps are deliberately absent. A signal executed months later in another repository would find steps authored against a context that no longer holds. This mirrors the invariant/mutable split that dual metadata applies to artifacts.

`state` is the one mutable field. It exists so a later sweep does not rediscover a question that was already answered — see § Delivery below.

### Where a signal lives

Signals are pages in the work-item folder, alongside the analysis pages and the plans:

| Page | Holds |
|---|---|
| `NN-signals.md` | signals that are relevant **or** actionable |
| `NN-other-signals.md` | everything else — the less defined remainder |

The split is derived from the same two axes, so nobody has to judge it:

> A signal belongs on `NN-signals.md` when its relevance is `high` **or** its actionability is `ready` or `bounded`. Otherwise it belongs on `NN-other-signals.md`.

A high-relevance signal is never demoted for being unshaped — relevance alone earns the primary page. A signal already `routed` stays on the primary page carrying its landing, so a later sweep does not rediscover a question that was already answered.

There is no repository-level register. The work-item folder is the unit, because the folder is what carries the context that makes each signal intelligible.

### Delivery — identification here, routing by hand

The capturing repository is responsible for **identification and definition only**. It never writes into the target repository, and no mechanism moves a record across. Carrying a signal to its landing is a manual act, performed by a person working in the target.

That division has one consequence, and it is the demanding one: **the record must stand alone.** A person opening `03-signals.md` months later, in a different repository, with no access to the conversation and no interest in the work item that produced it, must be able to act on the record as written. Every field exists to make that true — `goal` and `scope` say what the work is, `why it matters` says why it is worth starting, `target` says where it belongs, `existing landing` says whether it is already being done, and `actionability strategy` says how it becomes executable there.

When the signal has been carried across, whoever carried it updates `state` in the source record to `routed → <landing>` or `closed: <reason>`. That write-back is the only maintenance the record ever needs, and it is what stops a later sweep from rediscovering an answered question.

### Priority

Priority is **derived** from two declared axes, so two readers ordering the same signals reach the same order.

**Relevance** — what it costs to leave it undone:

| Value | Condition |
|---|---|
| `high` | it outdates or contradicts an authority document, or blocks other work |
| `medium` | it improves an artifact or content already in use |
| `low` | opportunistic — nothing degrades while it waits |

**Actionability** — how ready it is to be executed where it lands:

| Value | Condition |
|---|---|
| `ready` | landing resolved and the work list derivable without judgement |
| `bounded` | landing known, scope still to be shaped |
| `open` | landing unknown, or scope undefined |

Order by **relevance descending**, then actionability descending, then identifier ascending. A signal that resolves to an existing landing carries no priority — its ordering already lives in the plan that owns it.

The two axes are deliberately independent: relevance alone promotes work nobody can start, and actionability alone promotes whatever happens to be easy. That independence is also what earns a less relevant signal a place on the primary page — if it is actionable, it is worth listing below the relevant ones rather than exiling it to the remainder.

Ordering is derived **within a signals page**. Sequencing across work items is out of scope — see § Park lot.

### The sweep

Detection is a defined set of questions asked of the conversation before the split, not an invitation to notice things:

1. What did the conversation state should happen that is **not** this issue?
2. What knowledge was produced here that has an **authority document elsewhere** it now contradicts or extends?
3. Which changed artifacts have **path-parallel peers** in another repository? *(mechanical — Finding 5)*
4. What was decided in conversation and **written to no file**?
5. What references a **path outside this workspace**?
6. What subject did the conversation **open but not develop**, that belongs in the learning hub rather than in a decision?
7. Where did a term, framing or explanation **land wrong and get corrected** — and what is the formulation that would have avoided it?

The sweep fires deterministically at two moments: when a conversation becomes an issue analysis, and when an author routes an item away from § Park lot. Everywhere else — an ordinary review, a refactoring exchange — it fires only if the skill is discovered. That residue is accepted rather than hidden: both deterministic moments were present in the conversation that lost the original three signals, so the mechanism would have caught them.

### Artifacts that change

| Artifact | Change |
|---|---|
| `plan-execution.instructions.md` | bar divergent items from § Park lot and route them to the work item's signals page |
| `05.11-plan-authoring-discipline.md` | the coverage-versus-continuity rationale, and why signals live outside the plan |
| new skill — `signal-capture` | the kinds, the record shape, the priority derivation, the sweep and the page split |
| new template — `signals.template.md` | the page shape, so a signals page is well-formed at creation |
| `issue-generate-analysis-from-current-conversation.prompt.md` | run the sweep before the split; add both signal pages to the page table; report what was captured |

The reach argument from plan 01 applies unchanged: wiring the sweep into one prompt reaches one prompt. A skill is what reaches the artifacts nobody wired.

`issue.template.md` is deliberately **not** in this list. Signals live outside the analysis pages, so the incident template has nothing to gain. No inbound-processing prompt appears either: delivery is manual, so the consumer is a person, and what that person needs is a self-contained record — not another artifact.

## ❓ Open decisions

All four are resolved. D1–D3 were answered on 2026-08-18 and are folded into § Proposed target architecture; D4 was raised by resolving them and answered the same day.

| | Question | Resolution |
|---|---|---|
| D1 | Where does a signal record live? | A page per work item for relevant or actionable signals, plus an `other-signals` page for the less defined remainder. No repository-level register. |
| D2 | How is a cross-repository signal delivered? | **By hand.** The capturing repository identifies and defines; a person carries the record to its landing and writes `state` back. |
| D3 | Does a `done` plan still carry its signals? | The question dissolves — signals live outside plan files entirely, so the terminal rule is never engaged. |
| D4 | Does "worked in a defensible order" reach across work items? | **No — the goal is narrowed to *recorded* in a defensible order.** Ordering is derived within a signals page; cross-work-item sequencing leaves scope and is parked. |

D4 exists because the original goal promised more than the body delivered. D1 removed the register, so no artifact compares a signal in one work item against one in another, and D2's manual delivery reads no priority field at all. Rather than build an index and a priority-aware consumer to match the wording, the wording was corrected: this plan guarantees that every signal is **captured, defined and ordered on its page**. Which signal a person picks up next across work items is a judgement this plan does not automate, and does not pretend to.

## 🔭 Discovery

- Whether `Learn.01` carries the same issue-analysis prompt and plan-execution instruction, and therefore needs the same amendment → if absent, the change is single-repository and `SIG-2` shrinks accordingly.
- Whether the empty `overview.md` in this work item indicates the analysis prompt was never run here, or ran and failed → if it never ran, Finding 6 is an adoption gap rather than a prompt defect, and the fix differs.
- Whether any existing plan's § Park lot already holds divergent items that should migrate to a signals page → **if none is found, step 1.4 closes with no migration** and the plan proceeds unchanged.

## 🛠️ Plan

### Step 1 — Separate the two buckets in the plan artifacts (✅ done)

1. In [plan-execution.instructions.md](../../../../../.github/instructions/plan-execution.instructions.md) § Park Lot, add: *"§ Park lot MUST hold only items that were in-domain for this goal and deliberately excluded. An item that was never in scope is a **signal** and MUST be routed to the work item's signals page, NEVER parked."*
2. In the same file, extend Gate check 6 to read `expansions → § Park lot; divergent items → the work item's signals page`.
3. In [05.11-plan-authoring-discipline.md](../../../../../.copilot/context/00.00-prompt-engineering/05.11-plan-authoring-discipline.md) § *Open decisions, discovery, and park lot*, retitle to four buckets and add a `Signals` row whose *Where it lives* cell reads **outside the plan file**; below the table add the coverage-versus-continuity paragraph from § Proposed target architecture.
4. Read the § Park lot of every `*.plan.md` under `src/docs/90.00-issues/` and list items that were never in scope for their plan's goal; per § Discovery, an empty list closes this item. (✅ done) — three found, all in the convergence plan, all landing in `diginsight/tools`: `PL-2`, `PL-3`, `PL-7`. Migrated to [02-signals.md](../20260815.01-smartdocs-firstimpl/02-signals.md) as `SIG-C`, `SIG-B`, `SIG-A`; the `D12` back-reference to `PL-3` was repointed. Plan 01's five entries are all in-domain — no migration.
5. Bump `instruction_metadata.version` to `1.6.0` and `context_metadata.version` by one minor, with `last_updated: "2026-08-18"` on both. (✅ done) — `1.6.0` and `1.1.0` respectively.

### Step 2 — Author the `signal-capture` skill (✅ done)

Create `.github/skills/signal-capture/SKILL.md` under [pe-skills.instructions.md](../../../../../.github/instructions/pe-skills.instructions.md), carrying, verbatim from § Proposed target architecture: the five-kind table; the nine-field record shape; the relevance and actionability tables; the ordering rule; the seven-question sweep; the page-split rule; and the existing-landing check stated as a precondition to naming any new landing.

The `description:` frontmatter MUST name the triggering situations — splitting a conversation into an issue analysis, closing a work item, and routing an item away from § Park lot — because discovery is the only thing that reaches an unwired artifact.

### Step 3 — Author the signals page template (✅ done)

Create `.github/templates/01.00-article-writing/signals.template.md` under [pe-templates.instructions.md](../../../../../.github/instructions/pe-templates.instructions.md): renderer frontmatter with `publish: false`, the priority-ordered summary table header, one fully commented record block covering all nine fields, and the `state` vocabulary with its transitions. A sibling `signals.template.changelog.md` accompanies it, per the convention every other template in that folder follows.

### Step 4 — Wire the sweep into the analysis prompt (✅ done)

In [issue-generate-analysis-from-current-conversation.prompt.md](../../../../../.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md):

1. Insert a step between the current steps 2 and 3 that runs the seven-question sweep against the conversation **before** the split, delegating the record shape to the `signal-capture` skill.
2. Add `NN-signals.md` and `NN-other-signals.md` to the step 3 page table, taking the next free ordinals after the case pages and before the standing reference page, each created from `signals.template.md` and omitted when the sweep returns nothing.
3. Add to the step 5 quality checks: every captured record declares `kind`, `relevance`, `actionability`, `target` and `state`, and carries no execution steps.
4. Add to the step 6 report: the signals captured per page, or the explicit statement that the sweep found none.
5. Bump `prompt_metadata.version` to `1.1.0` with `last_updated: "2026-08-18"`.

### Step 5 — Migrate this work item's own signals (✅ done)

1. Create `03-signals.md` from the template holding `SIG-2`, `SIG-1`, `SIG-5` and `SIG-3`, in that order, with `SIG-3` written as `state: routed → WS-J-retirement`.
2. Create `04-other-signals.md` holding `SIG-4`, which is `medium` / `open` and therefore fails the primary-page rule.
3. Replace this plan's § Signals captured with a two-line pointer to both pages, so the records exist in exactly one place.

This step is the plan's own acceptance test: if the five signals cannot be expressed in the shape steps 2–3 define, the shape is wrong. It passed — all five fitted the nine-field record, and the three migrated park-lot items fitted it too, which the shape was not designed against.

## 🅿️ Park lot

- **Executing `SIG-1`, `SIG-2`, `SIG-4` and `SIG-5`** → `→ sibling plan` — this plan builds the mechanism; the signals route to their own landings once it exists.
- **Automating the propagation-debt comparison** (Finding 5) → `→ defer` — mechanically detectable, but tooling is a separate concern from capture.
- **Whether signals should feed the self-updating engine's Detect phase directly** → `→ defer` — the engine's integration seam is out of scope until the record shape is settled.
- **Metadata governance for the plan artifact type** → `→ sibling plan` — already parked in plan 01; noted here so the two plans do not both claim it.
- **What triggers a signal being carried to its landing** → `→ defer` — D2 makes delivery manual, so a signal is worked only when someone picks it up. Capture, definition and ordering are guaranteed by this plan; *initiation* is not.
- **Sequencing signals across work items** → `→ defer` — D4 narrows the goal to ordering within a page. A derived cross-page index would answer "what is next" globally, but it reverses part of D1 and is worth building only once several work items carry signals pages.
- **This work item's empty, non-conforming `overview.md`** (Finding 6) → `→ defer` — the finding explains why the split never surfaced signals; repairing the page is an adoption fix, and renaming it collides with the existing `01-` plan.

## 📌 Exit criteria

This plan is `done` when:

- an issue analysis produced from a conversation reports its captured signals, or explicitly reports that none were found,
- every captured signal declares a kind, a relevance and an actionability, and both the listing order and the page it landed on follow from them rather than from judgement,
- a signal that duplicates already-tracked work resolves to the existing landing instead of creating a second one,
- a signal targeting another repository names that repository and reads as a complete work item on its own — nothing in the record depends on the conversation or the work item that observed it,
- § Park lot contains only in-domain exclusions, so its coverage guarantee holds again,
- and the five signals above live on `03-signals.md` and `04-other-signals.md`, with `SIG-3` carrying `routed → WS-J-retirement` rather than new work.

## 📚 References

- **📘** [plan-execution.instructions.md](../../../../../.github/instructions/plan-execution.instructions.md) — park lot definition and Actionability Gate check 6
- **📘** [issue-generate-analysis-from-current-conversation.prompt.md](../../../../../.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md) — the conversation-to-analysis split
- **📘** [01-content-management-artifacts-improvement.plan.md](01-content-management-artifacts-improvement.plan.md) — the reach argument and the sibling park-lot entries
- **📘** [01-smartdocs-web-convergence.plan.md](../20260815.01-smartdocs-firstimpl/01-smartdocs-web-convergence.plan.md) — `WS-J-retirement`, the existing landing for `SIG-3`

<!--
plan_metadata:
  version: "1.2.0"
  created: "2026-08-18"
  last_updated: "2026-08-18"
-->
