---
title: "Startup and navigation at any scale"
author: "Dario Airoldi"
date: "2026-10-01"
categories: [analysis, performance, startup, caching, navigation, smartcache, blazor, scalability]
description: "Work item landing page: the analysis that reviews the 2026-09-29 startup findings, designs navigation for an unbounded corpus, and records what has landed."
publish: false
---

# Startup and navigation at any scale

This work item asks what startup, navigation, and metadata updates should look like when the number of documents has no upper bound, and turns the answer into a sequence of changes. It reviews the startup analysis of 2026-09-29, measures three local runs and one deployed instance, and sets one rule for the target: **no request, startup step, or change may cost work proportional to the size of the corpus.**

## 📄 Pages

| Page | What it holds |
|---|---|
| [Startup and navigation optimization](01-startup-and-navigation-optimization.analysis.md) | The analysis: the run review, the caching model, the target design, the ranked levers, the four waves, and an implementation record per landed change |
| [Signals](02-signals.md) | The six activities the sweeps surfaced that were never in scope for this work item's goal |
| [`_validation/`](_validation/20261002.01-validation-sequence.md) | Twelve visible-browser validation sequences, one per change or batch, with their screenshots — the link opens the first |

## 🚦 Where it stands

Waves 0, 1, 2, and 3 are implemented and validated, all on 2026-10-02; waves 1 to 3 are deployed and wave 0 reaches an instance on the next deploy:

- **Wave 1 — remove accidental work.** A static icon, prev/next from the level, validators and compression on every response, a cache that stores parsed records instead of raw header text, the asset-folder rule, and a startup that doesn't refold an unchanged snapshot. (✅ done)
- **`C32` — end the crawler trap.** The measured cost of the deployed instance wasn't navigation: one crawler held the core on routes that don't exist. A 404 before prerendering, rooted links, and a `robots.txt` cut CPU from 160–228 to 4 CPU-seconds per five minutes. (✅ done)
- **Wave 2 — one folder record, nothing waits.** The folder record became the one unit of folder metadata, and the warm-up, revalidation, top-bar dropdowns, and the hub all moved off the request path. (✅ done)
- **Wave 3 — render once, cache one way.** A page renders once on the server, the prerendered state carries what the browser already shows, and one key type and one path-set rule address every cached kind. (✅ done)
- **`M2` — the scale harness.** A generator for a synthetic tree of any fan-out, an overlay that serves it outside the debug profile, and a measurement script. Across a ten-fold step in corpus size the first page is byte-identical and memory is flat at 20 s, while the background crawl keeps growing with the corpus. (✅ done)
- **`C33` — Always On.** Turned on for both SmartDocs apps. The docs site's first request when idle went from `504` after 110.7 s to 422 ms. (✅ done)
- **`M1` — the deployed baseline, completed.** Steady state after waves 1 to 3 is 3.4–4.7 CPU-seconds per 15 minutes at 0.076–0.096 s average. (✅ done)
- **`PL-1` — what instrumentation costs.** 82–89% of a request's CPU before wave 0, on the profile a deployed instance runs. About 59 points were the application's own; 29 belong to the Diginsight library sources. Measured on a controlled host after an attempt on the deployed instance was withdrawn. (✅ done)
- **Wave 0 — stop paying for instrumentation on the hot path.** The per-lookup, per-file and per-level activities moved to a second activity source the base settings switch off and the Development overlay switches on; the application's own log category dropped to `Warning` outside local runs. A request costs **52% less CPU** and its p95 65% less, with no diagnostic deleted. (✅ done — not yet deployed)
- **`C34` — the folder-record validator.** `/_nav/folder` was the one response the browser re-reads that carried no entity tag; `C29` added the endpoint after `C20` tagged the rest. (✅ done)
- **Wave 4 — any size.** Persisted per-folder records, change-driven freshness, paged levels, server-side search, targeted pushes, and a narrowed body preload. The only wave still open. (🟡 todo)

## 💡 Conclusion

The cheapest wins were accidental costs rather than design faults, and the two largest were found outside navigation: a crawler trap that took the deployed core, and instrumentation that took 82–89% of a request. The second needed three configurations and a controlled host to measure — a two-way switch on a shared instance gave a figure ten times too large and attributed it to the wrong half. Wave 0 has since halved a request's CPU without deleting a diagnostic. What remains is wave 4, the only lever that removes the costs growing with the corpus.

## 📚 References

- [Startup and navigation optimization](01-startup-and-navigation-optimization.analysis.md) — the analysis this page summarizes.
- [Signals for this work item](02-signals.md) — the records the sweeps produced.
- [Startup and navigation analysis of 2026-09-29](../../202609/20260925.02-startup-optimization/overview.md) — the analysis this work item reviews and extends.

<!--
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
  structure: {status: "not_run", last_run: null}
  facts: {status: "not_run", last_run: null}

article_metadata:
  filename: "overview.md"
  created: "2026-10-01"
  last_updated: "2026-10-02"
  version: "1.2"
-->
