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
  - settings-sources
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
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

The host can serve both public and restricted documentation. Reader visibility applies to raw bytes, rendered pages, navigation, counts, shell/bootstrap state and hub messages. ^[security-04,security-30] Content-store identity and the ability to publish, invalidate or mutate content remain separate concerns. ^[security-05,security-07,security-08,security-10]

## 🧱 Trust boundaries

| Boundary | Crossed by | Guard |
|---|---|---|
| Browser ↔ application HTTP | Paths, cookies, validators and forms inward; content and status outward | Reader filtering, concealment, antiforgery and private response policies ^[security-26,security-27,security-28,security-35] |
| Browser ↔ bootstrap and hub | Page/connection identity inward; display state, filtered navigation/counts outward | Server-derived role groups; client display state isn't permission authority ^[security-30,security-35] |
| Host ↔ Entra | Challenge outward; returned identity claims inward | Optional Microsoft Identity Web sign-in; deployed policy still needs verification ^[security-02,security-35] |
| Application → content storage | Every content read | `DefaultAzureCredential`; no account key, shared-access signature or connection string is accepted ^[security-08] |
| Pipeline → Azure | Every deployment | An OIDC federated credential; no client secret is stored ^[security-09] |
| Pipeline → private overlay | Every deployment | A repository read token; the step fails when the secret is absent ^[devops-13] |
| Publish → content storage | Every publish | `--auth-mode login`, so storage operations run under the federated identity rather than an account key ^[security-10] |

## 🪪 Identity and authorisation

**Inbound.** Nonblank `AzureAd:ClientId` enables Microsoft Identity Web sign-in. Authentication runs before request-scoped reader visibility; explicit server-side filtering, not a blanket fallback policy, controls content. ^[security-02,security-03] Case-insensitive roles grant a union of permissions separately within each space, and every combined site/space/file requirement must be covered. ^[security-24]

Invalid, unreadable or required-but-missing access files deny visibility. The host checks exact selected content keys, conceals denials as missing content, and excludes access-file bytes from all mounted reads. ^[security-25,security-26,security-29] A private-site default challenges unsigned page requests before existence lookup. ^[security-26]

**Session.** The cookie is HttpOnly, always Secure and SameSite=Lax. Its configured positive lifetime defaults to eight hours without sliding or token-lifetime substitution. Sign-in accepts local return targets only; sign-out is an antiforgery-validated POST. ^[security-28]

**Management.** Invalidation retains its independent management protection. Reader roles don't grant management access, and source wiring alone doesn't establish the adequacy of deployed protection. ^[security-05,security-06]

Test mutations are mapped only when `Testing:ContentMutationEnabled` is true; source wiring doesn't prove its value in a deployed instance. ^[security-07]

**Outbound.** `BlobContentSource` uses `DefaultAzureCredential` and accepts no account key, SAS or connection string. This is a source contract, not a repository-wide secret scan. ^[security-08]

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
| Reader-dependent response reuse | Private revalidation for page/nav/site JSON; no storage for injected HTML and restricted successful raw bytes ^[security-27] |
| Content-key guards | Ambiguous keys rejected before reads; filesystem containment and blob-container checks preserved ^[security-31] |
| Hub isolation | Server-derived role-set membership and per-push visibility/count composition; cookie-expiry closure not yet proven ^[security-30] |

## 🗝️ Secret handling

- **The inspected base file has no populated credential.** This bounded configuration check isn't assurance of repository-wide secret absence. ^[configuration-25,security-18]
- Identity and deployment-specific values belong in the private overlay; the base has an empty space array, not a populated default Blob space. ^[configuration-07,configuration-26]
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
| No completed reader-access end-to-end matrix | Sequences 01–03 passed bounded checks, but sequence 04 stopped at preflight ^[security-32,security-33,security-34] |
| Deployed management enforcement isn't established by source alone | The invalidation guard depends on configuration ^[security-06] |
| No security assessment catalogue is declared | No control set, no requirement register and no dimension declaration was found ^[security-01] |
| No manual approval or review gate in any workflow | No workflow declares one inside its job definition ^[devops-27] |
| The overlay is checked for two keys only | The staging step fails only when `Site.Spaces` or `Deployment.WebAppName` is missing ^[devops-16] |

## 🕳️ Open questions

> **Not established**: effective cookie-expiry hub closure, lifetime-bound revocation, signed-in cross-group pushes, full cache-header coverage and the other F1–F6 outcomes. The session-6 execution waiver doesn't prove these scenarios; DS-4 remains open. ^[gap]

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
