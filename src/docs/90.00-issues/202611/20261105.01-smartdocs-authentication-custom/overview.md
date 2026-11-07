---
title: "Apply authentication to a custom documentation profile"
date: "2026-11-05"
type: how-to
---

# Apply authentication to a custom documentation profile

## 🎯 Introduction

SmartDocs can reuse its existing Microsoft Entra ID sign-in implementation for another deployment through configuration. You need an existing app registration, its Application (client) ID, and access to the deployment's private overlay. Registration changes and user assignments require tenant administrator approval.

## ⚙️ Configure sign-in

Set `AzureAd:TenantId` and `AzureAd:ClientId` in the deployment overlay. Use the **Application (client) ID**, not the registration's directory object ID. Keep deployment identifiers in private configuration.

The existing implementation requests an ID token without a client secret. Configure the registration's **Web** platform with the host's HTTPS `/signin-oidc` and `/signout-callback-oidc` redirect URIs, and turn on ID-token issuance.

Set `Access: authenticated` on each space that requires sign-in. The generated index can remain public while withholding those spaces from anonymous readers.

For sign-in-only protection, put an `access` mapping in the space's content-root `metadata.yml`:

```yaml
schema: smartdocs-metadata/v1
access:
  requires: [authenticated]
```

Set `AccessFileRequired: true` on protected spaces. Leave public spaces' `Access` empty and `AccessFileRequired: false`, and remove any named permission requirements from their policies. An optional policy still contributes requirements when present.

Existing `access.metadata.yml` files remain supported while `Site:LegacyMetadataCompatibility` is enabled, which is the default. Inventory and convert each participating content root before switching that setting off; conflicting canonical and legacy policies fail closed.

For the complete schema, cache behavior, and migration and rollback procedure, see the [unified metadata contract](../../../06.00-reference/08-unified-metadata-contract.md).

## 🔐 Optional role authorization

Authentication identifies the reader; it doesn't establish permission to read a particular space. For role-gated documentation, define approved app roles and assign users or groups on the enterprise application.

Each space maps the exact role values to its own permissions in the content-root `metadata.yml` under `access`. Set `AccessFileRequired: true` on that space so a missing or invalid logical policy keeps it hidden. Don't copy assignments from another tenant. Skip role definitions and mappings when the policy requires sign-in only.

## 🧪 Validation outcome

**Correction:** The earlier all-space role policy was superseded. The current local profile protects two selected spaces with authentication alone and leaves the other two public. Production-parser checks confirmed anonymous readers cannot satisfy the protected requirements, while signed-in readers without roles can.

A rebuilt, visible-browser run confirmed public pages render anonymously, protected pages and raw assets are denied, and the index and navigation exclude protected spaces. Sign-in is offered, and the challenge requests an ID token for the verified client.

The callback-only Entra registration update was denied with insufficient privileges. An administrator must configure Web callback URLs and ID-token issuance; no app roles are needed for this policy. Successful sign-in, callbacks, sign-out, and authenticated browser access remain unverified.

These observations describe the earlier profile and legacy policy files; they don't establish that this profile has been migrated to canonical metadata or that its registration has since changed.

## 💡 Conclusion

Reuse the existing sign-in implementation rather than adding another authentication stack. Complete registration settings and validate a real signed-in session before treating the deployment as fully configured.

## 📚 References

- **[Configure a web application](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-web-app-sign-user-app-configuration)** 📘 [Official]. Covers server-side application registration settings. Use it to configure the Web platform and callback URLs.
- **[Add app roles and receive them in tokens](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)** 📘 [Official]. Explains role definitions and assignments. Use it when implementing per-space authorization.

Deployment identifiers and the browser evidence are preserved in the internal companion: `smartdocs.internal/src/docs/90.00-issues/202611/20261105.01-smartdocs-authentication-custom/overview.internal.md`.

<!--
validations:
  implementation: {status: "partial", last_run: "2026-11-05", notes: "Selective public/protected behavior and role-free policy checks passed; Entra callback update denied; authenticated browser scenarios remain unverified."}
article_metadata:
  filename: "overview.md"
-->