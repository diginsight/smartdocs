---
title: "How SmartDocs authenticates readers and authorizes spaces"
date: "2026-11-10"
type: explanation
description: "How Entra ID app roles, per-space metadata policies, and deployment settings combine to decide what each SmartDocs reader can see."
---

# How SmartDocs authenticates readers and authorizes spaces

## 🎯 Introduction

SmartDocs decides what a reader can see from three independent inputs, each owned by a different party:

- **Who is reading.** Microsoft Entra ID signs the reader in. The token's `roles` claim lists the app roles the reader holds on the deployment's app registration.
- **What a role means in a space.** Each space's content-root `metadata.yml` declares the space's permissions, which of them a reader needs, and what each role value grants there.
- **How strict the deployment is.** Configuration turns sign-in on, adds floors that only a signed-in reader satisfies, and decides whether a space must carry a policy at all.

The rule that combines them is short: **a reader sees a space when the permissions they hold in that space cover everything the space requires.** Pages, raw files, navigation, the article index, counts, and live updates all follow that one verdict, computed once per request.

This explanation covers how you define roles and permissions, and how authentication and authorization work at runtime. It describes the current implementation, where policies live in the unified `metadata.yml` and the legacy `access.metadata.yml` is still read during migration. The [migration analysis](03-smartdocs-rebac-metadata-fix.analysis.md) applies it to the configured repositories.

### Is this relationship-based access control?

This work item's file names say ReBAC, but SmartDocs implements **role-based access control (RBAC) with bindings scoped to each space**. Two relationships decide access: Entra assigns a reader to app roles, and each space's policy binds role values to that space's own permissions. The same role value can therefore mean different things in different spaces.

Relationship-based systems in the style of Google's Zanzibar go further. They store relation tuples such as *reader R is a viewer of folder F* and evaluate them as a graph, including inheritance from parent objects. SmartDocs stores no per-reader relationship to any document, and no folder inherits a policy from its parent. Folder-level requirements are explicitly deferred work, recorded in the [metadata refactoring plan](01-metadata-refactry.plan.md#️-park-lot).

## 📚 Table of contents

- [🎯 Introduction](#-introduction)
- [🧩 The model at a glance](#-the-model-at-a-glance)
- [🪪 Defining roles in Entra ID](#-defining-roles-in-entra-id)
- [🔐 Defining permissions in metadata.yml](#-defining-permissions-in-metadatayml)
- [⚙️ Deployment controls](#️-deployment-controls)
- [🔑 How authentication works](#-how-authentication-works)
- [🛡️ How authorization works](#️-how-authorization-works)
- [🧮 A worked example](#-a-worked-example)
- [⏱️ Freshness, caching, and revocation](#️-freshness-caching-and-revocation)
- [🚧 Failure behavior and limits](#-failure-behavior-and-limits)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🧩 The model at a glance

The three layers meet in one comparison per space:

```text
 Entra ID (per deployment)          Space policy (content-root metadata.yml)       Deployment configuration
 ─────────────────────────          ──────────────────────────────────────────     ────────────────────────
 app registration: app roles        access.permissions  names this space declares   AzureAd:ClientId  sign-in on/off
 enterprise app:  assignments       access.requires     all-of requirement          DefaultRequirement  site default
            │                       access.roles        role value -> permissions   Spaces[].Access     space floor
            │ roles claim                    │                                       Spaces[].AccessFileRequired
            ▼                                ▼                                               │
 held(space) = { authenticated if signed in } ∪ grants of each role in this space            │
 required(space) = site default ∪ space floor ∪ access.requires  ◄───────────────────────────┘
 visible(space)  ⇔  held(space) ⊇ required(space)
```

The terms below keep the layers apart. Mixing them up is the most common source of confusion.

| Term | Owner | Meaning |
|---|---|---|
| App role | Entra app registration | A named role whose **value** (for example `Reader`) appears in the token's `roles` claim |
| Assignment | Entra enterprise application | Grants an app role to a user or group |
| Permission | Space policy | A name the space declares, such as `Read`; it means nothing outside that space |
| Requirement | Space policy and configuration | The permissions a reader needs, all of them |
| Grant | Space policy | The permissions one role value confers in that space |
| `authenticated` | SmartDocs | A built-in permission every signed-in reader holds in every space |
| Floor | Deployment configuration | A requirement added on top of the policy; only `authenticated` is accepted |

## 🪪 Defining roles in Entra ID

Roles are defined once per deployment, on the reader app registration that the deployment's `AzureAd:ClientId` names. Use a separate registration for each deployment, and don't assume a role assigned on one application applies to another.

1. **Create the app roles.** On the registration's **App roles** page, add each role with **Users/Groups** as the allowed member type. The role's **Value** is the contract with every space policy, so spell it exactly as the policies map it. SmartDocs compares role values case-insensitively, but keep one spelling. Display names and descriptions are for administrators.
2. **Assign the roles.** On the matching **Enterprise application**, assign each role to users or to security groups. Group-based assignment requires Entra ID P1 or P2, and it doesn't cascade to nested groups: add readers as direct members of the assigned group.
3. **Decide who can sign in.** With **Assignment required** set to **No**, any account the registration accepts can sign in. Such a reader holds `authenticated` and no roles. With **Yes**, only assigned users and group members receive tokens, so you must assign everyone who may sign in, including readers who need no role.
4. **Give several roles where needed.** A reader can hold any number of roles. SmartDocs unions their grants inside each space; no role has precedence over another.

A role means nothing until a space maps it. A role value that no space with a valid policy maps grants nothing, and the host logs a warning once per value. No setting grants every space at once, either: to make a role site-wide, map it in every space's policy.

## 🔐 Defining permissions in metadata.yml

### Placement and envelope

A space's policy is the `access` mapping in the `metadata.yml` at the space's **physical content root**. That's the folder the space's source points at, such as a repository's `src/docs` or a container's root. It isn't necessarily the repository root.

- The document must declare `schema: smartdocs-metadata/v1`. Without it, an `access` mapping makes the file invalid.
- Save the file as UTF-8 **without** a byte-order mark, no larger than 65,536 bytes. The current versioned parser treats a leading byte-order mark as part of the first key, so it doesn't recognize the `schema` line.
- An `access` mapping in a folder below the content root is rejected. It never protects that folder.

Keep any navigation fields, such as `label` or `order`, in the same file. The [unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md) lists every field.

### Fields and rules

| Field | Shape | Meaning |
|---|---|---|
| `permissions` | Mapping of name to description | Declares the names that `requires` and `roles` may use; descriptions grant nothing |
| `requires` | Sequence of names | Every listed permission is needed; may include the built-in `authenticated` |
| `roles` | Mapping of role value to a sequence of names | What each role grants in this space; grants of all a reader's roles are unioned |

The validator is strict on purpose, because an invalid policy hides the space instead of opening it:

- Names start with an ASCII letter and continue with letters, digits, `.`, `-`, or `_`. Matching is case-insensitive, so `Read` and `read` are duplicates.
- `requires` and every role's grants may name only declared permissions. `authenticated` may be required, but it can't be declared or granted.
- Unknown keys, duplicate names, and empty entries make the whole file invalid.
- Permissions are scoped to their space. Two spaces that both declare `Read` declare two unrelated permissions.

### Policy patterns

An explicitly public space states that it requires nothing. `access: {}` is a valid, explicit policy:

```yaml
schema: smartdocs-metadata/v1
access: {}
```

A sign-in-only space requires the built-in permission and maps no roles:

```yaml
schema: smartdocs-metadata/v1
access:
  requires: [authenticated]
```

A role-gated space declares its permissions, requires one, and maps role values to grants. Here a reader needs `Read`; `Write` is only a declared name, because SmartDocs has no editing feature:

```yaml
schema: smartdocs-metadata/v1
label: Product documentation
access:
  permissions:
    Read: Read the product documentation
    Write: Contribute to the product documentation
  requires: [Read]
  roles:
    Reader: [Read]
    Contributor: [Read, Write]
    Admin: [Read, Write]
```

A space can also require two permissions that different roles grant. Only a reader holding both roles, or one role granting both, sees it:

```yaml
schema: smartdocs-metadata/v1
access:
  permissions:
    Docs.Read: Read the documentation
    Docs.Restricted: Read the restricted chapters
  requires: [Docs.Read, Docs.Restricted]
  roles:
    Reader: [Docs.Read]
    Auditor: [Docs.Restricted]
```

### The legacy access file

Before the unified format, the same policy lived in a flat `access.metadata.yml` at the content root, with `schema: 1` and top-level `permissions`, `requires`, and `roles`. The host still reads it while `Site:LegacyMetadataCompatibility` is `true`, the default, and logs a migration warning. When both files exist, their policies must be equivalent, descriptions included, or the space is hidden. The [metadata refactoring analysis](01-metadata-refactry.analysis.md#-compatibility-and-migration) explains the coexistence rules.

## ⚙️ Deployment controls

These settings live in the deployment's private configuration overlay, never in a repository's metadata:

| Setting | Default | Effect |
|---|---|---|
| `AzureAd:Instance`, `TenantId`, `ClientId` | Empty IDs | A nonempty `ClientId` turns sign-in on; empty keeps the host anonymous-only |
| `AzureAd:CallbackPath`, `SignedOutCallbackPath` | `/signin-oidc`, `/signout-callback-oidc` | Callback paths registered on the app registration's Web platform |
| `Site:Authorization:DefaultRequirement` | Empty | `authenticated` makes every node, including the space index, require sign-in |
| `Site:Authorization:SessionLifetime` | `08:00:00` | Fixed sign-in lifetime; bounds how long removed roles keep working |
| `Site:Spaces[].Access` | Empty | Space floor; `authenticated` admits only signed-in readers whatever the policy says |
| `Site:Spaces[].AccessFileRequired` | `false` | `true` hides the space unless a valid policy is present |
| `Site:LegacyMetadataCompatibility` | `true` | Reads legacy `access.metadata.yml` files during migration |
| `Diginsight:SmartCache:SmartDocs:Access` | `00:00:30` | Freshness of each content root's cached policy |

Floors and the site default only add requirements. A repository can't lift them, and the host refuses to start if either holds anything other than an empty value or `authenticated`. A floor can't name a repository permission, because that repository's own role mappings could then satisfy it.

`AccessFileRequired` matters when a policy is what protects a space. Without it, a deleted or unpublished policy silently makes the space public. With it, a missing, invalid, or unreadable policy hides the space from everyone. An explicit `access: {}` satisfies the setting, but a navigation-only `metadata.yml` doesn't.

## 🔑 How authentication works

Sign-in exists only when `AzureAd:ClientId` is set. Otherwise the host registers no authentication services, and every reader is anonymous.

1. **Challenge.** The shell shows **Sign in** and links to `GET /_auth/signin?returnUrl=…`. The endpoint accepts only a local return path, so it can't redirect off the site. It issues an OpenID Connect challenge through Microsoft Identity Web.
2. **Entra sign-in.** The reader authenticates with Entra ID, which returns an ID token to `/signin-oidc`. A registration without a client credential uses the ID-token response for the credential-free development setup. A production deployment should use a certificate credential and the authorization-code flow, as the [reader access provisioning guide](../../202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md) describes.
3. **Claims.** The host keeps the token's claim names: inbound claim mapping is off, the role claim type is `roles`, and the name claim type is `name`.
4. **Session.** The host issues an authentication cookie that is HTTP-only, secure-only, and `SameSite=Lax`. Its lifetime is `SessionLifetime`, without sliding renewal and independent of the ID token's lifetime.
5. **Sign-out.** **Sign out** posts to `POST /_auth/signout` with an antiforgery token. It clears the cookie and signs out of the OpenID Connect session.

Authentication only identifies the reader. No fallback authorization policy is registered, so anonymous reading keeps working, and every visibility decision belongs to the authorization step that follows.

## 🛡️ How authorization works

### One evaluation per request

Before anything serves content, a middleware evaluates the current reader once and stores the verdicts for the request:

1. **Role set.** The reader is anonymous unless an identity is authenticated. A signed-in reader's role set is every value in the `roles` claim, or in a role claim renamed by inbound mapping. Values are trimmed, deduplicated, and compared case-insensitively.
2. **Requirement per space.** For each configured space, the effective requirement is the site default, plus the space floor, plus the policy's `requires`. A missing policy contributes nothing unless the space requires one. A missing required policy, or an invalid or unreadable one, makes the space **unresolvable**: no reader can see it.
3. **Held permissions per space.** An anonymous reader holds nothing. A signed-in reader holds `authenticated` plus the union of what the space's own policy grants to each of their role values.
4. **Verdict.** The space is visible when the held permissions include every required one. Nodes outside every space, such as the generated space index, need only the site default.

The evaluation reads each policy through its cached snapshot and combines it with current configuration. It caches nothing derived from a principal.

### Every surface follows the verdict

| Surface | Enforcement |
|---|---|
| Rendered pages, prerendered or through `/_page` | The file actually rendered is checked; a hidden page answers as **not found** |
| Raw files through `/_content` | Checked before the read and before any entity tag, so a hidden file returns `404`, never `304`; restricted bytes are sent `private, no-store` |
| Navigation levels and the article index | Nodes from hidden spaces are dropped |
| Folder records and counts | A hidden folder answers like a folder that doesn't exist; totals cover visible spaces only |
| Site shell and space index | Lists only the spaces the reader sees |
| Live updates | Connections are grouped by role set and receive only visible changes; a connection closes when its session expires |
| `robots.txt` | Built for an anonymous reader, whoever asks |
| `metadata.yml` and legacy carriers | Never delivered as raw content |

A hidden space is indistinguishable from a space that doesn't exist. That's deliberate: a `403` response would confirm that something is there.

## 🧮 A worked example

Consider four spaces and five readers. Space A has no policy and no floor. Space B has the `authenticated` floor. Space C uses the role-gated pattern above. Space D requires `Docs.Read` and `Docs.Restricted`, granted to `Reader` and `Auditor` respectively.

| Reader | A: public | B: sign-in floor | C: requires `Read` | D: requires both |
|---|---|---|---|---|
| Anonymous | ✅ | ❌ | ❌ | ❌ |
| Signed in, no roles | ✅ | ✅ | ❌ | ❌ |
| `Reader` | ✅ | ✅ | ✅ | ❌ (`Docs.Read` only) |
| `Contributor` | ✅ | ✅ | ✅ | ❌ (D doesn't map it) |
| `Reader` and `Auditor` | ✅ | ✅ | ✅ | ✅ (union of both grants) |

If space C's policy became invalid, every reader would lose it, administrators included. If it were deleted, C would become public, unless the deployment set `AccessFileRequired: true`.

## ⏱️ Freshness, caching, and revocation

- **Policy changes.** Each content root's parsed policy is cached for 30 seconds by default. The same snapshot serves navigation and authorization, so a policy is read and parsed once per refresh. Publishing invalidates it immediately through `POST /_nav/invalidate`, which checks the `X-Invalidate-Key` header when `Site:InvalidateApiKey` is set. Set that key on every deployed host.
- **Role changes.** Roles are read at sign-in and kept in the cookie. A new or removed assignment takes effect at the reader's next sign-in, and an open session keeps its roles until `SessionLifetime` ends it. Entra itself can take a minute or more to reflect a change in new tokens.
- **Revocation.** Remove the assignment, the group membership, or the policy mapping. Disabling an app role doesn't remove existing assignments. Shorten `SessionLifetime` if stale roles must expire sooner.

## 🚧 Failure behavior and limits

Every failure closes rather than opens:

| Condition | Result |
|---|---|
| No policy, `AccessFileRequired: false` | Only the site default and floor apply |
| No policy, `AccessFileRequired: true` | Space hidden from everyone |
| Invalid or unreadable policy, or conflicting canonical and legacy policies | Space hidden from everyone; the host logs the problem |
| Role value mapped by no space | Grants nothing; logged once |
| Floor or site default other than empty or `authenticated` | Host refuses to start |

The model also has deliberate limits:

- **No folder-level policies.** Protection applies to a whole space. To restrict part of a repository, publish it as a separate space.
- **No editing rights.** A permission named `Write` is only a name; SmartDocs serves documentation read-only.
- **No per-document grants.** Readers relate to roles, and roles relate to spaces; nothing relates a reader to an individual page.
- **Not a storage control.** Storage containers must stay private, and any other site generator built from the same content must exclude `metadata.yml`, `access.metadata.yml`, and `repository.metadata.yml`. SmartDocs' delivery guard protects only its own serving path.

## 💡 Conclusion

Define roles once per deployment in Entra ID, define what they mean per space in the content-root `metadata.yml`, and use configuration only for floors and required-policy guarantees. The host combines the three into one per-request verdict, applies it to every surface, and fails closed when any input is missing or wrong.

Because permissions are scoped to their space, the same role can open one repository and not another, and no deployment setting can grant everything at once. That keeps each repository in control of its own policy, while the deployment keeps the final word on minimum requirements.

Previous: [Metadata refactoring plan](01-metadata-refactry.plan.md). Next: [Legacy access policies across configured repositories](03-smartdocs-rebac-metadata-fix.analysis.md).

## 📚 References

### Microsoft documentation

- **[Add app roles and get them from a token](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)** 📘 [Official]. Shows how to define app roles on a registration, assign users and groups, and receive role values in the `roles` claim. Use it when creating or changing a deployment's roles.
- **[Manage users and groups assignment to an application](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal)** 📘 [Official]. Covers assigning roles on the enterprise application, the P1/P2 requirement for group assignment, and the lack of nested-group cascading. Use it before choosing group-based grants.
- **[Restrict a Microsoft Entra app to a set of users](https://learn.microsoft.com/en-us/entra/identity-platform/howto-restrict-your-app-to-a-set-of-users)** 📘 [Official]. Explains the **Assignment required** setting and its effect on who can sign in. Use it to decide whether role-free readers may sign in.
- **[ID token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference)** 📘 [Official]. Lists the claims an ID token carries, including `roles` and `name`. Use it to interpret what the host reads at sign-in.
- **[Microsoft Identity Web documentation](https://learn.microsoft.com/en-us/entra/msidweb/)** 📘 [Official]. Documents the library the host uses for OpenID Connect sign-in and cookie sessions. Use it for credential and flow configuration.

### Access-control models

- **[Role-based access control](https://csrc.nist.gov/projects/role-based-access-control)** 📘 [Official]. NIST's archived project page for the RBAC model and its INCITS 359 standard, in which permissions are assigned to roles and roles to users. Use it as the reference definition of the model SmartDocs implements.
- **[Zanzibar: Google's consistent, global authorization system](https://research.google/pubs/zanzibar-googles-consistent-global-authorization-system/)** 📗 [Verified Community]. The USENIX ATC 2019 paper describing relation-tuple, graph-evaluated authorization. Use it to compare SmartDocs' space-scoped roles with relationship-based access control.

### Repository sources

- **[Sign-in wiring](../../../../Diginsight.SmartDocs.Web/Endpoints/AuthEndpoints.cs)** 📘 [Repository source]. Registers Microsoft Identity Web, the cookie settings, claim mapping, and the sign-in and sign-out endpoints.
- **[Role set](../../../../Diginsight.SmartDocs.Web/Authorization/RoleSet.cs)**, **[visibility evaluator](../../../../Diginsight.SmartDocs.Web/Authorization/SpaceVisibilityEvaluator.cs)**, and **[requirement resolver](../../../../Diginsight.SmartDocs.Web/Authorization/AccessRequirementResolver.cs)** 📘 [Repository source]. Define role extraction, per-space held permissions, and the effective requirement.
- **[Reader access](../../../../Diginsight.SmartDocs.Web/Authorization/ReaderAccess.cs)**, **[page resolver](../../../../Diginsight.SmartDocs.Web/Authorization/AuthorizingRenderedPageResolver.cs)**, and **[content endpoints](../../../../Diginsight.SmartDocs.Web/Endpoints/ContentEndpoints.cs)** 📘 [Repository source]. Apply the verdicts to navigation, pages, and raw files.
- **[Policy validation](../../../../Diginsight.SmartDocs.Web/Authorization/AccessFile.cs)**, **[metadata parser](../../../../Diginsight.SmartDocs.Web/Metadata/MetadataParser.cs)**, and **[metadata provider](../../../../Diginsight.SmartDocs.Web/Metadata/ContentMetadataProvider.cs)** 📘 [Repository source]. Define the policy rules, the versioned envelope, and canonical and legacy acquisition.
- **[Site options](../../../../Diginsight.SmartDocs.Web.Shared/Sites/SiteOptions.cs)** and **[base settings](../../../../Diginsight.SmartDocs.Web/appsettings.json)** 📘 [Repository source]. Define floors, the required-policy flag, the session lifetime, and the compatibility switch.

### Repository documentation

- **[Unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md)** 📘 [Repository documentation]. The complete field catalog, failure table, cache behavior, and migration and rollback procedure.
- **[Reader access provisioning guide](../../202610/20261027.01-smartdocs-authentication/02-reader-access-provisioning.md)** 📘 [Repository documentation]. Step-by-step registration, role, group, and overlay setup for one deployment, with publishing and revocation rules.
- **[Roles and permissions analysis](../../202610/20261027.01-smartdocs-authentication/01-roles-permissions-support.analysis.md)** 📘 [Repository documentation]. The original design reasoning and the alternatives considered, including permissions as app roles and group claims.

<!--
validations:
  structure: {status: "not_run", last_run: null}
  sources: {status: "reviewed", last_run: "2026-11-10", notes: "Behavior described from the current sign-in, authorization, and metadata sources; external references fetched on 2026-11-10."}
article_metadata:
  filename: "02-smartdocs-rebac-support.analysis.md"
  created: "2026-11-10"
  last_updated: "2026-11-10"
-->
