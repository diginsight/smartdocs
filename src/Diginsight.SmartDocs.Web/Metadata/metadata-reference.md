---
title: "Unified metadata reference"
date: "2026-11-06"
type: reference
description: "The supported SmartDocs metadata contract, access semantics, cache boundaries, and migration procedure."
---

# Unified metadata reference

SmartDocs uses one canonical `metadata.yml` per directory. Navigation fields describe that directory; a content-root `access` section defines whole-space authorization; and a repository-root `repository` section guides documentation classification. These sections share a format, not a single scope or a common security meaning.

The [machine-readable schema](metadata.schema.json) describes the structural envelope. The host parser and policy validator also enforce the placement and semantic rules below. This hand-written reference documents the implementation while the evidence-backed reference pages await regeneration.

## 📚 Table of contents

- [📍 Placement and version](#-placement-and-version)
- [🧭 Navigation fields](#-navigation-fields)
- [🗂️ Repository classification](#-repository-classification)
- [🔐 Space access policy](#-space-access-policy)
- [🧪 Validation and failure](#-validation-and-failure)
- [⚡ Loading and caching](#-loading-and-caching)
- [🔄 Migration and rollback](#-migration-and-rollback)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 📍 Placement and version

Use the exact lowercase filename `metadata.yml`. Omit the file when a directory needs no overrides.

| Placement | Supported content | Authority |
|---|---|---|
| Repository root | `repository`, with optional navigation and access when this is also a content root | Authoring classification comes only from this root |
| Physical content root of a space | `access` and optional navigation; a repository mapping is syntactically permitted but doesn't change the repository-root authoring authority | Access applies to the whole space |
| A folder below the content root | Navigation fields only | Applies to this folder's presentation; no folder access inheritance |

Repository and content roots can be different physical directories. Don't copy the repository declaration into a content subtree merely to combine filenames. If both roots coincide, their sections can share the same file.

Structured documents declare `schema: smartdocs-metadata/v1`. The supported version is exact; unknown versions, unknown versioned keys, duplicate mapping keys, and multiple aliases for the same navigation field are invalid. Versioned keys use their documented lowercase spellings. All files must be UTF-8 and no more than 65,536 bytes. A versioned file must contain one YAML mapping; even a trailing empty YAML document is invalid. Empty unversioned content means no overrides.

Unversioned flat navigation files remain supported, including the former optional surrounding `---` fences. Multiple nonempty YAML documents remain invalid. Simple flat files retain the old parser's ordering, invalid-value behavior, and literal scalar interpretation: for example, `label: Example # note` includes `# note` in the label, and `short: null` supplies the text `null`. They may carry scalar custom keys, except reserved access and repository names, which are excluded from the navigation projection and produce a warning. A structured `access` or `repository` section requires the versioned envelope. Review legacy scalar values and custom fields deliberately before switching to the strict versioned form.

## 🧭 Navigation fields

Fields are local overrides; an omitted or null value uses the existing navigation defaults. Text fields accept scalar values. Numeric, Boolean, and date fields must have valid values in a versioned document. Don't supply more than one alias for a field.

| Field | Aliases | Type and effect |
|---|---|---|
| `label` | `nav-label` | Scalar sidebar label; empty means no override |
| `short` | `nav-short` | Scalar compact/top-bar label; empty means no override |
| `icon` | `nav-icon` | Scalar icon identifier; empty means no override |
| `order` | `nav-order` | Finite number or numeric string; overrides name-derived ordering |
| `hidden` | `nav-hidden` | Boolean or `true`/`false` string; default `false`, excludes the folder from navigation |
| `topbar-hidden` | `nav-topbar-hidden`, `hidden-topbar` | Boolean or `true`/`false` string; default `false`, hides only the top-bar entry |
| `topbar-align` | `nav-topbar-align` | `left` or `right`, case-insensitive; otherwise the navigation default applies when omitted |
| `article-count` | `nav-article-count`, `articles` | 32-bit integer or integer string; a seed superseded by computed counts |
| `latest-article` | `nav-latest-article`, `updated` | Parseable date/time scalar; a seed superseded by computed data |

The server projects only navigation values to folder responses and hub records. Navigation visibility isn't an authorization policy: don't use `hidden` to protect content.

## 🗂️ Repository classification

The `repository` value is a mapping with these optional fields. It is authoring metadata, not a grant or runtime space-visibility rule.

| Field | Type | Meaning |
|---|---|---|
| `id` | Scalar | Repository identity used by authoring guidance |
| `visibility` | Scalar | `private` skips the public/internal split; `public` applies it. Missing or unrecognized values are diagnosed and treated as public |
| `internal_peer` | Mapping | Describes the path-parallel companion repository |
| `deliberately_public` | Sequence of non-null scalars | Classification exceptions explicitly allowed in public text |

The optional `internal_peer` mapping accepts scalar `id`, `path_env`, `default_relative_path`, `mirror_strategy`, `suffix`, `on_missing`, and `alias_registry`. The classification procedure resolves the configured environment variable first and then the relative path. A configured fail-closed missing peer stops authoring. Unknown mapping keys are invalid.

The canonical root declaration takes precedence over the transitional `repository.metadata.yml` fallback. If both declare repository information, normalized values must agree; a conflict or read failure stops authoring. A nested directory cannot override classification.

## 🔐 Space access policy

At a space's physical content root, `access` must be a mapping:

```yaml
schema: smartdocs-metadata/v1
label: Example documentation
access:
  permissions:
    Read: Read the documentation
    Write: Contributor capability
  requires: [Read]
  roles:
    Reader: [Read]
    Contributor: [Read, Write]
```

| Field | Type and default | Meaning |
|---|---|---|
| `permissions` | Mapping of names to scalar descriptions, or null; omitted/null/empty declares none | Declares names that requirements and role mappings may use; descriptions don't grant access |
| `requires` | Sequence of permission names, or null; omitted/null/empty requires none from this section | Every listed requirement must be satisfied |
| `roles` | Mapping of role values to permission-name sequences, or null; omitted/null/empty maps none | Grants from all of the signed-in reader's mapped roles are unioned within this space; a null or empty grant list grants nothing |

Permission and role names begin with an ASCII letter and contain only ASCII letters, digits, `.`, `_`, or `-`. Matching is case-insensitive. Case-only duplicate permission or role names are invalid. Requirements and grants must name declared permissions, except that `authenticated` may be required as a built-in; it can't be declared or granted by a role. Empty or null sequence entries are invalid.

The deployment's site default and space floor are additive to file requirements. They accept only an empty value or `authenticated`. A permission named `Write` doesn't create an editing API. Grants are scoped to one space, even when two spaces use the same permission name.

`access: {}` is a valid, explicit policy with no additional requirements. It satisfies `AccessFileRequired`. A missing `access` section doesn't satisfy that setting; `access: null` and a bare `access:` are invalid. Versioned access below a content root is rejected by staged-tree validation and doesn't protect a folder at runtime.

## 🧪 Validation and failure

| State | Runtime result |
|---|---|
| Policy missing and `AccessFileRequired: false` | Apply the site and space floors |
| Policy missing and `AccessFileRequired: true` | Hide the space |
| Invalid or unreadable canonical root metadata, or selected invalid/unreadable legacy policy | Hide the space and log a diagnostic |
| Valid policy | Evaluate its requirements together with the configured floors |
| Unreadable source | Don't cache the failure as a successful absence; retry on a subsequent request |

Canonical and legacy metadata carriers are excluded from public raw and head delivery, including recognized filesystem aliases and branding paths. The internal provider still reads canonical files directly. These application guards don't make data private if it is independently published in a public repository or storage endpoint. Never put credentials in metadata.

Run the same parser and publication checks from the repository root:

```powershell
dotnet run --project eng\metadata-checks\MetadataChecks.csproj -c Debug -- test
dotnet run --project eng\metadata-checks\MetadataChecks.csproj -c Debug -- validate-tree src\docs
```

`validate-tree` rejects invalid canonical metadata, misplaced or invalid legacy access files, and conflicting root policies. It doesn't know each deployment's `AccessFileRequired` setting or prove that every external source has been migrated. Before validating and uploading the staged tree, the publish workflow checks the runner's .NET SDK and prepares the pinned upstream package releases used by the validator.

## ⚡ Loading and caching

The host reads and parses a complete canonical file once into a shared snapshot per mounted scope and compatibility mode. Navigation receives a safe projection; authorization evaluates the policy from the same snapshot. A simple unversioned flat file also takes an in-memory legacy navigation pass to preserve its old behavior; it doesn't trigger another file read, YAML parse, or cache. A warm snapshot adds no physical read or YAML parse. One hundred concurrent cold callers on one host are coalesced in the regression harness. This isn't a global single-flight guarantee across hosts.

Content-root snapshots use `Diginsight:SmartCache:SmartDocs:Access`, 30 seconds by default, even when no access section exists. Ordinary folder snapshots use the structural freshness setting, five minutes by default. These are cache freshness bounds on subsequent reads, not a promise that an existing sign-in cookie receives newly assigned roles.

Publish invalidation evicts affected content, navigation, and metadata branches. It also triggers role-filtered browser refreshes. The host doesn't watch content-store changes automatically, and a remote host can react only after it receives a companion event. After a relevant remote invalidation, a foreign-origin metadata snapshot is revalidated at the physical source to avoid accepting an older policy; this can reduce cross-host cache reuse.

For hub refresh filtering, a host computes a derived fingerprint of each visible space's navigable metadata tree. That cold walk uses cached listings where available; its local fingerprint is coalesced and retained no longer than the shorter access or structure freshness. Invalidation forces a new fingerprint, and unreadable outcomes aren't retained. A synthetic 67-folder harness run checks that 100 warm fingerprint requests add no listings, metadata reads, or parses; it isn't a production latency measurement.

## 🔄 Migration and rollback

1. Inventory every participating content root, including optional legacy policies and required-policy settings. Keep compatibility enabled for unknown sources.
2. Move each root policy's `permissions`, `requires`, and `roles` under `access` in a versioned canonical file. Preserve existing navigation fields and validate the tree. Move repository classification only to the repository root.
3. With `Site:LegacyMetadataCompatibility: true`—the default—the provider can read both carriers. A legacy-only policy is used with a warning; equivalent dual policies are accepted with a warning; conflicting, invalid, or unreadable selected sources fail closed. Compatibility may need an additional read and parse.
4. Remove legacy files only after all participating policies have been converted and checked. Set `Site:LegacyMetadataCompatibility: false`, invalidate caches, and repeat behavior and acquisition checks. Canonical-only mode doesn't read legacy access files.
5. To roll back to an old host, first restore both the legacy access and repository carriers **and the original navigation-only metadata**, along with matching configuration and cache state. An old host doesn't understand the unified policy or the new raw-metadata guard.

Folder-level authorization and fleet-wide legacy-reader removal are separate future work. The visible run covers anonymous behavior on the current parser and one genuine signed-in session on the preceding build. A signed-in rerun on the current parser, Reader, Contributor, multiple-role, and unmapped-role sessions, and live distributed-service behavior remain unverified.

## 💡 Conclusion

The canonical filename reduces authored carriers and the shared snapshot removes duplicate runtime acquisition. Scope boundaries, strict policy validation, and an explicit compatibility period keep the consolidation from silently expanding authorization semantics.

## 📚 References

- **[Structural JSON Schema](metadata.schema.json)** 📘 [Repository source]. Defines versioned fields and structural shapes; the parser adds file, scope, and policy checks.
- **[Metadata parser](MetadataParser.cs)** and **[metadata provider](ContentMetadataProvider.cs)** 📘 [Repository source]. Define parsing, acquisition, freshness, and compatibility behavior.
- **[Access policy validator](../Authorization/AccessFile.cs)** 📘 [Repository source]. Defines permission, role, and reserved-name validation.
- **[Classification procedure](../../../.github/prompt-snippets/content-classification-and-split.md)** 📘 [Repository authority]. Defines repository declaration resolution and companion handling.
- **[Validation sequence](../../docs/90.00-issues/202611/20261105.01-smartdocs-authentication-custom/_validation/01-metadata-refactry.validation-sequence.md)** 📘 [Repository evidence]. Records observed anonymous and signed-in behavior and remaining coverage gaps.

<!--
validations:
  structure: {status: "passed", last_run: "2026-11-07", notes: "Frontmatter, validation metadata, and local links checked."}
  sources: {status: "reviewed", last_run: "2026-11-07", notes: "Checked against the parser, policy validator, provider, freshness options, and regression harness."}
article_metadata:
  filename: "metadata-reference.md"
  created: "2026-11-06"
  last_updated: "2026-11-07"
-->
