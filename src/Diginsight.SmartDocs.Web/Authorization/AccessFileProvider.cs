using System.Collections.Concurrent;
using Diginsight.Diagnostics;
using Diginsight.Runtime;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// Reads and caches each space's <c>access.metadata.yml</c>, a content fact like a page or a folder
/// listing.
/// <para>
/// The file is read from the space's physical source, below the mounted view and the content cache,
/// because the mounted view refuses to serve it. The parsed outcome is cached under
/// <c>ContentPathCacheKey("access", &lt;mount&gt;)</c>, so the invalidation that clears a space's
/// navigation clears its access file too, and for no longer than
/// <see cref="ContentFreshnessOptions.Access"/> otherwise.
/// </para>
/// <para>
/// Only <see cref="AccessFileStatus.Missing"/>, <see cref="AccessFileStatus.Valid"/> and
/// <see cref="AccessFileStatus.Invalid"/> are cached. A read that fails for another reason escapes
/// the cached call and is answered as <see cref="AccessFileStatus.Unreadable"/>, so the next check
/// reads the file again instead of trusting a storage fault for the cache's lifetime.
/// </para>
/// </summary>
public sealed class AccessFileProvider(
    SpaceContentRegistry spaces,
    ISmartCache smartCache,
    ContentFreshnessOptions freshness,
    ILogger<AccessFileProvider> logger)
{
    public const string CacheKind = "access";

    private readonly ConcurrentDictionary<string, string> reported = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The cache path of a space's access file: its route base without slashes, empty when root-mounted.</summary>
    public static string MountOf(SpaceOptions space) => space.NormalizedRouteBase.Trim('/');

    public async Task<AccessFileOutcome> GetAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { spaceId });

        SpaceContentAccess access = spaces.Get(spaceId);
        var key = new ContentPathCacheKey(CacheKind, MountOf(access.Space));
        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.Access,
        };

        AccessFileOutcome outcome;
        Exception? failure = null;
        try
        {
            AccessFileEnvelope envelope = await smartCache.GetAsync(
                key,
                ct => ReadAsync(access, ct),
                options,
                callerType: typeof(AccessFileProvider),
                cancellationToken: cancellationToken);
            outcome = envelope.ToOutcome();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            failure = ex;
            outcome = AccessFileOutcome.Unreadable($"{ex.GetType().Name}: {ex.Message}");
        }

        Report(access.Space, outcome, failure);
        activity?.SetOutput(new { outcome.Status, outcome.Fingerprint });
        return outcome;
    }

    private static async Task<AccessFileEnvelope> ReadAsync(SpaceContentAccess access, CancellationToken ct)
    {
        ContentResult? result = await access.Source.GetAsync(AccessFile.FileName, ct);
        if (result is not null)
        {
            return AccessFileEnvelope.From(AccessFile.Parse(result.Bytes));
        }

        // On a case-sensitive store, a root file named with other casing isn't found by the read
        // above, while the mounted view still refuses to serve it. Reading it as "no file" would
        // publish a space its author meant to restrict, so a misnamed file closes the space instead.
        IReadOnlyList<ChildEntry> root = await access.Lister.ListChildrenAsync(string.Empty, ct) ?? [];
        return root.Any(static c => !c.IsFolder && AccessFile.IsAccessFileKey(c.Name))
            ? AccessFileEnvelope.From(AccessFileOutcome.Invalid(
                $"The content root holds an access file whose name differs from '{AccessFile.FileName}' in case or spelling. Rename it to '{AccessFile.FileName}'.",
                null))
            : AccessFileEnvelope.From(AccessFileOutcome.Missing);
    }

    /// <summary>Logs a space's outcome when it differs from the last one logged for that space.</summary>
    private void Report(SpaceOptions space, AccessFileOutcome outcome, Exception? failure)
    {
        string signature = $"{outcome.Status}|{outcome.Fingerprint}|{outcome.Problem}";
        if (reported.TryGetValue(space.Id, out string? previous) && previous == signature)
        {
            return;
        }

        reported[space.Id] = signature;
        switch (outcome.Status)
        {
            case AccessFileStatus.Valid:
                AccessFile file = outcome.File!;
                logger.LogInformation(
                    "Space '{SpaceId}' access file {Fingerprint} is valid. Requires: {Requires}. Roles: {Roles}.",
                    space.Id,
                    file.Fingerprint,
                    file.Requires.IsEmpty ? "nothing" : string.Join(", ", file.Requires),
                    file.Roles.IsEmpty
                        ? "none"
                        : string.Join("; ", file.Roles.Select(static r => $"{r.Key} -> [{string.Join(", ", r.Value)}]")));
                break;

            case AccessFileStatus.Invalid:
                logger.LogError(
                    "Space '{SpaceId}' access file {Fingerprint} is invalid, so the space is hidden until a valid file is published: {Problem}",
                    space.Id,
                    outcome.Fingerprint,
                    outcome.Problem);
                break;

            case AccessFileStatus.Unreadable:
                logger.LogError(
                    failure,
                    "Space '{SpaceId}' access file can't be read, so the space is hidden until a read succeeds: {Problem}",
                    space.Id,
                    outcome.Problem);
                break;

            case AccessFileStatus.Missing when space.AccessFileRequired:
                logger.LogError(
                    "Space '{SpaceId}' requires an access file, but {FileName} is missing at its content root, so the space is hidden.",
                    space.Id,
                    AccessFile.FileName);
                break;

            default:
                logger.LogDebug(
                    "Space '{SpaceId}' has no access file; only the site default and its floor apply.", space.Id);
                break;
        }
    }

    /// <summary>
    /// The cached form of an outcome: plain arrays, because a distributed cache stores entries
    /// serialized and would lose the case-insensitive comparers of the parsed sets.
    /// </summary>
    private sealed record AccessFileEnvelope(
        AccessFileStatus Status,
        string? Fingerprint,
        string? Problem,
        string[] Permissions,
        string[] Requires,
        AccessRoleEntry[] Roles) : ISizeableHeuristically
    {
        public static AccessFileEnvelope From(AccessFileOutcome outcome) =>
            outcome.File is { } file
                ? new(
                    outcome.Status,
                    outcome.Fingerprint,
                    outcome.Problem,
                    file.Permissions.ToArray(),
                    file.Requires.ToArray(),
                    file.Roles.Select(static r => new AccessRoleEntry(r.Key, r.Value.ToArray())).ToArray())
                : new(outcome.Status, outcome.Fingerprint, outcome.Problem, [], [], []);

        public AccessFileOutcome ToOutcome() => Status switch
        {
            AccessFileStatus.Missing => AccessFileOutcome.Missing,
            AccessFileStatus.Valid when Fingerprint is not null => AccessFileOutcome.Valid(AccessFile.Restore(
                Permissions ?? [],
                Requires ?? [],
                (Roles ?? []).Select(static r =>
                    new KeyValuePair<string, IEnumerable<string>>(r.Role, r.Permissions ?? [])),
                Fingerprint)),
            AccessFileStatus.Invalid => AccessFileOutcome.Invalid(Problem ?? "The access file is invalid.", Fingerprint),
            // An entry this build can't interpret must not be read as "no file": it closes the space.
            _ => AccessFileOutcome.Invalid("The cached access file entry is unrecognized.", Fingerprint),
        };

        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(128L + 2L * (
                (Fingerprint?.Length ?? 0) +
                (Problem?.Length ?? 0) +
                Permissions.Sum(static p => p.Length) +
                Requires.Sum(static p => p.Length) +
                Roles.Sum(static r => r.Role.Length + r.Permissions.Sum(static p => p.Length))));
    }

    private sealed record AccessRoleEntry(string Role, string[] Permissions);
}
