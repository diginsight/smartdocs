---
title: "Analysis: what feature/newstyle should give back to main"
author: "Dario Airoldi"
date: "2026-09-25"
categories: [analysis, branch-comparison, performance, caching, navigation, blazor]
description: "feature/newstyle is twenty commits of Learning Hub work sitting on top of main. Most of them are not about the Learning Hub at all — they are performance, caching and publishing fixes that main is still paying for."
publish: false
---

# What `feature/newstyle` should give back to `main`

**Date:** 2026-09-25
**Author:** Dario Airoldi
**Status:** ✅ Tier A applied to `main` (items 1–7) — Tier B/C still open
**Branches compared:** `main` (`6675c3f`) vs `origin/feature/newstyle`
**Component:** whole solution
**Framework:** .NET 10 / ASP.NET Core / Blazor Web App (server prerender + WASM)

## 📚 Table of contents

- [🎯 Summary](#-summary)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
  - [Tier A — take as-is](#tier-a--take-as-is)
  - [Tier B — take with care](#tier-b--take-with-care)
  - [Tier C — gate before taking](#tier-c--gate-before-taking)
  - [Do not take](#do-not-take)
- [✅ Recommended sequence](#-recommended-sequence)
- [🧪 Verification](#-verification)
- [🎓 Lessons learned](#-lessons-learned)

## 🎯 Summary

`feature/newstyle` is **not a divergent line of development**. Its merge-base with `main` *is* main's HEAD, so the branch is strictly twenty commits ahead and nothing exists in `main` that is missing from it. A fast-forward is mechanically possible today.

That makes the question "merge or not?" the wrong one. The right question is **which of the twenty commits belong in a product that must serve both the Learning Hub and smart documentation**, because only about a third of them are actually about the Learning Hub.

| Item | Result |
|---|---|
| merge-base(`main`, `newstyle`) | `6675c3f` — main's HEAD |
| Commits in `newstyle` not in `main` | 20 |
| Commits in `main` not in `newstyle` | **0** |
| Overall diff | 44 files, +4062 / −219 |
| Of which `app.css` | +1658 / −20 (1329 → 2968 lines) |

The headline finding is that the **most valuable changes on the branch are branch-agnostic**: a 120× page-response win, a cache-freshness bound, a five-requests-to-one fix, and a publish pipeline that no longer lies about having invalidated the cache. None of them know what a Learning Hub is. Main is currently paying every one of those costs.

The single largest measured win is also the cheapest to take:

> `Observability:DebugEnabled` routes every log record through `System.Diagnostics.Debug`, at roughly **38 ms per line**, paid synchronously inside the request. Turning it off took `/_nav` from **4.9 s to 0.028 s** and an article page from **5.0 s to 0.031 s**.

`newstyle` fixed this in `appsettings.devlearn.json`. **`appsettings.devdocs.json` — the SmartDocs profile — still has `"DebugEnabled": true` on both branches.** The documentation scenario has never had the fix at all.

> **Status, 2026-09-25:** all seven Tier A items are now applied to `main`'s working tree — 18 files, compiling with zero diagnostics, not yet committed or run. Tier B, Tier C and the `Branding.Shell` decision are untouched. Per-item detail is in [🔬 Analysis](#-analysis); the file list and what was verified are in [🧪 Verification](#-verification).

## 🔍 Context information

The twenty commits, oldest first:

```
d78e51c feat: reader-controlled look, ordering and filtering
a8a8e89 Lighten the iconography and remove decorative borders
f9c1a42 Make the classic blue the light-mode default
1df8c95 Bring the shell back to the approved prototype
fea9bf4 Make the library the landing page
23504a9 Carry the date as data, not as a prefix in the title
59783d9 Make in-page links work and settle the top bar
3939c96 Tell the sidebar's four levels apart
9e42b45 Make closing the menu a movement rather than a jump
fc223f8 Stop paying 38ms a line to write logs nobody reads
d1a1407 Answer a menu click with one request instead of five
e56f7dc Let the menu filter wait until you stop typing
439fbc4 Bound how long a lost publish notification can hide content
01e65fe Make publishing prove the site was told about it
83ea22b Warm the navigation cache that invalidating just dropped
ebc1e9d Stop dropping the warm that a close-following invalidation needs
4918674 Refine accessibility text for session recognition and improve tooltip usage
98ab672 feat(sidebar): resolve hover flyout issue in collapsed state
0fe6183 Add documentation for issue with hidden sections in Explore
6e744ec fix(theme): update initial theme to DefaultLight
```

Read in order, the branch has two halves. The first six commits restyle the shell for the Learning Hub and take over the site root. Everything from `59783d9` onwards is ordinary engineering that happened to be done on that branch: bug fixes, cache correctness, request-count reduction, CI hardening.

The two scenarios already exist as configuration, not as code: `appsettings.devdocs.json` (space `diginsight.smartdocs`, "Diginsight documentation") and `appsettings.devlearn.json` (space `learn`, "Learning Hub"). They differ only in `Site.Title`, `Site.Branding`, and the space definition. **No code on either branch branches on which of the two is loaded.** That is the fact the whole dual-scenario question turns on.

## 🔬 Analysis

### Tier A — take as-is

Branch-agnostic. No coupling to the Learning Hub, no visual consequence, no decision required.

> **✅ All seven items below are applied to `main` and compile clean.** Twelve of the files were taken
> wholesale from `newstyle` (they carry no Tier B/C commit at all); `Program.cs`, `ThemeState.cs`,
> `app-ui.js` and `DynNav.*` are mixed files and were taken surgically. Per-item notes below record
> what was deliberately left behind.

#### ✅ 1. Stop paying 38 ms a line (`fc223f8`)

`Observability.DebugEnabled` sends every log record through `System.Diagnostics.Debug`. On macOS and Linux that write costs ~38 ms and is paid **in the request**. Isolated measurements from the commit:

| Configuration | `/_nav` warm |
|---|---|
| Both sinks off | 0.034 s |
| log4net alone | 0.11 s |
| Debug sink alone | 5.2 s warm, 40 s cold |

**Action for main:** set `"DebugEnabled": false` in `appsettings.devdocs.json`. One line. This is independent of every other item here and should not wait for them.

**Applied.** `newstyle` has *no* commit touching `appsettings.devdocs.json`, which independently confirms the finding — so this was written fresh rather than taken, adding an `Observability` block with `DebugEnabled: false` plus the measurement comment.

#### ✅ 2. Bound how long a lost publish notification can hide content (`439fbc4`)

New `ContentFreshnessOptions`, bound eagerly in `Program.cs` and threaded into `CachedContentSource` and `CachedDynamicNavBuilder`:

```csharp
public sealed class ContentFreshnessOptions
{
    public TimeSpan Content  { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan Missing  { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan NavLevel { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan? NavIndex { get; set; }   // null = inherit Diginsight:SmartCache:MaxAge
}
```

The design rests on a SmartCache property worth stating plainly: **`MaxAge` is a read-time freshness tolerance, not a write-time TTL.** Nothing is evicted on a timer; a reader simply declines to accept an entry older than the age it asks for. So the four values are not expiries — they are *how stale a given reader is willing to be*, which is why they can differ per call site without any coordination.

The asymmetry between `Content` and `Missing` is the interesting part. A stale **hit** shows slightly old prose; a stale **miss** hides a freshly published article completely. The second failure is much worse than the first, so a cached not-found is re-read with the shorter tolerance:

```csharp
CachedContent envelope = await ReadAsync(key, contentKey, freshness.Content, ct);
if (envelope.Result is null && freshness.Missing < freshness.Content)
    envelope = await ReadAsync(key, contentKey, freshness.Missing, ct);
```

`NavIndex` is deliberately left null: rebuilding the flat index walks every article — **14–16 s across 1,133 articles** — and putting a bound on it would hand that wait to whichever visitor arrives first after it lapses.

This matters more for documentation than for the Learning Hub, because documentation is published by CI and the reader has no way to know a publish was missed.

**Applied.** `ContentFreshnessOptions.cs`, `CachedContentSource.cs` and `CachedDynamicNavBuilder.cs` taken as-is; `Program.cs` took only the `439fbc4` hunks (bind + thread into both constructors), leaving out that commit's Tier B/C service registrations.

#### ✅ 3. Make publishing prove the site was told (`01e65fe`)

`03.PublishDocsContent.yml` treated invalidation as best-effort: an unresolved resource group or hostname was logged and the workflow went green, so content could be live in blob storage and invisible on the site with nothing to show for it. Now an unresolved value is `Write-Error; exit 1`, the call retries on `@(5, 15, 30, 60)`, and the response body is parsed for `version` so that a 200 from something that is not the invalidate endpoint is rejected.

Documented limitation, worth carrying across with the change: a single request reaches **one instance**. Cross-instance eviction needs `Diginsight:SmartCache:ServiceBus`.

**Applied.** `.github/workflows/03.PublishDocsContent.yml` taken as-is.

#### ✅ 4. Warm what invalidation just dropped (`83ea22b`, `ebc1e9d`)

Invalidation drops the nav index. The next reader pays 14–16 s to rebuild it. `WarmInBackground()` moves that cost off the reader and onto the publish.

`ebc1e9d` is the one to read carefully, because the obvious implementation is wrong:

```csharp
private int _warming;
private int _warmRequested;

public void WarmInBackground()
{
    Interlocked.Exchange(ref _warmRequested, 1);
    if (Interlocked.Exchange(ref _warming, 1) == 1) return;
    _ = Task.Run(async () =>
    {
        do
        {
            while (Interlocked.Exchange(ref _warmRequested, 0) == 1)
            {
                try { await GetIndexAsync(); await WarmAllLevelsAsync(); }
                catch (Exception ex) { logger.LogWarning(ex, "Nav warm-up after invalidation failed"); }
            }
            Interlocked.Exchange(ref _warming, 0);
        }
        while (Volatile.Read(ref _warmRequested) == 1 && Interlocked.Exchange(ref _warming, 1) == 0);
    });
}
```

A plain "already warming? then skip" guard silently drops the second of two back-to-back invalidations, so the warm completes against content that has since been invalidated again and the index is left cold. Measured: **14.6 s for the next reader before the drain-flag fix, 0.11 s after.** Two rapid publishes is exactly what a CI pipeline does.

**Applied.** `CachedDynamicNavBuilder.cs` (drain flag) and `NavEndpoints.cs` (the `WarmInBackground()` call after invalidation) taken as-is.

#### ✅ 5. Answer a menu click with one request instead of five (`d1a1407`)

Opening a page probed up to five candidate filenames **from the browser**, so four of every five requests 404'd and shouted in the console. New `/_page` and `/_page/{**path}` endpoints run the candidate loop server-side and answer once.

The status-code design is the part to preserve: **204 No Content** means "no file backs this route" — the ordinary case for a section folder — while 404 keeps its real meaning of "no such endpoint". The resolved key comes back in `X-Content-Key` so relative images still resolve. `PageLoader.Candidates` was widened from `private static` to `public static` so the client and server loops cannot drift apart.

Shared contract in `IContentSource.cs`:

```csharp
ContentResolution: Unhandled | Nothing | Found(key, content)
```

`Unhandled` lets the client fall back to probing when the server does not implement the endpoint, so the change is safe to deploy in either order.

**Applied.** `ContentEndpoints.cs`, `IContentSource.cs`, `PageLoader.cs` and `HttpContentSource.cs` taken as-is — none of the four is touched by any Tier B/C commit.

#### ✅ 6. Correct the caching documentation (`439fbc4`)

`src/docs/03.00-architecture/05-caching-and-invalidation.md` documents two things that **are not true of main today**:

- `WatchForChanges: true` is declared on filesystem spaces and bound by `SpaceOptions`, but **read by no code**. The application watches nothing and rescans on no schedule. Both `appsettings.devdocs.json` and `appsettings.devlearn.json` set it.
- `Diginsight:SmartCache:Enabled` **does nothing** — `SmartCacheCoreOptions` has no `Enabled` property. It has `Mode`, which resolves to `InMemory`. The cache is active regardless of what that key says.

Both are documentation bugs in main independent of anything else on the branch.

**Applied.** `05-caching-and-invalidation.md` taken as-is, along with the `appsettings.json` / `appsettings.devlearn.json` `ContentFreshness` defaults the doc now describes.

#### ✅ 7. Two small fixes worth taking on their own merits

- **`ThemeState.Toggle()` loses the reader's palette.** It returns to the built-in defaults rather than the last light/dark theme the reader chose. `newstyle` remembers `lastLight`/`lastDark`. This is a bug fix, not styling.
- **In-page `#hash` links do not work** (`59783d9`). With `<base href="/">`, an anchor resolves against the base rather than the current URL, so every table-of-contents link navigates away. `main` has no hash handling in `app-ui.js` at all — the bug is present there too, just less obvious than on a library landing page. The fix is a capture-phase window handler with a `reveal`/`honour`/`settled` re-assert loop, cancelled by any wheel or keypress.
- **The menu filter repaints on every keystroke** (`e56f7dc`). Debouncing by separating "what the box shows" from "what the list is filtered by" took 8 keystrokes from 6 repaints to 2, and first-hit latency from 724 ms to 464 ms.

**Applied, all three — surgically, because all three files are mixed.**

- `ThemeState.cs`: took `lastLight`/`lastDark` and the new `Toggle()` only. Left behind the five signature palettes, the `Signature` record flag, and the `DefaultLight`/`DefaultDark` changes (`cosmo`→`azure`, `github-dark`→`aurora`) — those are Tier C styling. The comment's example palette was changed from newstyle's "Forest" to "Nord", which is the nearest palette that exists on `main`.
- `app-ui.js`: appended only the 133-line in-page-anchors IIFE from `59783d9`. Left behind the reading-preferences mirror (Tier B) and the `/` hotkey targeting `.topsearch input` (Tier C — that element only exists in the newstyle shell).
- `DynNav.razor` / `.razor.cs`: took the `_query`/`_applied` split, `SearchDebounceMs`, the `CancellationTokenSource` debounce and its disposal. Left behind the pin/count/ordering additions that newstyle's versions of the same two files also carry.

> **Note on method.** `git apply -3` is unsafe on these mixed files: the patch's pre-image blob sits on top of Tier B/C commits, so three-way reconstruction silently drags their content in. It did exactly that on `app-ui.js` (341 → 525 lines, with an empty "ours" side). The reliable approach is to extract the wanted hunk, `git checkout HEAD --` the file, and re-apply by hand.

### Tier B — take with care

Portable, but with consequences that need a decision.

#### Date as data, not as a title prefix (`23504a9`)

`NavRules.Label()` stops folding the `YYYYMMDD` prefix into the title, so `"20260813 - MAI-Thinking-1 observation…"` becomes `"MAI-Thinking-1 observation…"`. `WithDatePrefix()` is removed in favour of `DateFromName()`, and `DynamicNavBuilder` now falls back `FrontMatter.ParseDate(fm.Date) ?? NavRules.DateFromName(...)`.

This is the right model — a date is a field, not a string decoration — but it **changes displayed labels sitewide**, including in the documentation scenario where the numeric prefixes are a deliberate ordering convention that readers are used to seeing. Take it, but take it as its own change with its own before/after screenshots.

It also introduces `NavRules.HomeRoute = "home"` and makes the root Home entry point at it, with matching skips in `DynamicNavBuilder.WalkAsync` and `FolderMetricsIndex` so the shortcut does not inflate the library count. Those skips are only correct if the Home entry exists, which it only does once the route change lands — so this commit and the routing change in Tier C are coupled in one direction.

#### `NavOrdering` and `PreferencesState`

`NavOrdering` (new, 177 lines) is a **pure re-projection of the server's curated list** — sorting, partitioning into pinned/others, and multi-token search. It can never change which nodes exist, which makes it safe by construction. Multi-token search alone took `"copilot agent"` from 3 results to 18.

`PreferencesState` (new, 238 lines) is memory-authoritative and localStorage-mirrored, so the shell renders correctly during prerender. Pins and hides are keyed by **section label** rather than path, so they survive folder renumbering.

Both are additive and inert until something surfaces them. They can land ahead of any UI decision.

### Tier C — gate before taking

Genuinely Learning-Hub-shaped. Taking these unconditionally would make SmartDocs *be* a Learning Hub rather than support one.

#### The root route

`ContentPage.razor` moves from `@page "/"` to `@page "/home"`, and the new `ExplorePage.razor` (388 lines) claims `@page "/"` and `@page "/explore"`.

**This is the single most consequential change on the branch.** For a documentation site, the root document is the front door: it is what a repository README links to, what search engines index, and what every existing deep link assumes. Displacing it to `/home` is correct for a browsable library and wrong for a documentation set.

#### The shell

`MainLayout.razor` **removes** `<TopMenu Placement="Left|Right" />`, `<AboutMenu />` and `<SearchOverlay />`, and adds the ambient background, density/font attributes, an always-on topbar search, the Explore/Timeline toggle and `<PreferencesDrawer />`. `AboutMenu.razor` is deleted outright; `TopMenu.razor`, `TopMenuDropdown.razor.cs` and `SearchOverlay.razor.cs` survive as **orphans** — still compiled, no longer referenced.

There is a knock-on that is easy to miss: commit `1df8c95` notes that the `topbar-hidden` / `topbar-align` metadata keys **no longer do anything**, and that **six `metadata.yml` files in the content repository still set them**. That is a live authoring contract that has been silently broken. It needs either a decision or a documentation update, and it needs one regardless of how the branch question is resolved.

#### The hardcoded string

`ExplorePage.razor:16` carries `<p class="hero-eyebrow">Learning Hub</p>`. It is the **only** hardcoded "Learning Hub" string in `src`. Everything else already comes from `SiteOptions`. Moving it to `Branding.ProductName` costs one line and removes the last hard coupling.

#### The gating hook already exists

`BrandingOptions` declares:

```csharp
/// <summary>Named theme applied on first load; users may override it locally.</summary>
public string DefaultTheme { get; set; } = string.Empty;
```

Grep confirms **nothing reads it, on either branch**. So there is already precedent — and an unfinished one — for per-deployment shell configuration in exactly the place a shell mode belongs. Adding something like `Branding.Shell = Library | Documentation` alongside it, with the root route and the topbar composition keyed off it, turns Tier C from a fork into a configuration switch. The two `appsettings.dev*.json` profiles then differ in one more line and in nothing else.

That is the shape of the answer to "support both scenarios": **the branch is not wrong, it is unconditional.**

### Do not take

`global.json` moves `"version": "10.0.400"` → `"10.0.301"`. Commit `d78e51c` says so itself: *"global.json is relaxed to SDK 10.0.301 because 10.0.400 is not installed on this machine. That line is a local workaround, not an intended upstream change."*

## ✅ Recommended sequence

Ordered by value per unit of risk. Each step is independently shippable.

| # | Change | Tier | Risk | Why now | Status |
|---|---|---|---|---|---|
| 1 | `DebugEnabled: false` in `appsettings.devdocs.json` | A | None | 120× on the profile that never got the fix | ✅ done |
| 2 | `ContentFreshnessOptions` + wiring | A | Low | A lost invalidation currently hides content indefinitely | ✅ done |
| 3 | `WarmInBackground()` + `NavEndpoints` call | A | Low | 14.6 s → 0.11 s; note the drain flag is load-bearing | ✅ done |
| 4 | Hardened publish workflow | A | Low | CI currently goes green on a failed invalidation | ✅ done |
| 5 | `/_page` one-call resolve | A | Low | 5 requests → 1; `Unhandled` makes deploy order irrelevant | ✅ done |
| 6 | Caching doc corrections | A | None | Two documented behaviours do not exist | ✅ done |
| 7 | `ThemeState` last-palette, `#hash` fix, filter debounce | A | Low | Standalone bug fixes | ✅ done |
| 8 | `NavOrdering` + `PreferencesState` | B | Low | Additive; inert until surfaced | open |
| 9 | Date-as-data | B | Medium | Changes labels sitewide; ship with screenshots | open |
| 10 | `Branding.Shell` switch (+ finish `DefaultTheme`) | C | Medium | The prerequisite for everything below | open |
| 11 | Explore page, shell recomposition, root route | C | High | Only behind #10 | open |
| — | `global.json` downgrade | — | — | **Never** | not taken |

Steps 1–7 are roughly a third of the branch's insertions once `app.css` is set aside, and carry essentially all of its measured performance gain.

A note on the CSS: `app.css` grows by 1658 lines and **deletes 20**. That near-total additivity is good news — the new styling sits alongside the old rather than replacing it, so it can be adopted in pieces or scoped under a shell-mode class rather than merged wholesale.

## 🧪 Verification

### Of the analysis

The findings rest on:

- `git merge-base` and `git log main..origin/feature/newstyle` for topology
- `git diff --stat` and per-file diffs across all 44 changed files
- Commit bodies for the measured numbers (they carry before/after timings inline)
- `grep` across both branches for `DefaultTheme`, `WatchForChanges`, `SmartCache:Enabled`, `DebugEnabled` and `"Learning Hub"`

### Of the Tier A adoption

Eighteen files changed. Twelve were taken wholesale — pre-checked by grepping their `using` directives on `newstyle` to confirm none reaches for `NavRules`, `PreferencesState`, `NavOrdering` or `LibraryQueryState`, so no Tier B/C type could arrive by the back door:

```
src/Diginsight.SmartDocs.Web/Caching/ContentFreshnessOptions.cs   (new)
src/Diginsight.SmartDocs.Web/ContentSources/CachedContentSource.cs
src/Diginsight.SmartDocs.Web/Navigation/CachedDynamicNavBuilder.cs
src/Diginsight.SmartDocs.Web/Endpoints/NavEndpoints.cs
src/Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs
src/Diginsight.SmartDocs.Web.Shared/IContentSource.cs
src/Diginsight.SmartDocs.Web.Shared/Services/PageLoader.cs
src/Diginsight.SmartDocs.Web.Client/HttpContentSource.cs
src/Diginsight.SmartDocs.Web/appsettings.json
src/Diginsight.SmartDocs.Web/appsettings.devlearn.json
src/docs/03.00-architecture/05-caching-and-invalidation.md
.github/workflows/03.PublishDocsContent.yml
```

Six were written or edited by hand: `Program.cs`, `appsettings.devdocs.json`, `ThemeState.cs`, `app-ui.js`, `DynNav.razor`, `DynNav.razor.cs`.

`Diginsight.SmartDocs.Web`, `.Web.Client` and `.Web.Shared` compile with **zero diagnostics**. Verified with `dotnet msbuild src/Diginsight.SmartDocs.slnx -t:Compile`, then by timestamp on the three freshly written `obj/Debug/net10.0/*.dll`. A full `dotnet build` additionally fails at `_CopyFilesMarkedCopyLocal` with MSB3026/MSB3027 — the running dev server holds a lock on the output DLLs. That target runs *after* `CoreCompile`, so it says nothing about the code.

### Still to do

Nothing here has been committed, and none of it has been exercised at runtime. Before adopting, the numbers worth re-measuring locally are the 38 ms/line Debug-sink cost (platform-dependent — it is cheap on Windows and ruinous on macOS/Linux) and the 14–16 s index rebuild (it scales with article count, and the documentation set is smaller than the 1,133-article Learning Hub). The `#hash` handler and the filter debounce are both browser behaviour and want a manual pass.

## 🎓 Lessons learned

**A branch named after styling was carrying the performance work.** Twelve of twenty commits have nothing to do with the Learning Hub. Had the branch been merged as a unit, or abandoned as a unit, the same mistake would have been made in either direction. Branch names describe intent at the moment of creation; they stop being accurate about a week later.

**`MaxAge` as read-time tolerance is the idea that makes the freshness design work.** Because staleness is negotiated by the reader rather than enforced by the writer, different call sites can hold different opinions with no coordination. That is what lets a cached miss be treated more suspiciously than a cached hit — a distinction that would be awkward to express with expiry-based caching.

**Dead configuration accumulates quietly.** Three separate keys — `WatchForChanges`, `SmartCache:Enabled`, `Branding.DefaultTheme` — are bound, documented or declared but read by nobody, and two of them are documented as working. Configuration that is never read is worse than configuration that is absent, because it answers questions wrongly.

**"Supports both scenarios" is a statement about configuration, not about code.** The two scenarios already differ only in JSON. Every unconditional change in Tier C is unconditional by omission rather than by design, and each has an obvious home in `SiteOptions`. The branch did not choose the Learning Hub over documentation — it simply never had occasion to ask.

**The tier split turned out to be a file split, which is why Tier A was cheap.** Twelve of the eighteen files carry no Tier B/C commit at all, so they are exact `git checkout` takes rather than merges. That is not luck — performance work and styling work touch different files — but it is worth checking for deliberately, because it is the difference between an afternoon and a week.

**`git apply -3` is the wrong tool for cherry-picking out of a stack.** Its three-way reconstruction resolves against the patch's *pre-image blob*, which on a twenty-commit branch already contains everything below it. Applying the anchors patch to `app-ui.js` silently brought in two unrelated IIFEs from earlier commits, and reported only "applied with conflicts" — with an empty "ours" side, which looks like success. Extract the hunk, `git checkout HEAD --` the file, append by hand. The line count is the tell: 341 → 525 when 474 was expected.
