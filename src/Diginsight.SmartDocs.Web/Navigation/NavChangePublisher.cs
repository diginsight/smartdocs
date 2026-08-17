using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Bridges <see cref="FolderMetricsIndex"/> to connected clients: when a drain changes a folder's
/// value, the new <em>absolute</em> aggregate (plus its coverage) is pushed over <see cref="NavHub"/>,
/// so sidebar counts and the footer total update live without polling.
/// <para>
/// It owns no counting logic. A content change calls <see cref="PublishChangeAsync"/>, which only
/// stamps the changed folder's ancestor spine — the debounced, deepest-first drain does the folding
/// and calls back here with exactly the prefixes whose value moved.
/// </para>
/// </summary>
public sealed class NavChangePublisher(
    FolderMetricsIndex metrics,
    CachedDynamicNavBuilder nav,
    IHubContext<NavHub> hub,
    ILogger<NavChangePublisher> logger)
{
    private int _wired;

    /// <summary>Subscribes to drain results. Called once at startup; idempotent.</summary>
    public void Wire()
    {
        if (Interlocked.Exchange(ref _wired, 1) == 1)
        {
            return;
        }

        metrics.Changed += OnMetricsChangedAsync;
    }

    /// <summary>
    /// Records a changed content <paramref name="path"/>: stamps the folder and every ancestor as
    /// needing a refold. Synchronous, O(depth), no I/O — the drain is scheduled and debounced.
    /// </summary>
    public void PublishChangeAsync(string path)
    {
        // Drop the cached levels first: the fold reads a level to recount it, so it must not consume
        // one that was cached before this change.
        nav.InvalidateLevels();

        string folder = FolderOf((path ?? string.Empty).Replace('\\', '/').Trim('/'));
        metrics.Invalidate(folder);
    }

    private async Task OnMetricsChangedAsync(IReadOnlyList<string> prefixes)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefixes });

        try
        {
            // The counts live on the folder nodes inside each parent's cached level, so those levels
            // must rebuild before anyone reads them again.
            nav.InvalidateLevels();

            NavAggregateDelta[] deltas = prefixes
                .Select(p => (Prefix: p, Metrics: metrics.TryGet(p)))
                .Where(x => x.Metrics is not null)
                .Select(x => new NavAggregateDelta(
                    x.Prefix, x.Metrics!.Value.Count, x.Metrics.Value.Latest, null, x.Metrics.Value.Coverage))
                .ToArray();

            if (deltas.Length == 0)
            {
                return;
            }

            // Always carry the site root, even when it did not change in this pass: it is the one
            // value every client displays, and a drain that publishes a folder without it would
            // leave the footer showing a total that disagrees with the section it just updated.
            if (!deltas.Any(d => d.Prefix.Length == 0) && metrics.TryGet(string.Empty) is { } site)
            {
                deltas = [.. deltas, new NavAggregateDelta(string.Empty, site.Count, site.Latest, null, site.Coverage)];
            }

            await hub.Clients.All.SendAsync(NavHubContract.MetadataChanged, deltas);
            logger.LogDebug("Pushed {Count} nav metadata deltas", deltas.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav metadata change publish failed");
        }
    }

    /// <summary>Broadcasts every root section's current aggregate (called as the startup scan progresses).</summary>
    public async Task PublishCountsReadyAsync()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        try
        {
            NavAggregateDelta[] deltas = await BuildRootDeltasAsync();
            if (deltas.Length > 0)
            {
                await hub.Clients.All.SendAsync(NavHubContract.CountsReady, deltas);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav counts-ready publish failed");
        }
    }

    /// <summary>
    /// Sends the current root aggregates to a single just-connected client. A browser that connects
    /// after the scan finished would otherwise never learn the counts.
    /// </summary>
    public async Task SendCurrentCountsAsync(IClientProxy caller)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        try
        {
            NavAggregateDelta[] deltas = await BuildRootDeltasAsync();
            if (deltas.Length > 0)
            {
                await caller.SendAsync(NavHubContract.CountsReady, deltas);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav counts-ready send-on-connect failed");
        }
    }

    private async Task<NavAggregateDelta[]> BuildRootDeltasAsync()
    {
        IReadOnlyList<NavChild> roots = await nav.GetChildrenAsync(string.Empty);
        var deltas = roots
            .Where(c => c.IsSection && c.Prefix is not null && c.ArticleCount is not null)
            .Select(c => new NavAggregateDelta(c.Prefix!, c.ArticleCount!.Value, c.LatestArticleUtc, null, c.CountCoverage))
            .ToList();

        // The site total is the root cell itself, not the sum of the root sections: the root level
        // also holds standalone articles that belong to no section.
        if (metrics.TryGet(string.Empty) is { } site)
        {
            deltas.Insert(0, new NavAggregateDelta(string.Empty, site.Count, site.Latest, null, site.Coverage));
        }

        return deltas.ToArray();
    }

    // A change to "a/b/article.md" is a change to folder "a/b"; a change to a folder is itself.
    private static string FolderOf(string path)
    {
        if (path.Length == 0 || !Path.GetFileName(path).Contains('.'))
        {
            return path;
        }

        int cut = path.LastIndexOf('/');
        return cut < 0 ? string.Empty : path[..cut];
    }
}
