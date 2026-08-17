using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>Relevant fields parsed from an article's top YAML frontmatter.</summary>
public sealed record FrontMatterInfo(string? Title, bool Publish, bool Draft, string? Author = null, string? Date = null)
{
    public static readonly FrontMatterInfo Default = new(null, true, false);

    /// <summary>True when the article must be hidden from navigation (publish:false or draft:true).</summary>
    public bool Hidden => !Publish || Draft;
}

/// <summary>
/// Reads and parses only the leading YAML frontmatter block of a Markdown file. The reader stops
/// at the closing <c>---</c> (capped) so callers touch just the header, never the whole article.
/// </summary>
public static class FrontMatter
{
    private const int MaxHeaderBytes = 64 * 1024;

    private static readonly Regex TitleRx = new(@"(?m)^\s*title\s*:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex PublishRx = new(@"(?m)^\s*publish\s*:\s*(false|true)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DraftRx = new(@"(?m)^\s*draft\s*:\s*(true|false)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AuthorRx = new(@"(?m)^\s*author\s*:\s*(.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DateRx = new(@"(?m)^\s*date\s*:\s*(.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex H1Rx = new(@"(?m)^#\s+(.+?)\s*$", RegexOptions.Compiled);

    /// <summary>Parses frontmatter fields from a file's leading text (header + maybe some body).</summary>
    public static FrontMatterInfo Parse(string? leadingText)
    {
        if (string.IsNullOrEmpty(leadingText))
        {
            return FrontMatterInfo.Default;
        }

        string header = ExtractHeader(leadingText);
        if (header.Length == 0)
        {
            return FrontMatterInfo.Default;
        }

        string? title = Clean(TitleRx.Match(header).Groups[1].Value);
        bool publish = !(PublishRx.Match(header) is { Success: true } p && p.Groups[1].Value.Equals("false", StringComparison.OrdinalIgnoreCase));
        bool draft = DraftRx.Match(header) is { Success: true } d && d.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        string? author = Clean(AuthorRx.Match(header).Groups[1].Value);
        string? date = Clean(DateRx.Match(header).Groups[1].Value);
        return new FrontMatterInfo(title, publish, draft, author, date);
    }

    /// <summary>Title from frontmatter, else the first H1 heading, else null.</summary>
    public static string? ResolveTitle(string? leadingText)
    {
        FrontMatterInfo fm = Parse(leadingText);
        if (!string.IsNullOrWhiteSpace(fm.Title))
        {
            return fm.Title;
        }

        if (!string.IsNullOrEmpty(leadingText))
        {
            string body = StripHeader(leadingText);
            Match h1 = H1Rx.Match(body);
            if (h1.Success)
            {
                return Clean(h1.Groups[1].Value);
            }
        }

        return null;
    }

    /// <summary>Parses a frontmatter <c>date:</c> string into a UTC-assumed offset (null when absent/unparseable).</summary>
    public static DateTimeOffset? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dto)
            ? dto
            : null;
    }

    /// <summary>Reads the leading bytes of a stream up to the closing <c>---</c> (capped), as UTF-8 text.</summary>
    public static async Task<string> ReadHeadAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[8192];
        using var acc = new MemoryStream();
        int read;
        while (acc.Length < MaxHeaderBytes && (read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            acc.Write(buffer, 0, read);
            string soFar = Encoding.UTF8.GetString(acc.GetBuffer(), 0, (int)acc.Length);
            // Stop once we have a complete frontmatter block (opening --- then a closing ---).
            if (soFar.TrimStart().StartsWith("---", StringComparison.Ordinal))
            {
                int firstClose = soFar.IndexOf("\n---", soFar.IndexOf("---", StringComparison.Ordinal) + 3, StringComparison.Ordinal);
                if (firstClose >= 0)
                {
                    return soFar;
                }
            }
            else
            {
                // No frontmatter — a few KB is enough to find a leading H1.
                if (acc.Length >= 4096)
                {
                    break;
                }
            }
        }

        return Encoding.UTF8.GetString(acc.GetBuffer(), 0, (int)acc.Length);
    }

    private static string ExtractHeader(string text)
    {
        string t = text.TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (!t.StartsWith("---", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        int close = t.IndexOf("\n---", 3, StringComparison.Ordinal);
        return close < 0 ? string.Empty : t[3..close];
    }

    private static string StripHeader(string text)
    {
        string t = text.TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (!t.StartsWith("---", StringComparison.Ordinal))
        {
            return text;
        }

        int close = t.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (close < 0)
        {
            return text;
        }

        int after = t.IndexOf('\n', close + 4);
        return after < 0 ? string.Empty : t[(after + 1)..];
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string v = value.Trim();
        if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
        {
            v = v[1..^1];
        }

        return v.Trim();
    }
}
