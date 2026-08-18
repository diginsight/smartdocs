---
title: "Reference"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Settings, endpoints, contracts and schemas, in full."
source_sets:
  - settings-sources
  - entry-points
  - domain-model
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass"
  evidence:
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
  open_gaps: 0
-->

# Reference

## 🎯 Introduction

The exact surface: every setting, every route, every contract member, every schema key.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Configuration settings](01-configuration-settings.md) | Every configuration key, its declared value and its effect |
| [HTTP and hub endpoints](02-http-endpoints.md) | Every route mapped, what it returns, and what guards it |
| [Navigation hub contract](03-navigation-hub-contract.md) | The SignalR message shapes |
| [Folder metadata schema](04-folder-metadata-schema.md) | The optional `metadata.yml` beside a folder |
| [Article front matter](05-article-front-matter.md) | The YAML block at the head of a document |
| [Navigation rules](06-navigation-rules.md) | Labels, ordering, exclusions and classification |

## 🔑 Key points

- **An empty declared value is a placeholder, not a default.** `Site:InvalidateApiKey` ^[configuration-05], the space's `Blob.AccountUri` and `Blob.ContainerName` ^[configuration-07] and `Redis:Configuration` ^[configuration-10] are all declared empty here; the settings file states that environment-specific storage identity is deliberately absent and arrives from the external overlay ^[configuration-26].
- **Two configuration keys change the route table.** `Testing:ContentMutationEnabled` decides whether the mutation and metrics routes are mapped at all ^[smartdocs-web/code-17] ^[configuration-21]; an empty `Site:InvalidateApiKey` removes the header check on the invalidation endpoint ^[smartdocs-web/code-16] ^[configuration-22].
- **Hub values are absolute despite the type name.** `NavAggregateDelta` carries counts, not increments. ^[smartdocs-web-shared/code-09]
- **Names are normalised before they are displayed.** A date prefix becomes `YYYYMMDD - REST`, a numeric prefix is dropped, and what remains is titleized. ^[smartdocs-web-shared/code-13]
- **Some names never reach the level at all.** Names beginning `_` or `.`, `*.changelog.md` files, and — at the root only — `99.00-temp` are excluded during level construction. ^[smartdocs-web/code-24]

## 🔗 Related

- [Architecture](../03.00-architecture/index.md) — why these surfaces are shaped this way
- [Security posture](../09.00-security/01-security-posture.md) — the authorisation picture behind the endpoint table
- [Getting Started](../02.00-getting-started/index.md) — the settings that matter for a local run
