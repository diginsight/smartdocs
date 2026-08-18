---
description: Enforcement rules for repository-derived documentation under src/docs — verification stamps, source-set declarations, traceability anchors, chapter membership and evidence containment
applyTo: 'src/docs/**'
version: "1.0.0"
last_updated: "2026-08-16"
domain: "application-development"
context_dependencies:
  - ".copilot/context/10.00-application-development/"
---

# Repository documentation instructions

## Purpose

Enforce the invariants that MUST hold for **generated documentation pages** and **evidence files** under `src/docs/`, so that a page can always be traced to the evidence it was built from and evidence never leaks into published content.

## Scope and precedence

- This file layers on the shared baseline `documentation.instructions.md`, which governs Markdown structure, metadata and reference classification for all Markdown. The baseline's rules apply first; the rules below add to them and NEVER restate them.
- **Baseline carve-out for generated pages.** A generated page's section set is fixed by its bound template, so the baseline's mandatory *Conclusion*, *References* and bottom validation-metadata block do NOT apply to it. A generated page carries a `verification_stamp` in place of validation metadata, and carries traceability anchors in place of a references section. Every other baseline rule — heading emoji, kebab-case naming, encoding, reference classification where references do appear — applies unchanged.
- **The carve-out is exhaustive.** Those three sections are the *only* baseline exemptions. In particular the baseline's **table of contents** (required above 500 words) and **introduction** requirements apply to generated pages unchanged. A template's lead orientation section — `🎯 Introduction`, `🎯 Purpose`, `🎯 Goal`, `🎯 What it does`, `🎯 What it proves`, `🎯 What is protected` — satisfies the introduction requirement; a page still MUST carry `## 📚 Table of contents` once it exceeds 500 words, with one entry per H2 mirroring its emoji.
- **Reader-facing headings MUST name the subject, not the document.** A heading that describes the page's own role in the documentation set (*"What this chapter answers"*, *"What this is"*) is a defect: the reader sees a navigation tree, never a "chapter", and a heading is scanning apparatus, not scaffolding. Framework vocabulary — *derived*, *declared*, *established*, *surface* — belongs in the evidence layer and MUST NOT appear in a page heading.
- The **generated-page** and **chapter-membership** rules below apply ONLY to files that declare `source_sets:` or carry a `verification_stamp`. Hand-written articles under `src/docs/` are unaffected by those.
- The **evidence-containment** rules below carry no such carve-out: they apply to every file under `src/docs/`, generated or hand-written, and to every intermediate folder (`_evidence/`, `_validation/`, `_analysis/`). A disclosure does not care how the file was produced.
- `content-classification.instructions.md` governs the public/internal split for all Markdown repository-wide. This file adds the `src/docs/` specifics on top of it and NEVER relaxes it.
- For plan files, `plan-execution.instructions.md` and `plan-marking.instructions.md` take precedence — no conflict: those govern plan lifecycle and marking, this file governs generated documentation pages, and a plan file is never a generated documentation page.

## Rules — generated pages

- Every generated page MUST declare `source_sets:` in its frontmatter, listing role names only. Concrete paths MUST NEVER appear there.
- Every generated page MUST carry a `verification_stamp` HTML comment block.
- The `verification_stamp` and `source_sets` MUST be updated **in the same edit** that changes the page body. A body edit that leaves either stale is invalid.
- A page whose `verification_stamp` is absent MUST be treated as unverified. It MUST NEVER be reported as current.
- Every assertion MUST carry a traceability anchor `^[{area}-{nn}]` resolving to a record in a named dossier, OR sit inside an explicitly marked gap.
- An assertion that evidence does not establish MUST be marked as not established. It MUST NEVER be hedged into prose that reads as fact.
- Content marked `<!-- human-authored: preserved across regeneration -->` MUST NEVER be deleted by a regeneration. It MUST be classified first.

## Rules — chapter membership

- Chapter identity, label and icon MUST come from the folder's `metadata.yml`. They MUST NEVER be inferred from a folder or file name.
- Chapter **order** MUST be carried twice — in the folder's `NN.00-` prefix and in its `metadata.yml` `order` key — so that neither alone is load-bearing.
- Every generated page MUST carry an `NN-` filename prefix giving its position in the chapter's reading order, numbered contiguously from `01`. The chapter's own `index.md` is exempt.
- The prefix sequence MUST match the order of the chapter overview's Pages table. Page order has no `metadata.yml` equivalent — the file name is its only carrier, and an unprefixed page falls into alphabetical order with no error raised.
- An existing folder MUST NEVER be renamed to realise a chapter. Add or edit its `metadata.yml` instead.
- A page rename MUST rewrite every inbound link in the same edit.

## Rules — evidence containment

- Every file under `src/docs/_evidence/` MUST carry `publish: false`.
- Every `*.internal.md` file MUST carry `publish: false` and `classification: internal`. Neither is an access control; both are declarations.
- A published page MUST NEVER **quote** content from a `*.internal.md` file or any file under `_evidence/`.
- A published page MAY cite the **path** of its own internal companion as a signpost, stating what the companion adds. That pointer MUST be a backticked path rather than a Markdown link, because the target does not resolve in this repository. Citing the path of any OTHER internal file, or of an evidence dossier, remains forbidden in rendered content.
- The `verification_stamp` comment block is exempt: it MUST name the dossier paths the page was built from. That block is not rendered, and without the path the stamp cannot be checked against its evidence.
- A published page MUST NEVER contain a secret value, personal data, an internal hostname or endpoint, or exploit-actionable detail.
- Dossiers MUST be regenerated by their owning investigator. They MUST NEVER be hand-edited.

## Quality checklist

- [ ] `source_sets:` declared, roles only
- [ ] `verification_stamp` present and updated in the same edit as the body
- [ ] Every assertion anchored, or inside a marked gap
- [ ] Chapter membership sourced from `metadata.yml`
- [ ] Every page carries its `NN-` order prefix, contiguous from `01` and matching the chapter overview's Pages table
- [ ] `_evidence/` and `*.internal.md` carry `publish: false` and are unlinked from rendered content (the `verification_stamp` block is exempt)
- [ ] No secret, personal data, internal surface or exploit detail published

## References

- **📖** `.copilot/context/10.00-application-development/05-source-sets-and-propagation.md` — role names, anchor format, stamp contents
- **📖** `.copilot/context/10.00-application-development/02-evidence-dossier-schema.md` — dossier location, record shape, internal split
- **📖** `.copilot/context/10.00-application-development/04-documentation-structure.md` — chapters and `metadata.yml` mapping
- **📖** `.copilot/context/10.00-application-development/08-verification-gates.md` — the gates that enforce these rules during a run
- **📖** `documentation.instructions.md` — the shared Markdown baseline this file layers on
