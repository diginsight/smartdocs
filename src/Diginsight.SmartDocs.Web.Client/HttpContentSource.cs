using System.Net;
using System.Net.Http.Json;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Rendering;

namespace Diginsight.SmartDocs.Web.Client;

/// <summary>
/// Client-side content source: fetches raw Markdown from the server's <c>/_content/{key}</c>
/// endpoint. Storage credentials never reach the browser — the server owns them.
/// </summary>
public sealed class HttpContentSource(HttpClient http) :
    IContentSource,
    IContentPathResolver,
    IRenderedPageResolver
{
    /// <summary>Matches <c>ContentEndpoints.ContentKeyHeader</c>, which the client cannot reference.</summary>
    private const string ContentKeyHeader = "X-Content-Key";

    public async Task<RenderedPageResolution> ResolveRenderedAsync(
        string? routePath,
        CancellationToken cancellationToken = default)
    {
        string path = (routePath ?? string.Empty).Replace('\\', '/').Trim('/');
        using HttpResponseMessage response = await http.GetAsync(
            path.Length == 0 ? "_page" : $"_page/{path}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return RenderedPageResolution.Nothing;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return RenderedPageResolution.Unhandled;
        }

        response.EnsureSuccessStatusCode();
        RenderedPage? page = await response.Content.ReadFromJsonAsync<RenderedPage>(
            cancellationToken: cancellationToken);
        return page is null
            ? throw new InvalidOperationException($"Rendered page response for '{path}' had no body.")
            : RenderedPageResolution.Found(page);
    }

    /// <summary>
    /// Asks the server to walk the candidate file names where the files actually are. Probing them
    /// from here cost a round trip — and a 404 in the browser console — for every name that did not
    /// exist, which for a section folder was most of them.
    /// </summary>
    public async Task<ContentResolution> ResolveAsync(string? routePath, CancellationToken ct = default)
    {
        string path = (routePath ?? string.Empty).Replace('\\', '/').Trim('/');
        using HttpResponseMessage response = await http.GetAsync(
            path.Length == 0 ? "_page" : $"_page/{path}", ct);

        // A section folder with no page of its own answers 204; a server that predates this
        // endpoint answers 404, and then probing the candidates here is the only way through.
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return ContentResolution.Nothing;
        }

        if (!response.IsSuccessStatusCode ||
            !response.Headers.TryGetValues(ContentKeyHeader, out IEnumerable<string>? keys))
        {
            // Without the key the base directory would be a guess, and every relative image on the
            // page would quietly break; reporting nothing sends PageLoader back to probing.
            return ContentResolution.Unhandled;
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
        string etag = response.Headers.ETag?.Tag ?? string.Empty;
        return ContentResolution.Found(
            keys.First(),
            new ContentResult(bytes, response.Content.Headers.ContentType?.ToString(), etag));
    }

    public async Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await http.GetAsync($"_content/{contentKey}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
        string etag = response.Headers.ETag?.Tag ?? string.Empty;
        return new ContentResult(bytes, response.Content.Headers.ContentType?.ToString(), etag);
    }
}
