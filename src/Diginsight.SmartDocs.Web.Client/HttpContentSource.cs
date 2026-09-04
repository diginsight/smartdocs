using System.Net;
using Diginsight.SmartDocs.Web.Shared;

namespace Diginsight.SmartDocs.Web.Client;

/// <summary>
/// Client-side content source: fetches raw Markdown from the server's <c>/_content/{key}</c>
/// endpoint. Storage credentials never reach the browser — the server owns them.
/// </summary>
public sealed class HttpContentSource(HttpClient http) : IContentSource
{
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
