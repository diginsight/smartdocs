using Diginsight.Components.Azure.Extensions;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Rendering;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Raw Markdown/asset passthrough endpoint (<c>/_content/{**key}</c>) consumed by the WASM
/// client's <c>HttpContentSource</c> to fetch content bytes from the server-side content store.
/// </summary>
public static class ContentEndpoints
{
    /// <summary>Carries the content key a route resolved to, back to the client.</summary>
    public const string ContentKeyHeader = "X-Content-Key";

    private static ILogger? cachedLogger;
    // Never null: a null logger reaches StartMethodActivity/SetOutput without a valid logger attached
    // and SetOutput throws "Invalid logger in activity" instead of silently no-op'ing.
    private static ILogger logger => cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(ContentEndpoints)) ?? NullLogger.Instance;

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_content/{**key}", GetContentRawAsync);
        app.MapGet("/_page", (RenderedPageProvider pages, HttpContext http, CancellationToken ct) =>
            ResolvePageAsync(null, pages, http, ct));
        app.MapGet("/_page/{**path}", ResolvePageAsync);
        return app;
    }

    private static async Task<IResult> GetContentRawAsync(
        string key, IContentSource source, ContentFreshnessOptions freshness, HttpContext http, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { key });

        ContentResult? result = await source.GetAsync(key, ct);
        activity?.SetOutput(new { found = result is not null });
        if (result is null)
        {
            return Results.NotFound();
        }

        // Images and attachments: the browser may reuse its copy for the same tolerance the server
        // applies to content, and revalidates it afterwards instead of downloading it again. Markdown
        // read here — the client's fallback when it can't resolve a route — revalidates every time,
        // because an article invalidated by a publish must not linger in the browser.
        string etag = HttpValidators.Tag(key, result.ETag);
        string cacheControl = NavRules.IsMarkdown(key)
            ? HttpValidators.Revalidate
            : $"public, max-age={(int)freshness.Content.TotalSeconds}";
        if (HttpValidators.IsNotModified(http, etag, cacheControl))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(result.Bytes, result.ContentType ?? "text/markdown; charset=utf-8");
    }

    /// <summary>
    /// Resolves a route to the file that backs it and returns its bytes in one call. A route may be
    /// backed by any of several candidate names, and the WASM client paid a round trip — and left a
    /// 404 in the browser console — for every one that did not exist. The candidate order is
    /// <see cref="PageLoader.Candidates"/> itself, so the two paths cannot drift apart.
    /// </summary>
    private static async Task<IResult> ResolvePageAsync(
        string? path, RenderedPageProvider pages, HttpContext http, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { path });

        RenderedPageResolution resolution = await pages.ResolveRenderedAsync(path, ct);
        if (resolution.Page is not { } page)
        {
            activity?.SetOutput(new { found = false });
            return Results.NoContent();
        }

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(page);
        activity?.SetOutput(new { found = true });
        if (HttpValidators.IsNotModified(http, HttpValidators.Tag(body), HttpValidators.Revalidate))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(body, "application/json; charset=utf-8");
    }
}
