---
title: "Navigation hub contract"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The SignalR contract carrying navigation updates to the browser."
source_sets:
  - domain-model
  - entry-points
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-08-18"
  open_gaps: 1
-->

# Navigation hub contract

## 🎯 Purpose

- **Kind**: SignalR contract
- **Component**: `smartdocs-web-shared`
- **Declared in**: `Navigation/NavHubContracts.cs`

One file, shared by both sides, so the server cannot send a message shape the client does not expect.

## 📋 Members

### `NavHubContract`

| Member | Value | Meaning |
|---|---|---|
| `Route` | `/_nav/hub` | Where the hub is mapped ^[smartdocs-web-shared/code-08] ^[smartdocs-web/code-18] |
| `MetadataChanged` | `MetadataChanged` | A folder's metadata changed ^[smartdocs-web-shared/code-08] |
| `CountsReady` | `CountsReady` | Article counts are available ^[smartdocs-web-shared/code-08] |

Both names are declared once in this file, so client and server share one literal definition of the hub surface. ^[smartdocs-web-shared/code-08]

### `NavAggregateDelta`

| Member | Type | Meaning |
|---|---|---|
| `Prefix` | `string` | The folder this entry describes ^[smartdocs-web-shared/code-09] |
| `ArticleCount` | `int` | The article count beneath that prefix ^[smartdocs-web-shared/code-09] |
| `LatestUtc` | `DateTimeOffset?` | The most recent article date beneath it ^[smartdocs-web-shared/code-09] |
| `Author` | `string?` | The author associated with it ^[smartdocs-web-shared/code-09] |
| `Coverage` | `Coverage` | How much is known ^[smartdocs-web-shared/code-09] |

## 🔑 Keys and constraints

**The values are absolute, not increments.** Despite the type name, `ArticleCount` is the count beneath the prefix rather than a change to a previous count. ^[smartdocs-web-shared/code-09]

**One change publishes several entries.** A change publishes one entry for the changed folder and one for each ancestor up to the root, so a client can refresh a whole spine from a single message. ^[smartdocs-web-shared/code-10] ^[smartdocs-web/code-33]

**Coverage travels with the count.** `None` renders as `…`, `Partial` as `≥ N` and `Complete` as `N`; `Partial` supersedes `None` and `Complete` supersedes both. ^[smartdocs-web-shared/code-11] An unknown count is therefore never rendered as zero. ^[smartdocs-web-shared/code-12]

## ⚠️ Usage constraints

The client folds these values into its per-prefix cache through `ApplyAggregates`, without issuing an HTTP request. ^[smartdocs-web-client/code-07]

The connection is established with `WithAutomaticReconnect()` ^[smartdocs-web-client/code-13], and the hub sends the current root counts on `OnConnectedAsync` ^[smartdocs-web/code-18], so a client that reconnects recovers its baseline without asking.

## 🔗 Used by

`NavChangePublisher` on the server, wired after the application is built and before the host begins serving. ^[smartdocs-web/code-35] `NavHubClient` in the browser, whose connection is best-effort — a failure to connect does not prevent the client rendering, because navigation is also reachable over HTTP. ^[smartdocs-web-client/code-14]

## 🕳️ Open questions

> **Not established**: the hub declares no authorisation guard, and none of the recorded members carries a space identifier. Whether a multi-space deployment would expose one space's counts to another space's readers was not established, because no such deployment exists to observe. ^[gap]

## 🔗 Related

- [HTTP and hub endpoints](02-http-endpoints.md) — where this hub is mapped
- [Caching and invalidation](../03.00-architecture/05-caching-and-invalidation.md) — what triggers these messages
- [Browsing the navigation tree](../04.00-use-cases/02-browsing-the-navigation-tree.md) — these messages in the context of a session
