using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.Extensions.Logging;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>Server-side <see cref="INavProvider"/> — builds levels in-process (used during prerender).</summary>
public sealed class ServerNavProvider(
    INavBuilder builder,
    FolderMetricsIndex metrics,
    FolderRecordProvider folders,
    NavigationBootstrapState bootstrap,
    ILogger<ServerNavProvider> logger) : INavProvider
{
    public async Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        IReadOnlyList<NavChild> level =
            NavRules.WithoutEmptySections(await builder.GetChildrenAsync(prefix, ct));
        bootstrap.SetLevel(prefix, level);
        FolderRecord?[] records = await Task.WhenAll(level
            .Where(static child => child.IsSection && child.Prefix is not null)
            .Select(child => GetFolderAsync(child.Prefix!, ct)));
        bootstrap.SetFolderRecords(records.OfType<FolderRecord>());
        return level;
    }

    public Task<FolderArticleStats?> GetTotalAsync(CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        FolderArticleStats? total = metrics.TryGet(string.Empty) is { } site
            ? new FolderArticleStats(site.Count, site.Latest, null, site.Coverage)
            : null;
        return Task.FromResult(total);
    }

    public async Task<FolderRecord?> GetFolderAsync(string prefix, CancellationToken ct = default) =>
        await folders.GetAsync(prefix, ct);

    public async Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        return await builder.GetIndexAsync(ct);
    }
}
