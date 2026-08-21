---
title: "Configuration layering: an array overlay that patched instead of replacing"
author: "Dario Airoldi"
date: "2026-08-21"
categories: [issue, configuration, aspnetcore, smartdocs-web]
description: "The base settings file declared a Site:Spaces element that every environment overlay was assumed to replace. Configuration binds JSON arrays by index, so the overlay patched it — leaking three fields into every environment and giving the Learning Hub the documentation site's identity."
publish: true
---

# Configuration layering: an array overlay that patched instead of replacing

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [📝 Description](#-description)
- [🔍 Context information](#-context-information)
- [🔬 Analysis](#-analysis)
- [🔄 Reproduction](#-reproduction)
- [✅ Solution implemented](#-solution-implemented)
- [🧪 Validation](#-validation)
- [✔️ Resolution status](#-resolution-status)
- [🎓 Lessons learned](#-lessons-learned)
- [📡 Signals](#-signals)
- [📎 Appendix](#-appendix)
- [🏁 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🎯 Introduction

`Diginsight.SmartDocs.Web` publishes one or more **spaces**, declared as the JSON array `Site:Spaces`. The base settings file declared one element; each environment declared one too. The arrangement read as *base default, replaced per environment*, and everyone — the settings comments included — treated it that way.

Configuration does not replace arrays. It binds them **by index**, so an environment overlay patches element `0` field by field. Every field the environment did not restate stayed at the base value and travelled into that environment silently.

This page analyses the defect, the fix, and the two things the fix did not close.

Azure resources are named by **role** throughout — *the content storage account*, *the two deployment overlays* — never by resource name, because this repository is public. Nothing in the analysis depends on the withheld names.

## 📝 Description

The local `devlearn` environment is meant to serve the Learning Hub from a working-tree clone. Its overlay declared five fields. It received nine, because four came from the base element it was believed to replace:

| Field | Effective value | Came from | Correct? |
|---|---|---|---|
| `Id` | `diginsight.smartdocs` | base | ❌ that is the documentation site's identifier |
| `RouteBase` | `/` | overlay | ✅ |
| `Title` | `LearningHub` | overlay | ✅ |
| `Icon` | 📘 | **base** | ❌ the Learning Hub's icon is 💡 |
| `RepositoryUrl` | `https://github.com/diginsight/smartdocs` | **base** | ❌ the content comes from `darioairoldi/Learn` |
| `Source` | `FileSystem` | overlay | ✅ |
| `FileSystem:RootPath` | the Learning Hub content clone | overlay | ✅ |
| `Blob:AccountUri`, `Blob:ContainerName` | empty strings | **base** | inert, but present |

Nothing crashed. The wrong values were either unread (`RepositoryUrl` is bound but not yet rendered), cosmetic (`Icon`), or currently low-consequence (`Id`). That is precisely why the defect survived: **an overlay that silently under-replaces produces a running system, not an error.**

A second copy of the same values existed in `launchSettings.json`, as `Site__Spaces__0__Source`, `Site__Spaces__0__FileSystem__RootPath` and `Site__Spaces__0__FileSystem__WatchForChanges` on four profiles. The same setting was therefore declared in three places, with precedence decided inside the host's configuration extension and visible from none of the three files.

### Impact

- The Learning Hub ran under the documentation site's space identifier in local development, so any future space-scoped behaviour (cache partitioning, metrics, a shared Redis prefix) would collide between the two.
- The `devdocs` overlay was titled `LearningHub` while serving this repository's own documentation.
- The plain `http` and `https` profiles selected the `Development` environment — which declares the repository's own `docs` folder — while their own environment variables pointed the content root at the Learning Hub clone. The two contradicted, and the reader could not tell from either file which applied.

## 🔍 Context information

| | |
|---|---|
| **Component** | `Diginsight.SmartDocs.Web` (server host) |
| **Framework** | .NET 10 |
| **Configuration surface** | `Site:Spaces` (`SiteOptions` → `SpaceOptions` → `SpaceRegistry`) |
| **Severity** | Medium — no outage; wrong identity and a latent collision, plus a contradiction that misleads readers |
| **Environments involved** | `Development`, `devdocs`, `devlearn` (public repository) · two deployment overlays (private peer) |
| **Status** | ✅ Resolved |

### Binding mechanics

`IConfiguration` flattens a JSON array into indexed keys:

```text
Site:Spaces:0:Id
Site:Spaces:0:Blob:ContainerName
Site:Spaces:1:Id
…
```

Later providers override **individual keys**. There is no array-level replace, and no truncation: a base array of two elements plus an overlay array of one element yields **two** elements, the second still carrying base values.

## 🔬 Analysis

### Root cause

The base file declared a complete space element. Because arrays merge by index, that element was not a default — it was a **floor that partially showed through** every environment. Any field an overlay failed to restate silently became that environment's value.

The mental model in use ("the environment file only carries what differs") is correct for scalars and objects and **false for array elements**, because the identity of an array element is its position, not its content. Position is not something an author of an overlay thinks about.

### Why it was not caught

- **No failure mode.** All four wrong values bound successfully to a valid type.
- **`RepositoryUrl` has no consumer yet.** It is bound by `SpaceOptions` and read by nothing; the space index page that will render it is designed but not built. The leak was therefore invisible on screen.
- **`Id` currently has a small blast radius.** It keys `SpaceContentRegistry` and appears in one startup log line. It does **not** key the cache — despite the doc comment on `SpaceOptions.Id` claiming it does. `ContentPathCacheKey` is `(Kind, Path)` with no space dimension, so two spaces sharing an `Id` collide only in the log today.
- **Three declarations hid each other.** With the same value in the base file, the overlay and the launch profile, any one of them appearing correct was taken as the effective value.

### The counter-evidence that settled the fix

The decisive fact came from the two deployment overlays in the private peer: **both already declared their space element in full**, including fields identical to the base. One of them overrides `RouteBase` to a prefixed mount, which works *only* because it restates the key.

So the base element was never load-bearing for any real deployment. It existed solely as documentation of the production shape — and in doing so, it created the one hazard it was meant to prevent.

### Latent failure the fix also removes

Had the base ever declared **two** spaces while an overlay declared one, the second element would have survived with `Source: Blob` and an empty `ContainerName`. `SpaceRegistry.Validate` throws on that, so the host would have refused to start, citing a space that appears in no file the operator was editing.

## 🔄 Reproduction

1. Declare a complete element in `Site:Spaces` in the base settings file.
2. In an environment overlay, declare `Site:Spaces` with one element carrying only the fields that differ.
3. Start the host with that environment selected.
4. Read the bound `Site:Spaces:0:*` keys.

**Observed:** the element is the union of both declarations, base fields winning wherever the overlay is silent.
**Expected by the author:** the overlay's element, and only the overlay's element.

### Affected code locations

- [SiteOptions.cs](../../../../Diginsight.SmartDocs.Web.Shared/Sites/SiteOptions.cs) — `SiteOptions.Spaces`, `SpaceOptions`
- [SpaceRegistry.cs](../../../../Diginsight.SmartDocs.Web.Shared/Sites/SpaceRegistry.cs) — `Validate`, which is the only guard on a malformed element
- [Program.cs](../../../../Diginsight.SmartDocs.Web/Program.cs) — eager binding and the space-registry log line

## ✅ Solution implemented

### 1. The base file declares no space

`Site:Spaces` is now `[]`, with the reason stated in place. An empty array contributes no indexed keys, so nothing can leak. A missing declaration now fails loudly at startup — `SpaceRegistry.Validate` throws `Site:Spaces is empty` — which is the correct failure for a host that would otherwise serve the wrong content set.

### 2. Every environment states its element in full

| Environment | `Id` | Space title | Source |
|---|---|---|---|
| `Development` | `diginsight.smartdocs` | SmartDocs | this repository's `docs` folder |
| `devdocs` | `diginsight.smartdocs` | SmartDocs, site titled *Diginsight documentation* | this repository's `docs` folder |
| `devlearn` | `learn` | Learning Hub | the Learning Hub content clone |

`devlearn` now mirrors the Learning Hub deployment overlay exactly except for `Source` — same identifier, same icon, same repository URL — so a local run and a deployed run agree on identity, and any future space-scoped key means the same thing in both.

### 3. A launch profile selects an environment and nothing else

All `Site__Spaces__0__*` variables were removed from every profile. The environment overlay is now the single declaration, so no precedence question arises. The deployment profile already worked this way and served as the model.

## 🧪 Validation

The host was started once per environment and the resolved registry read from its startup output:

| Environment | Resolved |
|---|---|
| `Development` | `Site 'Diginsight SmartDocs' publishes 1 space(s): diginsight.smartdocs @ /` |
| `devdocs` | `Site 'Diginsight documentation' publishes 1 space(s): diginsight.smartdocs @ /` |
| `devlearn` | `Site 'Learning Hub' publishes 1 space(s): learn @ /` |
| deployment overlay (peer) | `Site 'Learning Hub' publishes 1 space(s): learn @ /`, source `Blob` |

The full `Site:Spaces:*` key dump was checked in both directions: **no `Blob:*` keys** appear under the file-system environments, and **no `FileSystem:*` keys** appear under the deployment environment. The leak is closed on both sides, not merely masked.

Both file-system environments were then run in a visible console and checked in a visible browser:

- `devlearn` → Learning Hub branding, 1,133 articles from the Learning Hub content clone
- `devdocs` → *Diginsight documentation* branding, SmartDocs navigation, 39 articles from this repository's `docs` folder

## ✔️ Resolution status

**Status:** ✅ Resolved · **Verified:** 2026-08-21

- [x] Base file declares no space element
- [x] All three public environments state their element in full
- [x] Redundant launch-profile variables removed
- [x] Every environment verified to bind correctly, including the deployment overlay
- [x] Both file-system environments verified in a visible browser
- [x] Solution builds clean; working tree contains only the intended files

### Follow-up actions

- [ ] **Behaviour change to communicate.** The plain `http` and `https` profiles now serve this repository's `docs` folder, which is what their environment always declared. Use the `devlearn` profile for the Learning Hub.
- [ ] **Refresh the derived documentation.** [01-configuration-settings.md](../../../06.00-reference/01-configuration-settings.md) and the `configuration-07` assertion in [configuration.md](../../../_evidence/smartdocs-web/configuration.md) both describe a base `Spaces` element that no longer exists. Both are verification-stamped, so they need a re-stamped update rather than an in-place edit.
- [ ] **Correct or satisfy the `SpaceOptions.Id` doc comment.** It claims the identifier keys the cache; `ContentPathCacheKey` carries no space dimension. Either fix the comment or add the dimension — the second becomes necessary the moment a shared cache backs two spaces.
- [ ] **Declare the Learning Hub clone location.** The `devlearn` overlay depends on a four-level-up sibling checkout that is documented nowhere; a differently placed clone fails at startup with no hint of the expected layout.

## 🎓 Lessons learned

- **An array element's identity is its position.** Every other configuration merge is keyed by name, so authors reason about arrays with the wrong model. Where an array is layered, the safe rule is that **only one layer declares it** — and the layer that declares it states every field.
- **"Overlay" and "default" both imply replacement, and neither is true here.** The vocabulary made the base element look like a fallback. A fallback that partially shows through is not a fallback; it is a floor.
- **Redundant declarations do not add safety — they subtract observability.** The same value in three files meant no file could be read as authoritative, and the contradiction between the profile and its environment survived because both looked deliberate.
- **A leak with no consumer is still a leak.** `RepositoryUrl` was harmless only because the page that renders it does not exist yet. Correctness that depends on a feature being unbuilt is a scheduled defect.
- **Loud beats silent when the alternative is serving the wrong thing.** Removing the base element converted "boots with unnoticed wrong identity" into "refuses to boot" — a strict improvement for a content host.

## 📡 Signals

The sweep found six activities outside this issue's goal. Five are on [02-signals.md](02-signals.md); one is on [03-other-signals.md](03-other-signals.md).

Highest relevance: the `configuration-drift` invariant class in the hardening catalogue does not describe this violation shape, so the robustness stream cannot currently detect it; and the two deployment overlays in the private peer are now the sole declaration of `Site:Spaces` without stating that they are.

## 📎 Appendix

### Files changed

| File | Change |
|---|---|
| `src/Diginsight.SmartDocs.Web/appsettings.json` | `Site:Spaces` reduced to `[]`, with the binding rationale stated in place |
| `src/Diginsight.SmartDocs.Web/appsettings.Development.json` | element stated in full |
| `src/Diginsight.SmartDocs.Web/appsettings.devdocs.json` | element stated in full; site title corrected to the documentation site |
| `src/Diginsight.SmartDocs.Web/appsettings.devlearn.json` | element stated in full; identifier, icon and repository URL corrected to the Learning Hub |
| `src/Diginsight.SmartDocs.Web/Properties/launchSettings.json` | `Site__Spaces__0__*` removed from all four development profiles |

No source file was changed. The defect was entirely in the configuration layering.

### Validation-harness notes

Three traps cost time and are worth recording, because they will recur on the next validation of this host:

- The running host process is named for the assembly, **not** `dotnet`, so a process kill filtered on `dotnet` leaves it holding the port.
- `Start-Process` **inherits** the calling shell's environment, so a leftover `AppsettingsEnvironmentName` or `ASPNETCORE_URLS` silently overrides the launch profile the new window was told to use.
- The article count in the footer settles after hydration; the first value read is the previous page's.

## 🏁 Conclusion

A settings file declared a space so that readers could see the production shape. Because configuration binds arrays by index, that courtesy became the mechanism by which the documentation site's identity, icon and repository URL travelled into the Learning Hub — undetected, because every wrong value bound successfully and none of them was rendered yet.

The fix is structural rather than corrective: the base declares nothing, each environment declares everything, and a launch profile chooses an environment and stays out of the way. A forgotten declaration now stops the host instead of quietly serving the wrong content.

What remains is smaller and named: two derived documents describe the element that was removed, a doc comment claims a cache behaviour that does not exist, and the overlays that are now the sole declaration do not say so.

## 📚 References

- **[Configuration providers in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)** 📘 [Official] — provider ordering and key-based override semantics
- **[Options pattern in .NET](https://learn.microsoft.com/dotnet/core/extensions/options)** 📘 [Official] — binding of nested objects and collections
- **[01-configuration-settings.md](../../../06.00-reference/01-configuration-settings.md)** 📗 [Repository] — the configuration reference this change makes stale
- **[09-hardening-invariant-catalog.md](../../../../../.copilot/context/10.00-application-development/09-hardening-invariant-catalog.md)** 📗 [Repository] — the `configuration-drift` invariant this defect extends
- **[01-overview.md](../20260817.01-deployfail/01-overview.md)** 📗 [Repository] — prior analysis using the same public/internal split for this environment

### Additional information 

Not resolvable from this repository; listed for provenance.

- **`.../20260821.01-appsettings-space-layering/01-overview.internal.md`** — the two deployment overlays as read, the developer-machine layout, the commands as executed, and the verified startup key dump
- **`src/docs/_aliases/testmc.aliases.internal.md`** — resolves the role names used above
