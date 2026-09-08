using Diginsight.Components.Azure.Extensions;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        app.MapGet("/_page", (IContentSource source, HttpContext http, CancellationToken ct) =>
            ResolvePageAsync(null, source, http, ct));
        app.MapGet("/_page/{**path}", ResolvePageAsync);
        return app;
    }

    private static async Task<IResult> GetContentRawAsync(string key, IContentSource source, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { key });

        ContentResult? result = await source.GetAsync(key, ct);
        activity?.SetOutput(new { found = result is not null });
        return result is null
            ? Results.NotFound()
            : Results.Bytes(result.Bytes, result.ContentType ?? "text/markdown; charset=utf-8");
    }

    /// <summary>
    /// Resolves a route to the file that backs it and returns its bytes in one call. A route may be
    /// backed by any of several candidate names, and the WASM client paid a round trip — and left a
    /// 404 in the browser console — for every one that did not exist. The candidate order is
    /// <see cref="PageLoader.Candidates"/> itself, so the two paths cannot drift apart.
    /// </summary>
    private static async Task<IResult> ResolvePageAsync(
        string? path, IContentSource source, HttpContext http, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { path });

        foreach (string key in PageLoader.Candidates(path))
        {
            ContentResult? result = await source.GetAsync(key, ct);
            if (result is null)
            {
                continue;
            }

            activity?.SetOutput(new { key });
            // The renderer resolves links and images relative to the file the Markdown came from,
            // which the client can no longer infer once the probing happens here.
            http.Response.Headers[ContentKeyHeader] = key;
            return Results.Bytes(result.Bytes, result.ContentType ?? "text/markdown; charset=utf-8");
        }

        activity?.SetOutput(new { key = (string?)null });
        // Not 404: the client has to tell "no page backs this route" (normal for a section folder)
        // apart from "this server has no such endpoint", which is what a 404 here would mean to a
        // client newer than the server it is talking to.
        return Results.NoContent();
    }
}
