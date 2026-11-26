using System.Text;
using System.Text.Json;
using Diginsight.Diagnostics;
using Diginsight.Runtime;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Endpoints;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;

namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// SmartCache decorator over an inner <see cref="IContentSource"/>. One versioned article entry
/// carries its source bytes, parsed navigation head, rendered page, and serialized response.
/// <para>
/// Only text Markdown keys (<c>.md</c>/<c>.qmd</c>) are cached for the full-content fetch; binary
/// assets (images, downloads) pass straight through. A menu can fill only the bounded head and a page
/// promotes that same entry to the full article representation.
/// </para>
/// </summary>
public sealed class CachedContentSource(
    IContentSource inner,
    IContentLister innerLister,
    ISmartCache smartCache,
    MetadataInvalidationTracker invalidations,
    ContentFreshnessOptions freshness,
    ContentMetadataProvider metadata,
    IMarkdownRenderer renderer,
    ILogger<CachedContentSource> logger) : IContentSource, IContentLister
{
    private const string ArticleCacheKind = "article-v1";
    private readonly string origin = Guid.NewGuid().ToString("N");

    public async Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { contentKey });

        // Binary assets bypass the distributed cache — only Markdown source is worth caching.
        if (!IsCacheable(contentKey))
        {
            return await inner.GetAsync(contentKey, ct);
        }

        CachedArticleRecord record = await GetArticleRecordAsync(contentKey, ct);
        activity?.SetOutput(new { found = record.Content is not null });
        return record.Content;
    }

    public Task<CachedArticleRecord> GetArticleRecordAsync(string contentKey, CancellationToken ct = default)
    {
        if (!IsCacheable(contentKey))
        {
            throw new ArgumentException("Only Markdown content has an article cache record.", nameof(contentKey));
        }

        var key = new ContentPathCacheKey(ArticleCacheKind, ContentPathCacheKey.Normalize(contentKey));
        CachedArticleRecord? previous = smartCache.TryGetDirectFromMemory(
            key, out _, out object? cached) ? cached as CachedArticleRecord : null;

        return GetRecordAsync();

        async Task<CachedArticleRecord> GetRecordAsync()
        {
            async Task<CachedArticleRecord> BuildAsync(CancellationToken innerCt)
            {
                ContentResult? content;
                if (previous?.Content is { } previousContent &&
                    inner is IConditionalContentSource conditionalSource)
                {
                    ContentValidationResult validation = await conditionalSource.GetIfChangedAsync(
                        contentKey, previousContent.ETag, innerCt);
                    content = validation.NotModified ? previousContent : validation.Content;
                }
                else
                {
                    content = await inner.GetAsync(contentKey, innerCt);
                }

                if (content is null)
                {
                    return CachedArticleRecord.Missing;
                }

                if (previous is { Content: { } priorContent, Page: not null, ResponseBytes: not null } &&
                    string.Equals(priorContent.ETag, content.ETag, StringComparison.Ordinal))
                {
                    return previous with { Content = content };
                }

                string leadingText = await FrontMatter.ReadHeadAsync(
                    new MemoryStream(content.Bytes, writable: false), innerCt);
                ArticleHead head = FrontMatter.ParseHead(leadingText);

                string contentDir = contentKey.Contains('/')
                    ? contentKey[..contentKey.LastIndexOf('/')]
                    : string.Empty;
                RenderedPage page = renderer.Render(Encoding.UTF8.GetString(content.Bytes), contentDir);
                byte[] responseBytes = JsonSerializer.SerializeToUtf8Bytes(page);
                return new CachedArticleRecord(
                    true,
                    content,
                    head,
                    page,
                    responseBytes,
                    HttpValidators.Tag(responseBytes));
            }

            CachedArticleRecord record = await ReadRecordAsync(key, freshness.Content, BuildAsync, ct);
            if (record.Exists && record.Content is null)
            {
                record = await ReadRecordAsync(key, TimeSpan.Zero, BuildAsync, ct);
            }

            if (!record.Exists && freshness.Missing < freshness.Content)
            {
                record = await ReadRecordAsync(key, freshness.Missing, BuildAsync, ct);
            }

            return record;
        }
    }

    private async Task<CachedArticleRecord> ReadHeadRecordAsync(string contentKey, CancellationToken ct)
    {
        var key = new ContentPathCacheKey(ArticleCacheKind, ContentPathCacheKey.Normalize(contentKey));

        async Task<CachedArticleRecord> BuildAsync(CancellationToken innerCt)
        {
            string? leadingText = await innerLister.ReadHeadAsync(contentKey, innerCt);
            return leadingText is null
                ? CachedArticleRecord.Missing
                : new CachedArticleRecord(true, null, FrontMatter.ParseHead(leadingText), null, null, null);
        }

        CachedArticleRecord record = await ReadRecordAsync(key, freshness.Content, BuildAsync, ct);
        if (!record.Exists && freshness.Missing < freshness.Structure)
        {
            record = await ReadRecordAsync(key, freshness.Missing, BuildAsync, ct);
        }

        return record;
    }

    private async Task<CachedArticleRecord> ReadRecordAsync(
        ContentPathCacheKey key,
        TimeSpan maxAge,
        Func<CancellationToken, Task<CachedArticleRecord>> valueFactory,
        CancellationToken ct)
    {
        bool forceOrigin = false;
        while (true)
        {
            var options = new SmartCacheOperationOptions
            {
                CoalesceRacingCacheMisses = true,
                MaxAge = forceOrigin ? TimeSpan.Zero : maxAge,
            };

            CachedArticleRecord record = await smartCache.GetAsync(
                key,
                async cacheCt =>
                {
                    long started = invalidations.Generation(key);
                    CachedArticleRecord created = await valueFactory(cacheCt);
                    return created with { Origin = origin, Generation = started };
                },
                options,
                callerType: typeof(CachedContentSource),
                cancellationToken: ct);
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

    /// <summary>
    /// Cached folder listing. This is the read every other layer sits on: a menu level is one
    /// listing plus a head read per child, and the whole-tree walk repeats that for every folder,
    /// so the same folders were being enumerated several times over within one scan. Keyed by
    /// path like the content entries, so the existing branch-scoped invalidation drops it too.
    /// </summary>
    public async Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

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

        activity?.SetOutput(new { count = envelope.Items.Length });
        return envelope.Items;
    }

    /// <summary>
    /// Raw header text, uncached. Navigation uses <see cref="ReadArticleHeadAsync"/> to keep only
    /// parsed fields in the article's SmartCache entry.
    /// </summary>
    public Task<string?> ReadHeadAsync(string key, CancellationToken ct = default) =>
        innerLister.ReadHeadAsync(key, ct);

    /// <summary>
    /// Cached, parsed article header. A page read promotes this same entry with source and rendered
    /// representations; the header read itself remains bounded.
    /// </summary>
    public async Task<ArticleHead> ReadArticleHeadAsync(string key, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { key });
        CachedArticleRecord record = await ReadHeadRecordAsync(key, ct);
        activity?.SetOutput(new { hasTitle = record.Head.Title is not null, record.Head.Date });
        return record.Head;
    }

    /// <summary>Navigation-only projection of the shared metadata snapshot.</summary>
    public async Task<FolderMeta> ReadFolderMetaAsync(string key, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { key });

        MetadataOutcome outcome = await metadata.GetForMetadataKeyAsync(key, ct);
        activity?.SetOutput(new { outcome.Status });
        return outcome.Document?.Navigation ?? FolderMeta.None;
    }

    private static bool IsCacheable(string contentKey) =>
        contentKey.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        contentKey.EndsWith(".qmd", StringComparison.OrdinalIgnoreCase);

    /// <summary>Serializable article payload, with an explicit size estimate for cache eviction.</summary>
    public sealed record CachedArticleRecord(
        bool Exists,
        ContentResult? Content,
        ArticleHead Head,
        RenderedPage? Page,
        byte[]? ResponseBytes,
        string? ResponseETag,
        string? Origin = null,
        long Generation = 0) : ISizeableHeuristically
    {
        public static CachedArticleRecord Missing { get; } =
            new(false, null, ArticleHead.Empty, null, null, null);

        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet)
        {
            long size = 192L + (Content?.Bytes.LongLength ?? 0) + (ResponseBytes?.LongLength ?? 0);
            size += 2L * (
                (Content?.ContentType?.Length ?? 0) +
                (Content?.ETag.Length ?? 0) +
                (Head.Title?.Length ?? 0) +
                (Head.Author?.Length ?? 0) +
                (Page?.Html.Length ?? 0) +
                (Page?.Title.Length ?? 0) +
                (ResponseETag?.Length ?? 0));
            if (Page is { } page)
            {
                size += page.Toc.Sum(static item => 2L * (item.Text.Length + item.Id.Length));
            }

            return new HeuristicSizeResult(size);
        }
    }

    /// <summary>Serializable envelope for a folder listing (an array, so it round-trips through Redis).</summary>
    public sealed record CachedChildren(ChildEntry[] Items) : ISizeableHeuristically
    {
        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(128L + Items.Sum(static item =>
                64L + 2L * (item.Name.Length + item.Path.Length)));
    }

}
