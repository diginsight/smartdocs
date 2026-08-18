---
title: "Diginsight SmartDocs"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What this repository is, what it produces, and where to go next."
source_sets:
  - composition-root
  - entry-points
  - deployment-descriptor
  - settings-sources
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
    - dossier: "_evidence/smartdocs-web/environment.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-content/data.md"
      observed: "2026-08-18"
  open_gaps: 1
-->

# Diginsight SmartDocs

## 📚 Table of contents

- [🎯 What this repository contains](#-what-this-repository-contains)
- [🧭 How it works, in one paragraph](#-how-it-works-in-one-paragraph)
- [🗄️ Where content lives](#-where-content-lives)
- [🚀 How it ships](#-how-it-ships)
- [🗺️ Where to go next](#-where-to-go-next)
- [🕳️ Open questions](#-open-questions)

## 🎯 What this repository contains

Three .NET projects and the content they serve.

| Project | Role |
|---|---|
| `Diginsight.SmartDocs.Web` | The host. An ASP.NET Core application whose `Program.cs` composes every service, exposes the HTTP and SignalR surfaces, renders Razor components server-side and hands interactivity to WebAssembly. ^[code-01,code-05,code-18] |
| `Diginsight.SmartDocs.Web.Client` | The interactive surface the host hands off to, running under WebAssembly in the browser. ^[code-05] |
| `Diginsight.SmartDocs.Web.Shared` | Holds the types the rest of the system is written against — `SpaceRegistry`, `PageLoader`, the Markdown renderer and the front-matter model all live here. ^[code-07,code-20,code-21,code-22] |

Alongside them sits `src/docs`, the Markdown content set this repository publishes. ^[data-01]

## 🧭 How it works, in one paragraph

A request arrives for any path. The host resolves that path to a space by longest matching route base, asks that space's content source for a document, and renders the Markdown to HTML with Markdig. ^[code-07,code-09,code-20,code-21] The sidebar is not a file — a level is built by listing the children of a prefix, scoring them, and ordering them by the naming and sorting rules. ^[code-23,code-28,code-29] Nothing is read from a pre-built manifest.

## 🗄️ Where content lives

A space declares where its content comes from, and the host creates a filesystem source or a blob source accordingly. ^[code-08] In development the space declares the local filesystem. ^[environment-11] The documentation environment stores its content in an Azure Blob Storage container, which the host reaches with `DefaultAzureCredential` — in Azure, the App Service's managed identity — so no storage credential is deployed with the application. ^[environment-08,environment-10]

## 🚀 How it ships

Four GitHub Actions workflow definitions exist. ^[devops-01] One is a reusable build-and-deploy workflow invoked by `workflow_call`; two thin callers invoke it, one per deployment environment. ^[devops-02,devops-05,devops-06] A fourth publishes the content set to blob storage and then asks the running site to drop its navigation cache. ^[devops-20,devops-26]

Every value that names a real resource is deliberately absent from this repository: the settings file states that storage identity is environment-specific and arrives from an external overlay. ^[configuration-26] The workflows fetch that overlay from a private peer repository at deploy time, mask the values they read from it, and strip the deployment section before the settings file is staged into the published output. ^[devops-13,devops-15]

## 🗺️ Where to go next

| If you want to | Go to |
|---|---|
| Build and run it locally | [Getting Started](02.00-getting-started/index.md) |
| Understand how the pieces fit | [Architecture](03.00-architecture/index.md) |
| See what it is used for | [Use Cases](04.00-use-cases/index.md) |
| Look up a setting, an endpoint or a schema | [Reference](06.00-reference/index.md) |
| Understand the deployed environments | [Infrastructure](05.00-infrastructure/index.md) |
| Understand the pipelines | [DevOps](10.00-devops/index.md) |
| Understand the security posture | [Security](09.00-security/index.md) |
| Know what is actually verified | [Validation](08.00-validation/index.md) |
| See the AI-customization artifacts | [Other Components](07.00-other-components/index.md) |
| Read the supporting material | [Appendix](11.00-appendix/index.md) |

## 🕳️ Open questions

> **Not established**: no infrastructure definition exists in this repository. Searches for Bicep, ARM, Terraform, Pulumi, Dockerfile and `azure.yaml` files all returned zero results, so the resources the pipelines deploy to are provisioned somewhere this repository does not describe. ^[gap]
