using Diginsight.Components.Azure.Extensions;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    private static async Task<IResult> GetContentRawAsync(string key, IContentSource source, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { key });

        ContentResult? result = await source.GetAsync(key, ct);
        activity?.SetOutput(new { found = result is not null });
        return result is null
            ? Results.NotFound()
            : Results.Bytes(result.Bytes, result.ContentType ?? "text/markdown; charset=utf-8");
    }
}
