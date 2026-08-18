---
title: "Other signals — docmanager improvement"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [signals, prompt-engineering]
description: "Signals from the docmanager-improvement conversations that are neither highly relevant nor yet actionable — kept so they are not lost, not promoted until they are shaped."
publish: false
---

# Other signals — docmanager improvement

The less defined remainder: signals whose relevance is not `high` **and** whose actionability is `open`. They are recorded so they survive, not because they are ready.

A record is promoted to [03-signals.md](03-signals.md) the moment either axis changes — when its landing is resolved, or when something makes it urgent.

📖 Record shape, kinds, sweep and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-4` | `investigation-lead` | medium | open | `darioairoldi/Learn` | none found | `pending` |

### `SIG-4` — what makes a document a prompt engineering artifact

- **Kind** — `investigation-lead`
- **Goal** — develop, in the learning hub, the criterion that decides whether a document type is a prompt engineering artifact and therefore carries the metadata contract.
- **Scope** — the question surfaced when plan files were found to be consumed by language models on every load, yet absent from the metadata contract's artifact enumeration and type table. The same question applies to vision documents, use-case documents, changelogs and validation sequences — each currently handled by a bespoke inclusion or a bespoke exemption, none derived from a stated rule.
- **Why it matters** — every omission so far was discovered the same way: something breaks, and the enumeration turns out never to have been derived from a criterion. Patching the instance leaves the next omission undiscovered until it too breaks. A stated criterion closes the class instead.
- **Target** — `Learn.01/06.00-idea/05.01-self-updating-prompt-engineering/`.
- **Existing landing** — none found.
- **State** — `pending`
- **Relevance** — `medium` — nothing degrades while it waits, but each instance patched without it adds to the debt.
- **Actionability** — `open` — the landing folder is known; the destination document is not, and the criterion itself has not been articulated.
- **Actionability strategy** — a subject development in the learning hub, **not** a rule change. The rule follows once the criterion exists; writing the rule first would produce another instance patch wearing a general name.
- **Relationship to plan 01** — [01-content-management-artifacts-improvement.plan.md](01-content-management-artifacts-improvement.plan.md) parks the concrete fix for the plan artifact type. This signal is the general criterion behind it. The two MUST NOT be merged, or the criterion collapses back into the instance it came from.
