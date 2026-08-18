---
title: "Incident B: docs site returned HTTP 500 after a successful deployment"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [issue, deployment, azure, networking, app-service, storage]
description: "A deployment that succeeded on every step but served HTTP 500 on every request, because the App Service had no network path to its privately secured content store."
publish: false
---

# Incident B: docs site returned HTTP 500 after a successful deployment

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [📝 Description](#-description)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction steps](#-reproduction-steps)
- [✅ Solution implemented](#-solution-implemented)
- [🧪 Verification](#-verification)
- [✔️ Resolution status](#-resolution-status)
- [🎓 Lessons learned](#-lessons-learned)
- [🏁 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🎯 Introduction

Once [incident A](02-incident-a-deployment-chain.md) was resolved, `02 · Deploy docs site` ran cleanly: checkout, build, overlay staging, Azure login and deploy all succeeded. Then the smoke check received ten consecutive HTTP 500 responses and failed the run.

The App Service was `Running`. The deployment was correct. The application failed on **every** request, and the reason turned out to have nothing to do with anything the workflow did.

**Naming convention.** Resources are named by role, not by resource name — see [the environment reference](04-environment-reference.md).

**Internal companion.** The content store's verified state, the HTTP 409 conflict detail, the commands as executed and the validation URLs are in the private peer repository: `src/docs/90.00-issues/202608/20260817.01-deployfail/03-incident-b-docs-site-500.internal.md`.

## 📝 Description

### Error signatures

| Stage | Error signature | Immediate effect |
|---|---|---|
| Smoke check | `Response status code does not indicate success: 500 (Internal Server Error)` on all 10 attempts | Workflow reported `Site did not return HTTP 200 after deployment` and exited 1 |
| Application log | `Azure.RequestFailedException` / `ErrorCode: AuthorizationFailure` / `Status: 403` | Blazor prerender threw while listing blob content, surfacing as HTTP 500 |

### Operational impact

- The docs-site workflow reported a failed run despite a wholly successful deployment.
- The documentation host answered every request with an unhandled-exception page.
- Diagnosis required separating an authorization failure from a **network reachability** failure — which, as it turned out, are indistinguishable from the error message alone.

## 🔍 Context information

### State at the time of failure

| Fact | Value |
|---|---|
| Failing workflow | `02 · Deploy docs site`, run `32062547792` |
| Failing component | Docs app service |
| Managed identity RBAC | `Storage Blob Data Reader`, scoped to the exact container the space configuration names — **correct from the start** |
| Content store public network access | `Disabled`, network `defaultAction: Deny` |
| Content store private endpoints | Two, both Approved (blob, table) |
| Docs app VNet integration | **None** |
| Shared plan integration slots | 2 of 2 already in use |

### Provenance caveat

The `AuthorizationFailure` stack was read from a **local** log folder while reproducing the fault on a workstation, which fails the same way for the same underlying reason — no private network path. This observation is therefore **corroborated**, not established: the App Service's own log stream was not independently read. The conclusion is well supported by the configuration evidence above and by the fix resolving the symptom, but the log attribution itself should be treated as inference.

## 🔬 Analysis

### Root cause — no network path to a privately secured content store

The docs app's managed identity already held the correct role, scoped correctly. RBAC was never the problem.

The content storage account had public network access disabled and accepted traffic only through its own private endpoints. The docs app had **no regional VNet integration at all**, so every blob call left the app over the public internet path, was rejected by the storage account's network rules, and returned as `AuthorizationFailure`.

The decisive evidence is that the **same** storage account also backs the Learning Hub space through a different container, and the Learning Hub app was reading from it successfully throughout. One caller worked and one did not, against a single store with a single set of network rules. The difference was entirely on the caller's side: the Learning Hub app had VNet integration and the docs app did not.

That is the trap:

> A network-layer denial and a role-layer denial surface to the caller as the **same** error shape. `AuthorizationFailure` does not distinguish "you may not do this" from "you cannot get here from there."

Because the role assignment was visibly correct, the error message pointed away from the actual cause. Every minute spent re-checking RBAC was a minute spent confirming something that was already right.

### Why the first fix attempt failed

The obvious remediation — give the docs app its own dedicated subnet — was rejected by the platform with an HTTP 409 conflict: the shared plan was already using both of its permitted VNet integration subnets.

The resolution was to reuse an **existing** subnet that was already delegated to `Microsoft.Web/serverFarms`, already inside the VNet holding the content store's private endpoints, and already shared by two other apps on the same plan. Adding an app to a subnet the plan already counts consumes **no** additional slot.

The dedicated subnet created during the failed attempt was deleted rather than left orphaned.

### Severity

| Dimension | Assessment |
|---|---|
| Service interruption | High — every request to the deployed docs site returned HTTP 500 |
| Scope | Single App Service; no sibling on the shared plan affected |
| Data integrity risk | None — read-only content path, nothing written or lost |
| Security risk | Low — a network isolation control correctly rejecting an unauthorised path |

**Severity:** High

## 🔄 Reproduction steps

1. Deploy an App Service whose content source is a storage account with public network access disabled.
2. Grant the app's managed identity the correct container-scoped data role.
3. Do **not** configure regional VNet integration for the app.
4. Request `/`; observe HTTP 500 rather than a permissions error.
5. Inspect the application log; find `AuthorizationFailure` raised while listing blob content during prerender.
6. Confirm the RBAC grant is correct — ruling out the apparent cause.
7. Confirm the storage account denies public network access and the app has no VNet integration — the actual cause.
8. Attempt to integrate a new dedicated subnet; observe rejection if the plan is at its two-subnet limit.
9. Integrate the app with an existing delegated subnet in the same VNet, restart, and re-request `/`; observe HTTP 200.

## ✅ Solution implemented

**No repository or workflow file changed.** This was an infrastructure change applied directly against the running environment:

1. Confirmed the managed identity's RBAC grant was already correct, ruling out an identity fix.
2. Confirmed the content store's network rules and its two Approved private endpoints.
3. Attempted a dedicated subnet integration; the platform rejected it as a third subnet on a plan limited to two. Removed the subnet rather than leaving it orphaned.
4. Integrated the docs app with the existing shared app subnet instead — no new plan slot consumed.
5. Restarted the App Service so the new network configuration took effect.

The exact commands and resource identifiers are recorded in the paired private repository at `src/docs/90.00-issues/202608/20260817.01-deployfail/03-incident-b-docs-site-500.internal.md`.

### ⚠️ This fix exists only in the live environment

The repository contains **no** infrastructure-as-code: zero Bicep, Terraform or parameter files, and no VNet or subnet configuration in any workflow.

The change that resolved this incident was applied imperatively and is declared nowhere. That means:

- **Recreating the docs app restores the broken state**, because the working configuration was never captured in anything executable.
- **No drift detection is possible** — there is no intended state to compare against.
- **This document is the only record of the fix**, and prose cannot be applied, validated or diffed.

The incident is resolved. The *cause of the incident* — an app provisioned without the network configuration it needs — remains fully reproducible. Treat this as the primary follow-up, ahead of the cosmetic items below.

## 🧪 Verification

- The docs-site root (`/`) and the mounted space route both moved from HTTP 500 to HTTP 200, confirmed in a **visible headed browser** with a screenshot captured for each route.
- Workflow run `32062547792`, attempt 2, completed end to end including a passing smoke check.
- The empty-container "Not found" body on both routes is **expected**: the docs container has no published content yet. Publishing content is a separate, later step.

### Plan-wide regression audit

Every app on the shared plan was checked before and after the change:

| App | State | Root HTTP status | Control-plane events in the window | Notes |
|---|---|---|---|---|
| AI-CM app service | Running | 403 (IIS, no public root page) | None | Pre-existing behaviour |
| Learn app service | Running | 200 | None | Unaffected |
| Samples app service | Running | 403 (IIS, no public root page) | None | Pre-existing behaviour |
| LiveQuiz app service A | Running | 200 | None | Unaffected; still has no VNet integration |
| Docs app service | Running | 200 (was 500) | One failed update (rejected subnet), one successful | Fixed by this change |

No pre-existing app was updated, restarted or reconfigured, and the plan itself was not scaled. The two apps returning 403 return the same IIS body they returned beforehand and have no configured health-check path, so their root response is not a health signal in either direction.

### Related app review

- **LiveQuiz app service A** shares the plan with the docs app but has no VNet integration. It can join the shared app subnet without exceeding the plan limit, since that subnet is already counted. **Not yet applied** — recommended follow-up.
- **LiveQuiz app service B** runs on its own plan and is already integrated with its own subnet inside the same shared VNet. Its document-store private endpoint and DNS zone link were confirmed. No action needed.

## ✔️ Resolution status

- Docs app VNet connectivity to the private content store restored. (✅ done)
- Visible-browser validation recorded for both routes. (✅ done)
- Plan-wide audit confirmed no regression to pre-existing apps. (✅ done)
- Workflow run confirmed green end to end. (✅ done)
- **Declare the plans, subnets, delegations and VNet integrations as infrastructure-as-code, then reconcile against deployed state.** (🔴 open — the fix is currently undeclared drift)
- Add a deploy preflight asserting VNet integration exists when a space is backed by a network-restricted store. (🟡 todo)
- Make the smoke check classify failures instead of retrying an identical request ten times. (🟡 todo)
- Join LiveQuiz app service A to the shared app subnet for full plan-wide coverage. (🟡 todo — recommended, not yet applied)
- Publish docs content so the space serves real articles. (📌 next steps)
- Consider a dedicated plan and subnet for the docs app for stronger isolation. (📌 next steps)

## 🎓 Lessons learned

- **A correct RBAC grant does not imply connectivity.** Blob storage returns `AuthorizationFailure` whether the caller lacks the role or simply cannot reach the endpoint. Check the network layer — public access setting, private endpoints, VNet integration — *before* re-auditing identity.
- **A green deployment is not a working application.** Every workflow step passed. Deployment success and service health are different claims, and only one of them was being measured.
- **Ten identical retries produce one bit of information.** The smoke check retried the same request ten times and learned nothing it did not know after the first attempt. A retry loop should either vary what it asks or report *why* it failed.
- **Per-plan integration limits shape design, not just operations.** Reusing an already-delegated subnet costs no slot and is preferable to provisioning a new one under pressure — but the limit needs to be budgeted at design time.
- **Audit every sibling before claiming "no regression."** A plan-level network change can plausibly affect other apps on that plan, even when one app is the intended target.
- **An imperative fix is a deferred repeat of the same incident.** Resolving the symptom without declaring the configuration leaves the failure fully reproducible on the next rebuild.

## 🏁 Conclusion

A deployment that succeeded on every measurable step produced a service that failed on every request, because the application had no network route to its content store and the resulting error named the wrong layer. Reusing an existing delegated subnet resolved it without consuming a plan slot, and a plan-wide audit confirmed no sibling was disturbed.

The incident is closed; the underlying exposure is not. Until this environment's network configuration is declared as code, the same failure will recur the next time an app is provisioned by hand.

The environment topology this investigation mapped is recorded in [the environment reference](04-environment-reference.md).

## 📚 References

- **[GitHub Actions run 32062547792, attempt 2](https://github.com/diginsight/smartdocs/actions/runs/32062547792/attempts/2)** 📘 [Official]  
  Execution evidence for the docs-site deployment after the VNet integration fix, including the passing smoke check.

- **[Integrate your app with an Azure virtual network](https://learn.microsoft.com/azure/app-service/overview-vnet-integration)** 📘 [Official]  
  Regional VNet integration behaviour and the per-plan subnet limit that rejected the first fix attempt.

- **[Use private endpoints for Azure Storage](https://learn.microsoft.com/azure/storage/common/storage-private-endpoints)** 📘 [Official]  
  How storage network rules and private endpoints decide which callers can reach an account.

- **[Configure Azure Storage firewalls and virtual networks](https://learn.microsoft.com/azure/storage/common/storage-network-security)** 📘 [Official]  
  Documents the `defaultAction: Deny` behaviour that produced the misleading `AuthorizationFailure`.

- **[Authorize access to blobs using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory)** 📘 [Official]  
  Explains the data-plane role model that was already correct here, and why its error surface overlaps with network denial.
