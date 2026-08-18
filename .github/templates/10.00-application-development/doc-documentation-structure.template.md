---
description: Scaffold for the generated documentation tree under src/docs — chapter folders, metadata.yml shape and the component pivot
domain: "application-development"
---

# Documentation structure scaffold

**Audience**: agent. Produce the folder tree, not prose.

## Tree

```text
src/docs/
├── _evidence/                      # publish: false — never linked
│   ├── _discovery.md
│   ├── _run-state.md
│   └── [component-id]/
│       ├── code.md
│       ├── code.internal.md        # only when sensitive facts exist
│       ├── data.md
│       ├── configuration.md
│       ├── environment.md
│       ├── devops.md
│       └── security.md
├── index.md                        # Home (chapter 1) — the space root page, NOT a folder
├── 02.00-getting-started/          # metadata.yml → label: Getting Started,  order: 2
├── 03.00-architecture/             # metadata.yml → label: Architecture,     order: 3
├── 04.00-use-cases/                # metadata.yml → label: Use Cases,        order: 4
├── 05.00-infrastructure/           # metadata.yml → label: Infrastructure,   order: 5
├── 06.00-reference/                # metadata.yml → label: Reference,        order: 6
├── 07.00-other-components/         # metadata.yml → label: Other Components, order: 7
├── 08.00-validation/               # metadata.yml → label: Validation,       order: 8
├── 09.00-security/                 # metadata.yml → label: Security,         order: 9
├── 10.00-devops/                   # metadata.yml → label: DevOps,           order: 10
└── 11.00-appendix/                 # metadata.yml → label: Appendix,         order: 11
```

## Rules

- **Name every new chapter folder `NN.00-<kebab-name>`**, where `NN` is the chapter's zero-padded order — `02.00-getting-started` … `11.00-appendix`. The **prefix carries the order**; `metadata.yml` only confirms it.
- **A chapter folder without a numeric prefix is a defect.** `NavRules.SortKey` puts an unprefixed name in the alphabetical group, so the chapter sequence collapses to `appendix, architecture, devops, …` the moment `metadata.yml` is missing, misspelt or unparsed — and it fails silently, with no error anywhere.
- **Before creating a chapter folder, list `src/docs/` and check whether a folder already serves that chapter.** If one does, reuse it by adding `metadata.yml` — NEVER create a second folder for the same chapter beside it.
- An existing folder that holds content or has inbound links MUST be reused, NEVER renamed. An empty, untracked placeholder folder is not protected by this rule.
- A folder outside the eleven keeps its own `metadata.yml` and is NEVER a placement target.
- Every chapter folder MUST contain an overview page, even when the chapter is otherwise empty.

## `metadata.yml`

```yaml
label: "[chapter name]"
short: "[abbreviated label for narrow navigation]"
icon: "[icon token]"
order: [1-11]
```

## Component pivot

Add one subfolder per relevant component **only when two or more components are relevant to that chapter**.

```text
[chapter-folder]/
├── index.md                        # chapter overview — written last
├── [component-id-a]/
│   └── [page].md
└── [component-id-b]/
    └── [page].md
```

Single relevant component → pages sit directly in the chapter folder.

## References

- **📖** `.copilot/context/10.00-application-development/04-documentation-structure.md` — chapters, placement, tie-breakers
- **📖** `.copilot/context/10.00-application-development/01-discovery-model.md` — layout mode and priority

<!--
---
template_metadata:
  version: "1.0.0"
  last_updated: "2026-08-16"
  created: "2026-08-16"
  consumers:
    - "ad-documentation-manager"
    - "ad-documentation-author"
    - "01.02-ad-docs-write"
  changes:
    - "v1.0.0: Initial creation"
---
-->
