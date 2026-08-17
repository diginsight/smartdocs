using System.Globalization;
using System.Text.RegularExpressions;

namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>Ordering key for sibling menu items.</summary>
public readonly record struct SortTuple(int Group, double Num, string Text);

/// <summary>
/// Deterministic folder/file → menu-item rules, per
/// <c>.copilot/context/90.00-learning-hub/07-sidebar-menu-rules.md</c>:
/// numeric-prefix removal, date-prefix preservation (separator → " - "), Title Case,
/// newest-first date ordering, exclusions, and a semantic icon heuristic.
/// </summary>
public static class NavRules
{
    // Date prefix: YYYYMM or YYYYMMDD, optional ".NN" same-day sub-index.
    private static readonly Regex DateRx = new(
        @"^(?<date>20\d{2}(?:0[1-9]|1[0-2])(?:\d{2})?(?:\.\d+)?)(?:[-_\s]+(?<rest>.*))?$",
        RegexOptions.Compiled);

    private static readonly Regex NumRx = new(
        @"^\d+(?:\.\d+)?[-_\s]+(?<rest>.*)$", RegexOptions.Compiled);

    private static readonly Regex LeadingNum = new(@"^(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Display label for a folder or file base-name (extension already removed).</summary>
    public static string Label(string rawName)
    {
        Match d = DateRx.Match(rawName);
        if (d.Success)
        {
            string date = DisplayDate(d.Groups["date"].Value);
            string rest = d.Groups["rest"].Success ? Titleize(d.Groups["rest"].Value) : string.Empty;
            return rest.Length > 0 ? $"{date} - {rest}" : date;
        }

        Match n = NumRx.Match(rawName);
        return Titleize(n.Success ? n.Groups["rest"].Value : rawName);
    }

    /// <summary>Prepend a folder's preserved date prefix to a title resolved from article metadata.</summary>
    public static string WithDatePrefix(string rawFolderName, string resolvedTitle)
    {
        string? date = DateToken(rawFolderName);
        return date is null ? resolvedTitle : $"{DisplayDate(date)} - {resolvedTitle}";
    }

    // Drop the same-day ".NN" sub-index from the displayed date (kept only for sorting).
    private static string DisplayDate(string dateToken)
    {
        int dot = dateToken.IndexOf('.');
        return dot >= 0 ? dateToken[..dot] : dateToken;
    }

    public static bool HasDatePrefix(string rawName) => DateRx.IsMatch(rawName);

    public static string? DateToken(string rawName)
    {
        Match d = DateRx.Match(rawName);
        return d.Success ? d.Groups["date"].Value : null;
    }

    /// <summary>
    /// Sort key: numeric-prefixed first (explicit ascending order), then date-prefixed newest-first,
    /// then alphabetical. Numeric prefixes (01.00-, 02.00-) express deliberate top-level order;
    /// dates express recency within a section.
    /// </summary>
    public static SortTuple SortKey(string rawName)
    {
        Match d = DateRx.Match(rawName);
        if (d.Success && double.TryParse(d.Groups["date"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double dv))
        {
            return new SortTuple(1, -dv, string.Empty); // date group, newest first
        }

        Match m = LeadingNum.Match(rawName);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double nv))
        {
            return new SortTuple(0, nv, rawName.ToLowerInvariant()); // numeric group, ascending
        }

        return new SortTuple(2, 0, rawName.ToLowerInvariant());
    }

    /// <summary><c>index</c>/<c>readme</c> represent their parent folder (used as the folder's own link).</summary>
    public static bool IsIndexName(string fileName)
    {
        string n = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        return n is "index" or "readme";
    }

    /// <summary>
    /// Non-navigable names: underscore-/dot-prefixed working or infrastructure material, and sidecar
    /// changelog files (<c>*.changelog.md</c>), which are metadata, never reader-facing pages.
    /// </summary>
    public static bool IsExcludedName(string name) =>
        name.StartsWith('_') || name.StartsWith('.') ||
        name.EndsWith(".changelog.md", StringComparison.OrdinalIgnoreCase);

    /// <summary>Asset folders hold images/media, not navigable pages — they never form menu sections.</summary>
    public static bool IsAssetFolder(string name) =>
        name.ToLowerInvariant() is "images" or "img" or "assets" or "media" or "attachments" or "files";

    /// <summary>A markdown content file eligible for the menu.</summary>
    public static bool IsMarkdown(string name) =>
        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".qmd", StringComparison.OrdinalIgnoreCase);

    private static string Titleize(string s)
    {
        s = Spaces.Replace(s.Replace('-', ' ').Replace('_', ' ').Trim(), " ");
        return s.Length == 0 ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);
    }

    private static readonly (string Keyword, string Icon)[] IconMap =
    {
        ("news", "newspaper"), ("event", "calendar-event"), ("build", "calendar-event"),
        ("ignite", "calendar-event"), ("auth", "shield-lock"), ("azure", "cloud"),
        ("data", "database"), ("program", "code-slash"), ("blazor", "bootstrap"),
        ("web", "globe"), ("github", "github"), ("devops", "diagram-3"),
        ("prompt", "chat-square-text"), ("markdown", "markdown"), ("feed", "rss"),
        ("http", "arrow-left-right"), ("diginsight", "activity"), ("hardware", "cpu-fill"),
        ("writing", "pencil"), ("tech", "cpu"), ("how", "tools"), ("guide", "tools"),
        ("issue", "bug"), ("idea", "lightbulb"), ("tune", "sliders"), ("travel", "geo-alt"),
    };

    /// <summary>Best-guess Bootstrap icon from the folder name/label keywords.</summary>
    public static string IconFor(string rawName, string label)
    {
        string hay = (rawName + " " + label).ToLowerInvariant();
        foreach ((string kw, string icon) in IconMap)
        {
            if (hay.Contains(kw, StringComparison.Ordinal))
            {
                return icon;
            }
        }

        return "folder2";
    }
}
