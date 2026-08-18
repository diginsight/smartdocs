---
title: "Incident A: deployment blocked by checkout, sparse-checkout and OIDC failures"
author: "Dario Airoldi"
date: "2026-08-17"
categories: [issue, deployment, github-actions, azure-oidc]
description: "A three-link fault chain that blocked the Learning Hub deployment workflow: unforwarded reusable-workflow secrets, a promisor fetch under partial clone, and a federated identity subject mismatch."
publish: false
---

# Incident A: deployment blocked by checkout, sparse-checkout and OIDC failures

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [📝 Description](#-description)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction steps](#-reproduction-steps)
- [✅ Solution implemented](#-solution-implemented)
- [✔️ Resolution status](#-resolution-status)
- [🎓 Lessons learned](#-lessons-learned)
- [🏁 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🎯 Introduction

The `01 · Deploy Learning Hub` workflow failed repeatedly while trying to read its private configuration overlay from `diginsight/smartdocs.internal`, and then — once that was fixed — failed again at Azure OIDC login.

What made this incident awkward is that it presented as **one** failure. It was three, stacked: each fix revealed the next blocker, so the workflow appeared to fail in a new way every time rather than to make progress.

**Internal companion.** The federated identity subjects, the credential state before and after, and the `az` commands as executed are in the private peer repository: `src/docs/90.00-issues/202608/20260817.01-deployfail/02-incident-a-deployment-chain.internal.md`.

## 📝 Description

### Fault chain

| # | Stage | Error signature | Immediate effect |
|---|---|---|---|
| 1 | Overlay clone | `fatal: could not read Username for 'https://github.com': terminal prompts disabled` | Checkout of the private overlay failed |
| 2 | Sparse checkout | `fatal: could not fetch ... from promisor remote` | Overlay file checkout failed after clone succeeded |
| 3 | Azure login | `AADSTS700213: No matching federated identity record found` | OIDC login failed, deployment halted |

### Operational impact

- Deployments through `01.DeployLearnHub.yml` were blocked outright.
- The environment configuration overlay could not be staged reliably.
- The Azure publish path was unreachable until identity trust conditions were corrected.

## 🔍 Context information

| Item | Value |
|---|---|
| Repository | `diginsight/smartdocs` |
| Workflow family | `00.BuildSmartDocsWeb.yml` (reusable) + callers `01`, `02`, `03` |
| Runner | Self-hosted, Windows |
| Git version observed | `2.55.0.windows.4` |
| Azure CLI observed | `2.80.0` |
| Framework target | .NET 10 (`10.0.x`) |

### Affected components

| Component | Role in the failure chain |
|---|---|
| `.github/workflows/00.BuildSmartDocsWeb.yml` | Reusable checkout/build/deploy logic |
| `.github/workflows/01.DeployLearnHub.yml` | Caller workflow that failed |
| `.github/workflows/03.PublishDocsContent.yml` | Shares the same private-overlay checkout pattern |
| Deployment app registration | OIDC trust principal for Azure login |

## 🔬 Analysis

### Root cause 1 — reusable workflow secret forwarding gap

The reusable workflow expected a token secret, but the caller workflows never forwarded secrets into the reusable invocation. GitHub Actions does **not** pass repository secrets into a reusable workflow automatically; they must be mapped explicitly through `secrets:`, or inherited where that is declared. The result was an empty token, and a non-interactive `git clone` that had nothing to authenticate with.

The failure message named a *prompt* problem, which is a symptom. The cause was an empty variable.

### Root cause 2 — partial clone plus unauthenticated promisor fetch

With secrets forwarded, the clone succeeded but sparse checkout did not. The checkout used `--filter=blob:none`, which creates a **partial clone**: blob content is not fetched up front, and the repository records a promisor remote to fetch it from on demand.

`sparse-checkout set` then needed to materialise the overlay file, which required fetching the deferred blobs — and that fetch did not reliably carry the same authentication context the original clone had. The same class of auth failure reappeared at a different stage, which is why it read as a new problem rather than a continuation.

### Root cause 3 — federated identity mismatch, on two axes at once

With checkout working, Azure login failed with `AADSTS700213`. The federated credential in Entra did not match the token the workflow presented, for two independent reasons:

- **Subject format**: the existing credential used the legacy *mutable* format (owner and repository by name), while the token presented the *immutable* format (owner and repository by numeric ID).
- **Context model**: the existing credential was scoped to a **branch** (`ref:refs/heads/main`), while the job declared an **environment**, so the token carried an `environment:` subject instead.

Either mismatch alone would have failed. Both were present, which meant a fix that corrected only one would have looked like no progress at all.

### Severity

| Dimension | Assessment |
|---|---|
| Service interruption | High — deployment pipeline unavailable |
| Scope | Deployment path only; no runtime service affected |
| Data integrity risk | Low |
| Security risk | Medium — identity trust misconfiguration |

**Severity:** High

## 🔄 Reproduction steps

1. Run `01.DeployLearnHub.yml`, which calls `00.BuildSmartDocsWeb.yml` with private overlay checkout.
2. Observe the clone fail when secrets are not forwarded into the reusable workflow.
3. Add secret forwarding and re-run.
4. Observe the sparse checkout fail under the `--filter=blob:none` promisor path.
5. Remove the blob filter and anchor the sparse path with a leading slash.
6. Re-run and observe the workflow progress as far as Azure login.
7. Observe `AADSTS700213` when the federated subject does not match the immutable, environment-scoped token subject.

## ✅ Solution implemented

### 1) Reusable workflow secret contract and caller forwarding

`00.BuildSmartDocsWeb.yml` now declares its required and optional secrets explicitly and fails early when the internal read token is missing. Callers `01` and `02` forward the required secrets.

The fail-fast check matters as much as the forwarding: it converts a confusing downstream `git` error into a named configuration error at the point of the mistake.

### 2) Overlay checkout reliability

In `00` and `03`:

- Removed `--filter=blob:none`, eliminating the deferred promisor fetch entirely.
- Added a leading slash to the sparse target (`"/$env:INTERNAL_CONFIG_PATH"`) to anchor single-file sparse selection.
- Applied the auth header on the sparse-checkout command as defensive hardening.

Removing the blob filter slightly increases the clone payload for the private configuration repository. That trade is worth taking: the repository is small, and the filter was buying a marginal saving in exchange for fragile deferred-fetch behaviour in a critical path.

### 3) Entra federated identity

Two environment-scoped federated credentials were added to the deployment app registration, using the immutable subject format — one for the Learning Hub environment, one for the docs environment.

The exact subjects and the commands used are recorded in the paired private repository at `src/docs/90.00-issues/202608/20260817.01-deployfail/02-incident-a-deployment-chain.internal.md`, not here.

## ✔️ Resolution status

- Reusable workflow secret forwarding added and validated. (✅ done)
- Private overlay sparse checkout failure eliminated. (✅ done)
- Azure OIDC trust corrected for both environments. (✅ done)
- End-to-end workflow run succeeded — run `32059829596`. (✅ done)
- Keep environment-scoped subjects for workflows declaring `jobs.<job>.environment`. (✅ done)
- Remove the stale branch-scoped credential once no consumers remain. (🟡 todo)
- Add a runbook note on immutable-subject rollout for future repositories and transfers. (🟡 todo)

## 🎓 Lessons learned

- **Multi-stage CI failures mask each other.** Trace them as a fault chain and expect the next blocker, rather than treating each new message as an unrelated problem.
- **Partial clone introduces a hidden auth boundary.** `--filter=blob:none` defers work to a later fetch that may not inherit the credentials the clone had. In a private repository on a critical path, the optimisation is rarely worth it.
- **OIDC trust must match on both axes.** Subject *format* (mutable vs immutable) and context *model* (branch vs environment) are independent; matching one and not the other fails identically to matching neither.
- **A reusable workflow is an interface.** Declare its secrets and inputs as a contract and enforce fail-fast checks, or callers will fail deep inside someone else's implementation.

## 🏁 Conclusion

Three independent faults — an unforwarded secret, a promisor fetch without credentials, and a doubly-mismatched federated identity — presented as a single recurring deployment failure. All three are resolved and the workflow completes end to end. The durable improvements are an explicit secret contract on the reusable workflow, checkout behaviour that no longer depends on deferred fetch, and environment-scoped immutable OIDC subjects.

Incident B, investigated the same day, is a separate failure in the docs-site pipeline and is analysed in [its own page](03-incident-b-docs-site-500.md).

## 📚 References

- **[GitHub Actions run 32059829596](https://github.com/diginsight/smartdocs/actions/runs/32059829596)** 📘 [Official]  
  Execution evidence for the successful Learning Hub deployment after all three fixes.

- **[OpenID Connect reference (GitHub Docs)](https://docs.github.com/en/actions/reference/security/oidc)** 📘 [Official]  
  Canonical specification for OIDC claims, immutable subject format, and environment-based subject patterns.

- **[Reusing workflows (GitHub Docs)](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows)** 📘 [Official]  
  Defines how secrets are passed to reusable workflows, and why they are not inherited by default.

- **[Microsoft Entra workload identity federation](https://learn.microsoft.com/entra/workload-id/workload-identity-federation)** 📘 [Official]  
  Trust-model guidance used to interpret and resolve `AADSTS700213`.

- **[Git partial clone documentation](https://git-scm.com/docs/partial-clone)** 📘 [Official]  
  Explains promisor remotes and the deferred object fetch that produced the second failure.
