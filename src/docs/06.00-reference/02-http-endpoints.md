---
title: "HTTP and hub endpoints"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Every route this application maps, what it returns, and what guards it."
source_sets:
  - entry-points
  - composition-root
  - authn-authz-surface
---

<!--
verification_stamp:
  generated: "2026-10-28"
  verified: "2026-10-28"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-10-28"
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-10-28"
  open_gaps: 4
-->

# HTTP and hub endpoints

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [📋 Operations](#-operations)
- [🔐 Authorisation](#-authorisation)
- [⚠️ Failure modes](#-failure-modes)
- [📏 Limits](#-limits)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)
## 🎯 Purpose

- **Kind**: HTTP minimal-API endpoints and one SignalR hub
- **Component**: `smartdocs-web`
- **Declared in**: `Endpoints/ContentEndpoints.cs`, `Endpoints/NavEndpoints.cs`, `Endpoints/TestContentEndpoints.cs`, `Navigation/NavHub.cs`

## 📋 Operations

### Content

| Route | Method | Returns |
|---|---|---|
| `/_content/{**key}` | GET | The raw bytes and content type for a key; 404 when absent ^[code-10] |
| `/_page`, `/_page/{**path}` | GET | Root or route-path rendered page; 204 for hidden/missing, 304 for matching successful-response ETag. `path` isn't a query selector ^[code-61] |

### Navigation

| Route | Method | Returns |
|---|---|---|
| `/_nav/children?prefix=` | GET | Reader-filtered `NavLevelResponse` with article/folder references and folder records; queues warming at depth 3 ^[code-11] |
| `/_nav/version` | GET | `{ version: long }` — a monotonically increasing value callers can use to detect staleness ^[code-12] |
| `/_nav/total` | GET | Reader-composed root `FolderArticleStats`, or 204 while unknown ^[code-13] |
| `/_nav/index` | GET | Reader-filtered flattened article index ^[code-14] |
| `/_nav/folder?prefix=` | GET | Visible folder record, or the unknown-folder representation when hidden ^[security-26] |
| `/_nav/invalidate?path=` | POST | Discards cached navigation for a path ^[code-15] |

### Test-only

These are mapped **only** when `Testing:ContentMutationEnabled` is true. When it is false the routes do not exist, rather than existing and refusing. ^[code-17] ^[configuration-21]

| Route | Method | Returns |
|---|---|---|
| `/_test/article` | POST, DELETE | Creates or removes a content article ^[code-17] |
| `/_nav/metrics?prefix=` | GET | Navigation metrics for a prefix ^[code-17] |

### Hub

| Route | Transport | Messages |
|---|---|---|
| `/_nav/hub` | SignalR | `MetadataChanged`, `CountsReady`. The current root counts are sent on `OnConnectedAsync` ^[code-18] |

### Identity and shell

| Route | Method | Contract |
|---|---|---|
| `/_auth/signin?returnUrl=` | GET | Enabled only with sign-in; OIDC challenge, local return target or `/` ^[configuration-33] |
| `/_auth/signout` | POST | Enabled only with sign-in; validates antiforgery, signs out cookie/OIDC, returns to `/`; invalid form returns 400 ^[configuration-33] |
| `/_site` | GET | Shell state with only visible spaces ^[code-49] |
| `/robots.txt` | GET | Policy derived from anonymous visibility, not requesting identity ^[code-49] |

## 🔐 Authorisation

| Surface | Guard |
|---|---|
| `/_nav/invalidate` | Separate configuration-dependent management guard; reader permissions aren't management grants. Verify its deployed enforcement independently ^[security-05,security-06] |
| `/_test/*`, `/_nav/metrics` | Not mapped at all unless `Testing:ContentMutationEnabled` is true ^[code-17] ^[security-07] |
| Content, pages, navigation, shell and bootstrap | Explicit server-side reader visibility; denied results use missing-content representations ^[security-03,security-04,security-26] |
| Hub | Server-derived role-set group and per-push visibility evaluation ^[security-30] |

Nonblank `AzureAd:ClientId` enables optional authentication before reader access. There is no blanket fallback authorization policy: public spaces remain readable anonymously. ^[security-02,security-03] Site-default `authenticated` challenges unsigned page requests before existence lookup. ^[security-26]

Reader-dependent JSON uses `private, no-cache`; injected HTML and restricted successful raw bytes use `private, no-store`. Public Markdown revalidates and public assets retain freshness. Raw authorization precedes content reads and ETag handling; filtered response ETags describe only the reader's response. ^[code-10,code-11,security-27]

## ⚠️ Failure modes

| Situation | Result |
|---|---|
| A content key does not resolve | 404 ^[code-10] |
| Nothing is known about the root aggregate yet | 204 ^[code-13] |
| The invalidation key does not match | 401 ^[code-15] |
| The client disconnects mid-request | 499, via an endpoint filter that maps `OperationCanceledException` raised on the request's own `RequestAborted` token ^[code-19] ^[security-20] |
| An unhandled exception outside `Development` | Handled at `/error` in a fresh scope ^[code-38] ^[security-17] |

## 📏 Limits

Front-matter parsing is capped at 64 KB per document, which bounds the cost of scoring a document but is not a request limit. ^[code-22] ^[security-13] The filesystem source rejects a resolved key that falls outside its configured root, so a key containing traversal segments cannot reach a file above the root. ^[security-11]

## 🕳️ Open questions

> **Not established**: the complete runtime cache-header matrix, expiry, revocation and baseline-regression scenarios remain pending after sequence 04's blocked preflight. ^[gap]

> **Not established**: whether either deployed environment supplies `Site:InvalidateApiKey`. The pipeline writes the setting only when the corresponding optional secret exists, and secret presence is a repository-settings fact this repository does not carry. ^[gap]

> **Not established**: no rate limiting or request throttling was found. The composition root and endpoint definitions were searched for `AddRateLimiter`, `UseRateLimiter`, `RequestSizeLimit` and equivalents on the anonymous read surface; none is present. ^[gap]

> **Not established**: no security headers beyond HSTS were found. A content security policy, frame options, referrer policy and MIME-sniffing header were sought in the composition root and `wwwroot`, and in a `web.config`; only HSTS and HTTPS redirection appear, both applied outside `Development` only. ^[gap]

## 🔗 Related

- [Navigation hub contract](03-navigation-hub-contract.md) — the message shapes carried over `/_nav/hub`
- [Configuration settings](01-configuration-settings.md) — the two keys that gate this surface
- [Security posture](../09.00-security/01-security-posture.md) — the posture that follows from the table above
