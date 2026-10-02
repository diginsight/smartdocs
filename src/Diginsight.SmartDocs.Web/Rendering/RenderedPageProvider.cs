using System.Text;
using Diginsight.SmartCache;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;
using Diginsight.Runtime;

namespace Diginsight.SmartDocs.Web.Rendering;

public sealed class RenderedPageProvider(
    IContentSource content,
    IMarkdownRenderer renderer,
    ISmartCache smartCache,
    BackgroundRevalidationCache revalidation,
    ContentFreshnessOptions freshness) : IRenderedPageResolver
{
    public async Task<RenderedPageResolution> ResolveRenderedAsync(
        string? routePath,
        CancellationToken cancellationToken = default)
    {
        string route = (routePath ?? string.Empty).Replace('\\', '/').Trim('/');
        var key = new ContentPathCacheKey("page", route);
        var options = new SmartCacheOperationOptions
        {
            CoalesceRacingCacheMisses = true,
            MaxAge = freshness.Content,
        };

        RenderedPageEnvelope envelope = await revalidation.GetAsync(
            key,
            freshness.Content,
            ct => smartCache.GetAsync(
                key,
                BuildAsync,
                options,
                callerType: typeof(RenderedPageProvider),
                cancellationToken: ct),
            cancellationToken);

        return envelope.Page is { } page
            ? RenderedPageResolution.Found(page)
            : RenderedPageResolution.Nothing;

        async Task<RenderedPageEnvelope> BuildAsync(CancellationToken ct)
        {
            foreach (string contentKey in PageLoader.Candidates(route))
            {
                ContentResult? result = await content.GetAsync(contentKey, ct);
                if (result is null)
                {
                    continue;
                }

                string markdown = Encoding.UTF8.GetString(result.Bytes);
                string contentDir = contentKey.Contains('/')
                    ? contentKey[..contentKey.LastIndexOf('/')]
                    : string.Empty;
                return new RenderedPageEnvelope(renderer.Render(markdown, contentDir));
            }

            return new RenderedPageEnvelope(null);
        }
    }

    private sealed record RenderedPageEnvelope(RenderedPage? Page) : ISizeableHeuristically
    {
        public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
            new(Page is null
                ? 64
                : 128L + 2L * (
                    Page.Html.Length +
                    Page.Title.Length +
                    Page.Toc.Sum(static item => item.Text.Length + item.Id.Length)));
    }
}
