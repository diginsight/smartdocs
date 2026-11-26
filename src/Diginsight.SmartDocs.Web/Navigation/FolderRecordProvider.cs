using System.Collections.Concurrent;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Navigation;

public sealed class FolderRecordProvider(
    INavBuilder nav,
    IContentLister content,
    FolderMetricsIndex metrics,
    SiteOptions site,
    ILogger<FolderRecordProvider> logger)
{
    private readonly ConcurrentDictionary<string, byte> warnedReservedFolders = new(StringComparer.OrdinalIgnoreCase);

    public async Task<FolderRecord> GetAsync(string prefix, CancellationToken cancellationToken = default)
    {
        string normalized = Normalize(prefix);
        using var activity = Observability.ActivitySource.StartMethodActivity(
            logger, () => new { prefix }, callerMemberName: nameof(GetAsync));

        FolderRecord record = await BuildAsync(normalized, cancellationToken);
        activity?.SetOutput(new { record.Classification, record.Coverage });
        return record;
    }

    public void Invalidate(string prefix)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });
        metrics.InvalidateIfTracked(Normalize(prefix));
    }

    private async Task<FolderRecord> BuildAsync(string prefix, CancellationToken cancellationToken)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

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
                child.Metadata is not null &&
                string.Equals(child.SourceFolder, prefix, StringComparison.OrdinalIgnoreCase));

        FolderMeta meta = node?.Metadata
            ?? await content.ReadFolderMetaAsync($"{prefix}/metadata.yml", cancellationToken);
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
            WithoutReservedFields(prefix, meta.Values));
    }

    /// <summary>
    /// Defends the browser projection if a nonstandard lister supplies reserved metadata fields.
    /// </summary>
    private IReadOnlyDictionary<string, string> WithoutReservedFields(string prefix, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
        {
            return new Dictionary<string, string>();
        }

        if (!values.Keys.Any(MetadataParser.IsReservedNavigationKey))
        {
            return values;
        }

        if (warnedReservedFolders.TryAdd(prefix, 0))
        {
            logger.LogWarning(
                "Folder '{Prefix}' supplied reserved metadata fields; they are excluded from the browser record.",
                prefix);
        }

        return values
            .Where(static pair => !MetadataParser.IsReservedNavigationKey(pair.Key))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string prefix) =>
        (prefix ?? string.Empty).Replace('\\', '/').Trim('/');

    private static string ParentOf(string prefix)
    {
        int cut = prefix.LastIndexOf('/');
        return cut < 0 ? string.Empty : prefix[..cut];
    }

}
