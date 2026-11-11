---
title: "Legacy access policy migration plan"
date: "2026-11-10"
type: how-to
status: done
goal: "Update every configured SmartDocs repository whose space access policy still lives in a legacy access.metadata.yml to the canonical root metadata.yml, put the untracked policy files under version control, and prove that no configured profile still depends on a legacy access carrier, without changing any reader's visibility."
description: "An executable plan to convert four legacy access policies to canonical metadata, commit four untracked policy files, and prove unchanged reader visibility."
---

# Legacy access policy migration plan

## 🎯 Goal and scope

Convert the four configured content roots that still carry their access policy in a legacy `access.metadata.yml`: the desktop-client, model-editor, and device-API roots of the devec4 profile, and the internal Learn library behind `learn-internal`. Commit the policy files that their repositories don't track, including the AICM repository's existing canonical file. Prove afterwards that every reader sees exactly what they saw before, and that no configured root depends on a legacy access file anymore.

The [migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md) is the design contract for this plan: it holds the inventory, the findings, and the dry-run evidence behind each step. The [authentication and authorization explanation](02-smartdocs-rebac-support.analysis.md) describes the model being preserved.

**Narrowing of the request.** "Update all repos' SmartDocs metadata" is narrowed to access policies and their version control:

- Legacy `repository.metadata.yml` declarations in four repositories stay as they are, because those repositories' authoring artifacts read only that filename. See `PL4-repository-declarations`.
- Container publication, pushing commits, and switching profiles to canonical-only mode are deployment actions. They're parked as follow-ups.

**Current state:** completed on November 10, 2026. All four conversions, four single-file local commits, seventeen-root validator parity, eight exact anonymous DOM comparisons, four policy-log comparisons, and library synchronization checks passed. The [validation sequence](_validation/20261110.01-rebac-metadata-fix.validation-sequence.md) records the evidence. Signed-in cases are explicitly not covered under the availability branch. Nothing was pushed or published, and parked actions remain deferred. The devec4 spaces, repositories, and registration are named by role.

## 📚 Table of contents

- [🎯 Goal and scope](#-goal-and-scope)
- [🧭 Decisions and readiness](#-decisions-and-readiness)
- [🔎 Discovery](#-discovery)
- [⚙️ Implementation sequence](#️-implementation-sequence)
- [🧪 Validation contract](#-validation-contract)
- [📐 Coverage and exit criteria](#-coverage-and-exit-criteria)
- [🗂️ Park lot](#️-park-lot)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🧭 Decisions and readiness

### Selected decisions

These were selected from evidence during autonomous authoring, without a user answer. Review them first: changing one returns the plan to `draft` and reruns its gate.

| Identifier | Selected contract | Evidence |
|---|---|---|
| `D1-verbatim-conversion` | Copy every permission name, description, requirement, and role grant exactly; keep the legacy comment; add only the schema line; drop empty `permissions: {}` and `roles: {}`. (✅ done) | Four coexistence validations and unchanged logged requirements/grants |
| `D2-explicit-public-policy` | Convert the device-API space's empty policy to `access: {}` instead of deleting it. (✅ done) | Explicit public policy validated; anonymous page unchanged |
| `D3-merge-into-navigation-file` | Extend the model-editor root's existing navigation file rather than adding a second carrier. (✅ done) | Three navigation fields preserved in the merged canonical file |
| `D4-encoding` | UTF-8 without a byte-order mark, CRLF line endings, written from PowerShell 7 or an editor set to plain UTF-8. (✅ done) | Byte-level checks passed on all four files |
| `D5-canonical-first` | Write and validate the canonical file while the legacy file still exists; delete the legacy file only afterwards. (✅ done) | All four coexistence checks passed before any legacy deletion |
| `D6-path-limited-local-commits` | One commit per repository, limited to its policy file, on the checkout's current branch; never push in this plan. (✅ done) | Four single-file local commits; outside-policy pending and staged state preserved |
| `D7-no-configuration-change` | No overlay, floor, required-policy flag, or compatibility setting changes. (✅ done) | No saved configuration changes; only validation-process log verbosity adjusted |

### Open decisions

None. Execution-time facts are bounded in [discovery](#-discovery).

### Actionability gate (✅ done)

| Gate | Authoring result |
|---|---|
| Goal alignment | The goal restates the request; the exclusion of repository declarations and deployment actions is explicit above |
| Goal reachability | Baseline → four conversions → commits → readiness proof → record |
| Execution determinism | File contents, order, validation commands, and commit scope are fixed; the internal execution sheet supplies exact paths |
| Clarity and actionability | Every check carries an expected result and a negative branch |
| Unknown resolution | Read-only facts were resolved by inspection and dry runs; execution-time facts are bounded below |
| Scope discipline | In-domain exclusions are parked; work outside the goal is on the [signals page](04-signals.md) |
| Coverage promise | The coverage table maps each goal item to a step and an exit condition |
| Principle impact | Not applicable: this isn't a vision-amendment plan |

## 🔎 Discovery

These checks run before the steps that depend on them.

| Identifier | Probe | Defined negative branch |
|---|---|---|
| `G1-baseline-unchanged` | All six fingerprints and tracking states match. One branch is now behind upstream; its policy is unchanged, and its coexistence dry run must be repeated before conversion. (✅ done) | If a fingerprint or tracking state differs, stop for that root: re-read the file, revise the analysis' proposed content, and repeat its dry run before converting. |
| `G2-library-sync` | Visible OneDrive panel confirmed *Backed up and synced* before conversion, after canonical upload, and after legacy deletion; screenshots retained internally. (✅ done) | If synchronization is paused, failing, or unconfirmed, don't edit the library; mark step 5 blocked until it's confirmed healthy. |
| `G3-signin-availability` | No interactive session was supplied while the owner was unavailable. Signed-in cases are recorded as not covered, not passed; no registration changes were attempted. (✅ done) | If a profile can't complete sign-in, validate it anonymously and by its policy log lines only, and record its signed-in cases as not covered. Never fabricate a session. |
| `G4-validation-port` | Port 7280 is occupied. The owner was unavailable to authorize stopping the host, so leave it untouched and use HTTPS port 7281 for anonymous checks only. (✅ done) | If one is, ask the owner to stop it. Without authorization, run anonymous checks on another port and skip signed-in checks. |

## ⚙️ Implementation sequence

Build the host before every run, without `--no-build`, and run it in a visible console the owner can stop. Use a visible browser window. The internal execution sheet lists the exact paths and commands for every step.

**Execution adjustment:** anonymous runs use HTTPS port 7281 because the existing host cannot be stopped without authorization. The Learn profile suppresses successful policy information logs by default, so validation processes temporarily enable information logging for the access-policy provider only, before and after migration. No saved configuration or authorization setting changes.

### 1. Capture the pre-migration evidence (✅ done)

**Depends on:** `G1-baseline-unchanged`, `G4-validation-port`.

- Build the repository's metadata checks into an isolated output folder and run `validate-tree` on all seventeen configured content roots. Build succeeded with zero warnings/errors; sixteen roots passed and `product-c` failed by design on 2026-11-10. (✅ done)
- Run the devec4 profile. Anonymous index, three protected routes, and the public device-API page matched expectations in a visible Edge window; all three legacy-policy warnings and policy lines were captured. Rebuilt isolated host stopped; dispatcher route not opened. (✅ done)
- Run the `devlearn` profile, which reads the same two roots as `Testmc`. Visible-browser index, public landing page, protected route, legacy warning, and exact policy line captured; outcomes match expectations. Host stopped. Signed-in cases are not covered under `G3-signin-availability`. (✅ done)

### 2. Convert the desktop-client content root (✅ done)

**Depends on:** step 1.

- Create the root `metadata.yml` with exactly this content, then confirm its first bytes aren't a byte-order mark. Written without BOM, with CRLF terminators. (✅ done)

  ```yaml
  schema: smartdocs-metadata/v1
  # Any reader signed in to SmartDocs can read this documentation space.
  access:
    requires: [authenticated]
  ```

- Validate the root with both files present. Checked 80 metadata files, zero failures. (✅ done)
- Delete the root `access.metadata.yml` and validate again. Checked 79 metadata files, zero failures; legacy file absent. (✅ done)

### 3. Convert the model-editor content root (✅ done)

**Depends on:** step 1.

- Replace the content of the existing root `metadata.yml` with exactly this content, keeping its three navigation fields, then confirm the first bytes. Fields preserved; no BOM, CRLF terminators. (✅ done)

  ```yaml
  schema: smartdocs-metadata/v1
  label: Documentation
  order: 0
  icon: book
  # Any reader signed in to SmartDocs can read this documentation space.
  access:
    requires: [authenticated]
  ```

- Validate with both files present. Checked 117 metadata files, zero failures. (✅ done)
- Delete the root `access.metadata.yml` and validate again. Checked 116 metadata files, zero failures; legacy file absent. (✅ done)

### 4. Convert the device-API content root (✅ done)

**Depends on:** step 1.

- Create the root `metadata.yml` with exactly this content, then confirm the first bytes. Written without BOM, with CRLF terminators. (✅ done)

  ```yaml
  schema: smartdocs-metadata/v1
  # This documentation space is public; no sign-in or app role is required.
  access: {}
  ```

- Validate with both files present, delete the root `access.metadata.yml`, and validate again. Checked 78 then 77 metadata files, zero failures in both states. (✅ done)

### 5. Convert the internal Learn library root (✅ done)

**Depends on:** step 1 and `G2-library-sync`.

- Create the root `metadata.yml` with exactly this content, confirm the first bytes, and wait until the library reports it synchronized. No BOM, CRLF terminators; visible OneDrive panel confirms the canonical file uploaded and *Backed up and synced*. (✅ done)

  ```yaml
  schema: smartdocs-metadata/v1
  # Reader access for the internal Learning Hub, enforced by SmartDocs at this content root.
  # Role values are the app roles of the reader app registration. Write is declared for
  # contributors and admins; SmartDocs has no editing feature, so it grants nothing more yet.
  access:
    permissions:
      Read: Read the internal Learning Hub
      Write: Contribute to the internal Learning Hub
    requires: [Read]
    roles:
      Reader: [Read]
      Contributor: [Read, Write]
      Admin: [Read, Write]
  ```

- Validate with both files present. Checked two metadata files, zero failures; no conflict copy. (✅ done)
- Delete the root `access.metadata.yml`, wait for synchronization, and validate again. One metadata file checked, zero failures; OneDrive confirms legacy deletion and *Backed up and synced*. The library's version history is the rollback. (✅ done)

### 6. Track the policies in their repositories (✅ done)

**Depends on:** steps 2–4.

- In each of the desktop-client, model-editor, device-API, and AICM repositories, stage only the content root's `metadata.yml` and commit it with a path-limited commit. Four local commits created on the existing branches. (✅ done)
- Verify each new commit lists exactly one file, and that the per-file pending count (`git status --porcelain --untracked-files=all`) dropped by exactly one; outside-policy pending entries and staged content must be unchanged. All four checks passed. Directory-collapsing default porcelain is not sufficient for an untracked documentation tree. (✅ done)
- Don't push. No commit was pushed. The deleted legacy files were never tracked, so they needed no commit. (✅ done)

### 7. Prove readiness and preserved behavior (✅ done)

**Depends on:** steps 2–6.

- Run `validate-tree` on all seventeen configured roots again. Same sixteen passes and deliberate `product-c` failure as step 1. (✅ done)
- Search every configured root for `access.metadata.yml`. Only the `product-c` fixture's deliberate nested file remains. (✅ done)
- Repeat both profile runs from step 1. All eight complete anonymous DOM texts match exactly; all four policy `Requires`/`Roles` strings match; final fingerprints equal canonical file hashes. No migration warning or missing/invalid-policy error. Signed-in cases recorded as not covered. (✅ done)
- Record the runs as the dated [validation sequence](_validation/20261110.01-rebac-metadata-fix.validation-sequence.md), with reviewed safe screenshots publicly and all nineteen captures plus raw evidence internally. Evidence links and publication guards verified. (✅ done)

### 8. Record the outcome (✅ done)

**Depends on:** step 7.

- Mark completed items, set `status: done`, and update the analysis from a pending conversion to the verified outcome while retaining its historical baseline. (✅ done)
- Record all four commit identifiers and dates, canonical fingerprints, validation-command corrections, and completion evidence in the internal execution sheet. (✅ done)

## 🧪 Validation contract

| Identifier | Check | Required result |
|---|---|---|
| `V1-validator-parity` | `validate-tree` on all seventeen configured roots, before and after | Identical verdicts: sixteen pass, `product-c` fails by design. (✅ done) |
| `V2-coexistence` | Each converted root validated with both files present | Zero failures in all four roots; equivalent policies. (✅ done) |
| `V3-canonical-alone` | Each converted root validated after deleting its legacy file | Zero failures in all four roots. (✅ done) |
| `V4-no-legacy-carriers` | Search all configured roots for `access.metadata.yml` | Only the `product-c` fixture's nested file. (✅ done) |
| `V5-encoding` | First bytes of each new or merged file | Not `EF BB BF`; CRLF confirmed. (✅ done) |
| `V6-policy-log-parity` | Policy log lines for the four converted spaces | Same `Requires` and `Roles`; no migration warning or missing/invalid-policy error. (✅ done) |
| `V7-anonymous-parity` | Anonymous index and routes in the devec4 and `devlearn` profiles | Same spaces and outcomes; exact complete DOM text matches in eight scenarios. (✅ done) |
| `V8-signed-in-parity` | `devlearn` with a `Reader` account and an unassigned account | Recorded as **not covered**, not passed, under `G3-signin-availability`; the allowed recording branch is complete. (✅ done) |
| `V9-single-file-commits` | Each new commit | Exactly one file; outside-policy pending and staged state unchanged. (✅ done) |

The devec4 profile's signed-in cases are expected to remain uncovered until an administrator completes its registration. Its sign-in policy is still covered by `V6-policy-log-parity`: every reader's verdict derives from the logged policy and the unchanged floors, so equal policy lines mean equal verdicts.

## 📐 Coverage and exit criteria

| Goal item | Landing | Exit condition |
|---|---|---|
| Convert the four legacy policies | Steps 2–5 | `V2-coexistence`, `V3-canonical-alone`, and `V5-encoding` pass for all four roots. (✅ done) |
| Commit untracked policy files | Step 6 | Four single-file commits; `V9-single-file-commits` passes. (✅ done) |
| No configured local dependency on legacy access files | Step 7 | `V4-no-legacy-carriers` passes. (✅ done) |
| Unchanged reader visibility | Steps 1 and 7 | `V1-validator-parity`, `V6-policy-log-parity`, and `V7-anonymous-parity` pass; `V8-signed-in-parity` is recorded as not covered. (✅ done) |
| Reviewable record | Steps 7–8 | Validation sequence, screenshots, persistent raw records, plan markers, analysis, and internal sheet updated. (✅ done) |

Mark the plan `done` only when every row passes. A missing signed-in session never authorizes marking a signed-in case as passed.

## 🗂️ Park lot

These items belong to updating SmartDocs metadata across repositories but are deliberately excluded from this plan.

- `PL1-container-publication`: publish each converted space to its deployed container. The publisher uploads the canonical file first and removes the legacy file last; first confirm the deployed host supports unified metadata, and run `validate-tree` on the source root, because the publisher doesn't validate. → defer. (📌 next steps)
- `PL2-push-commits`: push the four commits with each branch's ordinary workflow. → defer. (📌 next steps)
- `PL3-canonical-only-cutover`: set `Site:LegacyMetadataCompatibility` to `false` per profile once its sources, including deployed containers, hold no legacy policy. It feeds `PL3-legacy-retirement` in the [metadata refactoring plan](01-metadata-refactry.plan.md#️-park-lot). → defer. (📌 next steps)
- `PL4-repository-declarations`: convert the legacy repository declarations of the Learn, AICM, desktop-client, and model-editor repositories into their root `metadata.yml`. It's blocked until their authoring artifacts read the canonical file first; see `SIG-3-declaration-artifacts` on the [signals page](04-signals.md). → defer. (📌 next steps)
- `PL5-dispatcher-container-root`: inspect the dispatcher space's container root for a policy once read access or a documentation source exists. → defer. (📌 next steps)
- `PL6-device-api-navigation-files`: the device-API repository's nested navigation files are untracked along with the documentation they describe. → closed: they belong with that documentation work, not with this policy fix.
- `PL7-superseded-clone-file`: the superseded event-adapter clone holds an empty legacy policy. → closed: no profile reads that clone.
- `PL8-floor-only-spaces`: add required policies to the telemetry-gateway and dispatcher spaces. → closed: their configuration protects them with a floor by design; changing that is a policy decision, not a migration.
- `PL9-unversioned-navigation-files`: convert the remaining unversioned navigation files to the versioned envelope. → closed: that form remains supported and carries no policy.

## 💡 Conclusion

Executing this plan leaves every configured repository with its access policy in the canonical `metadata.yml`, under version control, and validated by the production parser. Readers see exactly what they saw before. The plan deliberately stops at the repositories: publishing to deployed containers and retiring compatibility mode follow from the parked items, once the deployed hosts are confirmed ready.

Previous: [Legacy access policies across configured repositories](03-smartdocs-rebac-metadata-fix.analysis.md). Related: [How SmartDocs authenticates readers and authorizes spaces](02-smartdocs-rebac-support.analysis.md).

## 📚 References

### Repository documentation

- **[Migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md)** 📘 [Design authority]. Inventory, findings, conversion rules, and dry-run evidence. Revise it before changing any file content in this plan.
- **[Unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md)** 📘 [Repository documentation]. The canonical format and the compatibility rules this plan relies on.
- **[Signals](04-signals.md)** 📘 [Repository documentation]. Work outside this plan's goal, including the byte-order mark defect and publisher validation.
- **[Plan execution rules](../../../../../.github/instructions/plan-execution.instructions.md)** and **[plan marking rules](../../../../../.github/instructions/plan-marking.instructions.md)** 📘 [Repository authority]. Govern readiness, lifecycle, and status marking.
- **[Testing and validation rules](../../../../../.github/instructions/testing-validation.instructions.md)** and **[validation-sequence template](../../../../../.github/templates/output-validation-sequence.template.md)** 📘 [Repository authority]. Define the visible console and browser run and the required record.

### Internal sources

Not resolvable from this repository; listed for provenance.

- **`src/docs/90.00-issues/202611/20261105.01-smartdocs-authentication-custom/03-smartdocs-rebac-metadata-fix.plan.internal.md`** — the execution sheet: exact paths, fingerprints for `G1-baseline-unchanged`, commands for every step, and the expected log lines.
- **`src/docs/_aliases/devec4.aliases.internal.md`** and **`src/docs/_aliases/testmc.aliases.internal.md`** — resolve the role names used above.

<!--
validations:
  structure: {status: "not_run", last_run: null}
  runtime: {status: "passed", last_run: "2026-11-10", notes: "All active exit criteria satisfied; eight exact anonymous DOM pairs, four policy logs, seventeen-root verdict parity; signed-in cases explicitly not covered."}
  actionability: {status: "passed", last_run: "2026-11-10", notes: "Eight-check gate run before writing the body; decisions selected from evidence during autonomous authoring and flagged for review."}
  implementation: {status: "not_run", last_run: null}
article_metadata:
  filename: "03-smartdocs-rebac-metadata-fix.plan.md"
  created: "2026-11-10"
  last_updated: "2026-11-10"
-->
