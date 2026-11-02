---
title: "Reading a document"
author: "Dario Airoldi"
date: "2026-08-18"
description: "A reader opens a URL and gets a rendered page."
source_sets:
  - entry-points
  - domain-model
  - composition-root
  - authn-authz-surface
  - test-surface
---

<!--
verification_stamp:
  generated: "2026-11-02"
  verified: "2026-11-02"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-11-02"
    - dossier: "_evidence/smartdocs-web/data.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-11-02"
  open_gaps: 2
-->

# Reading a document

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
- **Outcome**: the Markdown document behind a URL is rendered and displayed
- **Component**: `smartdocs-web`, `smartdocs-web-client`, `smartdocs-web-shared`

## ✅ Preconditions

- The application is running and the reader can reach it.
- At least one space is configured. The `Site` section is bound and eagerly resolved, and a missing section throws ^[smartdocs-web/code-06]; the space registry then validates that at least one space is declared ^[smartdocs-web/code-07].
- The document exists under a path the loader can resolve, and the reader covers its space's effective requirements. ^[smartdocs-web-shared/code-21,smartdocs-web/code-47]

## 🔬 Flow

1. The reader requests a path. `ContentPage` claims both `/` and the catch-all `/{*path}`, so any path routes to the same page component. ^[smartdocs-web-client/code-09] The host adds the client assembly to the router's additional assemblies, which is what makes those routes reachable from a server-rendered request. ^[smartdocs-web-client/code-10]
2. The path is resolved to a space. `SpaceRegistry.TryResolve` matches the longest route base, so a space mounted on a named segment wins over the space mounted at the root. ^[smartdocs-web-shared/code-24]
3. `PageLoader` asks its rendered-page resolver first. On the host, the authorizing resolver checks the exact selected `ContentKey`; a handled hidden result ends resolution without another candidate. ^[smartdocs-web/code-20,smartdocs-web-shared/code-33]
4. The space's `CachedContentSource` answers — from cache if it can, from the underlying filesystem or blob source otherwise. ^[smartdocs-web/code-09] The cache key is a value type over the space and the path together, so entries from different spaces cannot collide. ^[data-12]
5. Front matter is parsed from the head of the document, up to a 64 KB cap, giving `title`, `publish`, `draft`, `author` and `date`. ^[smartdocs-web-shared/code-18] When no `title` is declared the title falls back to the document's first H1. ^[smartdocs-web-shared/code-20]
6. `MarkdigMarkdownRenderer` renders the body with advanced extensions, automatic heading identifiers, YAML front matter and Mermaid. ^[smartdocs-web-shared/code-17]
7. The authorized rendered page is returned. Subsequent browser loads request the rendered-page API through `HttpContentSource`, rather than authorizing or rendering protected content locally. ^[smartdocs-web-client/code-04]

## 🔀 Alternate and failure paths

| Situation | What happens |
|---|---|
| No candidate resolves, or its space isn't visible | Page routes use missing-page presentation; the rendered-page API uses 204 for hidden/missing results. ^[smartdocs-web/security-26] |
| Site default requires `authenticated` and reader is unsigned | The page guard challenges before looking up existence. ^[smartdocs-web/code-54] |
| The document declares `publish: false` or `draft: true` | `Hidden` is computed as `!publish \|\| draft` and the document is excluded from navigation. ^[smartdocs-web/code-22] |
| The reader navigates away mid-request | An endpoint filter converts `OperationCanceledException` raised on the request's own `RequestAborted` token into HTTP 499 rather than a 500. ^[smartdocs-web/code-19] |
| The request is for a raw asset rather than a page | `GET /_content/{**key}` returns the bytes and content type directly, or 404 when absent. ^[smartdocs-web/code-10] A key the reader can't see answers 404 too, before any entity tag is compared, so a conditional request can't confirm it exists. ^[smartdocs-web/security-40] |
| An unhandled error occurs outside `Development` | The exception handler at `/error` takes it, in a fresh scope. ^[smartdocs-web/code-38] |

## 🧪 What proves it

Existing local sequences 01 and 03 record passing anonymous concealment and signed-in role views respectively. ^[smartdocs-web/code-57,smartdocs-web/code-59] A later manual run on a local host, on 2026-11-02, recorded the end-to-end matrix passing. A hidden protected asset answered 404 exactly as a missing one did, and a hidden space's listing and a nonexistent one were byte-identical empty listings. ^[smartdocs-web/code-65,smartdocs-web/security-37] Readers kept a removed role until their session expired and lost it at the next sign-in. ^[smartdocs-web/code-64] An unreadable, invalid or missing access file hid its space from every reader until it was restored. ^[smartdocs-web/code-67] These are manual fixture observations, not an automated suite. ^[smartdocs-web/code-63]

## 🕳️ Open questions

> **Not established**: no automated test covers document resolution or rendering. Prior manual observations don't replace a discoverable regression suite. ^[gap]

> **Not established**: how reading a protected document behaves in a deployed environment. Every recorded run used a local host, a fixture tree and a development sign-in registration, and whether the sign-in registrations are deployed to production isn't established. ^[gap]

## 🔗 Related

- [System architecture](../03.00-architecture/01-system-architecture.md) — the same flow in diagram form
- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the content endpoints named above
- [Article front matter](../06.00-reference/05-article-front-matter.md) — the schema parsed in step 5
