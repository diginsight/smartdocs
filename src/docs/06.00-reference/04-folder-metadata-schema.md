---
title: "Folder metadata schema"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The navigation fields in a directory's unified metadata.yml."
source_sets:
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
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-11-07"
  open_gaps: 3
-->

# Folder metadata schema

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [📋 Members](#-members)
- [🔑 Keys and constraints](#-keys-and-constraints)
- [⚠️ Usage constraints](#-usage-constraints)
- [🔗 Used by](#-used-by)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Purpose

A directory's `metadata.yml` supplies local navigation overrides. The host reads it through the shared metadata provider; a content root can also contain a space policy, but navigation fields don't grant access. ^[smartdocs-web/code-26,smartdocs-web/code-76,smartdocs-web/security-49]

> **Verification pending**: this refreshed draft hasn't passed the repository-documentation gates. ^[gap]

- **Kind**: navigation fields in the unified file schema.
- **Component**: `smartdocs-web` and `smartdocs-web-shared`.
- **Declared in**: `Metadata/MetadataParser.cs` and `Navigation/FolderMeta.cs`. ^[smartdocs-web/code-71,smartdocs-web-shared/code-25]

## 📋 Members

Fields are optional. Omitted values leave the corresponding navigation default in place. ^[smartdocs-web/code-75]

| Key | Aliases | Effect |
|---|---|---|
| `label` | `nav-label` | Sidebar display name. ^[smartdocs-web/code-27] |
| `short` | `nav-short` | Compact display name. ^[smartdocs-web/code-27] |
| `icon` | `nav-icon` | Icon identifier. ^[smartdocs-web/code-27] |
| `order` | `nav-order` | Sort weight, replacing the name-derived weight. ^[smartdocs-web/code-27,smartdocs-web/code-39] |
| `hidden` | `nav-hidden` | Excludes the folder from its navigation level when true. ^[smartdocs-web/code-26,smartdocs-web/code-27] |
| `topbar-hidden` | `nav-topbar-hidden`, `hidden-topbar` | Hides the top-bar entry when true. ^[smartdocs-web/code-27] |
| `topbar-align` | `nav-topbar-align` | Selects top-bar alignment. ^[smartdocs-web/code-27] |
| `article-count` | `nav-article-count`, `articles` | Seeds an article count until computed values are available. ^[smartdocs-web/code-27,smartdocs-web/code-40] |
| `latest-article` | `nav-latest-article`, `updated` | Seeds a latest-article date alongside the count. ^[smartdocs-web/code-27,smartdocs-web/code-40] |

## 🔑 Keys and constraints

Versioned files use `schema: smartdocs-metadata/v1`. Unknown or incorrectly cased keys, duplicate mapping keys, multiple aliases for one field, and invalid non-null typed values are rejected. The parser accepts UTF-8 input of at most 65,536 bytes. ^[smartdocs-web/code-71,smartdocs-web/code-72,smartdocs-web/code-73]

An unversioned flat file retains legacy ordering, optional fences, and scalar custom values, while reserved policy and repository keys don't enter its navigation projection. Structured `access` and `repository` sections require the versioned schema. ^[smartdocs-web/code-74,smartdocs-web/security-52]

The folder's own file supplies its navigation values; they aren't inherited from parent metadata. A declared `order` is compared on the same scale as a name-derived numeric prefix. Count and date seeds are superseded by the counting index when it has a value. ^[smartdocs-web/code-76,smartdocs-web/code-39,smartdocs-web/code-40]

## ⚠️ Usage constraints

`hidden` controls navigation, not authorization. A nested `access` section isn't a supported folder policy; whole-space access belongs at the physical content root. ^[smartdocs-web/code-26,smartdocs-web-shared/code-42]

The shared snapshot supplies navigation and authorization projections without a separate canonical read by each consumer. Compatibility mode can additionally read the legacy access carrier. ^[smartdocs-web/code-83,smartdocs-web/code-85,smartdocs-web/code-97]

## 🔗 Used by

| Consumer | Use |
|---|---|
| `DynamicNavBuilder` | Reads the folder's navigation projection while building a level. ^[smartdocs-web/code-26] |
| `FolderRecordProvider` | Sends filtered navigation values in browser-facing folder records. ^[smartdocs-web/code-90] |
| `ContentMetadataProvider` | Loads and caches the shared metadata snapshot. ^[smartdocs-web/code-83] |

## 🕳️ Open questions

> **Not established**: behavior with real distributed cache and messaging services; local and simulated checks don't prove a deployed multi-host configuration. ^[gap]

> **Not established**: independently repeatable verification from a fresh checkout. The current evidence dossiers are local and excluded from version control; regenerate them before verifying this page elsewhere. ^[gap]

## 🔗 Related

- [Unified metadata contract](08-unified-metadata-contract.md) — complete fields, defaults, migration, and validation rules.
- [Navigation rules](06-navigation-rules.md) — behavior when no metadata is supplied.
- [Article front matter](05-article-front-matter.md) — metadata for individual articles.
