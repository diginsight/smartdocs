---
title: "Signals — appsettings space layering"
author: "Dario Airoldi"
date: "2026-08-21"
categories: [signals, prompt-engineering]
description: "Activities surfaced by the appsettings space-layering conversation that were never in scope for its goal."
publish: false
---

# Signals — appsettings space layering

Activities this work item's conversation surfaced that were **never in scope** for its goal. Each record is self-contained: delivery is manual, so a person reading it in another repository — without this conversation and without this work item — must be able to act on it as written.

**Identifiers are identity; the listing order is priority** — derived from relevance and actionability, never assigned by impression.

📖 Record shape, kinds, sweep and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-1` | `upstream-feedback` | high | ready | `diginsight/smartdocs` | `configuration-drift` class | `pending` |
| 2 | `SIG-2` | `propagation-debt` | high | ready | `diginsight/smartdocs.internal` | none found | `pending` |
| 3 | `SIG-3` | `expression-defect` | medium | ready | `diginsight/smartdocs` | none found | `pending` |
| 4 | `SIG-4` | `investigation-lead` | medium | bounded | `diginsight/smartdocs` | none found | `pending` |
| 5 | `SIG-6` | `investigation-lead` | low | ready | `diginsight/smartdocs` | `testing-validation.instructions.md` | `pending` |

`SIG-5` is `low` / `open` and therefore lives on [03-other-signals.md](03-other-signals.md).

### `SIG-1` — the hardening catalogue cannot describe an index-merged array override

- **Kind** — `upstream-feedback`.
- **Goal** — make the `configuration-drift` invariant able to describe the violation shape that produced this work item, so a robustness scan can detect it instead of relying on someone noticing.
- **Scope** — the `configuration-drift` row of the hardening invariant catalogue: its **violation shape** column. The invariant statement itself ("every setting the process requires is declared everywhere it runs, with a safe value") already covers the case; only the shapes that make it detectable are missing. Two shapes are absent: *a layered collection whose lower layer contributes fields no upper layer restates*, and *the same setting declared in three or more layers with no stated precedence*.
- **Why it matters** — the catalogue is the authority the robustness stream scans against. A shape it does not name is a shape no scan reports. This defect ran undetected precisely because every wrong value bound successfully, which is the class of fault a catalogue exists to catch; leaving the row as-is guarantees the next instance is also found by accident.
- **Target** — `diginsight/smartdocs`, `.copilot/context/10.00-application-development/09-hardening-invariant-catalog.md`, the `configuration-drift` row under § Behaviour under the real world.
- **Existing landing** — the `configuration-drift` class already exists and is the correct home; no plan or work item currently proposes extending it. Searched: all `*.plan.md` in this repository for `configuration-drift` and layered-configuration wording — no match outside the convergence plan's unrelated deployment section.
- **State** — `pending`.
- **Relevance** — `high`. An authority document that cannot express a demonstrated fault class contradicts its own purpose, and every scan run against it inherits the gap.
- **Actionability** — `ready`. The row exists, the invariant is unchanged, and the two shapes are stated above in the column's existing idiom.
- **Actionability strategy** — the change is an amendment to one table row in an existing context file, so it lands as a normal context-file edit; the evidence it needs is this work item's analysis page, which is already written.

### `SIG-2` — the deployment overlays became the sole declaration without being told

- **Kind** — `propagation-debt`.
- **Goal** — make the two deployment overlays in the private peer state that they are now the only declaration of `Site:Spaces`, so a future edit cannot trim a field on the assumption that a base value backs it.
- **Scope** — the two environment overlay files under `src/Diginsight.SmartDocs.Web/` in the peer repository. Two changes: a header comment stating the invariant (the base declares no space; this file must state every field of its element), and a check that each element is in fact complete. No value changes — both files were verified complete on 2026-08-21.
- **Why it matters** — before this work item, trimming a "redundant" field from an overlay produced a wrong-but-running host. After it, the same trim produces either a startup failure or a space with an empty field and no fallback. The behaviour on the peer side got stricter, and nothing in the peer repository records that. The one overlay that mounts its space under a route prefix is the acute case: dropping that single line silently relocates every published URL to the site root.
- **Target** — `diginsight/smartdocs.internal`, the two `appsettings.<environment>.json` overlays under `src/Diginsight.SmartDocs.Web/`.
- **Existing landing** — none found. Searched the peer's `src/docs` tree (13 files) for any plan or work item covering configuration overlays: the tree holds only internal companions, alias registry, evidence and environment references — no plan file exists in that repository at all.
- **State** — `pending`.
- **Relevance** — `high`. The peer now carries an undeclared obligation, and the failure it guards against is silent in the worst case.
- **Actionability** — `ready`. Both files are known, the text to add is stated above, and completeness was already verified.
- **Actionability strategy** — the peer has no plan-file convention, so this lands as a direct edit to the two overlays; the verification it needs is a single host start per environment with `ExternalConfigurationFolder` pointed at the peer, which this work item already exercised.

### `SIG-3` — "overlay" and "default" name a replacement that does not happen

- **Kind** — `expression-defect`.
- **Goal** — establish a formulation for layered configuration that does not imply replacement, so the next author of an environment file reasons about position rather than difference.
- **Scope** — the vocabulary used for configuration layering in this repository's settings comments, reference documentation and agent guidance. Not a code change.
- **Why it matters** — the wrong model was not held carelessly; it was held because the words carry it. "Overlay", "override" and "default" all describe replacement, and for scalars and objects they are accurate. For array elements they are false, and the falsehood is invisible because the merged result still binds. Any guidance that keeps the vocabulary reproduces the defect.
- **Target** — `diginsight/smartdocs`; the settings-file comments and whichever context or reference artifact ends up owning configuration conventions.
- **Existing landing** — none found. The application-development context set has no configuration-conventions file; the closest artifact is the invariant catalogue addressed by `SIG-1`, which governs detection rather than vocabulary.
- **State** — `pending`.
- **Relevance** — `medium`. It improves artifacts already in use and prevents recurrence, but nothing degrades while it waits.
- **Actionability** — `ready`. The record below is the deliverable.
- **Actionability strategy** — carried by quoting the two formulations wherever configuration layering is described; no derivation or investigation is required at the landing.
- **The mistake** — the base file's element was described as "the production shape … that part is sound", framing it as a default that each environment replaces. That framing survived until the two deployment overlays were actually read and turned out to declare every field themselves, which proved the base element had never been a default for anything — it was only ever a source of leaked fields. The framing was not merely imprecise; it argued for keeping the very thing that had to be removed.
- **Better expression** — *a layered array element is a floor, not a default: whatever an upper layer does not restate shows through*. Paired with the operational rule that follows from it — **only one layer declares an array, and that layer states every field** — the correct behaviour is derivable without knowing the binder's internals.

### `SIG-4` — local runs never exercise a prefixed route base

- **Kind** — `investigation-lead`.
- **Goal** — determine whether local development should be able to run a space mounted under a route prefix, and if so, provide an environment that does.
- **Scope** — the relationship between the local environments and the deployment environments with respect to `RouteBase`. All three local environments mount their space at the site root; one deployment mounts under a path prefix. Everything that depends on the prefix — first-segment reservation, `SpaceRegistry.ToRoute` link rewriting, longest-base resolution, the unclaimed-root index page — is therefore never executed before deployment. The question to answer is whether a fourth local environment is the right answer, or whether the prefix path should be covered by tests instead.
- **Why it matters** — `SpaceRegistry` contains real prefix logic and a validation rule that a route base be a single segment, and none of it runs locally. The one prior incident in this environment family reached production before the fault was observable; a routing fault has the same shape, and its symptom would be every link on the site.
- **Target** — `diginsight/smartdocs`; a local environment overlay, or the test surface, or both.
- **Existing landing** — none found. Searched this repository's `*.plan.md` files for route-base and multi-space local coverage: the convergence plan designs the multi-space model and the space index page, but records no local-coverage work item for either.
- **State** — `pending`.
- **Relevance** — `medium`. It improves confidence in code already deployed; nothing is currently broken.
- **Actionability** — `bounded`. The landing repository is known and the gap is precisely stated, but the choice between a new environment and test coverage has not been made, and the multi-space index page it would also exercise is not built.
- **Actionability strategy** — becomes executable once the multi-space decision is made; until then the useful step at the landing is to decide between an environment and a test, which this record states the inputs for.

### `SIG-6` — the validation harness has three undeclared traps for this host

- **Kind** — `investigation-lead`.
- **Goal** — record the host-specific traps that make automated validation of `Diginsight.SmartDocs.Web` silently produce wrong results, so a validation run is not re-derived each time.
- **Scope** — three facts, each verified on 2026-08-21 while validating this work item: the running host process is named for its assembly rather than `dotnet`, so a process kill filtered on `dotnet` leaves the port held and the next environment silently reuses the previous host; a new process started from an existing shell **inherits that shell's environment**, so a leftover environment-selection or URL variable overrides the launch profile the new window was given; and the article count rendered in the footer settles after hydration, so the first value read belongs to the previous page.
- **Why it matters** — each trap produces a **confidently wrong** result rather than an error. The first two cause a validation run to report on an environment other than the one under test — which is indistinguishable from success. During this work item all three occurred, and the first two together produced a browser check of the wrong environment that looked entirely normal.
- **Target** — `diginsight/smartdocs`, `.github/instructions/testing-validation.instructions.md`.
- **Existing landing** — `.github/instructions/testing-validation.instructions.md` exists and governs validation of this application; it currently states the visible-console and visible-browser requirements but no host-specific mechanics. No plan proposes extending it.
- **State** — `pending`.
- **Relevance** — `low`. It costs time and creates a risk of a false pass, but no artifact is outdated or contradicted while it waits.
- **Actionability** — `ready`. All three facts are verified and stated; the landing artifact exists.
- **Actionability strategy** — lands as an addition to the existing instruction file's validation mechanics; no investigation is required, only placement.
