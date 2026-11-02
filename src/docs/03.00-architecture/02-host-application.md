---
title: "The host — Diginsight.SmartDocs.Web"
author: "Dario Airoldi"
date: "2026-08-18"
description: "How the server project composes itself, what it exposes, and how it warms up."
source_sets:
  - composition-root
  - entry-points
  - settings-sources
  - pipeline-definition
  - authn-authz-surface
  - options-model
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
    - dossier: "_evidence/smartdocs-web/data.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
  open_gaps: 1
-->

# The host — Diginsight.SmartDocs.Web

## 📚 Table of contents

- [🎯 Purpose and context](#-purpose-and-context)
- [🧱 Structure](#-structure)
- [🔀 Key flows](#-key-flows)
- [🔗 Dependencies](#-dependencies)
- [🧭 Design decisions](#-design-decisions)
- [🏗️ Physical placement](#-physical-placement)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)
## 🎯 Purpose and context

The host is where every decision in this system is made concrete. It reads configuration, decides which content sources exist, wires the cache, exposes the HTTP and SignalR surfaces, renders the first paint server-side and then hands the page to WebAssembly. It is the only project the pipeline publishes and deploys. ^[devops-10]

## 🧱 Structure

| Part | Responsibility | Established by |
|---|---|---|
| `Program.cs` | The whole composition root, top to bottom | ^[code-01] |
| `ContentSources/` | The per-space source factory and the cache wrapper around it | ^[code-08,code-09] |
| `Navigation/` | `DynamicNavBuilder`, `CachedDynamicNavBuilder`, `NavChangePublisher`, `NavHub` | ^[code-18,code-23,code-34,code-35] |
| `Endpoints/` | `ContentEndpoints`, `NavEndpoints`, `TestContentEndpoints` | ^[code-10,code-11,code-17] |
| `Authorization/` | Access-file facts, effective requirements, per-space role expansion, and request-scoped filtering | ^[code-46,code-47,code-48] |
| `Caching/` | `ContentPathCacheKey`, the value type keying the content cache by space and path | ^[data-12] |
| `Observability.cs`, `ObservabilityManager.cs` | Diginsight, OpenTelemetry and log4net setup | ^[code-02] |

## 🔀 Key flows

### Startup, in order

Observability comes first — before the builder exists — so that the composition itself is traced. ^[code-02] Then configuration is layered, including an out-of-tree overlay read from `ExternalConfigurationFolder` under the name given by `AppsettingsEnvironmentName`. ^[code-03,configuration-04]

Then, in sequence: the observability and telemetry registrations; Razor components with interactive WebAssembly; the `Site` options bound and eagerly resolved; the space registry; one cached content source per space; the SignalR hub; the navigation change publisher. ^[code-04,code-05,code-06,code-07,code-09,code-18,code-35]

Outside `Development`, the pipeline adds an exception handler at `/error`, HSTS, and HTTPS redirection; antiforgery and static assets apply in every environment. ^[code-38] When `AzureAd:ClientId` is nonblank, Microsoft Identity Web registers reader sign-in and authentication runs immediately before `ReaderAccess` initialization. Authorization then happens through that scoped service rather than a blanket fallback policy. ^[code-46,code-54]

The server checks the exact file selected by the shared rendered-page cache, filters navigation and shell responses, and composes visible totals before returning them. ^[code-20,code-49,code-50] Sign-in and sign-out are full-page transitions; the bootstrap carries display and sign-out-form state, not permission authority. ^[code-54,code-55]

### Warm-up

After the application is built a background warm-up runs: it seeds folder metrics from a snapshot, discovers root branches, warms the index and every level, then pushes reader-filtered ready counts. ^[code-36,code-52] Snapshot format 2 includes per-space-compatible cells and `rootOwn`; incompatible snapshots are ignored and rebuilt. ^[code-51] The snapshot path comes from `Site:MetricsSnapshotPath`, or falls back beside the binaries. ^[code-37]

### Cancellation

An endpoint filter maps `OperationCanceledException` raised on the request's own cancellation token to HTTP **499**. ^[code-19] A reader navigating away mid-request is recorded as a client abort rather than as a server fault — a distinction that matters once you start reading the traces. ^[security-20]

## 🔗 Dependencies

The host renders Razor components server-side and hands interactivity to WebAssembly. ^[code-05] It is the project that takes the Diginsight observability, SmartCache, Azure Blob and SignalR dependencies. ^[code-02,code-08,code-18,configuration-18]

## 🧭 Design decisions

**One cached source per space, composed at startup.** A local factory creates a filesystem or a blob source according to the space's `Source` value ^[code-08], and each is wrapped in a `CachedContentSource` and registered in a space registry ^[code-09]. The choice is therefore made once, at composition, not per request.

**Blob access without a key.** `BlobContentSource` authenticates with `DefaultAzureCredential`; no account key, shared-access signature or connection string is accepted. ^[security-08]

**Test-only endpoints behind a configuration gate.** The content-mutation and nav-metrics diagnostic routes are mapped only when `Testing:ContentMutationEnabled` is true. ^[code-17,configuration-21] They are absent from the route table entirely when the flag is off, rather than present and refusing.

**Cache invalidation guarded by a comparison that does not leak timing.** The invalidate endpoint compares the supplied `X-Invalidate-Key` against `Site:InvalidateApiKey` using `CryptographicOperations.FixedTimeEquals`, a constant-time comparison, and returns `401` on mismatch. ^[code-15,security-05]

## 🏗️ Physical placement

Published self-contained for a Windows runtime identifier ^[devops-10], zipped and pushed to Azure App Service ^[devops-12], with a smoke check closing the deploy ^[devops-19]. The published output has its zero-byte Brotli assets pruned and the static-asset endpoint manifest rewritten to match — a repair step the pipeline performs before packaging. ^[devops-11]

## 🕳️ Open questions

The invalidation endpoint has a configuration-dependent management guard. ^[security-05,security-06] Whether it is adequately configured in a deployed instance remains an operational verification question.

> **Not established**: whether either deployed environment supplies an invalidation key. The pipeline writes `Site__InvalidateApiKey` only when the corresponding optional secret is present, and whether it is configured is a repository-settings fact this repository does not carry. ^[gap]

## 🔗 Related

- [System architecture](01-system-architecture.md) — how this fits with the other two projects
- [Caching and invalidation](05-caching-and-invalidation.md) — the cache this host composes
- [Reference](../06.00-reference/index.md) — the endpoints and settings named above
- [Security](../09.00-security/index.md) — the posture that follows from the middleware pipeline above
