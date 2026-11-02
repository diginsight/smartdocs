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

Use this how-to to prepare reader access for one deployment. Microsoft Entra ID decides which app roles a reader holds. Each repository's `access.metadata.yml` decides what those roles grant in that repository's space. SmartDocs combines all of a reader's roles by union, separately in each space.

This is a provisioning procedure, not a deployment-completion record. **Don't enable restrictions in deployed overlays or publish access files to deployed spaces until every exit criterion of the [implementation plan](01-roles-permissions-support.plan.md) is met.** Session 6's execution-order waiver doesn't waive that rollout boundary.

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

For a deployed host, use a client certificate held in Key Vault and the authorization-code flow. Upload the certificate's public part to the reader registration, configure Microsoft Identity Web's supported `AzureAd:ClientCredentials` certificate description, and grant the host identity the required certificate/secret access. Keep the private key in Key Vault and verify rotation and access before rollout. The official certificate configuration guide in References defines the supported description fields.

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

### 6. Write each repository's access file

Place `access.metadata.yml` directly inside the space's published content root, beside its root index. Don't put it beside the repository metadata outside that content root or in a subfolder.

For example, a product space can declare:

```yaml
schema: 1
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

Use schema `1`. Unknown keys, case-insensitive duplicate names, undeclared permission references, and invalid names invalidate the file. Names start with a letter and continue with letters, digits, `.`, `-`, or `_`. The reserved `authenticated` may appear in `requires`, but not in `permissions` or a role's grant list. A nested access file doesn't restrict its folder in this version; SmartDocs ignores it and warns.

See the [access-file schema](../../../06.00-reference/07-access-file-schema.md) for field types, size limits and failure outcomes.

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

`AccessFileRequired: true` keeps a space hidden while its root access file is missing. Without it, a missing access file contributes no requirement. Invalid or unreadable access files fail closed in the implementation. Role maps belong in repository access files, not in `Site:Authorization`.

## 📦 Publish and operate safely

These six operating rules apply after rollout is authorized:

1. **Protect the first publish.** Publish a restricted space's access file with or before its first content, or set `AccessFileRequired` before publishing. Retain the required-file setting when a later deletion must not make the space public.
2. **Protect every publication of the same content.** Exclude `access.metadata.yml` from any other generator that builds that content root. For MkDocs, add the pattern below to `exclude_docs`. Restrict that other site to the same readers, or stop publishing the restricted space there; hiding the access file alone doesn't protect its pages.
3. **Use direct user membership.** App role assignments to groups don't cascade through nested groups. Adding a group inside a role group doesn't grant its users the app role.
4. **Separate shared caches by deployment.** When Redis is enabled, give every deployment its own `Diginsight:SmartCache:Redis:KeyPrefix`; don't reuse another deployment's namespace.
5. **Revoke actual grants.** Remove a user's group membership, direct app-role assignment, or repository mapping. Disabling an app role isn't a substitute for removing existing assignments. Membership changes need a new sign-in to update the cookie's roles; mapping changes take effect on subsequent server checks after invalidation or expiry of the access-file cache.
6. **Make site-wide grants explicit.** To give one role access everywhere, map that role in every repository's access file. No deployment setting grants a permission across repositories, and a grant in one space never opens another.

For MkDocs, merge this into its configuration without replacing other exclusions:

```yaml
exclude_docs: |
  access.metadata.yml
```

After an access-file change, call the deployment's `POST /_nav/invalidate`, with its configured invalidation credential. A path-scoped invalidation is appropriate for a changed subtree; a global invalidation refreshes the whole navigation view. This isn't an app-only permission grant: invalidation keeps its existing shared-secret mechanism.

## 🧪 Validate before rollout

Build the host and client together, run the access profile in a visible console, and validate in a visible browser. The owner completes sign-ins; don't capture credentials or put tenant-identifying screenshots in public evidence.

Check anonymous, signed-in/no-role, single-role, and multi-role readers against the expected spaces. Then complete the remaining session-5 scenarios: authorized versus anonymous push delivery, session expiry and revocation, response cache headers, byte-identical public-baseline comparison, unreadable/invalid/required-but-missing access files, and root-mount route divergence.

Record each actual run with screenshots under this work item's `_validation/` folder. A source-level control or a successful build isn't proof that an unexecuted browser scenario passes.

## 🔎 Development discovery outcomes

The plan's discovery items record what was learned locally:

| Item | Recorded outcome |
|---|---|
| `DS-1-group-licence` | The development tenant has P1/P2, so the four fixture roles were granted through four security groups. Other tenants without that licence must use direct user assignments. |
| `DS-2-client-credential` | The development registration accepted the credential-free ID-token flow. No client secret was created. This doesn't prove the deployed certificate/code flow. |
| `DS-3-test-accounts` | One account represented *none*, *reader*, *multi*, and contributor scenarios through membership changes and fresh sign-ins. |
| `DS-4-hub-expiry` | **Unresolved.** Sequence 04 stopped at CLI authentication preflight; cookie-authenticated hub expiry wasn't observed. Don't claim it works. |

The implementation configures `CloseOnAuthenticationExpiration`, but its effect on the cookie-authenticated hub still needs `F2-revocation`. If the connection remains open, the plan's defined negative branch removes expired connections from role-set groups before pushes, then reruns the scenario. That branch hasn't been selected or implemented without evidence.

## 🏁 Conclusion

Keep identity grants in Entra, permission composition in each repository, and deployment-specific restrictions in the private overlay. Use required access files and consistent restrictions on every publication to avoid accidental public access. Finish the outstanding end-to-end validation before enabling restrictions in deployed environments.

Previous: [Roles and permissions analysis](01-roles-permissions-support.analysis.md). Next: [Implementation plan and outstanding validation](01-roles-permissions-support.plan.md).

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
Defines the rollout boundary, discovery branches, session rules, and exit criteria. Its session-5 markers remain authoritative for work not yet validated.

**[Recorded anonymous validation](./_validation/20261028.01-validation-sequence.md)**, **[counts and push validation](./_validation/20261028.02-validation-sequence.md)**, **[sign-in validation](./_validation/20261028.03-validation-sequence.md)**, and **[blocked end-to-end preflight](./_validation/20261028.04-validation-sequence.md)** 📘 [Official repository evidence]  
These records distinguish passing local scenarios from the end-to-end scenarios that haven't run. They aren't evidence of a deployed rollout.

<!--
validations:
  grammar: {status: "not_run", last_run: null}
  readability: {status: "not_run", last_run: null}
  structure: {status: "pass", last_run: "2026-10-28"}
  facts: {status: "pass", last_run: "2026-10-28"}

article_metadata:
  filename: "02-reader-access-provisioning.md"
  created: "2026-10-28"
  last_updated: "2026-10-28"
  version: "1.0"
-->
