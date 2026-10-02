---
title: "Signals — startup optimization"
author: "Dario Airoldi"
date: "2026-09-29"
categories: [signals, performance, observability]
description: "Activities surfaced by the startup-optimization analysis that were never in scope for its goal of a smooth startup and navigation sequence."
publish: false
---

# Signals — startup optimization

Activities this work item's conversation surfaced that were **never in scope** for its goal — explaining the SmartDocs startup flow and proposing the sequence that makes startup and navigation smooth. Each record is self-contained: delivery is manual, so a person reading it in another repository — without this conversation and without this work item — must be able to act on it as written.

**Identifiers are identity; the listing order is priority** — derived from relevance and actionability, never assigned by impression.

📖 Record shape, kinds, sweep, and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

The table lists both records in priority order. Both earn this page: one on relevance, one on actionability.

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-1` | `divergent-commitment` | high | bounded | `diginsight/telemetry`, `diginsight/components` | none found | `pending` |
| 2 | `SIG-2` | `upstream-feedback` | medium | ready | `diginsight/smartdocs` | none found | `pending` |

No record is `low` and `open`, so there is no `other-signals` page.

### `SIG-1` — Diginsight re-binds activity logging options from configuration on every activity start and stop

- **Kind** — `divergent-commitment`.
- **Goal** — make an instrumented method call cost what its log level says it should — close to nothing when its records are filtered out — by resolving `DiginsightActivitiesOptions` from a cache instead of binding configuration again at every activity start and stop.
- **Scope** — three places and one mechanism. `ActivityLifecycleLogEmitter.ActivityStarted` and `ActivityStopped` resolve options through `IOptionsMonitor<DiginsightActivitiesOptions>.CurrentValue` (and a class-aware lookup) **before** they check `LogBehavior` or the log level. `ClassAwareOptionsCache<TOptions>.GetOrAdd` bypasses its cache whenever the options type is flagged dynamic, and calls the factory, which runs `ConfigureClassAwareOptionsFromConfiguration` → `FilteredConfiguration.GetChildren` → `ConfigurationBinder.Bind` over the whole `Diginsight:Activities` section. `ObservabilityExtensions` in `Diginsight.Components.Configuration` registers the options with `VolatilelyConfigureClassAware<DiginsightActivitiesOptions>()` and `DynamicallyConfigureClassAware<DiginsightActivitiesOptions>()`; the latter calls `FlagAsDynamic`, which is what disables the cache. Out of scope: what gets logged, and the dynamic-log-level feature itself, which must keep working.
- **Why it matters** — measured in `Diginsight.SmartDocs.Web` (Diginsight.Core 3.8.0.2, Diginsight.Components 1.x, .NET 10, Release build) on a startup crawl of about 1,700 file reads: with the emitter listening to the `Diginsight.*` sources, the crawl didn't finish in five minutes and held about one core the whole time; with the emitter gated off for those sources it finished in 20 seconds using about 20 CPU-seconds, and the first page response dropped from 56 s to 3 s. A 25-second sampled thread-time trace attributes 26% of all thread time — about 93% of the process's non-idle time, garbage-collection pauses from the binder's allocations included — to `ClassAwareOptionsMonitor.Get` → `ClassAwareOptionsFactory.Create` → `ConfigurationBinder.Bind`, most of it enumerating and sorting configuration keys. Because the level check comes after the options are resolved, an application that logs at `Warning` pays the same cost for records it never writes. Every Diginsight-instrumented application pays it on every instrumented call, in proportion to how fine-grained its activities are.
- **Target** — `diginsight/telemetry`: `src/Diginsight.Core/Options/ClassAwareOptionsCache.cs` and `src/Diginsight.Diagnostics/ActivityLifecycleLogEmitter.cs`. `diginsight/components`: `src/Diginsight.Components.Configuration/Hosting/Extensions/ObservabilityExtensions.cs`, where the registration flags the type as dynamic.
- **Existing landing** — none found. `diginsight/telemetry#1` and `diginsight/telemetry#2` (generalized performance degradation, both closed on 2026-04-03) are generic and name no mechanism; `diginsight/components` has no issues. No plan file in either checkout covers options caching.
- **State** — `pending`. Confirmed on a deployed instance on 2026-10-02: a deployed `Diginsight.SmartDocs.Web` — logging at `Warning`, sampling 10% of traces, on App Service B1 — was measured under four minutes of steady load with the `Diginsight.*` activity sources listened to and gated off, and spent 0.248 s of CPU per request against 0.019–0.025 s. Until then this record's production claim rested on local Release runs and on the mechanism; it now rests on a measurement taken where the application runs. The figures and method are in `src/docs/90.00-issues/202610/20261001.01-perfanalysis/01-startup-and-navigation-optimization.analysis.md`, section "What instrumentation costs a deployed instance".
- **Relevance** — `high`. It's the dominant CPU cost measured in the consuming application, it's paid in production regardless of log level — now measured on a deployed instance, not inferred — and until it's fixed the consumer can only work around it by switching instrumentation off.
- **Actionability** — `bounded`. The landing and the code are known; the design of the cache key — how a volatile or dynamic override, for example one carried by a request's headers, invalidates or bypasses a cached value — still has to be shaped.
- **Actionability strategy** — lands in `diginsight/telemetry` as a change to the options cache or the emitter, with a micro-benchmark as its acceptance measure: activities per second with the emitter listening and the category level at `Warning`, before and after, with and without an active dynamic override. `diginsight/components` follows if the registration needs to change. The consuming application keeps its own mitigation — gating the emitter off for hot sources and trimming per-item activities — until a fixed package ships, then re-enables the sources it wants.

### `SIG-2` — the caching-and-invalidation chapter describes count propagation that a publish doesn't trigger

- **Kind** — `upstream-feedback`.
- **Goal** — the architecture chapter states what happens to folder counts after the publish pipeline's invalidation call, so a reader who sees stale totals after a publish finds the explanation instead of a description that implies live updates.
- **Scope** — one generated page, `src/docs/03.00-architecture/05-caching-and-invalidation.md`, which carries a `verification_stamp` and is therefore regenerated by the documentation stream, never hand-edited. Two sections: *Propagating counts*, which describes propagation for a single path-scoped change only, and *Learning that content changed*, which describes the pipeline's `POST /_nav/invalidate` without noting that it carries no path. Behavior to re-establish from code: an empty-path invalidation drops every cached entry but refolds only the site-root metrics cell, so section counts keep their pre-publish values while still marked exact; a new section folder gets no metrics cell; and a later refold of its parent produces a lower bound that can't replace the stored exact value, never settles, and re-arms the 400 ms drain indefinitely.
- **Why it matters** — reproduced on 2026-09-29 against a copy of this repository's documentation content: after adding one article to a section and a new two-article section to a month folder, a whole-site invalidation left the root total at 47 instead of 50, the section at 5 instead of 6 and the month folder at 7 instead of 9, all still reported as `Complete`. The page reads as if counts follow every publish, which sends anyone debugging stale totals in the wrong direction.
- **Target** — `diginsight/smartdocs`, documentation stream: `@ad-documentation-manager` in revise mode, or `/01.04-ad-docs-update-from-changes` once the count-refresh fix lands.
- **Existing landing** — none found. Searched `src/docs/90.00-issues` for references to the page: the `20260925.01-perfanalysis` work item adopted it on 2026-09-25 for the `ContentFreshness` defaults and doesn't touch count propagation.
- **State** — `pending`.
- **Relevance** — `medium`. The page is accurate about the code it cites; it omits the whole-site path, which is the path the pipeline actually takes.
- **Actionability** — `ready`. The page, both sections, and the behavior to re-establish are named above.
- **Actionability strategy** — run the change-driven documentation update against the commit that makes an empty-path invalidation refold every cell (change `C6-refold-on-publish` in the startup-optimization analysis). If the documentation should describe the defect before the fix lands, run the revise mode now and record it as a marked gap on the page; the investigators re-derive the evidence from code, so no text from this record needs to be carried over.

<!--
article_metadata:
  filename: "01-signals.md"
  created: "2026-09-29"
  last_updated: "2026-09-29"
  version: "0.1"
-->
