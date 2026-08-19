---
title: "Documentation structure — chapters, placement and page shapes"
description: "The canonical eleven-chapter set, the component pivot rule, the page-shape catalogue that decides the template count, major-versus-minor placement and the mapping onto existing folders via metadata.yml"
domain: "application-development"
goal: "Make placement deterministic — given a component and a fact, exactly one chapter and exactly one page shape are correct, so two runs over the same repository place the same fact identically"
scope:
  covers:
    - "The eleven canonical chapters"
    - "Component pivot rule below a chapter"
    - "Page-shape catalogue and its template binding"
    - "Major versus minor placement by component priority"
    - "Mapping chapters onto existing folders through metadata.yml"
    - "Chapter folder naming and the numeric prefix that carries the order"
    - "Page file naming and the numeric prefix that carries the reading order"
    - "Placement tie-breakers"
  excludes:
    - "How a page is written once placed (see 07-documentation-authoring-criteria.md)"
    - "Component priority definitions (see 01-discovery-model.md)"
    - "Template bodies (see .github/templates/10.00-application-development/)"
boundaries:
  - "The chapter set is FIXED — a stream MUST NOT invent, merge or rename a chapter"
  - "NEVER rename an existing folder to realise a chapter — use metadata.yml"
  - "NEVER create a new chapter folder without its NN.00- numeric prefix"
  - "NEVER create a page file without its NN- numeric prefix — the chapter's own index.md is the only exception"
  - "NEVER leave the page prefix sequence disagreeing with the chapter overview's Pages table"
  - "NEVER create a chapter folder before listing src/docs/ to check whether one already serves that chapter"
  - "Every page shape MUST bind to exactly one template, and every template MUST be bound by at least one shape"
rationales:
  - "A fixed chapter set is what lets a reader move between two documented repositories without relearning the layout"
  - "Deriving the template count from page shapes rather than chapters prevents eleven near-identical templates or one template stretched over incompatible content"
  - "Realising chapters through metadata.yml keeps existing folder names and their inbound links intact"
  - "Carrying the order in the folder name makes the chapter sequence survive a lost or mistyped metadata.yml, which otherwise degrades to alphabetical order silently"
  - "Pages have no metadata.yml equivalent at all, so the file name is the only carrier of reading order — without a prefix a chapter presents itself alphabetically, which routinely puts its overview page last"
---

# Documentation structure

**Purpose**: The canonical chapter set, placement rules and page-shape catalogue for repository-derived documentation.

**Referenced by**:
- `ad-documentation-manager.agent.md`, `ad-documentation-author.agent.md`, `ad-documentation-verifier.agent.md`
- `.github/prompts/10.00-application-development/01.02-ad-docs-write.prompt.md`
- `.github/templates/10.00-application-development/doc-documentation-structure.template.md`

---

## 📚 The eleven chapters

Fixed and always present. A chapter with nothing to say carries its overview page stating that, rather than being omitted.

| # | Chapter | Answers |
|---|---|---|
| 1 | **Home** | what this repository is, in one page |
| 2 | **Getting Started** | how a newcomer builds and runs it |
| 3 | **Architecture** | how it is structured and why |
| 4 | **Use Cases** | what an actor can accomplish with it |
| 5 | **Infrastructure** | what is provisioned, per environment |
| 6 | **Reference** | the precise surface — types, keys, operations, tables |
| 7 | **Other Components** | 🟡 Tooling and ⚪ Peripheral components |
| 8 | **Validation** | what is tested and what that proves |
| 9 | **Security** | the observable security posture |
| 10 | **DevOps** | how it is built, gated and shipped |
| 11 | **Appendix** | glossary, decision record, superseded material |

---

## 🧭 Component pivot

Below a chapter, subfolders are **component-pivoted** when two or more components are relevant to that chapter, and **flat** otherwise. The decision is per chapter, not per repository — a repository may pivot Reference while keeping Getting Started flat.

---

## 🧩 Page-shape catalogue

The authority that decides the template count. Every shape binds to exactly one template.

| Page shape | Template | Used by chapters |
|---|---|---|
| Chapter overview | `doc-chapter-overview.template.md` | all eleven, including Home and Getting Started |
| System and logical architecture | `doc-architecture-page.template.md` | Architecture |
| Reference entry | `doc-reference-entry.template.md` | Reference |
| API unit | `doc-api-unit.template.md` | Reference |
| Use case | `doc-use-case.template.md` | Use Cases |
| Environment | `doc-infrastructure-environment.template.md` | Infrastructure |
| Security posture | `doc-security-posture.template.md` | Security |
| Security control family | `doc-security-control-family.template.md` | Security |
| Security requirement | `doc-security-requirement.template.md` | Security |
| Security requirement index | `doc-security-requirement-index.template.md` | Security |
| Pipeline | `doc-devops-pipeline.template.md` | DevOps |
| Validation unit | `doc-validation-unit.template.md` | Validation |
| Minor component | `doc-component-minor.template.md` | Other Components, Appendix |
| Artifact family | `doc-artifact-family.template.md` | Other Components, Appendix |

Three further templates are **structural** rather than page shapes — `doc-documentation-structure.template.md`, `doc-evidence-dossier.template.md`, `doc-mermaid-patterns.template.md` — and one belongs to the robustness stream, `finding-record.template.md`.

> The four **Security** shapes above are conditional together: they are produced only when the repository declares an assessment catalogue (📖 `12-security-assessment-model.md`). Absent a catalogue, Security carries its overview and posture pages only. Where a catalogue is declared, the control-family, requirement-index and requirement shapes are produced for **every dimension it declares** — covering one declared dimension and not another is a recorded non-conformance, never a silent omission.

> **Artifact family** is conditional in the same way: it is produced only where discovery derived artifact families (📖 `01-discovery-model.md`). It exists because the minor-component shape is code-shaped — *Deployed*, *What it does*, *Dependencies* — and has no slot for the facts that describe an artifact family: the names it is invoked by, the order its parts run in, what they bind to, and what they emit. A 🔴 Core or 🟠 Supporting family does **not** use this shape; it is documented in the main chapters with the ordinary shapes above, like any other major component.

---

## 🔴 Major versus minor placement

| Priority | Documented in |
|---|---|
| 🔴 Core, 🟠 Supporting | the main chapters — Architecture, Use Cases, Reference, Validation, Security, DevOps, Infrastructure |
| 🟡 Tooling, ⚪ Peripheral | *Other Components* and the Appendix, and nowhere else |

A 🔴 Core component whose only page sits under *Other Components* is a **defect**, not a stylistic choice — it means discovery mis-tiered it or placement ignored the tier.

---

## 🔐 Security chapter layout

Where a catalogue is declared, Security carries more than a flat page set. Its layout follows the ordinary component-pivot rule — the `{component}` segment is present only when two or more components are relevant to the chapter, and omitted otherwise. It introduces no new layout mode.

```
security/
  overview.md
  {component}/
    overview.md
    posture.md
    posture.internal.md              # never in navigation
    control-families/
      overview.md                    # index across every declared dimension
      {family}.md
    requirements/
      overview.md                    # every requirement, grouped by family, sorted by id
      {CONTROL-ID}-{title-slug}.md   # applicable requirements only
```

Two rules travel with this layout and are stated in full in `12-security-assessment-model.md`: a requirement page is **never** named by its control id alone, because two dimensions routinely reuse an id for unrelated controls; and families from different dimensions are **peers**, never nested beneath a node marking their origin.

The reverse is equally a defect: a build script promoted into Architecture inflates the apparent system and buries the components that matter.

---

## 🗺️ Mapping onto existing folders

Chapters are realised through per-folder `metadata.yml`, so **existing folders keep their names** and their inbound links.

| Key | Effect |
|---|---|
| `label` | the chapter name shown in navigation |
| `order` | position in the chapter sequence — **confirms** the folder's numeric prefix, and **supplies** it for a folder that cannot be renamed |
| `icon` | the chapter's navigation icon |
| `hidden` | excludes a folder from navigation without deleting it |

### Worked example — this repository

`src/docs/` holds one content folder outside the chapter set.

| Existing folder | `metadata.yml` | Result |
|---|---|---|
| `90.00-issues/` | `label: Issues`, `order: 90` | **not** one of the eleven — working documents (plans, investigations) that sit outside the chapter set; the `90` prefix keeps it last |

The numeric prefix is not a chapter privilege: **every** folder under `src/docs/` carries one, chapter or not. `metadata.yml` supplies the display label, so the prefix never has to read well.

### Naming a new chapter folder

A chapter that has no existing folder is created as `NN.00-<kebab-name>`, where `NN` is the chapter's zero-padded order:

`02.00-getting-started` · `03.00-architecture` · `04.00-use-cases` · `05.00-infrastructure` · `06.00-reference` · `07.00-other-components` · `08.00-validation` · `09.00-security` · `10.00-devops` · `11.00-appendix`

Chapter 1 (Home) is the space root page `src/docs/index.md`, not a folder.

**The prefix carries the order; `metadata.yml` only confirms it.** `NavRules.SortKey` sorts a numeric-prefixed name in the explicit-order group and an unprefixed name in the alphabetical group. A prefixed folder therefore holds its position even if `metadata.yml` is absent, misspelt or unparsed; an unprefixed one collapses to `appendix, architecture, devops, getting-started, …` — wrong, and with no error raised anywhere. Carry the order in both places so neither alone is load-bearing.

**Before creating any chapter folder, list `src/docs/` and check whether a folder already serves that chapter.** Creating a second folder for a chapter that already has one is the failure this check exists to prevent — the duplicate is invisible in a diff of new files, and both folders then appear in navigation.

An existing folder that holds content or has inbound links is reused, never renamed. An empty placeholder folder that git does not track carries neither, and is not protected by that rule — replace it with the correctly prefixed name.

A folder outside the chapter set is legitimate; it simply carries no page shape and is never a placement target for generated content.

### Naming a page file

Every page inside a chapter is created as `NN-<kebab-name>.md`, where `NN` is its zero-padded position in that chapter's reading order:

`01-system-architecture.md` · `02-host-application.md` · `03-browser-client.md` · `04-shared-library.md` · `05-caching-and-invalidation.md`

The chapter's own `index.md` is never prefixed — `NavRules.IsIndexName` treats it as the folder's own page rather than a sibling.

**A page has no other way to hold its position.** `metadata.yml` orders folders; there is no equivalent for files. `DynamicNavBuilder` reads only `hidden` from a page's front matter and then sorts on the file name alone, so an unprefixed chapter collapses to alphabetical order — which is what put `system-architecture.md`, the page that opens Architecture, last behind `browser-client.md` and `caching-and-invalidation.md`.

The prefix costs nothing in presentation. The sidebar label comes from the page's `title:`, and where there is none `NavRules.Label` strips the prefix anyway, so the number is never seen by a reader — it only moves the item.

**The prefix sequence MUST match the chapter overview's Pages table**, which is where the reading order is declared in prose. The table and the file names are two renderings of one sequence; when they disagree, the sidebar contradicts the page the reader has just finished. Number contiguously from `01`, leaving no gaps.

A page added later takes the **next free number** — appending never disturbs an existing name. Inserting one mid-sequence means renaming the pages after it, which is a confirm-first action: the rename and every inbound link are rewritten in the same edit, or the set is left with a stale link.

---

## ⚖️ Placement tie-breakers

Given a component and a fact, exactly one chapter is correct. Apply in order and stop at the first that resolves.

| # | Rule |
|---|---|
| 1 | If the fact describes a **precise surface** a caller must match exactly — a type, a key, an operation, a table — it belongs to **Reference**, wherever else it is also interesting |
| 2 | If it describes **what an actor achieves**, it belongs to **Use Cases** |
| 3 | If it describes **what is provisioned or where it runs**, it belongs to **Infrastructure** |
| 4 | If it describes **how the repository is built or shipped**, it belongs to **DevOps** |
| 5 | If it describes **what is proven and how**, it belongs to **Validation** |
| 6 | If it describes **a control or an exposure**, it belongs to **Security** |
| 7 | If it describes **structure or a design rationale**, it belongs to **Architecture** |
| 8 | Otherwise it is orientation material and belongs to **Home** or **Getting Started** |

A fact that resolves to two chapters is written **once** in the chapter that owns it and **linked** from the other. Duplicating it guarantees the two copies diverge on the next update.

---

## References

- **📖** `01-discovery-model.md` — component priority and layout mode
- **📖** `12-security-assessment-model.md` — dimensions, families, requirements and the conformance rule behind the Security shapes
- **📖** `05-source-sets-and-propagation.md` — how a changed source role maps to affected pages
- **📖** `07-documentation-authoring-criteria.md` — how a page is written once placed
- **📖** `08-verification-gates.md` — navigation coverage and placement gates
- **📖** `.github/templates/10.00-application-development/` — the eighteen templates bound above

## Version history

| Version | Date | Change | Author |
|---|---|---|---|
| 1.0.0 | 2026-08-16 | Initial version | System |
| 1.1.0 | 2026-08-16 | Added the Artifact family page shape; template count fifteen to sixteen | System |
| 1.2.0 | 2026-08-16 | Added the Security requirement and Security requirement index shapes and the Security chapter layout; template count sixteen to eighteen | System |

<!--
context_metadata:
  version: "1.2.0"
  last_updated: "2026-08-16"
  created: "2026-08-16"
-->
