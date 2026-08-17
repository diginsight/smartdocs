using System.Text;
using Diginsight.SmartDocs.Web.Shared.Rendering;

namespace Diginsight.SmartDocs.Web.Shared.Services;

/// <summary>
/// Resolves a route path to a source Markdown file (via <see cref="IContentSource"/>) and
/// renders it. Runs unchanged on the server (prerender) and in the WASM client (navigation);
/// only the injected <see cref="IContentSource"/> differs per platform.
/// </summary>
public sealed class PageLoader(IContentSource source, IMarkdownRenderer renderer)
{
    public async Task<RenderedPage?> LoadAsync(string? routePath, CancellationToken ct = default)
    {
        foreach (string key in Candidates(routePath))
        {
            ContentResult? result = await source.GetAsync(key, ct);
            if (result is not null)
            {
                string markdown = Encoding.UTF8.GetString(result.Bytes);
                string contentDir = key.Contains('/') ? key[..key.LastIndexOf('/')] : string.Empty;
                return renderer.Render(markdown, contentDir);
            }
        }

        return null;
    }

    /// <summary>Candidate source files for a request path, tried in order.</summary>
    private static IEnumerable<string> Candidates(string? routePath)
    {
        string path = (routePath ?? string.Empty).Replace('\\', '/').Trim('/');

        if (path.Length == 0)
        {
            yield return "index.md";
            yield return "readme.md";
            yield return "README.md";
            yield break;
        }

        // Already points at a file.
        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            yield return path;
            yield break;
        }

        yield return path + ".md";
        yield return path + "/index.md";
        yield return path + "/overview.md";
        yield return path + "/readme.md";
        yield return path + "/README.md";
    }
}
