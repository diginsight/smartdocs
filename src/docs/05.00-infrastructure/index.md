---
title: "Infrastructure"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Where Diginsight SmartDocs runs, and what this repository does and does not say about it."
source_sets:
  - deployment-descriptor
  - infrastructure-definition
  - settings-sources
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/environment.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-08-18"
  open_gaps: 2
-->

# Infrastructure

## 🎯 Introduction

Which environments exist, what the pipelines do to them, and how confident this documentation can be about any of it.

The honest answer to the last question is: less confident than usual. **This repository contains no infrastructure definition** — see the gap at the foot of this page. Everything below is reconstructed from what the deployment pipelines *do* to an environment, not from a model that declares what the environment *is*.

Resource names are held outside this repository, so these pages use role aliases throughout.

## 🗺️ Pages in this section

| Page | Covers |
|---|---|
| [testmc environment](01-testmc-environment.md) | The environment hosting the Learning Hub deployment |
| [testmcdocs environment](02-testmcdocs-environment.md) | The environment hosting the docs site, and receiving published content |

## 🔑 Key points

- **Two environments, one shape.** The workflows name two GitHub environments, `testmc` and `testmcdocs` ^[environment-01], and each hosts one instance of the host application on an Azure App Service ^[environment-03]. Both run on Windows with a `win-x64` runtime identifier ^[environment-05], and both run the application as a self-contained deployment ^[environment-06].
- **Each environment selects its own settings overlay by name**, fetched from the private peer repository at deploy time. ^[environment-02]
- **The difference is content.** The publish job is fixed to the `testmcdocs` environment; the other deployment is not a publish target. ^[devops-21] Content for the documentation environment lives in an Azure Blob Storage container, created by the publish workflow when absent. ^[environment-08]
- **Resource identity is deliberately absent from this repository.** The settings file states that storage identity is environment-specific and arrives from the overlay ^[configuration-26]; the workflow masks the deployment target it reads and strips the deployment section before the settings file is staged into the published output ^[devops-15].
- **The resource group is discovered, not declared.** The deployment workflow resolves it with `az webapp list` rather than being told where the application lives. ^[devops-17]
- **No key reaches the application.** Blob access uses `DefaultAzureCredential` ^[security-08], and pipeline authentication uses an OIDC federated credential with no stored client secret ^[devops-14].

## 🕳️ Open questions

> **Not established**: there is no infrastructure-as-code in this repository, so nothing here describes how these environments are created, what would happen if one had to be rebuilt, or what role assignments the pipelines assume. Glob searches across the whole tree for `**/*.bicep`, `**/*.arm.json`, `**/azuredeploy*`, `**/*.tf`, `**/*Pulumi*`, `**/Dockerfile*` and `**/azure.yaml` every one returned zero results. ^[gap]

> **Not established**: the scaling, availability and networking posture of either environment. Instance count, autoscale rules, availability zones, VNet integration and private-endpoint usage were all sought; the deploy workflow declares only bitness, framework version and application settings, and no other platform setting appears in this repository. ^[gap]

## 🔗 Related

- [DevOps](../10.00-devops/index.md) — the pipelines that configure and deploy to these environments
- [Security posture](../09.00-security/01-security-posture.md) — the identity and secret-handling posture
- [Architecture](../03.00-architecture/index.md) — what is deployed into them
