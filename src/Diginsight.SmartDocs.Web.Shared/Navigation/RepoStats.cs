namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// Repository-level statistics shown in the site footer, derived on the client from the SAME cached
/// nav queries the app already issues (root level + flat index) — no separate endpoint or round-trip.
/// </summary>
public sealed record RepoStats(
    int TotalArticles,
    DateTimeOffset? LastChangeUtc,
    string? LastAuthor,
    bool Complete = false)
{
    public static readonly RepoStats Empty = new(0, null, null);
}
