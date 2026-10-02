using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Navigation;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Dynamic navigation API, built live from the content store by <see cref="CachedDynamicNavBuilder"/>:
/// one menu level per call, a monotonic version, a flattened article index (for menu search /
/// prev-next), and an invalidation hook for content writers.
/// </summary>
public static class NavEndpoints
{
    private static ILogger? cachedLogger;
    // Never null: a null logger reaches StartMethodActivity/SetOutput without a valid logger attached
    // and SetOutput throws "Invalid logger in activity" instead of silently no-op'ing.
    private static ILogger logger => cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(NavEndpoints)) ?? NullLogger.Instance;

    public static IEndpointRouteBuilder MapNavEndpoints(this IEndpointRouteBuilder app)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var group = app.MapGroup("/_nav");

        // A client abort (navigate away, refresh, timeout) cancels HttpContext.RequestAborted mid-request.
        // That is expected, not a fault, so translate it into 499 ("client closed request") instead of
        // letting a TaskCanceledException bubble up as an unhandled exception. The guard on RequestAborted
        // ensures a genuine internal timeout (a different token) still surfaces as a real error.
        group.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                logger.LogDebug("Nav request canceled by client.");
                return Results.StatusCode(499);
            }
        });

        group.MapGet("/children", GetNavChildrenAsync);
        group.MapGet("/folder", GetFolderAsync);
        group.MapGet("/version", GetNavVersion);
        group.MapGet("/total", GetNavTotal);
        group.MapGet("/index", GetNavIndexAsync);
        group.MapPost("/invalidate", InvalidateNavCache);
        return app;
    }

    private static async Task<IResult> GetNavChildrenAsync(
        string? prefix, INavBuilder nav, INavigationWarmupQueue warmup,
        FolderRecordProvider folders,
        IOptions<JsonOptions> json, HttpContext http, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        IReadOnlyList<NavChild> children = await nav.GetChildrenAsync(prefix ?? string.Empty, ct);
        FolderRecord[] records = await Task.WhenAll(children
            .Where(static child => child.IsSection && child.Prefix is not null)
            .Select(child => folders.GetAsync(child.Prefix!, ct)));
        var response = new NavLevelResponse(
            children.Select(static child => child.IsSection && child.Prefix is not null
                ? NavLevelReference.Folder(child.Prefix)
                : NavLevelReference.Article(child)).ToArray(),
            records);

        warmup.EnqueueLevels(prefix ?? string.Empty, 3);

        activity?.SetOutput(new { count = children.Count });

        // Serialized here rather than by Results.Json so the entity tag covers exactly the bytes sent:
        // a browser holding the same level revalidates it with a 304 instead of downloading it again.
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(response, json.Value.SerializerOptions);
        if (HttpValidators.IsNotModified(http, HttpValidators.Tag(body), HttpValidators.Revalidate))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(body, "application/json; charset=utf-8");
    }

    private static async Task<IResult> GetFolderAsync(
        string? prefix,
        FolderRecordProvider folders,
        IOptions<JsonOptions> json, HttpContext http,
        CancellationToken cancellationToken)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        FolderRecord record = await folders.GetAsync(prefix ?? string.Empty, cancellationToken);

        // Tagged like /_nav/children, over the exact bytes sent: a folder record is the unit the
        // browser keeps and re-reads, so a repeat read of an unchanged record must cost a 304.
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(record, json.Value.SerializerOptions);
        if (HttpValidators.IsNotModified(http, HttpValidators.Tag(body), HttpValidators.Revalidate))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(body, "application/json; charset=utf-8");
    }

    private static IResult GetNavVersion()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        return Results.Json(new { version = CachedDynamicNavBuilder.Version });
    }

    private static IResult GetNavTotal(FolderMetricsIndex metrics)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        return metrics.TryGet(string.Empty) is { } site
            ? Results.Json(new FolderArticleStats(site.Count, site.Latest, null, site.Coverage))
            : Results.NoContent();
    }

    private static async Task<IResult> GetNavIndexAsync(INavBuilder nav, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        // Client-abort cancellation is handled centrally by the /_nav group endpoint filter.
        return Results.Json(await nav.GetIndexAsync(ct));
    }

    private static IResult InvalidateNavCache(
        HttpContext http, string? path,
        IOptions<SiteOptions> siteOptions,
        CachedDynamicNavBuilder nav, NavChangePublisher publisher, INavigationWarmupQueue warmup)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { path });

        // The endpoint drops the whole navigation tree of a public site, so it is guarded whenever a
        // key is configured. When no key is configured the endpoint stays open, which is what a local
        // run needs — the guard is therefore opt-in by configuration, not by environment sniffing.
        string configuredKey = siteOptions.Value.InvalidateApiKey;
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string presented = http.Request.Headers["X-Invalidate-Key"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(configuredKey)))
            {
                logger.LogWarning("Rejected nav invalidation: missing or invalid X-Invalidate-Key.");
                return Results.Unauthorized();
            }
        }

        // No path → whole cache (content + nav, every node); a path → just that branch.
        if (string.IsNullOrWhiteSpace(path))
        {
            nav.Invalidate();
        }
        else
        {
            nav.Invalidate(path);
        }

        // Recompute the affected folder aggregates and push them to connected clients (debounced),
        // so sidebar counts and the footer total update live without polling.
        publisher.PublishChangeAsync(path ?? string.Empty);

        // Rebuild what was just dropped before a reader asks for it. The caller is a publish
        // pipeline waiting on this response, so the warm runs behind it rather than inside it.
        warmup.EnqueueAll();

        return Results.Ok(new { version = CachedDynamicNavBuilder.Version });
    }
}
