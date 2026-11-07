---
title: "Provision reader access for a SmartDocs deployment"
author: "Dario Airoldi"
date: "2026-10-28"
categories: [how-to, security, authentication, authorization, entra-id, app-roles]
description: "Configure one deployment's Entra registration, role grants, repository access files, and private overlay, with safe publishing and revocation rules."
diataxis: how-to
publish: false
---

# Provision reader access for a SmartDocs deployment

Use this how-to to prepare reader access for one deployment. Microsoft Entra ID decides which app roles a reader holds. The `access` section of each space's root `metadata.yml` decides what those roles grant in that space. SmartDocs combines all of a reader's roles by union, separately in each space.

This is a provisioning procedure, not a deployment-completion record. **Don't enable restrictions in deployed overlays or publish access files to deployed spaces until every exit criterion of the [implementation plan](01-roles-permissions-support.plan.md) is met.** Every criterion was met locally on 2026-11-02, as [sequence 04](_validation/20261102.04-validation-sequence.md) records. A deployment still validates its own sign-in flow before it enables restrictions, as [Validate before rollout](#-validate-before-rollout) describes.

## 📚 Table of contents

- [📋 Prerequisites](#-prerequisites)
- [⚙️ Provision one deployment](#-provision-one-deployment)
- [📦 Publish and operate safely](#-publish-and-operate-safely)
- [🧪 Validate before rollout](#-validate-before-rollout)
- [🔎 Development discovery outcomes](#-development-discovery-outcomes)
- [🏁 Conclusion](#-conclusion)
- [📚 References](#-references)

## 📋 Prerequisites

You'll need permission to register an application, define its app roles, and assign users or groups to its enterprise application. Group-based application assignment needs Entra ID P1 or P2. Without that licence, assign roles directly to users instead.

Prepare the deployment's HTTPS origin, the list of content roots and mount routes, and access to its private settings overlay. Record tenant, application, and group identifiers only in the private overlay and its internal alias registry. Never commit a client secret, private key, certificate payload, or token to either repository.

The resources below have different purposes; don't interchange their identities.

| Resource role | Purpose |
|---|---|
| Reader app registration | Defines sign-in settings and app role values for this deployment. |
| Reader enterprise application | Holds the user/group assignments to those roles. |
| Reader role groups | One security group per app role, containing its direct user members. |
| Host managed identity | Reads content storage and, when configured, the client certificate from Key Vault. |
| Publish workload identity | Uploads content; it isn't a reader role assignment. |

## ⚙️ Provision one deployment

### 1. Register the reader application

In the Entra admin center, create a single-tenant app registration for this deployment. Under **Authentication**, add a **Web** platform, not a single-page application platform. Register the HTTPS origin with `/signin-oidc` and `/signout-callback-oidc`; set the front-channel logout URL to that origin with `/signout-oidc`.

For the local access profile, the corresponding URLs are:

```text
https://localhost:7280/signin-oidc
https://localhost:7280/signout-callback-oidc
https://localhost:7280/signout-oidc
```

Keep the registration's tenant and client IDs in the private configuration. Use a separate registration for another deployment rather than assuming app roles granted to one application apply to another.

### 2. Choose the sign-in credential mode

For the validated local, credential-free mode, allow **ID tokens** under **Implicit grant and hybrid flows**. The observed request uses `response_type=id_token` and `response_mode=form_post`; it doesn't redeem an authorization code. Don't describe that development setup as a tested certificate-based code flow.

For a production deployment, use a client certificate held in Key Vault and the authorization-code flow. Upload the certificate's public part to the reader registration, configure Microsoft Identity Web's supported `AzureAd:ClientCredentials` certificate description, and grant the host identity the required certificate/secret access. Keep the private key in Key Vault and verify rotation and access before rollout. The official certificate configuration guide in References defines the supported description fields.

If a local code-redemption setup instead requires a temporary client secret, store it with .NET user-secrets for the host project, never in an overlay or this guide. A certificate-based deployed flow hasn't been validated by this work item's local ID-token run.

### 3. Define the app roles

Under **App roles**, create coarse roles with **Users/Groups** as their allowed member type. Set each role's **Value** to exactly the role value that repository access files will map. The development fixture uses `Reader`, `Contributor`, `Admin`, and `ProductB.Reader`; choose only the roles your deployment needs.

Role descriptions and display names help administrators, but the token's role value is what SmartDocs consumes. A reader may hold several roles. No role overrides another, and a role has no built-in permission composition in Entra.

### 4. Grant roles through groups or direct assignments

Create one security group per app role. Under the reader **Enterprise application** > **Users and groups**, assign each group its corresponding app role. Add users as direct members of the corresponding group. A reader who needs two roles joins both groups.

Without P1 or P2, assign each user the needed app roles directly on the enterprise application. Repeat the assignment for each role; don't replace a two-role scenario with a single combined role.

### 5. Set the assignment policy

In the enterprise application's **Properties**, keep **Assignment required** set to **No** if a signed-in reader with no mapped role should be allowed. This is how the development fixture permits its *none* reader to see a space whose only requirement is `authenticated`.

If you set **Assignment required** to **Yes**, explicitly assign everyone allowed to sign in, including readers who only need a baseline role. Define that baseline role on the registration; map it in repository files only where it should grant a permission. This setting governs sign-in admission, not SmartDocs' space requirements, and anonymous public reading remains a separate application behavior.

### 6. Write each space's access policy

Place the `access` mapping in `metadata.yml` directly inside the space's published content root, beside its root index. Keep any existing navigation fields in the same file. If the repository root and content root differ, the repository classification remains at the repository root; the space policy belongs at the content root.

For example, a product space can declare:

```yaml
schema: smartdocs-metadata/v1
access:
  permissions:
    ProductA.Read: Read the product A documentation
    ProductA.Write: Describe the product A contributor capability
  requires: [ProductA.Read]
  roles:
    Reader: [ProductA.Read]
    Contributor: [ProductA.Read, ProductA.Write]
    Admin: [ProductA.Read, ProductA.Write]
```

`requires` is an all-of requirement. Permission names belong to this space only, even when another repository uses the same spelling. `ProductA.Write` is just a declared permission name: it doesn't create an editing API or grant access to management endpoints.

Use schema `smartdocs-metadata/v1`. Unknown versioned keys, case-insensitive duplicate names, undeclared permission references, and invalid names invalidate the file. Names start with an ASCII letter and continue with ASCII letters, digits, `.`, `-`, or `_`. The reserved `authenticated` may appear in `requires`, but not in `permissions` or a role's grant list. Folder-level access policies aren't supported; staged-tree validation rejects a nested `access` section.

Existing `access.metadata.yml` policies remain supported while `Site:LegacyMetadataCompatibility` is enabled, which is the default. Convert every participating root before disabling compatibility. Equivalent canonical and legacy policies are accepted with a warning; conflicting or invalid selected policies fail closed.

See the [unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md) for field types, size limits, failure outcomes, and migration and rollback steps.

### 7. Configure the private overlay

Merge the following settings into the deployment's existing configuration. The example is a shape, not a complete deployable overlay: retain every space's actual source settings and fill identity placeholders only in private configuration.

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<reader-tenant-id>",
    "ClientId": "<reader-application-client-id>",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "Site": {
    "Authorization": {
      "DefaultRequirement": "",
      "SessionLifetime": "08:00:00"
    },
    "Spaces": [
      {
        "Id": "product-a",
        "RouteBase": "/product-a",
        "Access": "",
        "AccessFileRequired": true
      }
    ]
  }
}
```

A nonempty `AzureAd:ClientId` enables sign-in. `DefaultRequirement` and the per-space `Access` floor accept only empty or `authenticated`. Set the default to `authenticated` for a fully private site, or use a space floor to require sign-in for just that space. Each requirement adds to, rather than replaces, the access file's requirement.

`AccessFileRequired: true` keeps a space hidden while its logical root policy is missing. An explicit `access: {}` is a valid policy, while navigation-only metadata doesn't satisfy this setting. Without it, a missing policy contributes no requirement. Invalid or unreadable selected policies fail closed. Role maps belong in each space's access policy, not in `Site:Authorization`.

### Space colors (optional)

Set `DefaultTheme` on each space to give public and internal content different default palettes. The local `devlearn` overlay uses `cosmo` for `/learn` and `sandstone` for `/learn-internal`; `Site:Branding:DefaultTheme` supplies the site-index default. A space's theme ID must exist in the resolved `Site:Themes` catalog (the built-in catalog when no themes are declared), or startup fails with the space and invalid ID.

An explicit space default creates a separate browser preference, stored as `lh-theme:<space-id>`. The **About** menu still lets readers choose any offered theme, but a choice in one space doesn't recolor the other. **Reset theme** removes that space's saved choice and restores its configured default. Defaults apply during server rendering and when navigating between spaces.

Spaces without `DefaultTheme` keep the existing site-wide `lh-theme` preference and fall back to `Site:Branding:DefaultTheme`, then the first light theme. An old global choice isn't copied into spaces with explicit defaults. Theme colors are a visual cue, not an access control; authorization and navigation filtering remain unchanged.

### Blob-backed App Service deployments

Keep the routes, access floors, required-policy flags, and themes when replacing a filesystem source with Blob Storage. Set each space's `Source` to `Blob` and supply its own `Blob:AccountUri` and `Blob:ContainerName`; don't use a local `FileSystem:RootPath`. Place the internal repository's access file at the **container root**, not alongside the application's settings. Keep the containers private and grant the host identity read access; reader roles don't grant direct storage access.

App Service uses double underscores for nested settings. For example, `Site__Spaces__1__Blob__ContainerName`, `Site__Spaces__1__Access=authenticated`, and `Site__Spaces__1__AccessFileRequired=true` configure the second space. The old `Content__*` and `BlobProxy__*` settings don't configure this implementation's spaces. Keep live settings aligned with the private deployment overlay so a later publish doesn't restore stale configuration.

Verify the **deployed binary** enforces authorization before mounting protected storage. Configuration alone can't add missing code. Similarly, saved per-space theme defaults require a host and client release containing that feature. Register the cloud HTTPS sign-in and signed-out callback URLs; preserve localhost URLs when a test registration is deliberately shared. A registration has only one front-channel logout URL, so shared-origin sign-out needs its own validation.

[Sequence 07](_validation/20261102.07-validation-sequence.md) records the Testmc configuration-only update using the existing ID-token registration. Public reading, anonymous denial, and the provider prompt passed; real-account role acceptance, sign-out, and the pending per-space theme deployment weren't covered. This test setup isn't evidence of a production certificate/code-flow rollout.

## 📦 Publish and operate safely

These six operating rules apply after rollout is authorized:

1. **Protect the first publish.** Publish a restricted space's access file with or before its first content, or set `AccessFileRequired` before publishing. Retain the required-file setting when a later deletion must not make the space public.
2. **Protect every publication of the same content.** Exclude `metadata.yml`, `access.metadata.yml`, and `repository.metadata.yml` from any other generator that builds that content root. For MkDocs, add the patterns below to `exclude_docs`. Restrict that other site to the same readers, or stop publishing the restricted space there; hiding metadata alone doesn't protect its pages.
3. **Use direct user membership.** App role assignments to groups don't cascade through nested groups. Adding a group inside a role group doesn't grant its users the app role.
4. **Separate shared caches by deployment.** When Redis is enabled, give every deployment its own `Diginsight:SmartCache:Redis:KeyPrefix`; don't reuse another deployment's namespace.
5. **Revoke actual grants.** Remove a user's group membership, direct app-role assignment, or repository mapping. Disabling an app role isn't a substitute for removing existing assignments. Membership changes need a new sign-in to update the cookie's roles, and an open session keeps its roles until `Site:Authorization:SessionLifetime` ends it. Entra may also take a minute or more to reflect a membership change in new tokens: in the development tenant, an added role appeared about 75 seconds after the change, and a removal 38 seconds after. Mapping changes take effect on subsequent server checks after invalidation or expiry of the access-file cache.
6. **Make site-wide grants explicit.** To give one role access everywhere, map that role in every repository's access file. No deployment setting grants a permission across repositories, and a grant in one space never opens another.

For MkDocs, merge this into its configuration without replacing other exclusions:

```yaml
exclude_docs: |
  metadata.yml
  access.metadata.yml
  repository.metadata.yml
```

After an access-file change, call the deployment's `POST /_nav/invalidate`, with its configured invalidation credential. A path-scoped invalidation is appropriate for a changed subtree; a global invalidation refreshes the whole navigation view. This isn't an app-only permission grant: invalidation keeps its existing shared-secret mechanism. **Set `Site:InvalidateApiKey` on every deployed host.** The endpoint checks the `X-Invalidate-Key` header only when a key is configured, so a host without one lets anyone trigger an invalidation.

## 🧪 Validate before rollout

Build the host and client together, run the access profile in a visible console, and validate in a visible browser. The owner completes sign-ins; don't capture credentials or put tenant-identifying screenshots in public evidence.

Check anonymous, signed-in/no-role, single-role, and multi-role readers against the expected spaces. Then repeat the end-to-end scenarios that [sequence 04](_validation/20261102.04-validation-sequence.md) recorded locally: authorized versus anonymous push delivery, session expiry and revocation, response cache headers, byte-identical public-baseline comparison, unreadable/invalid/required-but-missing access files, and root-mount route divergence. For production, also confirm that sign-in completes through the certificate-based authorization-code flow, which no local run has exercised. On every deployed host, confirm that `POST /_nav/invalidate` refuses a request without the key.

Record each actual run with screenshots under this work item's `_validation/` folder. A source-level control or a successful build isn't proof that an unexecuted browser scenario passes.

## 🔎 Development discovery outcomes

The plan's discovery items record what was learned locally:

| Item | Recorded outcome |
|---|---|
| `DS-1-group-licence` | The development tenant has P1/P2, so the four fixture roles were granted through four security groups. Other tenants without that licence must use direct user assignments. |
| `DS-2-client-credential` | The development registration accepted the credential-free ID-token flow. No client secret was created. This doesn't prove the deployed certificate/code flow. |
| `DS-3-test-accounts` | One account represented *none*, *reader*, *multi*, and contributor scenarios through membership changes and fresh sign-ins. |
| `DS-4-hub-expiry` | **Resolved on the positive branch** on 2026-11-02. With a five-minute session lifetime, the server closed the cookie-authenticated hub connection when the session expired, and the client reconnected anonymously. |

The implementation configures `CloseOnAuthenticationExpiration`, and [sequence 04](_validation/20261102.04-validation-sequence.md) observed it close a cookie-authenticated hub connection at the end of its session. The plan's negative branch, which would remove expired connections from role-set groups before each push, wasn't needed.

## 🏁 Conclusion

Keep identity grants in Entra, permission composition in each repository, and deployment-specific restrictions in the private overlay. Use required access files and consistent restrictions on every publication to avoid accidental public access. Validate each deployment's own sign-in flow and invalidation key before enabling restrictions there.

Previous: [Roles and permissions analysis](01-roles-permissions-support.analysis.md). Next: [Implementation plan](01-roles-permissions-support.plan.md).

## 📚 References

**[Add app roles and receive them in a token](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)** 📘 [Official]  
Explains role definitions and app role claims. Use it when choosing role values and managing the registration's role definitions.

**[Manage users and groups assignment to an application](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal)** 📘 [Official]  
Documents enterprise-application assignments, P1/P2 licensing, and the lack of nested-group propagation. Use it to choose group grants or direct assignments.

**[Using certificates with Microsoft Identity Web](https://github.com/AzureAD/microsoft-identity-web/wiki/Certificates)** 📘 [Official]  
The Microsoft-maintained project documents certificate descriptions, including Key Vault-backed client credentials. Use it when preparing the deployed certificate/code-flow configuration.

**[MkDocs configuration: exclude_docs](https://www.mkdocs.org/user-guide/configuration/#exclude_docs)** 📘 [Official]  
Defines exclusion patterns for files that must not enter the built site. Excluding the access file doesn't replace authorization of the generated pages.

**[SmartDocs implementation plan](01-roles-permissions-support.plan.md)** 📘 [Official repository source]  
Defines the rollout boundary, discovery branches, session rules, and exit criteria, with the evidence that met each criterion. Its park lot holds the deployment work, `PL-7-deployed-registrations`.

**[Unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md)** 📘 [Repository documentation]
Defines the canonical policy, compatibility behavior, exposure boundaries, and migration procedure.

**[Recorded anonymous validation](./_validation/20261028.01-validation-sequence.md)**, **[counts and push validation](./_validation/20261028.02-validation-sequence.md)**, **[sign-in validation](./_validation/20261028.03-validation-sequence.md)**, **[blocked end-to-end preflight](./_validation/20261028.04-validation-sequence.md)**, and **[end-to-end validation](./_validation/20261102.04-validation-sequence.md)** 📘 [Official repository evidence]  
These records show which scenarios passed locally, and the preflight that first blocked the end-to-end run. They aren't evidence of a deployed rollout.

<!--
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
  structure: {status: "pass", last_run: "2026-10-28"}
  facts: {status: "pass", last_run: "2026-10-28"}
  metadata_contract: {status: "passed", last_run: "2026-11-07", notes: "Canonical policy validated by the production parser; MkDocs exclusion patterns checked at root and nested paths."}

article_metadata:
  filename: "02-reader-access-provisioning.md"
  created: "2026-10-28"
  last_updated: "2026-11-07"
  version: "1.1"
-->
