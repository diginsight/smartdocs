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
  - settings-sources
---

<!--
verification_stamp:
  generated: "2026-11-02"
  verified: "2026-11-02"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-11-02"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
  open_gaps: 2
-->

# Security

## 🎯 Introduction

What protects this application, what does not, and which of those absences are established facts rather than assumptions.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [Security posture](01-security-posture.md) | Trust boundaries, identity, data protection, secret handling and the known absences |

This chapter carries an overview and a posture page only. **No security assessment catalogue is declared anywhere in the repository** — no control set, no requirement register and no dimension declaration was found ^[security-01] — so there is nothing to organise control-family or requirement pages around.

## 🔑 Key points

- **The pipeline holds the strongest declared controls.** Azure sign-in uses an OIDC federated credential with no stored client secret ^[security-09]; the values read from the private overlay are passed to `::add-mask::` before they can reach a log ^[security-19]; and the overlay itself is fetched at run time rather than committed ^[devops-13].
- **Reader access is server-authoritative.** Optional Entra sign-in precedes explicit per-space filtering across content, pages, navigation, shell and counts; roles expand through each space's own file. ^[security-02,security-03,security-04,security-24]
- **Restricted content fails closed.** Invalid/unreadable and required-but-missing files deny visibility; access-file bytes aren't publicly served. ^[security-25,security-29]
- **Response reuse and pushes respect the reader.** Private cache policies and server-derived role-set groups are implemented. A 2026-11-02 manual run on a local host showed the hub closing a connection when its sign-in session expires, and the full reader-access end-to-end matrix passing. Behaviour in a deployed environment is still unproven. ^[security-27,security-30,security-34,security-38]
- **No content credential exists to leak.** Outbound access to the content store uses `DefaultAzureCredential`; no account key, shared-access signature or connection string is accepted. ^[security-08]
- **Secret observations are scoped.** The inspected public base leaves identity/invalidation values unset; this isn't a repository-wide secret scan. ^[security-18]
- **Absence is documented as absence.** Where no control was found, the posture page says so and does not infer that none was wanted.

## 🕳️ Open questions

> **Not established**: whether any security assessment, penetration test, threat model or compliance baseline was performed outside this repository. The absence of a declared catalogue inside it is established ^[security-01]; whether an assessment exists elsewhere could not be determined from the repository, so this chapter describes posture rather than conformance. ^[gap]

> **Not established**: whether reader access, hub closure and sign-in behave the same way once deployed. The 2026-11-02 run used only a local host, a fixture tree and a development sign-in registration; no deployed-environment validation was recorded, and whether the sign-in registrations are deployed to production isn't established. ^[security-34,security-38] Whether every deployed host sets the invalidation key isn't established either. ^[security-06] ^[gap]

## 🔗 Related

- [HTTP and hub endpoints](../06.00-reference/02-http-endpoints.md) — the endpoint table this posture applies to
- [DevOps](../10.00-devops/index.md) — the pipeline controls
- [Infrastructure](../05.00-infrastructure/index.md) — the environments this posture is deployed into
