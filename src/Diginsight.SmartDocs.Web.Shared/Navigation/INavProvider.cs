namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// Supplies one menu level at a time. On the server it wraps the in-process nav builder; in the
/// WASM client it calls the <c>/_nav/children</c> API. This keeps prerender working without HTTP.
/// </summary>
public interface INavProvider
{
    Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Re-fetches a level from the origin, bypassing any client-side cache. Used by the footer's
    /// cold-start convergence to pick up the recursive folder counts once the server's background
    /// warm-up has computed them. No-op wrapper on the server (nothing is cached there).
    /// </summary>
    Task<IReadOnlyList<NavChild>> RefreshChildrenAsync(string prefix, CancellationToken ct = default)
        => GetChildrenAsync(prefix, ct);

    /// <summary>Returns the server-authoritative site-root aggregate, or null while it is unavailable.</summary>
    Task<FolderArticleStats?> GetTotalAsync(CancellationToken ct = default);

    /// <summary>Returns the flattened list of navigable articles for menu search.</summary>
    Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default);
}
