---
title: "Deployment smoke check"
author: "Dario Airoldi"
date: "2026-08-18"
description: "The one automated check that runs against a deployed application."
source_sets:
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
  open_gaps: 3
-->

# Deployment smoke check

## 📚 Table of contents

- [🎯 What it proves](#-what-it-proves)
- [📋 Covered behaviours](#-covered-behaviours)
- [🚫 What it does not prove](#-what-it-does-not-prove)
- [▶️ How to run it](#-how-to-run-it)
- [🔗 Dependencies](#-dependencies)
- [🚦 Where it gates](#-where-it-gates)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 What it proves

That the deployed application answers an HTTP request with 200 after a deployment. ^[devops-19]

It is the last step of the reusable build-and-deploy workflow — both as declared ^[devops-19] and as executed in a captured run of the deploy job ^[devops-28]. It is also the only step in any workflow whose success depends on a running instance answering: the publish workflow's call to the site is best-effort and its failure does not fail the run. ^[devops-26]

## 📋 Covered behaviours

| Behaviour | How |
|---|---|
| The site responds | The hostname is resolved with `az webapp show` and then requested ^[devops-19] |
| It responds within a startup window | Up to ten attempts, fifteen seconds apart ^[devops-19] |
| It responds within a request timeout | Thirty seconds per attempt ^[devops-19] |
| It responds successfully | HTTP 200 expected; the run fails if no 200 is seen ^[devops-19] |

## 🚫 What it does not prove

The check asserts a status code and nothing else. ^[devops-19] It therefore does not establish:

- **That any content renders.** A 200 from the site root does not establish that a Markdown document was found, parsed or converted.
- **That navigation builds.** The tree is built lazily on request, and this check makes one request.
- **That the content source is reachable.** The blob container is read on demand; a 200 does not establish that the credential resolved or that the container exists.
- **That configuration is correct.** The overlay is checked only for the presence of `Site.Spaces` and `Deployment.WebAppName`; no value in it is asserted. ^[devops-16]

## ▶️ How to run it

It runs automatically as the last step of every deployment. ^[devops-19] It is a step of the reusable workflow rather than a script, so it has no independent entry point.

## 🔗 Dependencies

A successful zip deployment in the preceding step — the publish folder is compressed to `smartdocs-deploy.zip` and pushed with `az webapp deploy --type zip` ^[devops-12] — and a deployment target resolved from the overlay's `Deployment.WebAppName` ^[devops-15].

## 🚦 Where it gates

At the end of deployment, after the application is already live. A failure marks the run failed. ^[devops-19]

## 🕳️ Open questions

> **Not established**: whether a failing smoke check triggers any notification or manual response. No notification step or handler was found in any of the four workflow definitions. ^[gap]

> **Not established**: whether a failed deployment can be reversed. No rollback step, previous-version retention, deployment slot or baseline comparison appears in the reusable workflow; its declared steps end at the smoke check ^[devops-28]. ^[gap]

> **Not established**: whether the deployed instance was ever observed directly. Response headers, TLS configuration, rendered navigation and cache behaviour were sought against a running instance; hostnames are not declared in this repository and are masked in workflow logs. ^[gap]

## 🔗 Related

- [Build and deploy pipeline](../10.00-devops/01-build-and-deploy-pipeline.md) — the workflow this step belongs to
- [Validation](index.md) — what else is and is not checked
