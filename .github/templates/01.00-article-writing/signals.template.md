---
# Frontmatter metadata
title: "Signals — [work item name]"
author: "Your Name"
date: "YYYY-MM-DD"
categories: [signals, prompt-engineering]
description: "Activities surfaced by the [work item name] conversation that were never in scope for its goals."
publish: false
---

# Signals — [work item name]

Activities this work item's conversations surfaced that were **never in scope** for any of its goals. Each record is self-contained: delivery is manual, so a person reading it in another repository — without this conversation and without this work item — must be able to act on it as written.

**Identifiers are identity; the listing order is priority** — derived from relevance and actionability, never assigned by impression.

📖 Record shape, kinds, sweep and priority derivation: `.github/skills/signal-capture/SKILL.md`

<!-- Replace the backticked path above with a relative link from the instantiated page's own
     location. From a work-item folder five levels below the repository root that is
     `[signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)`. -->

## 📡 Signals

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-1` | [kind] | high | ready | [repository] | none found | `pending` |
| — | `SIG-2` | [kind] | — | — | [repository] | [landing id] | `routed → [landing id]` |

<!-- Ordering rule: `artifact-defect` first, then relevance descending, then actionability descending,
     then identifier ascending. `artifact-defect` leads because every other kind describes work that is
     waiting, while an artifact defect describes a generator that keeps producing defective work on
     every future run until it is repaired.
     A signal that resolves to an existing landing carries no priority — its ordering lives in the
     plan that owns it. Show it last with `—` in the priority columns.

     This page holds signals whose relevance is `high` OR whose actionability is `ready` or
     `bounded`. Everything else belongs on the sibling `<NN>-other-signals.md`. -->

### `SIG-1` — [one-line statement of the activity]

<!-- Copy this block per record. All nine fields are required. NEVER add execution steps:
     they belong to the context that executes the signal, not the one that observed it. -->

- **Kind** — one of `upstream-feedback` | `propagation-debt` | `divergent-commitment` | `investigation-lead` | `expression-defect` | `artifact-defect`.
- **Goal** — the one outcome this activity exists to achieve.
- **Scope** — what it covers, stated tightly enough to size it.
- **Why it matters** — the cost of not doing it.
- **Target** — repository, and where known the artifact or stream inside it.
- **Existing landing** — a resolved reference, or an explicit "none found". Resolve this **before** naming a new landing.
- **State** — `pending` | `routed → <landing>` | `closed: <reason>`. Updated by whoever carries the signal across.
- **Relevance** — `high` outdates or contradicts an authority document, or blocks other work | `medium` improves an artifact or content already in use | `low` opportunistic, nothing degrades while it waits.
- **Actionability** — `ready` landing resolved and work list derivable without judgement | `bounded` landing known, scope still to be shaped | `open` landing unknown or scope undefined.
- **Actionability strategy** — how the goal becomes executable **where it lands**.

<!-- For `expression-defect`, add two fields — the record IS the deliverable:
- **The mistake** — what was said, and why the framing was wrong.
- **Better expression** — the formulation that would have avoided it. -->
