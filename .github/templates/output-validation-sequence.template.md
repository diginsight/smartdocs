# Validation Sequence Output Template

**Purpose:** Required shape of the artifact that records a visible-browser validation run.

**Referenced by:** `testing-validation.instructions.md`

**Audience:** Agent — produce this structure exactly. Nothing here is repository-specific; the consuming instruction file supplies the URL, build command and storage location.

---

## Frontmatter

```yaml
---
title: "Validation sequence — [short subject]"
type: validation-sequence
date: "[yyyy-mm-dd]"
publish: false
target:
  area: "[what area or behavior was validated]"
  change: "[one-line description of the change]"
  files: [ "[path]", "..." ]
environment:
  url: "[app URL used]"
  build: "[build command + result]"
  browser: "visible browser window (headed)"
result: PASS | FAIL
---
```

`publish: false` is mandatory — the artifact is a working record and MUST NEVER be wired into site navigation.

---

## Required body sections

### 1. Opening statement

One paragraph naming the behavior under validation and what a passing run proves.

### 2. Environment table

| Item | Value |
|---|---|
| URL | [app URL] |
| Build | [command] → [result] |
| Run | [how the server was started] |
| Browser | Visible headed window, viewport [w×h] |
| Date | [yyyy-mm-dd] |

### 3. Sequence and results table

One row per scenario. The `Observed` column MUST carry the exact on-screen value, not a paraphrase.

| # | Precondition | Action | Expected | Observed | Result |
|---|---|---|---|---|---|
| 1 | [starting state] | [what was done] | [what should happen] | [exact observed value] | PASS \| FAIL |

### 4. Evidence

**Preferred — per-step images in a two-column table.** Left column describes the step, right column carries its screenshot. One row per step.

| Step | Screenshot |
|---|---|
| **Step N** — [what this image proves] | `![descriptive alt text](images/NN-slug.png)` |

Alt text MUST describe what is visible, not repeat the caption.

**Alternative — a short recording.** Use only when the behavior is motion-based and the clip is short. Store alongside the images and link it.

**Console-only evidence.** When a scenario produces no rendered page (a startup guard, a fail-closed check), quote the output in a fenced block and say why no screenshot exists.

### 5. Notes

Caveats a reader needs to trust the run: responsive/viewport quirks, timing or settling behavior, values that are lower bounds rather than finals, and an explicit list of what this run did **not** cover.

---

## Naming

| Artifact | Pattern | Example |
|---|---|---|
| Markdown | `[yyyymmdd].[NN]-validation-sequence.md` | `20260817.01-validation-sequence.md` |
| Images | `NN-[short-slug].png` | `01-home-shell.png` |

The reverse-date prefix orders runs chronologically; the `NN` sequence allows repeat rounds against the same work item.
