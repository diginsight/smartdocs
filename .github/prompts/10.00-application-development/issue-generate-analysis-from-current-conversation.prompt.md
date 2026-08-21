---
name: issue-generate-analysis
description: Generate analysis from current conversation
agent: agent
model: claude-opus-4.6
tools: ['codebase', 'fetch']
argument-hint: 'topic="Your Article Topic" outline="key points to cover"'
---

# Generate analysis from current conversation

## Goal
Generate a comprehensive issue analysis from the current conversation, split into a **public document** and — when the repository is public — an **internal companion** carrying the identifying detail.

**Canonical issue-folder path:** `src/docs/90.00-issues/` is the repository's fixed work-item root. All generated issue analyses MUST live under that root using the path pattern `src/docs/90.00-issues/<YYYYMM>/<YYYYMMDD>.<NN>-<kebab-slug>/`. Do not invent alternate roots such as `90.00-issue`, `issues/`, or a date-only folder. The `90.00-issues` name is the canonical namespace and `90.00-` remains the stable prefix for the issue area. A single-page analysis is still a folder with one page, so a follow-up page can be added without renaming anything.

## Instructions

### 0. Classify before writing — MANDATORY

Run 📖 `.github/prompt-snippets/content-classification-and-split.md` in full **before** creating any file.

That procedure resolves `repository.metadata.yml`, decides whether a split is needed at all, resolves and reads the internal peer, and fixes the role names this analysis will use. It is not a review step afterwards — an identifier written into a public draft has already been written.

**Do not proceed to step 1 until it has completed or explicitly determined that `visibility: private` makes the split unnecessary.**

### 1. Analyze Current Conversation
Analyze the current conversation and identify the following information:
- **IssueTitle**: A concise, descriptive title about the issue identified in the current conversation
- **DatePrefix**: The current date in the format `YYYYMMDD` (e.g., `20251107`)
- **Author**: The current user name (e.g., "Dario Airoldi")
- **Severity**: Assess the severity level (Low/Medium/High/Critical)
- **Component**: Identify the affected component or project
- **Framework**: Determine the target framework version

### 2. Read and Understand Template Structure
Read the template file located at:
`.github/templates/01.00-article-writing/issue.template.md`

Understand the enhanced structure including:
- **Header with metadata** (Date, Author, Status, Severity, Component, Framework)
- **Table of Contents** with emoji navigation
- **Comprehensive sections** with detailed subsections
- **Modern formatting** with tables, code blocks, and checklists

### 2.5. Sweep the conversation for signals — MANDATORY

Run 📖 `.github/skills/signal-capture/SKILL.md` against the conversation **before** the split.

The split that follows asks the conversation *what happened*. It never asks *what else the conversation revealed*, so anything outside the issue is lost the moment the payload rolls over. The sweep is the only step that asks. Run all seven of its questions; do not substitute an impression that there was nothing.

The skill owns the record shape, the kinds, the priority derivation and the page split. This prompt owns only the timing: **before** step 3, so the pages it produces can take their ordinals in the same pass.

### 3. Create New Issue Document

Create the analysis folder:
`src/docs/90.00-issues/<YYYYMM>/<YYYYMMDD>.<NN>-<kebab-slug>/`

Inside it, create read-ordered pages. Order by how they should be read, not by when they were written — index first, then incidents in time order, then any standing reference:

| Page | Purpose |
|---|---|
| `01-overview.md` | entry point: what happened, how the parts relate, what generalises |
| `02-…`, `03-…` | one page per incident or theme, in order |
| `NN-signals.md` | signals from step 2.5 that are relevant **or** actionable, in priority order |
| `NN-other-signals.md` | the less defined remainder |
| `NN-…-reference.md` | standing reference material, last |

The two signal pages take the next free ordinals **after** the case pages and **before** the standing reference page, and are created from `.github/templates/01.00-article-writing/signals.template.md`. Omit either page when the sweep produced nothing for it — an empty signals page is noise, but a missing report is not: step 6 states the absence explicitly.

When the repository is public, each page gets a companion at the **same relative path** in the internal peer, named `<same-name>.internal.md`. Write the companion **first**.

The public page points at its companion from its **References section**, as a backticked path under an `### Internal sources` subheading — never from a heading, the table of contents, or an early section. Most readers of a public analysis cannot open the peer; for them a prominent pointer is a closed door that makes the analysis read as an abridgement. Where several pages have companions, the per-page table goes in the References section of `01-overview.md` only.

In the body, at most **one short note** — typically that resources are named by role — and only where it changes how a passage reads.

### 4. Fill Content from Conversation Analysis
Analyze the current debugging conversation and fill ALL sections of the issue report:

#### Required Sections to Complete:
- **📝 DESCRIPTION**: Brief description, error messages, and impact points
- **🔍 CONTEXT INFORMATION**: Environment details, exception details, call stack, variable values
- **🔬 ANALYSIS**: Root cause analysis, impact assessment, affected workflows
- **🔄 REPRODUCTION STEPS**: Step-by-step reproduction and affected code locations
- **✅ SOLUTION IMPLEMENTED**: Fix overview, code changes, solution features (if solution was discussed)
- **📚 ADDITIONAL INFORMATION**: Testing recommendations, migration considerations, performance impact
- **✔️ RESOLUTION STATUS**: Current status, verification checklist, follow-up actions
- **🎓 LESSONS LEARNED**: What went wrong/right, improvements for future
- **📎 APPENDIX**: Additional reference materials and examples

#### Content Guidelines:
- Use **emojis** in section headers for visual appeal
- Include **comprehensive tables** for structured data
- Add **code snippets** with proper syntax highlighting
- Use **checkboxes** for actionable items
- Include **links and references** where applicable
- Maintain **professional technical writing** style

### 5. Quality Assurance
Ensure the generated document:
- ✅ Follows the exact template structure
- ✅ Includes Table of Contents with proper anchor links
- ✅ Contains all emoji headers as specified
- ✅ Has comprehensive content in each section
- ✅ Uses consistent formatting throughout
- ✅ Includes actionable follow-up items
- ✅ Provides clear reproduction steps
- ✅ Documents lessons learned for future prevention

And, for every captured signal:
- ✅ Declares `kind`, `relevance`, `actionability`, `target` and `state`
- ✅ Carries **no execution steps** — those belong to the context that executes it
- ✅ Names an existing landing, or states "none found" after actually looking
- ✅ Reads as a complete work item without the conversation or this work item's folder
- ✅ Sits on the page and in the position its relevance and actionability dictate, not where judgement put it

And, when the repository is public, that the split holds:
- ✅ Zero sensitive values in any public page — scan, do not assume
- ✅ Every companion written at the path-parallel location and **complete**, not a list of redactions
- ✅ Every alias used publicly resolves in the alias registry
- ✅ Every companion pointer sits in a **References section**, not in a heading, the table of contents, or ahead of the analysis
- ✅ **Reader independence**: each public page reads as a complete account to someone with no peer access — read it straight through and check it never gestures at what it is not saying
- ✅ Screenshots checked for in-image disclosure (address bars, window titles, terminal prompts)
- ✅ **Correction test**: fixing an internal identifier would require no edit to any public page

### 6. Report the split and the signals

State which pages are public, which are internal, where the internal ones were written, and any fact classified sensitive that a reader might have expected to find publicly.

Then state the signals captured, per page, with their targets — or explicitly state that the sweep found none. Silence is indistinguishable from not having run the sweep.

<!--
prompt_metadata:
  version: "1.2.0"
  last_updated: "2026-08-21"
-->
