---
title: "Roles and permissions on the document hierarchy: implementation plan"
author: "Dario Airoldi"
date: "2026-10-27"
categories: [plan, security, authentication, authorization, entra-id, app-roles, navigation, caching]
description: "Builds per-space access files, app roles granted through Entra groups and expanded space by space, and one authorization check on every SmartDocs surface, in six sessions."
domain: "Diginsight SmartDocs — Web"
goal: "Let a SmartDocs deployment restrict any space to readers who hold a permission: each repository declares, in an access.metadata.yml file at its content root, the permissions its space requires and what each app role grants there; Entra ID grants app roles through one security group per role; a reader holds any number of roles, combined by union space by space; and spaces that declare nothing stay public as they are today"
scope:
  covers:
    - "The access file: access.metadata.yml at each space's content root, with declared permissions, the space's requirement, and a role map, parsed strictly and never served"
    - "Requirements per space: the access file's requires, a deployment floor, and a site default, the last two limited to authenticated"
    - "Role expansion space by space from the roles claim, with any number of roles per reader combined by union"
    - "Entra sign-in inside the application, with anonymous reading still allowed"
    - "One authorization check applied to all thirteen surfaces in the analysis' inventory, including counts, snapshots, and the push hub"
    - "One development app registration with four app roles, each granted through one security group, a fixture content tree, and visible-browser validation"
    - "Refreshed generated documentation and a provisioning how-to, delivered in six sessions"
  excludes:
    - "Folder and page requirements (park lot)"
    - "A site-wide read permission that no repository owns (park lot)"
    - "Internal companions served behind an internal permission (park lot)"
    - "App-only authorization of /_nav/invalidate (park lot)"
    - "Entra access packages (park lot)"
    - "Security trimming of full-text search, owned by work item 20261027.02 (park lot)"
    - "Registrations, role groups, and access files for deployed environments (park lot)"
boundaries:
  - "The server is the only authority: no surface may return content, titles, counts, records, or asset bytes of a space the reader can't see"
  - "A failed read of an access file never makes content more visible than its declarations allow"
  - "Shared caches stay keyed by content path or space; no cache entry holds anything derived from a principal or from configuration"
  - "A space's access file decides only that space's visibility"
  - "Content and deployments that declare nothing behave as they do today"
  - "No deployed overlay sets a floor, a site default, or AccessFileRequired, and no repository publishes an access file to a deployed space, before every exit criterion is met"
rationales:
  - "Microsoft's guidance keeps app roles coarse and fine-grained permissions in the application"
  - "Evaluating each space only against its own file confines a hostile or mistaken mapping to its own space, without naming rules"
  - "Filtering at the edge preserves the performance rule that no request costs work proportional to the corpus"
  - "Several roles per reader keep roles few and orthogonal; one role per reader would need a role for every combination of access"
  - "Granting roles through groups makes granting and revoking a role a membership change that group owners can make"
status: in-progress
publish: false
---

# Roles and permissions on the document hierarchy: implementation plan

> **Status: `in-progress`.** Session 1 started on 2026-10-28, after the actionability gate passed again. Session 2 confirmed `WS-A-access-model` done, passed the gate again, and finished `WS-B-enforcement` the same day; [validation sequence 01](_validation/20261028.01-validation-sequence.md) records it, including the fix from its security review. Session 3 confirmed `WS-B-enforcement` done, passed the gate again, and finished `WS-C-counts-and-push` the same day; [validation sequence 02](_validation/20261028.02-validation-sequence.md) records it, including a fix for a push that a request for an unknown folder could time. Session 4 confirmed `WS-C-counts-and-push` done, passed the gate again, and implemented `E1-identity-web` to `E6-not-found-sign-in`. When the owner named the development tenant, a test account, and an existing registration to reuse, it resolved `DS-1-group-licence` and `DS-3-test-accounts` and finished `WS-D-entra-provisioning`. After the owner's first sign-in it resolved `DS-2-client-credential` and finished `WS-E-sign-in-and-role-expansion`; [validation sequence 03](_validation/20261028.03-validation-sequence.md) records it, including four fixes the run required. Two pre-existing defects it surfaced are on [the signals page](02-signals.md). The owner delegated the first four decisions on 2026-10-27, and they were taken as [the analysis](01-roles-permissions-support.analysis.md) recommended. Versions 1.1 to 1.5, the same day, folded in an independent design review, recorded the owner's requirement that a reader can hold several roles as `D12-several-roles-per-reader`, and answered three questions—nesting permission groups, permissions as scopes, and roles in repository metadata—without changing a decision. On 2026-10-28 the owner restated the model as a scenario: role and permission definitions in repository metadata, the permissions content requires in repository metadata, and app roles granted through corresponding Entra groups. The scenario pulled `PL-14-permission-definitions` and `PL-15-space-role-definitions` into scope and changed `D1-role-composition`, `D2-requirement-carrier`, and `D4-plan-scope`, which returned the plan to `draft`. The decisions it raised, `D13-per-space-evaluation`, `D14-authenticated-only-floors`, `D15-site-wide-read-deferred`, and `D16-required-access-file`, were taken on the owner's behalf, the gate was run again and passed, and the plan is `actionable` again. Version 1.6 restates `D6-public-by-default`, `D7-monotonic-requirements`, and `D8-computed-requirements` for the access file without changing what they decide, rewrites the steps for the new model, and adds [📅 Sessions](#-sessions). It also compares the old build with the new one to catch regressions, validates live changes through `/_nav/invalidate`, and gives a hidden page an explicit resolution, so a route never falls through to another file. Changing any decision on review returns this plan to `draft`.

> **Session 6 prerequisite waiver (2026-10-28).** The owner explicitly waived the requirement that `WS-F-end-to-end-validation` be complete before session 6 starts. The seven applicable actionability checks passed again; principle impact isn't applicable. This changes execution order only: `F1-push-per-group` through `F6-mount-route-divergence`, `DS-4-hub-expiry`, and their unsupported exit criteria remain pending. `G1-docs-update` used `changes="875698d1e4e4b08c49c4aaa7e637a79d60b52bbe..ebb6ee2e9c74c7273b3f86c703f3f64e803d8847"` because the implementation was committed and the working tree was clean. The owner's subsequent instruction to continue autonomously acknowledged the presented nine-dimension mapping. The change-driven refresh is complete: 23 body-updated generated pages, four propagation stamp updates, and five investigator-owned evidence refreshes. Independent verification found repairable documentation defects; the parent applied the final precise source-factory correction and passed the scoped checks. No new runtime validation or deployed rollout is claimed.

## 📚 Table of contents

- [🎯 Goal](#-goal)
- [🧭 Decisions taken](#-decisions-taken--todo)
- [🔧 Execution steps](#-execution-steps--todo)
  - [Surface coverage](#surface-coverage)
  - [WS-A-access-model](#ws-a-access-model--done)
  - [WS-B-enforcement](#ws-b-enforcement--done)
  - [WS-C-counts-and-push](#ws-c-counts-and-push--done)
  - [WS-D-entra-provisioning](#ws-d-entra-provisioning--done)
  - [WS-E-sign-in-and-role-expansion](#ws-e-sign-in-and-role-expansion--done)
  - [WS-F-end-to-end-validation](#ws-f-end-to-end-validation--todo)
  - [WS-G-documentation](#ws-g-documentation--done)
- [📅 Sessions](#-sessions)
  - [Session 1: access model](#session-1-access-model)
  - [Session 2: enforcement](#session-2-enforcement)
  - [Session 3: counts and push](#session-3-counts-and-push)
  - [Session 4: Entra and sign-in](#session-4-entra-and-sign-in)
  - [Session 5: end-to-end validation](#session-5-end-to-end-validation)
  - [Session 6: documentation](#session-6-documentation)
- [🏁 Exit criteria](#-exit-criteria--todo)
- [❓ Open decisions](#-open-decisions)
- [🔭 Discovery](#-discovery--todo)
- [📦 Park lot](#-park-lot)
- [📚 References](#-references)

## 🎯 Goal

Let a SmartDocs deployment restrict any space to readers who hold a permission. Each space's repository declares, in an `access.metadata.yml` file at its content root, the permissions the space requires and what each app role grants there. Entra ID grants app roles through one security group per role. A reader can hold any number of roles, and in each space holds the union of what that space maps their roles to. Spaces that declare nothing stay public, as they are today.

**Motivation.** The work item asks whether an app registration's roles can support "a permission on the repository's metadata, roles as sets of permissions, roles granted to users or groups." On 2026-10-28 the owner restated it as a scenario in three points, and [The scenario, reviewed](01-roles-permissions-support.analysis.md#the-scenario-reviewed) gives a verdict on each point and the resolution the design adopts. This plan builds that design.

**Narrowing, stated explicitly.** The scenario names repository metadata as the place for permissions and role definitions. This plan uses `access.metadata.yml` at each space's content root, because `repository.metadata.yml` never reaches the content store (`D2-requirement-carrier`). Each repository maps the shared app roles in its own space only, and its file decides only that space (`D13-per-space-evaluation`). Version 1 restricts whole spaces (`D4-plan-scope`). Folder and page requirements, which the owner plans next, wait as `PL-17-folder-and-page-requirements`, and a site-wide read permission that no repository owns waits as `PL-18-site-wide-read` (`D15-site-wide-read-deferred`).

## 🧭 Decisions taken (🟡 todo)

The owner delegated the first four decisions on 2026-10-27, and they were taken as the analysis recommended. On 2026-10-28 the owner's scenario changed `D1-role-composition`, `D2-requirement-carrier`, and `D4-plan-scope`, and the four decisions it raised, `D13-per-space-evaluation` to `D16-required-access-file`, were taken on the owner's behalf. The owner set `D12-several-roles-per-reader` on 2026-10-27. The rest are resolved from evidence in the repository or from the design review. Each is a design decision, pending until the work stream that realizes it is done.

### D1-role-composition: app roles are roles, and each repository maps them in its own space (🟡 todo)

Entra app roles are coarse roles, such as `Reader`, `Contributor`, and `Admin`. Each space's access file maps role values to that space's permissions, and SmartDocs expands a reader's roles space by space. On 2026-10-27 the decision was taken with a deployment catalog, `Site:Authorization:Roles`; the owner's 2026-10-28 scenario moved role definitions into the repositories, and [Roles in a deployment catalog](01-roles-permissions-support.analysis.md#roles-in-a-deployment-catalog) records the catalog form. *Reverses if* the owner wants roles defined once for the whole deployment, or composed in Entra. A catalog adds `Roles` to `A4-site-authorization-options`, `E4-role-expansion` expands through it, and the access files keep only `permissions` and `requires`. Composition in Entra is evaluated in [📎 Appendix: alternatives considered](01-roles-permissions-support.analysis.md#-appendix-alternatives-considered). Under option B or D, role groups or access packages hold one app role per permission: `D2-app-roles` defines permissions instead of roles, `D3-groups-and-grants` grants them to role groups or bundles them into packages, and each access file maps each permission value to itself. Under option C, SmartDocs reads the `groups` claim instead of `roles`, and `E1-identity-web` adds a Microsoft Graph fallback for readers whose groups claim overflows. Group object IDs differ from tenant to tenant, so a map in each deployment's overlay, added to `A4-site-authorization-options`, turns them into the role values the access files use. In every case `G2-provisioning-how-to` changes, and the plan returns to `draft`. Nesting permission groups inside role groups isn't an alternative, because app role grants don't reach nested members, as [Permission groups inside role groups](01-roles-permissions-support.analysis.md#permission-groups-inside-role-groups) shows. Nor is defining permissions as scopes, because a scope authorizes a client application, not the reader, as [Permissions as scopes](01-roles-permissions-support.analysis.md#permissions-as-scopes) shows.

### D2-requirement-carrier: `access.metadata.yml` at each space's content root, plus a deployment floor (🟡 todo)

A space's declared permissions, its requirement, and its role map live in `access.metadata.yml`, beside the root `index.md` of the space's content root, which the publish workflow already copies to the content store. `Site:Spaces[i]:Access` adds a deployment floor. `repository.metadata.yml` isn't a carrier, because it never reaches the content store. The 2026-10-27 carrier, an `access` line in folder `metadata.yml`, returns only with folder requirements (`PL-17-folder-and-page-requirements`). *Reverses if* the owner wants `repository.metadata.yml` to be authoritative—the publish workflow would then copy its access section into the content root—or wants the declarations in deployment configuration only, which takes them out of the repositories' reviews.

### D3-unauthorized-response: not visible answers as not found (🟡 todo)

Each surface answers a node the reader can't see exactly as it answers a node that doesn't exist. When sign-in is configured, every 404 page offers anonymous readers a **Sign in** link, whether the route is missing or hidden. When the site default requirement isn't empty, every anonymous page request is challenged before the route is looked up. *Reverses if* the owner prefers discoverability, such as a "no access" page, over concealment.

### D4-plan-scope: space-level requirements only (🟡 todo)

Version 1 restricts whole spaces. Folder and page requirements, a site-wide read permission, internal companions, app-only invalidation, and access packages wait in [📦 Park lot](#-park-lot). On 2026-10-27 the decision covered the core model, folder requirements included; the owner's 2026-10-28 scenario put folder and article metadata in the future. *Reverses if* the owner pulls a parked item in; the plan then returns to `draft`.

### D5-sign-in-in-application: Microsoft Identity Web inside the host (🟡 todo)

Resolved from evidence. `testing-validation.instructions.md` requires visible local runs, and App Service built-in authentication has no local equivalent. Sign-in is wired only when `AzureAd:ClientId` is set; otherwise the host stays anonymous-only, as today.

### D6-public-by-default: no access file means public (🟡 todo)

Resolved from evidence: every current deployment is public, and no repository has an access file. A space without one keeps today's behavior, unless its `AccessFileRequired` setting is on (`D16-required-access-file`). A requirement fails closed on a host without sign-in, because anonymous readers hold no permissions.

### D7-monotonic-requirements: requirements accumulate (🟡 todo)

A space's effective requirement is the union of the site default, its floor, and its access file's `requires`. A source can only add, so a repository can't lift a floor or the default. A prefixed space's nodes start at its mount folder, so a root-mounted space's file never reaches them, and nodes outside every space follow the site default alone. Folder and page declarations, when `PL-17-folder-and-page-requirements` adds them, extend the same union down the tree.

### D8-computed-requirements: content facts cached, requirements computed per check, failures close the space (🟡 todo)

Each space's parsed access file is cached as a content fact, keyed by the space's mount, for `Diginsight:SmartCache:SmartDocs:Access`, 30 seconds by default, and cleared by the invalidation that clears navigation. Each check combines it with the configured default and floor, so no cache entry holds an effective requirement. An access file that can't be read, or whose content is invalid, gives the space a requirement no reader meets; a read error isn't cached, so the next check retries. Navigation nodes carry the folder they come from, and a page is checked through the exact file that was rendered, so neither routes nor candidate order can make the check and the content disagree.

### D9-explicit-session-lifetime: eight hours, no sliding (🟡 todo)

`Site:Authorization:SessionLifetime` defaults to `08:00:00` with sliding expiration off, so the cookie's lifetime bounds how long a removed grant keeps working. The ASP.NET Core default of 14 days with sliding expiration isn't used. Push connections close when the session expires.

### D10-response-policy: authorize before entity tags, cache privately (🟡 todo)

Every surface checks visibility before it compares entity tags, so a reader who can't see a node never gets a `304` for it. Responses whose content depends on the reader send `private, no-cache`; prerendered pages send `private, no-store`; bytes of a node with a non-empty requirement send `private, no-store`. Bytes of a node with an empty requirement keep today's headers.

### D11-configuration-shape: the settings this plan adds (🟡 todo)

The base `appsettings.json` carries these sections with empty values. Identifiers, floors, and `AccessFileRequired` come only from environment overlays, following the rule `Site:Spaces` already follows: a space element in an overlay can add `"Access": "authenticated"` and `"AccessFileRequired": true`. Role definitions aren't configuration; they live in each space's access file.

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "",
    "ClientId": "",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "Site": {
    "Authorization": {
      "DefaultRequirement": "",
      "SessionLifetime": "08:00:00"
    }
  },
  "Diginsight": {
    "SmartCache": {
      "SmartDocs": {
        "Access": "00:00:30"
      }
    }
  }
}
```

### D12-several-roles-per-reader: a reader holds any number of roles, combined by union (🟡 todo)

Set by the owner on 2026-10-27: supporting a single role per reader isn't an option. Entra grants a user any number of an application's roles, directly and through groups, and the `roles` claim carries every one of them. In each space, a signed-in reader's permissions are `authenticated` plus everything that space's access file maps any of their roles to. Roles only add: there's no deny entry, no precedence, and no primary role. Role values form a case-insensitive set, so order and repeats don't matter, and a value a space doesn't map adds nothing there without affecting the reader's other roles. A role's grant in one space never counts in another (`D13-per-space-evaluation`). `B1-reader-access`, `C3-push-per-group`, and `E4-role-expansion` implement it; `E7-validate-sign-in` and `F2-revocation` validate it. *Reverses if* the owner wants exclusive or ranked roles, where one role overrides or excludes another. That needs a precedence model the analysis doesn't define, and the plan returns to `draft`.

### D13-per-space-evaluation: each space is evaluated only against its own file (🟡 todo)

Taken on the owner's behalf on 2026-10-28. A permission is the pair of a space and a name, and a role's grant in one space never counts in another, so a repository's mappings can open only its own space, whatever names it uses. No naming convention is needed, and a hostile or mistaken file can only hide or open its own space. *Reverses if* the owner wants one repository to define roles for other spaces too. That needs the naming rules sketched in [Permissions and roles in repository metadata](01-roles-permissions-support.analysis.md#permissions-and-roles-in-repository-metadata), and the plan returns to `draft`.

### D14-authenticated-only-floors: floors and the site default accept only `authenticated` (🟡 todo)

Taken on the owner's behalf on 2026-10-28. `Site:Spaces[i]:Access` and `Site:Authorization:DefaultRequirement` accept an empty value or `authenticated`, and the host refuses to start with anything else. A floor naming a repository's permission would let that repository's own mappings satisfy it, so it couldn't restrict anything the repository didn't agree to. *Reverses if* the owner wants deployment-defined permissions, which also need a deployment-level role map (`PL-18-site-wide-read`); the plan then returns to `draft`.

### D15-site-wide-read-deferred: no site-wide read permission in version 1 (🟡 todo)

Taken on the owner's behalf on 2026-10-28. A permission that opens every space would belong to no repository, and per-space evaluation gives it nowhere to live. A site-wide role is each repository's deliberate mapping of a shared role value such as `Reader`, and signing in, or a baseline role on a private site, already gates a whole site. `A4-site-authorization-options` and `G2-provisioning-how-to` state it. *Reverses if* the owner wants a permission no repository has to map; `PL-18-site-wide-read` describes it, and the plan returns to `draft`.

### D16-required-access-file: `AccessFileRequired` per space, off by default (🟡 todo)

Taken on the owner's behalf on 2026-10-28. A space whose `Site:Spaces[i]:AccessFileRequired` setting is on is hidden while its access file is missing, and the host logs an `Error`. That closes two windows: a first publish that uploads pages before the access file, and a deletion of the file from a repository that turns the space public at the next publish. *Reverses if* the owner wants every space to need an access file, which changes `D6-public-by-default` for existing deployments, or no space to, which leaves both windows open; the plan then returns to `draft`.

## 🔧 Execution steps (🟡 todo)

`testing-validation.instructions.md` applies to every work stream that changes runtime behavior: rebuild without `--no-build`, run the server in a visible console, validate in a visible browser, and record each run under `_validation/` with screenshots. Captures that show a tenant, such as the Entra sign-in page's address bar, go to `_validation/_internal/`, per `content-classification.instructions.md`. The work streams run in order, grouped into the six sessions of [📅 Sessions](#-sessions).

The fixture's spaces are prefixed, and the `/_test/article` endpoints can't target a prefixed space, so validations change fixture files on disk and then call `POST /_nav/invalidate`, with a `path` when one subtree changed. The fixture overlay sets no `Site:InvalidateApiKey`, so the endpoint needs no key locally.

### Surface coverage

Every surface in the analysis' inventory lands in at least one step. The table maps each surface, by its number in the inventory, to the steps that change it.

| # | Surface | Steps |
|---|---|---|
| 1 | Page routes and prerender | `B4-page-routes`, `B10-prerender`, `E6-not-found-sign-in` |
| 2 | `/_page/{**path}` | `A8-render-key`, `B3-authorizing-page-resolver` |
| 3 | `/_content/{**key}` | `A9-exclude-access-file`, `B5-raw-content-endpoint` |
| 4 | `/_nav/children` | `B6-nav-children` |
| 5 | `/_nav/folder` | `B7-nav-folder`, `C2-reader-counts` |
| 6 | `/_nav/index` | `B8-nav-index` |
| 7 | `/_nav/total` | `C2-reader-counts` |
| 8 | `/_site` | `B9-site-shell` |
| 9 | Generated space index | `B4-page-routes`, `B9-site-shell` |
| 10 | Nav hub | `C1-counts-per-space`, `C3-push-per-group` |
| 11 | `/robots.txt` | `B11-robots`, `E2-sign-in-endpoints` |
| 12 | Branding mark | `B12-branding-exemption` |
| 13 | `POST /_nav/invalidate` | Unchanged; `PL-2-app-only-invalidation` |

### WS-A-access-model (✅ done)

**Landing:** `src/Diginsight.SmartDocs.Web/Diginsight.SmartDocs.Web.csproj` and its `packages.lock.json`, `src/Diginsight.SmartDocs.Web.Shared/Sites/{SiteOptions,SpaceRegistry}.cs`, `src/Diginsight.SmartDocs.Web.Shared/Navigation/IContentLister.cs`, `src/Diginsight.SmartDocs.Web.Shared/Rendering/IMarkdownRenderer.cs`, new `src/Diginsight.SmartDocs.Web/Authorization/{AccessFile,AccessFileProvider,AccessRequirement,AccessRequirementResolver}.cs`, `src/Diginsight.SmartDocs.Web/Caching/ContentFreshnessOptions.cs`, `src/Diginsight.SmartDocs.Web/ContentSources/SpaceMountedContentSource.cs`, `src/Diginsight.SmartDocs.Web/Navigation/{DynamicNavBuilder,FolderRecordProvider}.cs`, `src/Diginsight.SmartDocs.Web/Rendering/RenderedPageProvider.cs`, `src/Diginsight.SmartDocs.Web/Program.cs`, `src/Diginsight.SmartDocs.Web/appsettings.json`, and this folder's `_validation/baseline/`.

- `A0-baseline-pin`: Before any code change, run `git status` and `git rev-parse HEAD`, and write the commit to `_validation/baseline/commit.txt` in this folder. `F4-regression` builds that commit in a separate worktree and compares it with the finished work. When `git status` shows changes under `src/` outside `src/docs/`, list them in the same file, because the baseline build won't contain them. (✅ done)
- `A1-access-file-model`: Add `AccessFile`, which parses `access.metadata.yml` with YamlDotNet. Add the package with `dotnet add src/Diginsight.SmartDocs.Web package YamlDotNet`, which also updates the lock file, and build the deserializer with `WithDuplicateKeyChecking()` and without ignoring unmatched properties. (✅ done)
  - The keys are `schema`, required and equal to `1`; `permissions`, a map from permission name to description; `requires`, a list of permission names; and `roles`, a map from role value to a list of permission names. Every key except `schema` is optional.
  - The file is invalid when its YAML is malformed, when it has an unknown key or a missing or unknown `schema`, when a permission name or role value repeats, ignoring case, when `requires` or a role entry names a permission that `permissions` doesn't declare, or when `authenticated` is declared as a permission or mapped by a role. `requires` may name `authenticated`.
  - Permission names and role values start with a letter and continue with letters, digits, `.`, `-`, or `_`. Any other name makes the file invalid.
  - The result is `Valid`, carrying the space's `requires`, its role map, and a SHA-256 fingerprint of the file's bytes, or `Invalid`, carrying the problem in words an author can act on.
- `A2-access-file-provider`: Add `AccessFileProvider`, a singleton registered in `Program.cs`. `GetAsync(spaceId)` reads `access.metadata.yml` at the space's content root through `SpaceContentRegistry.Get(spaceId).Source`, the space's physical source below the mounted view and the content cache, and parses it with `AccessFile`. (✅ done)
  - It caches `Missing`, `Valid`, or `Invalid` under `ContentPathCacheKey("access", <mount>)`, where `<mount>` is the space's route base without slashes, empty for a root-mounted space, so the invalidation that clears a space's navigation clears its access file too. The lifetime is a new `ContentFreshnessOptions.Access`, 30 seconds by default, set as `"Access": "00:00:30"` under `Diginsight:SmartCache:SmartDocs` in `appsettings.json`.
  - A failure other than "not found" escapes the cached call and is returned as `Unreadable`, which isn't cached, so the next check reads the file again.
  - It logs each change of a space's outcome: `Information` for a valid file, with the space, its requirement, its role map, and the fingerprint; `Error` for an invalid or unreadable file, or for a missing one when the space's `AccessFileRequired` is on.
  - Added by session 1's security review: when the file isn't found, the provider lists the space's root, and a file whose name differs from `access.metadata.yml` only in case or spelling, which a case-sensitive store doesn't find, makes the outcome `Invalid` instead of `Missing`, so a misnamed file closes the space instead of publishing it.
- `A3-space-options`: In [`SpaceOptions`](../../../../Diginsight.SmartDocs.Web.Shared/Sites/SiteOptions.cs), add `string Access`, empty by default, and `bool AccessFileRequired`, false by default, documented next to `AllowGptBot` as the space's deployment floor and as the switch of `D16-required-access-file`. `/_site` serializes `SpaceShellInfo`, not `SpaceOptions`, so neither setting reaches the browser. (✅ done)
- `A4-site-authorization-options`: Add `SiteAuthorizationOptions` as `SiteOptions.Authorization`, with `DefaultRequirement` and `SessionLifetime`. At startup, refuse a site default or a space floor other than empty or `authenticated`, and a `SessionLifetime` that isn't positive, with an exception that names the setting: extend `SpaceRegistry.Validate` for floors, and validate the site settings where `Program.cs` binds `SiteOptions`. Add the sections of `D11-configuration-shape` to `appsettings.json`, with a comment stating that identifiers, floors, and `AccessFileRequired` come only from environment overlays, that roles live in the spaces' access files, and that no setting grants every space (`D15-site-wide-read-deferred`). (✅ done)
- `A5-access-requirement-type`: Add `AccessRequirement`: the owning space's id, or none for a path outside every space, and an immutable, case-insensitive, sorted set of permission names, with `Empty`, `Unresolvable`, which no permission set covers, and `IsCoveredBy(permissions)`. The name `authenticated` is reserved for the permission every signed-in reader holds. The type stays on the server, because the browser never evaluates access. (✅ done)
- `A6-node-source-folder`: Give `NavChild` and `NavLeaf`, in [`IContentLister.cs`](../../../../Diginsight.SmartDocs.Web.Shared/Navigation/IContentLister.cs), a `SourceFolder` property: the content folder the node comes from, which names its space. Mark it `[JsonIgnore]` from `System.Text.Json`, so navigation responses and the prerendered state don't change; the SmartCache serializer, Newtonsoft, still stores it. In [`DynamicNavBuilder`](../../../../Diginsight.SmartDocs.Web/Navigation/DynamicNavBuilder.cs), set it to the folder's own path for a section, a space mount, and a collapsed one-page folder, to the level's prefix for an article and for the **Home** node, and carry it to each leaf in `WalkAsync`. (✅ done)
- `A7-requirement-resolver`: Add `AccessRequirementResolver`, a singleton, with `ForSpaceAsync(spaceId)`, `ForFolderAsync(prefix)`, and `ForContentKeyAsync(key)`; the last two find the owning space with `SpaceRegistry.TryResolve`. (✅ done)
  - A space's requirement is the union of `Site:Authorization:DefaultRequirement`, the space's `Access` floor, and its access file's `requires`.
  - A missing file adds nothing, or makes the requirement `Unresolvable` when the space's `AccessFileRequired` is on. An invalid or unreadable file makes it `Unresolvable`.
  - A path outside every space gets the site default alone.
  - The resolver adds no cache entry: each call combines the provider's cached file with the current configuration.
- `A8-render-key`: Add `string? ContentKey` to `RenderedPageResolution` in [`IMarkdownRenderer.cs`](../../../../Diginsight.SmartDocs.Web.Shared/Rendering/IMarkdownRenderer.cs). In [`RenderedPageProvider`](../../../../Diginsight.SmartDocs.Web/Rendering/RenderedPageProvider.cs), store the candidate key that `BuildAsync` rendered in the cached envelope, and return it with the page. (✅ done)
- `A9-exclude-access-file`: Keep access files out of every content surface. (✅ done)
  - In [`SpaceMountedContentSource`](../../../../Diginsight.SmartDocs.Web/ContentSources/SpaceMountedContentSource.cs), make `GetAsync` and `ReadHeadAsync` return `null` when a key's last segment is `access.metadata.yml`, ignoring case, and drop such entries in `ListChildrenAsync` and `ListSiteRootAsync`. Log a `Warning` once per path for a file below a space's content root, which version 1 ignores.
  - In [`FolderRecordProvider.BuildAsync`](../../../../Diginsight.SmartDocs.Web/Navigation/FolderRecordProvider.cs), remove an `access` key from the dictionary placed in `FolderRecord.Metadata`, and log a `Warning` once per folder, because a folder `access` line restricts nothing until `PL-17-folder-and-page-requirements`.
  - Added by session 1's security review: the exclusion canonicalizes the key first, resolving dot segments, trailing dots and spaces, and alternate data streams. [`FileSystemContentSource`](../../../../Diginsight.SmartDocs.Web/ContentSources/FileSystemContentSource.cs) also refuses a key that resolves to an access file without naming one, because `Path.GetFullPath` expands NTFS 8.3 short names such as `ACCESS~1.YML`.
- `A10-build`: Build with `dotnet build src/Diginsight.SmartDocs.slnx -c Debug`, and fix every new warning before WS-B starts. Nothing enforces requirements yet, so this work stream has no visible-browser run; `B14-validate-anonymous` and `F5-unreadable-access-file` validate it. (✅ done)

### WS-B-enforcement (✅ done)

**Landing:** new `src/Diginsight.SmartDocs.Web/Authorization/{RoleSet,SpaceVisibilityEvaluator,ReaderAccess,AuthorizingRenderedPageResolver}.cs`, `src/Diginsight.SmartDocs.Web.Shared/Rendering/IMarkdownRenderer.cs`, `src/Diginsight.SmartDocs.Web/Program.cs`, `src/Diginsight.SmartDocs.Web/Endpoints/{PageRouteGuard,ContentEndpoints,NavEndpoints,SiteEndpoints,HttpValidators}.cs`, `src/Diginsight.SmartDocs.Web/Navigation/{ServerNavProvider,FolderRecordProvider,NavigationBootstrapMiddleware}.cs`, `src/Diginsight.SmartDocs.Web/Sites/BrandingAssets.cs`, `src/Diginsight.SmartDocs.Web/Properties/launchSettings.json`, `src/Diginsight.SmartDocs.Web/ContentSources/{ContentKeyGuard,SpaceMountedContentSource,BlobContentSource}.cs` (session 2's security review), new `eng/fixtures/access-tree/`, the internal peer's `src/Diginsight.SmartDocs.Web/appsettings.access.json`, and `_validation/`.

Every endpoint in this work stream applies `D10-response-policy`: the visibility check runs before `HttpValidators.IsNotModified`, and the headers follow the policy.

- `B1-reader-access`: Add three types under `Diginsight.SmartDocs.Web/Authorization/`. (✅ done)
  - `RoleSet`: whether the reader is signed in, plus an immutable, case-insensitive, sorted set of role values, with a stable `Id` derived from both. `RoleSet.FromPrincipal(user)` returns the anonymous set, or a signed-in set with no roles until `E4-role-expansion` fills it.
  - `SpaceVisibilityEvaluator`, a singleton that the push hub shares. In each space, an anonymous reader holds no permission, and a signed-in reader holds `authenticated` plus, for every role in the set, what that space's role map grants it, per `D12-several-roles-per-reader` and `D13-per-space-evaluation`. A space is visible when those permissions cover its requirement from `ForSpaceAsync`; a node outside every space is checked against the site default, which only `authenticated` can cover. The evaluator keeps no cache and no memo, so a changed access file or setting applies at the next check.
  - `ReaderAccess`, scoped: `InitializeAsync` evaluates every configured space for the request's `RoleSet`, on every request. It exposes `IsVisible(requirement)`, which checks a requirement against the reader's permissions in the space that owns it; `VisibleSpaces`; and `FilterLevelAsync` and `FilterIndexAsync`, which find each node's space from its `SourceFolder`, never from its route, and drop the nodes whose space isn't visible.
  - Added during session 2: `AccessRequirementResolver.ResolveSpaceAsync` returns a space's requirement together with the access file it came from, so the evaluator checks a requirement against grants from the same read of the file. `IsVisible(requirement)` also requires the space to be visible in the request's own evaluation, so a newer read can only hide more.
- `B2-reader-access-middleware`: In [`Program.cs`](../../../../Diginsight.SmartDocs.Web/Program.cs), register `ReaderAccess` and a middleware that calls `InitializeAsync`, placed before `NavigationBootstrapMiddleware`, so every later component and endpoint reads initialized values. `E1-identity-web` later inserts `UseAuthentication` ahead of it. (✅ done)
- `B3-authorizing-page-resolver`: Give `RenderedPageResolution` a `bool IsHidden` member and a static `Hidden`, handled and without a page; records compare by value, so without the member `Hidden` would equal `Nothing`. Add `AuthorizingRenderedPageResolver`, scoped, which calls `RenderedPageProvider` and returns `Hidden` when the resolution's `ContentKey` isn't visible, never another candidate. Register it as the server's `IRenderedPageResolver` in place of the singleton registration that forwards to `RenderedPageProvider`, and make both `/_page` routes in [`ContentEndpoints`](../../../../Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs) take `IRenderedPageResolver` instead of `RenderedPageProvider`. Whether a page answers depends on the reader, so both routes send `private, no-cache` through a new `HttpValidators.RevalidatePrivate` constant. The prerendered `ContentView`, through `PageLoader`, and `/_page` then read pages only through the check. `/_page` answers a hidden page with the existing `204 No Content`, as it answers a missing one, and `PageLoader` needs no change, because it returns a handled resolution's page as is. (✅ done)
- `B4-page-routes`: In [`PageRouteGuard`](../../../../Diginsight.SmartDocs.Web/Endpoints/PageRouteGuard.cs), make `RouteExistsAsync` also return the route's folder: the folder the route names, which every mount route does, or else the folder that holds the file naming it. When the route exists, resolve it through `IRenderedPageResolver`, and let the request through in three cases only; otherwise write the guard's existing 404 response. (✅ done)
  - The resolution carries a page, which the reader can therefore see.
  - The route is empty, and the reader sees at least one space. A reader who can't see the root-mounted space then gets the generated index of the spaces they can see, because `B9-site-shell` leaves no root-mounted space in their shell, so a reload of `/` and a navigation to it show the same page.
  - Any other route renders no page, hidden or absent, and `ForFolderAsync` finds the route's folder visible. At a mount route whose `<mount>.md` is hidden, the mounted space's folder decides, so the route answers as a folder without a page of its own; a page, or a file the renderer skips, in a space the reader can't see answers 404.
- `B5-raw-content-endpoint`: In `ContentEndpoints.GetContentRawAsync`, find the key's requirement with `ForContentKeyAsync`, and answer `404` when the reader can't see it, before any entity tag is computed. A visible key with a non-empty requirement gets `private, no-store`; a key with an empty requirement keeps today's headers. (✅ done)
  - Added by session 2's security review: Kestrel removes dot segments before it decodes `%5C`, so a key like `product-c\..\product-a\x.md` was checked in `product-c` while a blob store would collapse `..` and read another container. A new `ContentKeyGuard` refuses keys with dot, empty, or `%` segments in `AccessRequirementResolver`, which makes them `Unresolvable`, and in `SpaceMountedContentSource.TryMap`, which reads nothing for them; `BlobContentSource` refuses them too and checks that each blob client stays in its container. This covers `/_content`, `/_page`, and `/_nav/folder`.
- `B6-nav-children`: In [`NavEndpoints.GetNavChildrenAsync`](../../../../Diginsight.SmartDocs.Web/Endpoints/NavEndpoints.cs), filter the level through `FilterLevelAsync`, fetch folder records only for the children that remain, and send `private, no-cache` through `HttpValidators.RevalidatePrivate`. (✅ done)
- `B7-nav-folder`: Add `ReaderAccess.GetFolderAsync(prefix)`. When a non-empty prefix isn't visible, it returns `FolderRecordProvider.Unknown(prefix)`: a new method that builds the record the provider returns for a prefix with no node, no `metadata.yml`, and no metrics. The empty prefix is the site's own record, built from site settings, so it's never hidden, and `C2-reader-counts` composes its counts. Make `NavEndpoints.GetFolderAsync` and `ServerNavProvider.GetFolderAsync`, which the prerender uses, read records only through it, and send `private, no-cache` from the endpoint. (✅ done)
- `B8-nav-index`: In `NavEndpoints.GetNavIndexAsync`, filter the leaves through `FilterIndexAsync`, and send `private, no-cache`. (✅ done)
- `B9-site-shell`: Filter the spaces in `/_site`, in [`SiteEndpoints`](../../../../Diginsight.SmartDocs.Web/Endpoints/SiteEndpoints.cs), and send `private, no-cache`. Filter the scoped `SiteShellState` factory in `Program.cs`, which also seeds `NavigationBootstrapState.Site`, to `ReaderAccess.VisibleSpaces`. The generated space index follows, and when the root-mounted space is hidden, the reader's shell has none, so the browser serves the generated index at `/` too. (✅ done)
- `B10-prerender`: In [`ServerNavProvider`](../../../../Diginsight.SmartDocs.Web/Navigation/ServerNavProvider.cs), filter each level through `FilterLevelAsync` before `bootstrap.SetLevel`, fetch records only for the remaining children, and filter `GetIndexAsync`. In [`NavigationBootstrapMiddleware`](../../../../Diginsight.SmartDocs.Web/Navigation/NavigationBootstrapMiddleware.cs), send `private, no-store` on the HTML responses it completes. (✅ done)
- `B11-robots`: In `SiteEndpoints.BuildRobots`, list only the spaces an anonymous reader can see, and omit the rest entirely. When the site default requirement isn't empty, emit `Disallow: /` for every agent instead. (✅ done)
- `B12-branding-exemption`: Leave `BrandingAssets.LogoRoute` unauthorized, and state the exemption in the XML comment of `BrandingAssets`. Added during session 2: `BrandingAssetResolver` refuses a `LogoPath` that names an access file, because the mark is read from the physical source, below the mounted view's exclusion. (✅ done)
- `B13-access-fixture`: Add the fixture tree and the profile that serves it. (✅ done)
  - Under `eng/fixtures/access-tree/`, create five content roots. `product-a/` holds the analysis' [`product-a` file](01-roles-permissions-support.analysis.md#the-access-file), verbatim, as `access.metadata.yml`, plus `index.md`, an `overview.md` that embeds a small PNG from `images/diagram.png`, and `guides/` with two pages. `product-b/` holds the analysis' hostile [`product-b` file](01-roles-permissions-support.analysis.md#roles-in-the-repositories), verbatim, plus `index.md` and `release-notes.md`.
  - `product-c/` has no access file at its root. It holds `index.md`, an `overview.md` that embeds `images/chart.png`, and `notes/`, with an `index.md` and a misplaced `access.metadata.yml` declaring `schema: 1` and `requires: [authenticated]`. `product-d/` holds `index.md` and `overview.md`.
  - `home/` holds `index.md`, a decoy `product-c.md` whose text says it's the decoy, and an access file declaring `schema: 1` and `requires: [authenticated]`. Only `F6-mount-route-divergence` mounts it.
  - In `launchSettings.json`, add the profile `https - access`, with `AppsettingsEnvironmentName=access`, `ExternalConfigurationFolder=..\..\..\smartdocs.internal`, and `applicationUrl` `https://localhost:7280;http://localhost:5280`.
  - In the internal peer, write `src/Diginsight.SmartDocs.Web/appsettings.access.json` with four prefixed spaces, `product-a` to `product-d`. Each has `RouteBase` `/product-x`, `Source` `FileSystem`, and `FileSystem:RootPath` `..\..\eng\fixtures\access-tree\product-x`, where `product-x` is the space's id. `product-a` sets `AccessFileRequired`, `product-d` sets `Access` to `authenticated`, and the overlay sets no `Site:InvalidateApiKey`. It also sets console logging, `Information` for `Diginsight.SmartDocs.Web.Authorization` and `Diginsight.SmartDocs.Web.Program`, so `B14-validate-anonymous` can read the access file summaries and warnings in the console.
- `B14-validate-anonymous`: Run the `https - access` profile and record `_validation/<yyyymmdd>.01-validation-sequence.md`. As an anonymous reader, confirm the following, and return a failed scenario to the step that owns its surface. Counts are validated in `C4-validate-counts-and-push`. Recorded in [`_validation/20261028.01-validation-sequence.md`](_validation/20261028.01-validation-sequence.md). (✅ done)
  - Only `product-c` appears in the sidebar, the top bar, menu search, prev and next links, and the generated space index at `/`.
  - Each `product-a`, `product-b`, and `product-d` route, its `/_page` answer, its `/_nav/folder` record, and `product-a`'s `diagram.png` answer exactly like a missing node.
  - A request for `diagram.png` with `If-None-Match: *` answers `404`, not `304`.
  - `/_content/product-a/access.metadata.yml` and `/_content/product-c/notes/access.metadata.yml` answer `404`.
  - `product-c/notes/` stays visible despite its misplaced file, and the console shows one `Warning` for that file.
  - `/robots.txt` names only `product-c`.
  - The console shows one `Information` summary each for `product-a` and `product-b`.

### WS-C-counts-and-push (✅ done)

**Landing:** `src/Diginsight.SmartDocs.Web/Navigation/{FolderMetricsIndex,ServerNavProvider,NavHub,NavChangePublisher}.cs`, new `src/Diginsight.SmartDocs.Web/Navigation/{ReaderCountComposer,ActiveRoleSets}.cs`, `src/Diginsight.SmartDocs.Web/Navigation/FolderRecordProvider.cs` (session 3's validation), `src/Diginsight.SmartDocs.Web/Endpoints/NavEndpoints.cs`, `src/Diginsight.SmartDocs.Web/Program.cs`, `src/Diginsight.SmartDocs.Web/Authorization/{ReaderAccess,SpaceVisibilityEvaluator}.cs`, the internal peer's `src/Diginsight.SmartDocs.Web/appsettings.access.json` (one log level), and `_validation/`.

- `C1-counts-per-space`: In [`FolderMetricsIndex`](../../../../Diginsight.SmartDocs.Web/Navigation/FolderMetricsIndex.cs), keep a root-own cell next to the root cell: the root-mounted space's articles outside every mount, folded in the same pass from the root level's children whose folder isn't a mount. Every mount is a single path segment, so each mounted space's totals are already the cell of its mount folder. Persist the root-own cell in the snapshot under a new format version, and ignore a snapshot in the old format, logging that it was ignored, so the crawl rebuilds it. (✅ done)
- `C2-reader-counts`: Compose the site-wide counts per reader. When the reader sees every space, return the root cell unchanged. Otherwise add up the cells of the visible mounted spaces, plus the root-own cell when the reader sees the root-mounted space; take the newest `Latest` among them, and the weakest `Coverage`, or the root cell's coverage when the reader sees no space. When the index has no root cell yet, answer as today. Apply the composition to the root record that `ReaderAccess.GetFolderAsync` returns, to `/_nav/total`, which also sends `private, no-cache`, to `ServerNavProvider.GetTotalAsync`, and to pushed root records. (✅ done)
- `C3-push-per-group`: Push each record only to readers who can see it, and only while their session lasts. (✅ done)
  - In `Program.cs`, map the hub with `CloseOnAuthenticationExpiration` set to `true`, so a connection closes when its authentication expires.
  - In [`NavHub`](../../../../Diginsight.SmartDocs.Web/Navigation/NavHub.cs), take the connection's `RoleSet.FromPrincipal(Context.User)`, add the connection to the SignalR group `rs:<Id>`, and keep a reference count of active role sets, updated in `OnConnectedAsync` and `OnDisconnectedAsync`. A reconnect is a new connection, so its role set is taken again.
  - In [`NavChangePublisher`](../../../../Diginsight.SmartDocs.Web/Navigation/NavChangePublisher.cs), `OnMetricsChangedAsync`, `PublishCountsReadyAsync`, and `SendCurrentCountsAsync` evaluate each active role set, or the caller's, through `SpaceVisibilityEvaluator` at every push. They drop the records it can't see, compose its root record per `C2-reader-counts`, and send the result to its group or caller instead of `Clients.All`.
  - `OnMetricsChangedAsync` sends a group its composed root record only together with at least one changed record it can see, and otherwise sends it nothing, so a change in a hidden space can't be timed from outside.
  - Added during session 3: `ActiveRoleSets`, a singleton, holds the connection-to-role-set map and the reference count, because the hub is transient; `ReaderVisibility.IsVisible(requirement)` holds the check that `ReaderAccess.IsVisible` delegates to, so the publisher applies the same rule off the request path; and a change among the root-mounted space's own articles, which has no record of its own, counts as a visible change for the groups that see that space.
  - Added by session 3's validation: `FolderRecordProvider`'s invalidation callback calls the new `FolderMetricsIndex.InvalidateIfTracked`, so a folder record requested for a prefix that no content has no longer makes the index track it. Before, the next push folded that cell and sent its record to every group, timed by a change in a hidden space. (✅ done)
- `C4-validate-counts-and-push`: Record `_validation/<yyyymmdd>.02-validation-sequence.md`. A failed scenario returns to `C1-counts-per-space`, `C2-reader-counts`, or `C3-push-per-group`. Recorded in [`_validation/20261028.02-validation-sequence.md`](_validation/20261028.02-validation-sequence.md). (✅ done)
  - The first start after `C1-counts-per-space` ignores the snapshot that `B14-validate-anonymous` left, and the console says so.
  - As an anonymous reader, folder counts, the root record, and the footer total cover `product-c` only.
  - Add `product-a\guides\probe.md` and call `POST /_nav/invalidate?path=product-a/guides`. In the browser's network panel, the anonymous window receives no `MetadataChanged` frame, and its counts stay unchanged. Delete the probe and invalidate again.
  - Stop the host and restart it with the snapshot from this run. The anonymous total never includes `product-a`, `product-b`, or `product-d`, before or after the crawl settles.

### WS-D-entra-provisioning (✅ done)

**Landing:** one app registration and four security groups in the development tenant, the internal peer's `src/Diginsight.SmartDocs.Web/appsettings.access.json`, and a new alias registry `src/docs/_aliases/access.aliases.internal.md` in the internal peer.

- `D1-development-registration`: In the tenant the owner develops in, register a single-tenant application named `SmartDocs access validation (local)`. Give it the Web redirect URIs `https://localhost:7280/signin-oidc` and `https://localhost:7280/signout-callback-oidc`, and the front-channel logout URL `https://localhost:7280/signout-oidc`. Record its tenant ID and client ID under `AzureAd` in the internal overlay, and add alias rows for the tenant and the registration to the internal alias registry before any public page refers to them. On 2026-10-28 the owner chose the development tenant and, as a test, reused the samples testmc registration instead of registering a new one. Session 4 added only the two redirect URIs and the logout URL to it, kept its single-tenant audience and its other settings, and saved its previous manifest outside both repositories. The tenant ID and client ID are in the internal overlay, and the new internal alias registry `src/docs/_aliases/access.aliases.internal.md` names them. (✅ done)
- `D2-app-roles`: Define four app roles with allowed member types **Users/Groups** and the values `Reader`, `Contributor`, `Admin`, and `ProductB.Reader`: the role values the fixture's access files map. Added to the registration the owner chose; it had no app roles before. (✅ done)
- `D3-groups-and-grants`: Create four security groups, *SmartDocs readers*, *SmartDocs contributors*, *SmartDocs administrators*, and *Product B readers*, and grant each group its app role, `Reader`, `Contributor`, `Admin`, and `ProductB.Reader` respectively, as `DS-1-group-licence` and `DS-3-test-accounts` resolve. Make the *reader* account a direct member of *SmartDocs readers*, make the *multi* account a direct member of *SmartDocs readers* and *Product B readers*, and leave the *none* account in no group. Keep **Assignment required** set to **No**, and record the group object IDs only in the internal alias registry. The four groups are created and granted through Microsoft Graph's `appRoleAssignedTo`, and **Assignment required** was already **No**. With one test account, `E7-validate-sign-in` sets the account's memberships per scenario, as the negative branch of `DS-3-test-accounts` describes. (✅ done)

### WS-E-sign-in-and-role-expansion (✅ done)

**Landing:** `src/Diginsight.SmartDocs.Web/Diginsight.SmartDocs.Web.csproj` and its `packages.lock.json`, `src/Diginsight.SmartDocs.Web/Program.cs`, new `src/Diginsight.SmartDocs.Web/Endpoints/AuthEndpoints.cs`, `src/Diginsight.SmartDocs.Web/Endpoints/{PageRouteGuard,SiteEndpoints}.cs`, `src/Diginsight.SmartDocs.Web/Authorization/{RoleSet,SpaceVisibilityEvaluator}.cs`, `src/Diginsight.SmartDocs.Web.Shared/Sites/SiteOptions.cs`, `src/Diginsight.SmartDocs.Web.Client/{Program.cs,Layout/MainLayout.razor}`, a new client sign-in component under `Layout/`, the internal overlay, and `_validation/`. `E7-validate-sign-in`'s fixes also landed in `src/Diginsight.SmartDocs.Web/Navigation/NavigationBootstrapMiddleware.cs`, `src/Diginsight.SmartDocs.Web.Shared/Navigation/NavigationBootstrapState.cs`, new `src/Diginsight.SmartDocs.Web.Client/BootstrapAuthenticationStateProvider.cs`, `src/Diginsight.SmartDocs.Web/wwwroot/app.css`, and the client's `packages.lock.json`.

- `E1-identity-web`: Add the `Microsoft.Identity.Web` package to `Diginsight.SmartDocs.Web`. When `AzureAd:ClientId` is set, call `AddMicrosoftIdentityWebApp` with the `AzureAd` section and the role claim type `roles`, and configure the cookie with `ExpireTimeSpan` from `Site:Authorization:SessionLifetime` and `SlidingExpiration` off. Insert `UseAuthentication()` immediately before the `ReaderAccess` middleware, and register no fallback authorization policy, so anonymous reading keeps working. When `AzureAd:ClientId` is empty, register nothing. Done in session 4 ahead of `WS-D-entra-provisioning`, because the tenant work was blocked and no step of this work stream depends on its identifiers. `AuthEndpoints.AddReaderSignIn` also turns off inbound claim mapping and `UseTokenLifetime`, so role values arrive as `roles` claims and the ID token can't shorten or extend the session, and sends the cookie only over HTTPS. (✅ done)
- `E2-sign-in-endpoints`: In `AuthEndpoints`, map `GET /_auth/signin?returnUrl=` and `POST /_auth/signout`. The sign-in endpoint challenges with `returnUrl` as the redirect only when it's a local path—one leading `/`, not `//` or `/\`—and uses `/` otherwise. The sign-out endpoint validates antiforgery and signs out of the cookie and OpenID Connect schemes; signing out navigates away, which closes the page's hub connection. Add `/_auth/` to the application paths that `BuildRobots` disallows. Both endpoints are mapped only when sign-in is enabled. (✅ done)
- `E3-auth-state-to-browser`: Call `AddAuthenticationStateSerialization()` on the server's Razor components builder. In the client's `Program.cs`, call `AddAuthorizationCore()`, `AddCascadingAuthenticationState()`, and `AddAuthenticationStateDeserialization()`. The last needs the `Microsoft.AspNetCore.Components.WebAssembly.Authentication` package, added to `Diginsight.SmartDocs.Web.Client` with `dotnet add`, which also updated its lock file. The server also calls `AddCascadingAuthenticationState()`, so the prerendered sign-in component reads the same state the browser restores. Changed by `E7-validate-sign-in`: this host never emits the framework's persisted-state payload (`SIG-1` of [the signals page](02-signals.md)), so the serialization pair delivered nothing. `NavigationBootstrapMiddleware` now writes the reader's display name into the host's own bootstrap record, and the client registers `BootstrapAuthenticationStateProvider`, which reads it. The serialization calls are gone, and the client references `Microsoft.AspNetCore.Components.Authorization` in place of `Microsoft.AspNetCore.Components.WebAssembly.Authentication`. (✅ done)
- `E4-role-expansion`: Make `RoleSet.FromPrincipal` collect the values of every `roles` claim the principal carries into its case-insensitive set, so `ReaderAccess` and `NavHub` expand them space by space through `SpaceVisibilityEvaluator`, as `D12-several-roles-per-reader` and `D13-per-space-evaluation` define. A value that no space with a valid access file maps grants nothing and is logged at `Warning`, once per value per process, and the reader's other values still count. No configuration lists roles: the fixture's access files define what each role grants. `RoleSet.FromPrincipal` reads only authenticated identities, and the set of logged values is capped at 256. Changed by `E7-validate-sign-in`: the role values reached the principal as `ClaimTypes.Role`, renamed by inbound claim mapping despite `MapInboundClaims = false`, so `RoleSet.FromPrincipal` also reads that claim type. (✅ done)
- `E5-shell-sign-in`: Add `SiteShellOptions.SignInEnabled`, set when `AzureAd:ClientId` isn't empty, and serialize it only when it's `true`, with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`, so a host without sign-in sends today's `/_site` body to `F4-regression`. In [`MainLayout.razor`](../../../../Diginsight.SmartDocs.Web.Client/Layout/MainLayout.razor), between `AboutMenu` and the search button, render a new sign-in component when the flag is set: **Sign in** for anonymous readers, and the reader's name with a **Sign out** action for signed-in readers. The component is `Layout/SignInMenu.razor`. Its **Sign in** link forces a full load once the client runs, because the client router would otherwise claim `/_auth/signin`, and its **Sign out** action is a native form post carrying an antiforgery token. Changed by `E7-validate-sign-in`: the token travels in the host's bootstrap record and renders as a hidden field, because `<AntiforgeryToken />` lost it after hydration. Below 860 px the reader's name and the **Sign in** label hide, and the site title shrinks, so the top bar's controls stay inside the viewport. (✅ done)
- `E6-not-found-sign-in`: Change the 404 page and the challenge in `PageRouteGuard`. With sign-in enabled, the 404 page also sends `private, no-cache`, because it depends on the reader. (✅ done)
  - When sign-in is enabled and the reader is anonymous, every 404 page—for a missing route or a hidden one alike—gets a **Sign in** link. Build its target with `QueryHelpers.AddQueryString("/_auth/signin", "returnUrl", <request path>)`, and HTML-encode the finished URL with `HtmlEncoder.Default` before writing it into the page. Never write the raw request path into the HTML.
  - When `Site:Authorization:DefaultRequirement` isn't empty, challenge every anonymous page request before `RouteExistsAsync` runs, whether or not the route exists.
- `E7-validate-sign-in`: Record `_validation/<yyyymmdd>.03-validation-sequence.md`. This step's first sign-in also probes `DS-2-client-credential`. A failed scenario returns to the step that owns it. Recorded in [`_validation/20261028.03-validation-sequence.md`](_validation/20261028.03-validation-sequence.md), including four fixes the run required, in `E3-auth-state-to-browser`, `E4-role-expansion`, and `E5-shell-sign-in`. With one test account, each new sign-in after a membership change was a challenge while signed in, answered by Entra's session; **Sign out** has a step of its own. (✅ done)
  - Signed in as each account of `D3-groups-and-grants` in turn: the *none* account sees `product-c` and `product-d`; the *reader* account also sees `product-a`; the *multi* account sees all four spaces, because its second group's role opens `product-b`, which validates the union of `D12-several-roles-per-reader`. Counts and the footer total match what each reader sees, and **Sign out** returns the anonymous view.
  - Add the *none* account to *SmartDocs contributors*, sign out and in again, and confirm it sees all four spaces. Remove it again.
  - Remove the *multi* account from *SmartDocs readers*, sign out and in again, and confirm it sees `product-b`, `product-c`, and `product-d` but not `product-a`, although `product-b`'s file maps its `ProductB.Reader` role to a permission named `ProductA.Read`. That validates `D13-per-space-evaluation`. Restore the membership, and sign in again.
  - Remove the `ProductB.Reader` entry from `product-b`'s access file, and call `POST /_nav/invalidate` without a path. Without signing in again, the *multi* account loses `product-b` at its next reload but keeps `product-a`, and one `Warning` names `ProductB.Reader`. Restore the entry, and invalidate again.
  - As an anonymous reader, a request for `/x%22%3E%3Cb%3Einjected%3C%2Fb%3E` returns the 404 page with the link's markup intact in the live DOM and no injected element.

### WS-F-end-to-end-validation (🟡 todo)

Session 5's first attempt on 2026-10-28 confirmed all seven `WS-E-sign-in-and-role-expansion` steps done and passed the actionability gate. The solution rebuilt with no warnings or errors, but the active Azure CLI account wasn't authenticated to the development tenant; its Graph check failed with `AADSTS90072`. The owner wasn't available to complete the requested tenant login, so execution stopped before `F1-push-per-group`, without membership, overlay, or fixture changes. [Sequence 04](_validation/20261028.04-validation-sequence.md) records this blocked preflight. All steps below and `DS-4-hub-expiry` remain pending.

**Landing:** `_validation/` in this folder, the internal overlay, and a temporary worktree at `..\smartdocs.baseline`, which `F4-regression` removes.

- `F1-push-per-group`: Open two visible browser windows: one anonymous, one signed in as the *multi* account. Add `product-b\probe.md` and call `POST /_nav/invalidate?path=product-b`. Confirm that the *multi* window's `product-b` count and footer total update without a reload, and that the anonymous window's network panel shows no `MetadataChanged` frame. Delete the probe, and invalidate again. Record the results as the first scenario of `_validation/<yyyymmdd>.04-validation-sequence.md`. A failure returns to `C3-push-per-group`. (🟡 todo)
- `F2-revocation`: Validate revocation, and record the results in sequence 04. A failure returns to `E1-identity-web`, `E4-role-expansion`, or `C3-push-per-group`. (🟡 todo)
  - Set `Site:Authorization:SessionLifetime` to `00:05:00` in the internal overlay, and restart the host. Sign in as the *reader* account, keep its window open, and remove the account from *SmartDocs readers*. Confirm that the window's hub connection closes when the session expires, which probes `DS-4-hub-expiry`, and that after signing in again, `product-a` is gone. Restore the lifetime and the membership, and restart the host.
  - Remove the *multi* account from *Product B readers*, then sign out and in again as the *multi* account. Confirm that `product-b` is gone while `product-a` stays: removing one role removes only the spaces that no remaining role opens. Restore the membership.
- `F3-cache-headers`: In the network panel, signed in as the *reader* account, confirm that nav, site, and page JSON carry `private, no-cache`, prerendered pages carry `private, no-store`, and `product-a`'s `diagram.png` carries `private, no-store`, while `product-c`'s `chart.png` keeps `public, max-age`. Signed in as the *none* account, request `product-a`'s `diagram.png` with `If-None-Match: *` through `fetch` in the browser's console, so the request carries the session cookie, and confirm `404`. Record the results in sequence 04. A failure returns to the step that owns the surface. (🟡 todo)
- `F4-regression`: Compare the finished work with the commit that `A0-baseline-pin` recorded, serving the same content, and record the comparison in sequence 04. A difference is a defect in the step whose surface produced it. (🟡 todo)
  1. Run `git worktree add --detach ..\smartdocs.baseline <commit>`, with the commit from `_validation/baseline/commit.txt`, and build the worktree with `dotnet build src/Diginsight.SmartDocs.slnx -c Debug`.
  2. Mirror this repository's content into the worktree with `robocopy src\docs ..\smartdocs.baseline\src\docs /MIR`, which keeps file times, so both builds serve the same files.
  3. Delete `nav-metrics-snapshot.json` from `src\Diginsight.SmartDocs.Web\bin\Debug\net10.0\` in both trees, so both builds crawl from scratch.
  4. Run the worktree's `http` profile in a visible console. When `/_nav/total` reports complete coverage, save the body and the `Cache-Control`, `ETag`, and `Content-Type` headers of `/_nav/children?prefix=`, `/_nav/folder?prefix=`, `/_nav/total`, `/_nav/index`, and `/_site` to `_validation/baseline/`. Stop the host with Ctrl+C.
  5. Run this repository's `http` profile the same way, and save the same responses to `_validation/final/`.
  6. Confirm that the bodies are byte-identical and that only `Cache-Control` differs, as `D10-response-policy` defines. Then run `git worktree remove --force ..\smartdocs.baseline`.
- `F5-unreadable-access-file`: In the `https - access` profile, make `product-a`'s access file unusable in three ways, one at a time: hold an exclusive lock on it from a PowerShell session with `[System.IO.File]::Open(<path>, 'Open', 'Read', 'None')`; change its `schema` to `2`; and move it to `$env:TEMP`. After each change, call `POST /_nav/invalidate?path=product-a`, and confirm, as an anonymous reader and as the *reader* account, that `product-a` is invisible, that its pages answer 404, and that the console logs an `Error` for the space. Undo the change, invalidate again, and confirm the *reader* account sees `product-a` again. Record the results in sequence 04. A failure returns to `A2-access-file-provider` or `A7-requirement-resolver`. (🟡 todo)
- `F6-mount-route-divergence`: Add `home` to the internal overlay as a root-mounted space, with `RouteBase` `/`, `Source` `FileSystem`, and `FileSystem:RootPath` `..\..\eng\fixtures\access-tree\home`, and restart the host. Record the results in sequence 04. A failure returns to `A8-render-key`, `B3-authorizing-page-resolver`, or `B4-page-routes`. (🟡 todo)
  - As an anonymous reader, `/product-c` shows `product-c`'s listing on a reload and after a sidebar click, and never the decoy's text. `/_page/product-c` answers `204`, `/product-c/overview` is readable, and `/` shows the generated index listing only `product-c`, on a reload and after a navigation.
  - Signed in as the *none* account, which sees `home`, `/product-c` shows the decoy.
  - Remove `home` from the overlay, and restart the host.

### WS-G-documentation (✅ done)

**Landing:** the generated chapters under `src/docs/`, a new `02-reader-access-provisioning.md` in this folder, and [the analysis](01-roles-permissions-support.analysis.md).

- `G1-docs-update`: Run `/01.04-ad-docs-update-from-changes`, so the generated security, configuration, and architecture chapters describe the new options, the sign-in flow, and the authorization check. When `HEAD` still equals the commit in `_validation/baseline/commit.txt`, pass `changes="working-tree"`. When the work is committed and `git status` shows no change under `src/` outside `src/docs/`, pass `changes="<baseline>..<head>"`, with that commit and `git rev-parse HEAD`. Otherwise, ask the owner to commit the remaining changes first. Completed on 2026-10-28 with the exact range in the waiver note; the [access-file schema](../../../06.00-reference/07-access-file-schema.md), security, configuration, architecture and dependent reading/navigation pages now distinguish implementation from unexecuted validation. Final scoped checks passed for 27 generated pages, 880 anchor resolutions and 187 local links; `git diff --check` passed. (✅ done)
- `G2-provisioning-how-to`: Write `02-reader-access-provisioning.md` in this folder: a how-to for one deployment, naming every resource by role. Completed on 2026-10-28 in [the provisioning how-to](02-reader-access-provisioning.md): seven provisioning steps, all six operating rules, DS-1/DS-2 outcomes and DS-4 explicitly unresolved. Its JSON example parses, its links resolve, and the documentation review confirmed the required scope and distinction between local ID-token evidence and untested deployed certificate/code flow. (✅ done)
  - Its steps register the application; define app roles whose values match the role values that the deployment's access files map; create one security group per role and grant it that role, or grant roles to users directly where the tenant lacks Entra ID P1 or P2; set **Assignment required**, with a baseline role when it's **Yes**; write each repository's `access.metadata.yml`; and fill the overlay, including floors and `AccessFileRequired`.
  - It states six operating rules. Publish a restricted space's access file with or before its first content, or set `AccessFileRequired`, so the space stays hidden until the file arrives. Exclude `access.metadata.yml` from any other site generator that builds the same content root, such as MkDocs through `exclude_docs`, because such a generator copies files it doesn't recognize into its site, and restrict that site to the same readers or stop publishing a restricted space there, because it serves the same pages. Grant roles through direct group membership only, because a group's app roles don't reach the members of nested groups. Give each deployment its own `Diginsight:SmartCache:Redis:KeyPrefix` when Redis is enabled. Revoke by removing a member, an assignment, or a mapping, because disabling an app role leaves existing grants in force. Make a role site-wide by mapping it in every repository's access file, per `D15-site-wide-read-deferred`.
  - It records what `DS-1-group-licence`, `DS-2-client-credential`, and `DS-4-hub-expiry` resolved.
- `G3-analysis-status`: Update the **Status** line of the analysis with the outcome and links to the validation sequences. Completed on 2026-10-28: [the analysis](01-roles-permissions-support.analysis.md) links all four sequences, distinguishes the bounded PASS records from sequence 04's blocked preflight, and retains the plan's `in-progress` status and rollout boundary. (✅ done)

## 📅 Sessions

The six sessions run in order, each in a fresh conversation started with its prompt below. A session ends when every step of its work streams is `(✅ done)`; the steps carry the status, so sessions carry none. Sessions 1, 2, and 4 build or configure the authorization boundary itself, so they run on Claude Opus 5.5 and end with a security review. The other sessions run on Claude Sonnet 5.5.

| Session | Covers | After | Agent | Model |
|---|---|---|---|---|
| [1](#session-1-access-model) | `WS-A-access-model` | None | Default agent, then `/security-review` | Claude Opus 5.5 |
| [2](#session-2-enforcement) | `WS-B-enforcement`, validation sequence 01 | Session 1 | Default agent, then `/security-review` | Claude Opus 5.5 |
| [3](#session-3-counts-and-push) | `WS-C-counts-and-push`, validation sequence 02 | Session 2 | Default agent | Claude Sonnet 5.5 |
| [4](#session-4-entra-and-sign-in) | `DS-1-group-licence`, `DS-2-client-credential`, `DS-3-test-accounts`, `WS-D-entra-provisioning`, `WS-E-sign-in-and-role-expansion`, validation sequence 03 | Session 3 | Default agent with the `entra-app-registration` skill, after the owner runs `az login` to the development tenant; then `/security-review` | Claude Opus 5.5 |
| [5](#session-5-end-to-end-validation) | `DS-4-hub-expiry`, `WS-F-end-to-end-validation`, validation sequence 04 | Session 4 | Default agent, after the owner runs `az login` to the development tenant | Claude Sonnet 5.5 |
| [6](#session-6-documentation) | `WS-G-documentation` | Session 5 | `/01.04-ad-docs-update-from-changes` for `G1-docs-update`, then the default agent | Claude Sonnet 5.5; `G1-docs-update` runs on the model its prompt declares |

Every session follows these rules.

| Topic | Rule |
|---|---|
| Start | Read this plan and [the analysis](01-roles-permissions-support.analysis.md), cite ids in full, and run the actionability gate before any change. |
| Build | Rebuild with `dotnet build src/Diginsight.SmartDocs.slnx -c Debug`, never with `--no-build`, so client changes are served. |
| Host | Run `dotnet run --launch-profile "https - access"` from `src/Diginsight.SmartDocs.Web` in a visible console, and stop it with Ctrl+C. `F4-regression` uses the `http` profile instead. |
| Browser | Validate in a visible browser, reading the live DOM and the network panel. The owner completes every sign-in; the agent never asks for or types a password. |
| Records | Record each run as `_validation/<yyyymmdd>.NN-validation-sequence.md` in this folder, from `.github/templates/output-validation-sequence.template.md`, with `publish: false` and screenshots in `_validation/images/`. Captures that show a tenant go to `_validation/_internal/`. |
| Memberships | Scenarios change group memberships with `az ad group member add` and `az ad group member remove`. Where `DS-1-group-licence` took its negative branch, they add and remove app role assignments through Microsoft Graph's `appRoleAssignedTo` endpoint with `az rest` instead. A changed membership or assignment applies at the account's next sign-in. |
| Identifiers | Tenant IDs, client IDs, and group object IDs go only in the internal overlay and the internal alias registry, in `smartdocs.internal`. |
| Status | Each step is marked `(✅ done)` as soon as it's finished. A failed check returns to the step that owns it, and an edge case goes to [📦 Park lot](#-park-lot). |
| Commits | Changes stay uncommitted for the owner to review. |

### Session 1: access model

```text
Execute session 1 of the plan src/docs/90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md: work stream WS-A-access-model, from A0-baseline-pin to A10-build.

1. Read the plan and the analysis next to it. Run the plan's actionability gate, and stop and report if it fails.
2. Run git status. Set the plan's status to in-progress, and do A0-baseline-pin before changing any code.
3. Do A1-access-file-model through A10-build in order, marking each step (✅ done) as soon as it's finished.
4. Follow the session rules in the plan's 📅 Sessions section.
5. When A10-build passes, run /security-review on the changes, fix what it reports with high confidence, and summarize what's left.
```

### Session 2: enforcement

```text
Execute session 2 of the plan src/docs/90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md: work stream WS-B-enforcement, from B1-reader-access to B14-validate-anonymous.

1. Read the plan and the analysis next to it. Confirm that every step of WS-A-access-model is (✅ done), run the plan's actionability gate, and stop and report if either check fails.
2. Do the steps in order, marking each step (✅ done) as soon as it's finished. B13-access-fixture writes the fixture tree in this repository and the access overlay in the sibling repository smartdocs.internal.
3. For B14-validate-anonymous, run the https - access profile in a visible console, validate in a visible browser, and record _validation/<yyyymmdd>.01-validation-sequence.md with screenshots.
4. Follow the session rules in the plan's 📅 Sessions section.
5. Run /security-review on the changes, fix what it reports with high confidence, rerun the B14-validate-anonymous scenarios the fixes touch, and summarize what's left.
```

### Session 3: counts and push

```text
Execute session 3 of the plan src/docs/90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md: work stream WS-C-counts-and-push, from C1-counts-per-space to C4-validate-counts-and-push.

1. Read the plan and the analysis next to it. Confirm that every step of WS-B-enforcement is (✅ done), run the plan's actionability gate, and stop and report if either check fails.
2. Do the steps in order, marking each step (✅ done) as soon as it's finished.
3. For C4-validate-counts-and-push, watch the nav hub's WebSocket frames in the browser's network panel, and record _validation/<yyyymmdd>.02-validation-sequence.md with screenshots.
4. Follow the session rules in the plan's 📅 Sessions section.
```

### Session 4: Entra and sign-in

```text
Execute session 4 of the plan src/docs/90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md: discovery items DS-1-group-licence, DS-2-client-credential, and DS-3-test-accounts, and work streams WS-D-entra-provisioning and WS-E-sign-in-and-role-expansion, from D1-development-registration to E7-validate-sign-in.

1. Read the plan and the analysis next to it. Confirm that every step of WS-C-counts-and-push is (✅ done), run the plan's actionability gate, and stop and report if either check fails.
2. Use the entra-app-registration skill. I've run az login to the development tenant: show me az account show, and wait for my confirmation before creating anything.
3. Resolve DS-1-group-licence and DS-3-test-accounts before D3-groups-and-grants, and follow their negative branches when they apply. DS-2-client-credential resolves at the first sign-in of E7-validate-sign-in.
4. Write tenant IDs, client IDs, and group object IDs only to the internal overlay and the internal alias registry in smartdocs.internal, never to this repository.
5. Do the steps in order, marking each step (✅ done) as soon as it's finished. Ask me to complete each sign-in in the visible browser. Record E7-validate-sign-in as _validation/<yyyymmdd>.03-validation-sequence.md, with tenant captures under _validation/_internal/.
6. Follow the session rules in the plan's 📅 Sessions section.
7. Run /security-review on the changes, fix what it reports with high confidence, rerun the E7-validate-sign-in scenarios the fixes touch, and summarize what's left.
```

### Session 5: end-to-end validation

```text
Execute session 5 of the plan src/docs/90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md: discovery item DS-4-hub-expiry and work stream WS-F-end-to-end-validation, from F1-push-per-group to F6-mount-route-divergence.

1. Read the plan and the analysis next to it. Confirm that every step of WS-E-sign-in-and-role-expansion is (✅ done), run the plan's actionability gate, and stop and report if either check fails.
2. I've run az login to the development tenant. Change memberships as the session rules say, and ask me to complete each sign-in in the visible browser.
3. Do the steps in order, marking each step (✅ done) as soon as it's finished, and record them all in _validation/<yyyymmdd>.04-validation-sequence.md.
4. DS-4-hub-expiry resolves in F2-revocation. If its negative branch applies, implement it and rerun F2-revocation.
5. F4-regression creates and removes the worktree ..\smartdocs.baseline; leave no worktree behind.
6. Follow the session rules in the plan's 📅 Sessions section.
```

### Session 6: documentation

```text
Execute session 6 of the plan src/docs/90.00-issues/202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.plan.md: work stream WS-G-documentation, from G1-docs-update to G3-analysis-status.

1. Read the plan and the analysis next to it. Confirm that every step of WS-F-end-to-end-validation is (✅ done), run the plan's actionability gate, and stop and report if either check fails.
2. For G1-docs-update, run /01.04-ad-docs-update-from-changes with the changes argument that the step defines.
3. Do G2-provisioning-how-to and G3-analysis-status, marking each step (✅ done) as soon as it's finished.
4. Mark each exit criterion (✅ done) that its recorded evidence meets, and set the plan's status to done only when every step and exit criterion is (✅ done).
5. Follow the session rules in the plan's 📅 Sessions section.
```

## 🏁 Exit criteria (🟡 todo)

- With no access file, floor, or `AccessFileRequired` setting, the `http` profile's `/_nav/children?prefix=`, `/_nav/folder?prefix=`, `/_nav/total`, `/_nav/index`, and `/_site` bodies match the baseline commit's byte for byte, and only their `Cache-Control` headers differ, as recorded by `F4-regression`. (🟡 todo)
- An anonymous reader learns nothing about a hidden space from any of the thirteen surfaces—titles, routes, counts, newest dates, records, `304` answers, asset bytes, or access files—as recorded in sequences 01 and 02. Session 6 reviewed these records without upgrading their bounded fixture coverage to an exhaustive claim: explicit branding/administrative-contract coverage, every newest-date field, and nonzero root-own composition aren't established by those two runs. Root-mount validation remains in `F6-mount-route-divergence`. (🟡 todo)
- Each test account sees exactly the spaces its roles open, space by space: the *multi* account's two roles combine by union, and a role that one space's file maps opens nothing in another space, as recorded in sequence 03. (✅ done)
- A role value that no valid access file maps opens nothing and is logged, while the reader's other roles keep working, and a mapping removed from an access file takes effect for open sessions at their next reload, as recorded in sequence 03. (✅ done)
- A crafted request path can't inject markup into the 404 page, as recorded in sequence 03. (✅ done)
- Pushed records reach only the readers who can see them, and an open hub connection stops receiving them when its session expires, as recorded in sequence 04. (🟡 todo)
- A removed membership stops granting its role at the first sign-in after the session lifetime, and removing one of a reader's roles hides only the spaces that no remaining role opens, as recorded in sequence 04. (🟡 todo)
- Responses follow `D10-response-policy`, as recorded in sequence 04. (🟡 todo)
- An unreadable or invalid access file, or a missing one where `AccessFileRequired` is set, hides its space instead of publishing it, as recorded in sequence 04. (🟡 todo)
- A hidden space mounted at `/` never renders its page at a route that another space mounts, and `/` and mount routes show the same content on a reload and after a navigation, as recorded in sequence 04. (🟡 todo)
- The generated chapters and the provisioning how-to describe the shipped behavior. `G1-docs-update` and `G2-provisioning-how-to` completed on 2026-10-28 with source-linked documentation verification, a strict [access-file reference](../../../06.00-reference/07-access-file-schema.md), the required [provisioning procedure](02-reader-access-provisioning.md), and explicit separation of implemented controls from unexecuted validation. (✅ done)

## ❓ Open decisions

None. The four owner decisions were delegated and taken on 2026-10-27, and the owner set `D12-several-roles-per-reader`. On 2026-10-28 the owner's scenario changed `D1-role-composition`, `D2-requirement-carrier`, and `D4-plan-scope`, and `D13-per-space-evaluation`, `D14-authenticated-only-floors`, `D15-site-wide-read-deferred`, and `D16-required-access-file` were taken on the owner's behalf. [🧭 Decisions taken](#-decisions-taken--todo) records them and what would reverse each.

## 🔭 Discovery (🟡 todo)

These facts can't be settled until execution. Each probe has a defined negative branch, so the plan proceeds either way.

- `DS-1-group-licence`: Before `D3-groups-and-grants`, check **Licenses** in the Entra admin center for Entra ID P1 or P2 in the development tenant. If present, grant roles through groups, as `D3-groups-and-grants` describes. **Negative branch:** if absent, create no groups, and assign each account its roles directly on the enterprise application instead, one assignment per account and role, so the *multi* account gets two. State in the provisioning how-to that group grants need P1 or P2, and in `E7-validate-sign-in` and `F2-revocation`, add and remove assignments wherever they change memberships. Resolved in session 4 on 2026-10-28. A first check read the wrong tenant, an organization directory that `az login` had selected by default, where the owner's account couldn't create groups. The owner then named the development tenant, which has Entra ID P1 and P2 through Microsoft 365 E5, so the positive branch applies. (✅ done)
- `DS-2-client-credential`: At the first sign-in of `E7-validate-sign-in`, sign-in runs with only `TenantId` and `ClientId`, as in Microsoft's quickstart. **Negative branch:** if code redemption fails with `AADSTS7000218`, add a client secret to the registration and store it with `dotnet user-secrets` in `Diginsight.SmartDocs.Web`, which already declares a `UserSecretsId`—never in either repository. Record in the provisioning how-to that deployed hosts use a Key Vault certificate instead. Session 4 found, with a placeholder client ID, that Microsoft Identity Web without a client credential doesn't use the code flow: it requests `response_type=id_token` with `response_mode=form_post`. The first sign-in is therefore expected to fail with `AADSTS700054` unless the registration allows ID tokens, rather than with `AADSTS7000218`. The registration the owner chose already allows ID tokens, so the first sign-in is expected to succeed without a secret. Resolved at the first sign-in of `E7-validate-sign-in` on 2026-10-28: Entra returned an ID token with no `AADSTS` error, so the positive branch applies and no secret was created. The provisioning how-to still records that deployed hosts use a Key Vault certificate, and the code flow it enables. (✅ done)
- `DS-3-test-accounts`: Before `D3-groups-and-grants`, list the accounts available in the development tenant; the plan needs three, *reader*, *multi*, and *none*. **Negative branch:** with fewer than three, run every signed-in scenario of `E7-validate-sign-in` and `WS-F-end-to-end-validation` with one account, changing its memberships to match the account the scenario names—*SmartDocs readers* for *reader*, *SmartDocs readers* and *Product B readers* for *multi*, and no group for *none*—and signing in again after each change. Resolved in session 4 on 2026-10-28: the owner named one account, so the negative branch applies, here and in `WS-F-end-to-end-validation`. (✅ done)
- `DS-4-hub-expiry`: In `F2-revocation`, observe whether `CloseOnAuthenticationExpiration` closes a cookie-authenticated hub connection when its session expires; Microsoft documents it with bearer tokens. **Negative branch:** if the connection stays open, `NavHub.OnConnectedAsync` records the expiry from the connection's authentication properties, and `NavChangePublisher` removes expired connections from their `rs:<Id>` group with `Groups.RemoveFromGroupAsync` before every push. Rerun `F2-revocation`, confirming instead that after the session expires, adding `product-c\probe.md` and calling `POST /_nav/invalidate?path=product-c` sends the open window no `MetadataChanged` frame. (🟡 todo)

## 📦 Park lot

These items are in the domain of this goal and deliberately excluded from it.

- `PL-1-internal-companions`: Serve `_internal/` folders and `*.internal.md` files behind an internal permission of their space, and turn public pointers to companions into links that are absent for readers without it. → defer: needs internal content published into the same store, and `PL-17-folder-and-page-requirements`.
- `PL-2-app-only-invalidation`: Replace the `X-Invalidate-Key` shared secret with an app role granted to the publish workload identity. → defer.
- `PL-3-access-packages`: Entitlement management on top of the role groups, so readers can request a role and an approver grants it for a limited time. → defer: needs Entra ID Governance or Entra Suite.
- `PL-4-search-trimming`: Full-text search filters results through `ReaderAccess`. → defer to work item `20261027.02-smartdocs-search`.
- `PL-5-locked-space-cards`: Show restricted spaces as locked cards with a request-access hint. → closed: contradicts `D3-unauthorized-response`; reopen only if that decision changes.
- `PL-6-orphan-report`: Warn about permissions an access file declares that none of its roles grants, and about app roles that no access file maps. → defer.
- `PL-7-deployed-registrations`: Registrations, app roles, groups, and access files for deployed environments, starting with the multi-space product deployment. That includes excluding each access file from any other site generator that builds the same content root, and restricting that generator's site to the same readers, or no longer publishing a restricted space there. → defer: one sibling plan per deployment, after this plan is done.
- `PL-8-unit-tests`: A test project covering `AccessFile`, `AccessRequirement`, the resolver, `SpaceVisibilityEvaluator`, and the count composition. → defer: the repository has no test project today.
- `PL-9-platform-authentication`: App Service built-in authentication as an extra layer in front of the application. → defer.
- `PL-10-multi-tenant-readers`: Readers who sign in from several tenants. → defer.
- `PL-11-page-level-access`: `access` in front matter, with a render-time check that every file a restricted page links is covered by the page's requirement. → closed: merged into `PL-17-folder-and-page-requirements`.
- `PL-12-redis-prefix-check`: A startup warning when Redis is enabled with the default key prefix. → defer: the provisioning how-to states the rule.
- `PL-13-client-scopes`: One or two coarse delegated scopes, such as `Docs.Read`, on SmartDocs' registration, for client applications such as a command-line tool or an agent that call SmartDocs on a reader's behalf. Each call stays filtered by the reader's permissions. → defer: needs a client that calls SmartDocs with an access token; this plan adds none.
- `PL-14-permission-definitions`: Each repository lists the permission names its content may require, each with a description. → closed: pulled in on 2026-10-28 as each access file's `permissions` (`A1-access-file-model`).
- `PL-15-space-role-definitions`: Role definitions read from a space's own repository. → closed: pulled in on 2026-10-28 as each access file's `roles`, evaluated per space (`D13-per-space-evaluation`).
- `PL-16-access-first-publish`: A publish that uploads each space's access file before its other content, so a first publish never serves pages before the file that guards them. → defer: `AccessFileRequired` closes the window today, and the provisioning how-to states the rule.
- `PL-17-folder-and-page-requirements`: Requirements declared by folder and page metadata, under the rules in [Folder and page requirements, later](01-roles-permissions-support.analysis.md#folder-and-page-requirements-later): monotonic within the space, names from the space's access file, pages through their folders first, and count vectors per requirement. → defer: version 1 restricts whole spaces (`D4-plan-scope`).
- `PL-18-site-wide-read`: A permission that opens every space, defined in deployment configuration because no repository owns it. → defer: `D15-site-wide-read-deferred`; mapping a role in every repository's access file covers the need today.
- `PL-19-access-file-pipeline-check`: A publish pipeline check that each repository's access file is present where its deployment requires one, and valid. → defer.
- `PL-20-role-summary-report`: A report, across repositories, of what each app role grants in each space. → defer: the host logs each space's requirement and role map whenever it loads the space's file.

## 📚 References

- [Roles and permissions on the document hierarchy: analysis](01-roles-permissions-support.analysis.md): the options, the surface inventory, and the design this plan builds.
- [Testing and validation rules](../../../../../.github/instructions/testing-validation.instructions.md): the visible-run and validation-sequence requirements for every runtime change.
- [Content classification rules](../../../../../.github/instructions/content-classification.instructions.md): where captures and identifiers from the development tenant go.
- [Plan execution rules](../../../../../.github/instructions/plan-execution.instructions.md): the lifecycle and gate this plan follows.

**[Quickstart: sign in users in an ASP.NET Core web app](https://learn.microsoft.com/entra/msidweb/getting-started/quickstart-webapp)** 📘 [Official]  
The Microsoft Identity Web setup and registration settings that `E1-identity-web`, `D1-development-registration`, and `DS-2-client-credential` follow.

**[Add app roles to your application and receive them in the token](https://learn.microsoft.com/entra/identity-platform/howto-add-app-roles-in-apps)** 📘 [Official]  
How `D2-app-roles` defines the roles and how they reach the `roles` claim.

**[Manage users and groups assignment to an application](https://learn.microsoft.com/entra/identity/enterprise-apps/assign-user-or-group-access-portal)** 📘 [Official]  
How `D3-groups-and-grants` grants roles, why group grants depend on `DS-1-group-licence`, and why only a group's direct members receive its roles.

**[ASP.NET Core Blazor WebAssembly with Microsoft Entra ID groups and roles](https://learn.microsoft.com/aspnet/core/blazor/security/webassembly/microsoft-entra-id-groups-and-roles?view=aspnetcore-10.0)** 📘 [Official]  
How to give a principal a second role in the portal—add it again and select the other role—which the negative branch of `DS-1-group-licence` relies on.

**[appRoleAssignment resource type](https://learn.microsoft.com/graph/api/resources/approleassignment?view=graph-rest-1.0)** 📘 [Official]  
One assignment per principal and role, and a group's roles reaching its direct members, which `D3-groups-and-grants` and `DS-1-group-licence` rely on.

**[Grant an appRoleAssignment for a service principal](https://learn.microsoft.com/graph/api/serviceprincipal-post-approleassignedto?view=graph-rest-1.0)** 📘 [Official]  
The `appRoleAssignedTo` request that the session rules send with `az rest` when `DS-1-group-licence` takes its negative branch.

**[az ad group member](https://learn.microsoft.com/cli/azure/ad/group/member?view=azure-cli-latest)** 📘 [Official]  
The commands that the session rules use to change memberships in `E7-validate-sign-in` and `WS-F-end-to-end-validation`.

**[Configure group claims and app roles in tokens](https://learn.microsoft.com/security/zero-trust/develop/configure-tokens-group-claims-app-roles)** 📘 [Official]  
The three `roles` claim cases that `E4-role-expansion` handles—no claim, no role, and several values—and the baseline-role guidance that `G2-provisioning-how-to` states.

**[ASP.NET Core Blazor authentication and authorization](https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-10.0)** 📘 [Official]  
The authentication state flow that `E3-auth-state-to-browser` wires.

**[Authentication and authorization in ASP.NET Core SignalR](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0)** 📘 [Official]  
Why a hub connection outlives its authentication by default, and the `CloseOnAuthenticationExpiration` option that `C3-push-per-group` sets and `DS-4-hub-expiry` probes.

<!--
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
  structure: {status: "not_run", last_run: null}
  facts: {status: "not_run", last_run: null}

article_metadata:
  filename: "01-roles-permissions-support.plan.md"
  created: "2026-10-27"
  last_updated: "2026-10-28"
  version: "1.6"
-->
