---
title: "Signals — startup and navigation at any scale"
author: "Dario Airoldi"
date: "2026-10-01"
categories: [signals, performance, caching, observability]
description: "Activities surfaced by the startup and navigation analysis of 2026-10-01 that were never in scope for its goal of smooth startup and navigation."
publish: false
---

# Signals — startup and navigation at any scale

Activities this work item's conversation surfaced that were **never in scope** for its goal — analyzing startup, navigation, and caching in SmartDocs and designing them for an unlimited number of documents. Each record is self-contained: delivery is manual, so a person reading it in another repository — without this conversation and without this work item — must be able to act on it as written.

**Identifiers are identity; the listing order is priority** — derived from relevance and actionability, never assigned by impression.

📖 Record shape, kinds, sweep, and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

The table lists the six records in priority order. `SIG-5` earns this page on relevance and the next three on actionability; `SIG-6` is closed and `SIG-3` resolves to an existing landing, so neither carries a priority and both are listed last. `SIG-4` and `SIG-5` were added on 2026-10-02 while wave 1 of the analysis was implemented, and `SIG-6` the same day while a deployed instance was measured.

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-5` | `upstream-feedback` | high | ready | `diginsight/smartdocs` | partial: `SIG-2` of `20260925.02-startup-optimization` covers one of the seven statements | `pending` |
| 2 | `SIG-1` | `divergent-commitment` | medium | ready | `diginsight/telemetry` | none found | `pending` |
| 3 | `SIG-2` | `divergent-commitment` | low | bounded | `diginsight/smartcache` | none found | `pending` |
| 4 | `SIG-4` | `divergent-commitment` | low | bounded | `diginsight/smartcache` | none found | `pending` |
| — | `SIG-6` | `divergent-commitment` | — | — | `diginsight/smartdocs` | none found | `closed: done on 2026-10-02` |
| — | `SIG-3` | `upstream-feedback` | — | — | `diginsight/smartdocs` | `SIG-2` of `20260925.02-startup-optimization` | `routed → SIG-2 of 20260925.02-startup-optimization` |

No record is `low` and `open`, so there's no `other-signals` page. `SIG-2` stays here on actionability although its relevance dropped to `low` on 2026-10-01: see its record.

### `SIG-5` — the generated reference pages describe navigation, endpoints, and settings as they were before wave 1

- **Kind** — `upstream-feedback`.
- **Goal** — the generated architecture, use-case, and reference pages describe navigation exclusions, HTTP caching, configuration keys, and cache entries as the code has them after wave 1 of `src/docs/90.00-issues/202610/20261001.01-perfanalysis/01-startup-and-navigation-optimization.analysis.md`.
- **Scope** — eight statements on six generated pages, each carrying a `verification_stamp`, so the documentation stream regenerates them rather than anyone editing them by hand:
  1. `src/docs/06.00-reference/index.md` says `99.00-temp` is excluded "at the root only". The infrastructure names — `src`, `deploy`, `docs`, `scripts`, `readme_files`, `bin`, `obj`, `node_modules`, `99.00-temp` — are now excluded at the top of every space, build output at any depth, and asset folders at any depth.
  2. `src/docs/06.00-reference/06-navigation-rules.md` has a "Root level only" row for `99.00-temp` and lists `IsAssetFolder` as `images`, `img`, `assets`, `media`, `attachments`, `files`. The list now includes `asset`, `Site:AssetFolders` adds names, an asset folder is never an entry at all, and menus hide a section whose complete count is zero.
  3. `src/docs/03.00-architecture/04-shared-library.md` gives the same asset-folder list, and doesn't know `FrontMatter.ParseHead`, `ArticleHead`, or `NavRules.WithoutEmptySections`.
  4. `src/docs/06.00-reference/02-http-endpoints.md` describes `/_content` and `/_nav/children` without validators. `/_page`, `/_content`, and `/_nav/children` now send an `ETag` and `Cache-Control` and answer `If-None-Match` with `304`; JSON, Markdown, plain-text, and SVG responses are compressed; and a page route naming a file the content doesn't hold is answered `404` before prerendering.
  5. `src/docs/04.00-use-cases/01-reading-a-document.md` describes the `/_content` flow without its validators, and without the `max-age` assets now carry.
  6. `src/docs/06.00-reference/01-configuration-settings.md` doesn't list `Site:AssetFolders`, an overlay-only array, or `Diginsight:SmartCache:SizeLimit`, 64,000,000 in the base settings.
  7. `02-http-endpoints.md` and `01-reading-a-document.md` also don't know that, since `C32`, a page route naming nothing in the content answers `404` before prerendering, with a self-contained page, or that the site serves a `robots.txt`; `06-navigation-rules.md` doesn't know that every link the application writes is rooted.
  8. `src/docs/03.00-architecture/05-caching-and-invalidation.md` predates parsed-header entries (`article-head`, `folder-meta`), the size cap, the body-size floor, the browser tier's validators, and the rediscovery an empty-path invalidation now starts.
- **Why it matters** — these pages are the reference readers and agents use to reason about the system, and their stamps still mark them current. After wave 1 they misstate which folders reach the menus, what the endpoints send, and which settings exist.
- **Target** — `diginsight/smartdocs`, the documentation stream: the change-driven mode of `@ad-documentation-manager`, entered through `/01.04-ad-docs-update-from-changes`.
- **Existing landing** — partial. `SIG-2` in `src/docs/90.00-issues/202609/20260925.02-startup-optimization/01-signals.md`, `pending`, asks for the caching chapter of statement 8, and this page's `SIG-3` is routed to it. None found for statements 1–7.
- **State** — `pending`.
- **Relevance** — `high`. The pages now contradict the code.
- **Actionability** — `ready`. The change set, the pages, and the stream are known, and the work list follows from the change set without judgement.
- **Actionability strategy** — lands as one change-driven documentation run whose change set runs from wave 1 through `C32` — commits `35901a7` to `5f58c29`. The caching chapter joins the same run, which lets whoever runs it close `SIG-2` of the earlier work item and update `SIG-3` here.

### `SIG-1` — Diginsight.Core sizes a byte array one boxed element at a time

- **Kind** — `divergent-commitment`.
- **Goal** — make the heuristic size of an array of unmanaged elements cost constant time, so a SmartCache store of file or blob bytes adds no per-byte CPU or allocation.
- **Scope** — one method and one gap. `RuntimeUtils.GetSizeHeuristically` delegates to `SizeCalculator.Get`, whose `IEnumerable` branch enumerates an array element by element and calls `CoreGet` on each boxed element. No `IHeuristicSizeProvider` handles arrays — the only registered provider is the `JToken` one in `Diginsight.Json` — so a `byte[]`, `char[]` or `int[]` takes that branch. The change is a fast path for arrays whose element type is unmanaged: length times element size, plus the array header. Out of scope: object graphs of reference types, and the depth limit.
- **Why it matters** — SmartCache 3.8.0.2 sizes the key and the value on every `SetValue`. A consumer that caches bytes therefore pays one boxed visit per byte on every store: in `Diginsight.SmartDocs.Web`, every cached Markdown article — tens of kilobytes each — goes through it, and every refresh after the cache's read tolerance lapses does it again. The cost wasn't isolated in a measurement: SmartDocs' `SetValue` activities took 30 ms at the median and 214 ms at p95 during a startup warm-up, but that figure includes instrumentation overhead. Unlike the instrumentation itself, this cost doesn't depend on a logging or tracing profile: SmartCache sizes every value it stores, in every environment. Every Diginsight consumer that caches files, blobs, or images pays it, in proportion to the bytes it caches.
- **Target** — `diginsight/telemetry`: `src/Diginsight.Core/Runtime/RuntimeUtils.cs`, class `SizeCalculator`. Verified against the shipped `Diginsight.Core` 3.8.0.2 package, which declares `IHeuristicSizeProvider` and no array provider.
- **Existing landing** — none found. `diginsight/telemetry#1` and `diginsight/telemetry#2` are closed, generic performance issues that name no mechanism; no plan file in the `telemetry` or `smartcache` checkouts mentions size estimation.
- **State** — `pending`.
- **Relevance** — `medium`. It improves a library in use; consumers can work around it meanwhile by implementing `ISizeableHeuristically` on their cache envelopes.
- **Actionability** — `ready`. The file, the branch, and the fix are known, and the work list follows without judgement.
- **Actionability strategy** — lands as a small change to `SizeCalculator.Get` with unit tests over `byte[]`, `char[]`, and `int[]` asserting the expected byte count, plus a micro-benchmark asserting that sizing a 64 KB array allocates nothing per element. SmartDocs keeps `ISizeableHeuristically` on its envelopes either way, because it also avoids the reflection walk.

### `SIG-2` — SmartCache emits two nested activities and per-lookup `Debug` records on every lookup

- **Kind** — `divergent-commitment`.
- **Goal** — make a SmartCache hit cost close to nothing in instrumentation: at most one activity per lookup, and per-lookup diagnostics at `Trace` or behind an option.
- **Scope** — the public `SmartCache.GetAsync` and the private `GetAsync` it calls both start a method activity; `SetValue` and `OnEvicted` start one each; and every lookup writes `Cache entry found`, `Cache hit: valid creation date (…)`, or `Cache miss: creation date validation failed (…)` at `Debug`. Out of scope: the metric instruments, which are cheap and worth keeping, and the content of the records.
- **Why it matters** — measured in `Diginsight.SmartDocs.Web` with SmartCache 3.8.0.2, in a local debug profile: the activity emitter listening to `Diginsight.*`, Diginsight categories at `Debug`, and traces sampled at 100%. A startup warm-up of 2,983 lookups and about 1,120 file operations started 11,500–12,100 activities, of which SmartCache's were 63–64%, and wrote about 30,000 log records, of which SmartCache's were 70% (4.5 of 6.35 MB). With the emitter on, each activity pays a configuration rebind — recorded as `SIG-1` of the `20260925.02-startup-optimization` work item in `diginsight/smartdocs` — so SmartCache's granularity multiplies that cost. A consumer can only gate the whole `Diginsight.SmartCache` source off, which loses the diagnostics along with the cost. A deployed SmartDocs instance runs a different profile — logs at `Warning`, traces sampled at 10% — and whether it pays any of this is open: the analysis parks the question as `PL-1-runtime-instrumentation-cost` in `src/docs/90.00-issues/202610/20261001.01-perfanalysis/01-startup-and-navigation-optimization.analysis.md`.
- **Target** — `diginsight/smartcache`: `src/Diginsight.SmartCache/SmartCache.cs`.
- **Existing landing** — none found. `diginsight/smartcache` has no issues, and no plan file in its checkout covers instrumentation.
- **State** — `pending`.
- **Relevance** — `low`, lowered from `medium` on 2026-10-01. The measured cost comes from a debug profile that instruments every call on purpose, and the application's owner states that deployed environments don't enable that level of instrumentation, so nothing is known to degrade in a deployed instance while this waits. It returns to `medium` if the deployed baseline that resolves `PL-1` shows a real per-request cost from SmartCache's activities or records.
- **Actionability** — `bounded`. The code is known; which activities and records to keep, and at which level, needs a decision with the library's owners.
- **Actionability strategy** — lands as a change to SmartCache's instrumentation defaults, with a micro-benchmark as the acceptance measure: activities and log records per hit and per miss, before and after, with the emitter on and the category at `Debug`, and with an explicit opt-in that restores today's detail.

### `SIG-4` — SmartCache derives an entry's eviction priority from its size alone

- **Kind** — `divergent-commitment`.
- **Goal** — let a SmartCache consumer state an entry's eviction priority independently of its size, so that keeping cheap-to-refetch entries at low priority stops costing cache capacity.
- **Scope** — the priority rule in `SmartCache.cs`: before `memoryCache.Set`, the entry's priority is `Low` from `LowPrioritySizeThreshold` (20,000 by default), `Normal` from `MidPrioritySizeThreshold` (10,000), and `High` below, with no per-call or per-value override. The change is an optional priority — on `SmartCacheOperationOptions`, on the value through an interface, or both — that falls back to the size rule when absent. Out of scope: the size heuristic itself, which `SIG-1` covers, and `MemoryCache`'s compaction order.
- **Why it matters** — `Diginsight.SmartDocs.Web` wants article bodies, which a miss refetches with one read, compacted before navigation records, which a miss rebuilds from many reads. With size-only priority, wave 1 of the analysis (`C31-size-the-cache`) makes each cached body report at least 20,000 units: a 2 KB article then occupies ten times its size in the cap, and the Learning Hub's 1,147 bodies weigh 27.24 M units instead of 11.51 MB. The opposite also holds: a navigation level larger than 20,000 units — a folder with roughly 50 children or more — falls into `Low` and competes with bodies, and at an unbounded corpus size those large levels are the most expensive ones to rebuild. Every consumer that mixes large cheap entries with small costly ones meets the same trade-off.
- **Target** — `diginsight/smartcache`: `src/Diginsight.SmartCache/SmartCache.cs`, the priority computation in the store path, and `SmartCacheOperationOptions`.
- **Existing landing** — none found. `diginsight/smartcache` has no issues, and no document or plan file in its checkouts mentions eviction priority.
- **State** — `pending`.
- **Relevance** — `low`. SmartDocs works around it with the size floor and a cap configured per deployment (`Diginsight:SmartCache:SizeLimit`, 64,000,000 by default), which holds the Learning Hub's whole working set; nothing degrades while this waits except how many bodies fit.
- **Actionability** — `bounded`. The code is known; whether the priority travels with the call, the value, or both needs a decision with the library's owners.
- **Actionability strategy** — lands as an additive option with unit tests asserting that an entry's priority follows the option when it's set and the size rule otherwise. SmartDocs then drops the floor in `CachedContentSource.CachedContent` and reports bodies at their real size.

### `SIG-6` — the deploy's runtime-specific publish re-resolves every floating version

- **Kind** — `divergent-commitment`.
- **Goal** — a deployed build uses exactly the package versions the committed lock files name.
- **Scope** — the lock files of `Diginsight.SmartDocs.Web` and `Diginsight.SmartDocs.Web.Shared` record only the `net10.0` target. The deploy workflow publishes with a runtime identifier (`win-x64` or `win-x86`), which adds a target the lock files don't carry, so NuGet treats them as out of date, re-resolves the graph, and moves every floating reference — `10.0.*`, `1.*`, `12.*`, `3.*`, `0.*` — to its newest match. On an export of commit `35901a7`, that moved 19 resolved versions in the shared project and 10 in the host, among them Diginsight.Components 1.0.0.114 → 1.0.0.115, Azure.Storage.Blobs 12.29.2 → 12.30.0, log4net 3.4.0 → 3.5.0, and the ASP.NET Core packages 10.0.11 → 10.0.12. The client project builds for `browser-wasm` and keeps its lock file. Out of scope: which versions the references should float to.
- **Why it matters** — a deploy ships a package graph no developer built or ran, so a regression in any floating dependency reaches production untested, and two deploys of one commit can differ. The lock files exist to prevent exactly that: the convergence plan that introduced them, `src/docs/90.00-issues/202608/20260815.01-smartdocs-firstimpl/01-smartdocs-web-convergence.plan.md`, made them part of every restore.
- **Target** — `diginsight/smartdocs`: the host project's runtime identifiers, its lock files, and the restore performed by `.github/workflows/00.BuildSmartDocsWeb.yml`.
- **Existing landing** — none found. The convergence plan is done, and covers generating the lock files, not their runtime targets or a locked-mode restore.
- **State** — `closed: done on 2026-10-02`, at the owner's request, in this repository: the host and the shared library declare `win-x64` and `win-x86` in `RuntimeIdentifiers`, their lock files were regenerated with those targets, and the deploy's publish restores with `RestoreLockedMode=true`. Validated in `src/docs/90.00-issues/202610/20261001.01-perfanalysis/_validation/20261002.03-validation-sequence.md`.
- **Relevance** — `medium`. Nothing has failed from it yet, and every deploy carries the risk.
- **Actionability** — `ready`. The mechanism is known, and the work list follows without judgement.
- **Actionability strategy** — lands as a build change: declare on the host project the runtime identifiers the deploys use, so the lock files carry those targets; regenerate the lock files; and restore in locked mode in CI, so drift fails the build instead of shipping. The acceptance check is a CI publish that leaves every lock file byte-identical.

### `SIG-3` — the caching chapter describes a cache key the mounted namespace removed

- **Kind** — `upstream-feedback`.
- **Goal** — the architecture chapter on caching describes the cache key and the store of folder counts as the code has them since the multi-space mounting.
- **Scope** — one generated page, `src/docs/03.00-architecture/05-caching-and-invalidation.md`, which carries a `verification_stamp` and is therefore regenerated by the documentation stream, never hand-edited. Two statements to re-establish from code: *The cache key* says `ContentPathCacheKey` is a value type over the space and the path, whereas today it's a kind and a path in one mounted namespace, where a prefixed space's route segment is part of the path. *Structure* lists folder metrics as a layer without stating that they live outside SmartCache, aren't reached by its cross-instance broadcast, and are copied into cached menu levels.
- **Why it matters** — a reader who debugs cross-space collisions or counts that differ between instances starts from the wrong model.
- **Target** — `diginsight/smartdocs`, documentation stream: `@ad-documentation-manager` in revise mode.
- **Existing landing** — `SIG-2` in `src/docs/90.00-issues/202609/20260925.02-startup-optimization/01-signals.md`, `pending`, which already asks the documentation stream to regenerate the same page for count propagation after a publish.
- **State** — `routed → SIG-2 of 20260925.02-startup-optimization`.
- **Relevance** — — (routed; its ordering lives with `SIG-2`).
- **Actionability** — — (routed).
- **Actionability strategy** — carried as extra scope of the same regeneration: whoever acts on `SIG-2` re-establishes these two statements in the same run and updates both records' state.
