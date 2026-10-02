using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.StaticFiles;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Answers a request for a file name with a plain 404 before the page router prerenders it as an
/// article. The router's catch-all route claims every path, so the browser's <c>/favicon.ico</c> — or
/// any mistyped asset URL — used to cost a full prerender: the menus, five Markdown probes, and the
/// whole-tree walk behind prev/next, answered with an HTML page the browser could not cache.
/// </summary>
public static class FileRequestGuard
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IApplicationBuilder UseFileRequestGuard(this IApplicationBuilder app) =>
        app.Use(static async (context, next) =>
        {
            // Only the page endpoint is guarded: static assets and the /_content passthrough serve
            // real files with these same extensions through endpoints of their own.
            if (context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null
                && IsFileName(context.Request.Path)
                && !await IsContentRouteAsync(context))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(context);
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
    /// True when the content itself holds the name — an article <c>node.js.md</c>, a folder
    /// <c>azure.ai</c> — so a route that merely looks like a file is never refused. Every page a route
    /// can resolve to is that file or lives in that folder, so the parent's listing settles it: one
    /// read, which the cache usually already holds.
    /// </summary>
    private static async Task<bool> IsContentRouteAsync(HttpContext context)
    {
        string route = (context.Request.Path.Value ?? string.Empty).Trim('/');
        int slash = route.LastIndexOf('/');
        string parent = slash < 0 ? string.Empty : route[..slash];
        string name = route[(slash + 1)..];

        IContentLister lister = context.RequestServices.GetRequiredService<IContentLister>();
        IReadOnlyList<ChildEntry> children = await lister.ListChildrenAsync(parent, context.RequestAborted) ?? [];
        return children.Any(child => child.IsFolder
            ? child.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            : child.Name.Equals(name + ".md", StringComparison.OrdinalIgnoreCase));
    }
}