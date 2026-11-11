---
title: "Legacy access policies across configured SmartDocs repositories"
date: "2026-11-10"
type: explanation
description: "An inventory of every configured SmartDocs content root, which ones authenticate readers, and which still depend on legacy access metadata."
---

# Legacy access policies across configured SmartDocs repositories

## 🎯 Introduction

This analysis records the pre-migration inventory of every SmartDocs configuration profile in the private overlay set and every content root those profiles read. It answers three questions: which profiles authenticate readers, which spaces are protected and how, and which content roots carried their access policy in the legacy `access.metadata.yml` instead of the canonical `metadata.yml`.

The short answers:

- **Seven profiles declare 25 spaces, backed by 17 local content roots and one storage-only space.** Five profiles turn sign-in on; the two Diginsight product-documentation profiles don't. Nine distinct spaces are protected, by a floor, a policy, or both.
- **Four configured content roots depended on a legacy policy file.** All four now carry canonical policies; the only remaining local legacy access file is the deliberate negative fixture.
- **Four policy files were outside version control.** They now have four single-file local commits, with unrelated pending and staged work preserved. Nothing was pushed.
- **The conversion is validated before and after.** Every converted root passed coexistence and canonical-only validation. All seventeen root verdicts match the baseline, and eight anonymous browser scenarios have identical complete DOM text. Policy descriptions and logged grants are unchanged; new files have no UTF-8 byte-order mark.

Execution completed on November 10, 2026. The [migration plan](03-smartdocs-rebac-metadata-fix.plan.md) is done, and its [validation sequence](_validation/20261110.01-rebac-metadata-fix.validation-sequence.md) records the observed results and coverage limits. The detailed inventory and findings below retain the **pre-migration baseline** as design evidence. Signed-in scenarios were not covered under the plan's availability branch; deployed containers, publication, pushes, and compatibility cutover remain outside this execution.

The devec4 profile serves documentation from private repositories, so this analysis names its spaces, repositories, registration, and storage by role, such as *the desktop-client space*.

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🔍 How the inventory was taken](#-how-the-inventory-was-taken)
- [🔐 Which profiles authenticate readers](#-which-profiles-authenticate-readers)
- [🗂️ Content roots and their access carriers](#️-content-roots-and-their-access-carriers)
- [🚨 Findings](#-findings)
- [🔄 Required changes](#-required-changes)
- [✅ What doesn't need to change](#-what-doesnt-need-to-change)
- [⚠️ Risks and safeguards](#️-risks-and-safeguards)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🔍 How the inventory was taken

A profile is selected by a launch profile's environment name. The host layers the public base settings, then the environment overlay, then, in Development only, the shared and the profile-specific `.local.json` files. At each level the public file loads first and the private file second. The official environment overlays point spaces at storage containers; the Development `.local.json` overlays swap in local folders. A relative `FileSystem:RootPath` resolves against the web project folder, the host's content root, as the [content-source factory](../../../../Diginsight.SmartDocs.Web/Program.cs) does with `Path.Combine(env.ContentRootPath, RootPath)`.

On 2026-11-10, every profile's spaces were resolved to physical folders, and each folder was inspected read-only:

- **Root carriers.** Any root file named like `metadata.yml`, `access.metadata.yml`, or `repository.metadata.yml`, in any casing.
- **Nested carriers.** Every nested `metadata.yml`, `access.metadata.yml`, or `repository.metadata.yml`, plus any nested `metadata.yml` with policy or repository keys. None of the latter was found.
- **Production validation.** The repository's [metadata checks](../../../../../eng/metadata-checks/Program.cs) run `validate-tree` with the host's own parser. It rejects invalid canonical files, misplaced or invalid legacy files, and conflicting root policies. It doesn't know a space's floor or `AccessFileRequired`, so those came from the overlays.
- **Version control.** Whether each carrier is tracked, untracked, or modified.
- **Dry runs.** Each proposed canonical file was written to a scratch folder beside a copy of its legacy file and validated before and after removing the copy, together with negative controls.

A host already running the devec4 profile that day provided runtime evidence: its warm-up logged the migration warning, *a legacy access file supplies this scope's policy*, for the desktop-client, model-editor, and device-API roots.

Storage containers weren't read. Earlier attempts from this workstation returned authorization failures, so the contents of deployed containers, and the dispatcher space's root in particular, aren't established here.

## 🔐 Which profiles authenticate readers

No profile overrides `Site:LegacyMetadataCompatibility` or `Site:Authorization:DefaultRequirement`. Every profile therefore reads legacy policies, and none requires sign-in for the generated space index.

| Profile | Purpose | Sign-in | Registration | Spaces | Protected spaces |
|---|---|---|---|---|---|
| `access` | Local authorization fixtures in this repository | On | The development registration | 4 | `product-a` and `product-b` by policy; `product-d` by floor |
| `devdocs` | Diginsight product documentation, local | Off | None | 5 | None |
| `TestmcDocs` | Diginsight product documentation, deployed | Off | None | 5 | None |
| `devlearn` | Learning Hub, local | On | The Learn reader app registration | 2 | `learn-internal` by floor and role policy |
| `Testmc` | Learning Hub, deployed | On | The Learn reader app registration | 2 | `learn-internal` by floor and role policy |
| `TestmcAicm` | AICM documentation, deployed and local | On | The Learn reader app registration | 1 | `aicm` by floor and role policy |
| `devec4` | Six private product repositories | On; callbacks and ID tokens configured | The devec4 reader registration, in its own tenant | 6 | The desktop-client and model-editor spaces by floor and sign-in policy; the telemetry-gateway and dispatcher spaces by floor |

**Correction, November 10, 2026:** the devec4 registration now enables ID-token issuance and lists the local and deployed-host callbacks, superseding the earlier claim that both were missing. This configuration doesn't by itself prove a reader can complete sign-in. The [additional-fix plan](04-smartdocs-rebac-metadata-additionalfix.plan.md) covers the deployed sources and signed-in checks; the pre-migration inventory below remains historical.

## 🗂️ Content roots and their access carriers

*Required* is the space's `AccessFileRequired`. *Floor* is its `Access` setting. Roots shared by two profiles appear once.

| Space | Profiles | Floor | Required | Root carriers | Effective policy | Validator | Status |
|---|---|---|---|---|---|---|---|
| `product-a` | `access` | — | Yes | Canonical | Requires `ProductA.Read`; three roles mapped | Pass | Current |
| `product-b` | `access` | — | No | Canonical | Requires `ProductB.Read` | Pass | Current |
| `product-c` | `access` | — | No | None; one nested legacy file | None | Fails by design | Deliberate negative fixture |
| `product-d` | `access` | `authenticated` | No | None | Floor only | Pass | Current |
| `diginsight.smartdocs` | `devdocs`, `TestmcDocs` | — | No | None at the content root | None | Pass, 11 navigation files | Public |
| `diginsight.telemetry`, `.smartcache`, `.components`, `.tools` | `devdocs`, `TestmcDocs` | — | No | None | None | Pass, no metadata | Public |
| `learn` | `devlearn`, `Testmc` | — | No | Legacy repository declaration only | None | Pass, 10 navigation files | Public |
| `learn-internal` | `devlearn`, `Testmc` | `authenticated` | Yes | **Legacy access file only** | Requires `Read`; `Reader`, `Contributor`, `Admin` mapped | Pass | **Convert** |
| `aicm` | `TestmcAicm` | `authenticated` | Yes | Canonical, **untracked** | Requires `Read`; same three roles | Pass | **Track** |
| The desktop-client space | `devec4` | `authenticated` | Yes | **Legacy access file only**, untracked | Requires `authenticated` | Pass, 79 files | **Convert and track** |
| The model-editor space | `devec4` | `authenticated` | Yes | Unversioned navigation file **and legacy access file**, untracked | Requires `authenticated` | Pass, 117 files | **Merge and track** |
| The device-API space | `devec4` | — | No | **Legacy access file only**, empty, untracked | None | Pass, 77 files | **Convert and track** |
| The event-adapter space | `devec4` | — | No | None | None | Pass | Public |
| The telemetry-gateway space | `devec4` | `authenticated` | No | None | Floor only | Pass | Current |
| The dispatcher space | `devec4` | `authenticated` | No | Unknown: storage only | Floor; root unknown | Not run | Discovery |

The internal Learn library behind `learn-internal` is a synchronized document library, not a git working tree. Its changes take effect through synchronization, with the library's version history as rollback.

## 🚨 Findings

### `F1-required-policies-on-legacy-carrier`

The desktop-client, model-editor, and `learn-internal` spaces set `AccessFileRequired: true`, and their only policy is a legacy file. Compatibility mode reads it, so they behave correctly today. With compatibility off, the [metadata provider](../../../../Diginsight.SmartDocs.Web/Metadata/ContentMetadataProvider.cs) no longer reads the legacy file. The policy becomes missing, and the [requirement resolver](../../../../Diginsight.SmartDocs.Web/Authorization/AccessRequirementResolver.cs) makes each space unresolvable: hidden from every reader, administrators included. The model-editor space is no exception: its existing `metadata.yml` carries navigation only, which doesn't satisfy the required-policy setting.

### `F2-public-policy-on-legacy-carrier`

The device-API space carries an empty legacy policy whose comment declares the space public on purpose. Removing it wouldn't change visibility, because the space has no floor and doesn't require a policy. Converting it to an explicit `access: {}` keeps that declared intent, and keeps the space public even if a deployment later requires a policy.

### `F3-navigation-file-needs-envelope`

The model-editor root already has an unversioned navigation file with `label`, `order`, and `icon`. The policy must move into that file, not a second one. Adding the `access` mapping without `schema: smartdocs-metadata/v1` makes the file invalid, as the dry run confirmed. With the schema line, all three navigation values are valid in the stricter versioned form.

### `F4-policies-not-in-version-control`

The three devec4 legacy files and the AICM repository's canonical file are untracked. In three of those repositories the rest of the documentation, navigation metadata included, is committed. A fresh clone therefore lacks the policy:

- A local run from that clone hides the desktop-client, model-editor, and `aicm` spaces, which require a policy.
- The space publisher refuses to publish a required-policy space whose source root has no policy, so only the workstation holding the untracked file can publish it.

Committing the canonical files makes each repository carry its own policy.

### `F5-coexistence-requires-identical-policies`

The safe conversion writes the canonical file first, while the legacy file still exists. During that window the host compares the two policies: permission names, descriptions, requirements, and role grants. A one-letter difference in a permission description made the dry run report *root canonical and legacy access policies conflict*. At runtime a conflict hides the space. Descriptions must be copied byte for byte.

### `F6-byte-order-mark-breaks-canonical-files`

The legacy access parser tolerates a UTF-8 byte-order mark; the versioned [metadata parser](../../../../Diginsight.SmartDocs.Web/Metadata/MetadataParser.cs) doesn't. In the dry run, the desktop-client proposal saved with a byte-order mark failed with *'access' requires the unified schema*, because the mark became part of the `schema` key. An editor or a shell that adds the mark would hide the converted space. The existing carriers are all UTF-8 without a mark, with CRLF line endings, and the converted files must match.

### `F7-legacy-file-reaches-static-sites`

Two devec4 repositories also build static MkDocs sites from the same content root. Their `exclude_docs` patterns cover `**/metadata.yml` but not `access.metadata.yml`, so a local MkDocs build copies the legacy policy into the site output. Their pipeline builds can't, because the file is untracked. The conversion removes the file and closes this path. Both repositories' navigation hooks read only `label`, `order`, `hidden`, and `icon` from `metadata.yml`, and tolerate a byte-order mark, so the versioned root file doesn't change their sites.

### `F8-repository-declarations-still-legacy`

Four repositories keep a legacy `repository.metadata.yml` at their repository root: the Learn repository, the AICM repository, and the desktop-client and model-editor repositories. The host never reads it for authorization. Their own authoring instructions, however, read only that filename, unlike this repository's, which read the canonical root `metadata.yml` first. Converting those declarations requires updating those authoring artifacts first, so it's outside this access-policy fix.

### `F9-publisher-detects-but-does-not-validate`

The space publisher recognizes a canonical policy by finding a line that starts with `access:` in the root `metadata.yml`. It doesn't run `validate-tree`, so it would upload a byte-order-marked or otherwise invalid file and hide the space. Its ordering is otherwise helpful: it uploads the root policy first and prunes stale files only after every upload succeeds. After this migration, the next ordinary publish of a converted space uploads the canonical file and then removes the legacy one from the container. In between, the two coexist as equivalent policies.

### `F10-stale-statements`

Two statements and one file no longer match the current layout:

- The `devlearn` overlays, the [public half](../../../../Diginsight.SmartDocs.Web/appsettings.devlearn.json) and its private sibling, say the internal root's `metadata.yml` maps the roles. That root holds only the legacy file. The statement becomes true once the plan converts it.
- The event-adapter space's content root moved to a new clone, which has no policy. The superseded clone still holds an empty legacy file that no profile reads. The space was public before the move and remains public.

### `F11-deployed-containers-unverified`

The deployed profiles read containers, not local folders. Whether a container already holds a canonical policy, a legacy one, or both, wasn't established. The dispatcher space has no local source at all: its repository checkout has no documentation folder. Its container root is the only place its policy could be.

## 🔄 Required changes

### Conversion rules

Every conversion follows the same rules, each backed by a dry run:

1. Add `schema: smartdocs-metadata/v1` as the first line.
2. Move `permissions`, `requires`, and `roles` under `access`, copying names and descriptions exactly. Empty `permissions: {}` and `roles: {}` can be dropped: the comparison treats them as absent.
3. Keep existing navigation fields and the legacy file's explanatory comment.
4. Save as UTF-8 without a byte-order mark, with CRLF line endings.
5. Validate with both files present, delete the legacy file, and validate again. Never delete first: a required-policy space without any policy is hidden.

### Desktop-client content root

The new root `metadata.yml` replaces the legacy file:

```yaml
schema: smartdocs-metadata/v1
# Any reader signed in to SmartDocs can read this documentation space.
access:
  requires: [authenticated]
```

### Model-editor content root

The existing navigation file gains the envelope and the policy:

```yaml
schema: smartdocs-metadata/v1
label: Documentation
order: 0
icon: book
# Any reader signed in to SmartDocs can read this documentation space.
access:
  requires: [authenticated]
```

### Device-API content root

The empty legacy policy becomes an explicit public one:

```yaml
schema: smartdocs-metadata/v1
# This documentation space is public; no sign-in or app role is required.
access: {}
```

### Internal Learn library root

The role policy moves verbatim:

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

### Version control

Commit the three new or merged devec4 files and the AICM repository's existing canonical file. Each repository's branch carries substantial uncommitted work, so each commit must include only its policy file. The deleted legacy files were never tracked, so their removal needs no commit.

## ✅ What doesn't need to change

- **No configuration.** Every floor, required-policy flag, and the compatibility switch stays as it is. After the migration, no configured root depends on a legacy policy, so compatibility mode and canonical-only mode read the same files.
- **Profiles without sign-in.** The Diginsight product-documentation spaces have no policies and need none.
- **Already canonical roots.** The `product-a`, `product-b`, and `aicm` roots already use the canonical form.
- **Floor-only spaces.** `product-d` and the telemetry-gateway and dispatcher spaces rely on their floor by design. The devec4 overlay states that the latter two require sign-in without assuming their repositories carry a policy.
- **The deliberate fixture.** `product-c` keeps its nested legacy file as a negative test.
- **Unversioned navigation files.** About 290 nested navigation files across these roots use the older flat form. That form remains supported, and none of them carries a policy.
- **The application.** No code changes. The [unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md) already documents the target format.

## ⚠️ Risks and safeguards

| Risk | Consequence | Safeguard |
|---|---|---|
| Invalid canonical YAML | Space hidden from everyone | Validate before deleting the legacy file; the space fails closed, never open |
| Description or name drift while both files exist | Policy conflict hides the space | Copy verbatim; the coexistence validation reports a conflict |
| Byte-order mark | Canonical file invalid; space hidden | Write without a mark and check the first bytes |
| Deleting the legacy file first | Required-policy space hidden | Canonical first, delete second |
| Committing unrelated work | Unreviewed changes enter a shared branch | Path-limited commits; verify each commit lists exactly one file |
| Library synchronization lag or conflict | A half-applied change in the shared library | Confirm synchronization before and after; version history is the rollback |
| An older host binary reading a converted container | It doesn't understand the canonical policy | Out of this plan: container publication is deferred, and must first confirm the deployed host supports unified metadata |

## 💡 Conclusion

Authentication is active in five of the seven profiles. The four legacy-policy conversions and four path-limited local commits are complete. Production validation, exact anonymous DOM comparisons, unchanged policy-log requirements and grants, and confirmed library synchronization establish the execution outcome.

The migration preserves policy meaning and observed anonymous visibility, with signed-in cases explicitly not covered. No configured local root depends on a legacy access carrier anymore. Deployed sources must still be checked and published before any compatibility-mode retirement.

Previous: [How SmartDocs authenticates readers and authorizes spaces](02-smartdocs-rebac-support.analysis.md). Next: [Legacy access policy migration plan](03-smartdocs-rebac-metadata-fix.plan.md).

## 📚 References

### Repository sources

- **[Metadata provider](../../../../Diginsight.SmartDocs.Web/Metadata/ContentMetadataProvider.cs)** and **[requirement resolver](../../../../Diginsight.SmartDocs.Web/Authorization/AccessRequirementResolver.cs)** 📘 [Repository source]. Show when the legacy file is read and how a missing required policy hides a space.
- **[Metadata parser](../../../../Diginsight.SmartDocs.Web/Metadata/MetadataParser.cs)** and **[policy comparer](../../../../Diginsight.SmartDocs.Web/Metadata/MetadataPolicyComparer.cs)** 📘 [Repository source]. Define the versioned envelope and the equivalence check, descriptions included.
- **[Metadata checks](../../../../../eng/metadata-checks/Program.cs)** 📘 [Repository source]. The `validate-tree` command used for the inventory and the dry runs.
- **[Content-source factory](../../../../Diginsight.SmartDocs.Web/Program.cs)** and **[launch profiles](../../../../Diginsight.SmartDocs.Web/Properties/launchSettings.json)** 📘 [Repository source]. Resolve relative content roots and select each profile's private overlays.

### Repository documentation

- **[Unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md)** 📘 [Repository documentation]. The canonical format, the compatibility rules, and the migration and rollback procedure.
- **[Metadata refactoring analysis](01-metadata-refactry.analysis.md)** and **[plan](01-metadata-refactry.plan.md)** 📘 [Repository documentation]. Introduced the unified format; the plan's deferred legacy retirement depends on the inventory recorded here.
- **[Signals](04-signals.md)** 📘 [Repository documentation]. Work this analysis surfaced outside its scope: the byte-order mark defect, publisher validation, and authoring artifacts in peer repositories.

### Internal sources

Not resolvable from this repository; listed for provenance.

- **`src/docs/90.00-issues/202611/20261105.01-smartdocs-authentication-custom/03-smartdocs-rebac-metadata-fix.analysis.internal.md`** — real identifiers, absolute paths, file fingerprints, repository states, and the validator and dry-run output behind this analysis.
- **`src/docs/_aliases/devec4.aliases.internal.md`** and **`src/docs/_aliases/testmc.aliases.internal.md`** — resolve the role names used above.

<!--
validations:
  structure: {status: "not_run", last_run: null}
  sources: {status: "reviewed", last_run: "2026-11-10", notes: "Pre-migration inventory retained; four conversions and commits completed; seventeen-root parity, eight anonymous DOM pairs and four policy logs verified; signed-in sessions and containers not covered."}
article_metadata:
  filename: "03-smartdocs-rebac-metadata-fix.analysis.md"
  created: "2026-11-10"
  last_updated: "2026-11-10"
-->
