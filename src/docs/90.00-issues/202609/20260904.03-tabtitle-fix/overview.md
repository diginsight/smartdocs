---
title: "Issue: Learning Hub browser tab title fallback resolves to SmartDocs"
author: "Dario Airoldi"
date: "2026-09-04"
categories: [issue, bug-report, title, markdown, blazor]
description: "The Learning Hub app used a hard-coded SmartDocs browser title even when the site was configured as Learning Hub and the active page had no frontmatter title or H1."
publish: false
---

# Learning Hub browser tab title fallback resolves to SmartDocs

**Issue title:** Browser tab title ignored configured site title on Markdown pages without an article title  
**Date reported:** 2026-09-04  
**Reporter:** Dario Airoldi  
**Status:** Resolved  
**Severity:** Medium  
**Component:** `Diginsight.SmartDocs.Web`, `Diginsight.SmartDocs.Web.Shared`, Markdown rendering, and page title binding  
**Framework:** .NET 10 / ASP.NET Core / Blazor / Markdig

## 📚 Table of contents

- [🎯 Summary](#-summary)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction steps](#-reproduction-steps)
- [✅ Solution implemented](#-solution-implemented)
- [🧪 Verification](#-verification)
- [📎 Appendix](#-appendix)
- [🎓 Lessons learned](#-lessons-learned)
- [📡 Signal sweep](#-signal-sweep)

## 🎯 Summary

The Learning Hub was configured with a site title of `Learning Hub`, but the live browser tab still showed `SmartDocs` on content pages without a document-level title. The bug was not in configuration binding itself. It was in the title fallback logic: the rendered page and the Markdown parser both had a hard-coded fallback string, so the app never reached the configured site title when there was no frontmatter title and no H1.

| Item | Result |
|---|---|
| Expected browser title | `Learning Hub` |
| Observed browser title | `SmartDocs` / `Diginsight SmartDocs` |
| Active environment | `AppsettingsEnvironmentName=devlearn` |
| Root cause | Hard-coded fallback in page-title and Markdown title extraction logic |
| Fix | Resolve title via metadata/H1 and then fall back to the configured site title |
| Status | Resolved and validated in a live browser |

This issue mattered because it made the app appear misconfigured even when the runtime configuration was correct. It also made the root cause easy to misread as a deployment or metadata problem when the real defect was in the app shell and title fallback path.

## 🔍 Context information

The site has three layers relevant to the title:

1. the server config (`Site:Title` in `appsettings.devlearn.json`),
2. the shared UI shell that renders the page title element, and
3. the Markdown renderer that extracts a page title from frontmatter or H1 when available.

The platform was already configured to `Learning Hub`, but the runtime fallback path did not honor that value when a page lacked article metadata.

| Area | Observation |
|---|---|
| Site configuration | `Site.Title` in the Learning Hub environment is configured as `Learning Hub`. |
| Browser tab binding | The page shell used a literal title fallback. |
| Markdown extraction | The renderer had a title fallback path that was also product-specific. |
| Triggering condition | A document without frontmatter `title` and without an H1. |
| User-visible effect | Browser tab and page title were misleading even though configuration was correct. |

The investigation showed the real bug by following the fallback chain from the page shell into the Markdown renderer, rather than assuming the deployment overlay was wrong.

## 🔬 Analysis

The root cause was a hard-coded product title in the UI and renderer fallback path. The app already had a frontmatter and title-resolution model, but the default path bypassed it in the no-title case.

### Code-level fault pattern

The failing logic followed this shape:

```razor
<PageTitle>@(_page?.Title ?? "Diginsight SmartDocs")</PageTitle>
```

and in the Markdown renderer a similar fallback existed when no frontmatter title or H1 could be resolved.

That meant the ordering was effectively:

1. resolve page title from article metadata if present,
2. otherwise use a hard-coded product string,
3. never evaluate the configured site title as the general fallback.

The correct behavior is:

1. use frontmatter title if present,
2. otherwise use H1 title if present,
3. otherwise use the configured site title,
4. preserve the article title only when the document actually declares one.

### Why the issue was easy to misdiagnose

The site config itself was not broken. The deployed configuration was valid, and the app could still render the Learning Hub shell. The fact that the title stayed on the product name made it look like the configuration did not land anywhere, which is a common false lead in multi-layered UI apps.

The critical clue was that a page with no title and no H1 should not default to the product name. The general fallback belongs to the configured site identity, not to the original product label.

## 🔄 Reproduction steps

1. Run the app with `AppsettingsEnvironmentName=devlearn`.
2. Open a Markdown page that has no YAML `title` and no H1 heading.
3. Observe the browser tab title.
4. Confirm the value remains the product name instead of the configured site title.

Affected code locations:

| File | Role |
|---|---|
| `src/Diginsight.SmartDocs.Web.Shared/Components/ContentView.razor` | The page shell used a literal fallback title. |
| `src/Diginsight.SmartDocs.Web.Shared/Rendering/MarkdigMarkdownRenderer.cs` | The Markdown renderer extracted page titles and set the fallback. |
| `src/Diginsight.SmartDocs.Web.Shared/Navigation/FrontMatter.cs` | The canonical title resolver already existed and should have been used. |
| `src/Diginsight.SmartDocs.Web/appsettings.devlearn.json` | The configured Learning Hub title lived here. |

## ✅ Solution implemented

The fix was intentionally small and root-cause based. The app now uses the existing metadata-aware title resolution path and falls back to the configured site title instead of the product literal.

### What changed

- The Markdown renderer now prefers metadata/H1 title resolution and only falls back to the configured shell/site title when no page title exists.
- The page shell no longer hard-codes a SmartDocs literal for the browser title.
- The site-level value from the active app configuration is used as the general fallback for pages that intentionally have no article title.

### Resulting behavior

The effective order is now:

```text
frontmatter title -> H1 -> configured site title
```

This keeps article-specific titles authoritative while preserving a sensible site-wide default for generic content pages and content opened from generated navigation.

## 🧪 Verification

The fix was validated in a visible browser using the Learning Hub configuration.

| Check | Result |
|---|---|
| Build | PASS (`dotnet build src/Diginsight.SmartDocs.slnx -c Debug`) |
| App run | PASS with `AppsettingsEnvironmentName=devlearn` |
| Browser tab title on a page with no article title | `Learning Hub` |
| Live DOM brand text | `Learning Hub` |
| Validation artifact | `src/docs/90.00-issues/202609/20260904.01-optimization/_validation/20260904.01-validation-sequence.md` |
| Screenshot | captured in the issue validation folder |

The validation sequence confirms the live behavior: a generated Markdown page without frontmatter title or H1 now displays the configured site title rather than a literal SmartDocs string.

## 📎 Appendix

### Relevant validation evidence

- `src/docs/90.00-issues/202609/20260904.01-optimization/_validation/20260904.01-validation-sequence.md`
- The validation run confirms `document.title = "Learning Hub"` on the targeted page.

### Resolution status

- Root cause identified and fixed.
- Browser validation passed.
- No regression was observed in the targeted title fallback path.

## 🎓 Lessons learned

- A product fallback string can silently override the real configuration when no document title is present.
- The correct fallback order is not arbitrary: frontmatter title, then H1, then site title.
- The app should not duplicate title rules in multiple layers when one canonical resolver already exists.
- Browser validation is essential for title issues because the visible title is a runtime output, not a compile-time property.

## 📡 Signal sweep

The conversation was checked against the signal-capture procedure. The sweep found no out-of-scope follow-up items that required a separate signals page. This issue was self-contained and the fix remained in the title-resolution path itself.

| Sweep question | Result |
|---|---|
| What should happen that is not this issue? | Nothing material was identified beyond the title boundary itself. |
| What authority document was contradicted or extended? | None found. |
| Which changed artifacts have path-parallel peers in another repository? | None found for this bug fix. |
| What was decided and written to no file? | No material extra decision was left undocumented. |
| What references a path outside this workspace? | The repo config path is in the appsettings file, which is still within the workspace. |
| What subject was opened but not developed? | No separate subject was opened. |
| What framing landed wrong and was corrected? | The title was corrected from a product literal to a site-level fallback. |

### Split report

This issue analysis was written as a public document in the repository, and no internal companion was created because the configured internal peer was not resolved in this workspace. The repository metadata requires fail-closed behavior when the internal peer is missing, so the public analysis remains the only written artifact for this case.

<!--
article_metadata:
  filename: "overview.md"
  created: "2026-09-04"
  last_updated: "2026-09-04"
  version: "0.1"
  status: "resolved"
  issue_type: "bug"
-->