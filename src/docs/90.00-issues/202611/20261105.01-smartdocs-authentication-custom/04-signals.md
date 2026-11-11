---
title: "Signals — authentication for custom documentation profiles"
date: "2026-11-10"
categories: [signals, authentication, metadata]
description: "Activities surfaced by the access-metadata migration analysis that were never in scope for its goal."
publish: false
---

# Signals — authentication for custom documentation profiles

Activities this work item's conversations surfaced that were **never in scope** for its goals. Each record is self-contained, so someone reading it without this conversation can act on it as written.

**Identifiers are identity; the listing order is priority**, derived from relevance and actionability.

📖 Record shape, kinds, sweep, and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

The first four were added on 2026-11-10, during the [access-metadata migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md). Its goal was to migrate legacy access policies to canonical metadata across the configured repositories. `SIG-5-publisher-keeps-stale-policy` was added the same day, while investigating the [additional-fix plan](04-smartdocs-rebac-metadata-additionalfix.plan.md), whose goal is RBAC conformance across every configured source. None of these signals was introduced by that work. A less defined remainder is on the [other signals page](05-other-signals.md).

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-1-bom-rejected` | `divergent-commitment` | high | ready | `diginsight/smartdocs` | none found | `pending` |
| 2 | `SIG-5-publisher-keeps-stale-policy` | `divergent-commitment` | medium | ready | `diginsight/smartdocs.internal` | none found | `pending` |
| 3 | `SIG-2-publisher-validation` | `divergent-commitment` | medium | bounded | `diginsight/smartdocs.internal` | none found | `pending` |
| 4 | `SIG-3-declaration-artifacts` | `propagation-debt` | low | bounded | Four peer repositories | none found | `pending` |
| — | `SIG-4-dispatcher-source` | `divergent-commitment` | — | — | `diginsight/smartdocs.internal` | `20261108.01-configurations-update` | `routed → 20261108.01-configurations-update` |

### `SIG-1-bom-rejected` — the versioned metadata parser rejects a byte-order mark the legacy parser accepted

- **Kind** — `divergent-commitment`.
- **Goal** — a versioned `metadata.yml` saved with a UTF-8 byte-order mark parses exactly like the same file without one, as a legacy `access.metadata.yml` already does.
- **Scope** — `MetadataParser.Parse` in `src/Diginsight.SmartDocs.Web/Metadata/MetadataParser.cs` decodes with `new UTF8Encoding(false, true).GetString(bytes)`, which keeps a leading `U+FEFF`. The first key then reads as `U+FEFF` followed by `schema`, so the document is treated as unversioned, and an `access` mapping makes it invalid with *'access' requires the unified schema*. `AccessFile.Parse` reads through a `StreamReader` that detects byte-order marks, so the same policy in a legacy file is valid. Reproduced on 2026-11-10 with `eng/metadata-checks` `validate-tree` against scratch copies. A flat navigation file with a byte-order mark passed validation, but whether its first key is still recognized wasn't checked. The [unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md) requires only "UTF-8".
- **Why it matters** — saving a policy with an editor or shell that adds the mark, such as Windows PowerShell 5.1's `-Encoding UTF8`, hides the space from every reader. The diagnostic points at the schema, not at the encoding, and the migration from legacy files is exactly when authors rewrite these files.
- **Target** — `diginsight/smartdocs`: `src/Diginsight.SmartDocs.Web/Metadata/MetadataParser.cs`, a regression case in `eng/metadata-checks`, and `src/docs/06.00-reference/08-unified-metadata-contract.md`.
- **Existing landing** — none found. The in-progress [metadata refactoring plan](01-metadata-refactry.plan.md) owns the parser and its "existing behavior preserved" exit condition, but no item covers byte-order marks.
- **State** — `pending`.
- **Relevance** — `high`. The behavior contradicts the contract, because a file with a byte-order mark is UTF-8, and it turns a faithful migration into an outage.
- **Actionability** — `ready`. Strip one leading `U+FEFF` before loading, or decode with byte-order-mark detection as the legacy parser does. Add harness cases for a versioned file and a flat navigation file with the mark, and state the behavior in the contract.

### `SIG-5-publisher-keeps-stale-policy` — the space publisher never prunes an obsolete policy file from a space that requires one

- **Kind** — `divergent-commitment`.
- **Goal** — after a publish, a space's container holds exactly the root policy files present in its source, so a superseded policy can never conflict with the current one.
- **Scope** — the private space publisher in `devops/scripts/`. Its prune step skips any stale `metadata.yml` or `access.metadata.yml` at the container root whenever the space sets `AccessFileRequired: true`. It warns, "Remove it there deliberately if intended." The safeguard prevents a publish from leaving a required-policy space with no policy at all. It also keeps the legacy file after a source moves to the canonical `metadata.yml`. On 2026-11-10, the desktop-client and model-editor containers held equivalent pairs; the additional-fix plan subsequently removed both legacy blobs. The publisher's retention behavior remains unchanged and can leave a stale policy again.
- **Why it matters** — the host compares the two files whenever both exist. The first later change to the canonical policy, such as adding a role mapping, makes them differ, and the host then hides the space from every reader until someone deletes the old file by hand. Keeping the legacy file also blocks canonical-only mode for every profile reading that container.
- **Target** — `diginsight/smartdocs.internal`: the space publisher and its tests in `devops/scripts/tests/`.
- **Existing landing** — none found. The [additional-fix plan](04-smartdocs-rebac-metadata-additionalfix.plan.md) removes today's two leftover files, but not the behavior that leaves them. `SIG-2-publisher-validation` covers validation, not pruning.
- **State** — `pending`.
- **Relevance** — `medium`. It hardens a tool already in use, and the outage it prevents requires a later policy change to trigger.
- **Actionability** — `ready`. Prune a stale policy file whenever the source still carries a valid root policy of either form, and keep the safeguard only when the source carries none. Add a test for a source that moved from the legacy file to the canonical one.

### `SIG-2-publisher-validation` — the space publisher recognizes a policy by pattern instead of validating it

- **Kind** — `divergent-commitment`.
- **Goal** — publishing a space never uploads root metadata that the host would reject.
- **Scope** — the private space publisher and its environment wrapper under `devops/scripts/`. The publisher treats a root `metadata.yml` containing a line that starts with `access:` as a canonical policy, and otherwise checks only that a policy file exists. Neither script runs the production `validate-tree` from `eng/metadata-checks`. A canonical file that's invalid, carries a byte-order mark, or conflicts with a legacy file is uploaded as-is, and policy files upload first, ahead of content.
- **Why it matters** — an invalid policy reaches the deployed host before any other file and hides the space from everyone until a corrected publish. The host fails closed, so nothing leaks, but readers lose the space.
- **Target** — `diginsight/smartdocs.internal`: the space publisher in `devops/scripts/` and its tests in `devops/scripts/tests/`.
- **Existing landing** — none found. The publication records in both repositories describe dry runs and routing checks, not metadata validation.
- **State** — `pending`.
- **Relevance** — `medium`. It hardens a tool already in use.
- **Actionability** — `bounded`. The landing is known; the shape is open: how the script locates or builds the validator, and whether it refuses or only warns when the validator can't run.

### `SIG-3-declaration-artifacts` — peer repositories' authoring artifacts read only the legacy repository declaration

- **Kind** — `propagation-debt`.
- **Goal** — every repository that uses the content-classification authoring artifacts resolves its repository declaration from the canonical root `metadata.yml` first, with `repository.metadata.yml` as a transitional fallback, as `diginsight/smartdocs` does.
- **Scope** — the content-classification instruction, the classification procedure snippet, the issue-analysis prompt, and any other artifact that names `repository.metadata.yml`. Four repositories still name only the legacy file: the public `darioairoldi/Learn` repository, with three such artifacts; the AICM documentation repository; and two private product repositories served by the devec4 profile, with six each. Mirror phase 4 of the [metadata refactoring plan](01-metadata-refactry.plan.md), which updated this repository's five authoring consumers.
- **Why it matters** — until then, those repositories can't move their declarations into the canonical file, and the legacy filename can't be retired.
- **Target** — the four repositories above. The two private ones are the desktop-client and model-editor repositories, named by role as in the migration analysis.
- **Existing landing** — none found. The metadata refactoring plan's phase 4 covered this repository only, and the migration plan parks the declarations themselves as `PL4-repository-declarations`.
- **State** — `pending`.
- **Relevance** — `low`. Nothing degrades while the legacy declarations stay in place.
- **Actionability** — `bounded`. The landing and the change are known, but each repository's copies of the artifacts have drifted and must be compared before editing.

### `SIG-4-dispatcher-source` — the dispatcher checkout exists but has no documentation folder

- **Kind** — `divergent-commitment`.
- **Goal** — the devec4 Development overlay serves the dispatcher space from a local source, or states accurately why it can't.
- **Scope** — the overlay's comment says no dispatcher checkout was found. On 2026-11-10 a checkout of that repository existed on its development branch, but it had no `docs` or `src/docs` folder, so the space correctly remains storage-backed locally.
- **Why it matters** — the comment misleads the next maintainer, and the local route's failure persists until a documentation source exists.
- **Target** — `diginsight/smartdocs.internal`: the devec4 Development overlay.
- **Existing landing** — the configurations-update work item, `20261108.01-configurations-update`, which records the missing dispatcher source as an open gap.
- **State** — `routed → 20261108.01-configurations-update`.
- **Relevance** — not assigned; the owning work item orders it.
- **Actionability** — not assigned; the owning work item orders it.

## 📚 References

- **[Migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md)**, **[migration plan](03-smartdocs-rebac-metadata-fix.plan.md)**, and **[additional-fix plan](04-smartdocs-rebac-metadata-additionalfix.plan.md)** 📘 [Repository documentation]. The work that surfaced these signals, with the evidence behind each fact.

### Internal sources

Not resolvable from this repository; listed for provenance.

- **`src/docs/_aliases/devec4.aliases.internal.md`** — resolves the devec4 role names used above.

<!--
article_metadata:
  filename: "04-signals.md"
  created: "2026-11-10"
  last_updated: "2026-11-10"
-->
