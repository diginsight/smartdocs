using Diginsight.Components;
using Diginsight.Diagnostics;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.Extensions.Logging;
using Diginsight.Runtime;

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
    BackgroundRevalidationCache revalidation,
    IParallelService parallelService,
    ContentFreshnessOptions freshness,
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
    public void Invalidate(string path) => Invalidate([path]);

    public void Invalidate(IEnumerable<string> paths)
    {
        string[] normalized = paths
            .Select(ContentPathCacheKey.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        using var activity = Observability.ActivitySource.StartMethodActivity(
            logger,
            () => new { paths = normalized });

        Interlocked.Increment(ref _version);
        var rule = new ContentPathInvalidationRule(normalized);
        revalidation.Invalidate(rule);
        smartCache.Invalidate(rule);
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
        NavChildrenEnvelope envelope = await revalidation.GetAsync(
            key,
            freshness.NavLevel,
            innerCt => smartCache.GetAsync(
                key,
                async cacheCt => new NavChildrenEnvelope((await inner.GetChildrenAsync(levelPrefix, cacheCt)).ToArray()),
                options,
                callerType: typeof(CachedDynamicNavBuilder),
                cancellationToken: innerCt),
            ct);

        activity?.SetOutput(new { count = envelope.Items.Count() });
        return envelope.Items;
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
    public Task WarmAllLevelsAsync(
        CancellationToken ct = default,
        Func<CancellationToken, Task>? beforeLevel = null)
        => WarmLevelAsync(string.Empty, int.MaxValue, ct, beforeLevel);

    /// <summary>
    /// Pre-warms nav levels starting at <paramref name="prefix"/> down to <paramref name="depth"/> additional levels.
    /// Use to ensure N+2 levels ahead of a selected node are cache-hot.
    /// </summary>
    public Task WarmLevelsAsync(
        string prefix,
        int depth,
        CancellationToken ct = default,
        Func<CancellationToken, Task>? beforeLevel = null)
        => depth <= 0 ? Task.CompletedTask : WarmLevelAsync(prefix, depth, ct, beforeLevel);

    private async Task WarmLevelAsync(
        string prefix,
        int remainingDepth,
        CancellationToken ct,
        Func<CancellationToken, Task>? beforeLevel)
    {
        if (remainingDepth <= 0) return;

        if (beforeLevel is not null)
        {
            await beforeLevel(ct);
        }

        var children = await GetChildrenAsync(prefix, ct);

        // Sibling sections are independent (SmartCache single-flight already guards racing misses),
        // so warming them concurrently instead of one-at-a-time shortens the background warm-up.
        var sections = children.Where(c => c.IsSection && c.Prefix is not null).ToList();
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parallelService.MediumConcurrency, CancellationToken = ct };
        await parallelService.ForEachAsync(sections, parallelOptions,
            child => WarmLevelAsync(child.Prefix!, remainingDepth - 1, ct, beforeLevel));
    }

    /// <summary>Serializable envelope so a built level round-trips through SmartCache (incl. Redis).</summary>
    private sealed record NavChildrenEnvelope(NavChild[] Items) : ISizeableHeuristically
    {
        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(128L + Items.Sum(static item => 128L + 2L * (
                item.Text.Length +
                (item.Route?.Length ?? 0) +
                (item.Prefix?.Length ?? 0) +
                (item.Icon?.Length ?? 0) +
                (item.Short?.Length ?? 0) +
                (item.Author?.Length ?? 0))));
    }

    /// <summary>Serializable envelope so the flattened index round-trips through SmartCache.</summary>
    private sealed record NavIndexEnvelope(NavLeaf[] Items) : ISizeableHeuristically
    {
        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(128L + Items.Sum(static item => 96L + 2L * (
                item.Text.Length + item.Route.Length + item.Path.Length + (item.Author?.Length ?? 0))));
    }
}
