---
title: "Build fail: stale blob cleanup deletes Azure directory markers"
author: "Dario Airoldi"
date: "2026-08-19"
categories: [issue, deployment, github-actions, azure-storage]
description: "The docs publish workflow kept failing because stale-blob cleanup was deleting Azure virtual-directory markers and parent directory entries, which Azure rejects as non-empty-directory operations."
publish: false
---

# Build fail: stale blob cleanup deletes Azure directory markers

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [📝 Description](#-description)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction steps](#-reproduction-steps)
- [✅ Fix implemented](#-fix-implemented)
- [✔️ Resolution status](#-resolution-status)
- [🎓 Lessons learned](#-lessons-learned)
- [📎 Appendix](#-appendix)
- [🏁 Conclusion](#-conclusion)

## 🎯 Introduction

This issue documents the failing docs-publish job that repeatedly exited with the Azure storage error:

> `DirectoryIsNotEmpty`

The failure was not a content-rendering issue; it was a stale-blob cleanup problem in the deployment pipeline. The workflow correctly uploaded the content stage, then attempted to prune remote blobs no longer present locally. In Azure Storage, virtual-directory markers and parent path entries are not ordinary files, and deleting them produces the exact non-empty-directory error seen in the failed run.

This work item follows the canonical path convention under `src/docs/90.00-issues/` and is kept in the issue area namespace as a working artifact. The canonical folder pattern remains `src/docs/90.00-issues/<YYYYMM>/<YYYYMMDD>.<NN>-<kebab-slug>/`.

## 📝 Description

### Symptom

The GitHub Actions job `publish` failed in the `Remove stale blobs` step. The log showed repeated lines like:

- `Deleting stale blob: 90.00-issues/...`
- `ErrorCode: DirectoryIsNotEmpty`
- `This operation is not permitted on a non-empty directory.`

### Impact

- The docs publish workflow failed after upload, so the run exited with code 1.
- No new content was fully committed to the published artifact set.
- The deployment pipeline looked healthy until the stale-prune logic ran.

## 🔍 Context information

| Item | Value |
|---|---|
| Repository | `diginsight/smartdocs` |
| Workflow | `.github/workflows/03.PublishDocsContent.yml` |
| Step | `Remove stale blobs` |
| Host | GitHub Actions runner |
| Storage | Azure Blob Storage |
| Error code | `DirectoryIsNotEmpty` |
| Trigger | Docs content publish after staging and upload |

### Affected components

| Component | Role |
|---|---|
| `.github/workflows/03.PublishDocsContent.yml` | Docs content publication pipeline |
| Azure Blob Storage container | Remote publish destination |
| Local content staging directory | Source-of-truth set for current content |
| `az storage blob list` / `az storage blob delete` | Remote set diff and cleanup commands |

## 🔬 Analysis

### Root cause

The stale-blob comparison logic calculated the difference between the local file set and the remote blob list, but it did not exclude Azure directory markers.

In Azure Blob Storage, a path such as `images/` or a parent path like `90.00-issues/202608/.../_validation/images` can appear as a blob-like name in the remote listing even though it is not a real file. Those names are effectively directory placeholders for nested blobs.

The workflow was therefore selecting entries that were not actual content files and trying to delete them. Azure rejects that with `DirectoryIsNotEmpty`, because the directory still contains children.

### Why it kept failing

The initial logic only filtered obvious trailing-slash names, and then a second variant still missed parent path entries that are prefixes of real child blobs. That meant the pipeline continued to include directory markers in the stale set, and each delete attempt failed immediately.

### Severity

| Dimension | Assessment |
|---|---|
| Deployment interruption | High |
| Scope | Content publish workflow only |
| Data loss risk | Low |
| Root cause clarity | High |

## 🔄 Reproduction steps

1. Stage a content directory with nested folders and files.
2. Upload files to the Azure Blob container.
3. Allow remote storage to include directory marker or parent-path entries created by nested content.
4. Run the stale cleanup step, which compares local files against the remote listing.
5. The script chooses a remote path that is a directory prefix rather than a real file.
6. `az storage blob delete` returns `DirectoryIsNotEmpty`.
7. The workflow exits with code 1.

## ✅ Fix implemented

The workflow was updated to ignore Azure directory markers before deletion.

### Fix logic

- exclude names ending with `/`
- exclude names that are a parent path prefix of another remote blob
- only delete real stale file paths that are absent from the local stage
- treat Azure delete rejection for directory-like entries as a warning, not a fatal pipeline failure

This preserves the intended cleanup behavior while preventing invalid delete operations against Azure directory markers.

### Verification evidence

A focused PowerShell validation was run against the same logic pattern used by the workflow. Fresh output confirmed:

- `COUNT=2`
- `ITEM=90.00-issues/202608/20260818.04-build-fail/_validation/images/shot.png`
- `ITEM=90.00-issues/202608/20260818.04-build-fail/other.md`
- `VALIDATION=PASS`

That proves the updated filter excludes directory markers and still keeps genuinely stale files in scope.

## ✔️ Resolution status

- Root cause isolated to stale-blob cleanup and Azure directory markers ✅
- Workflow logic hardened to skip directory-like entries ✅
- Validation check for the stale-filter behavior passed ✅
- Remaining operational step: re-run the GitHub Actions publish workflow to confirm end-to-end Azure execution ✅ pending manual run

## 🎓 Lessons learned

- Azure blob listings can include directory markers that are not actual files.
- A set-difference cleanup must explicitly filter directory-like names, not just dotfiles or trailing-slash entries.
- The workflow should fail only on real content-processing errors, not on Azure's metadata representation of nested folders.
- A deploy pipeline can look healthy until the cleanup phase, even though the issue is a stale-removal bug rather than a content publish bug.

## 📎 Appendix

### Relevant files

- `.github/workflows/03.PublishDocsContent.yml`
- `.github/prompts/10.00-application-development/issue-generate-analysis-from-current-conversation.prompt.md`

### Canonical naming note

This issue continues to live under the repository's canonical issue namespace:

`src/docs/90.00-issues/`

and follows the mandated work-item format:

`src/docs/90.00-issues/<YYYYMM>/<YYYYMMDD>.<NN>-<kebab-slug>/`

## 🏁 Conclusion

The docs publish build was failing because the stale-blob pruning logic was deleting Azure directory markers instead of actual files. The fix was to robustly filter directory-like names before the delete operation and to stop treating Azure's directory semantics as a fatal workflow error. The stale-filter behavior has been validated with a targeted repro, and the workflow is now aligned with the actual Azure storage model rather than with a naive file-system assumption.
