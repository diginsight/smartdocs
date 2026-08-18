---
title: "Article writing artifacts"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The family governing how hand-authored articles are written and reviewed."
source_sets:
  - invocation-surface
  - artifact-composition
  - artifact-bindings
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/pe-artifacts/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/_discovery.md"
      observed: "2026-08-18"
  open_gaps: 3
-->

# Article writing artifacts

- **Priority**: 🟡 Tooling ^[discovery]
- **Grouped by**: declared domain, falling back to containing folder ^[code-05]
- **Roots**: `.github/`, `.copilot/context/01.00-article-writing/` ^[code-11]
- **Members**: 44 — 3 agents, 6 context files, 3 instruction files, 7 prompts, 25 templates ^[code-11]

## 🎯 Purpose

The component registry derives this family's purpose as governing article authoring and review. ^[discovery] 25 of its 44 members are templates, more than every other kind combined. ^[code-11]

**Derived from**: declared domain metadata, with the containing folder used only where no domain is declared. ^[code-05]

## 🧭 Behaviour

The family declares three instruction files ^[code-11]; each declares an `applyTo` glob and is loaded automatically for any file that glob matches ^[code-20].

The legacy template folders at the `.github/templates/` root were confirmed as sub-groups of this family rather than families of their own. ^[code-08]

One of the four skill-scoped families declared below the main four is `article-review`, holding 4 artifacts. ^[code-13]

> **Not established**: what these artifacts do when invoked. Each was read as a declaration; no execution log, run record or produced-output artefact tying an invocation to a result exists in the repository. ^[gap]

## ▶️ Invocation surface

| Type | How it is reached |
|---|---|
| 3 agents ^[code-11] | named by the user ^[code-18] |
| 7 prompts ^[code-11] | slash commands ^[code-19] |
| 3 instruction files ^[code-11] | automatically, for any file the `applyTo` glob matches ^[code-20] |
| 1 skill ^[code-13] | discovered from its description ^[code-21] |
| 25 templates ^[code-11] | referenced by a consuming prompt, agent or skill ^[code-23] |
| 6 context files ^[code-11] | retrieved by semantic search ^[code-22] |

## 🔗 Composition

Ten review-oriented prompts sit **outside** this family in the unparented set: `correlated-topics`, `fact-checking`, `gap-analysis`, `grammar-review`, `logic-analysis`, `publish-ready`, `readability-review`, `series-validation`, `structure-validation` and `understandability-review`. ^[code-17] They declare no domain and sit in no domain folder, so the derivation left them unparented. ^[code-15]

## ⚙️ Bindings

The family's three instruction files bind by glob, like every instruction file in the repository. ^[code-20]

## 📤 Outputs

Not observable from this repository. See the gap above.

## 🕳️ Open questions

> **Not established**: whether the ten unparented review prompts belong to this family. They match its subject by name but declare no domain and sit outside its folders ^[code-15,code-17], so they are recorded as unparented rather than assumed into it. ^[gap]

> **Not established**: what the family's 25 templates and 3 instruction files each cover. The counts are recorded ^[code-11]; the per-artifact subjects were not recorded by the artifact derivation. ^[gap]

## 🔗 Related

- [Other Components](index.md) — the other families and the unparented set
- [Prompt engineering artifacts](01-prompt-engineering-artifacts.md) — the family that governs how these artifacts are authored
