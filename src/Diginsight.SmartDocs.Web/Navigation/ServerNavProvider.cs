using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.Extensions.Logging;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Server-side <see cref="INavProvider"/> — builds levels in-process (used during prerender). Every
/// level, record and index it returns is filtered for the request's reader before it reaches the
/// prerendered page or the bootstrap state the browser hydrates from.
/// </summary>
public sealed class ServerNavProvider(
    INavBuilder builder,
    ReaderAccess reader,
    NavigationBootstrapState bootstrap,
    ILogger<ServerNavProvider> logger) : INavProvider
{
    public async Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        IReadOnlyList<NavChild> level = NavRules.WithoutEmptySections(
            await reader.FilterLevelAsync(await builder.GetChildrenAsync(prefix, ct)));
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

        FolderArticleStats? total = reader.GetTotal() is { } site
            ? new FolderArticleStats(site.Count, site.Latest, null, site.Coverage)
            : null;
        return Task.FromResult(total);
    }

    public async Task<FolderRecord?> GetFolderAsync(string prefix, CancellationToken ct = default) =>
        await reader.GetFolderAsync(prefix, ct);

    public async Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        return await reader.FilterIndexAsync(await builder.GetIndexAsync(ct));
    }
}
