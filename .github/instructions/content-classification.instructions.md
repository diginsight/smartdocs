---
description: Classification rules for every authored Markdown file — when content must be split into a public document and an internal companion, how the companion is named and located, and the repository-visibility conditional that switches the split off
applyTo: '**/*.md'
version: "1.0.0"
last_updated: "2026-08-18"
domain: "content-classification"
context_dependencies:
  - ".copilot/context/05.00-content-classification/"
---

# Content classification instructions

## Purpose

Ensure that no authored Markdown file discloses sensitive material, and that the detail withheld from a public document is preserved in full in its internal companion rather than lost.

## Scope and precedence

- These rules apply to **every** authored Markdown file, including files in intermediate folders such as `_validation/`, `_analysis/` and `_evidence/`, which are versioned in the public repository.
- `repository-docs.instructions.md` layers additional rules on generated pages and dossiers under `src/docs/`. Where both apply, both apply; neither relaxes the other.
- These rules do NOT apply when `repository.metadata.yml` declares `visibility: private`. See the conditional below.

## Rule 0 — read the repository declaration first

- An artifact that creates or updates a document MUST read `repository.metadata.yml` **before writing**.
- When `visibility: private`, the split MUST be skipped entirely: real names, one document, no companion, no sync.
- When the file is absent, or `visibility` is missing or unrecognised, the repository MUST be treated as **`public`**. It MUST NEVER be assumed private.

## Rules — public documents

- A sensitive value MUST NEVER be written into a public document, at any point, including as a placeholder to be removed later.
- A public document MUST name resources by **role** (`the docs app service`). It MUST NEVER use opaque tokens (`RESOURCE_1`) — an unreadable document cannot be reviewed.
- A value listed under `deliberately_public` in `repository.metadata.yml` MUST NOT be aliased.
- A public document MUST NEVER quote content from an internal companion.
- A public document MAY state that a companion exists, what it adds, and its path. Until the rendering application enforces authorization, that pointer MUST be written as a **backticked path, not a Markdown link**.
- When in doubt, the fact MUST be classified sensitive.

## Rules — internal companions

- An internal companion MUST be named for its public sibling with `.internal.md` replacing `.md`.
- It MUST be written at the **same repository-relative path** in the internal peer declared by `repository.metadata.yml`.
- It MUST carry `classification: internal` and `publish: false` in its metadata. Neither is an access control; both are declarations.
- It MUST be **complete**, not a list of redactions: real identifiers, the as-built diagrams, the commands as executed, and the date any state was verified.
- A correction to an earlier internal statement MUST be recorded **as a correction**, never applied silently.
- Non-Markdown internal assets MUST be placed in an `_internal/` folder.

## Rules — ordering and failure

- The internal companion MUST be written **before** its public sibling. A partial failure must never leave internal facts stranded in the public tree.
- Before modifying a document that may already have a companion, the artifact MUST fetch the existing companion from the internal peer.
- A failed fetch MUST abort the task. It MUST NEVER be treated as "no companion exists" and MUST NEVER degrade into writing a new one from scratch.
- Where local and remote companions diverge and neither is an ancestor of the other, the artifact MUST stop and surface the divergence rather than overwrite.

## Rules — captures and intermediate folders

- A screenshot or captured asset MUST be checked for sensitive material **in the image**, including browser address bars, window titles and terminal prompts.
- An image that cannot be made safe MUST be re-captured or cropped. It MUST NEVER be published on the assumption that it will not be read.
- A capture that cannot be made safe MUST be placed in `_internal/`.

## Quality checklist

- [ ] `repository.metadata.yml` read before writing; `visibility` honoured
- [ ] No sensitive value present in any public file, at any revision of this change
- [ ] Resources named by role, readably
- [ ] Internal companion written first, at the path-parallel location, and complete
- [ ] Companion pointer present in the public document as a backticked path
- [ ] Existing companion fetched before modification, or the task aborted
- [ ] Captured images checked for in-image disclosure

## References

- **📖** `.copilot/context/05.00-content-classification/00-classification-and-split-model.md` — the model, the taxonomy and the reasoning
- **📖** `.github/prompt-snippets/content-classification-and-split.md` — the procedure to run
- **📖** `.github/instructions/repository-docs.instructions.md` — additional rules for generated pages and dossiers
