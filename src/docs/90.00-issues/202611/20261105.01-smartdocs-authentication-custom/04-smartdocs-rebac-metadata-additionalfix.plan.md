---
title: "Complete SmartDocs RBAC conformance across every configured source"
date: "2026-11-10"
type: how-to
status: in-progress
goal: "Bring every space declared by any SmartDocs configuration profile into conformance with the current authorization implementation, covering what the first migration plan didn't: the deployed containers behind each space, the reader registrations whose app roles the policies depend on, the configured roots it never validated, and signed-in visibility. Change no reader's visibility."
description: "A follow-up plan that extends the legacy access policy migration from local folders to deployed containers, registration app roles, unvalidated roots, and signed-in readers."
---

# Complete SmartDocs RBAC conformance across every configured source

## 🎯 Goal and scope

The [first migration plan](03-smartdocs-rebac-metadata-fix.plan.md) converted every **local** content root that still carried a legacy access policy, and proved that anonymous readers saw the same thing afterwards. A space's verdict, though, depends on more than its local folder. Under the [current model](02-smartdocs-rebac-support.analysis.md), the host combines three inputs: the policy at the content root the **running** deployment reads, the floors and required-policy flags in configuration, and the app roles that the deployment's **registration** puts in the reader's token. This plan covers the parts of those inputs that the first plan never inspected.

**Did the first plan miss repositories?** Not the ones its profiles point at. All seven repositories that the local overlays name were inventoried and validated, along with the others those overlays reference. The four that needed conversion were converted, and the rest already conformed. What it missed was every **source and input outside those local folders**:

- **Deployed containers.** Fourteen containers back the same spaces in deployed environments, and none was read.
- **Identity.** No one verified that the role values the policies map exist on the registrations that issue the tokens.
- **Unvalidated roots.** One fixture root and one profile root were never checked.
- **Signed-in readers.** No signed-in reader was ever validated.

**Narrowing of the request.** "Ensure RBAC configuration is conforming" is narrowed to verifying and correcting what each deployment reads. It excludes changing how spaces are protected; see `D3-keep-protection-as-configured`. It also excludes deployment configuration changes and Entra changes; see `D4-readiness-not-cutover` and `D5-no-entra-changes`.

**Current state:** devec4 cleanup, hosted visibility checks, Testmc registration role checks, and the designated test-account assignment preflight passed on November 10, 2026. Testmc storage inspection and local signed-in checks remain blocked. Deployed devec4 resources and private product repositories are named by role.

## 📚 Table of contents

- [🎯 Goal and scope](#-goal-and-scope)
- [🗂️ Every configured space, by RBAC class](#️-every-configured-space-by-rbac-class)
- [🔍 What the first plan didn't cover](#-what-the-first-plan-didnt-cover)
- [🧭 Decisions and readiness](#-decisions-and-readiness)
- [🔎 Discovery](#-discovery)
- [⚙️ Implementation sequence](#️-implementation-sequence)
- [🧪 Validation contract](#-validation-contract)
- [📐 Coverage and exit criteria](#-coverage-and-exit-criteria)
- [🗂️ Park lot](#️-park-lot)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🗂️ Every configured space, by RBAC class

Seven private profiles and five public profiles (`Development`, `devdocs`, `devlearn`, `cpucost`, `scale`) declare or override spaces through their environment and development-local files. The public base settings and the two generic `local` overlays declare none. Each space below is classified by how the current implementation decides its visibility:

- **Role**: a policy permission that only mapped app roles grant.
- **Sign-in**: the built-in `authenticated` permission, from a policy or a floor.
- **Public**: the profile enables sign-in, but the space requires nothing.
- **No sign-in**: the profile has no reader registration.

*Local* is the content root a local run reads; *deployed* is the container a deployed host reads.

| Space | Profiles | Class | Local root state | Deployed source state |
|---|---|---|---|---|
| `learn-internal` | `devlearn`, `Testmc` | Role: `Reader`, `Contributor`, `Admin` | Canonical since the first plan | Not read: Testmc tenant |
| `aicm` | `TestmcAicm` | Role: `Reader`, `Contributor`, `Admin` | Canonical, now committed | Not read: Testmc tenant |
| `product-a`, `product-b` | `access` | Role, fixture | Canonical | Local-only fixtures |
| The desktop-client space | `devec4` | Sign-in, policy and floor | Canonical | Canonical only; equivalent legacy file removed |
| The model-editor space | `devec4` | Sign-in, policy and floor | Canonical | Canonical only; equivalent legacy file removed |
| The telemetry-gateway space | `devec4` | Sign-in, floor only | No policy, by design | No policy |
| The dispatcher space | `devec4` | Sign-in, floor only | No local source | **Container empty** |
| `product-d` | `access` | Sign-in, floor only | No policy, by design | Local-only fixture |
| Scenario-only `home` fixture | `access`, one validation scenario | Sign-in, policy | Canonical and validated | Local-only fixture |
| The device-API space | `devec4` | Public, explicit `access: {}` | Canonical | Canonical only |
| The event-adapter space | `devec4` | Public, no policy | No policy | No policy |
| `learn` | `devlearn`, `Testmc` | Public | No policy | Not read: Testmc tenant |
| `product-c` | `access` | Public; nested legacy file is a negative fixture | Fails validation by design | Local-only fixture |
| Five Diginsight product spaces | `devdocs`, `TestmcDocs`, public `Development` and `cpucost` | No sign-in | No policy | Not read: Testmc tenant |
| `diginsight.smartdocs.scale` | Public `scale` | No sign-in | **Root doesn't exist** | None |

No overlay, and no app setting on the devec4 docs host, overrides `Site:LegacyMetadataCompatibility` or `Site:Authorization:DefaultRequirement`. Every profile therefore runs in compatibility mode with no site-wide floor.

The deployed hosts were checked anonymously on November 10, 2026. The Testmc Learn and docs hosts were last deployed after the unified-metadata refactor, and so was the devec4 docs host. Anonymous readers see what the configuration predicts: protected routes answer *Page not found*, and public pages render. The AICM host answers HTTP 403 to anonymous requests, and no public workflow deploys it.

## 🔍 What the first plan didn't cover

Each finding began with direct, read-only inspection on November 10, 2026. Later results are noted where they changed the initial observation.

| Identifier | Finding | Consequence |
|---|---|---|
| `F1-deployed-legacy-retained` | The desktop-client and model-editor containers held equivalent canonical and legacy files after the first plan. Both legacy files have since been removed. The publisher still preserves stale root policy files for a required-policy space. | The observed legacy carriers and canonical-only blockers are gone. If a stale file is retained again and later diverges, the host hides the space from every reader. |
| `F2-testmc-sources-unread` | The Testmc storage account's eight containers, including those behind `learn-internal` and `aicm`, couldn't be read. After Testmc sign-in succeeded, the storage tool still refused the endpoint under its access policy. | Whether the deployed `learn-internal` container still holds only the legacy policy, and whether `aicm` holds any policy, isn't established. |
| `F3-role-values-unverified` | The Learn reader registration defines enabled `Reader`, `Contributor`, and `Admin` roles; the fixture registration defines those plus `ProductB.Reader`. Each permits assignment to users and groups. | All expected role definitions exist. A preflight checked the two designated test accounts; actual token claims and reader visibility haven't been verified. |
| `F4-devec4-registration-changed` | The devec4 reader registration issues ID tokens and lists callbacks for localhost port 7280, the devec4 docs host, and the stage static documentation host. It defines no app roles. | The stale sign-in claim in the [migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md#-which-profiles-authenticate-readers) was corrected. No app roles is appropriate because every devec4 policy needs only sign-in. |
| `F5-signed-in-never-validated` | Neither plan had validated a signed-in reader. Hosted devec4 sign-in and protected pages have since passed; local and Testmc role cases remain untested. | Sign-in-only visibility is observed on devec4. The designated Testmc account assignments were checked, but token claims and local reader verdicts aren't verified. |
| `F6-unvalidated-roots` | The scenario-only `home` fixture root hadn't been validated; it has since passed. The `scale` profile's root doesn't exist. | The `scale` profile can't start, but it has no sign-in, so it's not an RBAC concern. |
| `F7-dispatcher-container-empty` | The dispatcher space's container is empty, and the space has no local source. | The space is unusable for every reader, signed in or not. That's a content gap, not an authorization one. |
| `F8-older-checkout` | A second SmartDocs checkout predates the unified-metadata refactor. A host run from it would read only legacy access files and hide every converted required-policy space. | No profile or script selects it; the publisher prefers the current checkout. |

The stage static documentation host shares the devec4 registration but isn't a SmartDocs deployment. Its gap is recorded as `SIG-6-stage-static-host` on the [other signals page](05-other-signals.md). The publisher's stale-policy retention behind `F1-deployed-legacy-retained` is recorded as `SIG-5-publisher-keeps-stale-policy` on the [signals page](04-signals.md).

## 🧭 Decisions and readiness

### Selected decisions

The owner was asked these questions on November 10, 2026, but wasn't available to answer. Each selection is the option closest to the request and to the first plan's choices. Review them first: changing one returns the plan to `draft` and reruns its gate.

| Identifier | Selected contract | Evidence |
|---|---|---|
| `D1-delete-equivalent-legacy-blobs` | Delete a deployed `access.metadata.yml` only after backing it up, independently confirming that the canonical file contains a valid access policy, and validating the pair with zero failures. (🟡 todo) | `F1-deployed-legacy-retained`; the publisher's own warning asks for deliberate removal; the backup is the rollback |
| `D2-publish-learn-internal` | Publish the converted internal Learn library to its Testmc container with the existing publisher, then apply `D1-delete-equivalent-legacy-blobs` there. (🟡 todo) | The local root became canonical in the first plan; the deployed host's version reads canonical policies |
| `D3-keep-protection-as-configured` | Don't add policies to the floor-only telemetry-gateway space or the implicitly public event-adapter space. (🟡 todo) | Both are valid under the current model; the first plan closed the same question as `PL8-floor-only-spaces` |
| `D4-readiness-not-cutover` | Prove, per profile, whether every source is free of legacy access files; don't change `Site:LegacyMetadataCompatibility`. (🟡 todo) | Changing deployment configuration is outside both plans; the proof is the cutover's precondition |
| `D5-no-entra-changes` | Report a missing or disabled app role; never create, rename, or assign roles in this plan. (🟡 todo) | Entra changes are administrator actions with effects outside SmartDocs |
| `D6-aicm-cleanup-only` | For `aicm`, delete a legacy file only when the container already holds an equivalent canonical policy; don't publish `aicm`. (🟡 todo) | No known deployment route for its host; an unreviewed publish could change what readers see |

### Open decisions

None. Facts that can only be settled during execution are bounded in [discovery](#-discovery).

### Actionability gate (✅ done)

| Gate | Authoring result |
|---|---|
| Goal alignment | The goal restates the request; the narrowing to verifying and correcting deployed sources is explicit above |
| Goal reachability | Baseline → analysis correction → deployed cleanup → Testmc read and publish → signed-in checks → readiness matrix → record |
| Execution determinism | Each deletion requires backups, independent confirmation of canonical access, pair validation, and an unchanged legacy-file ETag |
| Clarity and actionability | Every check has an expected result and a negative branch |
| Unknown resolution | Read-only facts were resolved by inspection; tenant access, callback port, and signed-in availability are bounded below |
| Scope discipline | In-domain exclusions are parked; the stage static host and publisher behavior are routed to signal pages |
| Coverage promise | The coverage table maps each finding to a step and an exit condition |
| Principle impact | Not applicable: this isn't a vision-amendment plan |

## 🔎 Discovery

These checks run before the steps that depend on them.

| Identifier | Probe | Defined negative branch |
|---|---|---|
| `G1-baseline-current` | The four converted local policies and the `aicm` policy match the first plan's recorded hashes; both devec4 containers still hold their policy pairs. (✅ done) | If a local hash differs, stop for that root and rerun the first plan's coexistence check against the new bytes. If a container changed, re-read it and redo `F1-deployed-legacy-retained` for that container before step 3. |
| `G2-testmc-tenant-access` | The owner can sign in to the Testmc tenant interactively and read its storage account and its two registrations. Sign-in and registration reads succeeded, but the storage tool still refuses the endpoint under its access policy. (🟡 todo) | If storage remains unavailable, keep container-dependent work and `F2-testmc-sources-unread` open and leave the plan `in-progress`; don't infer contents. |
| `G3-callback-port-free` | Local HTTPS port 7280, the only local callback the registrations accept, is free, or its owner stops the process holding it. **Blocked:** an existing SmartDocs host owns the port; it was left running. (🟡 todo) | If it stays occupied, run local signed-in cases nowhere else, and record them as not covered. |
| `G4-devec4-hosted-signin` | An existing devec4-tenant session completed sign-in on the docs host without entering credentials. (✅ done) | If sign-in fails, record the error code and treat the hosted signed-in cases as not covered; don't change the registration. |

## ⚙️ Implementation sequence

Rebuild before every local run, and never pass `--no-build`. Run hosts in visible consoles the owner can stop, and validate pages in a visible browser window.

### 1. Refresh the baseline (✅ done)

**Depends on:** `G1-baseline-current`.

- Built the isolated validator and checked eighteen roots: seventeen passed, including `home`; `product-c` failed for its deliberate nested policy. (✅ done)
- Confirmed that the `scale` root is absent and that the `product-c` fixture is the only remaining local legacy access file. (✅ done)
- Listed all six devec4 container roots before deletion; the [RBAC class table](#️-every-configured-space-by-rbac-class) now records the post-cleanup state. (✅ done)

### 2. Correct the migration analysis (✅ done)

**Depends on:** step 1.

- Added the dated registration correction and the deployed-source plan link to the [migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md), preserving its historical inventory. (✅ done)

### 3. Remove the equivalent legacy files from the devec4 containers (✅ done)

**Depends on:** step 1 and `D1-delete-equivalent-legacy-blobs`.

- Downloaded both policy pairs into persistent internal backups and recorded their SHA-256 values and ETags. (✅ done)
- Validated each pair with two files and zero failures; the metadata regression harness also passed 344 assertions. (✅ done)
- Confirmed that both canonical files contain a root access policy and match their validated local canonical files byte-for-byte. (✅ done)
- Deleted only the two root legacy files, using their recorded ETags as preconditions. Both canonical files remained unchanged, and both root listings contain only `metadata.yml` among policy files; backups remain available for rollback. (✅ done)
- After the cache interval, repeated the visible-browser checks: the same two public spaces and pages rendered, and the protected routes still showed *Page not found*. (✅ done)

### 4. Read the Testmc sources and registrations (🟡 todo)

**Depends on:** `G2-testmc-tenant-access` for the container inspection. The storage portion remains **blocked** by the tool's access policy; registration roles were read independently after sign-in.

- List the root files of all eight Testmc containers. For `learn-internal` and `aicm`, download any root policy files and validate them together. Expected: the five Diginsight product containers and `learn` hold no access policy. If any of them holds a restrictive policy, record it as a defect: those profiles have no sign-in, so a restrictive policy would hide the space. (🟡 todo)
- Read both registrations. `Reader`, `Contributor`, and `Admin` are enabled for users and groups on both; the fixture registration also has `ProductB.Reader`. Each expected value appears once. No role changes were made. (✅ done)

### 5. Publish the internal Learn library and clean the Testmc policies (🟡 todo)

**Depends on:** step 4, `D2-publish-learn-internal`, and `D6-aicm-cleanup-only`. **Blocked** by step 4.

- Run the publisher for `learn-internal` with `-WhatIf` first. Expected: the plan uploads only the root `metadata.yml` and prunes nothing unexpected. Then publish for real. (🟡 todo)
- Download the container's root policy files, validate them together, and, when they're equivalent, delete the legacy file as in step 3. (🟡 todo)
- For `aicm`, delete a legacy file only when step 4 found a canonical access policy beside it and established equivalence. Otherwise, record the container state for `PL3-aicm-publication`. (🟡 todo)
- Anonymously request `/learn-internal` on the Testmc Learn host after at least 60 seconds. Expected: *Page not found*, as before. (🟡 todo)

### 6. Validate signed-in visibility (🟡 todo)

**Depends on:** steps 3 and 5, `G3-callback-port-free`, and `G4-devec4-hosted-signin`.

- Checked the designated accounts on the shared Learn registration: the Reader account has a direct `Reader` assignment, and the query returned no assignment for the unassigned account. The query included direct-group assignments; it doesn't prove token claims or visibility. (✅ done)
- Locally, run `devlearn` with a `Reader` and an unassigned account. **Not covered:** the callback port is occupied, so token claims and reader verdicts weren't observed. (🟡 todo)
- Locally, run `devec4` with a devec4-tenant account. **Not covered:** the callback port is occupied. (🟡 todo)
- Locally, run `TestmcAicm` with a `Reader` account; the validation contract also requires an unassigned-account case. **Not covered:** the callback port is occupied, so token claims and reader verdicts weren't observed. (🟡 todo)
- On the devec4 docs host, an existing SSO session opened the desktop-client and model-editor spaces. The gateway and two public pages also rendered; the empty dispatcher reported *Not found*. (✅ done)

### 7. Prove readiness for canonical-only mode (🟡 todo)

**Depends on:** steps 1, 3, 4, and 5.

- For each profile with sign-in, mark it ready only when every local root and every container it reads holds no `access.metadata.yml`. Expected: `devec4` ready; `devlearn` and `Testmc` ready after step 5; `TestmcAicm` ready only if its container holds a canonical policy; and `access` not ready by design. Change no setting. (🟡 todo)

The interim matrix records what can be established without the blocked Testmc reads:

| Profile | Interim verdict | Evidence |
|---|---|---|
| `devec4` | Ready for canonical-only mode | Local roots and every blob name in all six containers are legacy-free; the empty dispatcher remains a separate content gap. |
| `access` | Not ready by design | `product-c` retains its deliberate nested legacy fixture. |
| `devlearn`, `Testmc` | Not proven ready | Local roots pass, but the deployed Learn containers haven't been inspected; conformance is unknown. |
| `TestmcAicm` | Not proven ready | Its local root passes, but the deployed container hasn't been inspected; conformance is unknown. |

### 8. Record the outcome (🟡 todo)

**Depends on:** steps 1–7.

- Write a dated validation sequence under this work item's `_validation/` folder. The partial record includes reviewed screenshots and marks unavailable cases as not covered. (✅ done)
- Mark completed items and record backups, hashes, and deleted blob names. To do: set `status: done` only when every exit criterion passes. (🟡 todo)
- Review the landing of `SIG-5-publisher-keeps-stale-policy`. The publisher behavior is unchanged, so the signal remains pending. (✅ done)

## 🧪 Validation contract

| Identifier | Check | Required result |
|---|---|---|
| `V1-baseline-parity` | `validate-tree` on the eighteen local roots | Seventeen passes; `product-c` fails by design. (✅ done) |
| `V2-deployed-equivalence` | Each downloaded policy pair, validated before any deletion | Both devec4 pairs passed; Testmc remains blocked. (🟡 todo) |
| `V3-deployed-canonical-only` | Root listing of each cleaned container | Both devec4 roots are canonical-only; Testmc remains blocked. (🟡 todo) |
| `V4-anonymous-hosted-parity` | Anonymous index and routes on the devec4 docs host and the Testmc Learn host, before and after | devec4 parity passed; Testmc was observed without a publish, so its post-publish check remains blocked. (🟡 todo) |
| `V5-role-values-exist` | App roles on the two Testmc-tenant registrations | All seven required role definitions exist, are enabled, and permit user and group assignments. (✅ done) |
| `V6-signed-in-roles` | `devlearn` and `TestmcAicm` with `Reader` and unassigned accounts | Account assignments were checked, but token claims and reader verdicts remain not covered: callback port occupied. (🟡 todo) |
| `V7-signed-in-sign-in-only` | `devec4`, locally and on the docs host, with a devec4-tenant account | Hosted sign-in and protected pages passed; local case not covered because the callback port is occupied. (🟡 todo) |
| `V8-readiness-matrix` | Per-profile legacy-free proof | Matrix recorded with each profile's ready or not-ready verdict and its reason. (🟡 todo) |

A missing session never authorizes marking a signed-in case as passed.

## 📐 Coverage and exit criteria

| Finding | Landing | Exit condition |
|---|---|---|
| `F1-deployed-legacy-retained` | Step 3 | `V2-deployed-equivalence`, `V3-deployed-canonical-only`, and `V4-anonymous-hosted-parity` passed for both devec4 containers. (✅ done) |
| `F2-testmc-sources-unread` | Steps 4–5 | Every Testmc container's root is recorded. `learn-internal` passes `V3-deployed-canonical-only`; `aicm` passes it or is parked under `PL3-aicm-publication`. (🟡 todo) |
| `F3-role-values-unverified` | Step 4 | `V5-role-values-exist` passed for both registrations; no missing values require an administrator action. (✅ done) |
| `F4-devec4-registration-changed` | Step 2 | The analysis carries a dated correction. (✅ done) |
| `F5-signed-in-never-validated` | Step 6 | Hosted devec4 sign-in passed; each unavailable local/account case is recorded as not covered, as the exit condition permits. `V6` and the local part of `V7` remain unverified. (✅ done) |
| `F6-unvalidated-roots` | Step 1 | The `home` fixture passes `V1-baseline-parity`; the `scale` root's absence is recorded. (✅ done) |
| Readiness for canonical-only mode | Step 7 | `V8-readiness-matrix` is recorded. (🟡 todo) |
| Reviewable record | Step 8 | A partial sequence, current markers, execution evidence, and signal state are recorded; update the record after the blocked work runs. (🟡 todo) |

`F7-dispatcher-container-empty` and `F8-older-checkout` land in the park lot.

## 🗂️ Park lot

These items are part of RBAC conformance across configured sources but are deliberately excluded from this plan.

- `PL1-compatibility-cutover`: set `Site:LegacyMetadataCompatibility` to `false` for each profile that `V8-readiness-matrix` marks ready. It continues `PL3-canonical-only-cutover` of the [first plan](03-smartdocs-rebac-metadata-fix.plan.md#️-park-lot). → defer. (📌 next steps)
- `PL2-push-policy-commits`: push the first plan's four single-file commits so that fresh clones and default branches carry their policies. It continues that plan's `PL2-push-commits`. → defer. (📌 next steps)
- `PL3-aicm-publication`: publish `aicm` to its container, and establish how its host is deployed and why it answers HTTP 403, if step 4 finds no canonical policy there. → defer. (📌 next steps)
- `PL4-explicit-policies-for-floor-only-spaces`: give the telemetry-gateway space a `requires: [authenticated]` policy and the event-adapter space an explicit `access: {}`. → closed: `D3-keep-protection-as-configured`; floors and implicit public access are valid under the current model.
- `PL5-dispatcher-content`: fill the empty dispatcher container, or give the space a documentation source. → closed: owned by the configurations-update work item through `SIG-4-dispatcher-source` on the [signals page](04-signals.md).
- `PL6-older-checkout`: update or retire the older SmartDocs checkout, so that no host built from it serves converted spaces. → defer. (📌 next steps)
- `PL7-scale-profile-root`: create or remove the `scale` profile's synthetic content root. → closed: a performance-test profile without sign-in, unrelated to authorization.
- `PL8-access-fixture-compatibility`: make the `access` fixture profile legacy-free. → closed: its overlay keeps a legacy file on purpose as a negative fixture.
- `PL9-repository-declarations`: convert the legacy repository declarations. → defer: still blocked by `SIG-3-declaration-artifacts`, as in the first plan's `PL4-repository-declarations`. (📌 next steps)

## 💡 Conclusion

The first plan made every local folder conform. This plan follows the same spaces past their folders: into the containers that deployed hosts read, the registrations whose app roles give role-gated policies their meaning, and the signed-in readers those policies exist for. Executing it leaves no deployed source with an obsolete policy that a later change could turn into a conflict. It also establishes with evidence whether `Reader`, `Contributor`, and `Admin` actually reach the spaces that map them, and states which profiles are ready to stop reading legacy files.

Previous: [Legacy access policy migration plan](03-smartdocs-rebac-metadata-fix.plan.md). Related: [How SmartDocs authenticates readers and authorizes spaces](02-smartdocs-rebac-support.analysis.md).

## 📚 References

### Repository documentation

- **[Legacy access policy migration plan](03-smartdocs-rebac-metadata-fix.plan.md)** and **[its validation sequence](_validation/20261110.01-rebac-metadata-fix.validation-sequence.md)** 📘 [Repository documentation]. The local conversion, commits, and anonymous parity proof this plan builds on.
- **[Additional-fix validation sequence](_validation/20261110.02-rebac-metadata-additionalfix.validation-sequence.md)** 📘 [Validation record]. Passed checks, screenshots, and cases not yet covered.
- **[Migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md)** 📘 [Design authority]. The pre-migration inventory, findings, and conversion rules; step 2 corrects it.
- **[Authentication and authorization explanation](02-smartdocs-rebac-support.analysis.md)** 📘 [Repository documentation]. The verdict model, role definitions, and deployment controls that define conformance here.
- **[Unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md)** 📘 [Repository documentation]. The canonical format and compatibility rules.
- **[Signals](04-signals.md)** and **[other signals](05-other-signals.md)** 📘 [Repository documentation]. Work outside this plan's goal, including the publisher's stale-policy retention and the stage static documentation host.
- **[Plan execution rules](../../../../../.github/instructions/plan-execution.instructions.md)** and **[plan marking rules](../../../../../.github/instructions/plan-marking.instructions.md)** 📘 [Repository authority]. Govern readiness, lifecycle, and status marking.
- **[Testing and validation rules](../../../../../.github/instructions/testing-validation.instructions.md)** 📘 [Repository authority]. The visible console and browser run and the required record.

### External documentation

- **[Microsoft Graph appRole reference](https://learn.microsoft.com/en-us/graph/api/resources/approle?view=graph-rest-1.0)** 📘 [Official]. Defines the role value, enablement flag, and `User` member type for user and group assignments.
- **[Microsoft Graph user app-role assignments](https://learn.microsoft.com/en-us/graph/api/user-list-approleassignments?view=graph-rest-1.0)** 📘 [Official]. Describes the direct-user and direct-group assignment query and eventual-consistency requirement.

### Internal sources

Not resolvable from this repository; listed for provenance.

- **`src/docs/90.00-issues/202611/20261105.01-smartdocs-authentication-custom/04-smartdocs-rebac-metadata-additionalfix.plan.internal.md`** — the full inventory with real names, every observation and its command, and the commands for each step.

<!--
validations:
  structure: {status: "not_run", last_run: null}
  sources: {status: "reviewed", last_run: "2026-11-10", notes: "Overlays, devec4 registration and containers, hosted routes, both Testmc role definitions, and two test-account assignments observed; storage remains blocked by the tool's access policy."}
article_metadata:
  filename: "04-smartdocs-rebac-metadata-additionalfix.plan.md"
  created: "2026-11-10"
  last_updated: "2026-11-10"
-->
