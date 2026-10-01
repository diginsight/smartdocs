using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Diginsight.SmartDocs.Web.Sites;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web.Endpoints;

public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_site", (IOptions<SiteOptions> siteOptions, BrandingAssets branding) =>
            Results.Json(SiteShellOptions.From(siteOptions.Value, branding.LogoUrl)));

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
}