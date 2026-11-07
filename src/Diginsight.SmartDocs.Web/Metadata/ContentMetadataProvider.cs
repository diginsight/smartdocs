using System.Collections.Concurrent;
using System.Collections.Immutable;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Metadata;

/// <summary>Reads and caches one complete metadata snapshot per mounted directory and mode.</summary>
public sealed class ContentMetadataProvider(
    SpaceContentRegistry content,
    SpaceRegistry spaces,
    ISmartCache cache,
    MetadataInvalidationTracker invalidations,
    IMetadataDocumentParser parser,
    ContentFreshnessOptions freshness,
    SiteOptions site,
    ILogger<ContentMetadataProvider> logger)
{
    public const string CanonicalCacheKind = "metadata-v1";
    public const string CompatibleCacheKind = "metadata-v1-legacy";

    private readonly string origin = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, string> reported = new(StringComparer.OrdinalIgnoreCase);

    public Task<MetadataOutcome> GetForSpaceAsync(string spaceId, CancellationToken ct = default) =>
        GetAsync(content.Get(spaceId), string.Empty, ct);

    public Task<MetadataOutcome> GetForMetadataKeyAsync(string key, CancellationToken ct = default)
    {
        string normalized = ContentPathCacheKey.Normalize(key);
        if (!ContentKeyGuard.IsSafe(key) || !MetadataFiles.IsCanonicalKey(normalized) ||
            !spaces.TryResolve(normalized, out SpaceOptions space, out string inner) ||
            !content.TryGet(space.Id, out SpaceContentAccess access))
        {
            return Task.FromResult(MetadataOutcome.Invalid("The metadata path does not resolve to a content scope."));
        }

        return GetAsync(access, Parent(inner), ct);
    }

    /// <summary>Marks active fills on invalidated branches before their cache entries are removed.</summary>
    public void Invalidate(IEnumerable<string> paths)
    {
        invalidations.Invalidate(new ContentPathInvalidationRule(paths));
    }

    private async Task<MetadataOutcome> GetAsync(SpaceContentAccess access, string folder, CancellationToken ct)
    {
        bool root = folder.Length == 0;
        string innerKey = root ? MetadataParser.FileName : folder + "/" + MetadataParser.FileName;
        string mount = access.Space.NormalizedRouteBase.Trim('/');
        string mountedKey = mount.Length == 0 ? innerKey : mount + "/" + innerKey;
        string kind = site.LegacyMetadataCompatibility ? CompatibleCacheKind : CanonicalCacheKind;
        var key = new ContentPathCacheKey(kind, mountedKey);
        bool forceOrigin = false;

        try
        {
            while (true)
            {
                var options = new SmartCacheOperationOptions
                {
                    CoalesceRacingCacheMisses = true,
                    MaxAge = forceOrigin ? TimeSpan.Zero : root ? freshness.Access : freshness.Structure,
                };
                MetadataSnapshotEnvelope envelope = await cache.GetAsync(
                    key,
                    async innerCt =>
                    {
                        long started = invalidations.Generation(mountedKey);
                        MetadataOutcome outcome = await ReadAsync(access, folder, innerCt);
                        return MetadataSnapshotEnvelope.From(outcome, origin, started);
                    },
                    options,
                    callerType: typeof(ContentMetadataProvider),
                    cancellationToken: ct);
                long current = invalidations.Generation(mountedKey);
                if ((envelope.Origin == origin && envelope.Generation != current) ||
                    (envelope.Origin != origin && current != 0))
                {
                    forceOrigin = true;
                    continue;
                }

                MetadataOutcome result = envelope.ToOutcome();
                Report(mountedKey, result, null);
                return result;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            MetadataOutcome result = MetadataOutcome.Unreadable($"{ex.GetType().Name}: {ex.Message}");
            Report(mountedKey, result, ex);
            return result;
        }
    }

    private async Task<MetadataOutcome> ReadAsync(SpaceContentAccess access, string folder, CancellationToken ct)
    {
        bool root = folder.Length == 0;
        string canonicalKey = root ? MetadataParser.FileName : folder + "/" + MetadataParser.FileName;
        ContentResult? canonical = await access.Source.GetAsync(canonicalKey, ct);
        MetadataOutcome result = canonical is null ? MetadataOutcome.Missing : parser.Parse(canonical.Bytes, root);
        if (result.Status == MetadataStatus.Invalid)
        {
            return result;
        }

        ContentResult? legacy = null;
        bool compatibility = root && site.LegacyMetadataCompatibility;
        if (compatibility)
        {
            legacy = await access.Source.GetAsync(AccessFile.FileName, ct);
        }

        if (canonical is null || (compatibility && legacy is null))
        {
            IReadOnlyList<ChildEntry> children = await access.Lister.ListChildrenAsync(folder, ct);
            bool misnamed = children.Any(child => !child.IsFolder &&
                ((canonical is null && MetadataFiles.IsCanonicalKey(child.Name) &&
                  !child.Name.Equals(MetadataParser.FileName, StringComparison.Ordinal)) ||
                 (compatibility && legacy is null && AccessFile.IsAccessFileKey(child.Name) &&
                  !child.Name.Equals(AccessFile.FileName, StringComparison.Ordinal))));
            if (misnamed)
            {
                return MetadataOutcome.Invalid("A metadata carrier has noncanonical casing or spelling.");
            }
        }

        if (legacy is null)
        {
            return result;
        }

        AccessFileOutcome legacyOutcome = parser.ParseLegacyAccess(legacy.Bytes);
        if (legacyOutcome.Status != AccessFileStatus.Valid)
        {
            return MetadataOutcome.Invalid(
                $"The legacy access file is invalid: {legacyOutcome.Problem}", legacyOutcome.Fingerprint);
        }

        AccessFile policy = legacyOutcome.File!;
        if (result.Document is { Access: { } canonicalPolicy } &&
            !MetadataPolicyComparer.Equivalent(canonicalPolicy, policy))
        {
            return MetadataOutcome.Invalid("The canonical and legacy access policies conflict.", result.Fingerprint);
        }

        MetadataDocument document = result.Document ?? new MetadataDocument(
            false, FolderMeta.None, null, null, policy.Fingerprint, ImmutableArray<string>.Empty);
        string warning = document.Access is null
            ? "A legacy access file supplies this scope's policy; migrate it to metadata.yml."
            : "Equivalent canonical and legacy access policies coexist; remove the legacy file after migration.";
        return MetadataOutcome.Valid(document with
        {
            Access = document.Access ?? policy,
            Warnings = document.Warnings.Add(warning),
        });
    }

    private void Report(string key, MetadataOutcome outcome, Exception? exception)
    {
        string signature = $"{outcome.Status}|{outcome.Fingerprint}|{outcome.Problem}|{string.Join('|', outcome.Document?.Warnings ?? [])}";
        if (reported.TryGetValue(key, out string? previous) && previous == signature)
        {
            return;
        }

        reported[key] = signature;
        if (outcome.Status is MetadataStatus.Invalid or MetadataStatus.Unreadable)
        {
            logger.LogError(exception, "Metadata at '{Key}' is {Status}: {Problem}", key, outcome.Status, outcome.Problem);
        }
        else if (outcome.Document is { } document)
        {
            foreach (string warning in document.Warnings)
            {
                logger.LogWarning("Metadata at '{Key}': {Warning}", key, warning);
            }
        }
    }

    private static string Parent(string key)
    {
        int slash = key.LastIndexOf('/');
        return slash < 0 ? string.Empty : key[..slash];
    }

}
