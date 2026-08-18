---
title: "Learning Hub artifacts"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The smallest family — knowledge about the content site this application serves."
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
  open_gaps: 2
-->

# Learning Hub artifacts

- **Priority**: 🟡 Tooling ^[discovery]
- **Grouped by**: declared domain, falling back to containing folder ^[code-05]
- **Roots**: `.github/`, `.copilot/context/90.00-learning-hub/` ^[code-12]
- **Members**: 16 — 1 agent, 10 context files, 5 prompts ^[code-12]

## 🎯 Purpose

The component registry derives this family's purpose as carrying site-specific conventions for the hosted content. ^[discovery]

**Derived from**: declared domain metadata, with the containing folder used only where no domain is declared. ^[code-05]

## 🧭 Behaviour

Ten of the family's 16 members are context files ^[code-12] — a higher proportion than any other family, whose context shares are 43 of 137, 13 of 56 and 6 of 44. ^[code-09,code-10,code-11]

A context file is retrieved by semantic search rather than by declared invocation. ^[code-22]

> **Not established**: what these artifacts do when invoked. Every member was read as a declaration; no execution log, run record or produced-output artefact exists in the repository. Context files are retrieved by semantic search ^[code-22], so even their selection leaves no record here. ^[gap]

## ▶️ Invocation surface

| Type | How it is reached |
|---|---|
| 10 context files ^[code-12] | semantic search — no declared invocation ^[code-22] |
| 1 agent ^[code-12] | named by the user ^[code-18] |
| 5 prompts ^[code-12] | slash commands ^[code-19] |

The family declares **no instruction files** ^[code-12], so none of its knowledge is loaded automatically by path the way an instruction file's is ^[code-20]. It reaches a session only when search surfaces a context file, or when a user names the agent or a prompt. ^[code-18,code-19,code-22]

## 🔗 Composition

One agent, five prompts and ten context files, and nothing else. ^[code-12] It is the smallest of the four families — 16 members against 137, 56 and 44. ^[code-09,code-10,code-11,code-12]

## ⚙️ Bindings

None by glob — the family declares no instruction file. ^[code-12] See the invocation note above.

## 📤 Outputs

Not observable from this repository. See the gap above.

## 🕳️ Open questions

> **Not established**: whether a rule declared only in a context file, with no instruction file binding it by path, is reliably applied. The family declares no instruction file ^[code-12] and context files are reached by semantic search ^[code-22]; whether that retrieval happens on any given turn is not recorded anywhere in the repository. ^[gap]

## 🔗 Related

- [Other Components](index.md) — the other families and the unparented set
- [Appendix](../11.00-appendix/index.md) — the vocabulary used across this chapter
