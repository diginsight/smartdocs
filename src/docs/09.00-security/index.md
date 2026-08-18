---
title: "Security"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The security picture, established from the source and the pipelines."
source_sets:
  - authn-authz-surface
  - transport-and-crypto
  - secret-references
  - security-catalogue
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
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
  open_gaps: 1
-->

# Security

## 🎯 Introduction

What protects this application, what does not, and which of those absences are established facts rather than assumptions.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Security posture](01-security-posture.md) | Trust boundaries, identity, data protection, secret handling and the established absences |

This chapter carries an overview and a posture page only. **No security assessment catalogue is declared anywhere in the repository** — no control set, no requirement register and no dimension declaration was found ^[security-01] — so there is nothing to organise control-family or requirement pages around.

## 🔑 Key points

- **The pipeline holds the strongest declared controls.** Azure sign-in uses an OIDC federated credential with no stored client secret ^[security-09]; the values read from the private overlay are passed to `::add-mask::` before they can reach a log ^[security-19]; and the overlay itself is fetched at run time rather than committed ^[devops-13].
- **The application has almost no inbound guard.** No authentication or authorisation middleware is registered ^[security-02], no `[Authorize]` attribute or `RequireAuthorization()` call appears in the source ^[security-03], and the content and navigation read endpoints declare no identity requirement ^[security-04]. One endpoint — `POST /_nav/invalidate` — compares a shared-secret header in constant time ^[security-05], and that comparison is skipped entirely when the configured key is empty ^[security-06].
- **No content credential exists to leak.** Outbound access to the content store uses `DefaultAzureCredential`; no account key, shared-access signature or connection string is accepted. ^[security-08]
- **No secret value appears in this repository.** Every credential-shaped settings key is declared empty, and the settings file states that storage identity is deliberately absent. ^[security-18]
- **Absence is documented as absence.** Where no control was found, the posture page says so and does not infer that none was wanted.

## 🕳️ Open questions

> **Not established**: whether any security assessment, penetration test, threat model or compliance baseline was performed outside this repository. The absence of a declared catalogue inside it is established ^[security-01]; whether an assessment exists elsewhere could not be determined from the repository, so this chapter describes posture rather than conformance. ^[gap]

## 🔗 Related

- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the endpoint table this posture applies to
- [DevOps](../10.00-devops/index.md) — the pipeline controls
- [Infrastructure](../05.00-infrastructure/index.md) — the environments this posture is deployed into
