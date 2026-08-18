---
title: "Configuration settings"
author: "Dario Airoldi"
date: "2026-08-18"
description: "Every configuration key this application declares, with its declared value and its effect."
source_sets:
  - settings-sources
  - composition-root
---

<!--
verification_stamp:
  generated: "2026-08-18"
  verified: "2026-08-18"
  gate_outcome: "pass-with-gaps"
  evidence:
    - dossier: "_evidence/smartdocs-web/configuration.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/code.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/devops.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/environment.md"
      observed: "2026-08-18"
    - dossier: "_evidence/smartdocs-web/security.md"
      observed: "2026-08-18"
  open_gaps: 3
-->

# Configuration settings

## 📚 Table of contents

- [🎯 Purpose](#-purpose)
- [🗂️ Where settings come from](#-where-settings-come-from)
- [📋 Members](#-members)
- [🔑 Keys and constraints](#-keys-and-constraints)
- [⚠️ Usage constraints](#-usage-constraints)
- [🔗 Used by](#-used-by)
- [🕳️ Open questions](#-open-questions)
- [🔗 Related](#-related)

## 🎯 Purpose

- **Kind**: configuration surface
- **Component**: `smartdocs-web`
- **Declared in**: `appsettings.json`, `appsettings.Development.json`, and an out-of-tree overlay

Values below are those declared **in this repository**. An empty value is not a missing value — it is a deliberate placeholder for something the overlay supplies. ^[configuration-26]

## 🗂️ Where settings come from

| Source | Selected by | Note |
|---|---|---|
| `appsettings.json` | always | declares every key the host reads, with environment-specific values left empty ^[configuration-01] |
| `appsettings.Development.json` | the standard ASP.NET Core environment mechanism | ^[configuration-02] |
| `appsettings.local.json` | present for developer-machine overrides | ^[configuration-03] |
| Out-of-tree overlay | `ExternalConfigurationFolder` + `AppsettingsEnvironmentName` | read by `ConfigureAppConfiguration2` ^[configuration-04] ^[code-03] |
| Environment variables | always | how the pipeline writes App Service settings, using `__` as the separator ^[devops-18] |

## 📋 Members

### `Site`

| Key | Declared value | Effect |
|---|---|---|
| `Site:Title` | `Diginsight SmartDocs` | site title ^[configuration-05] |
| `Site:NotFoundPath` | `404.html` | declared; what consumes it is not established — see below ^[configuration-05] |
| `Site:InvalidateApiKey` | *(empty)* | compared against `X-Invalidate-Key`; when empty the check is skipped entirely ^[configuration-05] ^[code-16] |
| `Site:MetricsSnapshotPath` | *(not declared in the public base file)* | falls back to `nav-metrics-snapshot.json` beside the application binaries ^[code-37] |
| `Site:Branding:ProductName` | `Diginsight SmartDocs` | ^[configuration-06] |
| `Site:Branding:IconClass` | `bi-lightbulb-fill` | ^[configuration-06] |
| `Site:Branding:LogoPath` | *(empty)* | ^[configuration-06] |

### `Site:Spaces`

One space is declared in the public repository. ^[configuration-07]

| Key | Declared value |
|---|---|
| `Id` | `diginsight.smartdocs` |
| `RouteBase` | `/` |
| `Title` | `SmartDocs` |
| `RepositoryUrl` | `https://github.com/diginsight/smartdocs` |
| `Source` | `Blob` |
| `Blob:AccountUri` | *(empty — supplied by the overlay)* |
| `Blob:ContainerName` | *(empty — supplied by the overlay)* |

The settings file states that a space's mount point is stated by `RouteBase` and nothing else, that at most one space may claim `/`, and that every other space reserves the first segment of its route base. ^[configuration-08] `SpaceRegistry` enforces that at least one space exists, that every space declares an id and that every route base is a single segment, and resolves a request path by longest matching route base. ^[code-07]

`SpaceOptions` supplies the defaults: `RouteBase` `/`, `Source` `Blob`, `FileSystem.RootPath` `.` and `WatchForChanges` `false`. ^[configuration-17]

The `Development` overlay replaces the source for this space with `FileSystem`, a root path of `..\docs`, and `WatchForChanges: true`. ^[environment-11]

### `Diginsight:SmartCache`

| Key | Declared value | Effect |
|---|---|---|
| `Enabled` | `false` | ^[configuration-09] |
| `AbsoluteExpiration` | `31.00:00:00` | 31 days ^[configuration-09] |
| `MaxAge` | `7.00:00:00` | 7 days ^[configuration-09] |
| `SlidingExpiration` | `7.00:00:00` | 7 days ^[configuration-09] |
| `Redis:Configuration` | *(empty)* | the Redis passive backing store is registered only when this is non-empty ^[configuration-10] ^[configuration-20] |
| `Redis:KeyPrefix` | `smartdocs-content:` | ^[configuration-10] |
| `ServiceBus:TopicName` | `smartcache-learnweb` | ^[configuration-11] |
| `ServiceBus:ConnectionString` | *(no key declared — a comment states it comes from a key vault)* | the Service Bus companion is registered only when **both** this and the topic name are non-empty; the subscription name is a per-process GUID ^[configuration-11] ^[configuration-19] |

These options are bound class-aware from `Diginsight:SmartCache` before `AddSmartCache(...).AddHttp()` is called. ^[configuration-18]

### `Diginsight:Components`

Concurrency tiers used by the parallel service. ^[configuration-12]

| Key | Declared value | Intended for |
|---|---|---|
| `LowConcurrency` | `2` | heavy or fragile per-item work |
| `MediumConcurrency` | `8` | ordinary non-heavy I/O — the tier navigation scoring uses ^[code-23] |
| `HighConcurrency` | `16` | cheap work against a robust downstream |

### `Diginsight:Activities`

| Key | Declared value |
|---|---|
| `ActivitySources` | `Diginsight.*` on, `Diginsight.SmartDocs.Web` on, `Microsoft.AspNetCore` on, `System.Net.Http` on, `Experimental.*` off ^[configuration-13] |
| Hidden activities | the ASP.NET Core request-in and the HTTP request-out activities ^[configuration-13] |
| `LogLevel` | `Debug` ^[configuration-13] |
| `MeterName` | `Diginsight.SmartDocs.Web` ^[configuration-13] |
| `Metrics` | `diginsight.span_duration`, `diginsight.query_cost` ^[configuration-13] |
| `RecordSpanDuration` | `true` ^[configuration-13] |

### `Observability`

| Key | Declared value |
|---|---|
| `ConsoleEnabled` | `true` ^[configuration-15] |
| `DebugEnabled` | `false` ^[configuration-15] |
| `Log4NetEnabled` | `true` ^[configuration-15] |

The log4net sink writes to `%USERPROFILE%\LogFiles\Diginsight\Diginsight.SmartDocs.Web.<date>.log`. ^[environment-13]

### `OpenTelemetry`

| Key | Declared value |
|---|---|
| `ActivitySources` | `Diginsight.*`, `Diginsight.SmartDocs.Web`, `Microsoft.AspNetCore` ^[configuration-14] |
| `Meters` | `Diginsight.SmartDocs.Web` ^[configuration-14] |
| `EnableTraces`, `EnableMetrics` | `true` ^[configuration-14] |
| `AzureMonitorConnectionString` | *(empty)* ^[configuration-14] |
| `ExcludedHttpHosts` | `.applicationinsights.azure.com`, `.monitor.azure.com` ^[configuration-14] |

### Other keys

| Key | Declared value | Effect |
|---|---|---|
| `Testing:ContentMutationEnabled` | *(not declared in any settings file)* | gates whether the content-mutation and nav-metrics diagnostic endpoints are mapped at all ^[configuration-21] ^[code-17] |
| `Logging:LogLevel:Default` | `Warning` | ^[configuration-16] |
| `Logging:LogLevel:Microsoft` | `Warning` | ^[configuration-16] |
| `Logging:LogLevel:Microsoft.Hosting.Lifetime` | `Information` | ^[configuration-16] |
| `Logging:LogLevel:Diginsight.SmartCache.Externalization.ServiceBus` | `Warning` | ^[configuration-16] |

### Run profiles

| Profile | Sets |
|---|---|
| `http`, `https` | `ASPNETCORE_ENVIRONMENT=Development` and `AppsettingsEnvironmentName=Development` ^[configuration-23] |
| `https - Testmc` | `AppsettingsEnvironmentName=Testmc` and an `ExternalConfigurationFolder` pointing at the private peer repository checked out beside this one ^[configuration-24] |

## 🔑 Keys and constraints

- `Site` is bound **and eagerly resolved** at startup. A missing section throws `InvalidOperationException("Missing 'Site' configuration section.")` rather than starting with defaults. ^[code-06]
- At least one space must be declared, every space must have an identifier, and every route base must be a single segment. ^[code-07]
- No connection string, account key, client secret or certificate value appears anywhere in this repository's settings files; every such key is declared empty or omitted. ^[configuration-25]
- The overlay's `Deployment` section is read by the pipeline, masked, and then **removed** before the settings file is staged into the published output, so it is never visible to the running application. ^[devops-15]

## ⚠️ Usage constraints

An empty `Site:InvalidateApiKey` does not close the invalidation endpoint — it removes the check, and the endpoint then accepts any caller. ^[code-16] ^[security-06]

## 🔗 Used by

The composition root, for every service registration decision it makes. ^[code-01] The deployment workflow, for the four application settings it writes. ^[devops-18]

## 🕳️ Open questions

> **Not established**: no consumer of `AzureKeyVault:Uri` was found in this repository. The key is declared, empty, in the settings file; the source tree was searched for `AzureKeyVault`, `SecretClient` and key-vault configuration extensions and none appeared. It may be consumed by the external Diginsight configuration extension, which was not read. ^[gap]

> **Not established**: what consumes `Site:NotFoundPath`. The key and its `404.html` value are declared, but no record establishes the code path that serves it. ^[gap]

> **Not established**: the effective configuration of either deployed environment. The values in force at runtime for `Site:Spaces`, `Diginsight:SmartCache:Enabled` and `OpenTelemetry:AzureMonitorConnectionString` live in overlay files in the private peer repository and are masked by the workflows. ^[gap]

## 🔗 Related

- [Running against the private overlay](../04.00-use-cases/05-running-against-the-private-overlay.md) — how the overlay is layered on locally
- [HTTP and hub endpoints](02-http-endpoints.md) — the two keys that gate that surface
- [Infrastructure](../05.00-infrastructure/index.md) — the settings the pipeline writes per environment
