---
title: "Unifying repository, navigation, and access metadata"
date: "2026-11-06"
type: explanation
description: "A scoped metadata.yml contract, its migration boundaries, and measurable requirements for shared loading and caching."
---

# Unifying repository, navigation, and access metadata

## 🎯 Introduction

**Yes: `metadata.yml` can also carry the information formerly in `repository.metadata.yml`.** The useful end state is one standard metadata filename per directory, with explicitly scoped sections, not one enormous file describing the entire repository tree.

The request is clear in intent: reduce metadata-file proliferation, document the supported schema carefully, and share acquisition and caching. It wasn't initially unambiguous about physical location, folder authorization, or migration. Those choices have now been resolved for the [implementation plan](01-metadata-refactry.plan.md).

This hand-authored analysis preserves the pre-refactor baseline and selected design. Implementation is in progress: the shared provider, owned fixtures, and repository declaration have been converted, and the [unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md) records the current contract. Anonymous scenarios, deterministic checks, and one genuine signed-in session passed; the remaining role-assigned browser cases and evidence-backed reference regeneration are incomplete.

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🔍 Baseline metadata responsibilities](#-baseline-metadata-responsibilities)
- [🧭 Request clarity and selected scope](#-request-clarity-and-selected-scope)
- [🏗️ Unified schema and authority](#-unified-schema-and-authority)
- [⚡ Loading, caching, and efficiency](#-loading-caching-and-efficiency)
- [🔐 Failure and exposure boundaries](#-failure-and-exposure-boundaries)
- [🔄 Compatibility and migration](#-compatibility-and-migration)
- [📏 Complexity and acceptance boundaries](#-complexity-and-acceptance-boundaries)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🔍 Baseline metadata responsibilities

Before the refactor, the three filenames had different consumers. Consolidating their syntax doesn't make those consumers interchangeable.

| Baseline carrier | Responsibility | Consumer before refactoring |
|---|---|---|
| Repository-root `repository.metadata.yml` | Repository identity, publication classification, internal-peer resolution, and deliberately public exceptions | Documentation instructions, context, the classification snippet, and authoring artifacts |
| Folder `metadata.yml` | Navigation labels, icons, order, visibility, and metric seeds | The server's content lister, navigation builder, and folder-record provider |
| Content-root `access.metadata.yml` | Declared permissions, required permissions, and Entra-role-to-permission mappings | Access-file provider, requirement resolver, and visibility evaluator |

The former repository declaration contained `repository.id`, `repository.visibility`, `internal_peer`, and `deliberately_public`. The converted [root declaration](../../../../../metadata.yml) preserves those values under `repository`. No executable consumer of the former filename was found in `src/` or `scripts/`; its active consumers were authoring guidance, notably the [classification instruction](../../../../../.github/instructions/content-classification.instructions.md) and [classification procedure](../../../../../.github/prompt-snippets/content-classification-and-split.md).

**Classification and authorization are independent.** `repository.visibility: private` controls how documentation is authored; it neither changes GitHub repository permissions nor grants access through SmartDocs. Runtime access still depends on authentication, deployment floors, and the content policy.

The baseline [folder parser](../../../../Diginsight.SmartDocs.Web.Shared/Navigation/FolderMeta.cs) used a regex over flat lines and collected matching keys into `Values`. A nested policy couldn't safely be inserted into that browser-facing projection unchanged. The implemented provider instead reads the whole document, parses its YAML once, and sends an explicit navigation-only projection to [FolderRecordProvider](../../../../Diginsight.SmartDocs.Web/Navigation/FolderRecordProvider.cs). A simple flat unversioned document also uses an in-memory legacy pass to preserve its former fence, scalar, and repeated-key behavior.

The baseline [access parser](../../../../Diginsight.SmartDocs.Web/Authorization/AccessFile.cs) used YamlDotNet, rejected invalid policies, and limited input to 65,536 bytes. The implemented model also retains descriptions as metadata while continuing to evaluate authorization using names.

### Root boundaries explain the apparent duplication

The [publish workflow](../../../../../.github/workflows/03.PublishDocsContent.yml) copies only its selected content subtree, normally `src/docs`. It doesn't publish the repository-root declaration. A runtime reader of that subtree cannot discover a parent file that was never uploaded.

Nor does navigation guarantee a root metadata read. The virtual site's root folder record comes from site settings, and navigation scores configured space mounts separately. Authorization therefore needs an explicit content-root lookup independent of menu construction.

The [earlier authorization analysis](../../202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.analysis.md) justified a separate access file because folder parsing was flat, folder values reached browsers, and repository metadata wasn't published. These are implementation and deployment boundaries, not a permanent requirement for separate filenames.

## 🧭 Request clarity and selected scope

The user was asked about the three choices below. The response delegated pragmatic selection rather than supplying individual preferences. These are recorded defaults for this plan, not independently approved deployment changes.

| Decision | Selected contract | Why |
|---|---|---|
| `D1-metadata-authority` | One `metadata.yml` per directory/scope; repository-root authoring data and content-root runtime data remain at their respective roots when those differ | Avoids duplicate declarations, root discovery protocols, and publishing private authoring configuration unnecessarily |
| `D2-folder-rbac-scope` | Consolidate current whole-space authorization; defer inherited folder requirements | Avoids silently expanding this refactor into new navigation, count, index, and push semantics |
| `D3-legacy-transition` | Temporary legacy readers, explicit conflict detection, and a canonical-only operating mode | Preserves existing deployments while making the single-read end state testable |

With those choices, the implementation request is **clear, bounded, and actionable**:

> Use a documented, versioned `metadata.yml` format for repository declarations, folder navigation, and current space access policy. Route runtime metadata consumers through one parsed snapshot cache per scope. Preserve classification and authorization safeguards, migrate existing consumers, and provide an explicit compatibility exit.

This does **not** mean moving all configuration into metadata. Identity-provider settings, deployment connection settings, article front matter, bottom validation records, and proposed authoring-inheritance mechanisms remain outside this refactor.

## 🏗️ Unified schema and authority

### One filename, sections with different meanings

The selected document version is `schema: smartdocs-metadata/v1`. It is one envelope version; it replaces the legacy repository schema string and access schema integer rather than layering two independent version switches inside one file.

Existing navigation keys remain at the top level to minimize migration. Two structured sections carry the other concerns:

| Section or key | Scope | Meaning |
|---|---|---|
| `schema` | Document | Supported unified format version |
| Existing navigation keys and aliases | Local directory | Existing display overrides and metric seeds; no new inheritance |
| `repository.id`, `repository.visibility` | Repository root | Repository identity and authoring classification |
| `repository.internal_peer` | Repository root | Existing peer-resolution fields, unchanged in meaning |
| `repository.deliberately_public` | Repository root | Existing classification exceptions |
| `access.permissions`, `access.roles`, `access.requires` | Physical content root of a space | Existing permission and role semantics |

All former repository fields fit without a new carrier. `internal_peer` and `deliberately_public` move beneath `repository`; `repository.id` and `repository.visibility` keep their existing paths.

The following is an example of the implemented shape for a repository whose repository root is also its content root. The identifiers are illustrative.

```yaml
schema: smartdocs-metadata/v1
label: Documentation
icon: book

repository:
  id: example/docs
  visibility: public
  internal_peer:
    id: example/docs-internal
    path_env: DOCS_INTERNAL_FOLDER
    default_relative_path: "../docs-internal"
    mirror_strategy: path-parallel
    suffix: ".internal.md"
    on_missing: fail-closed
    alias_registry: "src/docs/_aliases/{environment}.aliases.internal.md"
  deliberately_public:
    - "The public project name"

access:
  requires: [Read]
  permissions:
    Read: Read the documentation
    Write: Contributor capability
  roles:
    Reader: [Read]
    Contributor: [Read, Write]
    Admin: [Read, Write]
```

`Write` remains a named permission, not a new editing feature. Roles are still assigned through Entra, not through this file.

### Different roots do not justify copying declarations

For a source-code repository serving only its documentation subtree, the layout is instead:

```text
metadata.yml                         repository section
src/docs/metadata.yml                access + optional root navigation
src/docs/03.00-architecture/metadata.yml
                                    local navigation overrides
```

There is no repeated repository section in the content subtree. The root declaration is read by authoring consumers; the web host reads only metadata within its configured content source. If the roots coincide, their sections occupy the same file.

Consequently, unification means **at most one authored metadata carrier per directory**, not exactly one file per repository. A directory without metadata overrides needs no file. No central path registry or materialized per-article metadata copy is introduced.

### Schema evolution without a generic merge engine

Versioned documents use structured YAML, duplicate-key detection, explicit types, and rejection of unknown structural keys. An empty mapping (`access: {}`) adds no requirement but is an explicit valid policy, so it satisfies the required-policy flag. A missing section doesn't satisfy that flag. An explicitly supplied section must be a mapping, not `null` or a scalar; `access:` without a value is invalid, not equivalent to `{}`.

Permission-name validation, reserved `authenticated`, case-insensitive role/permission identity, all-of requirements, and within-space grant union remain unchanged. Conflicting navigation aliases in a versioned document are invalid rather than dependent on key order.

Existing unversioned, flat navigation files remain a supported navigation-only input. Their known aliases, optional surrounding fences, and safe scalar custom values retain their existing projection. Legacy scalar syntax follows the former flat parser, so authors must review values such as inline `#` text or `null` when converting to structured YAML. These files cannot acquire structured repository or access meaning without declaring the unified schema. New versioned files expose only documented navigation fields; migration must report custom keys it cannot represent rather than discard them.

Folder RBAC is explicitly **not supported by this version** below a space's content root. Schema validation rejects such a declaration, publishing validation rejects the content set, and a runtime encounter reports the unsupported scope. There is no promise that a nested declaration protects a subtree: direct requests still use the whole-space policy. Future support requires ancestor evaluation and corresponding filters for pages, assets, navigation, counts, and live updates.

## ⚡ Loading, caching, and efficiency

### Baseline reuse was real but incomplete

Before the change, [CachedContentSource](../../../../Diginsight.SmartDocs.Web/ContentSources/CachedContentSource.cs) cached parsed folder data under `folder-meta`, while [AccessFileProvider](../../../../Diginsight.SmartDocs.Web/Authorization/AccessFileProvider.cs) cached access outcomes under `access`. Both used SmartCache and the same path-invalidation infrastructure. The duplication was in acquisition, parsing, and cached models, not in two independent cache engines.

The current defaults are five minutes for structural metadata and 30 seconds for access policy. The shorter interval bounds policy-change visibility after a missed invalidation. It doesn't refresh Entra role assignments in an existing authentication cookie.

The baseline folder path used an article-head reader. [FrontMatter.ReadHeadAsync](../../../../Diginsight.SmartDocs.Web.Shared/Navigation/FrontMatter.cs) may stop after a few kilobytes when there are no front-matter fences. The unified provider instead reads the complete file with an explicit size rejection, not silent truncation of a role or repository section near the end.

### Shared acquisition, separate consumers

The implemented server flow is:

```text
Physical source + normalized metadata path
                 |
      ContentMetadataProvider
      complete read + YAML parse
      immutable MetadataDocument outcome
                 |
        one SmartCache snapshot
          /                 \
 navigation projection    access policy
 browser-safe values      per-request evaluation
```

The provider is below navigation and authorization. The access-provider facade delegates to this provider and owns no second read or cache. Folder-lister methods likewise project from this snapshot rather than parse again. For a simple flat legacy input, the provider performs an additional in-memory navigation pass but no additional source read, YAML parse, or parsed cache.

Cache identity uses the existing mounted content-path namespace, a new serialized-model version, and compatibility mode. Different spaces and modes cannot share an entry accidentally. The distributed envelope restores case-insensitive comparisons and contains no principal-derived permissions or visibility decisions.

Content-root snapshots use the access freshness bound, including roots currently lacking an access section. Otherwise adding a restriction to a previously public root could wait for the longer navigation tolerance. Ordinary folder snapshots retain the structural bound. The same root snapshot is used by both consumers regardless of which asks first.

Authorization waits for required refreshes and never reads policy from a stale-while-revalidate folder record. Navigation can retain its existing derived caches, but those caches contain no authorization verdicts. Requirements and grants come from the same snapshot.

### What “single load” promises

The measurable promise applies to a **present canonical document, one scope, one host, one cache-fill generation**:

| Scenario | Required acquisition result |
|---|---|
| Cold canonical-only load used by authorization and navigation | Exactly one content-file read and one YAML parse |
| Repeated consumers within the freshness window | Zero additional origin reads and zero additional parses |
| 100 concurrent cold callers on one host | One coalesced content read and one YAML parse |
| Distributed-cache hit on another host | Zero origin reads and zero YAML parses; envelope deserialization is expected |
| First use after root policy freshness expires | A fresh snapshot is required before an authorization verdict is returned |
| Canonical file absent | One negative file probe; at most one additional directory listing to detect misnamed metadata |

The deterministic harness verifies these counts, including 100 coalesced callers and an ordinary second-host serialized cache hit. After a relevant invalidation, a foreign-origin snapshot is instead revalidated at the physical source to avoid accepting an older policy. Simultaneous cold misses on different hosts aren't claimed to have a global single-flight guarantee, and simulated companion delivery isn't a live Service Bus or Redis test.

There are three explicit exceptions to a literal “one read” claim:

- Different scopes require different snapshots; future ancestor policies would require cached ancestor lookups.
- Compatibility mode may read the canonical and legacy carriers to detect conflicting declarations.
- Authoring agents and the web host are separate processes and consumers. One format doesn't create a cache shared between them.

Ordinary authorization doesn't scan the navigation tree. Hub connections and whole-site invalidations do compute a fingerprint over visible navigable metadata. The derived fingerprint is coalesced and cached locally for at most the shorter access or structure freshness, and invalidation forces a new calculation. A synthetic 67-folder test measured one 68-listing cold walk and zero added listings, reads, or parses for 100 warm calls; it isn't a production latency measurement. Publish-time schema validation separately scans the staged metadata files.

## 🔐 Failure and exposure boundaries

The combined file holds sections with different consumers, not sections with interchangeable trust.

| Condition | Required behavior |
|---|---|
| Repository visibility absent or unrecognized | Authoring assumes `public`, reports the condition, and retains the existing peer-resolution safeguards |
| Repository declaration unreadable or structurally invalid | Stop authoring; never infer private status or silently recreate an inaccessible companion |
| Root metadata or access section absent, policy optional | Apply the site default and space floor |
| Root access section absent, policy required | Deny the space even if a navigation-only metadata file exists |
| Root unified document or selected policy invalid/unreadable | Deny the space and report the failure |
| Unknown permission, unsupported schema, or conflicting dual policy | Deny rather than silently fall back to a more permissive source |
| Storage fault during refresh | Don't retain an expired authorization verdict or cache the fault as successful absence |

The existing `AccessFileRequired` option remains accepted in this refactor, with its meaning documented as “a valid logical access policy is required.” Canonical mode satisfies it through `access`; compatibility mode can satisfy it through a valid legacy file. A second synonymous configuration property isn't needed.

Raw unified metadata and both legacy metadata carriers are server-internal through SmartDocs content endpoints. File-key checks, filesystem aliases, branding paths, and head reads must preserve this boundary. Internal navigation discovery may still see a canonical filename; it must use the dedicated metadata provider rather than a blocked raw-content read.

Browser folder records and hub messages contain only the navigation projection. They must not serialize repository peer details, classification exceptions, access definitions, or policy diagnostics. Blocking raw unified metadata is an intentional change from the baseline's raw folder-metadata availability; the anonymous [validation sequence](_validation/01-metadata-refactry.validation-sequence.md) records the observed boundary.

This protects the SmartDocs serving path, not a separate website, public repository, or blob endpoint. Metadata isn't a place for credentials, and a server-side delivery rule cannot make publicly committed data private.

## 🔄 Compatibility and migration

### One temporary compatibility boundary

A temporary `Site:LegacyMetadataCompatibility` setting defaults to `true` so installing the new host doesn't make existing role-gated spaces lose their policies. Setting it to `false` enables canonical-only runtime reads. The switch affects source selection, not whether authorization is enforced.

In compatibility mode, one provider reads the relevant root carriers and stores one combined outcome. Missing canonical policy can use legacy access; equivalent duplicate policies are accepted with a migration warning; conflicting, invalid, or unreadable policy sources deny the space. A valid canonical policy must not conceal a malformed legacy policy.

Canonical-only mode doesn't probe legacy access files. Switching modes therefore requires a validated source inventory, conversion of every participating policy, removal of legacy carriers, and explicit cache invalidation. Required-policy configuration alone isn't enough to prove safe cutover: an optional legacy file may currently restrict a space too.

For authoring, the classification procedure reads repository-root `metadata.yml` first, falls back to the legacy repository declaration during transition, and rejects conflicting declarations. Its existing public default and fail-closed peer behavior survive the filename change. These are authoring reads, not a new runtime repository loader.

### Migration reaches more than C#

The following active authoring consumers need coordinated updates:

- [Content classification instructions](../../../../../.github/instructions/content-classification.instructions.md).
- [Classification procedure](../../../../../.github/prompt-snippets/content-classification-and-split.md).
- [Classification model](../../../../../.copilot/context/05.00-content-classification/00-classification-and-split-model.md).
- [Documentation author](../../../../../.github/agents/10.00-application-development/ad-documentation-author.agent.md).
- [Issue-analysis prompt](../../../../../.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md).

The runtime schema references, access fixtures, required-policy documentation, and publication validation also change. Historical analyses remain historical; they aren't rewritten to pretend the new format was always supported.

The selected per-scope model requires no copy of repository authoring data into `src/docs` and no new root-file publish trigger solely for classification changes. If a deployment publishes a repository root, ordinary content staging naturally includes its combined document, whose server-only sections remain excluded from browser delivery.

Compatibility removal is a follow-up gated on a supported-consumer inventory and successful canonical-only validation for every participating source. This plan doesn't claim that unknown external repositories have been migrated.

Rollback must restore both the legacy policy carriers and the original navigation-only metadata before starting an old host. Merely restoring the access file isn't enough: an old host doesn't understand the unified document and doesn't apply the new raw-metadata exclusion. The rollback procedure must restore the matching host, content, configuration, and cache state together.

## 📏 Complexity and acceptance boundaries

The design reduces **authored carriers and duplicated runtime plumbing**. It deliberately doesn't promise zero internal model types or one cache object for unrelated scopes.

| Complexity dimension | Boundary |
|---|---|
| Filenames | One canonical `metadata.yml` per directory; legacy names are transitional, not additional permanent authorities |
| Versions | One unified document version |
| Inheritance | None added in this refactor |
| Parsing | One runtime YAML parser and section validators; a simple flat legacy input adds an in-memory compatibility pass, while versioned documents don't |
| Caching | One parsed runtime snapshot per scope/mode; existing derived navigation caches remain |
| Authoring authority | Root repository section only; never nearest-folder classification |
| Publication | Validate and copy the selected subtree; don't synthesize a second authored repository declaration |
| Extension | Add documented fields through schema evolution, not arbitrary includes, path catalogs, or merge directives |

The cost is bounded but real: a navigation-only edit to a combined root document participates in the shorter root freshness cycle, and malformed root YAML can deny the whole space. That is the selected trade-off for atomic metadata and fail-closed policy, not an accidental fallback.

The inactive `_metadata.yml` authoring-inheritance proposal and article-sidecar template are not additional runtime standards. Activating or merging them would expand the goal; neither is introduced here.

The plan's proof must cover equal public and protected behavior before and after migration, exact read/parse counts, missing/invalid/unreadable cases, dual-source conflicts, root-mount isolation, browser-safe projections, and source-only publishing parity. A build proves none of those on its own.

## 💡 Conclusion

The consolidation is sensible and actionable with explicit scope. Repository declarations can join the same schema and filename without becoming runtime authorization or being copied into every content root.

The efficiency benefit comes from the shared provider and cached snapshot, not the filename alone. Canonical-only operation has a precise single-read target; transitional dual-source reads are a disclosed migration cost. Folder authorization remains a separately bounded extension rather than hidden complexity in this refactor.

Previous: [Authentication configuration context](overview.md). Next: [Metadata refactoring plan](01-metadata-refactry.plan.md).

## 📚 References

- **[Repository declaration](../../../../../metadata.yml)** 📘 [Repository source]. Defines the migrated identity, classification, peer, and public-exception fields.
- **[Classification procedure](../../../../../.github/prompt-snippets/content-classification-and-split.md)** 📘 [Repository authority]. Defines canonical root discovery, the legacy fallback, public defaults, and fail-closed companion handling.
- **[Publish workflow](../../../../../.github/workflows/03.PublishDocsContent.yml)** 📘 [Repository source]. Establishes subtree-only staging and explicit cache invalidation. It explains why repository-root data isn't automatically available at runtime.
- **[Folder parser](../../../../Diginsight.SmartDocs.Web.Shared/Navigation/FolderMeta.cs)** and **[folder record provider](../../../../Diginsight.SmartDocs.Web/Navigation/FolderRecordProvider.cs)** 📘 [Repository source]. Define legacy flat parsing and the current browser-safe navigation projection.
- **[Folder cache](../../../../Diginsight.SmartDocs.Web/ContentSources/CachedContentSource.cs)**, **[access provider](../../../../Diginsight.SmartDocs.Web/Authorization/AccessFileProvider.cs)**, and **[freshness options](../../../../Diginsight.SmartDocs.Web/Caching/ContentFreshnessOptions.cs)** 📘 [Repository source]. Define shared-provider delegation and the configured freshness bounds.
- **[Access parser](../../../../Diginsight.SmartDocs.Web/Authorization/AccessFile.cs)** and **[requirement resolver](../../../../Diginsight.SmartDocs.Web/Authorization/AccessRequirementResolver.cs)** 📘 [Repository source]. Establish strict policy validation, required-file behavior, and additive space-level requirements.
- **[Content-path invalidation](../../../../Diginsight.SmartDocs.Web/Caching/ContentPathCacheKey.cs)** 📘 [Repository source]. Defines ancestor/descendant invalidation. Metadata changes must invalidate the containing scope, not assume a literal metadata-file path reaches every descendant cache.
- **[Unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md)** 📘 [Repository documentation]. Documents the implemented fields, migration, cache trade-offs, and current validation limits.
- **[Folder schema reference](../../../06.00-reference/04-folder-metadata-schema.md)** and **[access schema reference](../../../06.00-reference/07-access-file-schema.md)** 📘 [Repository documentation]. These refreshed drafts use locally regenerated evidence and remain unverified pending the documented gates.

<!--
validations:
  structure: {status: "passed", last_run: "2026-11-07", notes: "Local links, heading anchors, YAML syntax, status-marker conventions, and whitespace checked; proposal reviewed against the plan."}
  sources: {status: "reviewed", last_run: "2026-11-06", notes: "Static source and authoring-consumer inspection; no runtime or performance claims verified."}
  implementation: {status: "partial", last_run: "2026-11-07", notes: "Shared metadata, anonymous cases, and one genuine signed-in session checked; role-assigned browser and generated-reference evidence remain incomplete."}
article_metadata:
  filename: "01-metadata-refactry.analysis.md"
  created: "2026-11-06"
  last_updated: "2026-11-07"
-->
