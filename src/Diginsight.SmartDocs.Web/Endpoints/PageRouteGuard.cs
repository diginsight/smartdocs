using System.Text;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.StaticFiles;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Answers a page request for a route that names nothing in the content with a real 404, before the
/// page router prerenders it. The router's catch-all route claims every path, so an unknown route
/// used to cost a full prerender — the menus, the Markdown probes, the layout — and to come back as
/// a "Not found" page with status 200 and a full set of relative menu links. A crawler that resolves
/// those links against the page's own URL instead of the document base turns every such page into
/// dozens of new unknown routes: a trap with no end, which kept a deployed instance's only core busy
/// around the clock. A 404 ends the trap at its first step, and costs a few cached listings.
/// <para>
/// A route exists when every segment but the last is a folder and the last names a folder, an
/// article (<c>name.md</c>), or a Markdown file itself — the same places the page loader looks, read
/// from the listings navigation already caches. A prefixed space's mount point always exists.
/// </para>
/// </summary>
public static class PageRouteGuard
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    // Deliberately self-contained: no app shell, no menus, and one absolute link, so a crawler finds
    // nothing on it to follow but the home page.
    private static readonly byte[] NotFoundPage = Encoding.UTF8.GetBytes(
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" />" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />" +
        "<meta name=\"robots\" content=\"noindex\" /><title>Page not found</title>" +
        "<style>body{font-family:system-ui,sans-serif;max-width:40rem;margin:4rem auto;padding:0 1rem;line-height:1.5}</style>" +
        "</head><body><h1>Page not found</h1><p>Nothing is published at this address.</p>" +
        "<p><a href=\"/\">Go to the home page</a></p></body></html>");

    public static IApplicationBuilder UsePageRouteGuard(this IApplicationBuilder app) =>
        app.Use(static async (context, next) =>
        {
            // Only the page endpoint is guarded: static assets, /_content and the navigation API serve
            // through endpoints of their own.
            if (context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is null
                || await RouteExistsAsync(context))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.Headers.CacheControl = "no-cache";

            // A request for a file name — the browser's /favicon.ico, a mistyped image — wants no page.
            if (!IsFileName(context.Request.Path))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.Body.WriteAsync(NotFoundPage, context.RequestAborted);
            }
        });

    /// <summary>
    /// True when the last path segment ends in an extension a static file carries. Route segments
    /// here routinely contain dots — <c>03.00-tech</c>, <c>20260925.02-startup-optimization</c> — so a
    /// dot is not the test; an extension the content-type map knows is. Markdown is excluded, because
    /// a route may legitimately name the source file it renders.
    /// </summary>
    public static bool IsFileName(PathString path)
    {
        string value = path.Value ?? string.Empty;
        string segment = value[(value.LastIndexOf('/') + 1)..];
        int dot = segment.LastIndexOf('.');
        if (dot <= 0 || dot == segment.Length - 1)
        {
            return false;
        }

        string extension = segment[dot..];
        return !extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".qmd", StringComparison.OrdinalIgnoreCase)
            && ContentTypes.Mappings.ContainsKey(extension);
    }

    /// <summary>
    /// Walks the route one segment at a time through the cached folder listings. A junk route fails
    /// at its first unknown segment, usually within the first two listings.
    /// </summary>
    private static async Task<bool> RouteExistsAsync(HttpContext context)
    {
        string route = (context.Request.Path.Value ?? string.Empty).Trim('/');
        if (route.Length == 0)
        {
            return true;
        }

        // A route inside a prefixed space starts below its mount point, which may span segments.
        SpaceRegistry spaces = context.RequestServices.GetRequiredService<SpaceRegistry>();
        string prefix = string.Empty;
        string rest = route;
        if (spaces.TryResolve(route, out SpaceOptions space, out string inner) && !space.IsRootMounted)
        {
            prefix = space.NormalizedRouteBase.Trim('/');
            rest = inner.Trim('/');
        }

        if (rest.Length == 0)
        {
            return true;
        }

        IContentLister lister = context.RequestServices.GetRequiredService<IContentLister>();
        string[] segments = rest.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            string name = segments[i];
            IReadOnlyList<ChildEntry> children = await lister.ListChildrenAsync(prefix, context.RequestAborted) ?? [];
            bool isFolder = children.Any(c => c.IsFolder && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (i < segments.Length - 1)
            {
                if (!isFolder)
                {
                    return false;
                }

                prefix = prefix.Length == 0 ? name : prefix + "/" + name;
                continue;
            }

            return isFolder || children.Any(c => !c.IsFolder && NamesPage(c.Name, name));
        }

        return true;
    }

    // An article is addressed without its extension; a Markdown file may also be addressed by name.
    private static bool NamesPage(string fileName, string segment) =>
        fileName.Equals(segment + ".md", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals(segment + ".qmd", StringComparison.OrdinalIgnoreCase)
        || (NavRules.IsMarkdown(segment) && fileName.Equals(segment, StringComparison.OrdinalIgnoreCase));
}
