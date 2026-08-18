---
title: "SmartDocs deployment failures, 2026-08-17"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [issue, deployment, github-actions, azure-oidc, networking]
description: "Two unrelated deployment incidents on the same day: a three-link CI authentication and OIDC fault chain, and a docs site that served HTTP 500 because it had no network path to its content store."
publish: false
---

# SmartDocs deployment failures, 2026-08-17

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🗂️ The two incidents](#-the-two-incidents)
- [⏱️ Combined timeline](#-combined-timeline)
- [🎓 What carried across both](#-what-carried-across-both)
- [🔴 The open item](#-the-open-item)
- [📎 Appendix](#-appendix)
- [🏁 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🎯 Introduction

Two deployment incidents occurred in the SmartDocs pipeline on 2026-08-17. They shared a day and a pipeline family, and nothing else — different subsystems, different root causes, different fixes. They are analysed separately because merging them obscures both.

This page is the entry point: it says which incident is which, how they were sequenced, and what generalises beyond either one.

**Naming convention.** These pages name Azure resources by **role** — "the docs app service", "the shared app subnet" — never by resource name. Resource names, address ranges and identity identifiers are deliberately absent because this repository is public. Readers with access to the paired private repository can resolve every alias through its `testmc` alias registry.

## 🗂️ The two incidents

| | [Incident A](incident-a-deployment-chain.md) | [Incident B](incident-b-docs-site-500.md) |
|---|---|---|
| **Workflow** | `01 · Deploy Learning Hub` | `02 · Deploy docs site` |
| **Symptom** | Workflow failed, in a new place each time | Workflow deployed successfully, then smoke check got ten HTTP 500s |
| **Layer** | CI authentication and identity federation | Azure networking |
| **Root cause** | Three stacked faults: unforwarded reusable-workflow secrets, a promisor fetch without credentials, and a federated subject mismatched on two axes | The App Service had no VNet integration, so it could not reach its network-restricted content store |
| **Fix location** | Repository — workflow files and Entra credentials | Live environment only — **not** in any file |
| **Status** | ✅ Resolved | ✅ Symptom resolved, 🔴 cause still reproducible |

A third page, [the environment reference](environment-reference.md), documents the `testmc` network topology mapped during incident B. It is written as a standing reference rather than as incident narrative, because the constraints it records — chiefly the two-subnet-per-plan integration limit — apply to every future deployment in that environment.

### Why they read as one incident at the time

Incident A had to be fully resolved before incident B could even be observed: until OIDC login worked, the docs-site workflow never reached the deploy step that exposed the missing network path. So the two appeared as one long sequence of "the deployment is still broken", when in fact the first problem was hiding the second.

## ⏱️ Combined timeline

| # | Observable state | Resolution |
|---|---|---|
| 1 | Clone failed with prompts disabled | Secret forwarding contract added to the reusable workflow |
| 2 | Sparse checkout failed on promisor fetch | Blob filter removed, sparse path anchored |
| 3 | `AADSTS700213` OIDC mismatch | Environment-scoped immutable federated credentials added |
| 4 | Learning Hub workflow green | Verified — run `32059829596` |
| 5 | Docs-site smoke check: ten consecutive HTTP 500s | Traced to `AuthorizationFailure` while listing blob content |
| 6 | RBAC audit found the role grant already correct | Ruled out identity as the cause |
| 7 | Content store denies public access; app has no VNet integration | Actual root cause identified |
| 8 | Dedicated third subnet rejected — plan at its two-subnet limit | Reused the existing shared subnet instead |
| 9 | Docs app integrated and restarted | Both routes returned HTTP 200 |
| 10 | Run `32062547792` rerun, attempt 2 | Verified green, including smoke check |

## 🎓 What carried across both

Three lessons generalise past either incident. The incident-specific ones are on their own pages.

- **A fault chain is not a fault.** In both incidents, fixing the visible problem revealed the next one, and each new error message read as a regression rather than as progress. Expect the next blocker instead of re-litigating the last.
- **The error message names the layer that noticed, not the layer that failed.** "Terminal prompts disabled" was an empty variable. `AuthorizationFailure` was a missing network route. In both cases the message pointed confidently at the wrong thing, and the correct-looking evidence next to it — a valid credential, a correct RBAC grant — cost real time to re-verify.
- **Green steps are not a working system.** Every step of the docs-site deployment passed while the deployed application failed on every request. Deployment success and service health are separate claims and need separate measurements.

## 🔴 The open item

Incident B's fix — the VNet integration that made the docs site work — was applied imperatively and is declared in **no file**. The repository contains no Bicep, no Terraform, and no network configuration in any workflow.

Consequently:

- Recreating the docs app restores the broken configuration.
- No drift detection is possible, because there is no intended state to compare against.
- These documents are the only record, and prose cannot be applied or diffed.

The symptom is closed. The condition that produced it — apps provisioned by hand, without their required network configuration — is unchanged. Declaring this environment as code is the highest-value follow-up from either incident; details are in [incident B's resolution status](incident-b-docs-site-500.md#-resolution-status).

## 📎 Appendix

### Files changed during remediation

- `.github/workflows/00.BuildSmartDocsWeb.yml`
- `.github/workflows/01.DeployLearnHub.yml`
- `.github/workflows/02.DeployDocsSite.yml`
- `.github/workflows/03.PublishDocsContent.yml`

### Infrastructure changes applied, tracked in no file

- Created, then removed, a dedicated delegated subnet after the platform rejected it as a third integration for the shared plan.
- Added the docs app's regional VNet integration to the existing shared app subnet.
- Restarted the docs app to apply the new network configuration.
- No storage network rule, role assignment, or private endpoint was created, removed, or relaxed.

## 🏁 Conclusion

Two independent failures — a three-link CI authentication chain and a missing network path — presented as a single stalled deployment because the first concealed the second. Both are resolved, and both produced durable improvements: an explicit secret contract and environment-scoped OIDC federation on one side, restored private connectivity achieved through subnet reuse on the other.

One material gap remains. The network fix lives only in the running environment, so the incident is closed while its cause is not. Read [incident A](incident-a-deployment-chain.md) for the CI and identity analysis, [incident B](incident-b-docs-site-500.md) for the networking analysis, and [the environment reference](environment-reference.md) for the topology and constraints that apply to the next deployment.

## 📚 References

- **[GitHub Actions run 32059829596](https://github.com/diginsight/smartdocs/actions/runs/32059829596)** 📘 [Official]  
  Execution evidence for the successful Learning Hub deployment that closed incident A.

- **[GitHub Actions run 32062547792, attempt 2](https://github.com/diginsight/smartdocs/actions/runs/32062547792/attempts/2)** 📘 [Official]  
  Execution evidence for the docs-site deployment that closed incident B, including the passing smoke check.

- **[OpenID Connect reference (GitHub Docs)](https://docs.github.com/en/actions/reference/security/oidc)** 📘 [Official]  
  Canonical specification for the OIDC subject claims at the centre of incident A.

- **[Integrate your app with an Azure virtual network](https://learn.microsoft.com/azure/app-service/overview-vnet-integration)** 📘 [Official]  
  Regional VNet integration and the per-plan subnet limit at the centre of incident B.
