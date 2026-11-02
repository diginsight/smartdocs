---
title: "Getting Started"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What you need, how to build it, how to run it, and what you will see."
source_sets:
  - composition-root
  - settings-sources
  - options-model
  - pipeline-definition
  - deployment-descriptor
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/environment.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-client/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-client/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-10-28"
  open_gaps: 2
-->

# Getting Started

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🧰 What you need](#-what-you-need)
- [🏗️ Build](#-build)
- [▶️ Run](#-run)
- [🎚️ Run profiles](#-run-profiles)
- [📂 Where your content goes](#-where-your-content-goes)
- [🧭 What you will see](#-what-you-will-see)
- [🔗 Related](#-related)
- [🕳️ Open questions](#-open-questions)
## 🎯 Introduction

Build and run Diginsight SmartDocs locally, then choose a public-content or reader-access profile.

## 🧰 What you need

| Requirement | Established by |
|---|---|
| .NET SDK 10 | the pipeline verifies that the runner's pre-installed SDK matches `10.*` and fails otherwise ^[devops-09] |
| The repository's `nuget.config` | the deploy workflows treat `nuget.config` at the repository root as a build input and retrigger when it changes ^[devops-07] |
| Nothing else for the default profile | the `Development` overlay declares the local filesystem as the content source, so no cloud resource is required ^[environment-11] |

Every project builds through the same `Directory.Build.props`, which fixes the .NET 10 target, `LangVersion 13`, nullable-as-error and strong-name signing. ^[smartdocs-web-client/configuration-05] A warning you might ignore elsewhere will stop the build here.

## 🏗️ Build

```powershell
dotnet build src/Diginsight.SmartDocs.slnx
```

Three projects build: the ASP.NET Core host ^[smartdocs-web/code-01], the Blazor WebAssembly client ^[smartdocs-web-client/code-01] and the Razor Class Library both of them reference ^[smartdocs-web-shared/code-01].

## ▶️ Run

```powershell
dotnet run --project src/Diginsight.SmartDocs.Web
```

Two things are worth knowing before you do.

**The client ships inside the host.** The client is a Blazor WebAssembly project ^[smartdocs-web-client/code-01] whose output the host serves through static assets ^[smartdocs-web/code-38]; the host adds the client assembly to the router, which is what makes the client's routes reachable at all ^[smartdocs-web-client/code-10].

**Run it in a visible console.** The host declares the console sink enabled by default — `Observability:ConsoleEnabled` is `true`. ^[smartdocs-web/configuration-15] You will want to see it, and to stop it.

## 🎚️ Run profiles

The profiles below select different settings overlays; the access profile is local validation, not a deployed-environment access configuration. ^[smartdocs-web/configuration-23,smartdocs-web/configuration-24,smartdocs-web/configuration-39]

| Profile | `ASPNETCORE_ENVIRONMENT` | `AppsettingsEnvironmentName` | Content comes from |
|---|---|---|---|
| `http` | `Development` | `Development` | the local filesystem ^[smartdocs-web/configuration-23] ^[environment-11] |
| `https` | `Development` | `Development` | the local filesystem ^[smartdocs-web/configuration-23] ^[environment-11] |
| `https - Testmc` | `Development` | `Testmc` | whatever the private overlay declares ^[smartdocs-web/configuration-24] |
| `https - access` | `Development` | `access` | the private fixture overlay, using the repository's access-tree content ^[smartdocs-web/configuration-39] |

The third profile also sets `ExternalConfigurationFolder` to `..\..\..\smartdocs.internal`, meaning it expects the private peer repository checked out **beside** this one. ^[smartdocs-web/configuration-24] Without that checkout the profile will not find its overlay.

The access profile uses the same external-root convention. ^[smartdocs-web/configuration-39] Run it from the host project in a visible console, rebuilding rather than using `--no-build`:

```powershell
dotnet run --launch-profile "https - access"
```

Nonblank `AzureAd:ClientId` enables sign-in; an empty value leaves the host anonymous-only. Requirements still apply when sign-in is off, so a restricted space can be invisible to everyone until sign-in is configured. ^[smartdocs-web/configuration-28,smartdocs-web/code-47] See the [reader-access provisioning guide](../90.00-issues/202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md).

## 📂 Where your content goes

In the `Development` profile the space declares `Source: FileSystem` with a root path of `..\docs` and `WatchForChanges: true`. ^[environment-11]

A file becomes a page when it is Markdown — `.md` and `.qmd` are both accepted ^[smartdocs-web/code-30] — and its front matter does not mark it hidden. A file is hidden when it declares `publish: false` or `draft: true`. ^[smartdocs-web/code-22] A file or folder whose name starts with `_` or `.` is never enumerated into navigation at all. ^[smartdocs-web/code-24]

## 🧭 What you will see

`PageLoader` accepts a handled rendered-page resolution first. Its fallback candidate ladder applies only when resolution is unhandled; a hidden result doesn't trigger another candidate. ^[smartdocs-web-shared/code-21,smartdocs-web-shared/code-33] The host authorizes the exact selected content key before returning the page. ^[smartdocs-web/code-20]

The sidebar is built from whatever is on disk, one level at a time. ^[smartdocs-web/code-23] Numeric prefixes on folder names are stripped from the displayed label ^[smartdocs-web/code-28]; dated names sort newest-first and everything else sorts alphabetically ^[smartdocs-web/code-29].

## 🔗 Related

- [Architecture](../03.00-architecture/index.md) — how the pieces fit together
- [Reference](../06.00-reference/index.md) — the settings, endpoints and schemas named above
- [DevOps](../10.00-devops/index.md) — how this same build runs in the pipeline

## 🕳️ Open questions

> **Not established**: whether anything verifies a local build beyond the compiler. A test project, a test-framework reference and a test discovery entry were all sought across the solution and none was found, so no `dotnet test` step exists. ^[gap]

> **Not established**: the `Development` overlay declares a filesystem root of `..\docs`, but this repository's content set lives at `src/docs`. Whether a `docs` folder is expected beside the project, or whether the path is meant to be overridden in `appsettings.local.json`, is not stated anywhere that was read. ^[gap]
