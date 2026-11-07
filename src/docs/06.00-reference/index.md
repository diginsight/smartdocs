---
title: "Reference"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Settings, endpoints, contracts and schemas, in full."
source_sets:
  - settings-sources
  - entry-points
  - domain-model
  - authn-authz-surface
  - test-surface
---

<!--
verification_stamp:
  generated: "2026-11-07"
  verified: "2026-11-07"
  gate_outcome: "fail"
  evidence:
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-11-07"
  open_gaps: 3
-->

# Reference

## 🎯 Introduction

Settings, endpoints, contracts, and schemas for the application.

> **Verification pending**: this index and the refreshed schema entries haven't passed the repository-documentation gates. ^[gap]

> **Not established**: the older configuration, security, architecture, and navigation pages haven't yet been fully refreshed for unified metadata. Use the unified contract for the current metadata behavior. ^[gap]

> **Not established**: independent verification from a fresh checkout. The current evidence dossiers are local and excluded from version control; regenerate them before verification elsewhere. ^[gap]

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Configuration settings](01-configuration-settings.md) | Every configuration key, its declared value and its effect |
| [HTTP and hub endpoints](02-http-endpoints.md) | Every route mapped, what it returns, and what guards it |
| [Navigation hub contract](03-navigation-hub-contract.md) | The SignalR message shapes |
| [Folder metadata schema](04-folder-metadata-schema.md) | Navigation fields in a directory's `metadata.yml` |
| [Article front matter](05-article-front-matter.md) | The YAML block at the head of a document |
| [Navigation rules](06-navigation-rules.md) | Labels, ordering, exclusions and classification |
| [Access-file schema](07-access-file-schema.md) | Root space requirements, role grants, compatibility, and failure policy |
| [Unified metadata contract](08-unified-metadata-contract.md) | Field, scope, caching, and migration guidance ^[smartdocs-web/code-71,smartdocs-web/code-83,smartdocs-web/code-85] |

## 🔑 Key points

- **Spaces come from the environment.** The base space array is empty; provide complete elements in the overlay. ^[configuration-07]
- **Sign-in is optional, requirements aren't.** Nonblank `AzureAd:ClientId` enables identity; per-space permission evaluation still protects content when no reader can sign in. ^[configuration-28,smartdocs-web/code-47]
- **Hub values are absolute.** The publisher sends `FolderRecord` arrays filtered for role-set groups, not the legacy delta shape. ^[smartdocs-web/code-33,smartdocs-web/code-52]
- **Names are normalised before they are displayed.** A date prefix becomes `YYYYMMDD - REST`, a numeric prefix is dropped, and what remains is titleized. ^[smartdocs-web-shared/code-13]
- **Some names never reach the level at all.** Names beginning `_` or `.`, `*.changelog.md` files, and — at the root only — `99.00-temp` are excluded during level construction. ^[smartdocs-web/code-24]
- **Metadata acquisition is shared.** Navigation and authorization use one root snapshot; the regression harness checks the shared cold-read behavior. ^[smartdocs-web/code-83,smartdocs-web/code-97]
- **A required policy is logical, not just a filename.** An explicit `access: {}` satisfies `AccessFileRequired`; navigation-only metadata doesn't. ^[smartdocs-web/code-81]
- **Legacy compatibility is transitional.** It defaults to enabled, and canonical-only mode avoids legacy access-file reads after migration. ^[smartdocs-web/security-47,smartdocs-web/code-87]

## 🔗 Related

- [Architecture](../03.00-architecture/index.md) — why these surfaces are shaped this way
- [Security posture](../09.00-security/01-security-posture.md) — the authorisation picture behind the endpoint table
- [Getting Started](../02.00-getting-started/index.md) — the settings that matter for a local run
