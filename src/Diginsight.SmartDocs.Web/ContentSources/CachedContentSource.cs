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
    ContentFreshnessOptions freshness,
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

        // The path-addressed key lets a content write invalidate this exact entry (and the menu
        // levels above it) via ContentPathInvalidationRule.
        var key = new ContentPathCacheKey("content", ContentPathCacheKey.Normalize(contentKey));

        CachedContent envelope = await ReadAsync(key, contentKey, freshness.Content, ct);

        // A cached "not found" is the one that hides a just-published article, so it is given a
        // shorter tolerance than a cached hit. Re-reading with that tolerance returns the same entry
        // untouched when it is recent, and goes back to the origin only when it is not — so the
        // second lookup costs an origin read exactly when the answer might have become wrong.
        if (envelope.Result is null && freshness.Missing < freshness.Content)
        {
            envelope = await ReadAsync(key, contentKey, freshness.Missing, ct);
        }

        var result = envelope.Result;
        activity?.SetOutput(new { found = result is not null });
        return result;
    }

    private Task<CachedContent> ReadAsync(
        ContentPathCacheKey key, string contentKey, TimeSpan maxAge, CancellationToken ct)
    {
        // CoalesceRacingCacheMisses enables SmartCache single-flight: concurrent misses for the same
        // key share one origin fetch, so this decorator needs no in-flight guard of its own.
        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = maxAge,
        };

        return smartCache.GetAsync(
            key,
            async innerCt => new CachedContent(await inner.GetAsync(contentKey, innerCt)),
            options,
            callerType: typeof(CachedContentSource),
            cancellationToken: ct);
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
