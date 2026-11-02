---
title: "Navigation hub contract"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The SignalR contract carrying navigation updates to the browser."
source_sets:
  - domain-model
  - entry-points
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-11-02"
  verified: "2026-11-02"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-11-02"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-11-02"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-10-28"
  open_gaps: 1
-->

# Navigation hub contract

## 🎯 Purpose

- **Kind**: SignalR contract
- **Component**: `smartdocs-web-shared`
- **Declared in**: `Navigation/NavHubContracts.cs`

Shared constants name the events; the current publisher sends absolute `FolderRecord` arrays. ^[smartdocs-web/code-33]

## 📋 Members

### `NavHubContract`

| Member | Value | Meaning |
|---|---|---|
| `Route` | `/_nav/hub` | Where the hub is mapped ^[smartdocs-web-shared/code-08] ^[smartdocs-web/code-18] |
| `MetadataChanged` | `MetadataChanged` | A folder's metadata changed ^[smartdocs-web-shared/code-08] |
| `CountsReady` | `CountsReady` | Article counts are available ^[smartdocs-web-shared/code-08] |

Both names are declared once in this file, so client and server share one literal definition of the hub surface. ^[smartdocs-web-shared/code-08]

### Current payload: `FolderRecord[]`

Both events carry folder records, filtered for their recipient role set. Root count, latest date, and coverage are replaced with a reader-visible composition; other records must belong to visible spaces. ^[smartdocs-web/code-33,smartdocs-web/code-50]

### Other shared type: `NavAggregateDelta`

The shared assembly still declares the type below. Its existence doesn't make it the current publisher payload. ^[smartdocs-web-shared/code-09,smartdocs-web/code-33]

| Member | Type | Meaning |
|---|---|---|
| `Prefix` | `string` | The folder this entry describes ^[smartdocs-web-shared/code-09] |
| `ArticleCount` | `int` | The article count beneath that prefix ^[smartdocs-web-shared/code-09] |
| `LatestUtc` | `DateTimeOffset?` | The most recent article date beneath it ^[smartdocs-web-shared/code-09] |
| `Author` | `string?` | The author associated with it ^[smartdocs-web-shared/code-09] |
| `Coverage` | `Coverage` | How much is known ^[smartdocs-web-shared/code-09] |

## 🔑 Keys and constraints

**The values are absolute, not increments.** Despite the type name, `ArticleCount` is the count beneath the prefix rather than a change to a previous count. ^[smartdocs-web-shared/code-09]

**Recipients are role sets, not person identifiers.** The server normalizes signed-in state and roles into a group identity, tracks active sets, and reevaluates space visibility before pushes. A hidden-only mutation doesn't emit `MetadataChanged` for an unaffected reader. ^[smartdocs-web/code-52] A manual run on a local host on 2026-11-02 observed this: a signed-in reader's counts updated without a reload when a probe article was added to and removed from a space that reader could open, while an anonymous connection received no `MetadataChanged` message. ^[smartdocs-web/code-63]

**A connection lives no longer than its sign-in session.** The hub closes a connection when the cookie session that authorized it expires; the client then reconnects anonymously and receives the anonymous composition. In the same run, with a five-minute session, the server sent the close about five minutes after sign-in, and a role removed mid-session kept working until then. ^[smartdocs-web/code-64,smartdocs-web/security-38]

**Coverage travels with the count.** `None` renders as `…`, `Partial` as `≥ N` and `Complete` as `N`; `Partial` supersedes `None` and `Complete` supersedes both. ^[smartdocs-web-shared/code-11] An unknown count is therefore never rendered as zero. ^[smartdocs-web-shared/code-12]

## ⚠️ Usage constraints

The client folds these values into its per-prefix cache through `ApplyAggregates`, without issuing an HTTP request. ^[smartdocs-web-client/code-07]

The connection is established with `WithAutomaticReconnect()` ^[smartdocs-web-client/code-13], and the hub sends the current root counts on `OnConnectedAsync` ^[smartdocs-web/code-18], so a client that reconnects recovers its baseline without asking.

## 🔗 Used by

`NavChangePublisher` on the server, wired after the application is built and before the host begins serving. ^[smartdocs-web/code-35] `NavHubClient` in the browser, whose connection is best-effort — a failure to connect does not prevent the client rendering, because navigation is also reachable over HTTP. ^[smartdocs-web-client/code-14]

## 🕳️ Open questions

> **Not established**: role-set pushes and session-expiry closure in a deployed environment. They were observed only in a manual run on a local host with a development sign-in registration; no automated test covers them. ^[gap]

## 🔗 Related

- [HTTP and hub endpoints](02-http-endpoints.md) — where this hub is mapped
- [Caching and invalidation](../03.00-architecture/05-caching-and-invalidation.md) — what triggers these messages
- [Browsing the navigation tree](../04.00-use-cases/02-browsing-the-navigation-tree.md) — these messages in the context of a session
