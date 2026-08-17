using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Abstraction for building the site navigation tree on demand from the live content hierarchy.
/// </summary>
public interface INavBuilder
{
    /// <summary>Returns the immediate children for a given menu prefix (one level).</summary>
    Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default);

    /// <summary>Flattens the whole menu into navigable leaves + section breadcrumbs.</summary>
    Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default);
}
