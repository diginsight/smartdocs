using Diginsight.Components.Azure.Extensions;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Raw Markdown/asset passthrough endpoint (<c>/_content/{**key}</c>) consumed by the WASM
/// client's <c>HttpContentSource</c> to fetch content bytes from the server-side content store.
/// </summary>
public static class ContentEndpoints
{
    private static ILogger? cachedLogger;
    // Never null: a null logger reaches StartMethodActivity/SetOutput without a valid logger attached
    // and SetOutput throws "Invalid logger in activity" instead of silently no-op'ing.
    private static ILogger logger => cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(ContentEndpoints)) ?? NullLogger.Instance;

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_content/{**key}", GetContentRawAsync);
        return app;
    }

    private static async Task<IResult> GetContentRawAsync(
        HttpContext http,
        string key,
        IContentSource source,
        IOptionsMonitor<SiteOptions> siteOptions,
        CancellationToken ct)
    {
        ContentCacheOptions cache = siteOptions.CurrentValue.ContentCache;

        using var activity = Observability.ActivitySource.StartMethodActivity(
            logger,
            () => new { key, cache.ConditionalRequestsEnabled, cache.MaxAgeSeconds });

        ContentResult? result = await source.GetAsync(key, ct);
        activity?.SetOutput(new { found = result is not null });
        if (result is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = cache.MaxAgeSeconds > 0
            ? $"public, max-age={cache.MaxAgeSeconds}"
            : "no-cache";

        if (cache.ConditionalRequestsEnabled && !string.IsNullOrWhiteSpace(result.ETag))
        {
            http.Response.Headers.ETag = result.ETag;
            string presented = http.Request.Headers.IfNoneMatch.ToString();
            if (string.Equals(presented, result.ETag, StringComparison.Ordinal) ||
                string.Equals(presented, "*", StringComparison.Ordinal))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
        }

        return Results.Bytes(result.Bytes, result.ContentType ?? "text/markdown; charset=utf-8");
    }
}
