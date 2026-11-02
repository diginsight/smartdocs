---
title: "Publishing content"
author: "Dario Airoldi"
date: "2026-08-18"
description: "An author merges Markdown to main and it becomes live without a build."
source_sets:
  - deployment-descriptor
  - settings-sources
  - entry-points
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-content/data.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
  open_gaps: 2
-->

# Publishing content

## 📚 Table of contents

- [🎯 Goal](#-goal)
- [✅ Preconditions](#-preconditions)
- [🔬 Flow](#-flow)
- [🔀 Alternate and failure paths](#-alternate-and-failure-paths)
- [🧪 What proves it](#-what-proves-it)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)
## 🎯 Goal

- **Actor**: an author, via a merge to `main`
- **Outcome**: the content set in `src/docs` becomes the content the deployed site serves
- **Component**: `smartdocs-content`, `smartdocs-web`

## ✅ Preconditions

- The change touches `src/docs/**`, or the workflow is dispatched manually. ^[devops-20]
- The target space declares `Source: Blob` with both an account URI and a container name resolvable from the private overlay; the workflow refuses otherwise. ^[devops-22]
- The staged content contains at least one Markdown file; the workflow fails when it counts zero. ^[data-15]

## 🔬 Flow

1. A push to `main` touching `src/docs/**` triggers *03 · Publish docs content*. ^[devops-20] Concurrency is serialised under `publish-docs-content`, without cancelling a run in progress. ^[devops-08]
2. The workflow checks out this repository and then sparse-checks-out the configuration overlay from the private peer repository, authenticated with a read token; the step fails when that secret is absent. ^[devops-13]
3. It resolves the publish destination from the overlay: the space must declare `Source: Blob`, and both `Blob.AccountUri` and `Blob.ContainerName` must be present. The storage account name, container name and web-app name are all masked before use. ^[devops-22]
4. It stages the content, removing `bin`, `obj` and `node_modules`, and counts Markdown and image files. Zero Markdown files fails the run. ^[devops-23] ^[data-15]
5. It authenticates to Azure with an OIDC federated credential. No client secret is stored. ^[devops-14]
6. It ensures the container exists and uploads the staged content with `--overwrite true` and `--auth-mode login`; no storage key or connection string is used. ^[devops-24]
7. **After a successful upload**, it removes blobs present in the container but absent from the source — a case-insensitive set difference, deleted one at a time. ^[devops-25] The result is a mirror, not an append. ^[data-14]
8. It posts to `/_nav/invalidate` on the site, carrying `X-Invalidate-Key`, with a sixty-second timeout. ^[devops-26]
9. That endpoint invalidates navigation. ^[code-15] Page reads authorize the exact content key selected by the rendered-page cache before returning it; shared cached content facts don't bypass per-reader filtering. ^[code-20,code-48]

### Publishing a space's access policy

The root `access.metadata.yml` is part of the content-space policy, not navigation metadata. The host reads it through the physical source but excludes it from public content reads and listings at every depth. A nested copy doesn't create folder permissions. ^[smartdocs-web/security-29] Its parsed facts have a default 30-second freshness; reader requirements and grants aren't cached as shared principal-specific results. ^[code-48]

Treat access changes as a policy rollout: validate the root file before publishing, retain any deployment floor, and don't apply deployed restrictions until the [roles-and-permissions plan](../90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md) meets every exit criterion. Invalid or required-but-missing files hide the space. ^[smartdocs-web/security-25] See the [schema](../06.00-reference/07-access-file-schema.md) and [provisioning how-to](../90.00-issues/202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md).

## 🔀 Alternate and failure paths

| Situation | What happens |
|---|---|
| The space is not blob-backed, or is missing an account or container | The workflow fails before touching storage. ^[devops-22] |
| The staged content has no Markdown | The workflow fails rather than publishing an empty site. ^[data-15] |
| The upload fails | Stale-blob removal does not run — deletion is ordered strictly after a successful upload. ^[devops-25] |
| The invalidation call fails or times out | The run still succeeds. The call is best-effort with a 60-second timeout. ^[devops-26] |
| A file was deleted from `src/docs` | Its blob is removed by the mirror step. ^[data-14] |

## 🧪 What proves it

Two checks inside the workflow itself: the destination resolution refuses a misconfigured space ^[devops-22], and the content count refuses an empty publish ^[data-15]. The invalidation call that closes the run is explicitly best-effort — its failure does not fail the run — so the run's success does not assert that the site is serving the new content. ^[devops-26]

## 🕳️ Open questions

> **Not established**: whether the invalidation call succeeds in the target environment. It is best-effort and its outcome is not asserted, so a silent failure would leave cached navigation in place with nothing in the run to signal it. ^[gap]

> **Not established**: whether the environment adequately configures management protection for invalidation. The deploy step depends on an optional secret whose presence isn't recorded by this repository. ^[gap]

## 🔗 Related

- [Publish content pipeline](../10.00-devops/04-publish-content-pipeline.md) — the publish workflow in full
- [Caching and invalidation](../03.00-architecture/05-caching-and-invalidation.md) — what the invalidation call actually drops
- [testmcdocs environment](../05.00-infrastructure/02-testmcdocs-environment.md) — the environment this publishes into
