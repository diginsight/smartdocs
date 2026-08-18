---
title: "Appendix"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Supporting material: vocabulary, and how this documentation was produced."
source_sets:
  - domain-model
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/_discovery.md"
      observed: "2026-08-18"
  open_gaps: 1
-->

# Appendix

## 🎯 Introduction

The vocabulary this documentation uses, and how the documentation itself was produced.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Glossary](01-glossary.md) | Terms as this repository uses them, plus the naming conventions applied throughout |

## 🧪 How this documentation was produced

Every page here was derived from the repository's own source, configuration and workflow definitions. Nothing was carried over from prior knowledge of similar systems.

| Property | Value |
|---|---|
| Mode | Create — the first full pass ^[discovery] |
| Layout | Multi-component: three .NET projects, a content set, and four artifact families ^[discovery] |
| Revision observed | `8eb2c653931eddfd88800845e81490be24fd2cc3` on `main`, observed 2026-08-18 ^[discovery] |
| Sources available | Source ✅ · Running application ✅ (access-gated) · CI portal ✅ · Cloud portal ⚠️ · API explorer ❌ · Database ❌ ^[discovery] |
| Evidence | One dossier per investigated area per component, under `src/docs/_evidence/`, all declared unpublished ^[discovery] |
| Classification | Public repository with a private peer; anything naming a deployed resource lives in the peer ^[discovery] |

Every assertion on every page carries an anchor back to a numbered evidence record. Every statement that could not be established is marked as a gap rather than left to inference.

## 🔎 Reading the markers

| Marker | Meaning |
|---|---|
| `^[code-nn]`, `^[configuration-nn]`, `^[devops-nn]`, `^[security-nn]`, `^[environment-nn]`, `^[data-nn]` | Traceable to a numbered record in the evidence dossier for that area |
| `^[{component}/{area}-nn]` | The same, qualified by component where a page draws on more than one dossier of the same area |
| `^[discovery]` | Traceable to the discovery record for the run — the component registry, stack profile and capability matrix |
| `^[gap]` | Inside a **Not established** block — something that could not be determined from the sources available |

## 🕳️ Open questions

> **Not established**: no page in this set has been verified against a running instance of the application. Live observation of either deployed host was sought and blocked — hostnames are not declared in this repository and are masked in workflow logs. ^[gap]

## 🔗 Related

- [Diginsight SmartDocs](../index.md) — the entry point to this documentation
- [Other Components](../07.00-other-components/index.md) — the artifact families that produced it
- [Glossary](01-glossary.md) — the vocabulary used across these pages
