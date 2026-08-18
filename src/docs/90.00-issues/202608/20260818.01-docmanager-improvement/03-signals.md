---
title: "Signals — docmanager improvement"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [signals, prompt-engineering, cross-repository]
description: "Activities surfaced by the docmanager-improvement conversations that were never in scope for their goals — upstream feedback, propagation debt, two expression defects, and divergent commitments in this repository and elsewhere."
publish: false
---

# Signals — docmanager improvement

Activities this work item's conversations surfaced that were **never in scope** for any of its goals. Each record is self-contained: delivery is manual, so a person reading it in another repository — without this conversation and without this work item — must be able to act on it as written.

**Identifiers are identity; the listing order is priority** — derived from relevance and actionability, never assigned by impression.

📖 Record shape, kinds, sweep and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-2` | `propagation-debt` | high | ready | `darioairoldi/Learn` | none found | `pending` |
| 2 | `SIG-1` | `upstream-feedback` | high | bounded | `darioairoldi/Learn` | none found | `pending` |
| 3 | `SIG-6` | `expression-defect` | high | bounded | this repository | none found | `pending` |
| 4 | `SIG-5` | `expression-defect` | medium | bounded | this repository | none found | `pending` |
| 5 | `SIG-7` | `divergent-commitment` | medium | bounded | this repository | none found | `pending` |
| — | `SIG-3` | `divergent-commitment` | — | — | `darioairoldi/Learn` | `WS-J-retirement` | `routed → WS-J-retirement` |

`SIG-4` is `medium` / `open` and therefore lives on [04-other-signals.md](04-other-signals.md).

`SIG-6` and `SIG-7` were captured by the sweep run while producing [00-overview.md](00-overview.md); the other four were migrated from the conversation that produced this work item's plans.

### `SIG-2` — artifact changes were never reported to the peer repository

- **Kind** — `propagation-debt`
- **Goal** — bring the learning repository's prompt engineering artifacts into line with the changes made in `diginsight/smartdocs`.
- **Scope** — every artifact modified in this work item that has a path-parallel peer: `.github/instructions/`, `.github/prompts/`, `.github/skills/`, `.github/templates/` and `.copilot/context/`.
- **Why it matters** — the two artifact sets are meant to be peers. Silent divergence means a rule enforced in one repository is absent in the other, and neither reports it. The divergence compounds: each further change in either repository widens it, and nothing measures the gap.
- **Target** — `Learn.01/.github/` and `Learn.01/.copilot/context/`.
- **Existing landing** — none found.
- **State** — `pending`
- **Relevance** — `high` — a governance rule present in one repository and absent in its peer is a rule that does not hold.
- **Actionability** — `ready`
- **Actionability strategy** — compare the two artifact trees path by path; the differences **are** the work list, so no judgement is needed to build it. That is what makes this `ready` rather than `bounded`. Known changes in this work item at time of capture: `plan-execution.instructions.md` (park lot bars divergent items; Gate check 6 extended), `05.11-plan-authoring-discipline.md` (four buckets), the new `signal-capture` skill, the new `signals.template.md`, and `issue-generate-analysis-from-current-conversation.prompt.md` (sweep step, signal pages, QA and report).

### `SIG-1` — new rules and rationales never flowed back to the learning hub

- **Kind** — `upstream-feedback`
- **Goal** — reflect the rules and rationales invented during this work into the learning hub content that governs article writing and content management.
- **Scope** — three bodies of thinking: public/private information handling and the four content principles (`.internal.md` suffix plus gitignore; the two-repository path-parallel mirror; alias-first, public by construction; the internal companion as the complete document); the metadata contracts and the invariant/mutable split that underlies dual metadata; and skills as a reach mechanism for procedures nobody wired by hand.
- **Why it matters** — the vision documents are the authority these artifacts are supposed to derive from. Practice has moved ahead of the vision, so the next derivation from the vision would **regress** the improvement rather than build on it. The longer the gap persists, the more expensive the reconciliation.
- **Target** — `Learn.01/06.00-idea/05.01-self-updating-prompt-engineering/` and `Learn.01/06.00-idea/05.02-self-updating-article-writing/`.
- **Existing landing** — none found.
- **State** — `pending`
- **Relevance** — `high` — the authority documents are outdated by work already done.
- **Actionability** — `bounded`
- **Actionability strategy** — a vision-amendment plan in the target repository, governed by `vision-amendment.instructions.md`, **not** a direct rewrite of the vision documents. The landing folders are known but the amendment scope is unshaped, which is what makes this `bounded`. The amendment plan must carry per-item scope tags and principle-impact tagging, so the three bodies of thinking above are its natural item boundaries.

### `SIG-6` — goal satisfaction was confirmed by checklist coverage instead of reachability

- **Kind** — `expression-defect`
- **Goal** — make a gate that declares a goal satisfied test whether the goal is **reachable through the artifacts**, rather than whether every checklist item was addressed.
- **The mistake** — a plan was promoted to `actionable` after confirming that each item in its own step list had been carried out. The goal promised that captured signals would be *"worked in a defensible order"*; the delivery mechanism selected records on `target` and `state` and never read `relevance` or `actionability`, so a `high` / `ready` item could sit behind a `low` / `open` one with nothing reporting it. Every item was addressed and the goal was still unreachable. The promotion had to be reverted.
- **Why it matters** — checklist coverage is decidable and therefore comfortable; reachability is open-ended and can fail. A check that can only pass is not a check. The failure is worse than a missing gate, because the gate's presence certifies the thing it did not examine — and the certification is what a later reader trusts.
- **Better expression** — a gate MUST state the path from the goal to the artifacts that carry it, name the artifact where the path breaks, or state that it does not break. "All items complete" is evidence for that argument, never the argument itself.
- **Target** — the plan-authoring and gate guidance in this repository, and its learning-hub peer.
- **Existing landing** — none found. The Actionability Gate in `plan-execution.instructions.md` is the artifact this defeats; it has no rule requiring a reachability argument.
- **State** — `pending`
- **Relevance** — `high` — the gate that exists to prevent premature promotion is the gate this failure passes through.
- **Actionability** — `bounded`
- **Actionability strategy** — add a reachability obligation to the gate: before a plan may leave `draft`, its § Goal must be traceable to named artifacts, and the trace must be written down. The obligation is clear; which artifact carries it, and whether it also applies to exit criteria, is still to be shaped. Note that a failed trace is a **result**, not an error — the correct response may be to narrow the goal, which is what happened here.

### `SIG-5` — an implementation limitation was framed as a design constraint

- **Kind** — `expression-defect`
- **Goal** — record the misexpression and the formulation that avoids it, so the same framing error is not repeated in findings.
- **The mistake** — findings presented the folder-metadata parser's flat-scanner behaviour, and the content publication path, as reasons to **reject** folder `metadata.yml` as the home for the content-classification declaration. Both were current implementation artifacts — a parser that could be replaced and a path that could be changed — not intrinsic properties of the mechanism.
- **Why it matters** — framing a fixable limitation as an intrinsic constraint converts a defect into a rejected option. The reader inherits a wrong conclusion instead of a work item, and the better design is discarded silently, with no record that it was ever viable. This failure mode is invisible in review: the finding reads as sound reasoning.
- **Better expression** — state the observation, classify it explicitly as **intrinsic** or **an implementation artifact**, and only then draw the conclusion. An implementation artifact becomes an enabling work item; only an intrinsic property may rule an option out.
- **Target** — the findings and analysis writing guidance in this repository, and its learning-hub peer.
- **Existing landing** — none found.
- **State** — `pending`
- **Relevance** — `medium` — it improves guidance already in use rather than contradicting an authority document.
- **Actionability** — `bounded`
- **Actionability strategy** — a rule requiring every limitation cited **against** an option to be classified before it is used as an argument. The landing is the analysis-writing guidance; which artifact carries it is still to be shaped.

### `SIG-7` — nothing allocates the ordinal space of a work-item folder

- **Kind** — `divergent-commitment`
- **Goal** — define who allocates the `NN-` prefix inside a work-item folder, so the entry point can always take the first position.
- **Scope** — the issue-analysis prompt, which mandates `01-overview.md`; the plan naming rule, which mandates `<NN>-<kebab-name>.plan.md`; and the signals page rule, which places its pages between the case pages and the standing reference. Three rules, one namespace, no allocator.
- **Why it matters** — the rules are individually satisfiable and jointly not. In this folder the plans were created first and took `01-` and `02-`, so the analysis entry point could not take the position its own rule requires. The workaround is a deviation each time, and a deviation repeated is a convention nobody wrote down.
- **Better expression** — say explicitly whether plans, analysis pages and signals pages share one read order or occupy separate spaces. If they share it, one rule must allocate; if they do not, plans need a prefix that cannot collide.
- **Target** — this repository: `issue-generate-analysis-from-current-conversation.prompt.md` and `plan-execution.instructions.md`.
- **Existing landing** — none found. Plan 02 parked *this work item's non-conforming `overview.md`* as an adoption fix; that is the symptom, not the rule gap.
- **State** — `pending`
- **Relevance** — `medium` — it degrades navigability and forces a documented deviation; it does not make any rule wrong.
- **Actionability** — `bounded`
- **Actionability strategy** — a decision first, then a rule. The decision is whether the three artifact classes share one ordinal sequence. Only after it is taken does the rule shape follow, which is why this is `bounded` and not `ready`. Evidence available at capture: this folder, where `00-` was used for the entry point because `01-` was occupied.

### `SIG-3` — `Learn.Web` should be dismissed

- **Kind** — `divergent-commitment`
- **Goal** — retire the rendering application from the learning repository now that it has converged into `diginsight/smartdocs`.
- **Scope** — `src/Learn.Web/`, `src/Learn.Web.Client/`, `src/Learn.Web.Shared/` and their solution entries; the configuration files already moved to `smartdocs.internal`.
- **Why it matters** — two live copies of the same application is a divergence hazard, and the retirement is the last step that proves the convergence actually completed.
- **Target** — `Learn.01` and `Learn.internal`.
- **Existing landing** — `WS-J-retirement` in [01-smartdocs-web-convergence.plan.md](../20260815.01-smartdocs-firstimpl/01-smartdocs-web-convergence.plan.md), `🟡 todo`, gated on that plan's validation exit criterion.
- **State** — `routed → WS-J-retirement`
- **Relevance** — n/a — resolved to an existing landing, so it carries no priority: its ordering lives in the plan that owns it.
- **Actionability** — n/a
- **Actionability strategy** — none needed. This signal exists only so the same commitment is not captured a second time as new work.
