using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Diginsight.SmartDocs.Web.Sites;
using Microsoft.Extensions.Options;
using System.Text;

namespace Diginsight.SmartDocs.Web.Endpoints;

public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_site", (IOptions<SiteOptions> siteOptions, BrandingAssets branding) =>
            Results.Json(SiteShellOptions.From(siteOptions.Value, branding.LogoUrl)));

        app.MapGet("/robots.txt", (IOptions<SiteOptions> siteOptions, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Text(BuildRobots(siteOptions.Value), "text/plain; charset=utf-8");
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

    private static string BuildRobots(SiteOptions site)
    {
        string[] applicationPaths = ["/_framework/", "/_nav/", "/_page/", "/_blazor/"];
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
            string path = space.IsRootMounted ? "/" : $"{space.NormalizedRouteBase}/";
            text.Append(space.AllowGptBot ? "Allow: " : "Disallow: ").AppendLine(path);
        }

        return text.ToString();
    }
}