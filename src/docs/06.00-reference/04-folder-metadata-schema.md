---
title: "Folder metadata schema"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The metadata.yml file that lets a folder override how it appears in navigation."
source_sets:
  - domain-model
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
  open_gaps: 3
-->

# Folder metadata schema

## 🎯 Purpose

- **Kind**: file schema
- **Component**: `smartdocs-web-shared`
- **Declared in**: `Navigation/FolderMeta.cs`

A `metadata.yml` beside a folder's content overrides how that folder appears. It is read through a head-only read while the level is being built. ^[smartdocs-web/code-26]

## 📋 Members

Every key accepts more than one spelling. The alternatives are equivalent. ^[smartdocs-web-shared/code-25]

| Key | Aliases | Effect |
|---|---|---|
| `label` | `nav-label` | Display name ^[smartdocs-web/code-27] |
| `short` | `nav-short` | Short display name ^[smartdocs-web/code-27] |
| `icon` | `nav-icon` | Icon identifier ^[smartdocs-web/code-27] |
| `order` | `nav-order` | Sort weight ^[smartdocs-web/code-27] |
| `hidden` | `nav-hidden` | The folder is skipped when this is declared ^[smartdocs-web/code-27] ^[smartdocs-web/code-26] |
| `topbar-hidden` | `nav-topbar-hidden`, `hidden-topbar` | Top-bar visibility ^[smartdocs-web/code-27] |
| `topbar-align` | `nav-topbar-align` | Top-bar alignment ^[smartdocs-web/code-27] |
| `article-count` | `articles` | A declared article count ^[smartdocs-web/code-27] |
| `latest-article` | `updated` | A declared latest-article date ^[smartdocs-web/code-27] |

## 🔑 Keys and constraints

**The format is flat `key: value`.** A leading and trailing `---` fence is tolerated. ^[smartdocs-web/code-27] ^[smartdocs-web-shared/code-25]

**`hidden` removes the folder from the level.** A folder whose `metadata.yml` declares `hidden` is skipped while the level is being built. ^[smartdocs-web/code-26]

**Both spellings of every key are read.** A bare key and its `nav-`-prefixed form are equivalent, so a folder may use either convention consistently. ^[smartdocs-web-shared/code-25]

## 🔗 Used by

`DynamicNavBuilder`, which reads the file for each folder in a level. ^[smartdocs-web/code-26]

## 🕳️ Open questions

> **Not established**: what the sort key does with a declared `order`. The key is recorded as accepted by the metadata parser, but no record establishes that it overrides name-based ordering or changes the sort group, so this page does not state either. ^[gap]

> **Not established**: how `article-count` and `latest-article` interact with computed aggregates. The keys are recorded as accepted; whether a declared value is used in preference to a walked subtree was not established. ^[gap]

> **Not established**: what happens to an unrecognised key. Whether the parser ignores it or rejects the file was not established, so a misspelling's consequence is unknown. ^[gap]

## 🔗 Related

- [Navigation rules](06-navigation-rules.md) — what happens when no metadata is supplied
- [Article front matter](05-article-front-matter.md) — the equivalent for files
- [Browsing the navigation tree](../04.00-use-cases/02-browsing-the-navigation-tree.md) — where this file is read
