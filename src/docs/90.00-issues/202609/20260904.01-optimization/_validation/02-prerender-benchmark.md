---
title: "Benchmark — is Markdown rendering actually the bottleneck?"
author: "Dario Airoldi"
date: "2026-09-05"
description: "WS-0 measurements for the prerendering plan: Markdig render cost, conditional HTTP caching, document latency and in-app navigation, with the resulting gate decision."
---

# Benchmark — is Markdown rendering actually the bottleneck?

## 🎯 What this decides

`02-enable-markdown-prerendering.plan.md` is gated on `D1-measure-before-enabling` and `D6-compare-conditional-http-cache-first`. Neither rendered-page tier may be enabled until measurement shows rendering is expensive enough to be worth removing. This document records that measurement and applies the gate.

## 🧾 Run context

| Item | Value |
|---|---|
| Date | 2026-09-05 |
| Configuration | `devdocs` — `dotnet run --no-launch-profile`, `RootPath: ..\docs`, visible terminal |
| Corpus | 86 Markdown files, 778 649 bytes, 9 054 bytes average |
| Render harness | Console host referencing `Diginsight.SmartDocs.Web.Shared`, `Release`, single thread, direct `MarkdigMarkdownRenderer.Render` calls |
| Browser | Chromium at `http://localhost:5280` |
| Method | 10 warm-up iterations discarded, then 30 measured iterations, per the plan |

## 1️⃣ Markdig render cost (step 0.1)

Stratified sample, measured in isolation with no HTTP, no cache and no source retrieval.

| Sample | Source bytes | p50 ms | p95 ms | mean ms |
|---|---:|---:|---:|---:|
| smallest | 0 | 0.044 | 0.087 | 0.048 |
| p25 | 3 817 | 1.866 | 2.708 | 2.031 |
| median | 6 248 | 3.538 | 5.414 | 3.810 |
| p75 | 10 514 | 9.418 | 24.117 | 11.280 |
| metadata-heavy | 20 515 | 7.285 | 14.402 | 7.968 |
| largest | 108 207 | 38.370 | 53.719 | 38.321 |

Whole-corpus sweep: **204.4 ms** for all 86 files, **0 failures**, 2.377 ms per file, 8.66 ms per KB.

Extrapolating the measured per-file mean: **1 300 files ≈ 3.1 s**, 2 000 files ≈ 4.8 s, 5 000 files ≈ 11.9 s of single-threaded CPU.

## 2️⃣ Conditional HTTP caching (steps 0.4 and 0.6)

Same median article (6 248 bytes) through `GET /_content/…`, measured from the browser.

| Variant | Status | p50 ms | p95 ms | mean ms | Body bytes per request |
|---|---|---:|---:|---:|---:|
| Baseline, no validator (0.4) | 200 | 24.7 | 29.8 | 25.19 | 6 248 |
| With `If-None-Match` (0.6) | 304 | 29.0 | 34.5 | 29.13 | 0 |

> **⚠️ Corrected 2026-09-05 — the 3.9 ms gap above is an artifact, and the explanation first given for it was wrong.**
>
> The two rows were measured in **separate batches**, so they also captured unrelated drift in machine load. Re-measured with the two arms **interleaved** in one run — 60 pairs, alternating request by request, so drift hits both equally — the difference disappears:
>
> | Variant | p50 ms | p95 ms | mean ms | n |
> |---|---:|---:|---:|---:|
> | 200 unconditional | 60.21 | 138.96 | 69.71 | 60 |
> | 304 conditional | **58.30** | 149.55 | 70.52 | 60 |
>
> Mean delta **+0.81 ms**, with p50 slightly favouring `304` — well inside the noise implied by a p95 spread of 139–150 ms. **Conditional caching is latency-neutral on loopback, not slower.** (Absolute values are higher than the browser rows because this harness uses a PowerShell `HttpClient` with more per-request overhead; only the within-run comparison is meaningful.)
>
> The original causal explanation — *"the server re-reads and re-hashes the file on every request"* — **was incorrect**. `FileSystemContentSource` does hash the bytes, but it sits behind `CachedContentSource`, which caches the entire `ContentResult` **including its `ETag`** in SmartCache at `MaxAge` `7.00:00:00`. The read and the SHA-1 therefore happen once per cache entry, not once per request, and both a `200` and a `304` are served from the same cached envelope.

**What this means for change rate.** A low content change rate does not make a `304` cheaper, but not for the reason first stated: each `304` is already cheap because the validator is cached. The reason change rate cannot help is simply that a `304` still costs a **round trip**. Change rate only becomes valuable when the browser is allowed to skip the request altogether.

### 2b⃣ `Cache-Control: max-age` — the setting that does convert a low change rate into a win

Both mechanisms are now configurable under `Site:ContentCache` so they can be measured apart. Measured in the browser under normal cache semantics rather than `no-store`, which is what a real navigation does:

| Mode | Configuration | p50 ms | p95 ms | mean ms | Network requests |
|---|---|---:|---:|---:|---|
| Always revalidate | `MaxAgeSeconds: 0` | 29.0 | 34.5 | 29.13 | every request |
| Fresh for 5 minutes | `MaxAgeSeconds: 300` | **4.7** | **10.0** | **5.37** | **1 of 29** |

With `max-age=300`, 28 of 29 repeat fetches were answered by the browser cache (`transferSize` 0 against a `decodedBodySize` of 6 248), giving a **~6x reduction** in content-fetch latency on repeat views against the revalidating default.

The honest bound: this shrinks a ~29 ms component of a ~517 ms navigation, so the navigation-level gain is on the order of 5%. It is a real, cheap win, and it is larger than anything prerendering offers — but it does not touch the ~308 ms floor either.

The cost is staleness. There is no content-invalidation endpoint, so a non-zero `max-age` serves outdated Markdown until it expires. `MaxAgeSeconds` therefore defaults to `0`.

## 3️⃣ Document latency and navigation (steps 0.2 and 0.3)

Full prerendered document responses, same method.

| Article | Source bytes | p50 ms | p95 ms | Render share of p50 |
|---|---:|---:|---:|---:|
| p25 | 3 817 | 308.5 | 326.6 | 0.7% |
| median | 6 248 | 315.1 | 374.8 | 1.2% |
| p75 | 10 514 | 369.5 | 423.0 | 3.1% |
| largest | 108 207 | 456.5 | 584.7 | 8.4% |

In-app navigation after hydration measured **p50 517 ms, p95 628 ms** over 30 samples.

The floor is the finding: an article whose rendering costs 1.9 ms still takes **308 ms** to serve. That ~308 ms is independent of article size and therefore is not rendering.

## 🚦 Gate decision (step 0.8)

**Precondition.** The existing source-byte SmartCache still serves the live fallback path; every one of the 86 files rendered without failure.

**`D6-compare-conditional-http-cache-first`.** Conditional HTTP caching is **latency-neutral** — interleaved measurement puts it within 0.81 ms of an unconditional `200`, with p50 marginally in its favour. It eliminates 100% of response body bytes. It is therefore retained: it costs nothing measurable and saves bandwidth for remote clients. Loopback cannot quantify that bandwidth benefit; a remote-client measurement is required before claiming a latency number for it.

**`Cache-Control: max-age` does meet a latency target.** At `MaxAgeSeconds: 300` the browser answers 28 of 29 repeat fetches from its own cache, cutting content-fetch p50 from 29.0 ms to 4.7 ms — about 6x, and roughly 5% of a full navigation. Both settings are configurable under `Site:ContentCache` and default to today's behaviour (`ConditionalRequestsEnabled: true`, `MaxAgeSeconds: 0`), so enabling freshness is a deliberate act that trades staleness for latency.

**`D1-measure-before-enabling`, first arm — p95 render ≥ 5 ms.** Met for the median (5.41 ms), p75 (24.12 ms), metadata-heavy (14.40 ms) and largest (53.72 ms) samples; not met for p25 (2.71 ms).

**`D1-measure-before-enabling`, second arm — ≥ 10% cold article latency reduction.** **Not met.** Removing rendering entirely would cut 0.7% to 8.4% of document latency, and roughly 1% of in-app navigation.

**Outcome.** The threshold is an `OR`, and the first arm is met, so the plan's own rule permits enabling the server rendered-page tier. The measurement nevertheless contradicts the plan's premise: **rendering is not the dominant cost.** Prerendering every article perfectly would leave the p50 document response between 283 ms and 418 ms. The ~308 ms size-independent floor is the larger target and belongs to a different investigation.

Publish-time cost is also far smaller than the plan assumed: a full 1 300-file release renders in about **3 seconds** of CPU. The resumable durable operation state, publish-duration budget and batch tuning specified in WS-C are sized for a problem the measurement does not show.

## 🧭 Negative branches recorded

| Item | Status |
|---|---|
| Learn-scale corpus slice (0.1, E6a) | **Blocked** — `..\..\..\..\darioairoldi\Learn.01` is absent from this workspace. Extrapolation from the measured per-file mean is used in its place and is not a substitute for the required measurement. |
| Blob Storage cold retrieval (0.3) | **Blocked** — filesystem retrieval is measured; Blob requires a deployed environment. |
| WASM render cost isolated from navigation (0.2) | **Partial** — total in-app navigation is measured; the render share inside the WASM runtime is not separated, so the client-tier arm of `D1` is not yet decidable. |
| Conditional caching benefit for remote clients | **Not measurable on loopback** — recorded as a bandwidth-only result. |
| `max-age` on a genuinely low-change-rate deployment | **Open** — measured here with a synthetic 5-minute window on a local corpus. A production deployment with a real change rate should be measured before choosing a permanent value. |
