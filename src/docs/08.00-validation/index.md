---
title: "Validation"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What is checked automatically, and what is not."
source_sets:
  - test-surface
  - pipeline-definition
  - release-gates
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-11-02"
  verified: "2026-11-02"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-11-02"
    - dossier: "_evidence/smartdocs-web-shared/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web-client/configuration.md"
      observed: "2026-08-18"
  open_gaps: 5
-->

# Validation

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🗺️ Pages in this section](#-pages-in-this-section)
- [🔑 Key points](#-key-points)
- [🧪 Recorded reader-access validation](#-recorded-reader-access-validation)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)
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

## 🧪 Recorded reader-access validation

| Existing local run | Recorded result | Boundary |
|---|---|---|
| Sequence 01 | PASS: anonymous surface filtering and concealment | No signed-in behavior or composed-count proof ^[smartdocs-web/code-57] |
| Sequence 02 | PASS: anonymous visible-only totals, hidden-change suppression, visible-change push control and snapshot restart | Not signed-in cross-group isolation or expiry ^[smartdocs-web/code-58] |
| Sequence 03 | PASS after fixes: sign-in/bootstrap, space-local role union, changed mappings, sign-out and responsive menu | Not hub expiry, lifetime-bound revocation or all F scenarios ^[smartdocs-web/code-59] |
| Sequence 04 (2026-10-28) | FAIL at authentication preflight; superseded by the 2026-11-02 run | F1–F6 **NOT RUN**; DS-4 unresolved ^[smartdocs-web/code-60] |
| Sequence 04 (2026-11-02) | PASS: F1 pushes per role set, F2 revocation with DS-4 hub expiry resolved positively, F3 cache headers, F4 byte-identical anonymous regression, F5 fail-closed access files, F6 mount-route divergence after one fix, newest dates and branding | Local host, fixture tree, development sign-in registration and one test account; no deployed environment or phone widths ^[smartdocs-web/code-63,smartdocs-web/code-64,smartdocs-web/code-65,smartdocs-web/code-66,smartdocs-web/code-67,smartdocs-web/code-68,smartdocs-web/code-69,smartdocs-web/code-70] |

These are manual fixture observations on a local host, not tests run by this documentation refresh. See the [implementation plan](../90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md) and its validation records. The 2026-10-28 sequence 04 stopped before any scenario ran and is kept as history; the 2026-11-02 sequence 04 supersedes it. ^[smartdocs-web/code-60,smartdocs-web/code-63] During that run, a hidden space mounted at the site root was found to remove the root's Home link for readers who couldn't see it; the navigation filter was fixed during the run and the scenario passed afterwards. ^[smartdocs-web/code-62,smartdocs-web/code-68]

## 🕳️ Open questions

> **Not established**: whether any automated test asserts application behaviour. A test project, test-framework reference or test-discovery entry was sought across the solution and the tree — `src/Diginsight.SmartDocs.slnx` was enumerated and `*Test*.csproj`, `*Tests*` directories and test-framework package references were searched for. None was found. ^[gap]

> **Not established**: whether the behaviours documented in the [Reference](../06.00-reference/index.md) chapter — label derivation, sort ordering, folder classification, front-matter defaults, coverage merging — behave as declared. Unit tests for `NavRules`, `FrontMatter`, `PageLoader` and `SpaceRegistry` were sought specifically and not found, so those pages are established from source rather than from executed cases. ^[gap]

> **Not established**: a discoverable automated harness for the test-only endpoints. Manual fixture procedures exist, but aren't an automated regression suite. ^[gap]

> **Not established**: whether any change is built or checked before it is merged. Every trigger block in `.github/workflows/` was read; all four workflows trigger on `push` to `main` or `workflow_dispatch` only, and no `pull_request`-triggered workflow was found. ^[gap]

> **Not established**: reader-access behaviour in a deployed environment. Every recorded sequence ran on a local host with a fixture tree, a development sign-in registration and one test account; no deployed-environment run, certificate-based code-flow sign-in or phone-width check was recorded, and whether the sign-in registrations are deployed to production isn't established. ^[gap]

## 🔗 Related

- [DevOps](../10.00-devops/index.md) — where these two checks sit in the pipelines
- [Reference](../06.00-reference/index.md) — the behaviours that currently have no executed coverage
- [Security posture](../09.00-security/01-security-posture.md) — the controls that are declared rather than tested
