using Diginsight.Components;
using Diginsight.Diagnostics;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.Extensions.Logging;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// SmartCache decorator over <see cref="INavBuilder"/>. Caches navigation levels and the flattened
/// index in-memory (optionally Redis-backed) keyed on <see cref="ContentPathCacheKey"/> so a
/// content write can invalidate exactly the affected branch. Also owns the monotonic version that
/// clients poll to drop their own cache.
/// </summary>
public sealed class CachedDynamicNavBuilder(
    INavBuilder inner,
    ISmartCache smartCache,
    IParallelService parallelService,
    ContentFreshnessOptions freshness,
    ILogger<CachedDynamicNavBuilder> logger) : INavBuilder
{
    private static long _version = 1;

    private int _warming;

    // Set by every warm request and drained by the running warm. Without it a request that lands
    // while a warm is in flight is lost - and that is the damaging case, because the warm in flight
    // is repopulating entries the newer invalidation has already dropped.
    private int _warmRequested;

    /// <summary>Current nav version; bumps on <see cref="Invalidate()"/>.</summary>
    public static long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// Invalidates the whole navigation (and content) cache: bumps the version — the signal clients
    /// poll via <c>/_nav/version</c> to drop their own cache — and evicts every server-side entry on
    /// every node via an empty-path rule.
    /// </summary>
    public void Invalidate() => Invalidate(string.Empty);

    /// <summary>
    /// Evicts every cached navigation <c>level</c> (but not the flattened index or the content cache)
    /// without bumping the version. Used by the startup warm-up: the recursive per-folder counts are
    /// only known after <see cref="GetIndexAsync"/> walks the tree, so any level built on the request
    /// path before that finished was cached with null counts. Dropping those levels lets
    /// <see cref="WarmAllLevelsAsync"/> rebuild them with the now-computed counts.
    /// </summary>
    public void InvalidateLevels() =>
        smartCache.Invalidate(new ContentPathInvalidationRule(string.Empty, Kind: "nav-level"));

    /// <summary>
    /// Invalidates just the branch touched by a content write at <paramref name="path"/>: the cached
    /// article plus every menu level that lists an ancestor of it, on every node. Still bumps the
    /// version so clients (which hold only a single version number, not per-path state) refetch.
    /// An empty <paramref name="path"/> invalidates everything.
    /// </summary>
    public void Invalidate(string path)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { path });

        Interlocked.Increment(ref _version);
        smartCache.Invalidate(new ContentPathInvalidationRule(ContentPathCacheKey.Normalize(path)));
    }

    public async Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        prefix = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');

        // Path-addressed key: Invalidate(path) drops this level when the changed path is on its branch.
        // CoalesceRacingCacheMisses gives the cache-stampede protection that used to be a manual
        // ConcurrentDictionary. One level is cheap to rebuild, so it carries a short freshness
        // tolerance: it is what lets a newly published article reach the menu even if the
        // invalidation call after publishing never arrived.
        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.NavLevel,
        };
        var key = new ContentPathCacheKey("nav-level", prefix);

        string levelPrefix = prefix;
        NavChildrenEnvelope envelope = await smartCache.GetAsync(
            key,
            async innerCt => new NavChildrenEnvelope((await inner.GetChildrenAsync(levelPrefix, innerCt)).ToArray()),
            options,
            callerType: typeof(CachedDynamicNavBuilder),
            cancellationToken: ct);

        activity?.SetOutput(new { count = envelope.Items.Count() });
        return envelope.Items;
    }

    /// <summary>
    /// Rebuilds the index and every level away from the request path, after an invalidation has just
    /// dropped them. Without this, making the publish-time invalidation reliable would simply move
    /// the cost onto a reader: the index walks every article — around 14 seconds on a 1,100-article
    /// site — and whoever arrived first would wait for it. Returns immediately; concurrent requests
    /// collapse onto the warm already in flight, which then runs one more pass so that entries it
    /// had populated before the newer invalidation dropped them are rebuilt too.
    /// </summary>
    public void WarmInBackground()
    {
        // Record the request before claiming the slot, so a warm already running picks it up.
        Interlocked.Exchange(ref _warmRequested, 1);

        if (Interlocked.Exchange(ref _warming, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            do
            {
                while (Interlocked.Exchange(ref _warmRequested, 0) == 1)
                {
                    try
                    {
                        await GetIndexAsync();
                        await WarmAllLevelsAsync();
                    }
                    catch (Exception ex)
                    {
                        // Warming is an optimisation: a failure here costs the next reader time, not
                        // correctness, so it must never take the invalidation down with it.
                        logger.LogWarning(ex, "Nav warm-up after invalidation failed");
                    }
                }

                Interlocked.Exchange(ref _warming, 0);

                // A request that arrived between draining the flag and releasing the slot would
                // otherwise be lost, so reclaim the slot and drain again. If another caller claimed
                // it first, that caller runs the warm instead.
            }
            while (Volatile.Read(ref _warmRequested) == 1 && Interlocked.Exchange(ref _warming, 1) == 0);
        });
    }

    /// <summary>Flattened article index (menu search / prev-next), cached at the root path.</summary>
    public async Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        // The whole-tree walk is the expensive cold path, so coalesce racing misses. Unlike a single
        // level this one is not cheap to rebuild — it reads every article — so it does not get a
        // short tolerance by default: expiring it on a timer would hand the rebuild to whichever
        // visitor arrived first. It is refreshed by the invalidation call instead, and only takes a
        // tolerance where one is configured explicitly.
        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.NavIndex,
        };
        var key = new ContentPathCacheKey("nav-index", string.Empty);

        NavIndexEnvelope envelope = await smartCache.GetAsync(
            key,
            async innerCt => new NavIndexEnvelope((await inner.GetIndexAsync(innerCt)).ToArray()),
            options,
            callerType: typeof(CachedDynamicNavBuilder),
            cancellationToken: ct);

        return envelope.Items;
    }

    /// <summary>
    /// Pre-warms every nav level through the cache by recursively calling <see cref="GetChildrenAsync"/>
    /// for every section prefix at each depth. Call after startup so expand-all is instant.
    /// </summary>
    public Task WarmAllLevelsAsync(CancellationToken ct = default)
        => WarmLevelAsync(string.Empty, int.MaxValue, ct);

    /// <summary>
    /// Pre-warms nav levels starting at <paramref name="prefix"/> down to <paramref name="depth"/> additional levels.
    /// Use to ensure N+2 levels ahead of a selected node are cache-hot.
    /// </summary>
    public Task WarmLevelsAsync(string prefix, int depth, CancellationToken ct = default)
        => depth <= 0 ? Task.CompletedTask : WarmLevelAsync(prefix, depth, ct);

    private async Task WarmLevelAsync(string prefix, int remainingDepth, CancellationToken ct)
    {
        if (remainingDepth <= 0) return;

        var children = await GetChildrenAsync(prefix, ct);

        // Sibling sections are independent (SmartCache single-flight already guards racing misses),
        // so warming them concurrently instead of one-at-a-time shortens the background warm-up.
        var sections = children.Where(c => c.IsSection && c.Prefix is not null).ToList();
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parallelService.MediumConcurrency, CancellationToken = ct };
        await parallelService.ForEachAsync(sections, parallelOptions,
            child => WarmLevelAsync(child.Prefix!, remainingDepth - 1, ct));
    }

    /// <summary>Serializable envelope so a built level round-trips through SmartCache (incl. Redis).</summary>
    private sealed record NavChildrenEnvelope(NavChild[] Items);

    /// <summary>Serializable envelope so the flattened index round-trips through SmartCache.</summary>
    private sealed record NavIndexEnvelope(NavLeaf[] Items);
}
