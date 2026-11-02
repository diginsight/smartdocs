---
title: "Signals — roles and permissions on the document hierarchy"
author: "Dario Airoldi"
date: "2026-10-28"
categories: [signals, blazor, prerendering, layout, configuration, middleware]
description: "Activities surfaced while validating sign-in and the end-to-end behaviour of the roles and permissions work item that were never in scope for its goal."
publish: false
---

# Signals — roles and permissions on the document hierarchy

Activities this work item's conversation surfaced that were **never in scope** for its goal: restricting spaces to readers who hold a permission. Each record is self-contained, so someone reading it without this conversation can act on it as written.

**Identifiers are identity; the listing order is priority**, derived from relevance and actionability.

📖 Record shape, kinds, sweep, and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

`SIG-1` and `SIG-2` were added on 2026-10-28, during `E7-validate-sign-in` of [the implementation plan](01-roles-permissions-support.plan.md). `SIG-3` to `SIG-5` were added on 2026-11-02, during `WS-F-end-to-end-validation`, which [sequence 04](_validation/20261102.04-validation-sequence.md) records. None was introduced by this work item.

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-4` | `divergent-commitment` | medium | ready | `diginsight/components` | none found | `pending` |
| 2 | `SIG-1` | `divergent-commitment` | medium | bounded | `diginsight/smartdocs` | none found | `pending` |
| 3 | `SIG-2` | `divergent-commitment` | low | ready | `diginsight/smartdocs` | none found | `pending` |
| 4 | `SIG-3` | `divergent-commitment` | low | bounded | `diginsight/smartdocs` | none found | `pending` |
| 5 | `SIG-5` | `divergent-commitment` | low | bounded | `diginsight/smartdocs` | none found | `pending` |

### `SIG-4` — Diginsight's configuration extension crashes a local run in a git worktree

- **Kind** — `divergent-commitment`.
- **Goal** — a host that calls `ConfigureAppConfiguration2` starts locally from any folder: a git worktree, or a copy with no repository around it.
- **Scope** — in a local environment, `ConfigureAppConfiguration2` in `src/Diginsight.Components.Configuration/Hosting/Extensions/HostBuilderExtensions.cs` finds the repository root with `DirectoryHelper.GetRepositoryRoot`, in `Helper/DirectoryHelper.cs`. That helper walks up until it finds a `.git` *folder*. A git worktree has a `.git` *file* instead, so the helper returns null. The caller suppresses the null with `!` and passes it to `new DirectoryInfo(repositoryRoot)`, which throws `ArgumentNullException` (`Parameter 'path'`) from the host's `Program.Main`. The same happens in any folder with no repository above it. Observed on 2026-11-02 with SmartDocs at commit `875698d`, checked out by `git worktree add`.
- **Why it matters** — every host built on Diginsight's configuration extension refuses to start in a worktree. Comparing two commits side by side, which SmartDocs' roles plan prescribed for its regression check, had to use a second clone instead, and the next person to use a worktree hits the same crash without a clear message.
- **Target** — `diginsight/components`, `src/Diginsight.Components.Configuration/Helper/DirectoryHelper.cs` and `src/Diginsight.Components.Configuration/Hosting/Extensions/HostBuilderExtensions.cs`.
- **Existing landing** — none found. A search of the `diginsight` organization's issues for "worktree" and for `ConfigureAppConfiguration2` returned nothing.
- **State** — `pending`.
- **Relevance** — `medium`. A shared component in use by every Diginsight host fails on a standard git workflow.
- **Actionability** — `ready`. Accept a `.git` file as well as a `.git` folder as the repository marker, and when no root is found, skip the lookup of local settings files instead of dereferencing null.

### `SIG-1` — the framework's persisted component state is never emitted, because two components persist under one key

- **Kind** — `divergent-commitment`.
- **Goal** — a prerendered page carries the framework's `Blazor-WebAssembly-Component-State` payload, so `[PersistentState]` properties, `RegisterPersistentService`, `AddAuthenticationStateSerialization`, and `<AntiforgeryToken />` reach the WebAssembly client as .NET 10 documents.
- **Scope** — on every page, the host logs `ComponentStatePersistenceManager` `Error`: "There was an error executing a callback while pausing the application", with `System.ArgumentException: There is already a persisted object under the same key`. The likeliest cause is two instances of one component that persist the same `[PersistentState]` properties, such as `TopMenu` placed twice in `MainLayout.razor`, as `Placement="TopMenu.Group.Left"` and `Placement="TopMenu.Group.Right"`, each with `PersistedScope` and `PersistedRoot`. The framework derives a key from the component type and its parent, so the two collide, and the whole payload is dropped. Change `C10-persist-prerender-state` of `src/docs/90.00-issues/202610/20261001.01-perfanalysis/` already observed that the payload was missing, and worked around it with the host's `smartdocs-bootstrap` record. The error still appeared on every page throughout the end-to-end validation run of 2026-11-02.
- **Why it matters** — every framework feature that relies on persisted state silently does nothing on this host. Session 4 of the roles plan hit it twice: the browser lost the reader's sign-in state after hydration, and the sign-out form lost its antiforgery token. Both now travel in the bootstrap record instead. The next feature to rely on persisted state will fail the same way, unnoticed.
- **Target** — `diginsight/smartdocs`, `src/Diginsight.SmartDocs.Web.Client/Layout/TopMenu.razor.cs` and the components that persist state.
- **Existing landing** — none found.
- **State** — `pending`.
- **Relevance** — `medium`. A logged error on every page, and a framework mechanism that can't be used.
- **Actionability** — `bounded`. Confirm the colliding component, give each instance a distinct key with `@key`, and check that the payload appears. Whether the bootstrap record can then be retired is a separate decision.

### `SIG-2` — the top bar overflows on phone-width viewports

- **Kind** — `divergent-commitment`.
- **Goal** — the top bar's controls fit within the viewport at phone widths, down to 360 px.
- **Scope** — at 375 px, with four spaces and a signed-in reader, the top bar's content is 390 px wide, so the theme toggle ends 15 px past the right edge. That's measured after the roles plan's own fix, which lets the site title shrink with an ellipsis below 860 px and hides the sign-in label. Before this work item, the bar already measured 487 px at a 504 px viewport, so phone widths overflowed then too. The fixed-size controls are the space switcher, **About**, the sign-in menu, search, notifications, and the theme toggle.
- **Why it matters** — on a phone, the theme toggle can't be reached without scrolling the header horizontally.
- **Target** — `diginsight/smartdocs`, `src/Diginsight.SmartDocs.Web/wwwroot/app.css` and `src/Diginsight.SmartDocs.Web.Client/Layout/MainLayout.razor`.
- **Existing landing** — none found.
- **State** — `pending`.
- **Relevance** — `low`. A layout defect at widths no validation targets today.
- **Actionability** — `ready`. Below a phone breakpoint, gather the secondary controls, such as notifications and the theme toggle, into one overflow menu, or let them wrap.

### `SIG-3` — the footer's section line keeps its old count after a live push

- **Kind** — `divergent-commitment`.
- **Goal** — when a metadata push changes a section's article count, every count on screen shows the new value without a reload, including the footer's line for the section that holds the current page.
- **Scope** — on an article page, the footer shows two lines: the section that holds the page, and the site total. A push reaches `DynNav.OnAggregatesPushed`, in `src/Diginsight.SmartDocs.Web.Client/Layout/DynNav.razor.cs`, which updates the total and the sidebar's counts. The section line, though, is set only by `DynNavNode.OnParametersSetAsync`, in `src/Diginsight.SmartDocs.Web.Client/Layout/DynNavNode.razor.cs`, from the parent count it was given when it rendered, and nothing supplies a new one after the push. Observed on 2026-11-02: on Product B's release notes page, after a probe article was added to Product B, the footer read *Product B: 1 articles* and *Total: 8 articles*, while hovering Product B in the sidebar showed *Product B: 2 articles*. Both files are unchanged since commit `875698d`, before the roles work began.
- **Why it matters** — a reader watching a page sees two counts that disagree until they move the pointer or reload.
- **Target** — `diginsight/smartdocs`, `src/Diginsight.SmartDocs.Web.Client/Layout/DynNav.razor.cs` and `src/Diginsight.SmartDocs.Web.Client/Layout/DynNavNode.razor.cs`, and the footer statistics service they write to.
- **Existing landing** — none found.
- **State** — `pending`.
- **Relevance** — `low`. A display that is briefly out of date. It reveals nothing a reader may not see.
- **Actionability** — `bounded`. Either the push handler refreshes the selected section from the updated folder records, or the footer reads the section's count from the cached tree instead of keeping a copy; which fits the footer's priority tiers is still to be decided.

### `SIG-5` — the navigation bootstrap middleware sets `Content-Length` on a response that has already started

- **Kind** — `divergent-commitment`.
- **Goal** — `NavigationBootstrapMiddleware` never throws for a request whose response was started downstream.
- **Scope** — the middleware, in `src/Diginsight.SmartDocs.Web/Navigation/NavigationBootstrapMiddleware.cs`, buffers the body of every `GET`. When the response isn't a 200 HTML page, it sets `context.Response.ContentLength` and copies the buffer out, without checking `context.Response.HasStarted`. On 2026-11-02, once, right after the host started, that line threw `InvalidOperationException: Headers are read-only, response has already started`. The developer exception page couldn't run, and Kestrel logged the request as failed. The log doesn't name the request. A `GET` whose handler sends headers itself, such as a WebSocket upgrade or a flushed response, would reach that line in that state. The same lines existed at commit `875698d`, before the roles work began.
- **Why it matters** — the failing request ends with an error instead of its response, and the log shows an unhandled exception that hides real failures.
- **Target** — `diginsight/smartdocs`, `src/Diginsight.SmartDocs.Web/Navigation/NavigationBootstrapMiddleware.cs`.
- **Existing landing** — none found.
- **State** — `pending`.
- **Relevance** — `low`. One occurrence in a full validation run, with no visible effect on the page being tested.
- **Actionability** — `bounded`. Identify which requests reach that path with the response started, then either pass them through without buffering or skip the `Content-Length` and copy when `HasStarted` is true.
