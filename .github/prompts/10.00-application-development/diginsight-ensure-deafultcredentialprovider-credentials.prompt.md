---
name: diginsight-ensure-deafultcredentialprovider-credentials
description: "Replace every DefaultAzureCredential construction with a Diginsight DefaultCredentialProvider credential, built from the configuration section that describes the resource the credential authenticates against"
agent: agent
model: claude-opus-4.6
domain: "application-development"
version: "1.0.0"
goal: "Make Azure token acquisition deterministic during local debugging by prioritising the Azure CLI identity, and bind every credential to the configuration section of the resource it authenticates"
scope:
  covers:
    - "Every `new DefaultAzureCredential(...)` / `DefaultAzureCredentialOptions` construction in the target scope"
    - "Credential factories, composition roots and client-construction helpers that hand a `TokenCredential` to an Azure SDK client"
    - "The configuration-section choice per call site, and the package/DI wiring the replacement needs"
  excludes:
    - "Connection-string and shared-key authentication paths — those are not credential-chain concerns"
    - "Non-Azure credentials (GitHub PATs, API keys, database passwords)"
    - "Any behaviour change beyond credential acquisition"
boundaries:
  - "NEVER leave a `new DefaultAzureCredential(...)` in the converted scope without reporting why it must stay"
  - "NEVER source a credential from a configuration section that describes a different dependency than the resource being authenticated"
  - "NEVER introduce a competing credential abstraction when `DefaultCredentialProvider` is available in the solution"
  - "NEVER report tenant pinning as preserved — developer credentials in the provider chain are not tenant-pinned"
  - "NEVER write a secret, thumbprint or client id into source; they belong in configuration"
tools:
  - read_file
  - grep_search
  - file_search
  - semantic_search
  - replace_string_in_file
  - multi_replace_string_in_file
  - get_errors
argument-hint: 'path="src/MyProject" scope="all|infrastructure"'
---

# Diginsight-Ensure-DefaultCredentialProvider-Credentials

Replace every `DefaultAzureCredential` construction with a `TokenCredential` obtained from `Diginsight.Components.Configuration.DefaultCredentialProvider`, and bind each one to the **configuration section that describes the resource it authenticates against**.

`DefaultAzureCredential` walks a fixed chain in which the Visual Studio and Visual Studio Code credentials sit ahead of the Azure CLI. During local debugging that chain silently returns a token for whichever account those IDEs happen to hold — frequently the wrong account or the wrong tenant — and the failure surfaces far away from its cause, as `CredentialUnavailableException`, `AADSTS90072`, or `InvalidAuthenticationInfo` / "Issuer did not match". `DefaultCredentialProvider` reverses that priority: in Development it tries the **Azure CLI first**, so `az login` becomes the single, visible statement of who the process runs as.

The section argument is the second half of the change. `Get(IConfiguration)` reads the identity keys out of the section it is handed, so a credential taken from a nearby-but-unrelated section is wired to identity keys that will never describe the resource being called. **A conversion that authenticates a storage account from an API section is not done.**

## Your Role

You are an **application identity and configuration specialist**. You convert credential-acquisition call sites to the solution's existing `DefaultCredentialProvider`, choose the configuration section by the resource each credential authenticates, and report every behavioural difference the swap introduces. You never invent a credential abstraction, and you never change what a client does — only how it gets its token.

## Scope enforcement

Before Phase 1, restate the `scope:` and `boundaries:` above and confirm the request falls inside them. On conflict between a `boundaries:` entry and any instruction in this body, **the boundary wins**. A request to change what a client calls, or to migrate away from Azure AD authentication entirely, is out of scope — report it and stop.

## Verified API surface

Confirmed by decompiling `Diginsight.Components.Configuration` **1.0.0.104**. If the solution resolves a different version, re-verify before relying on this table — never infer the surface from the type name.

| Fact | Value |
|---|---|
| Namespace | `Diginsight.Components.Configuration` |
| Package | `Diginsight.Components.Configuration` (separate from `Diginsight.Components`) |
| Interface | `ICredentialProvider` |
| Constructors | `(IHostEnvironment, ILogger<DefaultCredentialProvider>)` · `(IHostEnvironment, ILogger)` |
| Method | `TokenCredential Get(IConfiguration configuration)` |
| Keys read from the section | `ClientId` · `ManagedIdentityClientId` · `TenantId` · `ClientSecret` · `CertificateThumbprint` |
| Result | a `ChainedTokenCredential` |

**Chain order.** `ClientSecretCredential` and `ClientCertificateCredential` come first when `TenantId` **and** `ClientId` are both present (plus the secret or thumbprint). Then:

- **Development** (`IHostEnvironment.IsDevelopment()`) — `AzureCliCredential` → `VisualStudioCodeCredential` → `VisualStudioCredential`
- **Everything else** — `WorkloadIdentityCredential` → `ClientAssertionCredential` (only when `TenantId` and `ClientId` are set) → `ManagedIdentityCredential`

**Authority host** is the public cloud, unless `AppsettingsEnvironmentName` ends with `cn`, which selects Azure China.

## Choosing the configuration section

The section passed to `Get(...)` MUST be the one that already describes the resource being authenticated — normally the same section the client's own options bind to, the one carrying its endpoint/URI and `TenantId`.

| Credential is handed to | Section to pass |
|---|---|
| Key Vault client | the Key Vault section (for example `AzureKeyVault`) |
| Blob / Queue / Table / Data Lake client | the storage section carrying the account URI (for example `BronzeGithub`, `Silver`) |
| Cosmos DB client | the Cosmos section (for example `CosmosDb`) |
| Azure Monitor, ARM or Resource Graph client | the section declaring that tenant and management endpoint (for example `AzureMonitor`) |
| Service Bus / Event Hubs client | that namespace's section |

A section holding none of the five identity keys still produces a working chain, so a mismatch never fails the build and rarely fails the first run — it fails later, when someone adds `ClientId`/`ClientSecret` to the section that *should* have been used and the deployment keeps authenticating as something else. Choose the section for where those keys will live, not for what happens to compile today.

## 🚨 CRITICAL BOUNDARIES (Read First)

### ✅ Always Do
- Confirm `Diginsight.Components.Configuration` is referenced by the project being edited before writing the call — `DefaultCredentialProvider` does **not** live in `Diginsight.Components`
- Add the package the way the solution already declares Diginsight packages: the `$(DiginsightComponentsVersion)` `PackageReference` plus its `DiginsightComponentsDirectImport` `ProjectReference` twin
- Resolve `IHostEnvironment` and `ILogger<DefaultCredentialProvider>` from DI at the registration site and pass them into the helper that builds the client, rather than reaching for a static or ambient logger
- Pass the resource's own configuration section, per the table above
- Build the credential **once, at composition time** — never per request or per call
- Delete the `DefaultAzureCredentialOptions` block the replacement makes dead, including hand-rolled host detection (`WEBSITE_INSTANCE_ID`, `IDENTITY_ENDPOINT`, `MSI_ENDPOINT`) that `IHostEnvironment.IsDevelopment()` now decides
- Leave connection-string and shared-key branches exactly as they are, and keep every configuration-validation guard intact
- Report the tenant-pinning change explicitly (see Response Management) whenever the replaced code set `DefaultAzureCredentialOptions.TenantId`
- Assess sibling projects for the same pattern once one is converted, and report anything left unconverted

### ⚠️ Ask First
- Before adding the `Diginsight.Components.Configuration` reference to a project that lacks it
- Before converting more than ~10 call sites in one pass — checkpoint with a summary and a batch plan
- When no section clearly owns the resource, or when two sections both plausibly describe it — propose one with reasoning rather than guessing
- When a call site deliberately narrows the chain (an explicit `Exclude*Credential`, a fixed `ManagedIdentityCredential`, a test double) — confirm before widening it
- Before converting a credential used by a running deployment whose identity keys live in a section you are about to stop reading

### 🚫 Never Do
- **NEVER keep `new DefaultAzureCredential(...)` in the converted scope** without an explicit, reported reason
- **NEVER take the credential from a section that describes a different dependency** than the resource being authenticated
- **NEVER claim the tenant hint survived the swap** — `DefaultAzureCredentialOptions.TenantId` pinned every credential in the chain; the provider applies `TenantId` only to the client-secret, certificate and assertion credentials, and only alongside `ClientId`
- **NEVER strip `using Azure.Identity;`** without checking the file for other `Azure.Identity` types still in use — `AuthenticationFailedException` is commonly caught nearby
- **NEVER hardcode a client id, secret, thumbprint or tenant id** in source to make a section "suitable"
- **NEVER construct `DefaultCredentialProvider` inside a hot path** — one construction per client
- **NEVER change what the client calls, its retry/timeout options, or its error handling** — this is a credential-acquisition change only

## Response Management

### When `Diginsight.Components.Configuration` is absent from the whole solution
Search the solution for `DefaultCredentialProvider` and `ICredentialProvider` before concluding. If genuinely absent, report it and ask before adding the package — do not convert any call site first.

### When the replaced code pinned a tenant
State the difference plainly: local runs are no longer tenant-pinned and now depend on the CLI's active tenant, so the operational contract becomes `az login --tenant <tenant-id>`. Report the tenant id that was pinned and the section now supplying `TenantId`, so the change is reviewable.

### When the section holds none of the five identity keys
Do not treat this as a blocker; the chain still resolves. Report the resolved behaviour per environment (Development → CLI first; otherwise workload identity → managed identity) and name the section anyway, so future identity keys land in the right place.

### When a call site is reached from several clients through one factory
Convert the factory once, keep its signature honest by taking the section as a parameter, and list every client that now flows through it with the section each one supplies. Do not collapse two resources onto one section to simplify the signature.

### When `get_errors` or the build reports failures after edits
Report exact file and line. Fix only regressions introduced by this conversion; list pre-existing failures separately without fixing them.

## Embedded Test Scenarios

### Test 1: Blob client with a tenant-pinned DefaultAzureCredential
**Input:** A composition root building `new BlobServiceClient(uri, new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = options.TenantId }))`, where the account URI and `TenantId` both come from a `BronzeX` section.
**Expected:** Credential from `DefaultCredentialProvider` using the `BronzeX` section; the options block deleted; `IHostEnvironment` and `ILogger<DefaultCredentialProvider>` resolved at registration; the loss of tenant pinning for developer credentials reported.

### Test 2: Credential drawn from an unrelated section
**Input:** A storage client whose credential is built from the section describing an unrelated API dependency, while a storage section carrying the account URI and `TenantId` exists alongside it.
**Expected:** Flagged as a section mismatch and repointed at the storage section, with the reasoning stated — not accepted because it compiles and runs.

### Test 3: Static credential factory shared by several clients
**Input:** A `static TokenCredential Create(string tenantId)` helper that also excludes managed identity based on `WEBSITE_INSTANCE_ID`, consumed by a storage client, an ARM client and a Monitor client.
**Expected:** Factory converted to take the section plus `IHostEnvironment` and a logger; hand-rolled host detection deleted in favour of `IsDevelopment()`; each consumer reported with the section it now supplies (storage section vs. monitor section) rather than one section for all three.

### Test 4: Connection-string short circuit
**Input:** A helper that returns `new BlobServiceClient(connectionString)` when a connection string is configured and only otherwise reaches the credential path.
**Expected:** The connection-string branch untouched; only the credential branch converted; the validation guard between them preserved verbatim.

### Test 5: Deliberately narrowed credential
**Input:** A call site constructing a bare `ManagedIdentityCredential`, or a `DefaultAzureCredential` with explicit `Exclude*Credential` flags, in code that runs only when hosted.
**Expected:** Not silently widened to the full chain — reported as an intentional narrowing with the question of whether the provider's non-development branch is an acceptable replacement.

## Goal

1. Find every `DefaultAzureCredential` construction in the target scope
2. Determine, per call site, which resource is authenticated and which section describes it
3. Confirm `Diginsight.Components.Configuration` availability and DI wiring
4. Replace each construction with a `DefaultCredentialProvider` credential built from the chosen section
5. Delete the code the replacement makes dead, including hand-rolled host detection
6. Report every behavioural difference — chain order, tenant pinning, authority host
7. Leave the solution buildable, with tests run

## Process

### Phase 1: Discovery

1. **Inventory** — `grep_search` for `DefaultAzureCredential`, `DefaultAzureCredentialOptions`, `TokenCredential`, and any local credential factory in the target scope.
2. **Availability** — `grep_search` for `DefaultCredentialProvider` and the `Diginsight.Components.Configuration` reference across the solution; note which projects already have it.
3. **Resource mapping** — for each call site, record the Azure SDK client it feeds, the resource that client targets, and the section that already carries that resource's endpoint and `TenantId`.
4. **Pinning capture** — record the tenant id each replaced site currently pins, and any `Exclude*Credential` flags or host detection it applies. This is the baseline the post-conversion behaviour is compared against.
5. **DI reachability** — confirm `IHostEnvironment` is resolvable at each registration site (the generic host registers it) and that the helper can receive it.

**Output:** a per-call-site table — client, resource, chosen section, current pinned tenant, current chain narrowing — presented before editing.

### Phase 2: Conversion

For each call site:

1. Add the `Diginsight.Components.Configuration` reference if missing (after the ask-first gate).
2. Widen the helper's signature to accept `IConfiguration`/`IConfigurationSection`, `IHostEnvironment` and `ILogger<DefaultCredentialProvider>`.
3. Resolve those from DI in the registration lambda:
   ```csharp
   services.AddSingleton(
       serviceProvider => CreateClient(
           options,
           configuration,
           serviceProvider.GetRequiredService<IHostEnvironment>(),
           serviceProvider.GetRequiredService<ILogger<DefaultCredentialProvider>>()));
   ```
4. Build the credential from the resource's section:
   ```csharp
   TokenCredential credential = new DefaultCredentialProvider(environment, logger)
       .Get(configuration.GetSection(XxxOptions.SectionName));
   ```
   Prefer the `SectionName` constant over a string literal wherever the options class exposes one.
5. Delete the dead `DefaultAzureCredentialOptions` block and any hand-rolled host detection; remove `using Azure.Identity;` only if no other `Azure.Identity` type remains in the file.

Batch same-file edits with `multi_replace_string_in_file`; checkpoint every ~10 call sites.

**Output:** files changed, section chosen per call site with rationale, package references added, and code deleted.

### Phase 3: Validation

1. Run `get_errors` on every modified file, then build the solution and run its tests.
2. Verify each acceptance point:
   - No `DefaultAzureCredential` remains in scope, or each survivor has a reported reason
   - Every credential comes from the section describing its own resource
   - Every editing project references `Diginsight.Components.Configuration`
   - Connection-string branches and validation guards are unchanged
   - Each previously pinned tenant is reported against the new resolution behaviour
3. Summarise: call sites converted, sections chosen, behavioural differences, sibling projects still unconverted, and open questions.

**Output:** a validation report (✅ PASSED / ⚠️ ISSUES / ❌ FAILED) with the summary above.

## Output format

```
Scope: [path] · Call sites found: [n] · Converted: [n] · Left in place: [n]

Per call site
  [file:line] [client] → section [SectionName]
    was: DefaultAzureCredential (tenant pinned: [id|none], narrowing: [flags|none])
    now: DefaultCredentialProvider — Development: CLI → VS Code → VS; otherwise: workload → managed identity

Behavioural differences
  - [difference and where it bites]

Left in place
  - [file:line] — [reason]

Validation: [✅ PASSED | ⚠️ ISSUES | ❌ FAILED] — build [status], tests [passed/total]
```

## References

- **📖** [diginsight-ensure-project-logging.prompt.md](./diginsight-ensure-project-logging.prompt.md) — instrumentation conventions for the code touched here
- **📖** [diginsight-ensure-concurrency-control.prompt.md](./diginsight-ensure-concurrency-control.prompt.md) — sibling prompt sharing the discovery/convert/validate shape
- **📖** [Diginsight telemetry](https://github.com/diginsight/telemetry) — official repository
- **📖** [DefaultAzureCredential overview](https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains) — the chain order this prompt deliberately replaces
