---
title: "Signals — SmartDocs first implementation"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [signals, prompt-engineering, cross-repository]
description: "Activities surfaced by the SmartDocs convergence work that were never in scope for its goal — all three land in diginsight/tools rather than in this repository."
publish: false
---

# Signals — SmartDocs first implementation

Activities the convergence work surfaced that were **never in scope** for its goal. All three land in `diginsight/tools`, which no park-lot disposition can name — that mismatch is what these pages exist to fix.

Migrated from § Park lot of [01-smartdocs-web-convergence.plan.md](01-smartdocs-web-convergence.plan.md) on 2026-08-18, when the park lot became a coverage guarantee restricted to in-domain exclusions. The park-lot identifiers (`PL-2`, `PL-3`, `PL-7`) are preserved as aliases so existing references still resolve.

**Identifiers are identity; the listing order is priority** — derived from relevance and actionability, never assigned by impression.

📖 Record shape, kinds, sweep and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| — | `SIG-A` *(`PL-7`)* | `divergent-commitment` | — | — | `diginsight/tools` | `01-autonomous-streams-artifacts.plan.md` | `routed → 01-autonomous-streams-artifacts.plan.md` |
| 1 | `SIG-B` *(`PL-3`)* | `divergent-commitment` | medium | bounded | `diginsight/tools` | none found | `pending` |
| 2 | `SIG-C` *(`PL-2`)* | `divergent-commitment` | low | open | `diginsight/tools` | none found | `pending` |

`SIG-C` is `low` / `open` and would normally fall to an `other-signals` page. It is kept here so all three `diginsight/tools` items are readable in one pass, which is the whole reason they were separated from the park lot.

### `SIG-A` — generalise the learning-hub context beyond one site

- **Kind** — `divergent-commitment`
- **Goal** — separate the rules in `.copilot/context/90.00-learning-hub/` that apply to **any** space from those specific to the learning hub site.
- **Scope** — the folder-organisation, dual-metadata and reference-classification context files, each of which mixes site-specific conventions with rules any SmartDocs space would need.
- **Why it matters** — a second space onboarded today inherits either nothing or a set of rules written for a different site. The convergence made multi-space real; the context did not follow.
- **Target** — `diginsight/tools`.
- **Existing landing** — `01-autonomous-streams-artifacts.plan.md` in `diginsight/tools`.
- **State** — `routed → 01-autonomous-streams-artifacts.plan.md`
- **Relevance** — n/a — resolved to an existing landing, so its ordering lives in the plan that owns it.
- **Actionability** — n/a
- **Actionability strategy** — none needed. Recorded so the commitment is not captured a second time as new work.

### `SIG-B` — the SmartDocs API scaffolds left behind in `diginsight/tools`

- **Kind** — `divergent-commitment`
- **Goal** — decide the fate of `src/20.00 Api/SmartDocs` and `src/20.00 Api/SmartDocsApi` — follow the renderer into this repository, or be removed.
- **Scope** — two git-tracked `dotnet new webapi` scaffolds with no solution entry, sharing a name with the application that has since moved here.
- **Why it matters** — they carry the SmartDocs name in a repository that no longer holds SmartDocs. Anyone searching for the product finds unbuilt scaffolds first, and nothing records that they were left deliberately. The convergence plan decided only that it would **not** touch them (`D12-tools-scaffolds-out-of-scope`); it did not decide what should happen to them.
- **Target** — `diginsight/tools`.
- **Existing landing** — none found.
- **State** — `pending`
- **Relevance** — `medium` — misleading rather than harmful; nothing breaks while it waits.
- **Actionability** — `bounded` — the repository is known and the two paths are named; whether the answer is migration or deletion is the open part.
- **Actionability strategy** — read both scaffolds, establish whether either contains work not reproduced by the converged application, then either migrate or delete. The decision is small but must be made in `diginsight/tools`, by someone who can see what else depends on them.

### `SIG-C` — AI content services over the spaces

- **Kind** — `divergent-commitment`
- **Goal** — provide semantic search, summarisation and question answering over SmartDocs spaces, as a separate service rather than inside the renderer.
- **Scope** — a service alongside the existing API scaffolds in `diginsight/tools`, consuming the same content sources the renderer reads.
- **Why it matters** — it was stated as a direction during the convergence work and would otherwise exist only in that conversation. Recording it costs nothing; losing it means rediscovering the idea and its rationale from scratch.
- **Target** — `diginsight/tools`.
- **Existing landing** — none found.
- **State** — `pending`
- **Relevance** — `low` — opportunistic; nothing degrades while it waits.
- **Actionability** — `open` — the repository is known, but the scope is a direction rather than a shaped piece of work, and it has no owner.
- **Actionability strategy** — needs shaping before it can be planned: which content sources, which query surface, and whether it serves the renderer or stands alone. Until that is answered, no plan can be written that would survive contact with the first design question.
