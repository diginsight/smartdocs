using System.Text.Json.Serialization;

namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>A raw child of a storage "folder": either a sub-folder or a file, path-addressed.</summary>
public sealed record ChildEntry(string Name, bool IsFolder, string Path);

/// <summary>
/// Implemented by server-side content sources (filesystem/blob) that can enumerate one level of
/// the hierarchy and cheaply read a file's leading frontmatter. The WASM client never lists — it
/// calls the server nav API instead.
/// </summary>
public interface IContentLister
{
    /// <summary>Lists the immediate children (folders + files) under <paramref name="prefix"/> (one level).</summary>
    Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default);

    /// <summary>Reads just the leading frontmatter/header text of a file (no full download).</summary>
    Task<string?> ReadHeadAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Reads an article's header and parses it into the fields navigation uses. A caching lister
    /// overrides this to keep the parsed fields rather than the header text they came from.
    /// </summary>
    async Task<ArticleHead> ReadArticleHeadAsync(string key, CancellationToken ct = default) =>
        FrontMatter.ParseHead(await ReadHeadAsync(key, ct));

    /// <summary>Reads a folder's navigation projection through the server metadata provider.</summary>
    Task<FolderMeta> ReadFolderMetaAsync(string key, CancellationToken ct = default) =>
        throw new NotSupportedException("Folder metadata requires a metadata-aware server lister.");
}

/// <summary>One built menu node returned by the nav API for a single level.</summary>
public sealed record NavChild(
    string Text,
    string? Route,
    string? Prefix,
    string? Icon,
    bool IsSection,
    bool HasChildren,
    string? Short = null,
    bool TopbarHidden = false,
    string? TopbarAlign = null,
    DateTimeOffset? Date = null,
    string? Author = null,
    int? ArticleCount = null,
    DateTimeOffset? LatestArticleUtc = null,
    Coverage CountCoverage = Coverage.None)
{
    /// <summary>
    /// The content folder this node comes from, which names its space: the folder's own path for a
    /// section, a space mount and a collapsed one-page folder; the level's prefix for an article and
    /// for <b>Home</b>. Authorization resolves a node's space from it, never from its route, because a
    /// mount node and a root-mounted space's page can share a route. Server-only: it is left out of
    /// navigation responses and the prerendered state, and kept in the server's cache.
    /// </summary>
    [JsonIgnore]
    public string? SourceFolder { get; init; }
}

/// <summary>A flattened navigable article (leaf) with its section breadcrumb — used by menu search.</summary>
public sealed record NavLeaf(string Text, string Route, string Path, DateTimeOffset? Date = null, string? Author = null)
{
    /// <summary>The content folder the leaf comes from, carried from its <see cref="NavChild"/>. Server-only.</summary>
    [JsonIgnore]
    public string? SourceFolder { get; init; }
}

/// <summary>One breadcrumb segment: display text and an optional link route.</summary>
public sealed record Crumb(string Text, string? Route);
