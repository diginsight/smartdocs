---
title: "Diginsight.SmartDocs.Web — convergence, multi-space rendering and Testmc deployment"
author: "Dario Airoldi"
date: "2026-08-15"
categories: [smartdocs, rendering, blazor, deployment, github-actions, azure]
description: "Migrates the Learn.Web rendering application into diginsight/smartdocs as Diginsight.SmartDocs.Web with unchanged behaviour for the learning hub, generalises it to render any repository's documentation through configured route bases, and deploys the one artifact to its two Testmc targets."
status: actionable
---

# Diginsight.SmartDocs.Web — convergence, multi-space rendering and Testmc deployment

## 📑 Table of contents

- [🎯 Goal and scope](#-goal-and-scope)
- [🧭 Decisions taken](#-decisions-taken)
- [⚠️ Pre-flight warnings](#️-pre-flight-warnings)
- [🔢 Execution order](#-execution-order)
- [📦 WS-A-relocation — repository preparation and projects into `src`](#-ws-a-relocation--repository-preparation-and-projects-into-src)
- [🧱 WS-B-space-model — configuration and resolution](#-ws-b-space-model--configuration-and-resolution)
- [🔌 WS-C-endpoints — space-addressed surface](#-ws-c-endpoints--space-addressed-surface)
- [🖼️ WS-D-space-index — generated index and switcher](#️-ws-d-space-index--generated-index-and-switcher)
- [🎨 WS-K-branding — configurable app-level look and feel](#-ws-k-branding--configurable-app-level-look-and-feel)
- [🔨 WS-E-scaffolding — solution, build files and local run](#-ws-e-scaffolding--solution-build-files-and-local-run)
- [🔐 WS-F-internal-config — move Testmc configuration to `smartdocs.internal`](#-ws-f-internal-config--move-testmc-configuration-to-smartdocsinternal)
- [🚀 WS-G-deployment — build and deploy the learning hub](#-ws-g-deployment--build-and-deploy-the-learning-hub)
- [🏢 WS-L-docs-instance — deploy the documentation site](#-ws-l-docs-instance--deploy-the-documentation-site)
- [📤 WS-H-content-publishing — the `diginsight.smartdocs` space](#-ws-h-content-publishing--the-diginsightsmartdocs-space)
- [🧪 WS-I-validation — visible browser evidence](#-ws-i-validation--visible-browser-evidence)
- [🧹 WS-J-retirement — remove from the source repositories](#-ws-j-retirement--remove-from-the-source-repositories)
- [🔎 Discovery](#-discovery)
- [🗳️ Open decisions](#️-open-decisions)
- [🅿️ Park lot](#️-park-lot)
- [🏁 Exit criteria](#-exit-criteria)
- [📚 References](#-references)

## 🎯 Goal and scope

Migrate the three-project Markdown rendering application from `Learn.01` into **this repository** — `diginsight/smartdocs` — as **`Diginsight.SmartDocs.Web`**, preserving its rendering behaviour exactly, and evolve it so that it renders **any repository's documentation**.

Two obligations. The plan fails if either is missed:

| Obligation | What it means |
|---|---|
| **Compatibility** | the learning hub keeps working through this renderer with **no URL change, no behaviour change and no content change** |
| **Generalisation** | any repository's documentation — produced by the `@ad-documentation-manager` autonomous stream — is published to a container and rendered as a **space**, by configuration alone, with no code change |

Diginsight's repositories are the **first consumers** of the second obligation, not its definition. `diginsight.smartdocs` — this repository's own `src/docs` — is one such space and is published alongside the others.

The renderer is deployed to **two targets** from one build — see `D17-two-deployments-one-codebase`:

| Deployment | Spaces | Routes | Branding | Host |
|---|---|---|---|---|
| **Learning Hub** | the `learn` space, root-mounted | `/**`, exactly as today | none — current appearance preserved | `learn-testmc-app-itn-01`, replacing `Learn.Web` |
| **Diginsight Documentation** | one per repository — `diginsight.smartdocs`, `diginsight.tools`, `diginsight.components`, … | `/{route-base}/**`, generated index at `/` | Diginsight logo and palette | `docs-testmc-app-itn-01`, on the same B1 plan — `D18-docs-instance-host` |

The learning hub is **not** one of the documentation spaces. It is a separate publication that happens to run the same renderer.

| Concern | Today | After |
|---|---|---|
| Application location | `Learn.01/src/Learn.Web{,.Client,.Shared}` | `smartdocs.01/src/Diginsight.SmartDocs.Web{,.Client,.Shared}` |
| Content roots | one, fixed | many, one per space, config-driven |
| Route | `/**` | wherever the space's `RouteBase` says — absent or `/` mounts at the root, `/x` mounts at `/x` (`D14-route-base-is-configured`) |
| Render model | SSR prerender + interactive WebAssembly | **identical — inherited unchanged** |
| Look and feel | compiled-in stylesheet and title | configurable logo, palette and site title per deployment |
| Environment configuration | `Learn.internal`, via `ConfigureAppConfiguration2` | `smartdocs.internal`, via `ConfigureAppConfiguration2` (`D19-public-repository-internal-configuration`) |
| Build and deploy | `darioairoldi/Learn` → `deploy-learnweb.yml` | `diginsight/smartdocs` → `01.DeployLearnHub.yml` and `02.DeployDocsSite.yml` (`D13-one-workflow-per-target`) |
| Target App Service | `learn-testmc-app-itn-01`, 32-bit worker | `learn-testmc-app-itn-01`, **64-bit worker**, plus `docs-testmc-app-itn-01` |

**In scope** — repository preparation, relocation, the space model, space-addressed endpoints, the generated index and switcher, configurable app-level branding, the solution and build files, the configuration move to `smartdocs.internal`, one deployment workflow per target, publishing this repository's `src/docs` as the `diginsight.smartdocs` space, browser validation, and retirement at source once verified.

**Explicit non-goals** — no new Azure infrastructure: both App Services, the shared plan and the storage account already exist and are reused (`D18-docs-instance-host`). **This repository's own product documentation is not authored here** — that is a sibling plan (`PL-11-smartdocs-own-documentation`); what is in scope is the *mechanism* that renders it. No AI content services; no change to the rendering pipeline, the navigation algorithm or the component set beyond what the space dimension requires; no per-**space** theme override (branding is per deployment — `PL-4`); no new SEO artifacts beyond preserving what prerendering already produces (`PL-9`); no onboarding of repositories other than this one (`PL-5`).

## 🧭 Decisions taken

These are closed. Re-opening any of them drops this plan back to `status: draft`.

**`D1-name-smartdocs`** — the projects are `Diginsight.SmartDocs.Web`, `Diginsight.SmartDocs.Web.Client`, `Diginsight.SmartDocs.Web.Shared`. The assembly names track the repository's own name, `diginsight/smartdocs`, rather than borrowing a family marker from another repository.

**`D2-projects-at-src-root`** — the projects live directly under `src/`, as `src/Diginsight.SmartDocs.Web{,.Client,.Shared}/`.

Supersedes `D2-folder-50-00-docs`, which placed them under `src/50.00 Docs/`. That decision existed to fit the `NN.NN Name` convention of `diginsight/tools`, a repository hosting several unrelated deliverables that need ordering. This repository hosts **one** deliverable. A numbered category folder holding exactly one category is noise, and the dependency on the sibling FeedMonitor plan's `60.00 Test` renumbering disappears with it.

**`D3-site-section`** — the configuration section is `Site`, holding `Spaces[]`. The current flat `Content` section is replaced, not extended, because a section named for a single content root cannot honestly hold a list of them. The obsolete `Content__*` application settings are removed in the same step that writes the new ones.

**`D4-container-per-space`** — each space maps to its **own blob container** on the shared storage account, not to a prefix inside one container. Rationale: the `learn` container already exists with content at its root, so per-container mapping leaves the learning-hub publishing workflow **completely unchanged**, and a per-repository SAS can be scoped to a container boundary.

**`D5-id-vs-container-naming`** — a space `Id` is a URL path segment and may contain dots (`diginsight.tools`); an Azure blob container name may **not** (lowercase letters, digits and hyphens only). The two are therefore separate fields: `Id: diginsight.tools` maps to `ContainerName: diginsight-tools`. Never derive one from the other by convention.

**`D6-invalidate-backward-compatible`** — the cache-invalidation endpoint keeps its current path `/_nav/invalidate`, its `POST` method and its existing `?path=` parameter, and gains an **optional** `?space={id}` query parameter. Absent parameter means "invalidate every space". The existing learning-hub content workflow therefore keeps working with no edit.

**Correction, verified 2026-08-16.** An earlier form of this decision stated the endpoint "keeps its `X-Invalidate-Key` header". It has no such header today. `ContentOptions.InvalidateApiKey` is declared and `deploy-learninghub.yml` sends `X-Invalidate-Key`, but `NavEndpoints.InvalidateNavCache` never reads it — the live endpoint is **unauthenticated**. Header validation is therefore **new work**, specified in `Step C3`, not a port. The caller is already sending the header, so adding enforcement is backward compatible in the direction that matters.

**`D7-learn-space-compatibility`** — the compatibility contract attaches to the **learning hub's configuration**, never to a space count. Configured as `Step F1` specifies — the `learn` space, `RouteBase: "/"`, no branding — the application must be indistinguishable from today.

Supersedes `D7-single-space-is-degenerate`, which made "exactly one space" the trigger for compatible behaviour. That form conflated two independent things: *how many* spaces a deployment serves, and *where* a space is mounted. Under it, a single repository's documentation could not be mounted at `/tools` — being alone would force it to `/` — which directly blocks this plan's generalisation obligation. The regression guard loses no strength; it is attached to the configuration that must not regress rather than to an arithmetic property.

**`D8-publish-profile-x64`** — the publish step produces a **self-contained `win-x64`** payload and keeps the **zero-byte Brotli asset scrub**.

The architecture change is not free: the target App Service currently runs a **32-bit worker process**, and a `win-x64` payload on a 32-bit worker fails to load with `HTTP 500.32 - ANCM Failed to Load dll`. Switching the worker to 64-bit is therefore a **prerequisite of the first deployment**, not a remediation applied after a failure — it is `Step G3`, and it runs before the deploy action every time so a manually reverted portal setting cannot silently break a later run.

The Brotli scrub is unrelated to architecture and stays unconditional: it removes broken Brotli siblings from both `wwwroot/_framework` and the static-web-assets endpoint manifest, so browsers negotiate gzip or identity instead of receiving an empty asset.

**`D9-config-authority`** — `appsettings.Testmc.json` in `smartdocs.internal` is authoritative for the space list. App Service application settings carry only `AppsettingsEnvironmentName` and the invalidation key.

Two reasons, and the second is the stronger. A space list is a structured array; expressing it as flat double-underscore settings is unreadable and drifts. And per `D19-public-repository-internal-configuration` this repository is **public**, so an environment's storage endpoints, container inventory and host names must not be committed here at all — the authoritative file has to live in the private repository regardless of how readable the alternative was.

This differs deliberately from the current action, which pins storage settings as explicit overrides — the override is replaced by fail-closed validation of the configuration file at publish time, which the current action already performs.

**`D10-vars-for-configuration-secrets-for-secrets`** — the rule is mechanical and admits no exceptions: **`vars.`** for everything non-sensitive, **`secrets.`** for everything sensitive.

Non-sensitive means the OIDC identifiers (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`), the App Service name, the resource group, the storage account and the container names — none of these grant access on their own. Sensitive means the internal-repository token, the cross-repository dispatch token and the cache-invalidation key.

The source repository placed the OIDC identifiers in `secrets.*`; do not carry that over. Variables and secrets that do not exist yet are **provisioned as part of `Step G1`**, not assumed to be present — an absent value resolves to an empty string rather than an error, so a missing variable surfaces as a confusing login failure much later.

**`D11-cutover-then-retire`** — projects are added here, deployed, and verified in a browser **before** anything is removed from the source repositories. Retirement is `WS-J-retirement`, gated on the validation exit criterion.

**`D12-tools-scaffolds-out-of-scope`** — `src/20.00 Api/SmartDocs` and `src/20.00 Api/SmartDocsApi` live in `diginsight/tools` and are not touched by this plan. They now sit in a different repository from the renderer whose name they share; whether they should follow it here is a separate question, parked as `PL-3-scaffold-cleanup`.

**`D13-one-workflow-per-target`** — one **reusable build** workflow, plus one **deployment** workflow per target, numbered so that adding a target is adding a file rather than adding a branch to a shared one:

| Workflow | Kind | Target |
|---|---|---|
| `00.BuildSmartDocsWeb.yml` | reusable — `workflow_call` | none: produces the `smartdocs-web` artifact |
| `01.DeployLearnHub.yml` | deployment | `learn-testmc-app-itn-01` |
| `02.DeployDocsSite.yml` | deployment | `docs-testmc-app-itn-01` |
| `03.PublishDocsContent.yml` | content | the `diginsight-smartdocs` container |

Supersedes `D13-dedicated-deployment-workflow`, whose `22.` numbering and whose entire rationale — *"keeps the FeedMonitor deployment path provably unaffected"* — existed to coexist with `diginsight/tools`. In a dedicated repository there is no FeedMonitor to protect and no `20.`/`21.` series to slot into.

The build stays in **one** file even though the deployments are separate. Two independent build definitions would let the two sites drift, and the drift would surface as a behaviour difference nobody could attribute — the property `D17-two-deployments-one-codebase` depends on is that both hosts run the *same artifact*, not merely the same source.

**`D14-route-base-is-configured`** — where a space mounts is stated by its **`RouteBase`**, and by nothing else. The space count never affects routing.

| `RouteBase` | Space mounts at | Content route |
|---|---|---|
| absent, or `/` | the site root | `/{**path}` — for the learning hub, **byte-identical to today** |
| `/x` | `/x` | `/x/{**path}` — including when it is the only space configured |

Four rules follow, all enforced at startup by `Step B2`:

1. **At most one space may claim the root.** Two root-mounted spaces are an unresolvable ambiguity, so the host fails to start rather than picking one.
2. **Longest prefix wins.** `/diginsight.tools` is matched before a root-mounted space's catch-all.
3. **A non-root `RouteBase` reserves its first segment** from any root-mounted space — always, not only when several spaces are configured.
4. **The generated index is served at `/` when no space claims the root**, and only then. It is a function of the root being free, not of the space count.

The switcher is the one surface that legitimately keys on the count: it renders when more than one space is configured, because a control offering a single destination is noise.

Supersedes `D14-root-mount-when-single-space`, which made prefixing conditional on the space count. That form protected the learning hub correctly — `https://learn-testmc-app-itn-01.azurewebsites.net/00.00-getting-started` must not become `/learn/00.00-getting-started`, or every bookmark, inbound link and indexed URL breaks — but it protected it by an accident of arithmetic, and it made a single repository's documentation **unmountable** at `/tools`. Stating the mount point explicitly protects the hub for the right reason and unblocks the generalisation obligation at the same time.

Redirects remain rejected: they would leave the learning hub permanently serving 301s for its entire URL space.

**`D15-branding-is-per-deployment`** — logo, colour palette and site title are **app-level** configuration under `Site:Branding`, applied to every space the deployment serves. They are supplied as configuration and content assets, never compiled in.

A space keeps the `Title` and `Icon` it already has in `Step B1`; those label the space inside a branded shell. A space does **not** carry its own logo or palette.

Rationale: the request is *"a custom layout can be configured **for the app**"*, and the observed reference deployment renders every space — `/apidevice/`, `/em-adapter/` and the root index — inside one identical shell with one logo and one palette. Branding identifies the **publisher**, and one deployment has one publisher. Per-space override is a different feature with a different justification and is parked as `PL-4`.

**`D16-prerender-parity-by-construction`** — server-side rendering is **inherited**, and it survives the port only if five structural properties are preserved. Each is a construction rule that governs how steps are written, not a test run afterwards.

| # | Rule | What breaks if it is violated |
|---|---|---|
| 1 | **Symmetric registration** — every service the shared components resolve is registered in *both* `Program.cs` files with the same lifetime | server-only ⇒ prerender succeeds then hydration throws; client-only ⇒ prerender throws |
| 2 | **The space rides in the path, never in DI** | the prerendered article is replaced by an empty or wrong-space page at hydration |
| 3 | **Server content-source lifetimes stay singleton** | captive dependency: host fails at startup in Development, silently serves one space's content in Production |
| 4 | **Assembly identity tracks the rename** | the server discovers no route, prerenders *Not found*, and the client repairs it — the browser looks perfect and prerendering is gone site-wide |
| 5 | **`<base href="/" />` and the `MapStaticAssets()` ordering stay as they are** | relative content fetches break under a space prefix, or the client bootstrap is not served |

Rule 1 is a consequence of how the render model works. `ContentView` resolves `PageLoader`, `INavProvider`, `TocState`, `ArticleState` and `IJSRuntime`; the *same component* runs twice — once on the server during prerender against the server container, once in the browser against the WebAssembly container. Every one of those services is therefore registered twice today, `HttpContentSource` / `HttpNavProvider` on the client standing in for `CachedContentSource` / `ServerNavProvider` on the server. The two `Program.cs` files must always be edited in the same step.

Rule 2 is the most dangerous one and the reason this decision exists. `ContentPage.razor` declares `@page "/"` and `@page "/{*path}"` and passes `Path` down; `ContentView` never reads the URL itself, and `IContentSource.GetAsync` takes the content key **as a string**. That is the seam multi-space must use: the space is carried in the path and resolved to a key identically on both sides. It must **not** be selected by a DI factory reading `IHttpContextAccessor` — there is no `HttpContext` in WebAssembly, so such a design resolves the right space during prerender and nothing after hydration. Every build passes, every unit test passes, and the site loses its rendered first response.

Rule 3 follows from the current lifetimes. `IContentSource` and `IContentLister` are singletons, and `FolderMetricsIndex`, `DynamicNavBuilder`, `CachedDynamicNavBuilder` and `INavBuilder` are singletons that capture them. Making the content source scoped so it can observe the request is a captive dependency. Multi-space must therefore be a **singleton registry of per-space singletons**, selected by an explicit space argument — see `Step B3`.

Rule 4 is a rename hazard, not a design one. `Routes.razor` declares `AppAssembly="typeof(Marker).Assembly"` and the host calls `.AddAdditionalAssemblies(typeof(Learn.Web.Client.Marker).Assembly)`; that call is what lets the **server** find the routable `ContentPage` while prerendering. `WS-A` must treat it as a named artifact.

Consequence: with these five rules held, the port is a rename plus a path-resolution change, and the replacement is behaviourally indistinguishable. `Step I2` then **proves** the rules held — it does not create the property.

**`D17-two-deployments-one-codebase`** — the learning hub and the Diginsight documentation site are **two deployments of the same artifact**, never two spaces of one deployment.

| Deployment | Content | Spaces | Shape | Branding |
|---|---|---|---|---|
| **Learning Hub** | authored personal learning material | one | root-mounted | none — keeps its current appearance |
| **Diginsight Documentation** | generated per-repository documentation | many | prefixed, index at `/` | Diginsight logo and palette |

**They are different publications.** A learning hub is authored, personal, and addressed to its own audience; repository documentation is generated from source, exists once per repository, and belongs under a publisher's shell. Merging them puts a *Learning Hub* tile on the Diginsight documentation index and a *Diginsight Tools* tile inside a personal learning site — each is noise to the other's reader. `D15-branding-is-per-deployment` makes this concrete: one deployment has one publisher, and these have two.

**Withdrawn second argument.** An earlier form of this decision also argued that the merge was *mechanically impossible* — that adding a second space would force the learning hub's URLs to move. Under `D14-route-base-is-configured` that is **false**: a root-mounted `learn` space and a `/diginsight.tools` space coexist without moving a single hub URL. The decision therefore rests on the first argument alone, and is an **editorial** choice about what belongs in a publication rather than a constraint imposed by the routing model. `Step F1` and `Step L2` must state and check it, because nothing now enforces it mechanically.

This correction supersedes the earlier shape of `Step F1`, `WS-H-content-publishing` and two exit criteria, all of which assumed one App Service carrying both.

**`D18-docs-instance-host`** — the Diginsight documentation site runs on **`docs-testmc-app-itn-01`** in resource group `learn-testmc-rg-itn-01`, sharing the existing `samples-testmc-asp-01` Basic B1 plan in `samples-testmc-rg-itn-01`. Provisioned 2026-08-16.

It is a **separate App Service sharing a plan**, not a separate plan. `D17-two-deployments-one-codebase` requires two hosts because a host serves one space list — not because it needs its own compute. `learn-testmc-app-itn-01` already sits on that plan from a different resource group, so the second site joins an established arrangement at no additional cost.

Created with what `D8-publish-profile-x64` requires, so `Step G3` has nothing to correct on this instance: **64-bit worker**, .NET 10, HTTPS-only, TLS 1.2, FTP disabled, and a system-assigned managed identity (`0442bb0f-9825-4d03-8dd3-4acf18a70e23`) for the container reads granted in `Step L4`.

Accepted consequence: B1 Basic is a single instance with no deployment slots, so the two sites share one worker and each deployment is a short outage for that site alone. `PL-10-docs-plan-capacity` holds the upgrade if that ever matters.

**`D19-public-repository-internal-configuration`** — `diginsight/smartdocs` is a **public** repository. Everything environment-specific therefore lives in the private `smartdocs.internal` repository and is composed at deployment time through Diginsight's `ConfigureAppConfiguration2`, exactly as `Learn.Web` composes its Testmc configuration from `Learn.internal` today.

| Lives here, public | Lives in `smartdocs.internal`, private |
|---|---|
| `appsettings.json` — structure and defaults | `appsettings.Testmc.json` — the space list, storage endpoints, container names, observability sinks |
| `appsettings.Development.json` — a filesystem source pointing into the working tree | any further environment file |

The mechanism is inherited, not invented: `ConfigureAppConfiguration2` overlays external files selected by `AppsettingsEnvironmentName` and `ExternalConfigurationFolder`. The deployment workflow checks out `smartdocs.internal` and copies the selected file into the publish root (`Step G2`), so the running host sees one merged configuration and no code is aware of the split.

Consequence binding every step in this plan: **no step may write a storage account name, a container name, an App Service host name or a key into a file under this repository.** Every one of those values lives in `smartdocs.internal` (`D9-config-authority`, `D21-deployment-target-travels-with-configuration`); only genuine credentials are repository secrets. A step that appears to require otherwise is wrong.

**`D20-inherited-workflows-removed`** — `.github/workflows/20.DeployTools.yml`, `21.DeployAppService.yml` and `quarto-publish.yml` arrived here with the rest of the copied `.github/` scaffolding and are **deleted** as the first executed step, `Step A0`.

They are not dormant. `quarto-publish.yml` triggers on **every push to `main` and every pull request**, in a repository with no Quarto site — it will run, and either fail or publish an empty Pages site, the moment this plan's first commit lands. `20.DeployTools.yml` restores `src/Diginsight.Tools.sln`, which does not exist here, and deploys `diginsighttools-testmc-job-itn-01`, which is not this repository's. `21.DeployAppService.yml` is a reusable WebJob workflow with no caller here.

This closes `PL-1-quarto-retirement`, which deferred Quarto removal until the documentation space was live. That deferral was right in `diginsight/tools`, where the Quarto site was actually serving; here the file is a copy artifact that never served anything.

**`D21-deployment-target-travels-with-configuration`** — the storage account, the container and the App Service name are **not** repository secrets. Each is read at run time out of the overlay in `smartdocs.internal` that already declares it.

The storage values were never anything but duplicates. `SpaceOptions` already carries `Source`, `Blob.AccountUri` and `Blob.ContainerName` **per space**, and both overlays already state them; a `SMARTDOCS_STORAGE_ACCOUNT` secret was a second copy of a value the serving host reads from the file, with nothing keeping the two in agreement. Change the container in the overlay and the publishing workflow keeps writing to the old one — the site goes empty while neither value is individually wrong. Worse, a flat pair of secrets caps the system at one destination, so the per-space model the code already implements could not be reached: a second space with a different account would need a second pair of secrets, and a third a third.

So `03.PublishDocsContent.yml` takes a **space id**, not a destination. It resolves `Blob.AccountUri` and `Blob.ContainerName` from that space, and refuses a space whose `Source` is not `Blob` — a filesystem-backed space would otherwise publish successfully and change nothing, which is the worst kind of green run.

The App Service name is a different kind of value and is handled differently. It is **not** a space property — a space does not know what host serves it, and two spaces routinely share one deployment — so it does not belong in `Spaces[]`. It sits in a sibling `Deployment` block in the same file, because the overlay and the host it is deployed to must change together; a secret holding the host name lets overlay A be deployed to host B silently. `00.BuildSmartDocsWeb.yml` reads it, then **strips the block** before staging the file, so the running host is never handed a section it has no binding for.

Log masking, which was the original reason for making these secrets, is preserved by `::add-mask::` on each resolved value before it can reach a log line. That one line buys back everything the secrets were providing.

Consequence: `D10-vars-for-configuration-secrets-for-secrets` is **no longer overridden anywhere in this plan**. The override existed only because non-secret values were being stored as secrets. With them in the private repository, the four remaining repository secrets are all genuine credentials or tenant identifiers, and `D10` holds unmodified.

Accepted cost: `03` gains a checkout of `smartdocs.internal`, so content publishing now depends on the internal read token that `01` and `02` already require.

## ⚠️ Pre-flight warnings

Read before executing any step.

- **This repository has no build scaffolding at all.** There is no solution, no `Directory.Build.props`, no `Directory.Build.targets` and no `nuget.config` — `src/` contains only documentation. The project files therefore do **not** port with name changes only: their conditional Diginsight import switches (`DiginsightCoreSolutionDirectory`, `DiginsightCoreDirectImport`) and version properties (`DiginsightCoreVersion`, `DiginsightSmartcacheVersion`, `DiginsightComponentsVersion`) resolve against build files that must be **created first**, in `Step A0`.
- **`RestorePackagesWithLockFile` must be set to `true`** in the `src/Directory.Build.props` created by `Step A0`. The first restore then generates `packages.lock.json` for all three projects. These files must be committed — the deployment workflow's NuGet cache keys on `hashFiles('**/packages.lock.json')`.
- **There is no build baseline to compare against.** The roughly 166 pre-existing nullable warnings (CS8604 / CS8618) quoted by earlier revisions were the `Diginsight.Tools.sln` baseline and mean nothing here. The bar is **0 errors** for a solution containing only these three projects; record the warning count observed at `Step E2` as this repository's baseline.
- **The App Service runs a 32-bit worker process today.** Per `D8-publish-profile-x64` the payload is `win-x64`, so the platform must be switched **before** the first deployment or the site answers `HTTP 500.32 - ANCM Failed to Load dll`. This is `Step G3`.
- **The three inherited workflows are live, not dormant.** Per `D20-inherited-workflows-removed`, `quarto-publish.yml` fires on every push to `main` and every pull request. `Step A0` deletes all three, and it runs before anything else in this plan.
- **A missing repository variable is not an error at expansion time** — it resolves to an empty string. `Step G1` provisions every variable and secret up front precisely so that an omission fails loudly at a validation step rather than quietly at `azure/login`.
- **The OIDC federated credential is scoped per repository.** The identifiers reused by earlier revisions were valid in `diginsight/tools`; a credential whose subject names that repository will not authenticate a workflow running here. `Step G0` adds the subject for `diginsight/smartdocs` before any workflow is written, and **every** variable and secret in `Step G1` is created from nothing — none is inherited.
- **`smartdocs.internal` is empty** — `README.md` and `LICENSE` only, no `src/`, no `.github/`. `WS-F-internal-config` establishes its structure rather than adding a file beside an existing convention.
- **This repository is public.** Per `D19-public-repository-internal-configuration`, no step may commit a storage account name, container name, App Service host name or key to a file here.
- **The render model is inherited, not rebuilt.** `App.razor` marks `HeadOutlet` and `Routes` as `@rendermode="InteractiveWebAssembly"` with prerendering left on, and `ContentView` loads content in `OnParametersSetAsync` — which runs *during* prerender. That pairing is what makes the first response contain real article HTML for crawlers while the client router takes over for SPA navigation afterwards. **Do not change render modes, do not move content loading to `OnAfterRenderAsync`, do not add `PrerenderMode=false`.** Any of the three silently removes server-side rendering while the site still looks correct in a browser.
- **`/_nav` is a route group with five children, not one endpoint.** `NavEndpoints.MapNavEndpoints` maps `/_nav/children`, `/_nav/version`, `/_nav/total`, `/_nav/index` and `/_nav/invalidate`. A naive `/_nav/{space}` route is **ambiguous with all five**. See `Step C2`.
- **`IContentSource` and `IContentLister` are the same object.** `Program.cs` registers the lister by **downcasting** the content source: `services.AddSingleton<IContentLister>(sp => (IContentLister)sp.GetRequiredService<IContentSource>())`. `BlobContentSource`, `FileSystemContentSource` and `CachedContentSource` all implement both; `HttpContentSource` (client-side) implements only `IContentSource`. A per-space factory that returns only `IContentSource` **compiles cleanly and breaks navigation at runtime**. See `Step B3`.
- **`/_nav/invalidate` is unauthenticated today.** The key is configured and sent but never checked. Treat enforcement as new code, not a port — `Step C3`.
- **There are two dependency-injection containers, not one.** `Diginsight.SmartDocs.Web/Program.cs` builds the container used for **prerendering**; `Diginsight.SmartDocs.Web.Client/Program.cs` builds the one used **after hydration**. They register the same service names with the same lifetimes and different implementations. Any step that adds, removes or re-lifetimes a service consumed by a shared component must edit **both**. See `D16-prerender-parity-by-construction`.
- **Never derive the current space from `IHttpContextAccessor` inside a DI factory.** WebAssembly has no `HttpContext`. The space must travel in the route path and be resolved to a content key by code that runs identically on both sides.
- **`.AddAdditionalAssemblies(typeof(Learn.Web.Client.Marker).Assembly)` is load-bearing for prerendering.** It is the only reason the server can route to `ContentPage`. Renaming the client assembly without updating this call removes server-side rendering from every page while leaving the browser experience apparently intact.
- **Line numbers and file inventories in this plan are anchored to 2026-08-15; code observations to 2026-08-16; repository-state observations to 2026-08-17.** Re-locate by symbol name, not by line, if execution is delayed.

## 🔢 Execution order

**The workstream letters are historical, not sequential.** They record the order in which the workstreams were written, not the order in which they run. Execute in the order below; each row names what the entry cannot start without.

| # | Execute | Cannot start before | Why |
|---|---|---|---|
| 1 | `WS-A-relocation` | — | `Step A0` deletes a workflow that fires on the first push to `main` |
| 2 | `WS-E-scaffolding` steps E1–E2 | `Step A0` | the solution and the first successful build — see the note below |
| 3 | `WS-B-space-model` | WS-A | the rename must land before the space dimension is added |
| 4 | `WS-C-endpoints` | WS-B | endpoints address the spaces the registry defines |
| 5 | `WS-D-space-index` | WS-C | the index and switcher render from `/_spaces` |
| 6 | `WS-K-branding` | WS-D | the index and switcher sit inside the branded shell |
| 7 | `WS-E-scaffolding` step E3 | WS-B, WS-K | the local run exercises the space model and the branding |
| 8 | `WS-F-internal-config` | WS-B | the configuration file carries the `Site` block |
| 9 | `WS-G-deployment` | WS-E, WS-F | builds the solution and carries the internal configuration |
| 10 | `WS-L-docs-instance` steps L1–L4 | WS-G | the second instance deploys and serves an empty index until content exists |
| 11 | `WS-H-content-publishing` | `Step L3` | `Step H3` invalidates the documentation deployment, which must already be running |
| 12 | `WS-L-docs-instance` step L5 | WS-H | it validates rendered content, which `Step H2` publishes |
| 13 | `WS-I-validation` | WS-G, WS-L | it validates both deployments |
| 14 | `WS-J-retirement` | WS-I | `D11-cutover-then-retire` |

**`WS-L` and `WS-H` interleave; they do not nest.** `Step L3` deploys the documentation site, `WS-H` fills its container, and only then can `Step L5` validate it. Running `WS-H` before `Step L3` leaves `Step H3` invalidating a host that does not exist; running `Step L5` before `Step H2` validates an empty container. This is the one place in the plan where two workstreams must be executed out of document order.

**`WS-E-scaffolding` is written after the code workstreams but executes in two parts.** `Step E1` and `Step E2` create the solution and establish the first successful build, and must run early: `Step A5` requires a runnable application, and without them it would restore implicitly and generate the `packages.lock.json` files that `Step E2` is written to create. If `Step A5` is nevertheless executed first, that is acceptable — `Step E2` then verifies the lock files rather than generating them, and its 0-error acceptance bar is unaffected.

## 📦 WS-A-relocation — repository preparation and projects into `src` (✅ done)

### Step A0 — Prepare the repository shell (✅ done)

Runs **before every other step in this plan**, because one of the files it removes fires on the first push to `main`.

Delete the three inherited workflows per `D20-inherited-workflows-removed`: `.github/workflows/20.DeployTools.yml`, `.github/workflows/21.DeployAppService.yml`, `.github/workflows/quarto-publish.yml`.

Then create the build scaffolding this repository does not have, porting from `tools.01/src/` and stripping what is specific to that repository:

| File | Content |
|---|---|
| `src/Directory.Build.props` | the conditional Diginsight import switches (`DiginsightCoreSolutionDirectory`, `DiginsightCoreDirectImport`), the three version properties, `RestorePackagesWithLockFile: true`, and the shared language settings |
| `src/Directory.Build.targets` | ported unchanged except for any reference to a project absent here |
| `nuget.config` | the feed list, ported unchanged |

Acceptance: the three files exist, and no file under `.github/workflows/` names `Diginsight.Tools.sln`.

### Step A1 — Copy the three projects (✅ done)

Copy — do not move, per `D11-cutover-then-retire` — from `Learn.01/src/` into `smartdocs.01/src/`, excluding `bin/`, `obj/` and `packages.lock.json`:

| Source | Destination |
|---|---|
| `Learn.Web/` | `Diginsight.SmartDocs.Web/` |
| `Learn.Web.Client/` | `Diginsight.SmartDocs.Web.Client/` |
| `Learn.Web.Shared/` | `Diginsight.SmartDocs.Web.Shared/` |

Use `Copy-Item`, not `git mv` — the destination files are untracked at this point.

### Step A2 — Rename the project files and their identity properties (✅ done)

Rename each `.csproj` to match its folder, and update `RootNamespace` and `AssemblyName` to the new names. Update `UserSecretsId` in the web project to `diginsight-smartdocs-web`. Update the three `ProjectReference` paths so the web project references the client and shared projects by their new file names, and the client references the shared project by its new file name.

Leave the `PackageReference` and conditional Diginsight `ProjectReference` blocks **untouched** — they resolve against the `src/Directory.Build.props` created in `Step A0`.

### Step A3 — Rewrite namespaces and using directives (✅ done)

Replace the namespace roots across all `.cs`, `.razor` and `_Imports.razor` files:

| Old | New |
|---|---|
| `Learn.Web.Shared` | `Diginsight.SmartDocs.Web.Shared` |
| `Learn.Web.Client` | `Diginsight.SmartDocs.Web.Client` |
| `Learn.Web` | `Diginsight.SmartDocs.Web` |

Apply longest-prefix-first so `Learn.Web.Shared` is not first mangled by the `Learn.Web` rule.

Four artifacts carry the client assembly's identity and must be updated by name rather than left to a bulk replace, per `D16-prerender-parity-by-construction` rule 4:

| Artifact | Location | Role |
|---|---|---|
| `Marker` class | client project root | the public type the host references to name the assembly |
| `AppAssembly="typeof(Marker).Assembly"` | `Routes.razor` (client) | how the **client** router discovers pages |
| `.AddAdditionalAssemblies(typeof(Learn.Web.Client.Marker).Assembly)` | host `Program.cs` | how the **server** discovers pages while prerendering |
| fingerprinted `_framework` payload | build output | keyed by assembly name; a stale `wwwroot` or publish folder serves the old boot manifest |

The third row is the one that fails silently. If it still names the old assembly the server routes nothing, prerenders *Not found*, and the client router repairs the page after load — so a browser shows a correct site with no server-side rendering at all.

### Step A4 — Rewrite non-code identifiers (✅ done)

Search for the remaining literal occurrences and update them: `launchSettings.json` profile names, the log4net configuration file, `appsettings*.json` logging category filters (`"Learn.Web": "Information"` → `"Diginsight.SmartDocs.Web": "Information"`), and the static-web-assets file names implied by the assembly rename.

### Step A5 — Prove the rename alone changed nothing (✅ done)

Run the renamed application **before any space work begins**, against the same filesystem content root the learning hub uses, and confirm with a plain HTTP client that an article URL returns rendered prose in the first response body.

This is a checkpoint, not a ceremony. At this point exactly one class of defect can exist — a broken identity from `Step A3` — so a failure here has a single cause and is cheap to find. The same failure discovered after `WS-B` and `WS-C` have landed is indistinguishable from a space-resolution bug and expensive to isolate. `D16` rule 4 is verified here and nowhere else.

## 🧱 WS-B-space-model — configuration and resolution (🟡 todo)

### Step B1 — Replace `ContentOptions` with `SiteOptions` (✅ done)

In `Diginsight.SmartDocs.Web`, replace `ContentOptions.cs` with `SiteOptions.cs` binding the `Site` section. Per `D17-two-deployments-one-codebase` there are **two** such files, one per deployment, and neither ever contains the other's spaces.

The learning-hub deployment — one space, root-mounted, unbranded:

```jsonc
{
  "Site": {
    "Title": "Learning Hub",
    "NotFoundPath": "404.html",
    "InvalidateApiKey": "",
    "Spaces": [
      {
        "Id": "learn",
        "RouteBase": "/",
        "Title": "Learning Hub",
        "Icon": "🎓",
        "Source": "Blob",
        "Blob": {
          "AccountUri": "https://<account>.blob.core.windows.net",
          "ContainerName": "learn"
        }
      }
    ]
  }
}
```

`RouteBase` is stated as `"/"` rather than omitted. Both forms mean the same thing under `D14-route-base-is-configured`, but writing it makes the hub's root mount a **declared intent** that `Step B2` validates, instead of a default that a later edit could silently change. The `Id` is still required — it keys the cache (`Step B4`) and names the metrics snapshot (`Step B6`).

The documentation deployment — many spaces, prefixed, branded:

```jsonc
{
  "Site": {
    "Title": "Diginsight Documentation",
    "NotFoundPath": "404.html",
    "InvalidateApiKey": "",
    "Branding": { "ProductName": "Diginsight", "LogoPath": "_brand/logo.svg" },
    "Spaces": [
      {
        "Id": "diginsight.smartdocs",
        "RouteBase": "/diginsight.smartdocs",
        "Title": "Diginsight SmartDocs",
        "Icon": "📘",
        "RepositoryUrl": "https://github.com/diginsight/smartdocs",
        "Source": "Blob",
        "Blob": {
          "AccountUri": "https://<account>.blob.core.windows.net",
          "ContainerName": "diginsight-smartdocs"
        }
      },
      {
        "Id": "diginsight.tools",
        "RouteBase": "/diginsight.tools",
        "Title": "Diginsight Tools",
        "Icon": "🔧",
        "RepositoryUrl": "https://github.com/diginsight/tools",
        "Source": "Blob",
        "Blob": {
          "AccountUri": "https://<account>.blob.core.windows.net",
          "ContainerName": "diginsight-tools"
        }
      }
    ]
  }
}
```

No space here claims the root, so the generated index is served at `/` (`D14-route-base-is-configured` rule 4).

**This block illustrates the shape, not the delivered configuration.** At the end of this plan the documentation deployment carries **exactly one** space, `diginsight.smartdocs`, because onboarding any other repository is an explicit non-goal (`PL-5-additional-spaces`). The `diginsight.tools` entry is shown to make the growth path concrete: the list grows by adding entries — `diginsight.components`, `diginsight.telemetry` and the rest — with no code change and no redeployment of the renderer. A space configured here whose container this plan does not fill would fail `Step L5`.

A space carries `Id`, `RouteBase`, `Title`, `Icon`, `RepositoryUrl`, `Source` (`Blob` or `FileSystem`), and exactly one of `Blob { AccountUri, ContainerName }` or `FileSystem { RootPath, WatchForChanges }`. Sources are **per space and may differ** — this is what allows one space to be served from the working tree during development while the others stream from storage.

### Step B2 — Add `SpaceRegistry` (✅ done)

Create `Spaces/SpaceRegistry.cs`. Responsibilities: validate the configured spaces at startup, expose lookup by id and by route base, and expose the ordered list for the index and switcher.

Validation is fail-fast at startup: ids unique and non-empty; ids restricted to characters valid in a URL path segment; route bases unique and rooted; exactly one source block populated per space. A space list that fails validation must stop the host, not degrade silently — a mis-typed id would otherwise surface as a 404 on a page that used to work.

Three further rules come from `D14-route-base-is-configured` and are validated here rather than assumed:

1. **At most one space has a `RouteBase` of `/` or absent.** Two root claims are an unresolvable ambiguity → fail to start.
2. **No non-root `RouteBase` is a prefix of another.** `/a` and `/a/b` would make longest-prefix matching order-dependent → fail to start.
3. **Lookup is longest-prefix-first**, and a non-root `RouteBase` reserves its first segment from a root-mounted space's catch-all — always, including when only one space is configured.

Expose `TryResolve(path) → (space, contentKey)` as the single entry point used by both containers. Per `Step C5` this must be the *same code* on the server and in WebAssembly, so the resolution function lives in the **shared** project and takes the ordered space list as an argument; only the list's construction differs per container.

Bind through `IOptionsMonitor<SiteOptions>` and rebuild the registry on change, so a configuration reload adds a space without a restart.

### Step B3 — Add `SpaceContentSourceRegistry` (✅ done)

Create `Spaces/SpaceContentSourceRegistry.cs`: a **singleton** holding one entry per configured space. `BlobContentSource`, `FileSystemContentSource` and `CachedContentSource` are constructed per space and **not otherwise modified**. Entries are built once and rebuilt when the registry rebuilds.

Three constraints, all of them consequences of `D16-prerender-parity-by-construction`:

**The lifetime does not change.** The registry is a singleton and each entry holds singletons. `FolderMetricsIndex`, `DynamicNavBuilder`, `CachedDynamicNavBuilder` and `INavBuilder` are singletons that capture the content source today; a scoped or request-derived content source is a captive dependency in all four. `ValidateScopes` fails the host at startup under Development, and Production binds the first scope's instance forever — every space silently serving the first space's content.

**Selection is by explicit argument, never by ambient request state.** Consumers receive the space id as a **method parameter** — `registry.For(spaceId)` — resolved from the route path by the caller. A DI factory that reads `IHttpContextAccessor` to pick the space works during prerender and returns nothing in WebAssembly, which removes the rendered first response while leaving the browser experience intact.

**Each entry exposes both contracts.** An entry carries `IContentSource` for reads and `IContentLister` for enumeration — a `SpaceContentAccess(IContentSource Source, IContentLister Lister)` record. The navigation builder needs the lister, and today obtains it by downcasting the singleton content source. An entry exposing only `IContentSource` compiles and then fails at the first navigation call.

Remove the `(IContentLister)` downcast registration from the host `Program.cs` in the same step — with per-space sources there is no singleton left to downcast, and leaving it in place resolves the wrong space's lister.

The client container gets the mirror of this: a registry whose entries wrap `HttpContentSource` against the space-addressed endpoint. Same type, same lifetime, same selection call — different implementation. Both `Program.cs` files change together.

### Step B4 — Add the space dimension to the cache key (🟡 todo)

`Caching/ContentPathCacheKey.cs` gains a space segment. Without it, two spaces holding a file at the same relative path collide in cache — a silent cross-space content leak, and the single most likely defect in this workstream.

### Step B5 — Make navigation space-aware (🟡 todo)

`Navigation/DynamicNavBuilder.cs` and `CachedDynamicNavBuilder.cs` take a space and build against that space's content source. `Navigation/FolderMetricsIndex.cs` is keyed per space. `Navigation/NavRules.cs` in the shared project is pure convention logic and stays unchanged — its numeric-prefix, date-prefix and title-casing rules already match this repository's folder naming.

### Step B6 — Make metrics opt-in and per-space (🟡 todo)

`NavStats`, `RepoStats` and `Coverage` are learning-hub-specific measures. Add a per-space boolean that defaults to off, and skip their computation when it is off. A repository documentation space should not be reporting article-coverage percentages.

`FolderMetricsIndex` is a singleton keyed by folder prefix only, and its snapshot is a single file at `Content:MetricsSnapshotPath`. Both must gain the space dimension: one index instance per space, and a snapshot path derived per space (`{configured-path}` for a single space, `{configured-stem}.{space-id}.json` for many). Without this, two spaces with metrics enabled overwrite each other's snapshot on every save, and the warm-up seeds each space from the other's counts.

The startup warm-up loop in `Program.cs` — seed, discover per root branch, drain, prune unreachable, save — runs **once per metrics-enabled space**, sequentially, so a cold multi-space start does not fan out into parallel whole-tree walks.

## 🔌 WS-C-endpoints — space-addressed surface (🟡 todo)

### Step C1 — Add `/_spaces` (🟡 todo)

New `Endpoints/SpaceEndpoints.cs` returning the registry projection consumed by the index page and the switcher: `id`, `routeBase`, `title`, `icon`, `repositoryUrl`, and the live values `articleCount`, `lastPublishedUtc` and `reachable`. The live values are what a statically generated landing page cannot carry, and are the reason this endpoint exists rather than a build-time file.

### Step C2 — Space-address the content and nav endpoints (🟡 todo)

`Endpoints/ContentEndpoints.cs` → `/_content-raw/{space}/{**key}`.

`Endpoints/NavEndpoints.cs` is a `MapGroup("/_nav")` carrying **five** routes. The space segment goes **between the group and each route**, never in place of them:

| Today | After |
|---|---|
| `GET /_nav/children?prefix=` | `GET /_nav/{space}/children?prefix=` |
| `GET /_nav/version` | `GET /_nav/{space}/version` |
| `GET /_nav/total` | `GET /_nav/{space}/total` |
| `GET /_nav/index` | `GET /_nav/{space}/index` |
| `POST /_nav/invalidate?path=` | unchanged — see `Step C3` |

A bare `/_nav/{space}` route MUST NOT be introduced: it is ambiguous with all five children above.

Per `D14-route-base-is-configured` the unprefixed form is mapped **when a space claims the root**, and resolves to that space — this is what keeps the learning hub's client and any external caller working unchanged. When no space claims the root the unprefixed form is not mapped. The condition is the root claim, never the space count: a deployment serving `learn` at `/` alongside a second space at `/x` still answers the unprefixed form, for `learn`.

Both endpoint families resolve the space through `SpaceRegistry` and return `404` for an unknown id — never fall back to a default space, because a silent fallback turns a typo into wrong content. The existing `/_nav` group endpoint filter that translates a client abort into `499` stays in place and applies to the space-addressed routes unchanged.

### Step C3 — Extend invalidation and enforce its key (🟡 todo)

`/_nav/invalidate` keeps its path, its `POST` method and its existing `?path=` parameter, and accepts an optional `?space={id}`. Present → invalidate that space only. Absent → invalidate all spaces. Per `D6-invalidate-backward-compatible`, this keeps the existing learning-hub content workflow working with no edit to it.

Implement the `X-Invalidate-Key` check, which does not exist today — see the correction under `D6`. Bind the key from `Site:InvalidateApiKey`. When the configured key is empty the endpoint stays open, preserving current behaviour for local runs; when it is non-empty, compare with `CryptographicOperations.FixedTimeEquals` and return `401` on mismatch or absence. The caller already sends the header, so enabling enforcement does not require a workflow edit.

### Step C4 — Group hub subscriptions by space (🟡 todo)

`Navigation/NavHub.cs` and `NavChangePublisher.cs` group SignalR subscriptions by space id, so publishing a change to one space notifies only the clients viewing it. Add a `spaces` group that broadcasts registry changes, so a space added at runtime appears in every connected client's index and switcher without a reload.

### Step C5 — Route client requests through the space (🟡 todo)

In the client project, `HttpContentSource.cs` and `HttpNavProvider.cs` take the current space id from the route and call the space-addressed endpoints. `NavHubClient.cs` subscribes to the current space's group plus the `spaces` group.

The path-to-space-and-key resolution written in `WS-B` must be the **same code** on both sides, living in the shared project. If the server resolves `/learn/guide` to space `learn` + key `guide` and the client resolves it any other way, the prerendered article is replaced at hydration by a different page or a 404 — the exact failure `D16` rule 2 exists to prevent. `HttpContentSource` today fetches the **relative** URL `_content-raw/{contentKey}`, which resolves against `<base href="/" />` from any path depth; keeping the base at `/` is what lets that continue to work under a space prefix.

`Routes.razor` and `ContentPage.razor` live in the client project and today declare `@page "/"` and `@page "/{*path}"`. Per `D14-route-base-is-configured` both forms must remain routable: keep the existing catch-all and resolve the leading segment through the shared `TryResolve` of `Step B2` at run time rather than declaring a second `@page "/{space}/{*path}"` template — two catch-all templates differing only by a leading segment do not disambiguate reliably. `<base href="/" />` in `App.razor` stays as it is; the WebAssembly `HttpClient` base address derives from it and all API calls are origin-absolute.

### Step C6 — Port the test content endpoints unchanged (🟡 todo)

`Endpoints/TestContentEndpoints.cs` maps `/_test/article` (POST, DELETE) and `/_nav/metrics` only when `Testing:ContentMutationEnabled` is true, which is never the case outside local runs, and it writes through the filesystem source.

Port it as-is with the names updated, bound to the **first `FileSystem` space** in the registry. Do not make it multi-space: it exists to exercise the metrics pipeline on a developer machine, and a configuration-gated dev-only write path does not justify a space dimension. If no `FileSystem` space is configured, do not map the endpoints.

## 🖼️ WS-D-space-index — generated index and switcher (🟡 todo)

Both surfaces render from `/_spaces`. Neither enumerates spaces in markup, so adding a space is a configuration change only. Per `D14-route-base-is-configured` the two are suppressed under **different** conditions: the index exists only when no space claims the root, and the switcher renders only when more than one space is configured. In the learning-hub deployment both are absent, which is what `D7-learn-space-compatibility` requires.

### Step D1 — Add the `SpaceIndex` page (🟡 todo)

A page in the shared project, served at `/` **when no space claims the root**, rendering one card per space: icon, title, a documentation link to the space's route base, a repository link when `RepositoryUrl` is set, and the live counts from `/_spaces`. It iterates the registry — it must not contain a literal space name. Handle the loading, unreachable-space and empty-registry states explicitly.

### Step D2 — Make the index copy editable without a rebuild (🟡 todo)

Heading, introduction and footer come from an optional Markdown fragment resolved through the ordinary content pipeline; the card grid is generated. Absent fragment → fall back to `Site:Title` and no introduction. This keeps wording a content edit while keeping the space list generated.

### Step D3 — Add the switcher to `TopMenu` (🟡 todo)

`Layout/TopMenu.razor` gains a space switcher bound to the same `/_spaces` projection, marking the current space. `Layout/MainLayout.razor` reads the current space's title and icon for branding. Both update live from the `spaces` hub group. The switcher renders nothing when the registry holds one space — a control offering a single destination is noise, and `D7-learn-space-compatibility` requires the learning hub's top bar to look exactly as it does today. This is the **only** surface keyed on the space count; routing never is.

### Step D4 — Handle the root route (🟡 todo)

Root behaviour follows the **root claim**, per `D14-route-base-is-configured`:

| Root claimed by a space? | `/` serves | `/{first-segment}/…` |
|---|---|---|
| yes | that space's root content — for the learning hub, exactly as today | matched against every non-root `RouteBase` first; no match → a content path in the root-mounted space |
| no | the generated index | resolved against `SpaceRegistry`; unknown → the configured not-found page, never a default space |

First-segment reservation applies **whenever a non-root `RouteBase` is configured**, regardless of how many spaces there are. A root-mounted space's top-level content folder is therefore shadowed if its name equals another space's route base. The learning hub is unaffected because its deployment configures no other space (`D17-two-deployments-one-codebase`), and a documentation deployment has no root-mounted space to shadow — but the rule is unconditional, so neither fact may be relied on by the implementation.

## 🎨 WS-K-branding — configurable app-level look and feel (🟡 todo)

Per `D15-branding-is-per-deployment`, one deployment has one brand. Every step here is configuration- or content-driven: onboarding a publisher's identity MUST NOT require a code change or a rebuild.

### Step K1 — Add the `Site:Branding` section (✅ done)

Extend `SiteOptions` from `Step B1` with a `Branding` block:

```jsonc
{
  "Site": {
    "Title": "Diginsight Documentation",
    "Branding": {
      "ProductName": "Diginsight Documentation",
      "LogoPath": "_branding/logo.svg",
      "FaviconPath": "_branding/favicon.ico",
      "StylesheetPath": "_branding/theme.css",
      "Palette": {
        "Primary": "#0d6efd",
        "OnPrimary": "#ffffff",
        "Accent": "#0a58ca"
      }
    }
  }
}
```

Every field is optional. All absent → the application renders exactly as the learning hub does today, which is what `D7-learn-space-compatibility` requires.

`LogoPath`, `FaviconPath` and `StylesheetPath` resolve through the **ordinary content pipeline** against the first configured space, so a publisher supplies its own assets by committing them alongside its documentation. No asset is compiled in and no deployment carries another publisher's marks.

### Step K2 — Emit the palette as CSS custom properties (🟡 todo)

`Palette` entries are written into the rendered `<head>` as CSS custom properties on `:root` (`--sd-primary`, `--sd-on-primary`, `--sd-accent`). Rewrite the fixed colours in `wwwroot/app.css` to reference those properties with the current learning-hub values as fallbacks, so an unconfigured palette is a visual no-op.

Emit them during **prerender**, not from client-side JavaScript — a palette applied after hydration produces a visible flash of the default theme on every cold navigation.

### Step K3 — Bind the shell to the branding (🟡 todo)

`Layout/MainLayout.razor` and `Layout/TopMenu.razor` render `ProductName` and the logo in the top bar in place of the hardcoded learning-hub mark. `App.razor` links `FaviconPath` and, when set, `StylesheetPath` after `app.css` so a publisher stylesheet overrides rather than replaces the base sheet.

In the multi-space shape the top bar shows the publisher brand plus the current space's `Title` and `Icon`; in the learning-hub shape it shows the brand alone, matching today's layout.

### Step K4 — Confirm the unbranded default is unchanged (🟡 todo)

Run the application with no `Branding` block and compare the top bar, palette and favicon against the current learning-hub site. Any visible difference means a hardcoded value was replaced with a non-equivalent default → fix the fallback rather than the configuration.

## 🔨 WS-E-scaffolding — solution, build files and local run (🟡 todo)

### Step E1 — Create the solution and add the projects (✅ done)

Create `src/Diginsight.SmartDocs.sln` and add the three projects from `Step A1`. No solution folders: three projects in one repository do not need a category level (`D2-projects-at-src-root`).

### Step E2 — Restore, generate lock files, build (✅ done)

```powershell
cd "c:\dev\darioa\Diginsight\smartdocs.01\src"
dotnet restore "Diginsight.SmartDocs.sln" --force-evaluate --nologo
dotnet build   "Diginsight.SmartDocs.sln" --no-restore -v m --nologo
```

`--force-evaluate` is restore-only; passing it to `build` fails with `MSB1001`. Acceptance: **0 errors**. Record the warning count as this repository's baseline — there is no inherited one. Commit the three generated `packages.lock.json` files.

### Step E3 — Run locally against the filesystem source (🟡 todo)

Add `appsettings.Development.json` with a single space `diginsight.smartdocs` on `Source: FileSystem`, `RootPath` pointing at `src/docs`, `WatchForChanges: true` and `RouteBase: "/"`. Run the web project in a **visible foreground console** — not a hidden or background process — rebuilding rather than using `--no-build`, so client WebAssembly changes are served.

This renders this repository's own documentation with no storage account and no deployment, and is the cheapest possible proof that the port is sound. It commits nothing environment-specific: a filesystem path into the working tree is not a secret, so this file stays here rather than in `smartdocs.internal` (`D19-public-repository-internal-configuration`).

Then change `RouteBase` to `/diginsight.smartdocs`, restart, and confirm the same content answers under the prefix **with that space still the only one configured**. This is the cheapest possible proof of `D14-route-base-is-configured`, and it is precisely the behaviour the superseded `D14-root-mount-when-single-space` made impossible.

The root-mounted half is done: `appsettings.Development.json` exists, the host ran in a visible foreground console, and nine scenarios passed in a visible browser — recorded in [20260817.01-validation-sequence.md](_validation/20260817.01-validation-sequence.md). **To do:** the prefixed half, which is blocked behind `G1-renderer-root-absolute-urls`.

## 🔐 WS-F-internal-config — move Testmc configuration to `smartdocs.internal` (🟡 todo)

Operates in `c:\dev\darioa\Diginsight\smartdocs.internal`, which today holds `README.md` and `LICENSE` and nothing else. This workstream establishes its structure, per `D19-public-repository-internal-configuration`.

### Step F1 — Create the learning-hub configuration file (✅ done)

Create `src/Diginsight.SmartDocs.Web/appsettings.Testmc.json`, mirroring the public repository's own layout so the two are trivially comparable.

Port the existing Testmc content from `Learn.internal` — logging levels, `Observability`, `OpenTelemetry`, `AzureKeyVault` — updating the logging category to the new assembly name, and replace the flat `Content` block with the **learning-hub** `Site` block from `Step B1`: one space, `learn`, on the existing `learn` container, `RouteBase: "/"`, no `Branding`.

Per `D17-two-deployments-one-codebase` this file must **never** gain a second space. That is now an editorial rule rather than a mechanical one — under `D14-route-base-is-configured` a second space would no longer move the hub's URLs — so it has to be stated and checked rather than relied upon. The documentation deployment gets its own configuration file (`Step L2`) alongside its own host (`D18-docs-instance-host`).

Keep the informational `Deployment` block, naming the `learn` container.

### Step F2 — Create the dispatch workflow (🟡 todo)

Create `.github/workflows/` in `smartdocs.internal` — it does not exist yet — and add `deploy-testmc-config.yml`. It triggers on push to `main` touching either configuration file or itself, plus `workflow_dispatch`, and dispatches the **matching** deployment workflow in `diginsight/smartdocs`: the learning-hub file dispatches `01.DeployLearnHub.yml`, the documentation file dispatches `02.DeployDocsSite.yml` (`D13-one-workflow-per-target`).

This mirrors the dispatch that exists in `Learn.internal`; use this repository's cross-repository token variable rather than carrying over the source repository's token name.

### Step F3 — Confirm the read path (🟡 todo)

The deployment workflows in `WS-G-deployment` check out `diginsight/smartdocs.internal` using `INTERNAL_REPOSITORY_TOKEN`. Unlike earlier revisions of this plan, that secret does **not** already exist — nothing in this repository has ever read an internal repository. It is created in `Step G1` with read access scoped to `smartdocs.internal` alone.

Confirm the composed result, not the checkout: start the published host with `AppsettingsEnvironmentName=Testmc` and assert that `SiteOptions` binds the space list from the internal file. A checkout that succeeds but lands the file where `ConfigureAppConfiguration2` does not look produces an empty space list, and `Step G2`'s fail-closed parse catches that only if it parses the same path the host reads.

If the space list binds empty → correct the destination path in `Step G2` step 8 to the one the host actually reads, and make the fail-closed parse read that same path. Do not work around it by adding the space list to the public `appsettings.json`, which would breach `D19-public-repository-internal-configuration`.

## 🚀 WS-G-deployment — build and deploy the learning hub (🟡 todo)

Per `D13-one-workflow-per-target` this workstream adds the reusable build workflow and the **first** deployment workflow. The second, `02.DeployDocsSite.yml`, is `WS-L-docs-instance`.

### Step G0 — Add the federated credential for this repository (✅ done)

The OIDC federated credential is scoped **per repository**. A credential whose subject names another repository will not authenticate a workflow running in `diginsight/smartdocs`, and `azure/login@v2` fails with no useful message.

Add a federated credential to the **existing** Entra application — subject `repo:diginsight/smartdocs:ref:refs/heads/main`, issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`. Reuse rather than create: the role assignments on the App Services and the storage account are already held by that principal, so a new application would need every one of them re-granted.

If the application cannot be modified with the available permissions → stop and record it as an open decision. Do not fall back to a client secret; that would introduce a stored credential where federated identity is already the pattern.

Done 2026-08-17. The credential named `smartdocs-main` was added alongside the pre-existing one, which was **not** removed — the predecessor repository still deploys from it. The audit run at the same time showed the principal already holds the control-plane and blob-data-plane grants of `Step G1`'s prerequisites, so those need no action; `Step L4` is genuinely outstanding.

### Step G1 — Provision the repository secrets (🟡 todo)

Create everything the new workflows read, before writing them.

**None of these already exists.** Earlier revisions marked the OIDC identifiers and `INTERNAL_REPOSITORY_TOKEN` as inherited; that was true in `diginsight/tools` and is false here — this repository has never run a deployment.

**`D10-vars-for-configuration-secrets-for-secrets` holds unmodified.** An earlier revision of this step overrode it — this repository is public, GitHub masks secrets and does not mask variables, so every value was stored as a secret regardless of sensitivity. That override is gone, because the values that forced it are gone: under `D21-deployment-target-travels-with-configuration` the storage account, container and host names are read from the `smartdocs.internal` overlay and masked at run time, not stored here at all. What remains is four credentials and one shared key, every one of which is a secret on its own merits.

The names below are the ones `00`–`03` actually read. Earlier revisions of this table named `INTERNAL_REPOSITORY_TOKEN`, `SMARTDOCS_WEBAPP_NAME`, `SMARTDOCS_RESOURCE_GROUP`, `SMARTDOCS_LEARNHUB_WEBAPP_NAME` and `SMARTDOCS_STORAGE_ACCOUNT`; none of those exists in the committed workflows.

| Name | Kind | Value | Read by |
|---|---|---|---|
| `AZURE_CLIENT_ID` | secret | OIDC application identifier — the application extended in `Step G0` | 01, 02, 03 |
| `AZURE_TENANT_ID` | secret | tenant identifier | 01, 02, 03 |
| `AZURE_SUBSCRIPTION_ID` | secret | subscription identifier | 01, 02, 03 |
| `SMARTDOCS_INTERNAL_READ_TOKEN` | secret | read access to `diginsight/smartdocs.internal` | 01, 02, 03 |
| `SMARTDOCS_INVALIDATE_KEY` | secret | the cache-invalidation key | 01, 02, 03 |

Five values, not nine. No App Service name, storage account, container or resource group appears. The first three come from the overlay per `D21`; the resource group is resolved at runtime from the App Service name via `az webapp list`, which means the workflow identity's role assignment must be broad enough for that lookup to return the site — resource-group scope, not resource scope.

None of these values is written into this plan or any file here, per `D19-public-repository-internal-configuration`.

Add a first job step that asserts every value above is non-empty and fails the run listing the missing names. Without it an omission surfaces much later as an unauthenticated Azure call.

**📖 Operational procedure**: [20260817.02-STEPS-configure-CICD-deploy-grants.steps.md](_validation/20260817.02-STEPS-configure-CICD-deploy-grants.steps.md) — the federated credential, the Azure role assignments, the internal read token and the secrets, with the verification order.

### Step G2 — Add the reusable build workflow `00.BuildSmartDocsWeb.yml` (✅ done)

`workflow_call` only — no triggers of its own, and it deploys nothing. One input: the internal configuration file to carry. `permissions: contents: read`.

Build job on `self-hosted`:

1. Run the `Step G1` value assertion.
2. Check out this repository.
3. Check out `diginsight/smartdocs.internal` with `secrets.INTERNAL_REPOSITORY_TOKEN`, sparse to the requested configuration file.
4. Set up the .NET 10 SDK — see `DSC1-runner-sdk`.
5. Restore `src/Diginsight.SmartDocs.sln`, cached on `hashFiles('**/packages.lock.json')`.
6. Publish `-c Release -r win-x64 --self-contained true` per `D8-publish-profile-x64`, into `./publish/Diginsight.SmartDocs.Web`. Fail if the produced executable is missing.
7. Apply the zero-byte Brotli scrub: remove empty `.br` files under `wwwroot/_framework` **and** their entries in the static-web-assets endpoint manifest, then fail the build if any zero-byte framework asset remains.
8. Copy the requested configuration from the internal checkout into the publish root, at the path `ConfigureAppConfiguration2` reads. Parse it first and fail closed if it is missing or unparseable — a deployment carrying no space list would serve an empty site.
9. Upload the artifact as `smartdocs-web`.

### Step G3 — Add `01.DeployLearnHub.yml` and switch the worker to 64-bit (🟡 todo)

Triggers: push to `main` touching `src/**` or itself, plus `workflow_dispatch` and the `repository_dispatch` from `Step F2`. `permissions: id-token: write, contents: read`. Concurrency group `deploy-learn-hub`, no cancel-in-progress.

It calls `00.BuildSmartDocsWeb.yml` with the learning-hub configuration path, then runs a deploy job that logs in with `azure/login@v2` using the OIDC variables and, **before** the deploy action, sets the worker process to 64-bit on `SMARTDOCS_WEBAPP_NAME` in `SMARTDOCS_RESOURCE_GROUP`.

Run the platform switch **on every deployment**, not once by hand: the setting is idempotent, and running it unconditionally means a manually reverted portal value cannot silently break a later run. This is the prerequisite established by `D8-publish-profile-x64`.

### Step G4 — Apply and prune the application settings (🟡 todo)

Still in the deploy job, before the deploy action. Set:

```text
AppsettingsEnvironmentName=Testmc
Site__InvalidateApiKey=<secrets.SMARTDOCS_INVALIDATE_KEY>
Site__MetricsSnapshotPath=D:\home\data\nav-metrics-snapshot.json
```

Then delete the settings left behind by the previous flat configuration: `Content__Source`, `Content__Blob__AccountUri`, `Content__Blob__ContainerName`, `Content__InvalidateApiKey` and `Content__MetricsSnapshotPath`. Per `D3-site-section` they no longer bind, and leaving them in place would make a future reader believe the site still has a single fixed content root. The snapshot path moves to `Site__MetricsSnapshotPath` because `Step B6` derives the per-space file names from it.

Applying settings from the workflow rather than the portal is what keeps environment selection reproducible — the current action does the same, and this is the parity requirement.

### Step G5 — Deploy and confirm the target (🟡 todo)

Download the `smartdocs-web` artifact and deploy it with `azure/webapps-deploy@v3` to the Production slot.

Confirm from the run log that the deployment targeted `learn-testmc-app-itn-01` in resource group `learn-testmc-rg-itn-01`, that the worker platform reads 64-bit, and that the site answers on its hostname.

If the log names a different host → the `SMARTDOCS_*` variables of `Step G1` are wrong; correct them and redeploy before proceeding, because a deployment that landed on the wrong App Service has already overwritten something else. If the site returns `HTTP 500.32` → the platform switch of `Step G3` did not take effect; re-run it and restart the site.

## 🏢 WS-L-docs-instance — deploy the documentation site (🟡 todo)

This workstream deploys the **second** instance of the same artifact. It adds no application code — everything it needs exists after `WS-B` through `WS-K`. What it adds is a second configuration, a second set of deployment variables, and a data-plane grant.

**This workstream is split by the execution order.** Steps L1–L4 run before `WS-H-content-publishing`; `Step L5` runs after it. `Step H3` invalidates the host that `Step L3` creates, and `Step L5` validates the content that `Step H2` publishes.

### Step L1 — Declare the documentation deployment target in its overlay (✅ done)

No second set of secrets. Per `D21-deployment-target-travels-with-configuration`, the documentation host name lives in a `Deployment` block in `appsettings.TestmcDocs.json`, beside the `Site:Spaces[]` entry whose `Blob.AccountUri` and `Blob.ContainerName` already name the storage it reads.

That is the whole registration. `02` deploys wherever that file says, and `03` publishes into whatever container the space it is given declares — so a documentation deployment cannot be pointed at the learning hub's host, and content cannot be published to a container the site does not read.

Earlier revisions named `DOCS_WEBAPP_NAME`, `DOCS_RESOURCE_GROUP`, `SMARTDOCS_DOCS_WEBAPP_NAME`, `SMARTDOCS_DOCS_CONTAINER` and a separate `DOCS_INVALIDATE_KEY`. The first four are gone — configuration, or resolved at runtime. The fifth **has not been implemented**: `01` and `02` both pass the single `SMARTDOCS_INVALIDATE_KEY`.

**That collapse is a deliberate simplification with a cost worth stating.** The keys were specified separately because the deployments have separate publishers, and a workflow that can flush the documentation cache has no business flushing the learning hub's. With one shared key that separation is gone. Splitting them later is a two-secret change in `01` and `02` plus a redeploy of each site; carrying it as a known reduction is acceptable while both publishers are this repository's own workflows, and stops being acceptable the moment a third party publishes to either.

Done 2026-08-17, in the same change that removed the four configuration secrets.

### Step L2 — Add the documentation configuration to `smartdocs.internal` (✅ done)

A second file, `src/Diginsight.SmartDocs.Web/appsettings.Testmc.Docs.json`, carrying the multi-space `Site` block from `Step B1` — `Title: "Diginsight Documentation"`, the `Branding` block from `WS-K-branding`, and one entry per repository space, **each with an explicit non-root `RouteBase`**. No space claims the root, so the generated index is served at `/` per `D14-route-base-is-configured`. It must never contain the `learn` space, per `D17-two-deployments-one-codebase`.

**Configure exactly the spaces this plan populates — at completion, that is `diginsight.smartdocs` alone.** A space entry whose container is never filled renders as an empty tree behind a working link, and `Step L5` asserts that every configured space's articles resolve. Adding the others is `PL-5-additional-spaces`, and each arrives with its own container and publishing workflow.

The publish-time fail-closed parse in `Step G2` applies to this file too: a documentation deployment carrying no space list would serve an empty index rather than an error.

### Step L3 — Add `02.DeployDocsSite.yml` (✅ done)

The same shape as `01.DeployLearnHub.yml`, differing only in the target variables (`DOCS_*`), the configuration path passed to the build, and the concurrency group (`deploy-docs-site`). It calls the **same** `00.BuildSmartDocsWeb.yml`.

That shared call is what makes the two sites provably the same build, per `D13-one-workflow-per-target`. Two independent build definitions would let them drift, and the drift would surface as a behaviour difference nobody could attribute. The 64-bit switch of `Step G3` is a no-op on this host — it was created 64-bit (`D18-docs-instance-host`) — but it runs anyway, for the same idempotence reason.

### Step L4 — Grant the documentation identity read access to the space containers (✅ done)

The instance carries the system-assigned managed identity created with it (`D18-docs-instance-host`). Assign **Storage Blob Data Reader**, scoped **per container** rather than to the account, so onboarding a space is an explicit grant rather than a blanket one already in force.

Reader, not Contributor: the renderer only ever reads. Content is written by `WS-H-content-publishing` under the workflow's own federated identity.

Done 2026-08-17. The identity held **no** role assignments at all beforehand, so the site would have started and rendered an empty tree rather than failing. The container did not exist either — a container-scoped assignment cannot name a container that is absent — so it was created empty ahead of `Step H2` rather than waiting for the publishing workflow to create it, which decouples this grant from `Step G1`'s outstanding secrets. The learning-hub identity still holds **account-scoped** Reader inherited from the predecessor deployment; narrowing it to container scope is deliberately left as a separate change, and must add the narrow assignment before removing the broad one or the live site loses access in between.

### Step L5 — Validate the documentation instance (🟡 todo)

**Runs after `WS-H-content-publishing`**, per the execution order — it asserts rendered content, which `Step H2` publishes.

Against the running site: the generated index at `/` lists every configured space; each space's articles resolve under its `RouteBase`; the branding from `WS-K-branding` is present on every space; and `Step I2`'s three prerender checks pass in this prefixed shape as well as in the root-mounted one.

The last item is not redundant with `Step I2`. `D16` rule 2 — the space rides in the path, never in DI — can only fail where there is more than one space to get wrong.

If a space's articles do not resolve under its `RouteBase` → fix `TryResolve` in `Step B2`, not the configuration. If the prerender checks pass at the root-mounted shape but fail here → a space is being resolved from ambient request state; that is a `D16` rule 2 breach and blocks `WS-J-retirement`.

## 📤 WS-H-content-publishing — the `diginsight.smartdocs` space (🟡 todo)

**Runs between `Step L4` and `Step L5`**, per the execution order — `Step H3` invalidates the host `Step L3` deploys, and `Step H2` publishes the content `Step L5` validates.

### Step H1 — Add `03.PublishDocsContent.yml` (✅ done)

New workflow publishing **this** repository's documentation into the `diginsight-smartdocs` container. Triggers: push to `main` touching `src/docs/**`, plus `workflow_dispatch`. Concurrency group `publish-docs-content`.

This is the first instance of the generalisation obligation, not a special case: every other Diginsight repository publishes its own `src/docs` to its own container from a copy of this workflow, and the documentation deployment renders them all. Onboarding one is `PL-5-additional-spaces` — a container, a copy of this file, and an entry in `Step L2`.

Filling a container is independent of which deployment renders it. This workstream produces the content; the **documentation** deployment registers `diginsight.smartdocs` as a space, and the learning-hub deployment never does — `D17-two-deployments-one-codebase`.

Stage `src/docs/**` — Markdown plus images — preserving repository-relative paths, excluding `bin`, `obj` and `node_modules`. Fail if nothing was staged, so an empty stage can never reach the container-reset path.

### Step H2 — Create the container and mirror the content (🟡 todo)

Log in with OIDC using `secrets.AZURE_*`, then resolve the destination from the `diginsight.smartdocs` space in the overlay per `D21-deployment-target-travels-with-configuration` — never from a secret. Create the container if absent, upload the staged content, and only **after a successful upload** prune blobs no longer present in the stage. Upload-then-prune makes a cleanup failure non-destructive: new content is live and stale blobs are removed on the next run.

### Step H3 — Invalidate the space (🟡 todo)

`POST /_nav/invalidate?space=diginsight.smartdocs` with the `X-Invalidate-Key` header. Best-effort with `continue-on-error: true` — a cache that refreshes on its own schedule is not a deployment failure.

The target is the **documentation** deployment — `docs-testmc-app-itn-01` per `D18-docs-instance-host`, never `learn-testmc-app-itn-01`. It is resolved from the `Deployment` block of the same overlay the space was read from (`Step L1`), so the workflow cannot invalidate a host it did not publish for. The key is the shared `SMARTDOCS_INVALIDATE_KEY`, per the reduction recorded in `Step L1`.

### Step H4 — Confirm the learning-hub workflow still works (🟡 todo)

The learning-hub content workflow calls `/_nav/invalidate` with no space parameter. Confirm from the running site that this still invalidates successfully after `WS-C-endpoints`. This is the direct test of `D6-invalidate-backward-compatible`.

If it returns `400` or `404` → `Step C3` made the space parameter mandatory; restore the omitted-parameter branch. If it returns `401` → the key enforcement added in `Step C3` is live but the calling workflow sends no header; add the header there before this plan's key becomes mandatory anywhere else.

## 🧪 WS-I-validation — visible browser evidence (🟡 todo)

Mandatory for this change. Record the run as a validation-sequence Markdown with screenshots under this work item's `_validation/` folder, images in `_validation/images/`, front matter `publish: false`, following `testing-validation.instructions.md`.

### Step I0 — Capture the learning-hub baseline before anything changes (🟡 todo)

Against the **currently deployed** site, record the reference every later step is compared against: the top bar, the sidebar tree, a rendered article with its table of contents, the footer counts, and the exact URL of at least three articles at different depths.

Without this, "behaves exactly as today" is an opinion. This step is the reason it can be a measurement.

### Step I1 — Prove learn-space compatibility (🟡 todo)

Run `Diginsight.SmartDocs.Web` configured exactly as `Step F1` specifies — the `learn` space, `RouteBase: "/"`, no branding — and confirm, against the `Step I0` baseline:

- the three recorded article URLs resolve at the **same paths**, with no space prefix and no redirect (`D14-route-base-is-configured`)
- `/` serves the learning-hub root content, **not** a space index
- no space switcher is present in the top bar
- `GET /_nav/children?prefix=`, `/_nav/version`, `/_nav/total`, `/_nav/index` answer at their **unprefixed** paths
- the sidebar tree, table of contents, breadcrumb, prev/next, search overlay and footer counts match the baseline

Then re-run with a second space added at `/probe`, and confirm **every assertion above still holds unchanged**. This is the direct test of `D14-route-base-is-configured`: under the superseded count-based rule this second run would have moved every hub URL. Remove the probe space afterwards.

Any difference in either run is a `D7-learn-space-compatibility` violation and blocks `WS-J-retirement`.

### Step I2 — Prove server-side rendering survived (🟡 todo)

This step **proves** `D16-prerender-parity-by-construction` held. It does not create the property — `WS-A` and `WS-B` do. A failure here means one of the five rules was violated upstream, and the table below identifies which.

Run all three checks in **both** deployment shapes — the root-mounted learning hub and the prefixed documentation site:

| Check | How | A failure points at |
|---|---|---|
| the first response is already rendered | plain HTTP client, no browser; body must contain the article heading and prose | rule 4 (assembly identity) or rule 1 (a service missing server-side) |
| the page is complete without JavaScript | visible browser with JavaScript disabled | same as above |
| hydration does not change what is shown | visible browser, JavaScript on, watch the article through load | rule 2 (space derived from ambient request state) or rule 3 (wrong space's content) |

The third check is the one that catches an asymmetric space resolution: the server renders the right article, the client then resolves the space differently and replaces it. Capture the no-JavaScript view, the raw response excerpt, and the hydration transition.

An accidental loss of prerendering is invisible in a normal browser, because the WebAssembly client fills the page in afterwards. Nothing else in `WS-I` detects it.

### Step I3 — Capture the space index (🟡 todo)

On the **documentation** deployment, where no space claims the root, navigate to `/`. Capture: every space card, its icon and title, the documentation and repository links, and the live counts.

### Step I4 — Capture each space (🟡 todo)

Navigate to each configured space's `RouteBase` in turn — `/diginsight.smartdocs` and any other space registered in `Step L2`. Capture for each: the navigation tree, a rendered article with a Mermaid diagram and a table of contents, and the branding showing the correct space title.

### Step I5 — Capture the switcher (🟡 todo)

Switch from one space to the other using the top-bar switcher. Capture before, during and after, confirming both the route base and the branding changed.

### Step I6 — Capture cross-space isolation (🟡 todo)

Request a path that exists in one space and not the other. Capture the not-found page. This is the direct test of the cache-key change in Step B4 — a leak here means Step B4 is incomplete.

### Step I7 — Capture branding applied and absent (🟡 todo)

With a `Site:Branding` block configured — logo, favicon, palette and product name — capture the top bar and an article page. Then remove the block, restart, and capture the same two views. The second pair MUST match the `Step I0` baseline (`Step K4`).

Confirm from the prerendered response that the palette custom properties are present in the first response, per `Step K2` — a palette that only appears after hydration flashes the default theme.

### Step I8 — Capture live invalidation (🟡 todo)

With a browser open on a space, publish a content change and confirm the navigation updates without a reload. Capture before and after.

Note: the automated browser can render in responsive rail mode at roughly 592 pixels and throttles painting when occluded. Bring the window to the front, wait for counts to settle, and read the live DOM value for exact assertions.

## 🧹 WS-J-retirement — remove from the source repositories (🟡 todo)

**Gated.** Do not begin until the `WS-I-validation` exit criterion is met. Every step here is destructive and outside this repository.

### Step J1 — Retire the source deployment workflow (🟡 todo)

In `Learn.01`, delete `.github/workflows/deploy-learnweb.yml`. The application is no longer built there. Leave `deploy-learninghub.yml` in place — it still publishes learning-hub content to the `learn` container and is unaffected.

### Step J2 — Remove the source projects (🟡 todo)

In `Learn.01`, remove `src/Learn.Web/`, `src/Learn.Web.Client/`, `src/Learn.Web.Shared/` and their entries from the solution files. Verify the remaining solution still restores and builds before committing.

### Step J3 — Remove the source internal configuration (🟡 todo)

In `Learn.internal`, remove `src/Learn.Web/appsettings.Testmc.json` and `.github/workflows/deploy-testmc-config.yml`. Both now live in `smartdocs.internal`. Confirm the file exists at the destination before removing the source — the move is complete only when the destination is committed.

### Step J4 — Redeploy and re-verify (🟡 todo)

Trigger both deployment workflows once more after retirement and confirm the learning hub still serves its space at the root and the documentation site still serves every configured space. This proves no removed file was still participating in the build.

If either fails → restore the removed file from history, identify what still referenced it, and remove the reference before retrying the removal. Retirement is reversible by construction: `D11-cutover-then-retire` copied rather than moved, so the source repositories are still intact at this point.

## 🕳️ Gaps found during execution

Defects this plan did not anticipate, found while executing it. Each is in scope — they block stated exit criteria — but none had a step until now.

**`G1-renderer-root-absolute-urls`** — `MarkdigMarkdownRenderer.Render(string markdown, string contentDir)` takes no space, and all three of its link-rewriting branches emit **root-absolute** URLs with no space segment: images become `/_content-raw/{resolved}`, `.md` and `.qmd` links become `/{stripped}{fragment}`, and other assets become `/_content-raw/{resolved}{fragment}`. Under any non-root `RouteBase` every image 404s and every cross-article link lands in the wrong space. This is invisible today only because the delivered configuration mounts one space at the root, which is exactly the condition `Step E3` is meant to leave behind. **To do:** thread the owning space into `Render` and prefix each emitted URL with its `NormalizedRouteBase`. Fix before `Step E3`'s prefixed half and before `Step L5`. (🟡 todo)

**`G2-about-menu-hardcoded-hrefs`** — the same class, one layer up: `AboutMenu.razor` hardcodes `href="readme"` and `href="_content-raw/LICENSE"`. Both are space-relative in intent and root-absolute in effect. **To do:** resolve both through `SpaceRegistry.ToRoute` against the space currently in scope. (🟡 todo)

## 🔎 Discovery

Items undecidable until execution. Each carries a defined negative branch.

**`DSC1-runner-sdk`** — `Learn.01`'s workflow documents that `actions/setup-dotnet` fails on its self-hosted runner because it cannot write to the system SDK location, and verifies the pre-installed SDK instead; `diginsight/tools` uses `actions/setup-dotnet@v5` successfully. Both observations come from **other** repositories, and this one has never run a build — whether they share a runner pool is unknown until the first run. Start with `actions/setup-dotnet@v5` at `Step G2`. If it fails on the runner → replace it with the pre-installed-SDK verification block, asserting a .NET 10 SDK is present and failing the job otherwise.

**`DSC2-oidc-storage-role`** — whether the principal extended in `Step G0` holds `Storage Blob Data Contributor` on the storage account. It holds whatever `diginsight/tools` needed; this plan asks it to write a container it has never written. Attempt container creation at `Step H2`. If it fails with an authorisation error → stop and record the required role assignment as an open decision; do not fall back to an account key, which would introduce a secret where managed identity is already the pattern.

**`DSC3-app-service-runtime-stack`** — whether the App Service is configured for a runtime stack that conflicts with a self-contained deployment. Inspect the configuration at `Step G3`, alongside the worker-platform switch. If a framework-dependent stack is pinned → set the stack to the one matching a self-contained deployment in the same step, and record it in the deployment block of the internal configuration file.

## 🗳️ Open decisions

None. `OD1-docs-instance-host` closed on 2026-08-16 when `docs-testmc-app-itn-01` was provisioned; it is recorded as `D18-docs-instance-host`, and the workstream it was gating is now written as `WS-L-docs-instance`.

## 🅿️ Park lot

Out of scope for this plan. Not to be executed here.

- **`PL-1-quarto-retirement`** — → closed: `quarto-publish.yml` is deleted in `Step A0`. It was inherited into this repository, triggers on every push to `main`, and has nothing to publish here. No other Quarto artefact exists in this repository.
- **`PL-2-ai-content-services`** — semantic search, summarisation and question answering over the spaces, as a separate service rather than inside the renderer. It belongs in `diginsight/tools`, alongside the existing API scaffolds, not here. → defer
- **`PL-3-scaffold-cleanup`** — the git-tracked, solution-absent `dotnet new webapi` scaffolds under `diginsight/tools`. Not this repository's concern, per `D12-tools-scaffolds-out-of-scope`. → defer
- **`PL-4-per-space-theme-override`** — letting an individual **space** override the deployment's logo or palette. App-level branding is now in scope as `WS-K-branding` per `D15-branding-is-per-deployment`; a per-space override is a separate feature needing its own justification. → defer
- **`PL-5-additional-spaces`** — onboarding further repositories. The model supports it with configuration only: a container, a copy of `03.PublishDocsContent.yml` in the source repository, and an entry in `Step L2`. No change here. → defer
- **`PL-6-dedicated-app-service`** — renaming or migrating the learning hub's App Service. → closed: `D17-two-deployments-one-codebase` makes the existing name correct — a host called `learn-testmc-app-itn-01` serving the learning hub is no longer a mismatch, and the documentation site got its own name in `D18-docs-instance-host`.
- **`PL-7-learning-hub-context-generalisation`** — `.copilot/context/90.00-learning-hub/` holds site-specific rules that partly apply to any space. → `01-autonomous-streams-artifacts.plan.md`, in `diginsight/tools`
- **`PL-8-space-level-authorisation`** — per-space access control. Every space is currently public. → defer
- **`PL-9-seo-artifacts`** — `sitemap.xml`, `robots.txt`, canonical links, meta descriptions and Open Graph tags. The application's entire SEO surface today is one `<PageTitle>` in `ContentView.razor`. Prerendering is preserved and proven by `Step I2`, so nothing regresses; adding these is **new capability** and belongs to its own plan with its own goal. → defer
- **`PL-10-docs-plan-capacity`** — `samples-testmc-asp-01` is Basic B1: one instance, no deployment slots, now shared by two sites. Upgrading to Standard would buy slots and therefore zero-downtime swaps for both. Not needed while these are test deployments. → defer
- **`PL-11-smartdocs-own-documentation`** — SmartDocs' own product documentation: what it is, how to configure a space, how to onboard a repository, how to deploy it. This plan **produces** the `diginsight.smartdocs` space and publishes `src/docs` into it (`WS-H-content-publishing`); **writing** that documentation is a distinct goal with distinct exit criteria. → sibling plan

## 🏁 Exit criteria

- The three projects exist under `src/` with the new names, and `src/Diginsight.SmartDocs.slnx` builds with **0 errors**. (✅ done) — `dotnet new sln` on .NET 10 emits `.slnx`, not `.sln`; build is green with 0 warnings.
- `src/Directory.Build.props`, `src/Directory.Build.targets` and `nuget.config` exist, and `packages.lock.json` is committed for all three projects. (✅ done) — `nuget.config` sits at the repository root.
- The learning-hub configuration serves the `Step I0` baseline article URLs at **identical paths**, with no space prefix, no redirect, no index page and no switcher, per `D7-learn-space-compatibility`. (🟡 todo)
- A space with a non-root `RouteBase` mounts at that prefix **even when it is the only space configured**, per `D14-route-base-is-configured`. (🟡 todo)
- Adding a second space to a deployment changes **no URL** of an already-configured space, proven by the `Step I1` probe run. (🟡 todo)
- An article's **first HTTP response** contains its rendered heading and prose with JavaScript disabled, per `Step I2`. (🟡 todo)
- The renamed application produced a rendered first response at `Step A5`, **before** any space work began. (✅ done)
- Hydration changes nothing visible: the article shown once the WebAssembly client takes over is identical to the prerendered one, in **both** deployment shapes. (🟡 todo)
- `IContentSource` and `IContentLister` are still registered as **singletons** on the server, and the host starts under Development with scope validation active, per `D16-prerender-parity-by-construction`. (✅ done) — both now resolve through `SpaceContentRegistry`; the two runtime downcasts that made this fragile are gone.
- No space is resolved from `IHttpContextAccessor`: path-to-space resolution lives in the shared project and is called identically by both containers. (✅ done) — `SpaceRegistry` is in `Diginsight.SmartDocs.Web.Shared`.
- The unprefixed `/_nav/*` and `/_content-raw/*` routes still answer whenever a space claims the root. (✅ done)
- The local filesystem-source run renders `src/docs` in a visible browser. (✅ done) — evidence in [20260817.01-validation-sequence.md](_validation/20260817.01-validation-sequence.md).
- `appsettings.Testmc.json`, `appsettings.TestmcDocs.json` and the dispatch workflow exist in `smartdocs.internal`, and **no** storage account name, container name or App Service host name is written into any file in this repository, per `D19-public-repository-internal-configuration`. (🟡 todo) — both overlay files exist; **to do:** the dispatch workflow (`Step F2`).
- The federated credential for `repo:diginsight/smartdocs:ref:refs/heads/main` exists and `azure/login@v2` succeeds in a workflow run. (🟡 todo)
- Every variable and secret in the `Step G1` table exists, and the assertion step passes. (🟡 todo)
- `01.DeployLearnHub.yml` deployed `Diginsight.SmartDocs.Web` to `learn-testmc-app-itn-01`, and the log confirms the target. (🟡 todo)
- `01.DeployLearnHub.yml` and `02.DeployDocsSite.yml` both call `00.BuildSmartDocsWeb.yml`, so the two sites run the same build definition, per `D13-one-workflow-per-target`. (🟡 todo)
- The three inherited workflows are removed, and no workflow in this repository references `Diginsight.Tools.sln`. (🟡 todo)
- The App Service worker platform reads 64-bit and the site starts — no `HTTP 500.32`. (🟡 todo)
- Obsolete `Content__*` application settings are removed. (🟡 todo)
- The learning-hub deployment serves exactly one space from storage, with no index page and no switcher. (🟡 todo)
- The documentation deployment renders every configured repository space from storage, and its generated index at `/` lists them all without any space name appearing in markup. (🟡 todo)
- No configuration file anywhere lists the learning-hub space alongside a repository documentation space, per `D17-two-deployments-one-codebase`. (🟡 todo)
- A configured `Site:Branding` block changes the logo, favicon, product name and palette with no rebuild, and removing it restores the `Step I0` baseline appearance exactly. (🟡 todo)
- The palette custom properties are present in the **prerendered** response, not applied after hydration. (🟡 todo)
- `/_nav/invalidate` returns `401` for a missing or wrong `X-Invalidate-Key` when a key is configured, and the learning-hub content workflow still invalidates successfully with no space parameter. (🟡 todo)
- A validation-sequence Markdown with screenshots exists under `_validation/`, covering the baseline, learn-space compatibility, the second-space probe, prerendering, the index, every space, the switcher, cross-space isolation, branding and live invalidation. (🟡 todo)
- Source projects, the source deployment workflow and the source internal configuration are removed from `Learn.01` and `Learn.internal`, and a subsequent deployment of both targets still succeeds. (🟡 todo)

## 📚 References

- **📘** `.github/instructions/testing-validation.instructions.md` — mandatory validation-sequence rules for `WS-I-validation`
- **📘** `.github/instructions/plan-execution.instructions.md` — readiness gate and lifecycle this plan was authored against
- **📘** `.github/instructions/plan-marking.instructions.md` — suffix notation and identifier readability used throughout
- **📘** `.github/instructions/repository-docs.instructions.md` — rules for the `src/docs` content this plan publishes as the `diginsight.smartdocs` space
- **📗** `01-autonomous-streams-artifacts.plan.md`, in `diginsight/tools` — sibling plan producing the repository documentation this rendering host serves

<!--
validation_metadata:
  plan_id: "20260815.01-smartdocs-web-convergence"
  created: "2026-08-15"
  revised: "2026-08-17"
  status: "actionable"
  gate_passed: true
  blocking_unknowns_resolved: 23
  open_decisions: 0
  discovery_items: 3
  sibling_plan: "PL-11-smartdocs-own-documentation"
  revision_note: "Retargeted on 2026-08-17 from diginsight/tools (src/50.00 Docs) to the dedicated diginsight/smartdocs repository (src), and reframed routing from space-count to configuration. D14-route-base-is-configured supersedes D14-root-mount-when-single-space; D7-learn-space-compatibility supersedes D7-single-space-is-degenerate; D2-projects-at-src-root supersedes D2-folder-50-00-docs; D12 and D13 restated. Added D19-public-repository-internal-configuration (smartdocs is public; configuration lives in smartdocs.internal) and D20-inherited-workflows-removed. Added Step A0 (repository shell), Step G0 (per-repository federated credential) and the second-space probe in Step I1. D17's second argument was withdrawn: under D14 a second space no longer moves the hub's URLs, so single-deployment isolation is now an editorial rule rather than a mechanical consequence."
  gate_rerun_note: "Eight-check re-run on 2026-08-17 found three sequencing defects and one scope ambiguity, all now closed. Added the Execution order section: workstream letters are historical, not sequential; WS-L and WS-H interleave (L1-L4, then WS-H, then L5) because Step H3 invalidates the host Step L3 creates while Step L5 validates the content Step H2 publishes; WS-E steps E1-E2 execute early because Step A5 needs a runnable application, with Step E2 verifying rather than generating lock files if Step A5 restored first. Step B1's documentation JSON is now marked illustrative, and Step L2 states that the delivered configuration carries diginsight.smartdocs alone - a configured space whose container is never filled would fail Step L5. Checks 1, 2, 4, 5, 6 and 7 passed on re-read; check 3 passed after these edits; check 8 is not applicable. Promotion to actionable is left to the author."
-->
