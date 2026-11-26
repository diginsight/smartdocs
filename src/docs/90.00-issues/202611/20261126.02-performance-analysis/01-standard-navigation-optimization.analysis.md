---
title: "Standard navigation: what an article revisit actually costs"
author: "GitHub Copilot"
date: "2026-11-26"
categories: [analysis, performance, navigation, caching]
description: "Traces a warm SmartDocs article visit from the browser to storage, tests conditional responses, and ranks cache changes by the work they remove."
publish: false
---

# Standard navigation: what an article revisit actually costs

**The implementation now keeps one article representation per mounted Markdown path and can reuse its serialized response for conditional requests.** The November 26 trace is the pre-change baseline: each article request checked seven spaces, and two requests with expired policy snapshots made 21 storage-provider calls each. Provider tests and a full build pass, but no valid p50/p95 comparison or visible-browser run has established the latency improvement yet.

This analysis covers navigation in an already running, hydrated app: opening an article, revisiting it, moving into a new folder, and encountering expired or invalidated entries. It excludes startup, initial warm-up, search, and debug-logging cost. The [implementation plan](01-standard-navigation-optimization.plan.md) gives the SmartCache settings and acceptance tests.

## 📚 Table of contents

- [🧭 How an article opens](#how-an-article-opens)
- [📦 One article record and a route reference](#one-article-record-and-a-route-reference)
- [🔁 What a conditional GET saves](#what-a-conditional-get-saves)
- [🗂️ Folder data and access policy](#folder-data-and-access-policy)
- [🔬 What the trace proves](#what-the-trace-proves)
- [📈 Changes ranked by navigation impact](#changes-ranked-by-navigation-impact)
- [💡 Conclusion](#conclusion)
- [📚 References](#references)

## 🧭 How an article opens

1. [ContentView][view] reuses the current page if the route matches its persisted or bootstrap state. This costs **zero page requests**. That state holds the current route, not a history of all articles. Showing A from prerender doesn't, by itself, put `/_page/A` in the browser's HTTP cache.
2. For a different article, [the WebAssembly client][client] makes one ordinary GET to `/_page/{route}`. It doesn't fetch Markdown, run Markdig, append a timestamp, or maintain its own dictionary of article validators. On a first visit it consumes JSON containing HTML, title, table of contents, and word count.
3. [The host][pipeline] captures the request's principal. A point read resolves only the policy for the selected file's owning space; global shell, totals, and hub paths still resolve every relevant space. The [authorizing resolver][authorize] checks the **file actually selected**, not merely the requested URL, before the endpoint compares an ETag.
4. Breadcrumbs use ancestor levels; previous/next loads in the background, not from the whole search index. [The client nav provider][client-nav] reuses loaded levels and shares an in-flight request for the same prefix. Entering an uncached branch can cost a separate `/_nav/children` GET. That response already includes its visible section records; it isn't one browser request per folder.

The cached HTML is the **article fragment**, not the personalized document shell, images, or client-rendered diagrams. A full document reload is a different operation: the injected shell is `private, no-store`.

## 📦 One article record and a route reference

The page and its aliases now share one article record. The route cache stores only a reference to the winning content path, not a second rendered-page payload. These are the current SmartCache entries relevant to an article visit:

| Server key | Contents and purpose | When it costs work |
|---|---|---|
| `article-v1` + mounted file path | A head-only or full record. The full record contains source bytes and validator, parsed head, rendered page, serialized JSON bytes, and the ETag for those bytes. Binary assets bypass this cache. | A menu can fill only the bounded head. A page promotes that same entry to a full representation. A fresh full-record hit does no origin read, front-matter parse, Markdown render, response serialization, or body hash. |
| `page-route-v1` + normalized route | The winning mounted content path, or a confirmed miss, plus invalidation generation. It does not contain rendered HTML. | A miss [tries route candidates][candidates]. File, folder-index, overview, and readme fallbacks retain their precedence; candidate-aware invalidation refreshes aliases when a higher-priority file changes. |

`Content`, `Structure`, and `NavLevel` still default to five minutes; `Missing` is 30 seconds. The route cache and article record use the shorter missing tolerance for a miss. These are read-time freshness tolerances, not the physical eviction lifetime.

Reads now use SmartCache directly and coalesce concurrent misses. The process-local <mark>stale-while-revalidate (SWR)</mark> dictionaries are no longer on the runtime path; the old `BackgroundRevalidationCache` type remains in the source tree but has no runtime registration. Expired reads wait for a refresh instead of serving a stale local copy. This makes freshness and size eviction belong to SmartCache, though a refresh can still add latency at expiry.

When the previous full record is in memory, expiry can conditionally validate a Blob using its origin ETag or check the filesystem content digest. An unchanged source reuses the rendered response and its ETag. A remote-only SmartCache hit may not provide the previous validator to the source adapter; distributed payload and conditional-validation costs remain a discovery item.

**Keep head-only navigation.** A page-first read also fills the parsed head, so a later menu read needs no separate header fetch. A menu-first read can still cost one bounded header read and one full read on promotion: the body can't be reconstructed from a header. Downloading every sibling to avoid that possible second read would penalize readers who only open the folder.

## 🔁 What a conditional GET saves

`/_page` already uses a strong <mark>entity tag (ETag)</mark>: a fingerprint of the serialized page representation. With `Cache-Control: private, no-cache`, a browser **may store** the response but must validate it before reuse. With a stored copy and normal Fetch caching, it can send `If-None-Match: "<previous tag>"` to the same URL. A matching authorized GET returns `304`: headers cross the network, **zero article-body bytes** do. Fetch can hand .NET the stored body as a successful response; the component needn't parse a bodyless `304`. No stored copy means a normal `200` is correct.

An in-process check on November 26 exercised the actual [page handler][endpoint] and [validator][validator] with a synthetic, visible page. It measured output in the response stream, not latency, authentication, compression, or browser behavior:

| Request | Status | Response-body bytes |
|---|---:|---:|
| First GET, no validator | 200 | 82 |
| Same representation, previous ETag | 304 | 0 |
| Same representation, weak form of the same tag | 304 | 0 |
| Same representation, only `If-Modified-Since` | 200 | 82 |
| Changed HTML of the same length, previous ETag | 200, new ETag | 82 |

All five responses carried `private, no-cache`; none carried `Last-Modified`. An [October visible-browser validation][browser-304] independently recorded the rendered JSON `200`, then a `304` when the test supplied its tag. This isn't a measurement of automatic browser revalidation or today's signed-in, end-to-end 95th percentile (p95).

**Keep ETags rather than sending a browser timestamp.** A changing query parameter changes the cache key. The article's publication date isn't a modification time. HTTP dates have one-second resolution, and a source timestamp doesn't identify the output after a route or renderer change. If date-only clients need support, use a server-issued `Last-Modified` for the representation as a fallback; `If-None-Match` takes precedence.

The implementation now retains the serialized bytes and ETag with the article record, then compares the tag only after authorization. A matching request returns a bodyless `304`; otherwise the endpoint writes the retained bytes. The bootstrap middleware skips buffering the known `/_page` and `/_nav` GET endpoints. The response remains `private, no-cache`, and the client still uses an ordinary GET—no timestamp URL or manual article store. The browser's automatic wire-level revalidation still needs the required visible-browser check; compilation and provider tests don't prove it.

## 🗂️ Folder data and access policy

Folder navigation still has separate source, membership, and display concerns:

| SmartCache key | Question answered | Checked-in freshness on the server |
|---|---|---|
| [`metadata-v1` or `metadata-v1-legacy`][metadata] | What does this directory's `metadata.yml` declare, or is it absent or invalid? The shared snapshot feeds navigation and authorization. | SmartCache directly; on expiry the caller waits. Space root: `Access` (30 s). Non-root: `Structure` (5 min). |
| `children` | Which files and directories actually exist here? | `Structure` (5 min), SmartCache directly. |
| `nav-level-v2` | Which entries belong in this menu, in what order? The cached server-side child description also carries folder metadata. | `NavLevel` (5 min), SmartCache directly. The endpoint filters it for the reader and includes records for visible sections. |
| Browser-facing `FolderRecord` | What should the browser show for this prefix? | Not a separate SmartCache payload. The provider projects the cached parent-level description and current metrics; reader-filtered counts are composed per request. |

**`metadata-v1-legacy` is a cache namespace, not another file.** With [compatibility enabled][compat], a space root checks `access.metadata.yml` as well as `metadata.yml`. A missing carrier can trigger a listing to distinguish absence from noncanonical naming. The namespace keeps that result distinct from one loaded under canonical-only rules. Nested folders use the same namespace but **don't probe the legacy file**. The replacement is the root `metadata.yml`'s `access:` section. Retire compatibility only after every affected root has been checked; deleting a cache key isn't a migration.

The browser-facing `folder` SmartCache payload and local SWR wrapper are gone. A section or collapsed folder retains its `FolderMeta` and source folder on the cached server-side navigation child; the folder provider projects that description instead of caching a second browser DTO. A small follow-up fix also resolves collapsed one-article folders from that cached description. Current metrics and reader-filtered totals remain projections because they change independently of YAML.

The parsed metadata snapshot remains separate from the navigation description: it supports authorization and keeps the root policy freshness at 30 seconds, without exposing access fields in a browser DTO. This is not literally one SmartCache item per physical folder; the consolidation removes the independently cached `folder` DTO and avoids repeat source reads where a cached menu description is available. [Today's schema][metadata-contract] accepts `access` only at a space's content root; the cache design still allows finer-grained policy scopes later.

## 🔬 What the trace proves

The November 26 application log predates these changes. It contains **four article requests and one level request** after warm-up, lines 63,068–63,944. The counts below join work by request trace and include only operations inside reader initialization. Source references describe the working tree used for this investigation. Debug elapsed times aren't production latency measurements.

| Initialization outcome | Requests | Distinct root snapshots consulted per request | Origin work per request during initialization |
|---|---:|---:|---|
| Fresh | 3 | 7 | No origin reads or listings |
| Expired | 2 | 7 | 12 filesystem gets + 6 listings; 2 Blob gets + 1 listing |

The two expiry cases start at lines 63,347 and 63,714. That's **21 storage-provider operations per expiry request**, including three at the remote provider, before resolving a single article. These are method calls, not a count of network packets or an assertion that all calls ran serially. The four different page routes each built a page; **none** of the five traces built a folder record. The one level build requested a parsed head but didn't read a head from origin. A separate repeat probe returned six `403`s; it is not a successful article-revisit benchmark.

One distinction for the next measurement: [a level request][nav-endpoint] also schedules a depth-three prefetch, even after startup. It doesn't await that traversal. Count its origin calls separately: it may help the *next* visit or compete with it for storage and CPU. The sample doesn't establish which effect dominates.

## 📈 Changes ranked by navigation impact

The order reflects *work avoided and how often the path runs*. It isn't a prediction in milliseconds.

| Priority | Change | Implementation status and expected benefit |
|---:|---|---|
| 1 | Evaluate only applicable policy scopes on a point read; share their state within that request. | Implemented and covered by provider checks. Removes the measured unrelated-scope fan-out without extending the revocation window. Global navigation and totals still need all relevant scopes. |
| 2 | Cache the rendered **response** and its ETag under one article revision; bypass HTML buffering on known JSON endpoints. | Implemented. Removes repeat serialization and body hashing on a fresh page record; the browser/network benefit still needs a visible run. |
| 3 | Merge article head, source, and render lifecycles; resolve aliases to the same file. | Implemented and covered by regression checks. Keeps head-only navigation cheap and aliases point to the same article record. |
| 4 | Unify folder projections; retain membership and live metrics as distinct inputs. | Implemented and covered by regression checks. No folder build occurred on the five sampled pre-change requests, so this isn't ranked first for revisits. |
| 5 | Retire legacy probing where migration is verified; reuse route resolution on error paths. | Deferred. It can remove work on policy refresh or denial paths, but doesn't speed a fresh successful article read and requires migration evidence. |

## 💡 Conclusion

The target revisit is **one conditional GET, current authorization for the selected file, one cached representation lookup, and zero body bytes**. The code path and provider checks now support that target, but they don't establish end-to-end browser behavior or a latency improvement. Keep the working browser protocol and current freshness bounds until a comparable baseline and visible-browser validation pass.

## 📚 References

- **[Current HTTP endpoint contract](../../../06.00-reference/02-http-endpoints.md)** 📘 [Repository]. Distinguishes rendered page JSON, raw assets, and reader-dependent navigation.
- **[Rendered-page browser validation][browser-304]** and **[hydration and navigation validation][browser-hydration]** 📘 [Repository]. The first records a conditional `304`; the second distinguishes a bootstrap hit from a subsequent article request.
- **[Unified metadata contract][metadata-contract]** and **[reader authorization plan][policy-plan]** 📘 [Repository]. The former is today's schema; the latter tracks folder and page requirements separately.
- **[HTTP conditional requests](https://www.rfc-editor.org/rfc/rfc9110.html#section-13.1.2)** 📘 [Standard]. Defines ETag comparison and precedence over date validators.

[view]: ../../../../Diginsight.SmartDocs.Web.Shared/Components/ContentView.razor.cs#L42
[client]: ../../../../Diginsight.SmartDocs.Web.Client/HttpContentSource.cs#L21
[client-nav]: ../../../../Diginsight.SmartDocs.Web.Client/HttpNavProvider.cs#L69
[pipeline]: ../../../../Diginsight.SmartDocs.Web/Program.cs#L369
[visibility]: ../../../../Diginsight.SmartDocs.Web/Authorization/SpaceVisibilityEvaluator.cs#L45
[authorize]: ../../../../Diginsight.SmartDocs.Web/Authorization/AuthorizingRenderedPageResolver.cs#L23
[candidates]: ../../../../Diginsight.SmartDocs.Web.Shared/Services/PageLoader.cs#L61
[page-cache]: ../../../../Diginsight.SmartDocs.Web/Rendering/RenderedPageProvider.cs#L27
[swr]: ../../../../Diginsight.SmartDocs.Web/Caching/BackgroundRevalidationCache.cs#L10
[endpoint]: ../../../../Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs#L91
[validator]: ../../../../Diginsight.SmartDocs.Web/Endpoints/HttpValidators.cs#L29
[bootstrap]: ../../../../Diginsight.SmartDocs.Web/Navigation/NavigationBootstrapMiddleware.cs#L26
[metadata]: ../../../../Diginsight.SmartDocs.Web/Metadata/ContentMetadataProvider.cs#L58
[compat]: ../../../../Diginsight.SmartDocs.Web/appsettings.json#L49
[folder]: ../../../../Diginsight.SmartDocs.Web/Navigation/FolderRecordProvider.cs#L70
[nav-endpoint]: ../../../../Diginsight.SmartDocs.Web/Endpoints/NavEndpoints.cs#L72
[metadata-contract]: ../../../06.00-reference/08-unified-metadata-contract.md
[policy-plan]: ../../202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md
[browser-304]: ../../202610/20261001.01-perfanalysis/_validation/20261002.10-validation-sequence.md
[browser-hydration]: ../../202610/20261001.01-perfanalysis/_validation/20261002.11-validation-sequence.md

<!--
article_metadata:
  filename: "01-standard-navigation-optimization.analysis.md"
  type: "explanation"
  created: "2026-11-26"
  last_updated: "2026-11-26"
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
-->
