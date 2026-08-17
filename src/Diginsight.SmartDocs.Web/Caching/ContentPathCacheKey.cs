using Diginsight.SmartCache;
using Diginsight.SmartCache.Externalization;

namespace Diginsight.SmartDocs.Web.Caching;

/// <summary>
/// A content-path-addressed SmartCache key shared by the content cache and the navigation cache.
/// Implementing <see cref="IInvalidatable"/> lets a single <see cref="ContentPathInvalidationRule"/>
/// drop exactly the entries on the changed path's branch — the article itself plus every menu level
/// that lists an ancestor of it — across all nodes, instead of flushing the whole cache.
/// </summary>
/// <param name="Kind">
/// Disambiguates entries that share a <paramref name="Path"/> but are produced by different callers
/// (e.g. <c>content</c> vs <c>nav-level</c> vs <c>nav-index</c>), so they never collide.
/// </param>
/// <param name="Path">
/// Normalized (forward-slash, trimmed) content path this entry is keyed on: a file key for content,
/// a folder prefix for a menu level, or the empty string for the whole-tree index.
/// </param>
[CacheInterchangeName("LPCK")]
public sealed record ContentPathCacheKey(string Kind, string Path) : IInvalidatable
{
    public bool IsInvalidatedBy(IInvalidationRule invalidationRule, out Func<Task>? invalidationCallback)
    {
        invalidationCallback = null;
        return invalidationRule is ContentPathInvalidationRule rule
            && (rule.Kind is null || string.Equals(rule.Kind, Kind, StringComparison.Ordinal))
            && OnSameBranch(Path, rule.Path);
    }

    /// <summary>
    /// True when the two paths lie on one root-to-node branch: either path is an ancestor-or-self of
    /// the other. An empty path is the root and matches everything (so the index and root level always
    /// rebuild). Siblings on different branches do not match, so they stay cached.
    /// </summary>
    private static bool OnSameBranch(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return a.Length == 0 || b.Length == 0 || IsAncestorOrSelf(a, b) || IsAncestorOrSelf(b, a);
    }

    private static bool IsAncestorOrSelf(string ancestor, string descendant) =>
        descendant.Equals(ancestor, StringComparison.OrdinalIgnoreCase) ||
        descendant.StartsWith(ancestor + "/", StringComparison.OrdinalIgnoreCase);

    internal static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/');
}

/// <summary>
/// Invalidation rule carrying the content path that changed. Emitted after a write; SmartCache asks
/// every held key whether it is invalidated by this rule and broadcasts it to the other nodes over
/// the companion. An empty <paramref name="Path"/> invalidates the entire content/navigation cache.
/// </summary>
/// <param name="Path">Normalized content path that changed, or the empty string for "everything".</param>
/// <param name="Kind">When set, restricts the rule to entries of that <see cref="ContentPathCacheKey.Kind"/>
/// (e.g. only <c>nav-level</c>); <c>null</c> matches every kind on the branch.</param>
[CacheInterchangeName("LPIR")]
public sealed record ContentPathInvalidationRule(string Path, string? Kind = null) : IInvalidationRule;
