---
title: "Browsing the navigation tree"
author: "Dario Airoldi"
date: "2026-08-18"
description: "A reader expands the sidebar and the tree is discovered as they go."
source_sets:
  - entry-points
  - domain-model
  - composition-root
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
  open_gaps: 2
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
6. Survivors are classified. A folder with subfolders, or with more than one article, becomes a section. A folder with exactly one article — or with only an index or readme — collapses into a leaf. Anything else yields nothing. ^[smartdocs-web/code-25] When a folder holds an index *and* exactly one other article, the article represents it and the index does not appear. ^[smartdocs-web/code-43]
7. The level is sorted into three groups: numeric-prefixed names ascending, dated names newest first, then everything else alphabetically. ^[smartdocs-web/code-29] A folder that declares `order` in its `metadata.yml` uses that value in place of its name-derived key and joins the numeric group. ^[smartdocs-web/code-39]
8. At the root only, a Home entry is inserted at index 0. ^[smartdocs-web/code-31]
9. `ReaderAccess` filters nodes by `SourceFolder` ownership, not display route. The endpoint returns the filtered level and reader-visible folder records, evaluates its ETag over that response, and queues warming at depth 3. ^[smartdocs-web/code-11,smartdocs-web/code-49]
10. The client caches the level per prefix, so a level is fetched at most once per prefix per session. ^[smartdocs-web-client/code-06]
11. Counts arrive separately. The hub joins a server-derived signed-in/role-set group and sends absolute records filtered for that group, including a root composed only from visible spaces. Hidden-only changes don't trigger a metadata frame for an unaffected reader. ^[smartdocs-web/code-18,smartdocs-web/code-50,smartdocs-web/code-52] The client folds the records into cached counts. ^[smartdocs-web-client/code-07]

## 🔀 Alternate and failure paths

| Situation | What happens |
|---|---|
| A count is not yet known | It renders as `…`; a known floor renders as `≥ N`; a fully walked subtree renders as `N`. An unknown count is never rendered as `0`. ^[smartdocs-web/code-32] |
| The hub connection drops | `NavHubClient` connects with `WithAutomaticReconnect()`. ^[smartdocs-web-client/code-13] |
| The hub cannot be reached at all | The client still renders, because navigation is also reachable over HTTP. ^[smartdocs-web-client/code-14] |
| The reader abandons the request | Cancellation on the request token maps to HTTP 499. ^[smartdocs-web/code-19] |
| A flat list is wanted instead of a tree | `GET /_nav/index` returns the flattened article index. ^[smartdocs-web/code-14] |
| A caller needs to detect staleness | `GET /_nav/version` returns a monotonically increasing version value. ^[smartdocs-web/code-12] |
| The requested folder is hidden | Its folder-record response is the unknown-folder representation, not a protected aggregate. ^[smartdocs-web/security-26] |

## 🧪 What proves it

Existing sequences 01 and 02 record passing anonymous filtering, visible-only root totals, hidden-change suppression, visible-change control pushes and restart/snapshot handling. ^[smartdocs-web/code-57,smartdocs-web/code-58] Signed-in cross-group pushes and cookie-expiry closure are still unproven; sequence 04 didn't execute those scenarios. ^[smartdocs-web/code-60]

## 🕳️ Open questions

> **Not established**: no automated test covers navigation building, classification, ordering or the coverage state machine. Unit tests for `NavRules` and the level builder were sought across the solution and none was found. ^[gap]

> **Not established**: how quickly a content change reaches a connected client. The folder-metrics index schedules its work behind a 400 ms debounce (`FolderMetricsIndex.cs`, line 30), which is a lower bound on scheduling delay only — no dossier record covers it, and nothing here establishes the latency a reader actually sees. ^[gap]

## 🔗 Related

- [System architecture](../03.00-architecture/01-system-architecture.md) — the coverage state machine in context
- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the navigation routes named above
- [Navigation hub contract](../06.00-reference/03-navigation-hub-contract.md) — the message shapes pushed in step 11
- [Folder metadata schema](../06.00-reference/04-folder-metadata-schema.md) — the `metadata.yml` read in step 5
