---
title: "Running against the private overlay"
author: "Dario Airoldi"
date: "2026-08-18"
description: "A developer runs the application locally with an environment's real settings."
source_sets:
  - settings-sources
  - composition-root
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-10-28"
  open_gaps: 1
-->

# Running against the private overlay

## 📚 Table of contents

- [🎯 Goal](#-goal)
- [✅ Preconditions](#-preconditions)
- [🔬 Flow](#-flow)
- [🔀 Alternate and failure paths](#-alternate-and-failure-paths)
- [🧪 What proves it](#-what-proves-it)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)
## 🎯 Goal

- **Actor**: a developer with access to the private peer repository
- **Outcome**: the application runs locally using an environment's settings rather than the development defaults
- **Component**: `smartdocs-web`

## ✅ Preconditions

- The private peer repository is checked out **beside** this one, at the relative path the launch profile expects. ^[configuration-24]
- The developer's identity can obtain a token for the content storage account, since blob access uses `DefaultAzureCredential` rather than a key. ^[security-08]

## 🔬 Flow

1. The developer selects the overlay launch profile. It sets `AppsettingsEnvironmentName` to the environment name and `ExternalConfigurationFolder` to the peer repository's relative path beside this one. ^[configuration-24]
2. At startup, `ConfigureAppConfiguration2` reads both variables and layers the out-of-tree settings file on top of the in-tree ones. ^[smartdocs-web/code-03] ^[configuration-04]
3. The `Site` section is bound and eagerly resolved. A missing section throws `InvalidOperationException("Missing 'Site' configuration section.")` rather than starting with defaults. ^[smartdocs-web/code-06]
4. `SpaceRegistry` validates the spaces on construction: at least one, each with an identifier, each route base a single segment. ^[smartdocs-web-shared/code-23]
5. Each space's physical source is chosen from its `Source` value — filesystem or blob ^[smartdocs-web/code-08] — and wrapped in a `CachedContentSource` ^[smartdocs-web/code-09]. With `Source: Blob`, the source authenticates with `DefaultAzureCredential`, so it runs as the developer's own identity. ^[security-08]
6. The application serves the environment's content locally.

### Reader-access fixture overlay

`https - access` selects `Development`, the `access` overlay, and the same private peer root. The profile alone doesn't enable sign-in: the resolved `AzureAd:ClientId` must be nonblank. ^[configuration-39,configuration-28] Define complete space elements in the overlay because the base space array is empty. ^[configuration-07]

The local ID-token sign-in and role-view checks in sequence 03 passed; this doesn't prove a deployed certificate-backed code flow or hub expiry. ^[smartdocs-web/code-59,smartdocs-web/code-60] Use the [provisioning guide](../90.00-issues/202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md) for identity and access-policy setup. Keep actual identifiers and credentials in the private overlay.

## 🔀 Alternate and failure paths

| Situation | What happens |
|---|---|
| The peer repository is not checked out beside this one | The relative path the profile declares does not resolve, so the environment's overlay is absent. ^[configuration-24] |
| The `Site` section does not resolve | Startup throws `InvalidOperationException` with an explicit message. ^[smartdocs-web/code-06] |
| A route base has more than one segment | `SpaceRegistry` rejects it on construction. ^[smartdocs-web-shared/code-23] |
| The developer's identity lacks storage access | Blob reads fail; `BlobContentSource` accepts no account key, shared-access signature or connection string. ^[security-08] |
| The overlay supplies both a Service Bus connection string and a topic name | The SmartCache Service Bus companion is registered, with a per-process subscription name. ^[configuration-19] |

## 🧪 What proves it

Startup validates the space registry and authorization-option constraints. ^[smartdocs-web-shared/code-23,configuration-34,configuration-35,configuration-36] Existing sequence 03 adds a bounded local sign-in and role-view observation, not an assurance of any deployed overlay's contents. ^[smartdocs-web/code-59]

## 🕳️ Open questions

> **Not established**: the effective configuration of either deployed environment. The overlay files are held in the private peer repository, and the workflows mask their identifying values, so what this profile actually loads was not observable from this repository. ^[gap]

## 🔗 Related

- [Getting Started](../02.00-getting-started/index.md) — the launch profiles side by side
- [Configuration settings](../06.00-reference/01-configuration-settings.md) — the configuration surface
- [Security posture](../09.00-security/01-security-posture.md) — how secrets are referenced rather than stored
