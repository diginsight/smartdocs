---
title: "Issue: hiding a section only hid it from the menu, not from Explore"
author: "Dario Airoldi"
date: "2026-09-09"
categories: [issue, bug-report, preferences, explore, navigation, blazor]
description: "The eye toggle in preferences removed a section from the sidebar but left it whole in Explore — its chip, its articles and its counts all stayed, so the same preference meant two different things depending on which page you were looking at."
publish: false
---

# Hiding a section only hid it from the menu, not from Explore

**Issue title:** Section hidden in preferences still appears in Explore
**Date reported:** 2026-09-09
**Reporter:** Dario Airoldi
**Status:** Resolved
**Severity:** Medium
**Component:** `Diginsight.SmartDocs.Web.Client` (`ExplorePage`, `PreferencesDrawer`), `Diginsight.SmartDocs.Web` (`app.css`)
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

Preferences let you hide a section with an eye toggle. The sidebar honoured it; Explore did not. A
section you had put away still had its chip in the filter bar, its articles in the results, and its
weight in every count on the page — including the "Articles" and "Sections" figures in the hero.

The preference therefore meant one thing in the menu and another thing one click away, which is the
kind of inconsistency that makes a setting feel broken rather than partial.

## 🔍 Context information

`PreferencesState` keeps a `hidden` set of section labels, persisted to local storage, and raises
`Changed` whenever it moves. Two places read it: `DynNavNode` when it renders the sidebar children,
and `NavOrdering` when it sorts them. `ExplorePage` injected `PreferencesState` but only used it for
favourites and the "dated only" switch — the hidden set was never consulted.

The labels line up exactly: `ExplorePage.SectionOf` takes the first breadcrumb segment, which is the
same string `NavChild.Text` carries into the drawer. Nothing needed translating; the filter was
simply absent.

## 🔬 Analysis

Explore built four derived lists once, in `OnInitializedAsync`, straight from the full index:
sections with counts, authors, the author count and the latest date. `Results` then filtered the
full index again on every render.

That shape had two consequences. Adding a `Where` to `Results` alone would have removed the articles
but left the chips, the section count and the author list advertising content that could no longer
be reached. And because the derived lists were computed once, they could not react to a preference
toggled later in the session — `OnPrefsChanged` only asked for a re-render, and a re-render of a
list that never recomputes shows the same thing.

So the fix had to move the derivation off the raw index and onto a visible subset, and rebuild that
subset on the preference gesture rather than on every render.

## 🔄 Reproduction steps

1. Open Explore and note the hero figures and the section chips.
2. Open preferences and hide a section with the eye toggle.
3. Return to Explore.

**Before:** the sidebar had lost the section, but Explore still showed its chip, its articles and
its counts.

## ✅ Solution implemented

`ExplorePage` now keeps `_visible` — the index minus every article whose section is hidden — and
derives the chips, the authors, the author count and the latest date from it. `Results` starts from
`_visible` rather than the raw index, and `Rebuild()` runs on `Changed` so a toggle mid-session is
reflected immediately.

Two details follow from the disappearance being total:

- A filter left pointing at something now hidden would empty the page with no way to tell why, so
  `Rebuild()` drops a `_section` or `_author` selection that no longer has anything behind it.
- Hiding a large section can remove hundreds of articles at once. A dashed, muted chip reports how
  many sections are hidden and brings them back when clicked, so the missing content is stated on
  the page rather than left to be discovered.

The drawer tooltip changed from "Hide from menu" to "Hide from the menu and from Explore", because
the old wording described the bug rather than the intent.

## 🧪 Verification

Validated in a visible browser against a live server — see
[`_validation/20260909.02-validation-sequence.md`](_validation/20260909.02-validation-sequence.md).
Five scenarios, all passing: the chip and its articles leave together, the counts follow, a selected
chip that gets hidden releases the filter, the preference survives a reload, and the indicator
restores everything.

## 🎓 Lessons learned

A preference is a promise about the whole application, not about the control nearest to it. The
sidebar honoured `IsHidden` from the day it was written, and that was enough to make the feature
look finished — the gap only showed on a page whose job is to list everything.

Worth checking the same way: the top bar filters on folder metadata (`TopbarHidden`), not on user
preferences, so it has the same shape of gap and has not been addressed here.
