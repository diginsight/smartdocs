---
title: "Optimize standard navigation with SmartCache"
author: "GitHub Copilot"
date: "2026-11-26"
categories: [performance, navigation, caching]
description: "An implementation and measurement plan for scoped access checks, one article record per source path, consolidated folder descriptions, and bodyless revalidation."
status: in-progress
goal: "Minimize steady-state article and folder navigation latency using SmartCache, without changing access guarantees or transferring unchanged article bodies."
scope:
  covers: ["Interactive article visits and revisits", "Navigation into a new folder", "Policy, source, and representation freshness", "Conditional GET and path invalidation"]
  excludes: ["Application startup and warm-up tuning", "Debug-logging cost", "Introducing new authorization rules or policy syntax"]
publish: false
---

# Optimize standard navigation with SmartCache

## 🎯 Goal

Make the **server's fresh, unchanged response** independent of the number of unrelated spaces and the size of the article. Reuse a first article read for subsequent menu and page reads. Opening a folder must **not** download and render every article in it.

The [analysis](01-standard-navigation-optimization.analysis.md) records the pre-change trace and impact order. The existing frontend and server support a bodyless `304`; the implementation changes the work behind it, not the HTTP contract. Point authorization and cache consolidation are implemented. Runtime/browser validation, valid latency measurements, and tuning remain open.

New folder/file policy syntax is already tracked as `PL-17-folder-and-page-requirements` in the [authorization plan][policy-plan]. It constrains the cache design here; it isn't another feature to implement in this plan.

## 📚 Table of contents

- [🎯 Goal](#goal)
- [🕒 SmartCache settings](#smartcache-settings)
- [🛠️ Implementation order](#implementation-order-todo)
- [🧪 Acceptance and release](#acceptance-and-release-todo)
- [🔍 Discovery](#discovery-todo)
- [❓ Open decisions](#open-decisions)
- [🅿️ Park lot](#park-lot)
- [💡 Conclusion](#conclusion)
- [📚 References](#references)

## 🕒 SmartCache settings

This is the **recommended starting profile and the current base configuration** for [SmartCache 3.8.0.2][version]. No runtime setting has been changed. The host binds [retention and capacity][binding] separately from [SmartDocs' per-read freshness][fresh-binding]. Check the effective deployment overlay against these [base settings][settings]; don't copy the fragment into an overlay that already inherits them.

```json
{
  "Diginsight": {
    "SmartCache": {
      "MaxAge": "7.00:00:00",
      "SlidingExpiration": "7.00:00:00",
      "AbsoluteExpiration": "31.00:00:00",
      "SizeLimit": 64000000,
      "SmartDocs": {
        "Content": "00:05:00",
        "Missing": "00:00:30",
        "Structure": "00:05:00",
        "NavLevel": "00:05:00",
        "Access": "00:00:30"
      }
    }
  }
}
```

`MaxAge` decides whether a value is fresh **when read**. Sliding expiration retires an idle entry; absolute expiration limits retention even if the entry stays hot. Neither eviction timer grants freshness. The size limit is in *estimated size units*, not a 64 MB process-memory guarantee. Use the same policy for memory and optional distributed reads; never renew the source's observed age merely by copying, promoting, or deserializing a value.

The change is **which result each setting governs**, not a longer lifetime for everything:

| Read | Current implementation | Effective `MaxAge` |
|---|---|---|
| Article found | One `article-v1` record per mounted Markdown path holds head, source, rendered page, response bytes, and ETag. Route aliases hold only a winning-path reference. | `Content`, 5 min from the last successful source validation. Head and render share a revision. A fresh hit needs no origin call. |
| File or route genuinely missing | Article and route-reference misses use `Missing` after the initial found-content tolerance check. A fallback route also depends on the absence of its higher-priority candidates. | `Missing`, 30 s for **both**. Invalidate on publication. A denial or read failure isn't a shared negative. |
| Folder, listing, and level | `metadata-v1` snapshots, `children` listings, and `nav-level-v2` descriptions remain distinct source/membership/menu inputs. Browser `FolderRecord` is projected from the cached description and live metrics, not stored as a separate SmartCache payload. | `Structure` and `NavLevel`, both 5 min. A count change doesn't refresh YAML; policy freshness is independent. |
| Policy, including confirmed absence | All space-root snapshots use `Access` (30 s). | Keep `Access` at **30 s maximum**, only for applicable policy scopes. Invalidate on notification. On expiry, wait for a valid read; never serve a stale grant. This isn't the lifetime of the sign-in session. |
| Whole-tree index | `NavIndex` unset; inherits global `MaxAge` of 7 days. | Unchanged. Ordinary article navigation doesn't require a whole-tree rebuild. |

**Fresh hit:** return the SmartCache value. **Miss or hard expiry:** concurrent readers wait for one refresh of the needed key (`CoalesceRacingCacheMisses = true`). **Invalidation:** discard the affected generation; a late refresh can't republish it. This replaces unbounded stale-while-revalidate. If waiting at expiry proves costly, refresh hot keys *before* the deadline, not after it. In-flight coordination may hold keys and short-lived tasks, not another resident dictionary of Markdown, HTML, or folder values.

## 🛠️ Implementation order (🟡 todo)

1. **Measure a settled baseline.** Use the same Release host, authorized reader, corpus, storage, compression, and browser cache settings before and after each stage; disable verbose diagnostics. Time a first `200`, A → B → A with an HTTP copy, an unseen branch, and the first read past `Access` and `Content`. Run three batches of at least 100 warm revisits; report p50/p95 and sample counts separately for each scenario. Count scopes, origin reads/listings, renders, serializations, hashes, bytes, and evictions; separate demand from background work. Reuse the [measurement script][measure] for unconditional timings; its one conditional probe isn't a conditional p95. (🟡 todo)
2. **Remove all-space fan-out from point reads.** Change [request initialization][pipeline] to establish the principal, not materialize the global visibility table. In [ReaderAccess][reader] and [the requirement resolver][requirements], resolve only scopes applicable to the selected file or folder and reuse the result **within that request**. Use the same policy revision for requirements and grants; discard an invalidated in-flight result before use. Global menus, totals, and hub filtering must still ask for all relevant scopes. Preserve site/space floors, optional versus required policy, current `403`/`404`/`204` behavior, and fail-closed handling. Carry the exact mounted resource and applicable scope versions; “sees every space” must not grant access to a future restricted file. Don't cache a principal's verdict in a shared article or folder. The request middleware now captures the principal only; a point read evaluates and memoizes its owning space from one policy snapshot. Global shell, totals, and hub paths retain all-scope evaluation. The provider regression suite confirms point evaluation reads only the selected space's metadata. (✅ done)
3. **Serve an authorized repeat without rebuilding its wire representation.** In [the page provider][page-cache], retain the serialized JSON bytes and the ETag of those exact bytes **with** the rendered result, once per representation version. In [the endpoint][endpoint], authorize before comparing tags, return a bodyless `304` on a match, or write the retained bytes on a miss. Preserve `private, no-cache`. Skip [HTML bootstrap buffering][bootstrap] for known page/nav JSON endpoints only; leave personalized HTML and raw-content cache rules alone. Keep native browser revalidation; no timestamp URL, JavaScript article store, or manual JSON parsing of a `304`. The cached bytes, post-authorization validator check, and endpoint bypass are implemented; provider checks pass. Visible-browser/wire validation remains in the release gate. (✅ done)
4. **Give each article one SmartCache lifecycle.** Replace independent `article-head`, Markdown `content`, and route `page` payloads with a record keyed by mounted source path. Fill **head only** for a menu; promote to **full** for a page. Parse the head from the same bounded prefix in both paths. On a full read, distinguish the **source validator** from the **rendered JSON ETag**. On expiry use a conditional Blob download if an origin tag is available; on filesystem use the content digest, not publication date. If source, base directory, and renderer/serializer version are unchanged, reuse the render and its tag. Coalesce promotions; a late head can't replace a full or newer record. Charge retained source, HTML, and response bytes to the size budget. Leave binary assets alone. The `article-v1` record, head-only promotion, conditional source validation, and response reuse are implemented and covered by provider regression checks. Distributed-only validator costs remain in Discovery. (✅ done)
5. **Make route resolution and invalidation agree.** Preserve [candidate precedence][candidates], explicit filenames, relative links, root handling, and mount collisions. Store a reference to the winning file, not its rendered payload, for each alias. Track membership and earlier absent candidates: `guide` and `guide.md` [aren't the same branch key][branches]. Add, delete, rename, or higher-priority publication must refresh the mapping. Apply [local and companion invalidation][coordination] to the canonical record and its dependent routes/levels; leave unrelated branches hot. Version changed cache schemas and release the article owner with its invalidation rules, never one without the other. `page-route-v1` stores the winning content-key reference; candidate-aware invalidation and fallback precedence pass regression checks. (✅ done)
6. **Consolidate folder descriptions into cached navigation levels.** Keep authored navigation fields and the computed folder description in the cached server-side child node rather than a second independently expiring browser DTO. Feed membership and classification from the same unfiltered child listing. Don't rebuild a parent level to get a [folder record][folder] that the level itself needs. Apply metrics, coverage, and reader-filtered totals at projection time. The parsed metadata snapshot remains separate because authorization has a tighter freshness requirement. Preserve site-root branding, mount overrides, hidden/asset folders, collapsed single-page folders, and the unknown-folder response. The separate `folder` SmartCache payload is removed; cached navigation children retain folder metadata and the provider projects live metrics. Section and collapsed-folder projections are covered by regression checks. (✅ done)
7. **Tune against the baseline, one variable at a time.** If hot policy reads wait at 30 s, try refreshing *recently requested* scopes at 20–25 s with staggered scheduling; keep the 30 s deadline. If idle entries displace active ones, first try `SlidingExpiration = 1.00:00:00` with the absolute setting unchanged, then test `AbsoluteExpiration = 7.00:00:00` separately. If active entries still churn, test a larger `SizeLimit` only within the deployment's measured memory budget. Compare median batch p95s; use the spread of baseline batch p95s as a noise band. Keep a variant only if the target p95 improves beyond that band without a repeat-path regression, excessive storage work, or a failed correctness gate. Otherwise restore the previous setting. (🟡 todo)

## 🧪 Acceptance and release (🟡 todo)

Compare successful, equivalent requests. A denied request, bootstrap reuse without HTTP, and a wire-level `304` are three different outcomes. The full solution rebuild and 204 provider/authorization assertions pass. No visible-browser run, wire capture, valid latency baseline, or p50/p95 comparison has been recorded; provider checks are not an end-to-end pass.

| Scenario | Required result before rollout |
|---|---|
| Fresh visible A → B → A; cached HTTP copy of A | The wire shows `If-None-Match` and `304`, **0 response-body bytes**; no origin I/O, Markdown render, JSON serialization, or body hash on the warm A request. A `200` exposed by browser Fetch from its cache isn't a failed wire check. |
| First view, eviction, and head-first vs. page-first | Known source, page first: one full origin read, no separate head read for that revision. Menu first: at most one bounded head read then one full read on promotion; opening the folder alone renders **zero siblings**. Concurrent requests on one host don't duplicate the same fill. |
| Unchanged source at expiry | One coalesced source validation, zero Markdown renders or response serializations/hashes, matching `304`. On a storage error don't mark the version fresh or confirm an expired tag. |
| Changed, moved, deleted, newly published, or re-rendered article | Old tag and different visible bytes: `200` with a new ETag. Deleted: fall back to the next eligible candidate or the normal missing response, not a stale `304`. Cover same-second/same-length edits and renderer changes. A missing route becomes visible on processed invalidation, or on the first read after 30 s without notification. |
| Point access and revocation | With one space and with seven, a page request performs **zero unrelated scope reads**. After policy invalidation, and on the first read beyond the hard age if notification is lost, an old ETag never yields `304` for a now-denied reader. Exercise two principals, a required-but-unreadable policy, and the mounted-folder exception. An adapter test can supply nested scopes without enabling new policy syntax. |
| Folder navigation, edits, and counts | Existing wire contracts and filtered totals remain unchanged. One read of a present YAML per fill/generation; updating only a count rereads no YAML. A folder label edit updates both its record and parent level. No private metadata is serialized. |
| Multi-node invalidation, cache pressure, and release | Repeat edit/revoke tests across two cooperating hosts and with a delayed in-flight fill. Old fills can't resurrect invalidated values. Measure article selection to visible content, HTTP p50/p95, origin calls, allocation, and eviction under the same load. Release only with lower expiry-case cost, no repeat-path latency regression beyond measured variance, and no unbounded payload store outside SmartCache. |

Record the runtime changes in a visible browser against a rebuilt host, with normal browser caching enabled. Save the network observations, screenshots, and exact outcomes in this work item's `_validation/` folder. Compare the actual rendered HTML after edits, not just raw Markdown. Roll back the changed stage and repeat the baseline if a correctness or performance gate fails. No foreground console/browser run or screenshot artifact has been recorded yet; do not mark this gate complete based on builds or provider checks alone. (🟡 todo)

## 🔍 Discovery (🟡 todo)

- **Distributed payload cost.** Compare an in-memory hit with a local eviction followed by a distributed hit. Measure bytes from the cache store as well as HTTP response bytes: a wire `304` can still pull a whole article from Redis. If menu or validator-only reads transfer the full payload, keep a compact head and tag with a reference to immutable response bytes for that **same revision**; load those bytes only for a `200` or prerender. Coalesce fills and test recovery if the payload was evicted. Keep one freshness decision and invalidation path, not independent timers. (🟡 todo)
- **Legacy carrier use.** Check all mounted roots for legacy-only, conflicting, or misnamed policy. Only after migration and parity checks pass, set `Site:LegacyMetadataCompatibility` to `false` in that deployment; otherwise keep compatibility and don't count those reads as savings. (🟡 todo)
- **Measurement access.** If an authorized Blob-backed fixture or a second cooperating host isn't available, complete the filesystem checks, mark the missing results unmeasured, and block that rollout gate rather than reporting synthetic timings as production performance. (🟡 todo)

## ❓ Open decisions

None block implementation. The plan preserves the present freshness and authorization contract. “Fastest” is determined by the measured workload and the release gates, not an unverified increase in cache lifetime.

## 🅿️ Park lot

- Adding `Last-Modified` for clients that can't use ETags → defer until such a client and a reliable representation timestamp exist.
- Eliminating a second resolution on a denied, existing route → defer until a denial-path baseline warrants it. Don't mix its timings with successful navigation.

## 💡 Conclusion

The code changes remove unrelated point-read policy work, repeated article rendering/serialization, duplicate route payloads, and the separate folder DTO cache without widening freshness windows. The full solution builds and provider checks pass. Keep the plan in progress until the visible-browser release gate and comparable performance measurements pass; tune retention only when the measured workload justifies a change.

## 📚 References

- **[Standard navigation analysis](01-standard-navigation-optimization.analysis.md)** 📘 [Repository]. Documents what is already cached and the measured `200`/`304` behavior.
- **[SmartCache configuration reference](../../../06.00-reference/01-configuration-settings.md)** 📘 [Repository]. Describes retention, capacity, and the optional distributed components.
- **[Existing provider checks](../../../../../eng/metadata-checks/ProviderChecks.cs)** 📘 [Repository]. Reuse their concurrency, scope-isolation, compatibility, and local/remote invalidation cases.

[settings]: ../../../../Diginsight.SmartDocs.Web/appsettings.json#L127
[version]: ../../../../Directory.Build.props#L24
[binding]: ../../../../Diginsight.SmartDocs.Web/Program.cs#L165
[fresh-binding]: ../../../../Diginsight.SmartDocs.Web/Program.cs#L224
[measure]: ../../../../../scripts/Smartdocs.Deployed.Measure.ps1#L52
[pipeline]: ../../../../Diginsight.SmartDocs.Web/Program.cs#L369
[reader]: ../../../../Diginsight.SmartDocs.Web/Authorization/ReaderAccess.cs
[requirements]: ../../../../Diginsight.SmartDocs.Web/Authorization/AccessRequirementResolver.cs
[page-cache]: ../../../../Diginsight.SmartDocs.Web/Rendering/RenderedPageProvider.cs
[endpoint]: ../../../../Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs#L91
[bootstrap]: ../../../../Diginsight.SmartDocs.Web/Navigation/NavigationBootstrapMiddleware.cs
[candidates]: ../../../../Diginsight.SmartDocs.Web.Shared/Services/PageLoader.cs#L61
[branches]: ../../../../Diginsight.SmartDocs.Web/Caching/ContentPathCacheKey.cs#L62
[coordination]: ../../../../Diginsight.SmartDocs.Web/Caching/CoordinatedSmartCache.cs
[folder]: ../../../../Diginsight.SmartDocs.Web/Navigation/FolderRecordProvider.cs#L70
[metadata]: ../../../../Diginsight.SmartDocs.Web/Metadata/ContentMetadataProvider.cs
[policy-plan]: ../../202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md

<!--
article_metadata:
  filename: "01-standard-navigation-optimization.plan.md"
  type: "implementation-plan"
  created: "2026-11-26"
  last_updated: "2026-11-26"
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
-->
