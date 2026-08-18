---
title: "Prompt engineering artifacts"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The largest artifact family — the rules that govern how the other artifacts are written."
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

# Prompt engineering artifacts

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [🧭 Behaviour](#-behaviour)
- [▶️ Invocation surface](#-invocation-surface)
- [🔗 Composition](#-composition)
- [⚙️ Bindings](#-bindings)
- [📤 Outputs](#-outputs)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Purpose

The component registry derives this family's purpose as governing how AI-customization artifacts are authored and validated. ^[discovery] It is the largest of the four families. ^[code-09]

**Derived from**: declared domain metadata on each artifact, with the containing folder used only where no domain is declared. ^[code-05]

## 🧭 Behaviour

The family's 15 instruction files ^[code-09] each declare an `applyTo` glob and are loaded automatically for any file the glob matches. ^[code-20]

Four prompt sub-groups sit under this family rather than forming families of their own: `pe-simple` (3 prompts), `pe-consolidated` (3), `pe-granular` (20) and `pe-meta` (29), 55 prompts in total. ^[code-07]

Four skill-scoped families are declared below the main four; two of them are named for prompt-engineering concerns — `pe-prompt-engineering-validation` (5 artifacts) and `pe-artifact-coherence-check` (3). ^[code-13]

> **Not established**: what any of these artifacts does when invoked. Every member was read as a declaration; no execution log, run record or produced-output artefact tying an invocation to a result exists in the repository. ^[gap]

## ▶️ Invocation surface

| Type | How it is reached |
|---|---|
| Agent | named by the user ^[code-18] |
| Prompt | a slash command ^[code-19] |
| Instruction file | automatically, for any file its `applyTo` glob matches ^[code-20] |
| Skill | discovered from its description ^[code-21] |
| Context file | retrieved by semantic search rather than declared invocation ^[code-22] |
| Template | referenced by a consuming prompt, agent or skill ^[code-23] |
| Prompt snippet | included by file reference ^[code-23] |

## 🔗 Composition

The family's 44 templates, 43 context files, 15 instruction files and 32 agents ^[code-09] reach a session by three different mechanisms: a template is referenced by a consuming prompt, agent or skill ^[code-23]; a context file is retrieved by semantic search ^[code-22]; an instruction file loads automatically for any file its glob matches ^[code-20].

It holds the largest agent population of the four families — 32, against 11, 3 and 1. ^[code-09,code-10,code-11,code-12]

`.github/copilot-instructions.md` is declared as the highest-authority artifact and injected last into every system prompt. ^[code-24]

## ⚙️ Bindings

Instruction files bind by glob: each declares an `applyTo` pattern and is loaded for any file that pattern matches. ^[code-20]

## 📤 Outputs

Not observable from this repository. See the gap above.

## 🕳️ Open questions

> **Not established**: whether the four prompt sub-groups represent a deliberate maturity progression or an incremental accumulation. The sub-groups were confirmed as sub-groups rather than families ^[code-07]; nothing states why they are separated that way. ^[gap]

> **Not established**: whether every template, snippet and context reference in these artifacts resolves to an existing file. A coherence-check skill is declared for exactly this purpose, but no recorded run of it exists. ^[gap]

## 🔗 Related

- [Other Components](index.md) — the other families and the unparented set
- [Application development artifacts](02-application-development-artifacts.md) — the family that produced this documentation set
