---
title: "Issue: the collapsed sidebar flew the whole menu out on hover"
author: "Dario Airoldi"
date: "2026-09-09"
categories: [issue, bug-report, navigation, sidebar, blazor, css]
description: "Putting the menu away did not put it away: resting the pointer anywhere near the icon rail reopened the full navigation tree as a floating panel over the article."
publish: false
---

# The collapsed sidebar flew the whole menu out on hover

**Issue title:** Collapsed sidebar reopens the full menu on hover
**Date reported:** 2026-09-09
**Reporter:** Dario Airoldi
**Status:** Resolved
**Severity:** Medium
**Component:** `Diginsight.SmartDocs.Web.Client` (sidebar layout), `Diginsight.SmartDocs.Web` (`app.css`, `app-ui.js`)
**Framework:** .NET 10 / ASP.NET Core / Blazor WebAssembly

## 📚 Table of contents

- [🎯 Summary](#-summary)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction steps](#-reproduction-steps)
- [✅ Solution implemented](#-solution-implemented)
- [🧪 Verification](#-verification)
- [🎓 Lessons learned](#-lessons-learned)

## 🎯 Summary

Collapsing the sidebar was supposed to hand the width back to the article. It didn't: the tree was still one pointer movement away from covering it again, because the collapsed state kept the panel alive as a hover flyout pinned over the reading column. The rail sits against the left edge of the window, which is exactly where a pointer travels on its way to anything else, so the menu reappeared unbidden.

| Item | Result |
|---|---|
| Expected on hovering the rail | Nothing — the rail stays a rail |
| Observed on hovering the rail | The full navigation tree slid out over the article |
| Reopening the menu | Only possible by accident, or via the pin button hidden inside the flyout |

## 🔍 Context information

The collapsed sidebar renders a narrow icon rail carrying the top-level section icons plus two always-visible buttons: **Open menu** and **Search the menu**. The full tree lives in `.sidebar-panel`, which the collapsed state repositioned as `position: fixed` over the article and revealed on `:hover` and `:focus-within`.

## 🔬 Analysis

The flyout was deliberate — an imitation of the vertical-tab behaviour in Edge — and it had already been patched twice:

1. `display: none` → `display: block` on hover snapped, because `display` cannot be animated. That was replaced by a `visibility`/`opacity`/`transform` transition with asymmetric delays: 0.2s to open, 0.35s to close.
2. Collapsing the menu left the pointer resting exactly where the panel was about to appear, so the tree vanished and instantly sprang back. A `html.nav-no-flyout` guard in `app-ui.js` suppressed the flyout until the pointer had left the strip once under its own steam.

Both patches treat symptoms of the same premise: that a put-away menu should reopen itself. Neither could fix the underlying conflict — the rail occupies the window edge, so *any* delay is either too short to prevent accidental opening or too long to feel responsive.

The rail already carries an explicit **Open menu** button, so the flyout was not the only way back. It was simply the loudest one.

## 🔄 Reproduction steps

1. Open the Learning Hub at `http://localhost:5280/`.
2. Collapse the sidebar with the **Collapse menu** button.
3. Move the pointer over the icon rail, or merely across it.
4. The full navigation tree slides out over the article.

## ✅ Solution implemented

Reopening is now a decision, not an accident. The collapsed panel is `display: none` and nothing reveals it but a click.

| File | Change |
|---|---|
| `src/Diginsight.SmartDocs.Web/wwwroot/app.css` | Collapsed `.sidebar-panel` reduced to `display: none`; removed the flyout positioning, the `:hover`/`:focus-within` reveal, the `html.nav-no-flyout` counter-rule, and their reduced-motion entries. |
| `src/Diginsight.SmartDocs.Web/wwwroot/js/app-ui.js` | Removed the `nav-no-flyout` guard, which existed only to hold the flyout back. |
| `src/Diginsight.SmartDocs.Web.Client/Layout/DynNav.razor` | Removed the "Pin menu" toolbar button, unreachable now that the panel never renders while collapsed. |
| `src/Diginsight.SmartDocs.Web.Client/Layout/DynNavNode.razor` | Removed the `title` attributes that produced a native tooltip on every menu entry. |

## 🧪 Verification

See [the validation sequence](_validation/20260909.01-validation-sequence.md) — four scenarios, all PASS, run in a visible browser against a freshly built and restarted server.

## 🎓 Lessons learned

- A hover target on the window edge cannot be made deliberate by tuning delays. The pointer passes there for reasons that have nothing to do with the menu.
- Two successive patches to the same behaviour is a signal to re-examine the premise rather than add a third.
- `title` is not a substitute for a truncated label. The browser shows it on every element, not only on the ones that are actually clipped.
