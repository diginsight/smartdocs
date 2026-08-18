---
title: "Other Components"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The AI-customization artifacts alongside the application, grouped into families."
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
  open_gaps: 3
-->

# Other Components

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🗺️ Pages in this section](#-pages-in-this-section)
- [📊 The inventory](#-the-inventory)
- [🔑 Key points](#-key-points)
- [🧩 Families too small for a page](#-families-too-small-for-a-page)
- [🧩 Unparented artifacts](#-unparented-artifacts)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Introduction

Alongside the .NET projects the repository declares seven kinds of AI-customization artifact ^[code-01] and holds **379 of them** ^[code-02]. This chapter covers that second group: what families they fall into, how each family was derived, and which artifacts belong to none.

Every artifact is a **declaration**. No execution record exists anywhere in the repository, so these pages state what a family declares — never what it achieves.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Prompt engineering artifacts](01-prompt-engineering-artifacts.md) | 137 artifacts — 32 agents, 43 context files, 15 instruction files, 2 prompts, 1 skill, 44 templates ^[code-09] |
| [Application development artifacts](02-application-development-artifacts.md) | 56 artifacts — 11 agents, 13 context files, 1 instruction file, 12 prompts, 1 skill, 18 templates ^[code-10] |
| [Article writing artifacts](03-article-writing-artifacts.md) | 44 artifacts — 3 agents, 6 context files, 3 instruction files, 7 prompts, 25 templates ^[code-11] |
| [Learning Hub artifacts](04-learning-hub-artifacts.md) | 16 artifacts — 1 agent, 10 context files, 5 prompts ^[code-12] |

## 📊 The inventory

| Type | Count | Changelogs |
|---|---|---|
| Templates | 116 ^[code-02] | 58 ^[code-03] |
| Prompts | 91 ^[code-02] | 14 ^[code-03] |
| Contexts | 73 ^[code-02] | 3 ^[code-03] |
| Agents | 54 ^[code-02] | 14 ^[code-03] |
| Instructions | 21 ^[code-02] | — ^[code-03] |
| Skills | 18 ^[code-02] | — ^[code-03] |
| Snippets | 6 ^[code-02] | — ^[code-03] |
| Hooks | 0 ^[code-04] | — ^[code-04] |

Neither of two further artifact shapes is present: `.github/hooks/` yields zero artifacts, and no chat-mode file was found. ^[code-04]

## 🔑 Key points

- **Families were derived, not declared.** Membership was resolved mechanically by a metadata-then-folder ladder — an artifact's declared domain takes precedence, and its containing folder is used only when no domain is declared. ^[code-05]
- **Most artifacts resolved by metadata.** Across the whole set, 178 resolved by declared metadata, 160 by folder, and 41 by neither. ^[code-06]
- **Four prompt sub-groups are not families.** `pe-simple` (3), `pe-consolidated` (3), `pe-granular` (20) and `pe-meta` (29) total 55 prompts and were confirmed as sub-groups of the prompt-engineering family. ^[code-07] The legacy template folders at the `.github/templates/` root were likewise confirmed as sub-groups of the article-writing family. ^[code-08]
- **Four skill-scoped families sit below the main four.** `pe-prompt-engineering-validation` (5), `article-review` (4), `evidence-capture` (4) and `pe-artifact-coherence-check` (3). ^[code-13]
- **Changelogs accompany 89 artifacts.** Only agents, prompts, templates and context files carry them. ^[code-03]

## 🧩 Families too small for a page

Two groupings had too few members to warrant a page and are recorded here instead. ^[code-14]

| Grouping | Members | What it covers |
|---|---|---|
| `content-classification` | 2 | Splitting a document into a public part and an internal companion |
| `testing-validation` | 1 | Requiring a visible-browser validation artifact for runtime and UI changes |

## 🧩 Unparented artifacts

41 artifacts declare no domain and sit in no domain folder: 7 agents, 10 prompts, 6 prompt snippets and 18 templates. ^[code-15] They are listed rather than assumed into a family.

**Agents (7)** ^[code-16]

`dotnet-upgrade` · `gpt-5-beast-mode` · `microsoft-study-mode` · `research-technical-spike` · `search-ai-optimization-expert` · `task-planner` · `task-researcher`

**Prompts (10)** ^[code-17]

`correlated-topics` · `fact-checking` · `gap-analysis` · `grammar-review` · `logic-analysis` · `publish-ready` · `readability-review` · `series-validation` · `structure-validation` · `understandability-review`

**Prompt snippets (6)** — every prompt snippet in the repository is unparented: six are declared in total ^[code-02] and all six resolved to no family ^[code-15].

**Templates (18)** ^[code-15]

## 🕳️ Open questions

> **Not established**: which prompt snippets and which templates make up the 18 unparented templates and the 6 unparented snippets. The counts were recorded; the individual names were sought in the same derivation output that names the unparented agents and prompts, and were not recorded there. ^[gap]

> **Not established**: whether the 41 unparented artifacts are deliberately unscoped or simply predate the domain convention. The artifact-map section of the repository-wide instructions file and the prompt-engineering instruction files were read; neither addresses artifacts without a domain. ^[gap]

> **Not established**: whether references between artifacts — template paths, snippet includes, context pointers — all resolve. A coherence-check skill is declared for this purpose, but no recorded run of it exists. ^[gap]

## 🔗 Related

- [Architecture](../03.00-architecture/index.md) — the .NET projects, which are the other half of the repository
- [Appendix](../11.00-appendix/index.md) — the vocabulary used across this chapter
