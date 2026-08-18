---
title: "Content management artifacts — safety, robustness and coverage improvement"
author: "Dario Airoldi"
date: "2026-08-18"
categories: [prompt-engineering, content-classification, governance]
description: "Closes three defects in the content-management artifact set: a duplicated declaration mechanism, a split procedure that reaches only hand-wired consumers, and an unsafe private-repository default."
domain: "prompt-engineering"
goal: "Make every content-management artifact implement a safe, robust and effective classification criterion and endorse the four content principles — not only the one issue prompt that was wired by hand"
scope:
  covers:
    - "Where the repository and subtree classification declaration lives"
    - "How the classification procedure reaches artifacts nobody wired to it"
    - "The private-repository conditional and the direction of its fail-safe"
    - "Enabling changes: folder metadata parsing and non-public attribute projection"
  excludes:
    - "Runtime authorization for internal content (sibling plan)"
    - "Publish-workflow staging of both repositories (sibling plan)"
    - "Git history remediation for previously committed identifiers (parked)"
    - "Metadata governance for plan files themselves (parked)"
boundaries:
  - "MUST NOT let the split reduce readability or completeness in a private repository — principle E outranks the split"
  - "MUST NOT leave per-consumer wiring as the only path by which the procedure reaches an artifact"
  - "MUST NOT introduce a second declaration mechanism maintained in parallel with folder metadata"
  - "NEVER silently assume repository visibility when it cannot be determined — ask"
rationales:
  - "A rule that reaches 3 of ~170 artifacts is a convention, not an enforcement mechanism"
  - "A fail-safe must be safe in both directions — silent exposure and silent unreadability are both failures"
  - "One declaration mechanism resolved hierarchically prevents two files drifting apart"
status: draft
---

# Content management artifacts — safety, robustness and coverage improvement

> **Status: `draft`.** Three decisions in § Open decisions are preference calls that evidence cannot settle. Per `plan-execution.instructions.md` the actionable body is written only once they close. Everything below is goal, evidence and staged decisions — no executable step list yet.

## 🎯 Goal

Make the **whole** content-management artifact set — not one prompt — implement a safe, robust and effective classification criterion, and endorse the four content principles (**D** alias-first, **B** `.internal.md` separation, **C** two-repo mirror, **E** internal completeness).

Three defects found in the current implementation must be closed:

1. The peer declaration duplicates a metadata mechanism the repository already has.
2. The split **procedure** reaches only artifacts that were explicitly wired to it.
3. The private-repository case is unsafe: a private repo that never declares itself is silently treated as public and pays a readability cost for zero benefit.

## 🧭 Why this plan exists

The classification model shipped earlier today is sound, but it was wired the way a single incident report needed it. Reviewing it against the wider artifact set surfaces a coverage problem and a correctness problem that the incident report alone could not expose.

## 🔍 Evidence gathered

Every claim below was read from source, not inferred.

### Finding 1 — the folder metadata parser is the limitation, and it should be removed

[src/Diginsight.SmartDocs.Web.Shared/Navigation/FolderMeta.cs](src/Diginsight.SmartDocs.Web.Shared/Navigation/FolderMeta.cs#L26-L27) parses folder metadata with a **flat line regex**, not a YAML parser. Its own comment concedes the scope: *"Flat 'key: value' lines only (no nesting needed)"*.

What that costs today:

- **No nesting.** A block such as `internal_peer:` with indented children is scanned as unrelated top-level keys — the structure is invisible to the app.
- **Silent key collision.** The switch matches bare key names anywhere in the file, so an indented `label:`, `order:` or `hidden:` inside any nested block would be consumed as a **navigation override**.
- Unknown keys are ignored, so extending the file breaks nothing today — but the failure mode when nesting arrives is silent, not loud.

`metadata.yml` will grow in content and complexity, so "no nesting needed" is already false. The conclusion is **not** that the declaration belongs elsewhere — it is that the regex scanner must be replaced by a real YAML parser, with unknown-key tolerance kept deliberately and nav keys read from their own section so nothing can collide by accident.

### Finding 2 — published-by-default is correct; `metadata.yml` needs a way to mark attributes non-public

[.github/workflows/03.PublishDocsContent.yml](.github/workflows/03.PublishDocsContent.yml#L160) stages content with an **unfiltered** copy — every file under `src/docs`, not just Markdown — and [src/Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs](src/Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs) serves `/_content-raw/{**key}` without an authentication check. So a folder `metadata.yml` is anonymously retrievable in full.

Public-by-default is the right default for folder metadata — labels, icons and ordering are presentation, and nothing is gained by hiding them. What is missing is the *opt-out*: a way for a metadata file to declare that specific attributes are not public, and a raw-content path that honours that declaration by projecting the public subset rather than serving the file verbatim.

That capability is worth having on its own merits, independently of classification. It is the same principle the content model already applies to documents, applied to metadata.

Note on sensitivity: the internal peer's **repository name is already deliberately public** and is not the part worth withholding. The candidates for a non-public marking are operational details — resolution paths and environment-variable names — and even those are low-value to an attacker. This is hygiene, not a breach vector.

### Finding 3 — the procedure reaches only artifacts that were wired to it

The rules live in an instruction file matching `**/*.md`, so they are always loaded. But the **procedure** lives in a prompt snippet, which fires only where a consumer writes `#file:`. Currently that is two prompts and one agent.

[.copilot/context/00.00-prompt-engineering/01.03-file-type-decision-guide.md](.copilot/context/00.00-prompt-engineering/01.03-file-type-decision-guide.md) is explicit about the right instrument: a **skill** is *AI-discovered*, carries bundled resources, and works across VS Code, CLI and coding agent. A snippet is a `#file:` fragment requiring per-consumer wiring.

The user's instinct is correct, and the repository's own decision guide already says so.

### Finding 4 — the private-repository case is unsafe in the direction that matters

The current fail-safe is *absent declaration ⇒ treat as public*. That protects a public repository, but in a private repository that never adds `repository.metadata.yml` it silently:

- aliases identifiers that need no aliasing,
- splits documents that should stay whole,
- degrades **E** (understandability) for zero security gain.

There is no detection beyond the declaration file, and no point at which the agent stops and asks. Silence is the failure mode in both directions — which is precisely what the user challenged.

### Finding 5 — incidental, unrelated to the above (resolved)

[.github/instructions/plan-execution.instructions.md](.github/instructions/plan-execution.instructions.md) carried two `U+FFFD` replacement characters in its References section: one bullet had **lost** its `📘` marker, another carried a **stray** `U+FFFD` immediately before an intact `📘` — both shapes of the emoji-corruption class this workspace has hit before.

Repaired on 2026-08-18; all four reference markers verified present, replacement-character count zero, original BOM state preserved.

## 🧱 Proposed target architecture

Stated as a proposal because § Open decisions gates it.

| Layer | Artifact | Why this layer |
|---|---|---|
| Model and rationale | `.copilot/context/05.00-content-classification/` | semantic search; explains *why*, never restates rules |
| Always-on rules | `.github/instructions/content-classification.instructions.md` | `applyTo: '**/*.md'` — cannot be forgotten |
| Discoverable procedure | **new** `.github/skills/content-classification/SKILL.md` | AI-discovered, so it reaches artifacts nobody wired; carries checklists and the verification script |
| Guaranteed inline procedure | `.github/prompt-snippets/content-classification-and-split.md` | retained only for the highest-risk prompts, where discovery must not be relied on |
| Declaration, at any level | folder `metadata.yml` | one mechanism the repository already has; hierarchical by construction |

**One metadata mechanism, resolved hierarchically.** A `metadata.yml` at any level may declare the peer and the classification policy; the nearest declaration wins, so a subtree can name a different peer or opt out entirely, and the outermost declaration serves as the repository default. `repository.metadata.yml` is retired rather than maintained in parallel.

Two enabling changes fall out of Findings 1 and 2, and both stand on their own merits:

- **Replace the flat regex scanner with a real YAML parser** so nested structure is representable and nav keys cannot be captured by accident.
- **Add a non-public attribute marking** to `metadata.yml`, and make the raw-content path serve the public projection instead of the verbatim file.

Until both land, a declaration in `metadata.yml` would be parsed as loose top-level keys and served in full — so sequencing matters, and the parser change is the first domino.

## ❓ Open decisions

Each carries an evidence-backed recommendation. Answering all three closes the gate and promotes this plan to `actionable`.

### D1 — How far up does the declaration reach?

Gates: every artifact that resolves a companion destination.

The mechanism is settled — folder `metadata.yml`, hierarchical, with `repository.metadata.yml` retired. What remains open is **coverage outside the content root**.

Classification applies to every authored Markdown file, including artifacts under `.github/` and `.copilot/` that sit outside `src/docs`. A declaration living in the content tree does not naturally reach them.

| Option | Assessment |
|---|---|
| Declaration at `src/docs/metadata.yml` only | Clean, but files outside the content root inherit nothing and fall back to a hard-coded default |
| Declaration also at repository root (`metadata.yml`) | Covers everything; the root file is not content, so it is neither staged nor served |

**Question:** should the outermost declaration sit at the repository root so non-content Markdown inherits it too, or is a content-root declaration plus a documented default sufficient?

### D2 — Snippet, skill, or both?

Gates: how the procedure reaches ~170 artifacts across both repositories.

Recommendation: **promote the procedure to a skill**, keep the snippet only where a prompt must guarantee execution without relying on discovery. Editing every artifact individually is rejected — it does not scale and every new artifact would reopen the gap.

**Question:** confirm skill-first, and confirm whether the snippet is retained for the issue prompt or removed entirely.

### D3 — How is private visibility determined, and what happens when it is unknown?

Gates: the safety property the user challenged.

Recommendation, in order:

1. Declaration in `repository.metadata.yml` wins when present.
2. Otherwise probe the actual remote (`gh repo view --json visibility`) — the authoritative answer, not a guess.
3. If still undetermined: **stop and ask.** Never silently split, never silently expose.

Plus an explicit precedence rule: in a private repository, **E outranks the split** — classification must never reduce readability or completeness there.

**Question:** approve the probe step (it requires `gh` availability), and confirm that "ask when undetermined" replaces the current silent default-to-public.

## 🔭 Discovery

Undecidable until execution; each carries a negative branch.

- **Whether `gh` is available and authenticated in the execution environment.** → If absent, fall back to declaration-only and treat undetermined as *ask*, never as *assume*.
- **Whether the Learn repository's ~158 prompts and agents include content-producing artifacts beyond the issue prompt.** → If they do, they are covered by the skill and instruction layers without individual edits; if any bypass both, list them in a sibling plan rather than expanding this one.
- **Whether any existing `metadata.yml` relies on the loose scanner's tolerance** — surrounding `---` fences, duplicate keys, or values a strict parser would reject. → If any file fails under real YAML, fix the file rather than weakening the parser; the scanner's permissiveness is not a contract worth preserving.

## 🅿️ Park lot

- **Repair the two U+FFFD characters in `plan-execution.instructions.md`** (Finding 5) → `→ closed: repaired 2026-08-18`.
- **The live App Service hostname and managed-identity GUID committed in `01-smartdocs-web-convergence.plan.md` at `08c6e0e`** → `→ defer` — owner's call, explicitly parked; not revisited by this plan.
- **Runtime authorization for internal content** (`ContentPathCacheKey` has no identity dimension; `/_content-raw` has no auth) → `→ sibling plan` — this is application work, not artifact work.
- **Extending the publish workflow to stage the union of both repositories** → `→ sibling plan` — depends on the authorization work above.
- **Plan files are PE artifacts but carry no metadata contract** — `00.03-metadata-contracts.md` omits plans from its artifact enumeration and type table, and no `pe-plan-files.instructions.md` exists → `→ sibling plan` — governance of the plan artifact type is a separate concern from content classification.

## 📌 Exit criteria

This plan is `done` when:

- a content-producing artifact that nobody explicitly wired still applies the classification rules,
- a private repository with no declaration produces whole, readable documents and never a silent split,
- the declaration uses one mechanism resolved hierarchically, with no parallel file to keep in sync,
- folder metadata parses as real YAML, so nesting is representable and no nested key can be mistaken for a navigation override,
- and an attribute marked non-public in `metadata.yml` is absent from what `/_content-raw` serves.

<!--
plan_metadata:
  version: "0.2.0"
  created: "2026-08-18"
  last_updated: "2026-08-18"
-->
