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
  verified: "2026-09-04"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-09-04"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-09-04"
  open_gaps: 1
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

The group comes from the name alone — the function is given no folder or depth context — so a date-prefixed name is ordered newest-first wherever in the tree it sits. ^[smartdocs-web-shared/code-28]

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

Those two index names are the whole set: nothing else represents a folder, and a name beginning `_` is removed before the index test is ever reached. ^[smartdocs-web-shared/code-29]

## 🔑 Keys and constraints

**Classification decides what a child becomes.** ^[smartdocs-web/code-25]

| Condition | Result |
|---|---|
| The folder has at least one subfolder, or more than one article | a section |
| The folder holds exactly one article, or only an index or readme | a collapsed leaf |
| Neither | nothing — the folder does not appear |

**A declared folder `order` joins the numeric group.** When a folder's `metadata.yml` declares `order`, that value replaces the name-derived key and lands in group 0, alongside the numerically prefixed names; ties break on the lowercased folder name. ^[smartdocs-web/code-39]

**The three exclusion mechanisms run in a fixed order.** ^[smartdocs-web/code-42]

| Step | Applies to | Discards |
|---|---|---|
| 1 | every child | an excluded name, and at the root only an infrastructure name |
| 2 | folders | a folder whose `metadata.yml` declares `hidden` |
| 3 | folders | a folder that classifies as nothing |
| 4 | files | a non-Markdown file, and an index or readme, which represents its folder instead |
| 5 | files | a document whose front matter is hidden |

A name excluded at step 1 is never read, so neither its metadata nor its front matter can bring it back.

**A single sibling article displaces the folder's index.** A collapsed folder is represented by its one article whenever it has exactly one, and by its index or readme only when it has none — so a folder holding both an index and one other article does not surface that index in the level. ^[smartdocs-web/code-43]

**A collapsed folder's link follows its representative.** The emitted route is the folder's own path when the index or readme represents it, and the article's path with the extension stripped when an article does. ^[smartdocs-web/code-44]

**A hidden single article falls back to the index.** When the one article is hidden by front matter, the folder still yields a link to the folder path if an index or readme exists, and yields nothing otherwise. ^[smartdocs-web/code-45]

**Scoring is concurrent.** A level's children are scored at the medium concurrency tier rather than one at a time. ^[smartdocs-web/code-23]

## 🔗 Used by

`DynamicNavBuilder` on the server, when it builds a level. ^[smartdocs-web/code-23] The same rules are available to the client through the shared library, which both sides reference. ^[smartdocs-web-shared/code-01]

## 🕳️ Open questions

> **Not established**: these rules have no test coverage. Unit tests for `NavRules`, `FrontMatter`, `PageLoader` and `SpaceRegistry` were sought across the solution and none was found, so the label, ordering and classification behaviours above are established from source rather than from executed cases. ^[gap]

## 🔗 Related

- [Folder metadata schema](04-folder-metadata-schema.md) — how a folder overrides these rules
- [Article front matter](05-article-front-matter.md) — how a document overrides them
- [Browsing the navigation tree](../04.00-use-cases/02-browsing-the-navigation-tree.md) — these rules in the context of a request
