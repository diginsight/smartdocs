---
title: "Article front matter"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The YAML block at the head of a document, and what the application does with it."
source_sets:
  - domain-model
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass"
  evidence:
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-content/data.md"
      observed: "2026-08-18"
  open_gaps: 0
-->

# Article front matter

## 🎯 Purpose

- **Kind**: document schema
- **Component**: `smartdocs-web-shared`
- **Declared in**: `Rendering/FrontMatter.cs`

The YAML block at the head of a Markdown document. It is parsed for navigation purposes ^[smartdocs-web-shared/code-18], and separately understood by the renderer, whose pipeline includes YAML front matter support ^[smartdocs-web-shared/code-17].

## 📋 Members

| Key | Type | Default | Effect |
|---|---|---|---|
| `title` | string | the document's first H1 | The navigation label and page title ^[smartdocs-web-shared/code-18] ^[smartdocs-web-shared/code-20] |
| `publish` | boolean | `true` | `false` marks the document hidden ^[smartdocs-web-shared/code-18] ^[smartdocs-web-shared/code-19] |
| `draft` | boolean | `false` | `true` marks the document hidden ^[smartdocs-web-shared/code-18] ^[smartdocs-web-shared/code-19] |
| `author` | string | — | Carried on the navigation aggregate ^[smartdocs-web-shared/code-18] ^[smartdocs-web-shared/code-09] |
| `date` | date | — | Feeds the latest-article value on that aggregate ^[smartdocs-web-shared/code-18] ^[smartdocs-web-shared/code-09] |

## 🔑 Keys and constraints

**A document is hidden when `publish` is false *or* `draft` is true.** `Hidden` is computed as `!publish || draft`. ^[smartdocs-web-shared/code-19]

**Only the leading block, up to 64 KB, is read.** The cap bounds the cost of scoring a large document. ^[smartdocs-web-shared/code-18]

**Title falls back to content.** A document with no declared `title` takes its first H1. ^[smartdocs-web-shared/code-20]

**Hidden means excluded from navigation.** A document marked hidden is excluded when the level is built. ^[smartdocs-web/code-22] Exclusion from navigation is not exclusion from publication — the publish workflow uploads every file under the source path except `bin`, `obj` and `node_modules`, so a hidden file is still present in the container and reachable by direct key. ^[data-13]

## ⚠️ Usage constraints

A file counts as Markdown only when its extension is `.md` or `.qmd` ^[smartdocs-web-shared/code-16], and level construction excludes names beginning `_` or `.` and files ending `.changelog.md` ^[smartdocs-web-shared/code-15]. A file that is excluded by name does not become a navigation entry whatever its front matter declares.

## 🔗 Used by

`DynamicNavBuilder`, which scores each child of a level. ^[smartdocs-web/code-23]

## 🔗 Related

- [Navigation rules](06-navigation-rules.md) — the exclusions and label rules applied around this
- [Folder metadata schema](04-folder-metadata-schema.md) — the equivalent for folders
- [Reading a document](../04.00-use-cases/01-reading-a-document.md) — where this block is parsed on a request
