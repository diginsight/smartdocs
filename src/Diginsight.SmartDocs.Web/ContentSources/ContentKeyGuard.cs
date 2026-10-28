namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// Refuses content keys whose segments a store could resolve to another location than the one the
/// key names. Kestrel removes dot segments from a request path before it decodes <c>%5C</c>, so a key
/// such as <c>product-c\..\product-a\x.md</c> reaches an endpoint with its <c>..</c> intact. The access
/// check places that key in <c>product-c</c>, while a store that collapses dot segments, as the blob
/// SDK does when it builds a blob URI, reads from somewhere else. Refusing such keys wherever a key
/// picks a space or reaches a store keeps the space checked and the space read the same.
/// </summary>
public static class ContentKeyGuard
{
    /// <summary>
    /// True when <paramref name="key"/>, with <c>\</c> read as <c>/</c> and outer slashes ignored, has
    /// no empty segment, no segment made only of dots and spaces (<c>.</c>, <c>..</c>, <c>...</c>), and
    /// no <c>%</c>, which a store could decode into either. The empty key, the site root, is safe.
    /// </summary>
    public static bool IsSafe(string? key)
    {
        string normalized = (key ?? string.Empty).Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            return true;
        }

        if (normalized.Contains('%'))
        {
            return false;
        }

        foreach (string segment in normalized.Split('/'))
        {
            if (segment.Trim('.', ' ').Length == 0)
            {
                return false;
            }
        }

        return true;
    }
}
