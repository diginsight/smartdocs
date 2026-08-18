---
title: "Application development artifacts"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The family that produced this documentation set."
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

# Application development artifacts

- **Priority**: 🟡 Tooling ^[discovery]
- **Grouped by**: declared domain, falling back to containing folder ^[code-05]
- **Roots**: `.github/`, `.copilot/context/10.00-application-development/` ^[code-10]
- **Members**: 56 — 11 agents, 13 context files, 1 instruction file, 12 prompts, 1 skill, 18 templates ^[code-10]

## 🎯 Purpose

Two documentation streams are declared here, each with a manager agent as its sole entry point: `@ad-documentation-manager` produces chapter pages under `src/docs`, and `@ad-robustness-manager` produces tiered findings and a plan file. ^[code-25]

**Derived from**: declared domain metadata, with the containing folder used only where no domain is declared. ^[code-05]

## 🧭 Behaviour

Each stream declares a **manager agent as its sole entry point**. ^[code-25] The family declares 11 agents in total ^[code-10], so the remaining nine are reached by delegation rather than by name.

The family's 12 prompts are each reached as a slash command ^[code-10,code-19], and its 18 templates are referenced by a consuming prompt, agent or skill rather than invoked ^[code-10,code-23].

The family declares one instruction file ^[code-10], which — like every instruction file — declares an `applyTo` glob and is loaded automatically for any file that glob matches ^[code-20].

> **Not established**: what these artifacts do when invoked. This documentation set is the output of one such invocation, but no execution log, run record or produced-output artefact tying the invocation to the result exists in the repository. ^[gap]

## ▶️ Invocation surface

| Entry point | Kind | Reached by |
|---|---|---|
| `@ad-documentation-manager` | agent | named by the user; produces chapter pages under `src/docs` ^[code-18,code-25] |
| `@ad-robustness-manager` | agent | named by the user; produces tiered findings and a plan file ^[code-18,code-25] |
| The family's 12 prompts | prompts | slash commands ^[code-10,code-19] |

## 🔗 Composition

Two of the family's 11 agents are declared entry points; the other nine are reached only by delegation. ^[code-10,code-25] Alongside them sit 12 prompts, 13 context files, 18 templates, 1 instruction file and 1 skill. ^[code-10] A context file is retrieved by semantic search rather than by declared invocation ^[code-22], and a skill is discovered from its description ^[code-21].

## ⚙️ Bindings

The family's single instruction file binds by glob, like every instruction file in the repository. ^[code-20]

## 📤 Outputs

Chapter pages under `src/docs` from the documentation stream, and tiered findings plus a plan file from the robustness stream. ^[code-25]

## 🕳️ Open questions

> **Not established**: whether the nine non-manager agents are ever invoked directly in practice, despite the manager being declared the sole entry point. No invocation record of any kind exists in the repository. ^[gap]

> **Not established**: which page shapes the family's 18 templates declare. The template count is recorded ^[code-10]; the per-template shapes were not recorded by the artifact derivation. ^[gap]

## 🔗 Related

- [Other Components](index.md) — the other families and the unparented set
- [Appendix](../11.00-appendix/index.md) — how this documentation set was produced
