---
title: "Use Cases"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What Diginsight SmartDocs is actually used to do."
source_sets:
  - entry-points
  - deployment-descriptor
  - domain-model
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-content/data.md"
      observed: "2026-08-18"
  open_gaps: 1
-->

# Use Cases

## 🎯 Introduction

What people do with this system, step by step, and what each step can be observed doing.

One deploy run is captured in this repository, and it records the step sequence the build-and-deploy job actually executed. ^[devops-28] Every other flow below is reconstructed from the source and the pipeline definitions rather than from a recorded execution.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Reading a document](01-reading-a-document.md) | A reader opens a URL and gets a rendered page |
| [Browsing the navigation tree](02-browsing-the-navigation-tree.md) | The sidebar is discovered level by level, with counts that improve |
| [Publishing content](03-publishing-content.md) | Markdown merged to `main` becomes the live content set |
| [Deploying the application](04-deploying-the-application.md) | An application change reaches a running environment |
| [Running against the private overlay](05-running-against-the-private-overlay.md) | A developer runs locally with an environment's real settings |

## 🔑 Key points

- **Publishing and deploying are separate.** They are different workflows with different triggers: the two deployment workflows fire on pushes touching the three project folders and the shared build files, while the content workflow fires on pushes touching `src/docs`. ^[devops-07,devops-20] Publishing uploads blobs and then posts a cache-invalidation request ^[devops-24,devops-26]; deployment pushes a zip package to App Service ^[devops-12].
- **Publishing mirrors rather than appends.** Blobs absent from the source are removed after a successful upload, so deleting a file removes a page. ^[data-14]
- **Every flow fails closed at configuration.** Startup throws when the `Site` section is absent ^[code-06]; the deployment workflow fails when the overlay declares no `Site.Spaces` or no deployment target ^[devops-16]; the publish workflow fails when the target space is not blob-backed or is missing its account or container ^[devops-22].
- **The smoke check is the only automated observation of a running instance.** It requests the site and expects HTTP 200, up to ten times. ^[devops-19]

## 🕳️ Open questions

> **Not established**: no automated test surface exists for any of these flows. A test project, test-framework reference and test discovery entry were all sought across the solution and none was found, so every behaviour described in this chapter is established from source and pipeline definitions rather than from executed cases. ^[gap]

## 🔗 Related

- [Architecture](../03.00-architecture/index.md) — the mechanisms behind these flows
- [DevOps](../10.00-devops/index.md) — the workflows two of these flows run through
- [Validation](../08.00-validation/index.md) — what is and is not verified
