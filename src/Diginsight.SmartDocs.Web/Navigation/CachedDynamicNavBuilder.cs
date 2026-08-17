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
    ILogger<CachedDynamicNavBuilder> logger) : INavBuilder
{
    private static long _version = 1;

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
        // ConcurrentDictionary. Freshness (MaxAge / SlidingExpiration / AbsoluteExpiration) comes from
        // Diginsight:SmartCache config — including any class-aware override such as
        // SlidingExpiration@CachedDynamicNavBuilder — resolved via the caller type below.
        var options = new SmartCacheOperationOptions { CoalesceRacingCacheMisses = true };
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

    /// <summary>Flattened article index (menu search / prev-next), cached at the root path.</summary>
    public async Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        // The whole-tree walk is the expensive cold path, so coalesce racing misses. Freshness comes
        // from Diginsight:SmartCache config (class-aware via CachedDynamicNavBuilder), keyed at the
        // root path so any content change invalidates it.
        var options = new SmartCacheOperationOptions { CoalesceRacingCacheMisses = true };
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
