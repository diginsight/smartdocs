---
title: "Learning Hub title configuration mismatch"
author: "Dario Airoldi"
date: "2026-08-19"
categories: [issue, configuration, blazor, deployment]
description: "Analysis of why the Learning Hub deployment showed the SmartDocs brand even though its site title was configured as Learning Hub."
publish: false
---

# Learning Hub title configuration mismatch

**Issue title:** Learning Hub shell title ignored runtime configuration  
**Date reported:** 2026-08-19  
**Reporter:** Dario Airoldi  
**Status:** Resolved  
**Severity:** Medium  
**Component:** `Diginsight.SmartDocs.Web` shell, shared site options, and WebAssembly layout  
**Framework:** .NET 10 / ASP.NET Core Razor Components with interactive WebAssembly

The Learning Hub deployment was configured with `Site:Title` set to `Learning Hub`, but the top navigation still showed `Diginsight SmartDocs`. The configuration overlay was not the broken part. The visible shell brand was hardcoded in the client layout, so the deployed settings had no path into the rendered navbar.

**Internal companion.** The companion records the real deployment URL, private configuration path, resource identifiers, and exact command transcript — `src/docs/90.00-issues/202608/20260818.05-title/overview.internal.md` in the private peer repository.

## 📚 Table of contents

- [🎯 Summary](#-summary)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction steps](#-reproduction-steps)
- [✅ Solution implemented](#-solution-implemented)
- [🧪 Verification](#-verification)
- [📌 Follow-up boundary](#-follow-up-boundary)
- [🎓 Lessons learned](#-lessons-learned)
- [📡 Signal sweep](#-signal-sweep)

## 🎯 Summary

The expected behavior was straightforward: a deployment configured as `Learning Hub` should present `Learning Hub` as its shell title. Instead, the top bar showed the product name from the original SmartDocs shell.

| Item | Result |
|---|---|
| Expected shell brand | `Learning Hub` |
| Observed shell brand | `Diginsight SmartDocs` |
| Configuration state | `Site:Title` was set correctly in the deployment overlay |
| Root cause | The client layout rendered a literal brand label |
| Fix | Add a site-shell projection and bind the layout to configured site state |
| Status | Resolved and validated in a visible browser |

The issue mattered because it made the deployment look like it was using the wrong configuration, even though startup configuration binding was working. That false signal is expensive: it pushes investigation toward the deployment pipeline and private overlay when the real defect sits in the UI component.

## 🔍 Context information

The application has a server project, a WebAssembly client, and a shared project. The server binds `SiteOptions` from configuration during startup. That bound object already controls core runtime behavior such as the configured spaces and the default content source. The shell, however, is rendered by the shared/client layout path, and that path had not been connected to the bound site options.

| Area | Observation |
|---|---|
| Server configuration | `Program.cs` binds `SiteOptions` from the `Site` section. |
| Visible shell | `MainLayout.razor` owned the navbar brand markup. |
| Configuration field involved | `Site:Title`, with branding fields available under `Site:Branding`. |
| Failure mode | Runtime configuration changed, but the navbar literal did not. |
| First validation defect | The first fix attempt exposed a prerender-only missing `HttpClient` registration. |

The first validation run was useful because it found the boundary between WebAssembly-only services and server-side prerendering. A component used by both render modes cannot inject a service that exists only in the WebAssembly service collection.

## 🔬 Analysis

The controlling line was in the layout:

```razor
<a class="brand" href=""><i class="bi bi-lightbulb-fill"></i> <span>Diginsight SmartDocs</span></a>
```

That literal made the shell brand independent of `Site:Title`. Because the server did bind and log the configured site title, the local hypothesis was that configuration reached the host but not the UI shell. The cheap discriminating check was to replace the literal with a configured value, run the app with a local `Site__Title=Learning Hub` override, and read the live DOM value from `.brand span`.

The fix needed to satisfy two render paths:

| Render path | Requirement |
|---|---|
| Server prerender | The shell must have configured state before the WebAssembly client starts. |
| WebAssembly interactivity | The client must be able to refresh the same public shell fields after startup. |

That led to a small public projection rather than a broad client-side configuration system. The projection exposes only non-secret shell identity: title and branding fields.

## 🔄 Reproduction steps

1. Configure the Learning Hub deployment with `Site:Title` set to `Learning Hub`.
2. Open the deployment home page in a browser.
3. Inspect the top navigation brand text.
4. Observe that the top navigation shows `Diginsight SmartDocs` instead of `Learning Hub`.
5. Inspect `MainLayout.razor` and find the literal brand label.

Affected code locations:

| File | Role |
|---|---|
| `src/Diginsight.SmartDocs.Web.Client/Layout/MainLayout.razor` | Rendered the hardcoded brand and now binds to site state. |
| `src/Diginsight.SmartDocs.Web.Client/Layout/MainLayout.razor.cs` | Loads site shell state on the client when needed. |
| `src/Diginsight.SmartDocs.Web.Shared/Sites/SiteOptions.cs` | Defines the shared site options and the new shell state projection. |
| `src/Diginsight.SmartDocs.Web/Endpoints/SiteEndpoints.cs` | Exposes the public `/_site` projection. |
| `src/Diginsight.SmartDocs.Web/Program.cs` | Registers server prerender services and maps the endpoint. |

## ✅ Solution implemented

### Step 1 - Add shared shell state (✅ done)

`SiteShellOptions` and `SiteShellState` were added beside `SiteOptions`. `SiteShellOptions.From(site)` projects only the shell-safe fields the client needs. `SiteShellState` keeps the current title, branding, and a change event used by the layout.

### Step 2 - Seed server prerender from bound configuration (✅ done)

The server registers scoped `SiteShellState` from the already-bound `SiteOptions`. That gives prerendered markup the configured title immediately, without waiting for WebAssembly to fetch anything.

### Step 3 - Add a public site endpoint (✅ done)

The server maps `GET /_site`, returning the same shell projection. This lets the interactive WebAssembly client refresh the shell state after startup.

### Step 4 - Register compatible client and server services (✅ done)

The WebAssembly client registers scoped `SiteShellState`. The server also registers scoped `HttpClient` for prerender because the shared layout injects `HttpClient` and is constructed on the server before WebAssembly takes over.

### Step 5 - Bind the navbar to configured state (✅ done)

The layout now renders:

```razor
<a class="brand" href=""><i class="bi @BrandIconClass"></i> <span>@Site.Title</span></a>
```

The icon falls back to `bi-lightbulb-fill` if no configured icon class exists.

## 🧪 Verification

Validation used a local configuration override rather than the deployed environment so the public validation artifact would not expose deployment-specific details.

| Check | Result |
|---|---|
| `dotnet build src\Diginsight.SmartDocs.slnx` | PASS |
| Visible browser opened against the local app | PASS |
| Live DOM read from `.brand span` | `Learning Hub` |
| Live DOM read from `.brand i` | `bi bi-lightbulb-fill` |
| Validation artifact | `src/docs/90.00-issues/202608/20260818.05-title/_validation/20260819.01-validation-sequence.md` |
| Screenshot | `src/docs/90.00-issues/202608/20260818.05-title/_validation/images/01-learning-hub-shell.png` |
| Replacement-character scan on validation markdown | `0` U+FFFD characters |

### Verification checklist

- Solution builds after the shell-state changes. (✅ done)
- The first browser run exposed the missing server-side `HttpClient` registration. (✅ done)
- The server-side prerender dependency was fixed. (✅ done)
- The same focused build was rerun successfully. (✅ done)
- Visible browser validation confirmed the configured navbar brand. (✅ done)

## 📌 Follow-up boundary

The browser tab title and article footer can still report `Diginsight SmartDocs` when the loaded article itself declares that title. That is a content-title concern, not the shell-brand configuration defect fixed here.

| Surface | Controlled by | Status |
|---|---|---|
| Navbar brand | `Site:Title` through `SiteShellState` | Fixed |
| Navbar icon | `Site:Branding:IconClass` through `SiteShellState` | Fixed with fallback |
| Browser tab title for an article | Rendered article title/frontmatter | Not changed |
| Home article heading | Content source Markdown | Not changed |

## 🎓 Lessons learned

- Configuration binding can be correct while a UI still ignores it. Start at the rendered component before assuming a deployment overlay failed.
- Shared Blazor layout components must be valid in both server prerender and WebAssembly service containers.
- A small public projection is enough for shell identity. The client does not need direct access to the full site configuration.
- Visible browser validation caught a real prerender defect that a compile-only check missed.

## 📡 Signal sweep

The signal-capture sweep found no out-of-scope signals that require a separate signals page.

| Sweep question | Result |
|---|---|
| What should happen that is not this issue? | The article/browser-title distinction is an in-scope boundary and documented as follow-up, not a separate signal. |
| What authority document was contradicted or extended? | None found. |
| Which changed artifacts have path-parallel peers in another repository? | None found for the code changes. |
| What was decided and written to no file? | The shell-brand versus article-title distinction is written in this analysis and validation notes. |
| What references a path outside this workspace? | The private overlay path is recorded only in the internal companion. |
| What subject was opened but not developed? | None found. |
| What framing landed wrong and was corrected? | The phrase "site title" was narrowed to shell/navbar brand versus article title in the analysis. |

## 📎 Appendix

The implemented public endpoint returns the shell-safe projection:

```csharp
app.MapGet("/_site", (IOptions<SiteOptions> siteOptions) =>
		Results.Json(SiteShellOptions.From(siteOptions.Value)));
```

The layout uses the shared state and remains compatible with prerender:

```razor
<a class="brand" href=""><i class="bi @BrandIconClass"></i> <span>@Site.Title</span></a>
```

No separate public signals page was created because the sweep produced no relevant or actionable out-of-scope records.

<!--
validations:
	grammar: {status: "not_run", last_run: null}
	readability: {status: "not_run", last_run: null}

article_metadata:
	filename: "overview.md"
	created: "2026-08-19"
	last_updated: "2026-08-19"
	version: "0.1"
	status: "resolved"
	issue_type: "bug"
-->
