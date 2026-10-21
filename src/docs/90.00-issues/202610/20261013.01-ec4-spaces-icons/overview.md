---
title: "Configuring a custom documentation space icon"
author: "Dario Airoldi"
date: "2026-10-13"
description: "Use a content-relative image for a documentation space while preserving existing emoji icons and keeping publisher branding independent."
---

# Configuring a custom documentation space icon

## 🎯 Introduction

Use `Site:Spaces:<index>:IconPath` when a documentation space needs its own application artwork instead of an emoji. This optional setting applies to the home card, space switcher, sidebar, collapsed sidebar rail, and top menu. It doesn't replace the publisher's site-wide logo.

## 🔧 Configuration

Store the image inside the space's content root, preferably in `assets/`, and configure its relative path:

```json
{
  "IconPath": "assets/application-icon.svg"
}
```

`IconPath` takes precedence over `Icon`. Leave it empty to preserve existing emoji rendering on cards and switcher entries, and existing Bootstrap icons in navigation.

The host sends a derived `IconUrl` to the shell using the space's route base and the existing `/_content/` asset endpoint. For example, a space mounted at `/product` with this setting uses `/_content/product/assets/application-icon.svg`; a root-mounted space uses `/_content/assets/application-icon.svg`. The image must be available in the configured filesystem or blob content source.

Image icons occupy a 20-by-20 CSS-pixel slot at the default root font size. Menu image icons have a small white backing to keep colored artwork visible against a branded header. They are decorative; the adjacent space title remains the accessible label.

Rebuild and restart the application after changing its configuration. Include the asset when publishing the space's content.

## ✅ Verification

The solution built successfully with zero warnings and errors in an isolated output directory. A visible, independent Edge window verified image loading across the five icon surfaces and after client-side navigation into the space. Existing emoji cards remained unchanged. Deployment-specific screenshots stay in the internal companion.

## 🏁 Conclusion

Space artwork now comes from configuration rather than a substituted emoji or hard-coded product asset. Existing spaces that don't set `IconPath` keep their previous behavior.

## 📚 References

- [Space configuration model](../../../../Diginsight.SmartDocs.Web.Shared/Sites/SiteOptions.cs) — the optional content-relative path and its display-only URL.
- [Shared space icon renderer](../../../../Diginsight.SmartDocs.Web.Shared/Components/SpaceIcon.razor) — image precedence and decorative accessibility attributes.
- 📘 [Blazor component parameters](https://learn.microsoft.com/aspnet/core/blazor/components/?view=aspnetcore-10.0#component-parameters) [Official] — parameter binding for reusable UI components.
- Internal companion: `src/docs/90.00-issues/202610/20261013.01-ec4-spaces-icons/overview.internal.md` in the private peer contains the deployment configuration, original asset location, and screenshot evidence.

<!--
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
article_metadata:
  filename: "overview.md"
-->