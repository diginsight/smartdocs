---
title: "Content publish checks"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The guards the content publishing workflow applies before it touches storage."
source_sets:
  - pipeline-definition
  - release-gates
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
  open_gaps: 2
-->

# Content publish checks

## 🎯 What it proves

That the destination the content is going to is fully declared ^[devops-22], and that the content about to be published is non-empty ^[devops-23].

## 📋 Covered behaviours

| Check | Failure condition |
|---|---|
| The target space is blob-backed | The space looked up by `space-id` does not declare `Source: Blob` ^[devops-22] |
| The destination is complete | `Blob.AccountUri` or `Blob.ContainerName` is missing ^[devops-22] |
| The staged content is non-empty | Zero Markdown files after staging ^[devops-23] |

Staging copies the source path into a temporary directory, removes `bin`, `obj` and `node_modules`, and counts Markdown (`.md`, `.qmd`) and image files before proceeding. ^[devops-23]

## 🚫 What it does not prove

The three checks above are the whole guard set. ^[devops-22,devops-23] They therefore do not establish:

- **That any document is well-formed.** No Markdown is parsed and no front matter is validated by the workflow.
- **That links resolve.** Nothing in the workflow reads a cross-document reference.
- **That the content renders.** The application is never asked to render any of it.
- **That removing stale blobs is safe in every case.** Removal computes a case-insensitive set difference against the local file set after a successful upload, so a file that failed to stage would have no local counterpart and would be deleted. ^[devops-25]
- **That readers see the change.** The invalidation call is best-effort and its failure does not fail the run. ^[devops-26]

## ▶️ How to run it

Automatically, on a push to `main` touching `src/docs/**` or the workflow file itself; or manually, choosing a space id, a source path and an overlay file. ^[devops-20]

## 🔗 Dependencies

The private configuration overlay, fetched by a sparse clone at run time ^[devops-13], which supplies the storage account and container the destination check reads ^[devops-22].

## 🚦 Where it gates

Before the upload. A failed check stops the run without touching storage. ^[devops-22,devops-23]

## 🕳️ Open questions

> **Not established**: whether content is reviewed before it is published. The trigger is a push to `main` or a manual dispatch ^[devops-20], and no workflow declares a manual approval or review gate inside its job definition ^[devops-27]. Whether a review happens outside the repository could not be determined from it. ^[gap]

> **Not established**: whether the two GitHub environments carry protection rules such as required reviewers, wait timers or branch restrictions. The workflows reference the environments by name; protection rules are repository settings rather than workflow content. ^[gap]

## 🔗 Related

- [Publish content pipeline](../10.00-devops/04-publish-content-pipeline.md) — the workflow these checks belong to
- [Publishing content](../04.00-use-cases/03-publishing-content.md) — the same flow from an author's point of view
- [Validation](index.md) — what else is and is not checked
