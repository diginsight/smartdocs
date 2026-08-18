---
title: "Documentation-manager artifacts — three defects found by using them, 2026-08-18"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [issue, prompt-engineering, governance, plan-authoring]
description: "A working session on content classification exposed three unrelated defects in the artifact set that governs documentation: a classification procedure reaching almost nothing, a plan artifact type absent from the metadata contract, and a conversation-to-analysis path that silently discards everything outside the issue."
publish: true
---

# Documentation-manager artifacts — three defects found by using them, 2026-08-18

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🗂️ What this work item contains](#-what-this-work-item-contains)
- [🔍 The three defects](#-the-three-defects)
- [🧨 Root cause A — the plan artifact type escaped the metadata contract](#-root-cause-a--the-plan-artifact-type-escaped-the-metadata-contract)
- [🪞 Root cause B — goal satisfaction was confirmed by proxy, twice](#-root-cause-b--goal-satisfaction-was-confirmed-by-proxy-twice)
- [🛠️ What changed](#-what-changed)
- [📡 Signals](#-signals)
- [🎓 What generalises](#-what-generalises)
- [🔴 Open items](#-open-items)
- [📎 Appendix — deviations in this analysis](#-appendix--deviations-in-this-analysis)
- [🏁 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🎯 Introduction

This work item started as a review of the content-classification model shipped the day before, and did not stay there. Using the artifact set exposed three defects that reading it had not: the classification **procedure** reached almost none of the artifacts it governs, the **plan** artifact type was absent from the metadata contract that every other type obeys, and the path from a conversation to an issue analysis had no slot for anything the conversation revealed **other than the issue**.

The three are unrelated in cause. They share one property, and it is the reason they are analysed together: **each was invisible from inside the artifact that contained it.** Every one required either using the artifact against a case it was not built for, or being challenged from outside.

This page is the entry point. It says which defect is which, what each one's root cause turned out to be, what changed, and what generalises. The detailed evidence and the executable work live in the two plan files; the material that belongs to other repositories lives on the signals pages. **Nothing is restated here that has an authority elsewhere** — a second copy is a second authority, with no way to tell which is current.

**Internal companion.** `src/docs/90.00-issues/202608/20260818.01-docmanager-improvement/00-overview.internal.md` in the private peer repository `diginsight/smartdocs.internal` carries what this page deliberately omits: one security finding stated precisely, the resolved local locations of the peer repositories, and the verification commands as executed. This is a code-formatted path rather than a link, because the target does not exist in this repository.

## 🗂️ What this work item contains

| Page | What it is | Status |
|---|---|---|
| `00-overview.md` | this page — narrative, root causes, generalisation | — |
| [01-content-management-artifacts-improvement.plan.md](01-content-management-artifacts-improvement.plan.md) | defect 1: reach, declaration mechanism, private-repo fail-safe | `draft` — three decisions open |
| [02-artifacts-improvement-for-signals-capturing.plan.md](02-artifacts-improvement-for-signals-capturing.plan.md) | defect 3: signal capture — evidence, design, and the executed steps | `in-progress` — all five steps done |
| [03-signals.md](03-signals.md) | signals that are relevant **or** actionable, in priority order | 5 records |
| [04-other-signals.md](04-other-signals.md) | the less-defined remainder | 1 record |

Defect 2 has no plan of its own. It is analysed on this page and parked in plan 01's excludes, for the reason given in [🔴 Open items](#-open-items).

## 🔍 The three defects

| | Defect 1 — reach | Defect 2 — contract gap | Defect 3 — lost signals |
|---|---|---|---|
| **Artifact** | the classification procedure | the metadata contract | the conversation-to-analysis path |
| **Symptom** | the rules load everywhere; the procedure fires in three places | a plan was written that satisfied every loaded rule and still lacked the required metadata | five items surfaced in conversation, none had anywhere to go |
| **How it surfaced** | reviewing the model against the full artifact set instead of the one prompt it was built for | the owner observing that *the plan itself is a PE artifact* | the owner observing that *the conversation produced signals no artifact captures* |
| **Root cause class** | wiring — a `#file:` snippet requires per-consumer wiring, so coverage equals the number of consumers wired | omission compounded by a near-miss — four separate documents each had a reason not to cover plans | scope — the analysis path asks *what happened*, never *what else was revealed* |
| **Owner** | [plan 01](01-content-management-artifacts-improvement.plan.md) | this page, parked | [plan 02](02-artifacts-improvement-for-signals-capturing.plan.md) — closed |

### Why they surfaced together

They did not share a cause, but they shared a **trigger**: this was the first session in which the artifact set was used to produce something it had not been designed against. Defect 1 needed the set to be surveyed rather than read. Defect 2 needed a plan to actually be written. Defect 3 needed a conversation long enough to diverge.

That is worth stating plainly, because it predicts where the next defect is: **in whichever artifact has never yet been run against a case it was not written for.**

## 🧨 Root cause A — the plan artifact type escaped the metadata contract

Defect 2 is the one worth a full root-cause pass, because the failure was not a missing rule. It was **four documents each independently declining to cover the same type**, with no fifth document noticing the hole.

| # | Document | Why it did not cover plans |
|---|---|---|
| 1 | `.copilot/context/00.00-prompt-engineering/00.03-metadata-contracts.md` | enumerates a row per artifact type. **There is no Plan row.** The contract is a table, and a table's silence is not visible as an absence |
| 2 | `.github/instructions/` | there is no `pe-plan-files.instructions.md`. Every other PE artifact type has one; the type that most needs governance has none |
| 3 | the one place plans *are* mentioned | the requirement is written with a trailing colon, in the style of a frontmatter key — so it reads as an example of a field, not as a required field. It is never listed among the required ones |
| 4 | `.github/instructions/documentation.instructions.md` | matches `src/docs/**/*.md`, which **does** match a plan file, and mandates the bottom block. But its wording is *"Articles use two metadata blocks"*. Changelog files were given an explicit exemption; plans were given neither an exemption nor an inclusion |

Cause 4 is the interesting one. The rule that would have caught the defect **was loaded** and **did match the path**. It failed on wording: an author reasonably reads *"articles"* as excluding a plan, and the file itself proves that exemptions are stated explicitly when intended — so the absence of an exemption reads as *not considered* rather than *deliberately included*.

**The generalisable failure.** A contract expressed as an enumeration cannot report a missing row. Every one of the four documents was individually defensible; the set was not. Nothing in the system compares *artifact types that exist* against *artifact types the contract covers*, so a new type is governed only if someone remembers to add it.

**Consequence carried forward.** `plan_metadata` is used as the bottom-block name in both plan files in this folder, and it remains **provisional** — it is not in the contract, so it is a convention this work item invented, not one it obeyed. That is stated here so a later reader does not mistake current practice for a settled decision.

## 🪞 Root cause B — goal satisfaction was confirmed by proxy, twice

The second defect worth analysing was in the working method, not in a file.

Twice in this session, a gate was declared satisfied by checking that **every item on a checklist had been addressed** rather than by testing whether **the stated goal was reachable through what had been built**. Both times the owner rejected the answer, the second time explicitly: *"this doesn't answer the question — please understand if the goal and scope are really satisfied."*

The substantive failure the second challenge exposed is concrete. Plan 02's goal promised that captured signals would *"survive the conversation"* **and** be *"worked in a defensible order."* The first half held. The second did not:

- an earlier decision had removed the repository-level register that would have ordered signals across work items;
- the delivery mechanism selected records on `target` and `state`, and **never read `relevance` or `actionability`**;
- so a `high` / `ready` signal could sit behind a `low` / `open` one, with nothing in the system reporting it.

Every checklist item was addressed. The goal was not reachable. The plan had been promoted to `actionable` on that basis and had to be reverted to `draft`.

**Why the proxy is attractive.** Checklist coverage is *decidable* — each item is present or absent. Goal reachability requires constructing the path from the goal to the artifacts and finding where it breaks, which is open-ended and can fail. A verification step that can only pass is not a verification step.

**The resolution was to correct the goal, not to fake the mechanism.** Rather than build an index and a priority-aware consumer so the wording would become true, the wording was narrowed: the plan now guarantees a signal is *captured, defined and ordered on its page*, and states that cross-work-item prioritisation is a judgement it does not automate. That is a smaller promise, and it is one the artifacts actually keep.

**This pattern is not yet governed anywhere.** It is captured as `SIG-6` on [03-signals.md](03-signals.md).

## 🛠️ What changed

Defect 3 was closed end to end in this session. Defects 1 and 2 were analysed and left staged.

| Artifact | Change |
|---|---|
| `.github/skills/signal-capture/SKILL.md` | **new** — the seven-question sweep, five kinds, nine-field record, priority derivation, page placement |
| `.github/templates/01.00-article-writing/signals.template.md` | **new** — page shape, with sibling changelog |
| `.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md` | step 2.5 (mandatory sweep before the split), amended page table, five signal QA checks, and a step 6 that must report the signals **or** state that none were found |
| `.github/instructions/plan-execution.instructions.md` | gate check 6 bars divergent items from the park lot and routes them to the signals page |
| `.copilot/context/00.00-prompt-engineering/05.11-plan-authoring-discipline.md` | open decisions, discovery, park lot and signals are now four distinct buckets, with a *where it lives* column |

**The design decision that shaped all of it.** A park lot and a signal look similar and guarantee opposite things. A park lot entry is *in-domain for the plan's goal but deliberately excluded*, and it guarantees **coverage** — nothing in-domain was dropped. A signal is *never in scope*, and it guarantees **continuity** — nothing out-of-domain was dropped. A plan is terminal; a signal outlives every plan in its work item. That is why signals live outside the plan file entirely, and why plan 02 replaced its own inline signal records with a pointer rather than keeping a copy.

**Self-containment became the whole obligation.** The owner decided routing to other repositories happens **by hand** — this repository identifies and defines the signal, and nothing more. That removes the automation, and in doing so raises the bar on the record: a person will open it months later, in a different repository, without the conversation and without this work item. Every field of the record exists to make that reading possible.

**This analysis is the acceptance test.** Plan 02's first exit criterion is that *an issue analysis produced from a conversation reports its captured signals, or explicitly reports that none were found.* The section below is that report, and it was produced by running the amended prompt — not by inspecting it.

## 📡 Signals

The sweep was run over this conversation, all seven questions. It found **two signals not already captured**, and confirmed that everything else it surfaced was either already recorded or in-domain for one of the two plans.

| New | Id | Kind | Relevance | Actionability | Target |
|---|---|---|---|---|---|
| ✅ | `SIG-6` | `expression-defect` | high | bounded | this repository |
| ✅ | `SIG-7` | `divergent-commitment` | medium | bounded | this repository |

- **`SIG-6` — verification by proxy.** The failure analysed in [root cause B](#-root-cause-b--goal-satisfaction-was-confirmed-by-proxy-twice). No artifact currently requires a gate to test goal reachability rather than checklist coverage — which is the one thing the Actionability Gate exists to prevent.
- **`SIG-7` — the work-item folder ordinal space.** Analysis pages, plan files and signals pages all compete for the same `NN-` sequence in a work-item folder, and no rule allocates it. In this folder the consequence was concrete: the entry point could not take `01-`, because a plan already had it. See [the appendix](#-appendix--deviations-in-this-analysis).

Both are recorded in full on [03-signals.md](03-signals.md), which now holds five records; [04-other-signals.md](04-other-signals.md) is unchanged at one.

**What the sweep did not find, stated deliberately.** No new `propagation-debt` beyond `SIG-2` — every artifact changed today falls inside the scope `SIG-2` already declares. No new `upstream-feedback` beyond `SIG-1`. No new `investigation-lead` beyond `SIG-4`. Silence on these is a result, not an omission.

**A limitation the sweep exposed in itself.** `SIG-7` does not fit the five-kind taxonomy cleanly. It is an in-repository defect that falls outside every current plan's domain, and the kinds assume a signal points *elsewhere*. It is filed as `divergent-commitment` because that is the closest fit, and the strain is recorded here rather than hidden by the filing.

## 🎓 What generalises

1. **An enumerated contract cannot report a missing row.** Any governance model built as *a table of types* needs a separate check comparing types that exist against types the table covers. Otherwise a new type is ungoverned by default, and looks compliant.

2. **A rule that is loaded is not a rule that is applied.** Cause 4 above had the right rule, loaded, matching the right path — and it still failed, on wording. Coverage of the matcher is not coverage of the reader.

3. **Discovery-based reach is probabilistic; wiring-based reach is bounded.** Defect 1 chose a skill over a snippet precisely to trade a bounded, small reach for an unbounded, uncertain one. Plan 02 hit the mirror image and recorded it: signal capture is **deterministic at exactly two moments** — issue analysis, and routing away from a park lot — and probabilistic everywhere else. Both moments were present in the conversation that originally lost three signals, which is why that was accepted rather than engineered around.

4. **A verification step that cannot fail is not verifying.** If the check is *"is every item addressed?"*, it will pass whenever the work was done, whether or not the work reaches the goal. The check has to be *"construct the path from goal to artifacts and say where it breaks."*

5. **Correcting the promise can beat building the mechanism.** When a goal and its artifacts disagree, the default instinct is to build until the goal is true. Narrowing the goal to what the artifacts genuinely guarantee is legitimate, and produces a system that does not lie about itself — provided the narrowing is stated, not slipped in.

6. **The convenient artifact and the correct artifact diverge under folder pressure.** `SIG-7` is a small instance of a general problem: conventions that assign positions (`01-`, `02-`) assume a single allocator. As soon as two artifact classes share the namespace, the convention silently stops being satisfiable, and the first casualty is the one that has to come first.

## 🔴 Open items

| Item | Where | Why still open |
|---|---|---|
| Three decisions gating plan 01 | [plan 01](01-content-management-artifacts-improvement.plan.md) § Open decisions | preference calls that evidence cannot settle; the actionable body is written only once they close |
| Defect 2 has no plan | parked in plan 01's `scope.excludes` | fixing the metadata contract touches the contract, a new instruction file and the wording of `documentation.instructions.md` — a scope of its own, not an addendum to either plan |
| `plan_metadata` is provisional | both plan files | the block name is a convention this work item invented; it becomes real only when the contract gains a Plan row |
| Two discovery items in plan 02 | [plan 02](02-artifacts-improvement-for-signals-capturing.plan.md) § Discovery | one of them — *did the analysis prompt ever run in this folder?* — is answered below |
| `SIG-6`, `SIG-7` | [03-signals.md](03-signals.md) | newly captured; no landing resolved yet |

**Discovery item answered by this run.** Plan 02 asked whether the empty `overview.md` in this folder meant the analysis prompt was *never run here* or *ran and failed*. It was never run: the file was a placeholder of zero bytes, untracked by git, with no frontmatter and no inbound reference. So Finding 6 of plan 02 is an **adoption gap, not a prompt defect** — which matters, because it means no prompt change was warranted for it, and none was made.

## 📎 Appendix — deviations in this analysis

Two deviations from the analysis prompt, stated rather than absorbed.

**1. The entry point is `00-overview.md`, not `01-overview.md`.** The prompt names `01-overview.md`. In this folder `01-` and `02-` were already taken by plan files created earlier in the session, and `03-` / `04-` by the signals pages. Renumbering all four would have rewritten nine cross-references and changed the identity of a plan that is currently `in-progress`; leaving the page unnumbered would have sorted the entry point **last**, which is the exact non-conformance the page exists to fix. `00-` sorts first, and the file it replaced had zero inbound references, so the rename cost nothing. This is a deviation, not compliance — the underlying convention gap is `SIG-7`.

**2. No separate case pages were written.** The prompt's page table provides one page per incident. Here the incident detail already has an authority: defect 1 in plan 01's § Evidence gathered, defect 3 in plan 02's six findings, and the five original signals on the two signals pages. Case pages would have been a second copy of each, and a second copy is a second authority. Only the two root causes with **no** existing home — the metadata-contract escape and the verification-by-proxy pattern — are written out, as sections on this page.

**What this run did not prove.** It exercised the sweep and the reporting requirement of the amended prompt, and both worked. It did **not** exercise the classification split against sensitive content — this work item produced no resource-bearing material, so the companion holds three facts rather than a full parallel document, and the alias registry gained no entries. The split machinery remains verified only by the previous incident report.

## 🏁 Conclusion

Three defects, one trigger. The set of artifacts that governs this repository's documentation was sound as written and incomplete as used, and the only thing that exposed the difference was using it — surveying the whole set instead of reading one file, writing a plan instead of describing one, and letting a conversation run long enough to diverge from its own goal.

Defect 3 is closed and its mechanism is now the thing reporting on itself. Defects 1 and 2 are analysed and staged: one waiting on three preference decisions, one waiting on a scope of its own. The most durable output is probably neither — it is the fourth lesson above, that a verification step which cannot fail is not verifying, and the finding that a table of types cannot report the row it is missing.

## 📚 References

- **📘** [01-content-management-artifacts-improvement.plan.md](01-content-management-artifacts-improvement.plan.md) — defect 1, with the reach argument and the private-repository fail-safe
- **📘** [02-artifacts-improvement-for-signals-capturing.plan.md](02-artifacts-improvement-for-signals-capturing.plan.md) — defect 3, with the six findings and the executed steps
- **📘** [03-signals.md](03-signals.md) — five signal records in priority order
- **📘** [04-other-signals.md](04-other-signals.md) — the less-defined remainder
- **📗** `.github/skills/signal-capture/SKILL.md` — the sweep, the kinds and the record shape
- **📗** `.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md` — the prompt this analysis was produced by
- **📕** `src/docs/90.00-issues/202608/20260818.01-docmanager-improvement/00-overview.internal.md` in `diginsight/smartdocs.internal` — the identifier-bearing companion
