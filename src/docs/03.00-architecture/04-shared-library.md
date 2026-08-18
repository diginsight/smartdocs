---
title: "The shared library — Diginsight.SmartDocs.Web.Shared"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The abstractions, models and rules that both the host and the browser depend on."
source_sets:
  - domain-model
  - composition-root
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
  open_gaps: 3
-->

# The shared library — Diginsight.SmartDocs.Web.Shared

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

A Razor Class Library referenced by both the host and the browser client, which is what allows one component and one type set to run on both sides. ^[code-01] It declares no dependency on ASP.NET Core hosting, Azure Storage or SignalR server types, so nothing server-only leaks into the browser build. ^[code-02]

## 🧱 Structure

| Part | Responsibility | Established by |
|---|---|---|
| `IContentSource`, `IContentLister` | The abstractions over "where documents come from" | ^[code-04,code-05] |
| `IMarkdownRenderer`, `MarkdigMarkdownRenderer` | Markdown to HTML | ^[code-07,code-17] |
| `PageLoader` | Path to document resolution | ^[code-21] |
| `Navigation/` | `INavProvider`, `NavHubContract`, `NavAggregateDelta`, `Coverage`, `NavRules`, `FrontMatter`, `FolderMeta` | ^[code-06,code-08,code-09,code-11,code-15,code-18,code-25] |
| `Sites/` | `SiteOptions`, `SpaceOptions`, `SpaceRegistry` | ^[code-22,code-23,code-24] |

## 🔀 Key flows

### Rendering

`MarkdigMarkdownRenderer` builds one pipeline: advanced extensions, automatic heading identifiers, YAML front matter and Mermaid. ^[code-17] Automatic identifiers are what make in-page anchors and the table of contents work without the author writing them; front-matter support is what keeps the metadata block out of the rendered body.

### Resolution

`PageLoader` owns the candidate ladder described in the architecture overview and asks whichever `IContentSource` it was given. ^[code-21] It is identical code on both sides — only the source differs. ^[code-03]

### The rules

`NavRules` is the single place where the shape of a folder tree becomes the shape of a menu. ^[code-13,code-14,code-15,code-16]

- **Labels.** A date prefix becomes `YYYYMMDD - REST`. A numeric prefix is dropped. What remains is titleized. ^[code-13]
- **Order.** Numeric-prefixed names sort first, ascending. Dated names sort next, newest first. Everything else sorts alphabetically. ^[code-14]
- **Exclusions.** Names beginning `_` or `.` are excluded, as are `*.changelog.md` files. ^[code-15]
- **Asset folders.** `images`, `img`, `assets`, `media`, `attachments` and `files` are recognised as assets rather than as sections. ^[code-16]
- **Markdown.** `.md` and `.qmd` both count. ^[code-16]

`FrontMatter` reads at most 64 KB from the head of a document and extracts `title`, `publish`, `draft`, `author` and `date`. ^[code-18] A document is hidden when `publish` is false or `draft` is true ^[code-19], and its title falls back to the first H1 when none is declared ^[code-20].

`Coverage` encodes what is known about an article count rather than asserting a number: `None` renders as `…`, `Partial` as `≥ N`, `Complete` as `N`, with partial superseding none and complete superseding both. ^[code-11] An unknown count is therefore never rendered as zero. ^[code-12]

### The site model

`SpaceRegistry` validates that at least one space is declared, that every space has an identifier, and that every route base is a single segment. ^[code-23] Resolution is longest-route-base-wins, so a space mounted at a first segment takes precedence over the space mounted at the root. ^[code-24]

## 🔗 Dependencies

Markdig, for rendering. ^[code-17] The library declares no dependency on ASP.NET Core hosting, Azure Storage or SignalR server types — deliberately, since everything here must also run under WebAssembly. ^[code-02]

## 🧭 Design decisions

**Put the rules in the library, not in the builder.** `NavRules` is pure and shared ^[code-13,code-14,code-15], and the library is referenced by both sides ^[code-01], so the server's tree and the browser's tree cannot diverge in labelling or ordering.

**Model unknown counts explicitly.** `Coverage` exists so that "we have not looked yet" is representable. ^[code-11,code-12] Without it, an un-walked subtree would have to be rendered as zero — a wrong answer presented with full confidence.

**Cap front-matter reading.** Reading at most 64 KB bounds the cost of scoring a large document and bounds the damage a pathological file can do. ^[code-18]

## 🏗️ Physical placement

Referenced by both the host and the client rather than deployed on its own. ^[code-01]

## 🕳️ Open questions

> **Not established**: what the components under `Components/` are and what they render. The dossier records the abstractions, models and rules this library declares, but no record covers its component set. ^[gap]

> **Not established**: there is no test project covering these rules, so the label, ordering and coverage behaviours above are established from the source and not from executed cases. Unit tests for `NavRules`, `FrontMatter`, `PageLoader` and `SpaceRegistry` were sought across the solution and none was found. ^[gap]

> **Not established**: what the Mermaid Markdig extension emits. The two dossiers that record it disagree on what it even is — one records it as a repository-local file that was not read, the other as an external package that could not be read — so neither its nature nor its output is established here. ^[gap]

## 🔗 Related

- [System architecture](01-system-architecture.md) — where these rules take effect
- [The host](02-host-application.md) and [the browser client](03-browser-client.md) — the two sides that implement these abstractions
- [Reference](../06.00-reference/index.md) — the `metadata.yml` and front-matter schemas in full
