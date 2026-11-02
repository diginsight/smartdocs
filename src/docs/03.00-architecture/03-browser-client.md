---
title: "The browser client — Diginsight.SmartDocs.Web.Client"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What runs in the browser, how it reaches the server, and what state it owns."
source_sets:
  - composition-root
  - entry-points
  - domain-model
  - pipeline-definition
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-client/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
  open_gaps: 2
-->

# The browser client — Diginsight.SmartDocs.Web.Client

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

The client is the same application, running in a different place. It is not deployed on its own: it ships inside the host's published output, and its routes become reachable only because the host adds its assembly to the router. ^[code-10]

The client implements shared content and navigation abstractions over HTTP. ^[code-03,code-06] For pages, `HttpContentSource` also implements `IRenderedPageResolver` and requests server-rendered page responses; the browser doesn't independently decide which protected file to render. ^[code-04]

## 🧱 Structure

| Part | Responsibility | Established by |
|---|---|---|
| `Program.cs` | Client composition root | ^[code-02,code-05] |
| `Routes.razor` | Router configuration | ^[code-08] |
| `HttpContentSource` | `IContentSource` over the host's raw-content endpoint | ^[code-03,code-11] |
| `HttpNavProvider` | `INavProvider` over the navigation endpoints | ^[code-06,code-12] |
| `NavHubClient` | SignalR connection to the navigation hub | ^[code-13,code-14] |
| `Layout/` | `MainLayout`, `DynNav`, `DynNavNode`, `NotificationPanel`, `AboutMenu` | ^[code-15] |
| `Marker.cs` | The type the host uses to locate this assembly | ^[code-10] |

The shell includes the space switcher, account menu, navigation, page body, table of contents, and footer. ^[code-15]

## 🔀 Key flows

### Reaching the server

A scoped `HttpClient` is registered with its base address set to the host environment's own base address. ^[code-02] The client therefore talks to whatever origin served it, and no server address is embedded in the client at all — the base address is the only environment-derived value it reads. ^[configuration-02,configuration-07]

### Routing

The router is pointed at this assembly, given `MainLayout` as the default layout, and told to move focus to the page's `h1` on navigation. ^[code-08] `ContentPage` claims both `/` and a catch-all `/{*path}`, which is what allows an arbitrary content path to resolve to a page. ^[code-09] ^[code-10]

### Restoring account display

Startup restores the bootstrap reader and registers `BootstrapAuthenticationStateProvider`. Its principal contains display identity, not reconstructed role grants; the server remains the authorization authority. ^[code-19] `SignInMenu` is absent when sign-in isn't enabled. Otherwise it offers **Sign in** or an account menu, with accessible icon-only presentation at narrow widths. ^[code-20,code-23]

Sign-in preserves the current path and query through a full-page challenge. Sign-out submits a native antiforgery-protected POST and navigates away from the client session. ^[code-21,code-22]

### Navigation, lazily

`HttpNavProvider` keeps a per-prefix cache of in-flight and completed tasks, so a level is fetched at most once per prefix per session. ^[code-06] Its `ApplyAggregates` method takes the counts pushed from the server and folds them into the cached level without issuing an HTTP request. ^[code-07]

`NavHubClient` connects with automatic reconnect. ^[code-13] The connection is best-effort: a failure to connect does not prevent the client rendering, because navigation is also reachable over HTTP. ^[code-14]

## 🔗 Dependencies

The shared library, for the state services and navigation types it registers and consumes. ^[code-05,code-17,code-18] It takes no dependency on the host project — the direction is one way, and the host adds the client's assembly to the Razor component set rather than the reverse. ^[code-10]

## 🧭 Design decisions

**No settings file of its own.** The client project declares no `appsettings` file. ^[configuration-01] Everything it needs is either derived from the host address or handed to it as component state — branding, for instance, arrives as state rather than as client configuration. ^[configuration-03]

**Everything scoped.** Content source, renderer, page loader and the state holders are all registered scoped. ^[configuration-04] In WebAssembly a scope is the lifetime of the application, so this is effectively a singleton — but registering scoped keeps the registrations symmetrical with the server, where the distinction is real.

**No credential anywhere.** No secret, key or connection string appears in the client project. ^[configuration-07] Given that everything shipped to a browser is public, this is the only defensible arrangement.

## 🏗️ Physical placement

Compiled to WebAssembly ^[code-01] and carried inside the host's published output, under `wwwroot/_framework` ^[devops-11]. The pipeline's Brotli repair step operates directly on that output. ^[devops-11]

## 🕳️ Open questions

> **Not established**: the remaining layout markup beyond the changed shell and sign-in menu wasn't inspected in this scoped refresh. Its complete rendered presentation isn't described here. ^[gap]

> **Not established**: whether the published client is trimmed or ahead-of-time compiled. No trimming or AOT property was found in the project file or in the shared build properties. ^[gap]

## 🔗 Related

- [System architecture](01-system-architecture.md) — the flow this participates in
- [The shared library](04-shared-library.md) — the abstractions implemented here
- [The host](02-host-application.md) — the surfaces this client calls
- [Reference](../06.00-reference/index.md) — the endpoints this client calls
