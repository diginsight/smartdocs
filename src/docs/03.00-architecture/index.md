---
title: "Architecture"
author: "Dario Airoldi"
date: "2026-08-18"
description: "How Diginsight SmartDocs is put together."
source_sets:
  - composition-root
  - entry-points
  - domain-model
  - settings-sources
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
  open_gaps: 0
-->

# Architecture

## 🎯 Introduction

What the parts are, how a request travels through them, and why the system is shaped this way rather than some other way.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [System architecture](01-system-architecture.md) | The whole picture — parts, request path, count semantics, and the decisions behind them |
| [The host — Diginsight.SmartDocs.Web](02-host-application.md) | The server project: composition, middleware, startup and warm-up |
| [The browser client — Diginsight.SmartDocs.Web.Client](03-browser-client.md) | What runs under WebAssembly and how it reaches the server |
| [The shared library — Diginsight.SmartDocs.Web.Shared](04-shared-library.md) | The abstractions, models and navigation rules both sides depend on |
| [Caching and invalidation](05-caching-and-invalidation.md) | How repeated work is avoided and how the site learns content changed |

## 🔑 Key points

- **Three projects, two execution contexts, one set of abstractions.** The shared library declares `IContentSource`, `IMarkdownRenderer` and `INavProvider`, and both sides register the same interface set against different implementations. ^[smartdocs-web-shared/code-03,smartdocs-web-shared/code-04,smartdocs-web-shared/code-06,smartdocs-web-shared/code-07] The host creates a filesystem or a blob source per space ^[smartdocs-web/code-08]; the client resolves the same interfaces over HTTP ^[smartdocs-web-client/code-03,smartdocs-web-client/code-06].
- **Nothing is pre-built.** A document is resolved and rendered when its path is requested ^[smartdocs-web/code-20,smartdocs-web/code-21], and navigation is discovered one level at a time ^[smartdocs-web/code-23].
- **Unknown is a first-class value.** An article count that has not been computed renders as `…` or `≥ N`, never as `0`. ^[smartdocs-web-shared/code-11,smartdocs-web-shared/code-12]
- **External dependencies are opt-in by configuration presence.** The Service Bus companion is registered only when both a connection string and a topic name resolve, and the Redis passive store only when a configuration string resolves. ^[configuration-19,configuration-20]
- **Resource identity lives outside this repository.** The public settings declare the *shape* of a deployment and leave every identifying value empty, arriving instead from an external overlay. ^[configuration-07,configuration-26]

## 🔗 Related

- [Getting Started](../02.00-getting-started/index.md) — build and run what this chapter describes
- [Reference](../06.00-reference/index.md) — the settings, endpoints and schemas named throughout
- [Infrastructure](../05.00-infrastructure/index.md) — where this architecture is deployed
- [Security](../09.00-security/index.md) — the posture that follows from the middleware pipeline
