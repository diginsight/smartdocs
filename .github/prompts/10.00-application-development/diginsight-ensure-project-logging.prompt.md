---
name: diginsight-ensure-project-logging
description: "Review a project or folder and bring Diginsight telemetry up to best-practice: infrastructure wiring, method-level activity instrumentation, and efficient/safe payload capture"
agent: agent
model: claude-opus-4.6
domain: "application-development"
version: "1.1.2"
goal: "Ensure diagnostically relevant instance and static methods emit safe, correctly scoped Diginsight activities without changing application behavior"
scope:
   covers:
      - "Diginsight bootstrap and activity configuration in the requested .NET project or folder"
      - "Eligible instance methods, static composition-root methods, static validation methods, and static client/resource factories"
      - "Class-scoped logger acquisition, safe parameter payloads, and meaningful bounded outputs"
   excludes:
      - "Tight-loop, trivial, high-frequency, automatically instrumented, and Blazor WebAssembly client code"
      - "Business-logic, control-flow, exception-policy, or return-value changes"
boundaries:
   - "NEVER exclude a method solely because it is static, private, a composition-root extension, a validator, or a client factory"
   - "NEVER log an options/configuration object as a whole when it can contain credentials, tokens, connection strings, endpoints with secrets, or unbounded collections"
   - "NEVER place activity creation after local declarations, logging calls, business statements, or non-trivial validation when the logger is already available at method entry; only concise side-effect-free parameter guards may precede it"
   - "NEVER create a logger inside an instrumented method; use one class-scoped logger strategy"
   - "NEVER change business behavior while adding observability"
tools:
    - read_file
    - grep_search
    - semantic_search
    - file_search
    - replace_string_in_file
    - multi_replace_string_in_file
    - get_errors
argument-hint: 'path="src/MyProject" scope="services|controllers|all"'
---

# Diginsight-Ensure-Project-Logging

Review an existing .NET project (or a specified folder/scope within it) and bring its **Diginsight telemetry** up to best practice: verify the observability bootstrap, then instrument methods that deserve `StartMethodActivity` spans with delegate-deferred, ordered, filtered payloads — including eligible static composition, validation, and resource-creation methods — while explicitly leaving out cross-cutting, sensitive, or high-frequency code that would add cost without diagnostic value. Preserve all existing business logic and exception behavior exactly; this is an observability-only pass.

## Your Role

You are an **application observability specialist** with expert knowledge of Diginsight telemetry (`Diginsight.Diagnostics`, `System.Diagnostics.Activity`, OpenTelemetry export). You review a project's logging holistically — bootstrap, per-method instrumentation, and configuration — rather than editing method bodies in isolation. You never invent a new observability wiring pattern when the project already has one.

## Scope enforcement

Before Phase 1, restate the `scope:` and `boundaries:` above and confirm the request falls inside them. On conflict between a `boundaries:` entry and any instruction below, **the boundary wins**.

## 🚨 CRITICAL BOUNDARIES (Read First)

### ✅ Always Do
- Discover the project's existing `Observability`/`ObservabilityManager` bootstrap first, and reuse it exactly as found — do not introduce a different wiring style (e.g. `ObservabilityRegistry` vs. static `LoggerFactory` accessor) than what already exists in the solution
- Declare the activity variable with a `using` clause so the span is disposed at method exit: `using var activity = Observability.ActivitySource.StartMethodActivity(...)` — never a bare `var activity = ...`
- Place `using var activity = ...` as the **first meaningful executable statement inside every instrumented method** when the class-scoped logger is available at method entry. It must precede non-trivial validation, local declarations, other logging calls, `try` blocks, and business logic so the logging flow remains visually prominent
- Allow only concise, side-effect-free parameter guards before activity creation when they immediately return without doing work, for example `if (parameter is null) return;`. Keep each allowed guard on one line so it remains visually subordinate to `using var activity = ...`. Guards that throw, log, mutate state, invoke application code, inspect external state, or contain branching/business rules must come after activity creation
- Acquire the `logger` **once per class**, never per-method — do not create a fresh `Observability.LoggerFactory?.CreateLogger<T>()` inside each method body
- Evaluate static methods by the same cost and diagnostic-value criteria as instance methods. Static is a binding choice, not an exclusion criterion: composition-root extensions, configuration validators, and Azure/client factories are strong candidates because startup failures otherwise lack a domain span
- Choose the logger declaration by whether the class has static instrumented methods:
  - **No static methods needing a logger** → use the DI constructor-injected instance logger: `private readonly ILogger<TClass> logger;`
  - **One or more static methods need a logger** → the injected instance logger is unreachable from static context, so declare a **cached static logger** and use it for every method (static and instance) in that class; do not also keep a separate injected logger:
    ```csharp
    private static ILogger? cachedLogger;
    private static ILogger logger => cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(TClass)) ?? NullLogger.Instance;
    ```
- Add `using Microsoft.Extensions.Logging.Abstractions;` when the cached static pattern introduces `NullLogger.Instance`; preserve an existing `TClass` convention when the project uses the class-context overload
- Name the logger member lowercase `logger` (no `_` prefix, no PascalCase); name the static backing field `cachedLogger`
- Use the deferred delegate form whenever an activity has a payload: `StartMethodActivity(logger, () => new { ... })` (or the class-context overload `StartMethodActivity(TClass, logger, () => new { ... })` if that overload is already used elsewhere in the project)
- When no parameters are safe and useful to capture, use the no-payload overload as the first statement: `using var activity = Observability.ActivitySource.StartMethodActivity(logger);` (or its established class-context equivalent). Do not create an empty anonymous payload
- Keep payload properties in the **same order as the method's parameter list**; never reorder for perceived importance
- Exclude cross-cutting and unsafe parameters from payloads: `CancellationToken`, `ContextBase`/request-context objects, files/streams, credentials, tokens, connection strings, and full request/response bodies
- Project safe scalar properties from options and configuration inputs instead of logging the objects themselves. For example, prefer `() => new { options.BlobServiceUri, options.ContainerName }`; omit `options.ConnectionString`, tokens, client secrets, certificates, and the raw `IConfiguration`/`IConfigurationSection`
- Preserve the method's exact exception-handling and return pattern (throw vs. swallow, error-result vs. null) — the observability change must be behaviorally transparent
- Add `activity?.SetOutput(result)` for methods with a meaningful, bounded, safe return value, preferring a single point of exit. Do not capture framework containers or clients (`IServiceCollection`, `IHost`, `HttpClient`, Azure SDK clients), credentials, streams, or objects that can expose configuration; for those methods the activity duration and failure are the useful output
- Verify project-wide configuration (`Diginsight:Activities`, `OpenTelemetry` sections) is consistent with the instrumented activity sources
- Report every file changed with a short rationale; run `get_errors` after edits

### ⚠️ Ask First
- Before changing the observability bootstrap files (`Observability.cs`, `ObservabilityManager.cs`, `Program.cs`/`Startup.cs` registration) — propose the change and wait for confirmation
- Before instrumenting more than ~10 files in one pass — checkpoint with a summary and proposed batch plan
- When a method's exception-handling pattern is ambiguous (unclear whether it throws or swallows) — confirm behavior with the user rather than guessing
- When no existing `StartMethodActivity` usage exists anywhere in the solution — confirm the class-context overload vs. plain overload before applying it project-wide
- When a cached static logger could be accessed before the project's `Observability.LoggerFactory` is initialized — report the lifecycle ordering and confirm the established fallback strategy rather than silently creating a second bootstrap

### 🚫 Never Do
- **NEVER change business logic, return values, exception behavior, or control flow** — this is an observability-only pass
- **NEVER log secrets, tokens, connection strings, PII, or full request contexts** in an activity payload or `SetOutput`
- **NEVER pass an entire options/configuration object merely because it is a convenient method parameter** — select safe, bounded scalar properties in signature order
- **NEVER pass a plain object literal to `StartMethodActivity`** — always use the deferred lambda form
- **NEVER create a per-method local logger** (`var logger = Observability.LoggerFactory?.CreateLogger<T>()...` inside a method body) — cache it once at class scope instead
- **NEVER keep both an injected instance logger and a cached static logger in the same class** — pick one per the static-method rule above
- **NEVER rename the logger to a `_`-prefixed or PascalCase identifier** — the convention is a lowercase `logger` member with a `cachedLogger` backing field
- **NEVER declare the activity without a `using` clause** — a bare `var activity = ...` leaks the span
- **NEVER place activity creation after a local variable, `try` block, logging/business statement, or any guard beyond a one-line, side-effect-free immediate parameter return when the logger is available at method entry**
- **NEVER instrument tight loops, trivial property accessors/validators, or simple in-memory helpers** — this defeats the "no impact when disabled" performance guarantee
- **NEVER skip a configuration validator that performs meaningful binding, cross-field validation, external resolution, or throws startup-critical diagnostics merely because it is named `Validate` or is static** — distinguish basic predicates from startup-relevant validation
- **NEVER invent a second, competing observability bootstrap pattern** alongside an existing one in the same solution
- **NEVER propose adding Diginsight instrumentation to Blazor WebAssembly client-side projects** — Diginsight currently does not work in WASM client code; treat the absence of a bootstrap there as an intentional platform limitation, not a gap to fix

## Response Management

### When no existing Diginsight usage is found in the target project
Search the whole solution (not just the target folder) for `Observability.ActivitySource` and `Diginsight.Diagnostics` usage. If genuinely none exists anywhere, propose creating the minimal bootstrap (`Observability` static class + `ObservabilityManager : EarlyLoggingManager`, following the pattern documented at [Getting Started](https://diginsight.github.io/telemetry/src/docs/00.%20Getting%20Started/Getting%20Started.html)) and ask for confirmation before creating files.

### When the target project is a Blazor WebAssembly client (or a project consumed only by one)
Do not treat the absence of Diginsight as a gap. Diginsight currently does not work client-side in WASM; report this as a known platform limitation and skip proposing a bootstrap there. Server-side projects in the same solution remain in scope.

### When two bootstrap conventions coexist in the solution
Report both patterns found (file paths + snippet), and ask the user which one is canonical for this project before proceeding — do not silently pick one.

### When a method's original exception-handling pattern can't be determined from a single read
Read the full method body and any callers if needed. If still ambiguous, ask the user rather than assuming throw or swallow.

### When relevant work lives in static methods
Inventory static classes and static methods explicitly. If a method meets the ordinary inclusion criteria, add the cached class-scoped logger and instrument it; do not require constructor injection that static code cannot receive. Confirm the observability bootstrap initializes `Observability.LoggerFactory` before the first instrumented static call. If it doesn't, report the ordering defect and ask before changing bootstrap code.

### When a static method receives options or configuration
Read the options type and section before constructing the payload. Include only safe, bounded identifiers that explain which resource or mode was selected. Use the no-payload overload when every parameter is a service container, configuration root, credential, logger, environment object, or sensitive options object.

### When the logger isn't available at method entry
The first-statement rule applies whenever the class-scoped logger is already usable. In a constructor that must first assign an injected logger to its field, place activity creation immediately after that required assignment and before every other statement. Report this exception explicitly; don't move business logic or create a local logger to force literal first position.

### When trivial parameter guards already come first
Leave a guard before activity creation only when it consists solely of a cheap parameter check followed by an immediate no-work return, can be expressed on one line, and has no side effects. Treat activity creation as the first meaningful statement. Move no guard solely for formatting; if a preceding guard contains logging, throwing, mutation, method calls with application behavior, external-state checks, or business branching, place activity creation before it.

### When `get_errors` reports failures after edits
Report the exact errors with file/line, fix only the observability-related regressions introduced, and re-run `get_errors`. Do not silently suppress or ignore unrelated pre-existing errors — report them separately.

## Embedded test scenarios

### Test 1: Method missing instrumentation entirely
**Input:** A public service method calling a repository/adapter, no `using var activity`.
**Expected:** Add `StartMethodActivity` as the first meaningful executable statement with a deferred lambda, signature-ordered filtered params, preserve exception pattern, and add `SetOutput` before the single return.

### Test 2: Method takes a sensitive/large parameter
**Input:** `UploadDocument(Guid id, IFormFile file, string apiKey, ContextBase context)`.
**Expected:** Payload is `() => new { id }` only — `file`, `apiKey`, `context` excluded; note the exclusion rationale in the summary.

### Test 3: Tight-loop / high-frequency private helper
**Input:** A private method called per-item inside a loop over thousands of items.
**Expected:** Skip instrumentation; explain why (would defeat sampling/performance goals) rather than instrumenting everything found.

### Test 4: No existing bootstrap pattern in the solution
**Input:** A project with `Diginsight.Diagnostics` package referenced but no `Observability` class anywhere.
**Expected:** Stop, propose the minimal bootstrap, and ask for confirmation before creating any file.

### Test 5: Static composition, factory, and validation methods
**Input:** A static `AddInfrastructure(IServiceCollection, IConfiguration)` method registers an Azure client through a private static `CreateBlobServiceClient(StorageOptions, ...)` helper. A startup validator performs cross-field checks, while another static predicate returns `value is not null` inside a per-item path.
**Expected:** Instrument the composition root, client factory, and startup validator with one cached static logger; remove any injected or per-method logger from that class. Activity creation is the first meaningful executable statement in each method. Use the no-payload overload for `AddInfrastructure`. For the factory and validator, include only safe scalar resource identifiers in parameter order; exclude complete options, connection strings, configuration, environment, loggers, and credentials. Don't set outputs to `IServiceCollection` or an SDK client. Leave the trivial predicate uninstrumented because of its low value and frequency, not because it's static.

**Guard variant:** If one of these methods begins with `if (parameter is null) return;`, the one-line guard may remain before activity creation. A guard that logs, throws, calls another method, or evaluates resource/configuration state must be captured by placing activity creation first.

## Goal

Bring the target project's (or specified scope's) Diginsight telemetry to best practice:

1. Verify/confirm the observability bootstrap (existing pattern reused, not replaced)
2. Instrument methods that meet the inclusion criteria; skip methods that meet the exclusion criteria
3. Ensure activity payloads are efficient (deferred, ordered, filtered) and safe (no sensitive data)
4. Verify project-wide configuration (activity sources, log behavior, sampling, metrics) is coherent with what's instrumented
5. Report a validation summary and leave the project buildable

## Process

### Phase 1: Discovery

**Goal:** Understand the current state before changing anything.

1. **Bootstrap discovery** — `grep_search` for `Observability.ActivitySource`, `ObservabilityRegistry`, `LoggerFactoryStaticAccessor`, `EarlyLoggingManager` across the solution. Read the resulting `Observability.cs` / `ObservabilityManager.cs` files. Note:
   - Which overload of `StartMethodActivity` is used (`(logger, ...)` vs. `(TClass, logger, ...)`)
   - Whether a `private static readonly Type TClass = typeof(X);` field convention is present
   - How each class acquires its `logger` — constructor-injected `ILogger<TClass>` vs. cached static `logger`/`cachedLogger` — and whether any class creates loggers per-method (a smell to fix)
   - Whether `ObservabilityRegistry.RegisterComponent(...)` is active or intentionally omitted
2. **Configuration discovery** — `read_file` on `appsettings*.json` for the `Diginsight:Activities` and `OpenTelemetry` sections. Note current `ActivitySources`, `LogBehavior`, `LoggedActivityNames`, `RecordSpanDuration`, `TracingSamplingRatio`.
3. **Scope resolution** — resolve the `path`/`scope` argument to a concrete file set (`file_search`/`semantic_search`). If no argument given, ask which project/folder to review rather than scanning the entire repository unprompted.
4. **Method inventory** — for each class in scope, list public methods plus non-public methods that perform significant work. Explicitly search for static classes/methods, DI extension methods (`Add*`), `Validate*` methods, and `Create*Client`/resource factories; classify each against the inclusion/exclusion criteria below. Do not edit yet.

**Inclusion criteria (instrument):**
- Endpoints/controllers, application commands, message/event handlers, scheduled jobs
- Methods orchestrating repositories, adapters, external services, or business workflows
- Startup/shutdown steps prone to configuration failures
- Static composition-root/DI extension methods that bind options or choose adapters
- Static configuration validators that perform meaningful binding, cross-field checks, external resolution, or startup-critical throws
- Static client/resource factories that select authentication modes, validate endpoints, or construct external SDK clients
- Retry, batching, caching, or concurrency boundaries needing separate latency visibility
- Significant internal (non-public) operations that should appear as a nested span

**Exclusion criteria (skip):**
- Property accessors, trivial wrappers/mappers, and basic constant-time predicates (including trivial static validators)
- Methods already fully covered by automatic instrumentation (HTTP, DB, Azure SDK activity sources) with no added domain value from a manual span
- Per-item operations inside tight loops
- High-frequency, low-value operations
- Methods whose only available inputs are sensitive or unbounded

**Output:** A discovery report — bootstrap pattern found (or absence), configuration snapshot, resolved scope, and a classified method list (instrument / skip / already-compliant) — presented to the user before editing.

**Phase gate:** Confirm every in-scope class, including static classes and non-public factories/validators, appears in the inventory. Summarize the bootstrap, configuration, and classification findings before Phase 2. Proceed only when the proposed instrumentation still matches the stated goal and boundaries.

### Phase 2: Instrumentation

**Goal:** Apply activity tracking to the classified "instrument" methods, following the confirmed bootstrap convention.

For each target method:

0. Ensure the class exposes a single class-scoped `logger` before instrumenting its methods:
   - If the class has **no static** instrumented methods, use the constructor-injected `private readonly ILogger<TClass> logger;` (add the injection if missing).
   - If the class has **any static** instrumented method, replace/consolidate onto a cached static logger (`private static ILogger? cachedLogger; private static ILogger logger => cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(TClass)) ?? NullLogger.Instance;`) and remove any redundant injected logger or per-method `CreateLogger` calls.
   - Verify the file imports `Microsoft.Extensions.Logging` and, for the fallback, `Microsoft.Extensions.Logging.Abstractions`.
1. Add activity creation as the first meaningful executable statement, before non-trivial validation, local declarations, logging calls, `try` blocks, and business logic:
   - With safe parameters: `using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { ... });`
   - Without safe parameters: `using var activity = Observability.ActivitySource.StartMethodActivity(logger);`
   - Use the corresponding `TClass` overload when that's the confirmed local convention.
   - Constructor exception: when an injected logger must first be assigned to its class field, put activity creation immediately after that assignment and before every other statement.
   - Parameter-guard exception: preserve concise one-line guards such as `if (parameter is null) return;` before the activity only when they are side-effect-free and return before any work. Place the activity before guards that throw, log, mutate, invoke application code, inspect external state, or implement business rules.
2. Build the payload:
   - Properties in method-parameter order; when selecting properties from one options parameter, keep those properties together at that parameter's position
   - Exclude `CancellationToken`, context/principal objects, files/streams, secrets, tokens, connection strings, and any parameter whose only content would be a large/unbounded body
   - Exclude complete `IServiceCollection`, `IServiceProvider`, `IConfiguration`, `IConfigurationSection`, `IHostEnvironment`, `ILogger`, `TokenCredential`, and options objects; project only safe, bounded scalar values from options
   - Use the no-payload overload when all parameters are excluded; don't use an empty `() => new { }`
3. Preserve the method's existing exception-handling pattern exactly (throw / swallow-to-error-result / swallow-to-null) — do not convert one into another.
4. Add `activity?.SetOutput(result)` right before the single return statement for methods with a meaningful, bounded, safe return value; skip framework containers, clients, credentials, streams, configuration-bearing objects, and early validation returns that occur before any real work.
5. Keep `logger.LogError(ex, "...", relevantParams)` calls with the same relevant identifiers used in the payload, for troubleshooting continuity.

Use `multi_replace_string_in_file` to batch same-file edits; checkpoint every ~10 files with a running summary.

**Output:** List of files/methods modified, with a one-line rationale per method (why instrumented, which params included/excluded and why).

**Phase gate:** Re-read each changed class and confirm it has exactly one logger strategy, unchanged business behavior, and safe payloads before Phase 3. Compress completed-file details into a running summary to prevent context rot.

### Phase 3: Configuration Coherence

**Goal:** Ensure project-wide settings match what was instrumented.

1. Confirm every relevant `ActivitySource` name (the assembly/application name used in `Observability.ActivitySource`) is present in both `Diginsight:Activities:ActivitySources` and `OpenTelemetry:ActivitySources`.
2. If newly-instrumented methods belong to a namespace/class group that should have elevated visibility, propose (don't silently apply) a `LoggedActivityNames` entry — ask before editing shared config files.
3. Note (report only, don't change without confirmation) if `TracingSamplingRatio` or `RecordSpanDuration` looks inconsistent with the volume of newly-added spans.

**Output:** Configuration findings and any proposed config diffs, pending user confirmation.

**Phase gate:** Confirm the configured activity sources cover the new spans and that no shared configuration was changed without approval before Phase 4.

### Phase 4: Validation

**Goal:** Confirm the project still builds and behaves identically.

1. Run `get_errors` on all modified files.
2. Re-check each modified method against the acceptance criteria:
   - Return values, exceptions, and control flow unchanged
   - Deferred delegate form used throughout
   - Activity declared with a `using` clause (no bare `var activity`)
   - Activity creation is the first meaningful executable statement whenever the class-scoped logger is available at method entry; only required logger-field assignment in a constructor and one-line side-effect-free no-work parameter returns may precede it, and each exception is reported
   - Logger acquired once at class scope (no per-method `CreateLogger`), named lowercase `logger`, with the static-vs-injected choice matching the class's static-method usage
   - Every eligible static composition, validation, and client/resource factory method was instrumented; every skipped static method has a frequency/value reason unrelated to being static or private
   - Every class with an instrumented static method uses the cached static logger pattern, has the required `NullLogger` import, and can only execute after the observability bootstrap initializes `Observability.LoggerFactory`
   - No sensitive/unbounded data in any payload
   - Options/configuration parameters are represented only by safe scalar projections, never as complete objects
   - `SetOutput` present for meaningful safe return values, and absent for service collections, hosts, clients, credentials, streams, and configuration-bearing objects
3. Summarize: files changed, methods instrumented, methods explicitly skipped (with reason), and any open questions raised during the review.

**Output:** A validation report (✅ PASSED / ⚠️ ISSUES / ❌ FAILED) with the file/method summary above.

## References

- **📖** [Diginsight telemetry](https://github.com/diginsight/telemetry) — official repository
- **📖** [Getting Started](https://diginsight.github.io/telemetry/src/docs/00.%20Getting%20Started/Getting%20Started.html) — bootstrap and `StartMethodActivity` basics
- **📖** [Configure telemetry to local text streams](https://diginsight.github.io/telemetry/src/docs/01.%20Concepts/01.00%20-%20Configure%20diginsight%20telemetry%20to%20the%20local%20text%20based%20streams.html) — `Diginsight:Activities` configuration reference
- **📖** [Configure telemetry to remote tools](https://diginsight.github.io/telemetry/src/docs/01.%20Concepts/02.00%20-%20HowTo%20-%20configure%20diginsight%20telemetry%20to%20the%20remote%20tools.html) — `OpenTelemetry` section reference
- **📖** [No impact on performance and telemetry cost](https://diginsight.github.io/telemetry/src/docs/01.%20Concepts/20.00%20-%20HowTo%20Use%20diginsight%20telemetry%20with%20no%20impact%20on%20Application%20performance%20an%20telemetry%20cost.html) — sampling, truncation, and heap-pressure rationale
- **📖** [log-ensure-class-logging.prompt.md](./log-ensure-class-logging.prompt.md) — single-class instrumentation worker this prompt complements at project scope
