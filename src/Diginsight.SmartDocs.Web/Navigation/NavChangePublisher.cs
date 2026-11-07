using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
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
    FolderRecordProvider folders,
    IHubContext<NavHub> hub,
    ActiveRoleSets activeRoleSets,
    SpaceVisibilityEvaluator evaluator,
    AccessRequirementResolver requirements,
    ReaderCountComposer counts,
    SpaceRegistry spaces,
    VisibleMetadataFingerprint fingerprints,
    ILogger<NavChangePublisher> logger)
{
    private int _wired;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    // The root-own value the previous drain left, to tell a change among the root-mounted space's own
    // articles from a change in any other space. Written only by the drain's handler, which never overlaps itself.
    private FolderMetrics? _lastRootOwn;

    // Coalesces whole-site rediscoveries: requests arriving while one runs trigger exactly one more.
    private int _rediscovering;
    private int _rediscoverRequested;

    /// <summary>Subscribes to drain results. Called once at startup; idempotent.</summary>
    public void Wire()
    {
        if (Interlocked.Exchange(ref _wired, 1) == 1)
        {
            return;
        }

        _lastRootOwn = metrics.TryGetRootOwn();
        metrics.Changed += OnMetricsChangedAsync;
    }

    /// <summary>
    /// Records a changed content <paramref name="path"/>: stamps the folder and every ancestor as
    /// needing a refold. Synchronous, O(depth), no I/O — the drain is scheduled and debounced.
    /// An empty path is a whole-site change, handled by <see cref="RediscoverInBackground"/>.
    /// </summary>
    public async Task PublishChangeAsync(string path)
    {
        string normalized = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            // A whole-site change — the call the publish pipeline makes — may have added or removed
            // folders anywhere. Stamping only the root used to leave every section's count as it was
            // before the publish, still marked complete; now every cell refolds, and the tree is
            // walked again so folders created by the publish get cells of their own.
            metrics.InvalidateAll();
            RediscoverInBackground();
        }
        else
        {
            metrics.Invalidate(FolderOf(normalized));
        }

        if (normalized.Length == 0 || MetadataFiles.IsCarrierKey(normalized))
        {
            await PublishMetadataRefreshAsync(normalized);
        }
    }

    private async Task PublishMetadataRefreshAsync(string path)
    {
        string? changedSpace = null;
        if (path.Length > 0)
        {
            if (!spaces.TryResolve(path, out SpaceOptions space, out _))
            {
                return;
            }

            changedSpace = space.Id;
        }

        await _refreshGate.WaitAsync();
        try
        {
            var snapshots = new Dictionary<string, Task<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (RoleSet roles in activeRoleSets.Snapshot())
            {
                try
                {
                    ReaderVisibility visibility = await evaluator.EvaluateAsync(roles);
                    IReadOnlyDictionary<string, string> current =
                        await VisibleMetadataAsync(visibility, snapshots, changedSpace);
                    if (!activeRoleSets.NeedsRefresh(roles, current, changedSpace))
                    {
                        continue;
                    }

                    await hub.Clients.Group(ActiveRoleSets.GroupOf(roles)).SendAsync(NavHubContract.RefreshRequired);
                    activeRoleSets.Refreshed(roles, current, changedSpace);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Metadata refresh publish failed for a role set");
                }
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> VisibleMetadataAsync(
        ReaderVisibility visibility, Dictionary<string, Task<string>>? snapshots = null, string? onlySpace = null)
    {
        var visible = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        snapshots ??= new Dictionary<string, Task<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in visibility.VisibleSpaceIds)
        {
            if (onlySpace is not null && !string.Equals(id, onlySpace, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!snapshots.TryGetValue(id, out Task<string>? snapshot))
            {
                SpaceOptions space = spaces.ById(id)
                    ?? throw new InvalidOperationException($"Visible space '{id}' is not registered.");
                snapshots[id] = snapshot = fingerprints.ForSpaceAsync(space);
            }
            visible[id] = await snapshot;
        }

        return visible;
    }

    private void RediscoverInBackground()
    {
        // Record the request before claiming the slot, so a rediscovery already running picks it up.
        Interlocked.Exchange(ref _rediscoverRequested, 1);
        if (Interlocked.Exchange(ref _rediscovering, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            do
            {
                while (Interlocked.Exchange(ref _rediscoverRequested, 0) == 1)
                {
                    try
                    {
                        await RediscoverAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Nav rediscovery after a whole-site invalidation failed");
                    }
                }

                Interlocked.Exchange(ref _rediscovering, 0);
            }
            while (Volatile.Read(ref _rediscoverRequested) == 1 && Interlocked.Exchange(ref _rediscovering, 1) == 0);
        });
    }

    private async Task RediscoverAsync()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NavChild root in await nav.GetChildrenAsync(string.Empty))
        {
            if (root.IsSection && root.Prefix is not null)
            {
                reachable.UnionWith(await metrics.DiscoverAsync(root.Prefix));
            }
        }

        // Folders the publish deleted must not linger in the index; the root total then refolds over
        // what is left.
        metrics.PruneUnreachable(reachable);
        metrics.Invalidate(string.Empty);
    }

    private async Task OnMetricsChangedAsync(IReadOnlyList<string> prefixes)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefixes });

        try
        {
            var changedPrefixes = new HashSet<string>(prefixes, StringComparer.OrdinalIgnoreCase);
            if (metrics.TryGet(string.Empty) is not null)
            {
                changedPrefixes.Add(string.Empty);
            }

            foreach (string prefix in changedPrefixes)
            {
                folders.Invalidate(prefix);
            }

            FolderRecord[] records = await Task.WhenAll(
                changedPrefixes.Select(prefix => folders.GetAsync(prefix)));
            if (records.Length == 0)
            {
                return;
            }

            // The root-mounted space's own articles have no record of their own: their change moves only
            // the root, so it is detected here, and it counts as a visible change for the readers who
            // see that space.
            FolderMetrics? rootOwn = metrics.TryGetRootOwn();
            bool rootOwnMoved = rootOwn != _lastRootOwn;
            _lastRootOwn = rootOwn;

            int sent = 0;
            foreach (RoleSet roles in activeRoleSets.Snapshot())
            {
                try
                {
                    ReaderVisibility visibility = await evaluator.EvaluateAsync(roles);
                    (FolderRecord[] visible, int visibleChanges) = await RecordsForAsync(visibility, records);

                    // A group gets its composed root only together with a change it can see, so a change in
                    // a hidden space can't be timed from outside.
                    bool seesRootOwn = rootOwnMoved
                        && spaces.Root is { } rootSpace
                        && visibility.Spaces.TryGetValue(rootSpace.Id, out SpaceVisibility? rootVerdict)
                        && rootVerdict.IsVisible;
                    if (visibleChanges == 0 && !seesRootOwn)
                    {
                        continue;
                    }

                    await hub.Clients.Group(ActiveRoleSets.GroupOf(roles)).SendAsync(NavHubContract.MetadataChanged, visible);
                    sent++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Nav metadata change publish failed for a role set");
                }
            }

            logger.LogDebug("Pushed {Count} folder records to {Groups} groups", records.Length, sent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav metadata change publish failed");
        }
    }

    /// <summary>Sends every active role set the root section aggregates it may see (called as the startup scan progresses).</summary>
    public async Task PublishCountsReadyAsync()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        try
        {
            FolderRecord[] records = await BuildRootRecordsAsync();
            foreach (RoleSet roles in activeRoleSets.Snapshot())
            {
                try
                {
                    ReaderVisibility visibility = await evaluator.EvaluateAsync(roles);
                    (FolderRecord[] visible, _) = await RecordsForAsync(visibility, records);
                    if (visible.Length > 0)
                    {
                        await hub.Clients.Group(ActiveRoleSets.GroupOf(roles)).SendAsync(NavHubContract.CountsReady, visible);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Nav counts-ready publish failed for a role set");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav counts-ready publish failed");
        }
    }

    /// <summary>
    /// Sends the current root aggregates a single just-connected client may see. A browser that connects
    /// after the scan finished would otherwise never learn the counts.
    /// </summary>
    public async Task SendCurrentCountsAsync(IClientProxy caller, RoleSet roles)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        try
        {
            ReaderVisibility visibility = await evaluator.EvaluateAsync(roles);
            activeRoleSets.RememberVisibility(roles, await VisibleMetadataAsync(visibility));
            await caller.SendAsync(NavHubContract.VisibleSpaces, visibility.VisibleSpaceIds.ToArray());
            FolderRecord[] records = await BuildRootRecordsAsync();
            (FolderRecord[] visible, _) = await RecordsForAsync(visibility, records);
            if (visible.Length > 0)
            {
                await caller.SendAsync(NavHubContract.CountsReady, visible);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav counts-ready send-on-connect failed");
        }
    }

    /// <summary>
    /// The records a reader may see, with the root's counts composed for them, and how many of the
    /// others they see. The site's own record is never hidden; every other record is checked against
    /// its space.
    /// </summary>
    private async Task<(FolderRecord[] Records, int VisibleChanges)> RecordsForAsync(
        ReaderVisibility visibility, IReadOnlyList<FolderRecord> all)
    {
        var visible = new List<FolderRecord>(all.Count);
        int others = 0;
        foreach (FolderRecord record in all)
        {
            if (record.Prefix.Length == 0)
            {
                visible.Add(counts.ComposeRootRecord(record, visibility));
                continue;
            }

            if (visibility.SeesEverything || visibility.IsVisible(await requirements.ForFolderAsync(record.Prefix)))
            {
                visible.Add(record);
                others++;
            }
        }

        return (visible.ToArray(), others);
    }

    private async Task<FolderRecord[]> BuildRootRecordsAsync()
    {
        IReadOnlyList<NavChild> roots = await nav.GetChildrenAsync(string.Empty);
        var prefixes = roots
            .Where(static child => child.IsSection && child.Prefix is not null)
            .Select(static child => child.Prefix!)
            .ToList();
        if (metrics.TryGet(string.Empty) is not null)
        {
            prefixes.Insert(0, string.Empty);
        }

        foreach (string prefix in prefixes)
        {
            folders.Invalidate(prefix);
        }

        return await Task.WhenAll(prefixes.Select(prefix => folders.GetAsync(prefix)));
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
