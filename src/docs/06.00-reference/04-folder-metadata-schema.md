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
  verified: "2026-09-04"
  gate_outcome: "pass"
  evidence:
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-09-04"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-09-04"
  open_gaps: 0
-->

# Folder metadata schema

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [📋 Members](#-members)
- [🔑 Keys and constraints](#-keys-and-constraints)
- [🔗 Used by](#-used-by)
- [🔗 Related](#-related)

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
| `order` | `nav-order` | Sort weight, replacing the name-derived one ^[smartdocs-web/code-39] |
| `hidden` | `nav-hidden` | The folder is skipped when this is declared ^[smartdocs-web/code-27] ^[smartdocs-web/code-26] |
| `topbar-hidden` | `nav-topbar-hidden`, `hidden-topbar` | Top-bar visibility ^[smartdocs-web/code-27] |
| `topbar-align` | `nav-topbar-align` | Top-bar alignment ^[smartdocs-web/code-27] |
| `article-count` | `articles` | A seed article count, used until the folder has been counted ^[smartdocs-web/code-40] |
| `latest-article` | `updated` | A seed latest-article date, used alongside the seed count ^[smartdocs-web/code-40] |

## 🔑 Keys and constraints

**The format is flat `key: value`.** A leading and trailing `---` fence is tolerated. ^[smartdocs-web/code-27] ^[smartdocs-web-shared/code-25]

**`hidden` removes the folder from the level.** A folder whose `metadata.yml` declares `hidden` is skipped while the level is being built. ^[smartdocs-web/code-26]

**Both spellings of every key are read.** A bare key and its `nav-`-prefixed form are equivalent, so a folder may use either convention consistently. ^[smartdocs-web-shared/code-25]

**A declared `order` replaces the name-derived sort key.** The value becomes the same kind of key a numeric prefix produces, so a folder declaring `order: 3` and a folder named `03.00-…` are ordered against one another on one scale, and a tie breaks on the lowercased folder name. ^[smartdocs-web/code-39]

**A declared count is a seed, never an override.** A folder's article count and latest-article date are taken from the counting index whenever it holds a value for that path; `article-count` and `latest-article` are read only when it does not, and are then treated as a lower bound rather than a total. ^[smartdocs-web/code-40] The index reports nothing at all for a folder it has not yet counted, which is what sends such a folder to the declared seed instead of to zero. ^[smartdocs-web/code-41]

**An unrecognised key is ignored silently.** Every line is matched by one expression and dispatched on the lowercased key; a key matching nothing has no effect, and the file is neither rejected nor reported — so a misspelled key reads exactly like an absent one. ^[smartdocs-web-shared/code-26]

**A malformed value is discarded, not corrected.** `hidden` and `topbar-hidden` are true only for the literal `true`; a non-numeric `order` or `article-count` and an unparseable `latest-article` are left unset; and `topbar-align` is ignored unless it reads `left` or `right`. An empty or whitespace file yields no overrides at all. ^[smartdocs-web-shared/code-27]

## 🔗 Used by

`DynamicNavBuilder`, which reads the file for each folder in a level. ^[smartdocs-web/code-26]

## 🔗 Related

- [Navigation rules](06-navigation-rules.md) — what happens when no metadata is supplied
- [Article front matter](05-article-front-matter.md) — the equivalent for files
- [Browsing the navigation tree](../04.00-use-cases/02-browsing-the-navigation-tree.md) — where this file is read
