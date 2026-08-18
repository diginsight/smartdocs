---
description: Enforces that every runtime/UI change to a web application is validated in a visible browser and recorded as a validation-sequence artifact with screenshots
applyTo: 'src/*Web*/**,**/*.razor,**/*.razor.cs,**/*.razor.css,**/wwwroot/**'
version: "2.0.0"
last_updated: "2026-08-17"
domain: "testing-validation"
goal: "Make every runtime/UI change verifiable by a human reviewer who was not present for the run"
rationales:
  - "A change that compiles and serves is not a change that behaves correctly — only an observed run proves behavior"
  - "A hidden browser produces evidence nobody watched, which is indistinguishable from no evidence"
  - "Path-name coupling in applyTo breaks silently on rename, so the scope is anchored on project shape and file type rather than on a product name"
---

# Testing & Validation Rules

## Purpose

Every runtime or UI change MUST be proven by an observed run and MUST leave behind a reviewable record. These rules are repository-agnostic; the single repository-specific table at the end supplies the bindings.

## When these rules apply

MUST apply whenever a change alters **runtime behavior or UI** — components, layout, navigation, rendering, startup wiring, endpoints, client interactivity, or styling that changes behavior.

MUST NOT apply to pure documentation edits or to tooling that does not run in the application.

## Rules — running the validation

- ALWAYS rebuild before validating. NEVER pass `--no-build`; a stale client bundle invalidates the entire run.
- ALWAYS run the server in a **visible foreground console** the user can watch and stop with Ctrl+C. NEVER a hidden or background process.
- ALWAYS open a **visible browser window**. NEVER use a hidden, embedded or background browser surface as the validation surface. A headed automation window is acceptable; a headless page is not.
- ALWAYS reproduce each scenario end-to-end and read the **live DOM value** of the element under test, so the recorded observation is exact rather than inferred from a screenshot.

## Rules — what to record

For every scenario the artifact MUST carry: precondition, action, expected result, exact observed result, and a screenshot of the validated state.

- Screenshots MUST show the element under test in its validated state.
- Alt text MUST describe what is visible.
- The artifact MUST state what the run did **not** cover.

📖 **Required artifact shape:** `.github/templates/output-validation-sequence.template.md`

## Rules — where to store

- MUST live in a `_validation/` subfolder **inside the work item's own folder**, beside the issue or use-case it validates. Images in `_validation/images/`.
- MUST be marked `publish: false`. NEVER wire a `_validation/` artifact into site navigation or render configuration.
- MUST NOT be placed in the validator agent's catalog folder — per-issue validation and catalog validation are separate concerns.

## Never do

- NEVER treat compilation, `Invoke-WebRequest` or `curl` as validation of a UI/behavior change — they prove the app serves, not that it behaves.
- NEVER declare a task complete before the artifact exists with every scenario marked PASS.
- NEVER record an observation you did not watch happen.

## Repository bindings

The only repository-specific values. Replace this table when porting these rules elsewhere; everything above transfers unchanged.

| Binding | Value |
|---|---|
| Application | `Diginsight.SmartDocs.Web` (+ `.Client`, `.Shared`) |
| Build | `dotnet build src/Diginsight.SmartDocs.slnx -c Debug` |
| Run | `dotnet run --launch-profile http` from the web project |
| URL | `http://localhost:5280/` |
| Work-item root | `src/docs/90.00-issues/<yyyymm>/<yyyymmdd>.NN-<slug>/` |
| Catalog folder (off-limits) | `src/docs/95. Validations/` |
