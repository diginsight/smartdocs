using Diginsight.Diagnostics;
using Diginsight.Runtime;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;

namespace Diginsight.SmartDocs.Web.Rendering;

public sealed class RenderedPageProvider(
    CachedContentSource content,
    ISmartCache smartCache,
    MetadataInvalidationTracker invalidations,
    ContentFreshnessOptions freshness,
    ILogger<RenderedPageProvider> logger) : IRenderedPageResolver
{
    private const string RouteCacheKind = "page-route-v1";
    private readonly string origin = Guid.NewGuid().ToString("N");

    public async Task<RenderedPageResolution> ResolveRenderedAsync(
        string? routePath,
        CancellationToken cancellationToken = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { routePath });

        string route = (routePath ?? string.Empty).Replace('\\', '/').Trim('/');
        var key = new ContentPathCacheKey(RouteCacheKind, route);
        RouteRecord routeRecord = await GetRouteAsync(key, route, cancellationToken);
        if (routeRecord.ContentKey is not { } contentKey)
        {
            activity?.SetOutput(new { found = false });
            return RenderedPageResolution.Nothing;
        }

        CachedContentSource.CachedArticleRecord article =
            await content.GetArticleRecordAsync(contentKey, cancellationToken);
        activity?.SetOutput(new { found = article.Page is not null });

        return article.Page is { } page && article.ResponseBytes is { } responseBytes
            ? RenderedPageResolution.Found(page, contentKey, responseBytes, article.ResponseETag)
            : RenderedPageResolution.Nothing;
    }

    private async Task<RouteRecord> GetRouteAsync(
        ContentPathCacheKey key, string route, CancellationToken cancellationToken)
    {
        RouteRecord record = await ReadRouteAsync(key, route, freshness.Content, cancellationToken);
        if (record.ContentKey is null && freshness.Missing < freshness.Content)
        {
            record = await ReadRouteAsync(key, route, freshness.Missing, cancellationToken);
        }

        return record;
    }

    private async Task<RouteRecord> ReadRouteAsync(
        ContentPathCacheKey key, string route, TimeSpan maxAge, CancellationToken cancellationToken)
    {
        bool forceOrigin = false;
        while (true)
        {
            var options = new SmartCacheOperationOptions
            {
                CoalesceRacingCacheMisses = true,
                MaxAge = forceOrigin ? TimeSpan.Zero : maxAge,
            };

            RouteRecord record = await smartCache.GetAsync(
                key,
                async ct =>
                {
                    long started = invalidations.Generation(key);
                    string? resolvedKey = await ResolveContentKeyAsync(route, ct);
                    return new RouteRecord(resolvedKey, origin, started);
                },
                options,
                callerType: typeof(RenderedPageProvider),
                cancellationToken: cancellationToken);

            long current = invalidations.Generation(key);
            if ((record.Origin == origin && record.Generation != current) ||
                (record.Origin != origin && current != 0))
            {
                forceOrigin = true;
                continue;
            }

            return record;
        }
    }

    private async Task<string?> ResolveContentKeyAsync(string route, CancellationToken ct)
    {
        foreach (string candidate in PageLoader.Candidates(route))
        {
            CachedContentSource.CachedArticleRecord article = await content.GetArticleRecordAsync(candidate, ct);
            if (article.Content is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed record RouteRecord(string? ContentKey, string Origin, long Generation) : ISizeableHeuristically
    {
        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(96L + 2L * ((ContentKey?.Length ?? 0) + Origin.Length));
    }
}
