using Diginsight.Diagnostics;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// SmartCache decorator over an inner <see cref="IContentSource"/>. It caches the Markdown
/// source-byte fetch — the expensive blob/file read that runs on every prerender and on every
/// WASM navigation via <c>/_content</c> — in-memory, optionally backed by Redis for
/// distributed, multi-instance sharing.
/// <para>
/// Only text Markdown keys (<c>.md</c>/<c>.qmd</c>) are cached; binary assets (images, downloads)
/// pass straight through so Redis is not bloated with large payloads. Listing/head calls delegate
/// to the inner source unchanged (navigation keeps its own cache).
/// </para>
/// </summary>
public sealed class CachedContentSource(
    IContentSource inner,
    IContentLister innerLister,
    ISmartCache smartCache,
    ILogger<CachedContentSource> logger) : IContentSource, IContentLister
{
    public async Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { contentKey });

        // Binary assets bypass the distributed cache — only Markdown source is worth caching.
        if (!IsCacheable(contentKey))
        {
            return await inner.GetAsync(contentKey, ct);
        }

        // Freshness (MaxAge / expirations) comes from Diginsight:SmartCache config — including the
        // class-aware MaxAge@CachedContentSource override — via the caller type below.
        // CoalesceRacingCacheMisses enables SmartCache single-flight: concurrent misses for the same
        // key share one origin fetch, so this decorator no longer needs its own in-flight guard.
        // The path-addressed key lets a content write invalidate this exact entry (and the menu
        // levels above it) via ContentPathInvalidationRule.
        var options = new SmartCacheOperationOptions { CoalesceRacingCacheMisses = true };
        var key = new ContentPathCacheKey("content", ContentPathCacheKey.Normalize(contentKey));

        CachedContent envelope = await smartCache.GetAsync(
            key,
            async innerCt => new CachedContent(await inner.GetAsync(contentKey, innerCt)),
            options,
            callerType: typeof(CachedContentSource),
            cancellationToken: ct);

        var result = envelope.Result;
        activity?.SetOutput(new { found = result is not null });
        return result;
    }

    public Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default) =>
        innerLister.ListChildrenAsync(prefix, ct);

    public Task<string?> ReadHeadAsync(string key, CancellationToken ct = default) =>
        innerLister.ReadHeadAsync(key, ct);

    private static bool IsCacheable(string contentKey) =>
        contentKey.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        contentKey.EndsWith(".qmd", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Serializable envelope so both hits and misses (a <c>null</c> <see cref="ContentResult"/>)
    /// round-trip through the in-memory and Redis stores.
    /// </summary>
    public sealed record CachedContent(ContentResult? Result);
}
