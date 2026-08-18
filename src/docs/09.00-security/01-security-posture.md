---
title: "Security posture"
author: "Dario Airoldi"
date: "2026-08-18"
description: "What protects this application, and what does not."
source_sets:
  - authn-authz-surface
  - transport-and-crypto
  - secret-references
  - entry-points
  - pipeline-definition
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
  open_gaps: 8
-->

# Security posture

## 📚 Table of contents

- [🎯 What is protected](#-what-is-protected)
- [🧱 Trust boundaries](#-trust-boundaries)
- [🪪 Identity and authorisation](#-identity-and-authorisation)
- [🔐 Data protection](#-data-protection)
- [🗝️ Secret handling](#-secret-handling)
- [⚠️ Established absences](#-established-absences)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 What is protected

The application serves public technical documentation from a content store. Two things are worth protecting: the **content store credential**, and the **ability to change what readers see** — through the invalidation endpoint, the test-only mutation endpoints, or the publish pipeline.

## 🧱 Trust boundaries

| Boundary | Crossed by | Guard |
|---|---|---|
| Browser → application | Every request | No identity requirement on the content and navigation read endpoints ^[security-04]; HSTS and HTTPS redirection outside `Development` ^[security-15] |
| Application → content storage | Every content read | `DefaultAzureCredential`; no account key, shared-access signature or connection string is accepted ^[security-08] |
| Pipeline → Azure | Every deployment | An OIDC federated credential; no client secret is stored ^[security-09] |
| Pipeline → private overlay | Every deployment | A repository read token; the step fails when the secret is absent ^[devops-13] |
| Publish → content storage | Every publish | `--auth-mode login`, so storage operations run under the federated identity rather than an account key ^[security-10] |

## 🪪 Identity and authorisation

**Inbound.** No authentication middleware is registered, and neither `UseAuthentication` nor `UseAuthorization` appears in the composition root. ^[security-02] No endpoint or component carries an authorisation attribute: `[Authorize]` and `RequireAuthorization()` do not appear in the source tree. ^[security-03] The content and navigation read endpoints declare no identity requirement at all. ^[security-04]

The only inbound guard in the application is on `POST /_nav/invalidate`, which compares a shared-secret header with `CryptographicOperations.FixedTimeEquals`, a constant-time comparison. ^[security-05] That comparison is **skipped entirely when `Site:InvalidateApiKey` is empty**, and the endpoint then accepts any caller. ^[security-06]

The content-mutation endpoints are not part of the production surface: they are mapped only when `Testing:ContentMutationEnabled` is true, which the public base settings do not enable. ^[security-07]

**Outbound.** Blob access uses `DefaultAzureCredential`, so no account key, shared-access signature or connection string for content storage exists anywhere in the repository. ^[security-08]

**Pipeline.** Azure sign-in uses an OIDC federated credential and stores no client secret. ^[security-09] Every workflow declares `permissions: id-token: write, contents: read`. ^[devops-04]

## 🔐 Data protection

| Control | State |
|---|---|
| HSTS | Applied only when the hosting environment is not `Development` ^[security-15] |
| HTTPS redirection | Applied only when the hosting environment is not `Development` ^[security-15] |
| Antiforgery | Applied in every environment ^[security-16] |
| Production error handling | `/error` with `createScopeForErrors: true`, so an unhandled exception does not surface a developer exception page in a deployed environment ^[security-17] |
| Path containment | The filesystem content source rejects a resolved path that does not fall within its configured root ^[security-11] |
| Front-matter cap | Parsing stops at a 64 KB cap per document, so a malformed document cannot force an unbounded scan ^[security-13] |
| Navigation exclusion | Names beginning `_` or `.` are not enumerated into navigation ^[security-12] |
| Hidden documents | A document declaring `publish: false` or `draft: true` is marked hidden and excluded from navigation ^[security-14] |
| Cancellation | A client disconnect is translated to HTTP 499 by an endpoint filter rather than surfacing as a server error ^[security-20] |

## 🗝️ Secret handling

- **No secret value appears in this repository.** No connection string, account key, client secret or certificate value appears in any settings file; every such key is declared empty or omitted. ^[configuration-25] ^[security-18]
- The keys declared empty include `Site:InvalidateApiKey` ^[configuration-05], `Blob:AccountUri` and `Blob:ContainerName` ^[configuration-07], `Diginsight:SmartCache:Redis:Configuration` ^[configuration-10] and `OpenTelemetry:AzureMonitorConnectionString` ^[configuration-14].
- The Service Bus connection string is **absent** rather than empty: `Diginsight:SmartCache:ServiceBus` declares a topic name and no `ConnectionString` key, with a comment stating the connection string comes from a key vault. ^[configuration-11]
- The settings file states explicitly that storage identity is environment-specific and deliberately absent from the public repository, arriving from the external configuration overlay. ^[configuration-26]
- The overlay is fetched at deploy time by a sparse clone of a private peer repository, authenticated with a repository read token; the step fails when that secret is absent. ^[devops-13]
- Values read from the overlay are masked before they can reach a log — the web-app name, the storage account name and the container name are all passed to `::add-mask::`. ^[security-19] ^[devops-15] ^[devops-22]
- The overlay's `Deployment` section is **removed** from the settings file before it is staged into the published output, so deployment metadata never reaches the running application. ^[devops-15]
- The invalidation key is written as an App Service application setting only when the corresponding optional secret is present. ^[devops-18]

## ⚠️ Established absences

These were established by reading the artifact that would carry the control. Each is what was *not* found, not a route to exploit.

| Absence | Established from |
|---|---|
| No inbound authentication or authorisation anywhere in the application | No middleware in the composition root ^[security-02], no attribute or `RequireAuthorization()` call in the source tree ^[security-03], and no identity requirement on the read endpoints ^[security-04] |
| The invalidation guard is disabled by an empty key rather than closed | The comparison is skipped when the configured key is empty ^[security-06] |
| No security assessment catalogue is declared | No control set, no requirement register and no dimension declaration was found ^[security-01] |
| No manual approval or review gate in any workflow | No workflow declares one inside its job definition ^[devops-27] |
| The overlay is checked for two keys only | The staging step fails only when `Site.Spaces` or `Deployment.WebAppName` is missing ^[devops-16] |

## 🕳️ Open questions

> **Not established**: whether the application is intended to be reachable without authorisation. The absence of every inbound guard is established ^[security-02,security-03,security-04]; the intent behind it is not, and this page does not read the absence as a decision. ^[gap]

> **Not established**: no rate limiting or request throttling was found. A rate-limiting policy, throttling middleware and a request-size limit on the anonymous read surface were sought — the composition root and the endpoint definitions were searched for `AddRateLimiter`, `UseRateLimiter`, `RequestSizeLimit` and equivalents — and none is present. ^[gap]

> **Not established**: no security headers beyond HSTS were found. A content security policy, frame options, a referrer policy and a MIME-sniffing header were sought in the composition root, in `wwwroot` and in a `web.config`; only HSTS and HTTPS redirection appear, both applied outside `Development` only. ^[gap]

> **Not established**: the cross-origin posture. Whether a CORS policy is registered was sought in the security dossier for this component, whose coverage runs from the assessment catalogue through authorisation, outbound authentication, input constraint, transport and secret referencing; CORS registration is recorded neither as a control nor as an absence. This page therefore states nothing about it. ^[gap]

> **Not established**: the dependency vulnerability posture. A dependency-scanning workflow, a lock-file audit step and a declared advisory policy were sought; a `packages.lock.json` exists for each project, but no workflow consumes it for auditing and no Dependabot configuration was found under `.github/`. ^[gap]

> **Not established**: whether either deployed environment supplies `Site:InvalidateApiKey`, and therefore whether the invalidation endpoint is guarded in practice. The deploy step sets it only when the optional secret is present ^[devops-18]; whether that secret is configured is a repository-settings fact this repository does not carry. ^[gap]

> **Not established**: whether any network-level restriction limits who can reach the application. Instance count, autoscale rules, availability zones, VNet integration and private-endpoint usage were sought; the deploy workflow declares only bitness, framework version and application settings, and no other platform setting appears in this repository. ^[gap]

> **Not established**: whether the two GitHub environments carry protection rules such as required reviewers, wait timers or branch restrictions. The workflows reference the environments by name; protection rules are repository settings rather than workflow content. ^[gap]

## 🔗 Related

- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the surface this posture applies to
- [Configuration settings](../06.00-reference/01-configuration-settings.md) — the two keys that change it
- [DevOps](../10.00-devops/index.md) — where the pipeline controls above are defined
- [Infrastructure](../05.00-infrastructure/index.md) — the environments this posture is deployed into
