---
title: "Navigation rules"
author: "Dario Airoldi"
date: "2026-08-18"
description: "How a folder tree becomes a menu: labels, ordering, exclusions and classification."
source_sets:
  - domain-model
  - composition-root
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
  open_gaps: 2
-->

# Navigation rules

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [📋 Members](#-members)
- [🔑 Keys and constraints](#-keys-and-constraints)
- [🔗 Used by](#-used-by)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Purpose

- **Kind**: rule set
- **Component**: `smartdocs-web-shared`
- **Declared in**: `Navigation/NavRules.cs`, applied by `Navigation/DynamicNavBuilder.cs`

Pure functions in a Razor Class Library referenced by both the host and the client ^[smartdocs-web-shared/code-01], which declares no server-only dependency ^[smartdocs-web-shared/code-02] — so the server's tree and the browser's tree are labelled and ordered by the same code.

## 📋 Members

### Labels

`NavRules.Label` normalises a name. ^[smartdocs-web-shared/code-13]

| Name shape | Pattern | Label |
|---|---|---|
| Dated | `20\d{2}(0[1-9]\|1[0-2])(\d{2})?(\.\d+)?` prefix | `YYYYMMDD - REST` ^[smartdocs-web-shared/code-13] |
| Numeric-prefixed | `\d+(\.\d+)?[-_\s]+` prefix | prefix dropped, remainder titleized ^[smartdocs-web-shared/code-13] |
| Anything else | — | titleized ^[smartdocs-web-shared/code-13] |

### Ordering

`NavRules.SortKey` assigns three groups. ^[smartdocs-web-shared/code-14]

| Group | Contains | Ordered |
|---|---|---|
| 0 | Names with a numeric prefix | ascending ^[smartdocs-web-shared/code-14] |
| 1 | Names with a date prefix | newest first ^[smartdocs-web-shared/code-14] |
| 2 | Everything else | alphabetically ^[smartdocs-web-shared/code-14] |

At the root, and only at the root, a Home entry is inserted at index 0. ^[smartdocs-web/code-31]

### Exclusions

| Rule | Excludes |
|---|---|
| `IsExcludedName` | names beginning `_`, names beginning `.`, and names ending `.changelog.md` ^[smartdocs-web-shared/code-15] |
| Root level only | `99.00-temp` ^[smartdocs-web/code-24] |
| Folder metadata | folders whose `metadata.yml` declares `hidden` ^[smartdocs-web/code-26] |
| Document front matter | documents whose `Hidden` is true, that is `publish: false` or `draft: true` ^[smartdocs-web/code-22] |

### Recognised kinds

| Function | Recognises |
|---|---|
| `IsMarkdown` | `.md`, `.qmd` ^[smartdocs-web-shared/code-16] |
| `IsIndexName` | `index.md`, `readme.md` ^[smartdocs-web/code-30] |
| `IsAssetFolder` | `images`, `img`, `assets`, `media`, `attachments`, `files` ^[smartdocs-web-shared/code-16] |
| `IconFor` | a Bootstrap icon per keyword, falling back to `folder2` ^[smartdocs-web-shared/code-16] ^[smartdocs-web/code-30] |

## 🔑 Keys and constraints

**Classification decides what a child becomes.** ^[smartdocs-web/code-25]

| Condition | Result |
|---|---|
| The folder has at least one subfolder, or more than one article | a section |
| The folder holds exactly one article, or only an index or readme | a collapsed leaf |
| Neither | nothing — the folder does not appear |

**Scoring is concurrent.** A level's children are scored at the medium concurrency tier rather than one at a time. ^[smartdocs-web/code-23]

## 🔗 Used by

`DynamicNavBuilder` on the server, when it builds a level. ^[smartdocs-web/code-23] The same rules are available to the client through the shared library, which both sides reference. ^[smartdocs-web-shared/code-01]

## 🕳️ Open questions

> **Not established**: these rules have no test coverage. Unit tests for `NavRules`, `FrontMatter`, `PageLoader` and `SpaceRegistry` were sought across the solution and none was found, so the label, ordering and classification behaviours above are established from source rather than from executed cases. ^[gap]

> **Not established**: how a folder's declared `order` interacts with these three sort groups, and in what sequence exclusions, metadata and front matter are evaluated relative to one another. The individual rules are each recorded; their relative ordering is not. ^[gap]

## 🔗 Related

- [Folder metadata schema](04-folder-metadata-schema.md) — how a folder overrides these rules
- [Article front matter](05-article-front-matter.md) — how a document overrides them
- [Browsing the navigation tree](../04.00-use-cases/02-browsing-the-navigation-tree.md) — these rules in the context of a request
