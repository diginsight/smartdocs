---
title: "Signals — roles and permissions on the document hierarchy"
author: "Dario Airoldi"
date: "2026-10-28"
categories: [signals, blazor, prerendering, layout]
description: "Activities surfaced while validating sign-in for the roles and permissions work item that were never in scope for its goal."
publish: false
---

# Signals — roles and permissions on the document hierarchy

Activities this work item's conversation surfaced that were **never in scope** for its goal: restricting spaces to readers who hold a permission. Each record is self-contained, so someone reading it without this conversation can act on it as written.

**Identifiers are identity; the listing order is priority**, derived from relevance and actionability.

📖 Record shape, kinds, sweep, and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

Both records were added on 2026-10-28, during `E7-validate-sign-in` of [the implementation plan](01-roles-permissions-support.plan.md). Neither was introduced by this work item.

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-1` | `divergent-commitment` | medium | bounded | `diginsight/smartdocs` | none found | `pending` |
| 2 | `SIG-2` | `divergent-commitment` | low | ready | `diginsight/smartdocs` | none found | `pending` |

### `SIG-1` — the framework's persisted component state is never emitted, because two components persist under one key

- **Kind** — `divergent-commitment`.
- **Goal** — a prerendered page carries the framework's `Blazor-WebAssembly-Component-State` payload, so `[PersistentState]` properties, `RegisterPersistentService`, `AddAuthenticationStateSerialization`, and `<AntiforgeryToken />` reach the WebAssembly client as .NET 10 documents.
- **Scope** — on every page, the host logs `ComponentStatePersistenceManager` `Error`: "There was an error executing a callback while pausing the application", with `System.ArgumentException: There is already a persisted object under the same key`. The likeliest cause is two instances of one component that persist the same `[PersistentState]` properties, such as `TopMenu` placed twice in `MainLayout.razor`, as `Placement="TopMenu.Group.Left"` and `Placement="TopMenu.Group.Right"`, each with `PersistedScope` and `PersistedRoot`. The framework derives a key from the component type and its parent, so the two collide, and the whole payload is dropped. Change `C10-persist-prerender-state` of `src/docs/90.00-issues/202610/20261001.01-perfanalysis/` already observed that the payload was missing, and worked around it with the host's `smartdocs-bootstrap` record.
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
