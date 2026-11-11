using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Components;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Answers restricted page requests with 403 and missing page requests with 404, using a lightweight
/// branded page without loading the interactive shell or exposing protected navigation.
/// <para>
/// Answers a page request for a route that names nothing in the content with a real 404, before the
/// page router prerenders it. The router's catch-all route claims every path, so an unknown route
/// used to cost a full prerender — the menus, the Markdown probes, the layout — and to come back as
/// a "Not found" page with status 200 and a full set of relative menu links. A crawler that resolves
/// those links against the page's own URL instead of the document base turns every such page into
/// dozens of new unknown routes: a trap with no end, which kept a deployed instance's only core busy
/// around the clock. A 404 ends the trap at its first step, and costs a few cached listings.
/// </para>
/// <para>
/// A route exists when every segment but the last is a folder and the last names a folder, an
/// article (<c>name.md</c>), or a Markdown file itself — the same places the page loader looks, read
/// from the listings navigation already caches. A prefixed space's mount point always exists.
/// </para>
/// </summary>
public static class PageRouteGuard
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IApplicationBuilder UsePageRouteGuard(this IApplicationBuilder app) =>
        app.Use(static async (context, next) =>
        {
            // Only the page endpoint is guarded: static assets, /_content and the navigation API serve
            // through endpoints of their own.
            if (context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is null)
            {
                await next(context);
                return;
            }

            bool signInEnabled = context.RequestServices.GetRequiredService<SignInConfiguration>().Enabled;
            bool anonymous = !context.RequestServices.GetRequiredService<ReaderAccess>().Roles.IsSignedIn;

            // A private site challenges an anonymous reader before the route is looked up, so the
            // answer can't tell an existing route from a missing one. Without sign-in there is no one
            // to challenge, and every route answers as missing, because nothing is visible.
            if (signInEnabled && anonymous && !string.IsNullOrWhiteSpace(context.RequestServices
                    .GetRequiredService<IOptions<SiteOptions>>().Value.Authorization.DefaultRequirement))
            {
                await context.ChallengeAsync();
                return;
            }

            int statusCode = await GetStatusCodeAsync(context, context.Request.Path);
            if (statusCode == StatusCodes.Status200OK)
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = statusCode;
            // With sign-in, the page depends on the reader, so no shared cache may keep it.
            context.Response.Headers.CacheControl = signInEnabled ? HttpValidators.RevalidatePrivate : "no-cache";

            // A request for a file name — the browser's /favicon.ico, a mistyped image — wants no page.
            if (!IsFileName(context.Request.Path) && !HttpMethods.IsHead(context.Request.Method))
            {
                string returnUrl = context.Request.PathBase.Add(context.Request.Path) + context.Request.QueryString;
                await new RazorComponentResult<RouteStatusPage>(new
                {
                    AccessDenied = statusCode == StatusCodes.Status403Forbidden,
                    SignInEnabled = signInEnabled,
                    IsSignedIn = !anonymous,
                    SignInUrl = QueryHelpers.AddQueryString(AuthEndpoints.SignInPath, "returnUrl", returnUrl),
                })
                {
                    StatusCode = statusCode,
                    PreventStreamingRendering = true,
                }.ExecuteAsync(context);
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
    /// Returns 404 for an absent route, 403 for a restricted one, and 200 when the reader may see
    /// what it shows, in three cases only: the route
    /// renders a page, which the reader can therefore see; the route is the site root and the reader
    /// sees at least one space, whose generated index or root page the shell then shows; or the route
    /// renders no page, hidden or absent, and names a folder the reader can see. Everything else is
    /// answered with 403. Only the status distinguishes restricted content from missing content;
    /// the response never includes the protected page's title, body, or navigation.
    /// <para>
    /// The page is the one the shared rendered-page cache rendered for the route, checked through the
    /// exact file rendered. At a mount route whose <c>&lt;mount&gt;.md</c> belongs to a space the reader
    /// can't see, the mounted space's folder decides, so the route answers as a folder without a
    /// page of its own.
    /// </para>
    /// </summary>
    internal static async Task<int> GetStatusCodeAsync(HttpContext context, PathString path)
    {
        (bool exists, string folder) = await RouteExistsAsync(context, path);
        if (!exists)
        {
            return StatusCodes.Status404NotFound;
        }

        ReaderAccess reader = context.RequestServices.GetRequiredService<ReaderAccess>();
        if (reader.SeesEverything)
        {
            return StatusCodes.Status200OK;
        }

        string route = (path.Value ?? string.Empty).Trim('/');
        RenderedPageResolution resolution = await context.RequestServices
            .GetRequiredService<IRenderedPageResolver>()
            .ResolveRenderedAsync(route, context.RequestAborted);
        if (resolution.Page is not null)
        {
            return StatusCodes.Status200OK;
        }

        if (route.Length == 0)
        {
            return reader.VisibleSpaces.Count > 0 ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden;
        }

        AccessRequirement requirement = await context.RequestServices
            .GetRequiredService<AccessRequirementResolver>()
            .ForFolderAsync(folder, context.RequestAborted);
        return reader.IsVisible(requirement) ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden;
    }

    /// <summary>
    /// Walks the route one segment at a time through the cached folder listings. A junk route fails
    /// at its first unknown segment, usually within the first two listings. An existing route also
    /// yields its folder: the folder the route names, which every mount route does, or else the folder
    /// that holds the file naming it.
    /// </summary>
    private static async Task<(bool Exists, string Folder)> RouteExistsAsync(HttpContext context, PathString path)
    {
        string route = (path.Value ?? string.Empty).Trim('/');
        if (route.Length == 0)
        {
            return (true, string.Empty);
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
            return (true, prefix);
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
                    return (false, string.Empty);
                }

                prefix = prefix.Length == 0 ? name : prefix + "/" + name;
                continue;
            }

            if (isFolder)
            {
                return (true, prefix.Length == 0 ? name : prefix + "/" + name);
            }

            return (children.Any(c => !c.IsFolder && NamesPage(c.Name, name)), prefix);
        }

        return (true, prefix);
    }

    // An article is addressed without its extension; a Markdown file may also be addressed by name.
    private static bool NamesPage(string fileName, string segment) =>
        fileName.Equals(segment + ".md", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals(segment + ".qmd", StringComparison.OrdinalIgnoreCase)
        || (NavRules.IsMarkdown(segment) && fileName.Equals(segment, StringComparison.OrdinalIgnoreCase));
}
