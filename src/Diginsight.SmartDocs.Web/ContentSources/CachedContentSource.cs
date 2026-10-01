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
/// Only text Markdown keys (<c>.md</c>/<c>.qmd</c>) are cached for the full-content fetch; binary
/// assets (images, downloads) pass straight through so Redis is not bloated with large payloads.
/// Listing and head reads are cached separately, on a shorter structural tolerance: they are the
/// primitives navigation is built from, and every layer above them repeats the same few calls.
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

    /// <summary>
    /// Cached folder listing. This is the read every other layer sits on: a menu level is one
    /// listing plus a head read per child, and the whole-tree walk repeats that for every folder,
    /// so the same folders were being enumerated several times over within one scan. Keyed by
    /// path like the content entries, so the existing branch-scoped invalidation drops it too.
    /// </summary>
    public async Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
    {
        var key = new ContentPathCacheKey("children", ContentPathCacheKey.Normalize(prefix));

        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.Structure,
        };

        CachedChildren envelope = await smartCache.GetAsync(
            key,
            async innerCt => new CachedChildren((await innerLister.ListChildrenAsync(prefix, innerCt))?.ToArray() ?? []),
            options,
            callerType: typeof(CachedContentSource),
            cancellationToken: ct);

        return envelope.Items;
    }

    /// <summary>
    /// Cached front-matter head. Same tolerance as the listing: a level's labels and dates come
    /// from these reads, and a folder's children are re-scored on every level rebuild.
    /// </summary>
    public async Task<string?> ReadHeadAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = new ContentPathCacheKey("head", ContentPathCacheKey.Normalize(key));

        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.Structure,
        };

        CachedHead envelope = await smartCache.GetAsync(
            cacheKey,
            async innerCt => new CachedHead(await innerLister.ReadHeadAsync(key, innerCt)),
            options,
            callerType: typeof(CachedContentSource),
            cancellationToken: ct);

        return envelope.Text;
    }

    private static bool IsCacheable(string contentKey) =>
        contentKey.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        contentKey.EndsWith(".qmd", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Serializable envelope so both hits and misses (a <c>null</c> <see cref="ContentResult"/>)
    /// round-trip through the in-memory and Redis stores.
    /// </summary>
    public sealed record CachedContent(ContentResult? Result);

    /// <summary>Serializable envelope for a folder listing (an array, so it round-trips through Redis).</summary>
    public sealed record CachedChildren(ChildEntry[] Items);

    /// <summary>Serializable envelope for a head read, so an absent file caches as a miss rather than re-probing.</summary>
    public sealed record CachedHead(string? Text);
}
