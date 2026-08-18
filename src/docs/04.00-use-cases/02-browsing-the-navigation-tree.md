---
title: "Browsing the navigation tree"
author: "Dario Airoldi"
date: "2026-08-18"
description: "A reader expands the sidebar and the tree is discovered as they go."
source_sets:
  - entry-points
  - domain-model
  - composition-root
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
  open_gaps: 3
-->

# Browsing the navigation tree

## 📚 Table of contents

- [🎯 Goal](#-goal)
- [✅ Preconditions](#-preconditions)
- [🔬 Flow](#-flow)
- [🔀 Alternate and failure paths](#-alternate-and-failure-paths)
- [🧪 What proves it](#-what-proves-it)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Goal

- **Actor**: a reader with a browser
- **Outcome**: the sidebar shows the content hierarchy, with counts that improve as they become known
- **Component**: `smartdocs-web`, `smartdocs-web-client`, `smartdocs-web-shared`

## ✅ Preconditions

- The application is running and a space is configured. ^[smartdocs-web/code-06,smartdocs-web/code-07]
- The reader's browser has loaded the WebAssembly client. ^[smartdocs-web-client/code-01]

## 🔬 Flow

1. The client asks for a level: `GET /_nav/children?prefix=`. ^[smartdocs-web/code-11]
2. `CachedDynamicNavBuilder` is the registered `INavBuilder`; it wraps `DynamicNavBuilder` over an in-process memory cache. ^[smartdocs-web/code-34]
3. `DynamicNavBuilder.BuildLevelAsync` lists the children of that prefix and scores them concurrently at the medium concurrency tier ^[smartdocs-web/code-23] — declared as eight in the settings file ^[configuration-12].
4. Exclusions are applied: names beginning `_` or `.`, `*.changelog.md` files, and at the root only, `99.00-temp`. ^[smartdocs-web/code-24]
5. Each surviving folder has its `metadata.yml` read through a head-only read, and the folder is skipped when it declares `hidden`. ^[smartdocs-web/code-26] Each surviving file has its front matter read; `Hidden` is `!publish || draft`. ^[smartdocs-web/code-22]
6. Survivors are classified. A folder with subfolders, or with more than one article, becomes a section. A folder with exactly one article — or with only an index or readme — collapses into a leaf. Anything else yields nothing. ^[smartdocs-web/code-25]
7. The level is sorted into three groups: numeric-prefixed names ascending, dated names newest first, then everything else alphabetically. ^[smartdocs-web/code-29]
8. At the root only, a Home entry is inserted at index 0. ^[smartdocs-web/code-31]
9. The server returns the level, then starts a fire-and-forget warm of two levels deeper without waiting for it. ^[smartdocs-web/code-11]
10. The client caches the level per prefix, so a level is fetched at most once per prefix per session. ^[smartdocs-web-client/code-06]
11. Counts arrive separately. The hub at `/_nav/hub` pushes `MetadataChanged` and `CountsReady`, and sends the current root counts on connect ^[smartdocs-web/code-18]; the client folds the pushed aggregates into its cached counts without issuing an HTTP request ^[smartdocs-web-client/code-07].

## 🔀 Alternate and failure paths

| Situation | What happens |
|---|---|
| A count is not yet known | It renders as `…`; a known floor renders as `≥ N`; a fully walked subtree renders as `N`. An unknown count is never rendered as `0`. ^[smartdocs-web/code-32] |
| The hub connection drops | `NavHubClient` connects with `WithAutomaticReconnect()`. ^[smartdocs-web-client/code-13] |
| The hub cannot be reached at all | The client still renders, because navigation is also reachable over HTTP. ^[smartdocs-web-client/code-14] |
| The reader abandons the request | Cancellation on the request token maps to HTTP 499. ^[smartdocs-web/code-19] |
| A flat list is wanted instead of a tree | `GET /_nav/index` returns the flattened article index. ^[smartdocs-web/code-14] |
| A caller needs to detect staleness | `GET /_nav/version` returns a monotonically increasing version value. ^[smartdocs-web/code-12] |

## 🧪 What proves it

Nothing executes this flow automatically. Every step is readable in the source, and the navigation endpoints sit under the same declared activity sources as the rest of the host. ^[configuration-13]

## 🕳️ Open questions

> **Not established**: no automated test covers navigation building, classification, ordering or the coverage state machine. Unit tests for `NavRules` and the level builder were sought across the solution and none was found. ^[gap]

> **Not established**: how a folder's declared `order` interacts with the three sort groups. `metadata.yml` is recorded as accepting an `order` key, but no record establishes what the sort key does with it, so this page does not state that it overrides name-based ordering. ^[gap]

> **Not established**: how quickly a content change reaches a connected client. The folder-metrics index schedules its work behind a 400 ms debounce (`FolderMetricsIndex.cs`, line 30), which is a lower bound on scheduling delay only — no dossier record covers it, and nothing here establishes the latency a reader actually sees. ^[gap]

## 🔗 Related

- [System architecture](../03.00-architecture/01-system-architecture.md) — the coverage state machine in context
- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the navigation routes named above
- [Navigation hub contract](../06.00-reference/03-navigation-hub-contract.md) — the message shapes pushed in step 11
- [Folder metadata schema](../06.00-reference/04-folder-metadata-schema.md) — the `metadata.yml` read in step 5
