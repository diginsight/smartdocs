---
title: "Access-file schema"
author: "Dario Airoldi"
date: "2026-10-28"
description: "The root access.metadata.yml schema, permission evaluation and failure policy."
source_sets:
  - authn-authz-surface
  - domain-model
  - settings-sources
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
  open_gaps: 1
-->

# Access-file schema

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [📋 Members](#-members)
- [🔑 Keys and constraints](#-keys-and-constraints)
- [⚠️ Failure policy](#-failure-policy)
- [🔗 Used by](#-used-by)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)
## 🎯 Purpose

- **Kind**: strict YAML schema, version 1
- **Component**: `smartdocs-web`
- **Declared in**: `Authorization/AccessFile.cs`

Place one `access.metadata.yml` at the physical content root of each space that needs repository-owned permissions. The provider reads this file separately from mounted content; public reads and listings exclude it at every depth. Nested files don't establish folder-level permissions. ^[smartdocs-web/security-29]

## 📋 Members

| Key | Type | Contract |
|---|---|---|
| `schema` | Integer | Required, non-null, exactly `1` ^[smartdocs-web/security-36] |
| `permissions` | Mapping of names to nullable descriptions | Optional; omitted, null or `{}` declares nothing. A description can be a string, empty or null; only the names affect validation ^[smartdocs-web/security-36] |
| `requires` | Sequence of permission names | Optional; omitted, null or `[]` adds no file requirement. Every entry must be held ^[smartdocs-web/security-36] |
| `roles` | Mapping of app-role values to permission-name sequences | Optional; omitted, null or `{}` maps nothing. A null/empty role sequence grants nothing ^[smartdocs-web/security-36] |

These shapes, defaults and validation rules come from the strict parser. ^[smartdocs-web/security-36]

```yaml
schema: 1
permissions:
  ProductA.Read: Read the product documentation
  ProductA.Write: Contributor capability
requires: [ProductA.Read]
roles:
  Reader: [ProductA.Read]
  Contributor: [ProductA.Read, ProductA.Write]
```

This example requires `ProductA.Read`; `ProductA.Write` doesn't itself create an editing endpoint. A signed-in reader's app roles grant the union of their mapped permissions in **this space only**. Another space using the same permission spelling doesn't share those grants. ^[smartdocs-web/security-24]

## 🔑 Keys and constraints

- Names begin with an ASCII letter, then use only ASCII letters, digits, `.`, `_` or `-`. Whitespace is invalid. Matching is case-insensitive. ^[smartdocs-web/security-36]
- Duplicate permission declarations and role mappings, including case-only duplicates, invalidate the file. Repeated requirements or grants collapse into sets. ^[smartdocs-web/security-36]
- Every requirement or grant must refer to a declared permission, except `authenticated`: that built-in may be required without declaration, but can't be declared or granted by a role. Null or empty sequence elements are invalid. ^[smartdocs-web/security-36]
- Unknown keys, duplicate mapping keys, multiple YAML documents, and input larger than 65,536 bytes are invalid. ^[smartdocs-web/security-23,smartdocs-web/security-36]
- Empty `requires` doesn't remove deployment restrictions. Effective requirements are the union of the site default, space floor, and file requirements, evaluated all-of. Only empty or `authenticated` is accepted for configured defaults/floors. ^[smartdocs-web/security-24,smartdocs-web/configuration-34,smartdocs-web/configuration-36]

## ⚠️ Failure policy

| File state | Outcome |
|---|---|
| Missing, optional | Apply site default and space floor; no file grants |
| Missing, `AccessFileRequired: true` | Hide the space |
| Invalid or unreadable | Hide the space |
| Valid | Combine requirements and evaluate mapped grants |

Unresolvable requirements also deny visibility. Unreadable outcomes aren't cached; requirements and grants use the same file read. ^[smartdocs-web/security-25] Missing, valid and invalid parsed facts have a default 30-second freshness; effective principal permissions aren't shared cache entries. ^[smartdocs-web/code-48,smartdocs-web/configuration-38]

## 🔗 Used by

`AccessFileProvider` supplies physical-source facts, `AccessRequirementResolver` combines additive requirements, and `SpaceVisibilityEvaluator` expands roles and checks coverage. Request-scoped `ReaderAccess` applies the result across content, pages, navigation, shell and push boundaries. ^[smartdocs-web/code-46,smartdocs-web/code-47,smartdocs-web/code-48,smartdocs-web/code-49,smartdocs-web/code-52]

## 🕳️ Open questions

> **Not established**: the end-to-end unreadable/invalid/required-missing recovery scenario hasn't run. Sequence 04 stopped at authentication preflight; the source failure policy isn't a substitute for F5 evidence. ^[gap]

## 🔗 Related

- [Configuration settings](01-configuration-settings.md) - identity, floors and freshness
- [Security posture](../09.00-security/01-security-posture.md) - response and trust boundaries
- [Provision reader access](../90.00-issues/202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md) - deployment procedure and operating rules
