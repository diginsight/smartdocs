---
title: "Refactor SmartDocs metadata into one scoped contract"
date: "2026-11-06"
type: how-to
status: in-progress
goal: "Unify repository declarations, navigation metadata, and current space access policies under a documented metadata.yml schema, with shared runtime acquisition/cache, preserved safeguards, and a bounded legacy migration."
description: "An implementation-ready plan for scoped metadata consolidation, measurable cache reuse, and compatibility without adding folder authorization."
---

# Refactor SmartDocs metadata into one scoped contract

## 🎯 Goal and scope

Replace permanent metadata-carrier proliferation with one canonical `metadata.yml` per directory. Preserve the distinct meanings of repository classification, navigation, and authorization. Runtime consumers must share a parsed snapshot rather than independently read or interpret the same file.

The [analysis](01-metadata-refactry.analysis.md) is the design contract for this plan. This plan covers implementation, migration of this repository and fixtures, schema documentation, and regression proof. The user authorized execution; changes to external deployments or Entra registrations remain outside scope.

Repository-root classification and content-root runtime metadata remain at separate roots when those roots differ. When they coincide, their sections share one physical file. Whole-space authorization remains the supported runtime scope; inherited folder permissions are explicitly deferred.

**Current state:** the shared provider, consumer wiring, owned migration, and staged validator are implemented. Automated checks, the controlled authoring walkthrough, anonymous scenarios, and one genuine signed-in session pass. The role-assigned browser matrix and generated-reference regeneration remain pending.

## 📚 Table of contents

- [🎯 Goal and scope](#-goal-and-scope)
- [🧭 Decisions and readiness](#-decisions-and-readiness)
- [🔎 Discovery](#-discovery)
- [⚙️ Implementation sequence](#-implementation-sequence)
- [🧪 Validation contract](#-validation-contract)
- [📐 Coverage and exit criteria](#-coverage-and-exit-criteria)
- [🗂️ Park lot](#-park-lot)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🧭 Decisions and readiness

### Selected decisions

The author asked the user about location, folder-RBAC scope, and migration. The response delegated pragmatic choices. The selected defaults are explicit below; changing one requires returning the plan to `draft` and rerunning its gate.

| Identifier | Selected contract |
|---|---|
| `D1-metadata-authority` | One file per directory/scope; no duplicated root repository declaration inside a separate content subtree. (✅ done) |
| `D2-folder-rbac-scope` | Root access policies only in this implementation; no generic metadata inheritance or new folder enforcement. (✅ done) |
| `D3-legacy-transition` | Temporary legacy compatibility plus a canonical-only runtime mode; conflicting dual declarations fail closed. (✅ done) |
| `D4-schema-shape` | `schema: smartdocs-metadata/v1`; existing top-level navigation keys; structured `repository` and `access` sections. (✅ done) |
| `D5-shared-snapshot` | One provider and one immutable parsed snapshot per mounted scope/mode; projections aren't separate acquisition caches. (✅ done) |
| `D6-policy-freshness` | Root metadata uses access freshness, including currently public roots; non-root navigation uses structural freshness. (✅ done) |

### Open decisions

None block this bounded plan. External rollout dates, fleet-wide legacy retirement, and future folder-role inheritance aren't implied approvals; they are excluded follow-ups.

### Actionability gate (✅ done)

| Gate | Authoring result |
|---|---|
| Goal alignment | Repository data, navigation, access, schema documentation, and shared acquisition all have explicit landings |
| Goal reachability | Contract → provider → consumers → migration → validation → current documentation |
| Execution determinism | Locations, schema shape, mode behavior, failure semantics, and performance counters are fixed |
| Clarity | Every implementation phase has outputs and a failure branch |
| Unknown resolution | Read-only facts resolved; user trade-offs delegated; execution-time dependencies bounded below |
| Scope discipline | No folder authorization, identity-provider changes, or unrelated metadata frameworks |
| Coverage promise | The coverage table maps every in-scope goal |
| Principle impact | Not applicable: this isn't a vision-amendment plan |

## 🔎 Discovery

These checks precede their dependent actions. They are execution-time facts, not unsettled design preferences.

| Identifier | Probe | Defined negative branch |
|---|---|---|
| `G1-execution-baseline` | Record current source revision, relevant dirty changes, and metadata/configuration inventory before edits | Baseline: revision `42d79a5`, no tracked changes, and three untracked work-item documents, including the user-owned overview. One repository declaration, eleven tracked folder metadata files, and four legacy access fixture files were found; local application profiles are present. The private peer is readable and the plan has no existing companion. (✅ done) |
| `G2-policy-migration-inventory` | Enumerate every space in the specific profile being validated, including optional legacy policy files | The four local roots are accessible: Product A has a canonical required policy, Product B has a canonical policy, Product C has no root policy and retains a nested legacy negative fixture, and Product D has an `authenticated` deployment floor. External profiles weren't inventoried or switched. (✅ done) |
| `G3-documentation-evidence` | Resolve existing companions and evidence owners before revising generated reference pages or companion-bearing articles | To do: run the owning investigators where they can read the declared peer and write their dossiers. The attempted code investigators couldn't read outside their workspace; the security investigator had no editing tool, and the configuration invocation failed. Generated pages remain unchanged and stale. (🟡 todo) |
| `G4-visible-authentication` | Establish a visible local browser and an authorized way to exercise signed-in roles | A genuine local session succeeded through an already-registered access-test callback; an earlier registration-update request was denied and no registration was changed. The final parser build passed a repeated signed-in run, and a valid probe found no effective match for the registration's four defined, enabled role values. The probe doesn't rule out another unmapped role claim. To do: obtain authorized sessions for the remaining role-assigned cases; don't fabricate identities or grants. (🟡 todo) |

## ⚙️ Implementation sequence

### 1. Establish the schema and regression harness (✅ done)

**Depends on:** `G1-execution-baseline`.

- Add the machine-readable envelope schema at `src/Diginsight.SmartDocs.Web/Metadata/metadata.schema.json`, matching the analysis. Define supported root/folder placement, aliases, nullable legacy forms, version rules, section types, and the 65,536-byte input limit. Keep runtime-only semantic rules explicitly identified alongside the structural schema. The schema and representative structural examples passed JSON Schema validation. (✅ done)
- Add a focused executable regression harness at `eng/metadata-checks/MetadataChecks.csproj`, referencing the existing web project and packages. No existing test project was found; use explicit assertions and a nonzero exit on failure rather than adding a general test framework for this refactor. Provide `test` and `validate-tree <path>` commands. Both commands run; provider and integration cases are added in later phases. (✅ done)
- Add synthetic fixtures for combined roots, separate repository/content roots, legacy flat navigation, role-gated and sign-in-only spaces, malformed policies, duplicate aliases, oversized files, and a policy section beyond 8 KiB. Use no deployment identifiers or real principals. Size-boundary inputs are generated by the harness. (✅ done)
- Define immutable `MetadataDocument`/outcome types under `src/Diginsight.SmartDocs.Web/Metadata/` and implement structural and strict policy semantic validation through the same parser entry point the host will use. Retain permission descriptions, but evaluate permissions by name. Reuse existing name, duplicate, reserved-permission, and grant rules rather than creating divergent validators. The parser and existing policy validator passed 51 assertions; all eleven existing folder metadata files passed tree validation. (✅ done)

**Completion:** the harness rejects unsupported versions, malformed root sections, unknown required permissions, and invalid nested-policy placement. Legacy navigation fixtures preserve their supported display values. If a fixture requires changing the agreed semantics, stop and revise the analysis before proceeding.

### 2. Build one runtime metadata provider (✅ done)

**Depends on:** phase 1.

- Add `ContentMetadataProvider` under `src/Diginsight.SmartDocs.Web/Metadata/`, using phase 1's document/outcome types. Resolve each physical source through `SpaceContentRegistry`; use its existing mounted path identity rather than inventing another space registry. (✅ done)
- Read the complete metadata document, reject oversized input, and never reuse article-head truncation for unified YAML. Preserve missing, valid, invalid, and unreadable outcomes; unreadable outcomes must not become cached absence. Filesystem reads now distinguish missing files from read failures. (✅ done)
- Cache one serializable snapshot through existing SmartCache. Use versioned kind names that distinguish canonical-only and compatibility modes, with the normalized metadata content path. Restore ordinal-ignore-case policy comparers after deserialization. The harness round-trips the actual SmartCache serializer and simulates a second-host hit. (✅ done)
- Coalesce racing misses. Root snapshots always use `ContentFreshnessOptions.Access`; ordinary folder snapshots use `Structure`. Do not wrap authorization reads in `BackgroundRevalidationCache`. One hundred concurrent callers produced one origin read and one parse; a controlled local invalidation race reloaded the policy. (✅ done)
- Add `Site:LegacyMetadataCompatibility`, defaulting to `true`. In that mode only, integrate legacy access reads into the same snapshot and outcome. Do not create a separate legacy cache. Implement the conflict matrix in the validation section. Compatibility checks count the additional legacy parse separately. (✅ done)
- Preserve canonical-name validation: a missing canonical lookup may perform one directory listing to detect a misnamed policy-bearing file. Keep this explicit exception separate from normal successful-read counters. (✅ done)

**Completion:** the instrumented provider passes the read/parse assertions below. If canonical-only authorization plus navigation produces two reads or parses, fix the consumer/provider boundary; don't weaken the metric.

### 3. Wire consumers and preserve delivery boundaries (✅ done)

**Depends on:** phase 2.

- Wire the provider in `Program.cs`. Change `AccessRequirementResolver` to consume the snapshot policy; an interim `AccessFileProvider` facade may delegate, but must own neither a cache nor an origin read. Preserve one-snapshot requirement/grant evaluation in `SpaceVisibilityEvaluator`. The facade delegates to the shared provider. (✅ done)
- Route `CachedContentSource.ReadFolderMetaAsync`, `DynamicNavBuilder`, and `FolderRecordProvider` through the shared metadata projection. Remove the independent `folder-meta` parse/cache path. Keep `FolderMeta` as a navigation DTO; versioned unified bytes must never reach its old flat regex parser. Simple unversioned flat files use an additional in-memory legacy navigation pass without another read or cache, preserving fences, scalar values, and repeated keys. (✅ done)
- Update the relevant `IContentLister` path so its default head-based parsing cannot bypass the server provider. Preserve existing article-head behavior and avoid introducing server-only repository/policy types into WASM contracts. The fallback now rejects metadata parsing. (✅ done)
- Keep explicit source-root policy loading before direct page/content reads. Preserve virtual site-root branding and configured mount precedence; neither should accidentally start inheriting repository navigation fields. Mount isolation and precedence are covered by the harness. (✅ done)
- Replace arbitrary unified-value serialization with an explicit browser-safe navigation projection. Preserve supported legacy scalar values only for unversioned navigation documents; never project reserved policy or repository fields. Apply the same projection to folder endpoints and hub messages. The projection is checked in the harness and visible browser. (✅ done)
- Extend existing metadata-file exclusion checks in `SpaceMountedContentSource`, `FileSystemContentSource`, and `BrandingAssets` to canonical and legacy repository/access carriers. Deny raw/head delivery and filesystem aliases. Keep server-internal canonical filename discovery usable by navigation, with parsing delegated below the raw-content guard. Named paths, symbolic links, and a distinct Windows 8.3 alias passed. (✅ done)
- Keep `AccessFileRequired` as the existing configuration name, now documented as requiring a valid logical policy. In canonical-only mode, a navigation-only file doesn't satisfy it; in compatibility mode, a valid legacy policy can. Preserve configured floors, anonymous behavior, and same-named permissions in different spaces. The policy fixtures and anonymous browser scenarios pass. (✅ done)
- On metadata changes, invalidate the containing directory scope, related navigation/metrics entries, and the live-update version using existing invalidation and hub mechanisms. A literal file-key invalidation must not be mistaken for descendant-scope invalidation. Local and simulated remote races, plus anonymous browser refresh, pass. (✅ done)

**Completion:** both metadata consumers share one provider; old `access`/`folder-meta` cache entries cannot deserialize into the new model; raw content, rendered pages, navigation, counts, and live updates retain the same whole-space visibility in deterministic checks and the observed anonymous run. Signed-in browser acceptance remains in phase 5.

### 4. Migrate repository authoring and publication consumers (✅ done)

**Depends on:** phases 1–3 and `G2-policy-migration-inventory`.

- Convert this repository's root `repository.metadata.yml` into root `metadata.yml`. Preserve every value; move `internal_peer` and `deliberately_public` beneath `repository`. If a root canonical file appears before execution, merge its distinct sections without replacing its existing data. The converted values match the baseline declaration. (✅ done)
- Update `.github/instructions/content-classification.instructions.md`, `.github/prompt-snippets/content-classification-and-split.md`, `.copilot/context/05.00-content-classification/00-classification-and-split-model.md`, `.github/agents/10.00-application-development/ad-documentation-author.agent.md`, and `.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md`. Load their applicable artifact instructions before editing; update required artifact metadata alongside the changes. All five consumers were updated. (✅ done)
- Make authoring resolve the canonical repository-root declaration, fall back to the old filename during transition, compare duplicate declarations semantically, and stop on conflict/read failure. Preserve public defaults, path-parallel companions, and peer environment-variable precedence. A nested folder must never change repository classification. In a manual walkthrough of the updated procedure, this assistant accepted equivalent reordered declarations and a legacy-only declaration, stopped on conflicting declarations and a locked companion, and left both existing targets unchanged. Missing and unrecognized visibility were treated as public and stopped at a missing required peer. A separate public case selected the environment-specified peer and wrote the path-parallel companion before the public page. This is a controlled synthetic walkthrough, not automated conformance testing of every authoring agent. (✅ done)
- Remove this repository's legacy declaration only after the updated consumers and field-preservation checks pass. Historical source discussions remain intact; a remaining old-name match must be classified as a legacy adapter, compatibility reference, or historical record, not blindly replaced. The root legacy file was removed; known stale generated pages await phase 6. (✅ done)
- Convert owned access fixtures under `eng/fixtures/access-tree/` into canonical source-root files, retaining legacy-only and conflicting cases under the new harness fixtures. Keep the existing nested-access scenario as a negative example, not working folder RBAC. The three owned root fixtures were converted. (✅ done)
- Extend `.github/workflows/03.PublishDocsContent.yml` to run `validate-tree` on staged metadata before upload. Stop the publish on invalid versioned YAML or unsupported policy placement. Update workflow path triggers for the schema/validator sources whose changes affect validation. Do not copy repository-root authoring data into a separate content subtree. The workflow prepares the SDK and pinned upstream packages before validation; its ordering and triggers were checked locally. (✅ done)
- Prove filesystem-source and staged-content parity using the synthetic trees. Keep the existing post-publish invalidation path; require a nonzero validation exit to prevent upload. Don't deploy to external resources as part of local proof. The valid staged copy passed and the invalid nested policy returned nonzero; no live Azure upload was attempted. (✅ done)

**Completion:** the root declaration has one canonical authority and authoring behavior is unchanged. Invalid staged policy cannot publish through this workflow. Any unconverted external source remains explicitly in compatibility mode; it isn't silently treated as public.

### 5. Prove behavior, efficiency, and safe cutover (🟡 todo)

**Depends on:** phases 1–4 and `G4-visible-authentication`.

- Run `dotnet run --project eng\metadata-checks\MetadataChecks.csproj -c Debug -- test`. Exercise real parser/provider code with counted physical-source fakes, principal fixtures, controlled invalidation, and serialization round trips. Any failed assertion blocks browser sign-off. The Debug and Release harnesses each passed 246 assertions, including comparisons of all 11 existing documentation metadata files and additional fenced/scalar legacy cases against the legacy parser. (✅ done)
- Run `dotnet build src\Diginsight.SmartDocs.slnx -c Debug`. Fix regressions introduced by this work; report unrelated baseline blockers without treating them as a pass. The isolated build passed with no warnings or errors. (✅ done)
- Run the rebuilt web server in a visible foreground console and validate in a visible browser. Use the repository's validation-sequence template and store the observed scenarios/screenshots under this work item's `_validation/` folder. Never substitute HTTP probes for observed browser behavior. The [validation sequence](_validation/01-metadata-refactry.validation-sequence.md) records 22 anonymous and 24 signed-in/local-sign-out scenarios across both builds, including 12 repeated on the final parser; its overall result remains incomplete. (✅ done)
- Exercise anonymous, signed-in role-free, Reader, Contributor, multiple-role, and unmapped-role cases against the synthetic spaces. Compare visible pages, direct assets, navigation, index results, totals, and live updates before and after conversion. One genuine session passed on the final parser, including a valid probe with no effective match for the registration's four defined, enabled role values; principal fixtures also passed. To do: establish the role-free case and observe Reader, Contributor, multiple-role, and unmapped-role cases using authorized sessions. (🟡 todo)
- For a fully inventoried local profile, remove migrated legacy files, set compatibility to `false`, invalidate caches, and rerun the same scenarios plus read/parse counts. If any source remains unknown, don't switch that profile; complete the canonical-only proof with the controlled fixture profile instead. The available signed-in session passed a controlled comparison of both modes and legacy-to-canonical allow/deny conversion; acquisition counters passed. To do: repeat the remaining role-assigned browser scenarios. (🟡 todo)
- Record compatibility-mode overhead separately from canonical-only results. Include root-policy revocation after the freshness window, restoration after unreadable storage, and invalidation while a read is in flight. Don't report synthetic counters as production latency measurements. For a present dual-policy root, the harness counted two origin reads and two parses in compatibility mode versus one read and one YAML parse in canonical-only mode; the specified freshness and race checks pass. (✅ done)

**Completion:** every required scenario is observed or asserted by its specified method. The unavailable role-assigned browser cases leave this phase and the plan incomplete.

### 6. Publish the supported contract and migration instructions (🟡 todo)

**Depends on:** phases 1–5 and `G3-documentation-evidence`.

- Update `src/docs/06.00-reference/04-folder-metadata-schema.md` as the canonical unified schema reference, preserving its path. Cover every field's type, default, scope, aliases, inheritance behavior, exposure, invalid forms, examples, and schema-version rules. (🟡 todo)
- Update `src/docs/06.00-reference/07-access-file-schema.md` to describe access semantics and legacy migration, linking the unified reference rather than duplicating its field catalog. Update `01-configuration-settings.md` and the reference `index.md` for the temporary compatibility setting and logical required-policy meaning. (🟡 todo)
- Refresh impacted security/architecture explanations and this work item's `overview.md` without rewriting unrelated historical analyses. Fetch any existing companion first. For generated reference pages, use their owning investigators to refresh evidence and update verification stamps/source sets with the body. (🟡 todo)
- Document conversion order, equal/conflicting duplicate behavior, canonical-only prerequisites, raw-metadata delivery changes, compatibility overhead, and rollback. Before starting an old host, restore its legacy repository/access carriers and original navigation-only metadata, then its matching configuration/cache state. An old host must neither interpret a new-only policy tree as unrestricted nor serve unified server-only sections. The [implementation reference](../../../../Diginsight.SmartDocs.Web/Metadata/metadata-reference.md) records these steps while generated pages remain stale. (✅ done)
- Link the schema and regression commands from the reference documentation. State that folder policies and fleet-wide legacy removal remain deferred, and preserve the recorded limits of the actual validation. The interim implementation reference contains these links and limits. (✅ done)

**Completion:** implementation and documentation agree. Missing evidence, broken links, or a statement claiming unimplemented folder authorization blocks completion.

## 🧪 Validation contract

### Acquisition and cache assertions

Counts below apply to a present canonical file, one scope, one host, and one cache generation unless stated otherwise.

| Identifier | Scenario | Required result |
|---|---|---|
| `T1-shared-cold-read` | Authorization and navigation load the same cold canonical scope | One origin content read, one YAML parse, one parsed cache entry. (✅ done) |
| `T2-shared-warm-read` | Repeat both consumers before expiry | Zero added origin reads/parses. (✅ done) |
| `T3-concurrent-fill` | Release 100 cold callers together | One coalesced read/parse on that host. (✅ done) |
| `T4-envelope-roundtrip` | Read a serialized snapshot as a second-host cache hit | No origin read/YAML parse; identical case-insensitive grants and navigation values. (✅ done) |
| `T5-root-freshness` | Remove a grant without invalidation; advance beyond the configured 30-second default | First subsequent authorization waits for refreshed policy and denies the removed grant. (✅ done) |
| `T6-invalidation-race` | Invalidate the metadata scope during an in-flight fill | An older fill cannot restore an accepted post-invalidation policy; apply an existing generation mechanism or a provider-local guard if the cache doesn't provide this. (✅ done) |
| `T7-complete-document` | Place valid policy after 8 KiB, then exceed 65,536 bytes | Complete valid file parsed; oversized file rejected, never truncated into a permissive policy. (✅ done) |
| `T8-scope-isolation` | Two mounts and a root mount use identical permission names | Distinct snapshots/grants; repository-root classification causes no out-of-source read. (✅ done) |

### Policy and compatibility matrix

Run each applicable row for both filesystem and blob-shaped fake sources. A fake proves the application contract, not live Azure behavior.

| Inputs | Required result |
|---|---|
| No policy; required flag off | Only configured site/space floors apply. (✅ done) |
| Canonical navigation file without access; required flag on | Deny the space. (✅ done) |
| Explicit `access: {}` versus `access: null` or a bare `access:` | Empty mapping is a valid explicit policy; null/bare values are invalid and deny the root space. (✅ done) |
| Invalid/unreadable canonical root, even with valid legacy access | Deny and diagnose; no permissive fallback. (✅ done) |
| Valid canonical policy plus invalid/unreadable legacy policy in compatibility mode | Deny and diagnose. (✅ done) |
| Equivalent canonical and legacy policies | Accept with a migration warning; compare normalized policy content, not YAML formatting. (✅ done) |
| Conflicting canonical and legacy policies | Deny; neither source silently wins. (✅ done) |
| Legacy-only policy with compatibility enabled | Preserve its effective permissions and requirements. (✅ done) |
| Canonical-only mode after validated conversion | No legacy reads; preserve converted access results. (✅ done) |
| Missing/unrecognized repository visibility | Public authoring classification, diagnostic, and existing peer safeguards; the manual synthetic walkthrough stopped when the required peer was missing. (✅ done) |
| Conflicting repository declarations or inaccessible companion | Abort authoring; don't overwrite or invent the companion. The manual synthetic walkthrough stopped on a visibility conflict and an actual locked-file read error without modifying either target. (✅ done) |
| Versioned access below the content root | Validator rejects unsupported placement; no claim of folder protection. (✅ done) |
| Raw metadata, aliased metadata path, or metadata used as branding | No raw policy/repository document delivered; the harness exercised a distinct 8.3 alias. (✅ done) |
| Folder response or hub update | Navigation projection only; no repository/access section or internal diagnostic. (✅ done) |

### Visible-browser evidence

The artifact is `_validation/01-metadata-refactry.validation-sequence.md`, with screenshots in `_validation/images/`, following the repository template and `publish: false` rule. Every scenario records preconditions, action, expected outcome, exact observed DOM/result, and a screenshot. Sensitive captures and deployment facts belong in the declared internal companion, never this public tree.

Required coverage includes public/protected pages, direct attachments, menus, search/index, totals, live updates after a policy change, missing/invalid required policy, and canonical-only versus compatibility behavior. These surfaces were observed anonymously and with one genuine signed-in session on the final parser; the role-free, Reader, Contributor, multiple-role, and unmapped-role browser cases remain pending. (🟡 todo)

## 📐 Coverage and exit criteria

The landings below close the user's goals without silently absorbing future work.

| Goal | Landing | Exit condition |
|---|---|---|
| Repository data in unified metadata | Root declaration and five authoring consumers in phase 4 | Field-preserving conversion; one authority; public/peer safeguards pass the controlled manual walkthrough. (✅ done) |
| Navigation and RBAC use the same file | Metadata provider and consumers in phases 2–3 | No independent unified-document read/parser/cache path. (✅ done) |
| Carefully documented supported schema | Machine schema and reference pages in phases 1 and 6 | Fields, scopes, failures, versions, and migration agree with tests. (🟡 todo) |
| Single load/cache efficiency | Harness and phase 5 evidence | Canonical-only read/parse targets pass; compatibility exceptions reported separately. (✅ done) |
| No proliferation of permanent carriers | Migration and documented retirement gate | Owned declarations/fixtures converted; no new authored sidecar family or replicated repository block. (✅ done) |
| Existing behavior preserved | Policy matrix and visible validation | Whole-space outcomes preserved; intentional raw-metadata restriction documented and proven. (🟡 todo) |

Mark the plan `done` only when all active phases and exit conditions pass. Missing external rollout approval doesn't authorize external changes; missing required local validation does prevent completion.

## 🗂️ Park lot

These items belong to metadata consolidation but are deliberately excluded from this implementation.

- `PL1-folder-requirements`: add monotonic ancestor requirements and update page/asset enforcement, indexes, counts, and hub filtering together. → defer. (📌 next steps)
- `PL2-folder-role-mappings`: decide whether folders can redefine grants, how scope is qualified, and how inherited restrictions remain protected. → defer. (📌 next steps)
- `PL3-legacy-retirement`: remove legacy readers and the temporary switch only after a supported-consumer inventory, zero remaining legacy policy dependencies, and successful canonical-only validation across participating profiles. → defer. (📌 next steps)
- `PL4-other-metadata-families`: consider article sidecars or the inactive `_metadata.yml` authoring-inheritance proposal only under a separately scoped requirement. This refactor doesn't activate either. → defer. (📌 next steps)

## 💡 Conclusion

The implementation has a defined end state: one canonical metadata carrier per directory, one runtime snapshot provider per scope, separate browser and authorization projections, and tested preservation of current policy behavior.

Temporary compatibility is explicitly not the single-read fast path. The canonical-only proof is mandatory, while external rollout and eventual removal of legacy support remain controlled follow-ups.

Previous: [Metadata consolidation analysis](01-metadata-refactry.analysis.md). Related: [Authentication configuration context](overview.md).

## 📚 References

- **[Metadata consolidation analysis](01-metadata-refactry.analysis.md)** 📘 [Design authority]. Records the inspected sources, selected trade-offs, schema shape, and cache guarantees. Revise it with any agreed contract change before changing dependent implementation steps.
- **[Plan execution rules](../../../../../.github/instructions/plan-execution.instructions.md)** and **[plan marking rules](../../../../../.github/instructions/plan-marking.instructions.md)** 📘 [Repository authority]. Govern readiness, lifecycle, and pending/done marking. They apply again when execution is requested.
- **[Testing and validation rules](../../../../../.github/instructions/testing-validation.instructions.md)** and **[validation-sequence template](../../../../../.github/templates/output-validation-sequence.template.md)** 📘 [Repository authority]. Define visible console/browser execution and required evidence. Compilation and HTTP probes don't replace that evidence.
- **[Repository documentation rules](../../../../../.github/instructions/repository-docs.instructions.md)** 📘 [Repository authority]. Govern evidence-backed reference-page updates and companion containment. Generated pages cannot retain stale verification stamps.
- **[Access schema](../../../06.00-reference/07-access-file-schema.md)** and **[folder schema](../../../06.00-reference/04-folder-metadata-schema.md)** 📘 [Repository documentation]. These generated pages describe the historical baseline and are stale until their evidence is regenerated.
- **[Unified metadata implementation reference](../../../../Diginsight.SmartDocs.Web/Metadata/metadata-reference.md)** 📘 [Repository source]. Documents the implemented contract and interim migration instructions.

<!--
validations:
  structure: {status: "passed", last_run: "2026-11-07", notes: "Local links, heading anchors, YAML syntax, status-marker conventions, and whitespace checked; dependencies and scope reviewed against the analysis."}
  actionability: {status: "passed", last_run: "2026-11-06", notes: "Scope choices delegated; bounded discovery records remaining execution blockers."}
  implementation: {status: "partial", last_run: "2026-11-07", notes: "Core implementation, controlled authoring walkthrough, and one signed-in session pass; role-assigned browser and generated evidence remain pending."}
article_metadata:
  filename: "01-metadata-refactry.plan.md"
  created: "2026-11-06"
  last_updated: "2026-11-07"
-->
