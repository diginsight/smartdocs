---
title: "Access-file schema"
author: "Dario Airoldi"
date: "2026-10-28"
description: "Whole-space access policy in the unified metadata file, including compatibility and failure behavior."
source_sets:
  - authn-authz-surface
  - domain-model
  - settings-sources
  - test-surface
---

<!--
verification_stamp:
  generated: "2026-11-07"
  verified: "2026-11-07"
  gate_outcome: "fail"
  evidence:
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-11-07"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
  open_gaps: 4
-->

# Access-file schema

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [📋 Members](#-members)
- [🔑 Keys and constraints](#-keys-and-constraints)
- [⚠️ Usage constraints](#-usage-constraints)
- [🔗 Used by](#-used-by)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Purpose

Place a versioned `access` mapping in `metadata.yml` at a space's physical content root. The policy governs the whole space; nested files don't establish folder-level permissions. ^[smartdocs-web/code-79,smartdocs-web-shared/code-42]

> **Verification pending**: this refreshed draft hasn't passed the repository-documentation gates. ^[gap]

- **Kind**: root access policy in the unified YAML schema.
- **Component**: `smartdocs-web`.
- **Declared in**: `Metadata/MetadataParser.cs` and `Authorization/AccessFile.cs`. ^[smartdocs-web/code-79,smartdocs-web/security-46]

## 📋 Members

| Key | Type | Contract |
|---|---|---|
| `schema` | String | Use `smartdocs-metadata/v1` for the unified envelope. ^[smartdocs-web/code-71] |
| `access` | Mapping | Contains optional `permissions`, `requires`, and `roles`; see the [unified contract](08-unified-metadata-contract.md#-space-access-policy) for their types and defaults. ^[smartdocs-web/code-79,smartdocs-web/security-48] |

For example:

```yaml
schema: smartdocs-metadata/v1
access:
  permissions:
    Read: Read the documentation
    Write: Contributor capability
  requires: [Read]
  roles:
    Reader: [Read]
    Contributor: [Read, Write]
```

In this example every reader must hold `Read`; role grants are combined within this space. ^[smartdocs-web/security-24,smartdocs-web/security-48]

## 🔑 Keys and constraints

Permission and role names use the supported name pattern and are matched case-insensitively. Case-only duplicate declarations and undeclared permission references are invalid. The built-in `authenticated` permission may be required without being declared, but it can't be declared or granted by a role. ^[smartdocs-web/code-80,smartdocs-web/security-46]

`access: {}` is a valid explicit policy and satisfies `AccessFileRequired`. An absent section doesn't satisfy that setting, and `access: null` or a bare `access:` is invalid. File requirements are additive to the site default and space floor, and all effective requirements must be satisfied. ^[smartdocs-web/security-48,smartdocs-web/security-24]

## ⚠️ Usage constraints

| Policy state | Outcome |
|---|---|
| Missing, `AccessFileRequired: false` | Apply the site and space floors. ^[smartdocs-web/security-25] |
| Missing, `AccessFileRequired: true` | Hide the space. ^[smartdocs-web/code-81,smartdocs-web/security-25] |
| Invalid or unreadable selected metadata | Hide the space; unreadable outcomes aren't cached. ^[smartdocs-web/security-25] |
| Valid policy | Evaluate its requirements against the reader's space-specific grants. ^[smartdocs-web/security-24] |

`Site:LegacyMetadataCompatibility` defaults to true. A legacy-only `access.metadata.yml` or an equivalent canonical and legacy pair is accepted with a warning; invalid or conflicting selected policies fail closed. Disabling compatibility prevents legacy access-file reads. Follow the [migration and rollback steps](08-unified-metadata-contract.md#-migration-and-rollback) before disabling it. ^[smartdocs-web/security-47,smartdocs-web/code-85,smartdocs-web/code-87]

Root metadata snapshots default to 30-second freshness. Missing, valid, and invalid outcomes can be cached; unreadable outcomes are retried. The same shared snapshot supplies policy and navigation facts, not a shared cache of effective reader permissions. ^[smartdocs-web/code-83,smartdocs-web/code-84,smartdocs-web/code-48,smartdocs-web/security-27]

Mounted raw and content-head delivery reject canonical and legacy metadata carriers. Those host guards don't establish protection for copies independently exposed by a public repository or storage endpoint. ^[smartdocs-web/security-29]

## 🔗 Used by

| Consumer | Use |
|---|---|
| `ContentMetadataProvider` | Reads and caches the root metadata snapshot. ^[smartdocs-web/code-83] |
| `AccessFileProvider` | Adapts the shared snapshot for the authorization resolver. ^[smartdocs-web/code-48] |
| `AccessRequirementResolver` and `SpaceVisibilityEvaluator` | Combine requirements and evaluate mapped grants. ^[smartdocs-web/security-24,smartdocs-web/security-25] |
| Metadata regression harness | Exercises required-policy, role-union, and compatibility cases. ^[smartdocs-web/code-98] |

## 🕳️ Open questions

> **Not established**: the policy and storage boundaries of a deployed environment. The recorded checks don't establish deployed configuration. ^[gap]

> **Not established**: the current refactor's complete role-assigned browser matrix. Reader, Contributor, multiple-role, unmapped-role, and an established role-free session remain unverified. ^[gap]

> **Not established**: independently repeatable verification from a fresh checkout. The current evidence dossiers are local and excluded from version control; regenerate them before verifying this page elsewhere. ^[gap]

## 🔗 Related

- [Unified metadata contract](08-unified-metadata-contract.md) — complete schema and migration procedure.
- [Configuration settings](01-configuration-settings.md) — identity, floors, and freshness; its metadata-specific details await regeneration.
- [Security posture](../09.00-security/01-security-posture.md) — response and trust boundaries.
- [Provision reader access](../90.00-issues/202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md) — deployment procedure.
