using System.Collections.Concurrent;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Diginsight.Runtime;

namespace Diginsight.SmartDocs.Web.Navigation;

public sealed class FolderRecordProvider(
    INavBuilder nav,
    IContentLister content,
    FolderMetricsIndex metrics,
    ISmartCache smartCache,
    BackgroundRevalidationCache revalidation,
    ContentFreshnessOptions freshness,
    SiteOptions site,
    ILogger<FolderRecordProvider> logger)
{
    /// <summary>The folder metadata key that would carry a folder requirement once folder requirements exist.</summary>
    private const string AccessKey = "access";

    private readonly ConcurrentDictionary<string, byte> warnedAccessFolders = new(StringComparer.OrdinalIgnoreCase);

    public Task<FolderRecord> GetAsync(string prefix, CancellationToken cancellationToken = default)
    {
        string normalized = Normalize(prefix);
        FolderInvalidationCallbacks.Register(normalized, () =>
        {
            metrics.InvalidateIfTracked(normalized);
            return Task.CompletedTask;
        });
        var key = new ContentPathCacheKey("folder", normalized);
        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.Structure,
        };

        return GetRecordAsync();

        async Task<FolderRecord> GetRecordAsync()
        {
            FolderRecordEnvelope envelope = await revalidation.GetAsync(
            key,
            freshness.Structure,
            ct => smartCache.GetAsync(
                key,
                async cacheCt => new FolderRecordEnvelope(await BuildAsync(normalized, cacheCt)),
                options,
                callerType: typeof(FolderRecordProvider),
                cancellationToken: ct),
            cancellationToken);
            return envelope.Record;
        }
    }

    public void Invalidate(string prefix)
    {
        var rule = new ContentPathInvalidationRule(Normalize(prefix), Kind: "folder");
        revalidation.Invalidate(rule);
        smartCache.Invalidate(rule);
    }

    private async Task<FolderRecord> BuildAsync(string prefix, CancellationToken cancellationToken)
    {
        if (prefix.Length == 0)
        {
            FolderMetrics root = metrics.TryGet(string.Empty) ?? default;
            return new FolderRecord(
                string.Empty,
                site.Title,
                "/",
                null,
                site.Branding.IconClass,
                null,
                false,
                false,
                null,
                "site",
                true,
                root.Count,
                root.Latest,
                root.Coverage,
                new Dictionary<string, string>());
        }

        string parent = ParentOf(prefix);
        NavChild? node = (await nav.GetChildrenAsync(parent, cancellationToken))
            .FirstOrDefault(child =>
                child.IsSection &&
                string.Equals(child.Prefix, prefix, StringComparison.OrdinalIgnoreCase));

        FolderMeta meta = await content.ReadFolderMetaAsync($"{prefix}/metadata.yml", cancellationToken);
        FolderMetrics aggregate = metrics.TryGet(prefix) ?? default;
        return Compose(prefix, node, meta, aggregate);
    }

    /// <summary>
    /// The record of a folder prefix that has no navigation node, no <c>metadata.yml</c> and no
    /// metrics: what <see cref="GetAsync"/> answers for a folder that doesn't exist. A folder the
    /// reader can't see is answered with it, so it can't be told apart from a missing one.
    /// </summary>
    public FolderRecord Unknown(string prefix) =>
        Compose(Normalize(prefix), null, FolderMeta.None, default);

    private FolderRecord Compose(string prefix, NavChild? node, FolderMeta meta, FolderMetrics aggregate)
    {
        string name = prefix[(prefix.LastIndexOf('/') + 1)..];
        string label = node?.Text ?? meta.Label ?? NavRules.Label(name);
        string classification = meta.Hidden ? "hidden"
            : NavRules.IsAssetFolder(name) ? "asset"
            : node is null ? "folder"
            : "section";

        return new FolderRecord(
            prefix,
            label,
            node?.Route,
            node?.Short ?? meta.Short,
            node?.Icon ?? meta.Icon ?? NavRules.IconFor(name, label),
            meta.Order,
            meta.Hidden,
            node?.TopbarHidden ?? meta.TopbarHidden,
            node?.TopbarAlign ?? meta.TopbarAlign,
            classification,
            node?.HasChildren ?? true,
            aggregate.Count,
            aggregate.Latest,
            aggregate.Coverage,
            WithoutAccess(prefix, meta.Values));
    }

    /// <summary>
    /// Removes an <c>access</c> line from the metadata sent to browsers. Version 1 restricts whole
    /// spaces only, so the line restricts nothing; sending it would advertise a restriction that
    /// isn't enforced. Logged once per folder so the author learns it has no effect yet.
    /// </summary>
    private IReadOnlyDictionary<string, string> WithoutAccess(string prefix, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
        {
            return new Dictionary<string, string>();
        }

        if (!values.Keys.Any(static k => string.Equals(k, AccessKey, StringComparison.OrdinalIgnoreCase)))
        {
            return values;
        }

        if (warnedAccessFolders.TryAdd(prefix, 0))
        {
            logger.LogWarning(
                "Folder '{Prefix}' declares '{Key}' in metadata.yml. Version 1 restricts whole spaces only, so the line restricts nothing; it is left out of the folder record.",
                prefix,
                AccessKey);
        }

        return values
            .Where(static pair => !string.Equals(pair.Key, AccessKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string prefix) =>
        (prefix ?? string.Empty).Replace('\\', '/').Trim('/');

    private static string ParentOf(string prefix)
    {
        int cut = prefix.LastIndexOf('/');
        return cut < 0 ? string.Empty : prefix[..cut];
    }

    private sealed record FolderRecordEnvelope(FolderRecord Record) : ISizeableHeuristically
    {
        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(256L + 2L * (
                Record.Prefix.Length +
                Record.Label.Length +
                (Record.Route?.Length ?? 0) +
                (Record.Short?.Length ?? 0) +
                (Record.Icon?.Length ?? 0) +
                Record.Classification.Length +
                Record.Metadata.Sum(static pair => pair.Key.Length + pair.Value.Length)));
    }
}
