using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Diginsight.SmartDocs.Web.Sites;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Diginsight.SmartDocs.Web.Endpoints;

public static class SiteEndpoints
{
    private static ILogger? cachedLogger;
    private static ILogger logger => (cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(SiteEndpoints))) ?? NullLogger.Instance;

    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_site", async (IOptions<SiteOptions> siteOptions, BrandingAssets branding, SignInConfiguration signIn, ReaderAccess reader, HttpContext http) =>
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { endpoint = "/_site" });

            // Only the spaces the reader may see; the generated space index follows.
            http.Response.Headers.CacheControl = HttpValidators.RevalidatePrivate;
            return Results.Json(ForReader(
                SiteShellOptions.From(siteOptions.Value, branding.LogoUrl, signIn.Enabled),
                await reader.GetVisibleSpacesAsync(http.RequestAborted)));
        });

        app.MapGet("/robots.txt", async (IOptions<SiteOptions> siteOptions, SpaceVisibilityEvaluator evaluator, HttpContext http) =>
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { endpoint = "/robots.txt" });

            // Built for an anonymous reader, whoever asks, so the answer is the same for everyone and
            // stays publicly cacheable.
            ReaderVisibility anonymous = await evaluator.EvaluateAsync(RoleSet.Anonymous, http.RequestAborted);
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Text(BuildRobots(siteOptions.Value, anonymous), "text/plain; charset=utf-8");
        });

        // The publisher's mark, on one URL that does not name a space. The shell shows the same
        // header on every page — including the generated index, which has no space to resolve
        // against — so the mark cannot be addressed through the space-mounted content namespace.
        app.MapGet(BrandingAssets.LogoRoute, async (
            BrandingAssets branding,
            SpaceContentRegistry spaces,
            ContentFreshnessOptions freshness,
            HttpContext http,
            CancellationToken ct) =>
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { endpoint = BrandingAssets.LogoRoute });

            if (branding.LogoSpaceId is not { Length: > 0 } spaceId
                || branding.LogoContentKey is not { Length: > 0 } key
                || !spaces.TryGet(spaceId, out SpaceContentAccess access))
            {
                return Results.NotFound();
            }

            ContentResult? result = await access.Source.GetAsync(key, ct);
            if (result is null)
            {
                return Results.NotFound();
            }

            // The same staleness bound the site applies to any other content read, restated to the
            // browser so the mark is fetched once a session instead of once a navigation.
            http.Response.Headers.CacheControl =
                $"public, max-age={(int)freshness.Content.TotalSeconds}";

            return Results.Bytes(result.Bytes, result.ContentType ?? "application/octet-stream");
        });

        return app;
    }

    /// <summary>Filters the site shell to a previously resolved set of visible spaces.</summary>
    public static SiteShellOptions ForReader(SiteShellOptions shell, IReadOnlyList<SpaceOptions> visibleSpaces)
    {
        shell.Spaces = shell.Spaces.Where(space => visibleSpaces.Any(visible =>
            string.Equals(visible.Id, space.Id, StringComparison.OrdinalIgnoreCase))).ToList();
        return shell;
    }

    /// <summary>
    /// The crawler policy. A space an anonymous reader can't see is omitted entirely, so the file
    /// names no restricted mount. A private site, whose default requirement isn't empty, disallows
    /// everything for every agent instead.
    /// </summary>
    private static string BuildRobots(SiteOptions site, ReaderVisibility anonymous)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        if (!string.IsNullOrWhiteSpace(site.Authorization.DefaultRequirement))
        {
            return new StringBuilder()
                .AppendLine("# This site is private. Nothing may be crawled.")
                .AppendLine("User-agent: *")
                .AppendLine("Disallow: /")
                .ToString();
        }

        string[] applicationPaths = ["/_framework/", "/_nav/", "/_page/", "/_blazor/", "/_auth/"];
        var text = new StringBuilder()
            .AppendLine("# Published pages may be crawled. Application endpoints may not.")
            .AppendLine("User-agent: *");

        foreach (string path in applicationPaths)
        {
            text.Append("Disallow: ").AppendLine(path);
        }

        text.AppendLine()
            .AppendLine("User-agent: GPTBot");

        foreach (string path in applicationPaths)
        {
            text.Append("Disallow: ").AppendLine(path);
        }

        foreach (SpaceOptions space in site.Spaces)
        {
            if (!anonymous.Spaces.TryGetValue(space.Id, out SpaceVisibility? visibility) || !visibility.IsVisible)
            {
                continue;
            }

            string path = space.IsRootMounted ? "/" : $"{space.NormalizedRouteBase}/";
            text.Append(space.AllowGptBot ? "Allow: " : "Disallow: ").AppendLine(path);
        }

        return text.ToString();
    }
}