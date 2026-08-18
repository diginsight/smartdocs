---
title: "Validation"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What is checked automatically, and what is not."
source_sets:
  - test-surface
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
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web-client/configuration.md"
      observed: "2026-08-18"
  open_gaps: 5
-->

# Validation

## 🎯 Introduction

What this repository checks automatically, where those checks run, and — at least as importantly — what they leave unchecked.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Deployment smoke check](01-deployment-smoke-check.md) | The one check that requests a response from a running instance |
| [Content publish checks](02-content-publish-checks.md) | The guards applied before content reaches storage |

## 🔑 Key points

- **The compiler is the strictest gate.** Every project compiles against the same `Directory.Build.props`, which fixes the .NET 10 target, `LangVersion 13` and **nullable-as-error** — so a null-safety mistake fails the build rather than surfacing at runtime. ^[smartdocs-web-client/configuration-05]
- **Two automated checks exist, both inside pipelines.** One HTTP smoke check closes every deployment ^[devops-19]; three presence-and-count checks run before content is published ^[devops-22,devops-23].
- **Neither check exercises rendering or navigation.** The smoke check asserts only an HTTP 200 from the deployed host ^[devops-19]; the publish checks assert only that the destination is fully declared and that the staged Markdown count is non-zero ^[devops-22,devops-23].
- **The application ships test-only endpoints.** `POST /_test/article`, `DELETE /_test/article` and `GET /_nav/metrics` are mapped only when `Testing:ContentMutationEnabled` is true; when it is false the routes do not exist. ^[smartdocs-web/code-17]
- **Deployment is triggered, not gated.** Workflows 01 and 02 run on a push to `main` under the project paths, or on manual dispatch ^[devops-07]; workflow 03 runs on a push to `main` under `src/docs/**`, or on manual dispatch ^[devops-20]. No workflow declares a manual approval or review gate inside its job definition. ^[devops-27]

## 🕳️ Open questions

> **Not established**: whether any automated test asserts application behaviour. A test project, test-framework reference or test-discovery entry was sought across the solution and the tree — `src/Diginsight.SmartDocs.slnx` was enumerated and `*Test*.csproj`, `*Tests*` directories and test-framework package references were searched for. None was found. ^[gap]

> **Not established**: whether the behaviours documented in the [Reference](../06.00-reference/index.md) chapter — label derivation, sort ordering, folder classification, front-matter defaults, coverage merging — behave as declared. Unit tests for `NavRules`, `FrontMatter`, `PageLoader` and `SpaceRegistry` were sought specifically and not found, so those pages are established from source rather than from executed cases. ^[gap]

> **Not established**: what consumes the test-only endpoints. The routes are declared ^[smartdocs-web/code-17]; no caller, suite or harness for them was found in this repository. ^[gap]

> **Not established**: whether any change is built or checked before it is merged. Every trigger block in `.github/workflows/` was read; all four workflows trigger on `push` to `main` or `workflow_dispatch` only, and no `pull_request`-triggered workflow was found. ^[gap]

> **Not established**: whether any manual validation procedure exists. No validation-sequence artifact, test plan or checklist was found under the searched roots. ^[gap]

## 🔗 Related

- [DevOps](../10.00-devops/index.md) — where these two checks sit in the pipelines
- [Reference](../06.00-reference/index.md) — the behaviours that currently have no executed coverage
- [Security posture](../09.00-security/01-security-posture.md) — the controls that are declared rather than tested
