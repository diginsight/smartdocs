---
title: "Other signals — appsettings space layering"
author: "Dario Airoldi"
date: "2026-08-21"
categories: [signals, prompt-engineering]
description: "The less defined remainder of the signal sweep for the appsettings space-layering work item."
publish: false
---

# Other signals — appsettings space layering

The less defined remainder of this work item's sweep: signals whose relevance is not `high` **and** whose actionability is `open`. The primary page is [02-signals.md](02-signals.md).

📖 Record shape, kinds, sweep and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-5` | `investigation-lead` | low | open | `diginsight/smartdocs` | none found | `pending` |

### `SIG-5` — model the space collection as a keyed map rather than an ordered array

- **Kind** — `investigation-lead`.
- **Goal** — determine whether `Site:Spaces` should be a map keyed by space identifier instead of an array, so that a layered declaration is matched by identity rather than by position.
- **Scope** — the shape of `SiteOptions.Spaces` and every declaration of it, across both this repository and the private peer. A keyed map would make an override name the space it modifies, would make adding a space in one layer and modifying another in a different layer expressible, and would remove the index coupling entirely. Open questions the investigation must answer: whether ordering matters anywhere (route resolution already sorts by route-base length, so probably not); whether a map still leaks fields the same way (it does, but the leak becomes attributable to a named space instead of a position); and whether the migration is worth a breaking change to files in two repositories.
- **Why it matters** — the fix this work item applied is a **convention** — one layer declares, and it declares everything. Conventions hold until someone reasonable does the other thing. A keyed map would make the same guarantee structural, which is the difference between a rule that is followed and a rule that cannot be broken.
- **Target** — `diginsight/smartdocs`, `SiteOptions`/`SpaceOptions` and every settings file that declares a space; with mandatory coordination into `diginsight/smartdocs.internal`, whose two deployment overlays would change in the same breaking step.
- **Existing landing** — none found. Searched this repository's `*.plan.md` files for `Site:Spaces` and options-shape work: the convergence plan defines the array shape as designed and records no proposal to revise it.
- **State** — `pending`.
- **Relevance** — `low`. The convention now in place removes the failure mode; nothing degrades while this waits.
- **Actionability** — `open`. The scope spans two repositories, the migration is breaking, and the trade-off against a working convention has not been assessed.
- **Actionability strategy** — becomes bounded once someone decides whether structural enforcement is worth a coordinated breaking change; the questions that decision needs are enumerated in the scope above, so the investigation starts from them rather than from the code.
