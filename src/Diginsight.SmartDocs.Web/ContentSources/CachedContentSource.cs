using Diginsight.Diagnostics;
using Diginsight.Runtime;
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
/// Listings and parsed headers are cached separately, on a shorter structural tolerance: they are the
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
    /// Raw header text, uncached. Navigation reads headers through <see cref="ReadArticleHeadAsync"/>
    /// and <see cref="ReadFolderMetaAsync"/>, which cache the parsed fields: the text itself — the
    /// first 8 KB of every article — once filled most of the cache to keep a few hundred bytes of it.
    /// </summary>
    public Task<string?> ReadHeadAsync(string key, CancellationToken ct = default) =>
        innerLister.ReadHeadAsync(key, ct);

    /// <summary>
    /// Cached, parsed article header. Same tolerance as the listing: a level's labels and dates come
    /// from these reads, and a folder's children are re-scored on every level rebuild.
    /// </summary>
    public async Task<ArticleHead> ReadArticleHeadAsync(string key, CancellationToken ct = default)
    {
        // Not "head": that kind held the raw header text, and a Redis store or a companion still on the
        // previous version could hand such an entry back to be read as a parsed header.
        var cacheKey = new ContentPathCacheKey("article-head", ContentPathCacheKey.Normalize(key));

        CachedArticleHead envelope = await smartCache.GetAsync(
            cacheKey,
            async innerCt => new CachedArticleHead(FrontMatter.ParseHead(await innerLister.ReadHeadAsync(key, innerCt))),
            StructureOptions(),
            callerType: typeof(CachedContentSource),
            cancellationToken: ct);

        return envelope.Head;
    }

    /// <summary>Cached, parsed <c>metadata.yml</c> overrides, on the same tolerance as the listing.</summary>
    public async Task<FolderMeta> ReadFolderMetaAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = new ContentPathCacheKey("folder-meta", ContentPathCacheKey.Normalize(key));

        CachedFolderMeta envelope = await smartCache.GetAsync(
            cacheKey,
            async innerCt => new CachedFolderMeta(FolderMeta.Parse(await innerLister.ReadHeadAsync(key, innerCt))),
            StructureOptions(),
            callerType: typeof(CachedContentSource),
            cancellationToken: ct);

        return envelope.Meta;
    }

    private SmartCacheOperationOptions StructureOptions() => new()
    {
        CoalesceRacingCacheMisses = true,
        MaxAge = freshness.Structure,
    };

    private static bool IsCacheable(string contentKey) =>
        contentKey.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        contentKey.EndsWith(".qmd", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Serializable envelope so both hits and misses (a <c>null</c> <see cref="ContentResult"/>)
    /// round-trip through the in-memory and Redis stores.
    /// <para>
    /// It reports its own size. SmartCache measures an entry by walking it, which for a byte array
    /// means one boxed visit per byte; and it derives eviction priority from size alone. An article
    /// body is the cheapest entry to lose — a miss costs one origin read, where a lost level or header
    /// costs a rebuild — so a body reports at least the low-priority threshold and is compacted before
    /// any record when the cache reaches its cap.
    /// </para>
    /// </summary>
    public sealed record CachedContent(ContentResult? Result) : ISizeableHeuristically
    {
        // The library's default threshold. A deployment that overrides LowPrioritySizeThreshold keeps
        // bodies sized honestly but no longer pinned to the lowest priority.
        private static readonly long LowPriorityFloor = new SmartCacheCoreOptions().LowPrioritySizeThreshold;

        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet)
        {
            long size = Result is { } r
                ? r.Bytes.LongLength + sizeof(char) * ((r.ContentType?.Length ?? 0) + (r.ETag?.Length ?? 0))
                : 0;
            return new HeuristicSizeResult(Math.Max(size, LowPriorityFloor));
        }
    }

    /// <summary>Serializable envelope for a folder listing (an array, so it round-trips through Redis).</summary>
    public sealed record CachedChildren(ChildEntry[] Items);

    /// <summary>Serializable envelope for a parsed article header; an absent file parses to <see cref="ArticleHead.Empty"/>.</summary>
    public sealed record CachedArticleHead(ArticleHead Head);

    /// <summary>Serializable envelope for parsed <c>metadata.yml</c> overrides; an absent file parses to <see cref="FolderMeta.None"/>.</summary>
    public sealed record CachedFolderMeta(FolderMeta Meta);
}
