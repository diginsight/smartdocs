---
title: "Glossary"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Terms used across this documentation, defined as this repository uses them."
source_sets:
  - domain-model
  - settings-sources
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
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
  open_gaps: 0
-->

# Glossary

## 📚 Table of contents

- [📚 Terms](#-terms)
- [🏷️ Naming conventions used in this documentation](#-naming-conventions-used-in-this-documentation)
- [🔗 Related](#-related)

## 📚 Terms

| Term | Meaning here |
|---|---|
| **Article** | A document with a `.md` or `.qmd` extension that navigation counts and links to ^[smartdocs-web-shared/code-16]. Names beginning `_` or `.`, and names ending `.changelog.md`, are excluded ^[smartdocs-web-shared/code-15] |
| **Collapsed leaf** | A folder holding exactly one article, or only an index or readme, which yields a single link rather than an expandable node ^[smartdocs-web/code-25] |
| **Content source** | The abstraction over where documents live — `IContentSource` defines retrieval of an item by key ^[smartdocs-web-shared/code-04] — realised per space as a filesystem source or a blob source by a factory in the composition root ^[smartdocs-web/code-08] |
| **Coverage** | How much is known about an article count: `None` renders `…`, `Partial` renders `≥ N`, `Complete` renders the bare number; `Partial` supersedes `None` and `Complete` supersedes both ^[smartdocs-web-shared/code-11] |
| **Front matter** | The leading YAML block of a document, read up to a 64 KB cap for `title`, `publish`, `draft`, `author` and `date` ^[smartdocs-web-shared/code-18]. The rendering pipeline handles it separately, through `.UseYamlFrontMatter()` ^[smartdocs-web-shared/code-17] |
| **Level** | One tier of the navigation tree. `DynamicNavBuilder.BuildLevelAsync` lists the children of a prefix, scores them concurrently and returns a sorted level ^[smartdocs-web/code-23] |
| **Overlay** | The private configuration file fetched by a sparse clone at deploy time ^[smartdocs-web/devops-13], supplying the environment-specific values the public settings file deliberately leaves absent ^[smartdocs-web/configuration-26] |
| **Prefix** | A folder path used as the key for a navigation level ^[smartdocs-web/code-23] and carried on each hub aggregate entry ^[smartdocs-web-shared/code-09] |
| **Reusable workflow** | A GitHub Actions workflow invoked by `workflow_call` from another workflow rather than by an event of its own ^[smartdocs-web/devops-02] |
| **Section** | A folder with subfolders, or with more than one article, which yields an expandable navigation node ^[smartdocs-web/code-25] |
| **Space** | A named content mount — an identifier, a route base, a title and a source ^[smartdocs-web-shared/code-22]. At most one space may claim `/`, and every other space reserves the first segment of its route base ^[smartdocs-web/configuration-08] |
| **Sort key** | The three-group ordering navigation applies: numerically prefixed names ascending, dated names newest-first, everything else alphabetically ^[smartdocs-web-shared/code-14] |
| **Test-gated endpoint** | A route mapped only when `Testing:ContentMutationEnabled` is true ^[smartdocs-web/configuration-21]; when it is false the route does not exist rather than refusing ^[smartdocs-web/code-17] |

## 🏷️ Naming conventions used in this documentation

| Convention | Why |
|---|---|
| Resources are named by **role**, never by resource name | The settings file states that environment-specific identity is deliberately absent from this repository and arrives from the overlay ^[smartdocs-web/configuration-26] |
| **"It declares X"**, not "it does X" | Every statement here comes from a definition read in the repository, not from an observed run |
| **A gap is a marked absence** | Where something was not established, it is stated as such rather than filled in with a plausible answer |

## 🔗 Related

- [Reference](../06.00-reference/index.md) — where most of these terms are defined precisely
- [Architecture](../03.00-architecture/index.md) — where they are used in context
- [Appendix](index.md) — how this documentation was produced
