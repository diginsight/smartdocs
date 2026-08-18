---
title: "Reading a document"
author: "Dario Airoldi"
date: "2026-08-18"
description: "A reader opens a URL and gets a rendered page."
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
    - dossier: "_evidence/smartdocs-web/data.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
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
- The document exists in that space's content store under a path the candidate ladder will try. ^[smartdocs-web/code-20]

## 🔬 Flow

1. The reader requests a path. `ContentPage` claims both `/` and the catch-all `/{*path}`, so any path routes to the same page component. ^[smartdocs-web-client/code-09] The host adds the client assembly to the router's additional assemblies, which is what makes those routes reachable from a server-rendered request. ^[smartdocs-web-client/code-10]
2. The path is resolved to a space. `SpaceRegistry.TryResolve` matches the longest route base, so a space mounted on a named segment wins over the space mounted at the root. ^[smartdocs-web-shared/code-24]
3. `PageLoader` walks its candidate list until a source returns bytes: at the root, `index.md` then `readme.md` then `README.md`; elsewhere, `{path}.md`, `{path}/index.md`, `{path}/overview.md`, `{path}/readme.md`, `{path}/README.md`. A path already ending `.md` is used as given. ^[smartdocs-web/code-20]
4. The space's `CachedContentSource` answers — from cache if it can, from the underlying filesystem or blob source otherwise. ^[smartdocs-web/code-09] The cache key is a value type over the space and the path together, so entries from different spaces cannot collide. ^[data-12]
5. Front matter is parsed from the head of the document, up to a 64 KB cap, giving `title`, `publish`, `draft`, `author` and `date`. ^[smartdocs-web-shared/code-18] When no `title` is declared the title falls back to the document's first H1. ^[smartdocs-web-shared/code-20]
6. `MarkdigMarkdownRenderer` renders the body with advanced extensions, automatic heading identifiers, YAML front matter and Mermaid. ^[smartdocs-web-shared/code-17]
7. The rendered page is returned. Automatic identifiers are part of that same pipeline, which is what supplies in-page anchors without the author writing them. ^[smartdocs-web-shared/code-17]

## 🔀 Alternate and failure paths

| Situation | What happens |
|---|---|
| No candidate resolves | `Site:NotFoundPath` is declared as `404.html`. ^[configuration-05] What the host does with that value is not established — see below. |
| The document declares `publish: false` or `draft: true` | `Hidden` is computed as `!publish \|\| draft` and the document is excluded from navigation. ^[smartdocs-web/code-22] |
| The reader navigates away mid-request | An endpoint filter converts `OperationCanceledException` raised on the request's own `RequestAborted` token into HTTP 499 rather than a 500. ^[smartdocs-web/code-19] |
| The request is for a raw asset rather than a page | `GET /_content-raw/{**key}` returns the bytes and content type directly, or 404 when absent. ^[smartdocs-web/code-10] |
| An unhandled error occurs outside `Development` | The exception handler at `/error` takes it, in a fresh scope. ^[smartdocs-web/code-38] |

## 🧪 What proves it

Nothing in this repository executes this flow. The steps above are each readable in the source, and the request path is instrumented — `Diginsight.SmartDocs.Web` and `Microsoft.AspNetCore` are both declared as enabled activity sources and span duration recording is declared on. ^[configuration-13] That is instrumentation, not proof.

## 🕳️ Open questions

> **Not established**: no automated test covers document resolution or rendering. A test project and test-framework reference were sought across the solution and none was found, so the behaviour above is established from source rather than from executed cases. ^[gap]

> **Not established**: what consumes `Site:NotFoundPath`. The key and its value are declared in the settings file, but no record establishes the code path that serves it when no candidate resolves. ^[gap]

## 🔗 Related

- [System architecture](../03.00-architecture/01-system-architecture.md) — the same flow in diagram form
- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the content endpoints named above
- [Article front matter](../06.00-reference/05-article-front-matter.md) — the schema parsed in step 5
