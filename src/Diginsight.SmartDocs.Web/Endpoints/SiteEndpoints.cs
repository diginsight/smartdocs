using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web.Endpoints;

public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_site", (IOptions<SiteOptions> siteOptions) =>
            Results.Json(SiteShellOptions.From(siteOptions.Value)));

        return app;
    }
}