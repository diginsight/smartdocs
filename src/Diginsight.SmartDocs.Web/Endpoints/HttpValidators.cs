using System.Security.Cryptography;
using System.Text;
using Microsoft.Net.Http.Headers;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Entity tags and conditional requests for the content and navigation endpoints, so a browser that
/// already holds a response revalidates it with a <c>304</c> instead of downloading it again.
/// </summary>
internal static class HttpValidators
{
    /// <summary>Cache-Control for a response the browser may keep but must revalidate before every use.</summary>
    public const string Revalidate = "no-cache";

    /// <summary>A strong entity tag over the given parts, for responses whose identity spans several values.</summary>
    public static string Tag(params string?[] parts) => Tag(Encoding.UTF8.GetBytes(string.Join('\n', parts)));

    /// <summary>A strong entity tag over a response body.</summary>
    public static string Tag(ReadOnlySpan<byte> body) =>
        "\"" + Convert.ToHexString(SHA256.HashData(body), 0, 16) + "\"";

    /// <summary>
    /// Writes the entity tag and caching policy, and returns true when the request already holds this
    /// version — the caller then answers <c>304</c> with no body. <c>If-None-Match</c> compares weakly,
    /// so a tag the browser stored with a <c>W/</c> prefix still matches.
    /// </summary>
    public static bool IsNotModified(HttpContext http, string etag, string cacheControl)
    {
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = cacheControl;

        foreach (EntityTagHeaderValue candidate in http.Request.GetTypedHeaders().IfNoneMatch)
        {
            if (candidate.Equals(EntityTagHeaderValue.Any) || candidate.Tag.Equals(etag))
            {
                return true;
            }
        }

        return false;
    }
}
