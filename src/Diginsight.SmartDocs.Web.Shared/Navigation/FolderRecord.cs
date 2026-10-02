namespace Diginsight.SmartDocs.Web.Shared.Navigation;

public sealed record FolderRecord(
    string Prefix,
    string Label,
    string? Route,
    string? Short,
    string? Icon,
    double? Order,
    bool Hidden,
    bool TopbarHidden,
    string? TopbarAlign,
    string Classification,
    bool HasChildren,
    int ArticleCount,
    DateTimeOffset? LatestArticleUtc,
    Coverage Coverage,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record NavLevelReference(string? FolderPrefix, NavChild? Leaf)
{
    public static NavLevelReference Folder(string prefix) => new(prefix, null);

    public static NavLevelReference Article(NavChild leaf) => new(null, leaf);
}

public sealed record NavLevelResponse(
    IReadOnlyList<NavLevelReference> Children,
    IReadOnlyList<FolderRecord> Folders);
