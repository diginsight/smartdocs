using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Client.Layout;

/// <summary>
/// Scopes the menus to the space the reader is in. The navigation API serves one path namespace in
/// which every prefixed space is mounted under its route-base segment; the sidebar and the tab band
/// show only the current space's part of it, and the space switcher moves between spaces.
/// </summary>
internal static class SpaceScope
{
    private const string HomeIcon = "house-fill";

    /// <summary>The nav prefix that roots the menus for <paramref name="route"/>: a space's segment, or empty.</summary>
    public static string PrefixFor(SiteShellState site, string? route) =>
        site.ResolveSpace(route) is { IsRootMounted: false } space ? space.Segment : string.Empty;

    /// <summary>
    /// The top menu level for a scope. Inside a prefixed space the level is that space's own top
    /// level with a leading Home link to the space landing. At the site root, a root-mounted space
    /// sees its own content without the other spaces' mount points (those belong to the switcher),
    /// while the generated index sees the mount points themselves.
    /// </summary>
    public static async Task<IReadOnlyList<NavChild>> LoadRootAsync(
        INavProvider provider, SiteShellState site, string scope, bool refresh = false)
    {
        IReadOnlyList<NavChild> level = refresh
            ? await provider.RefreshChildrenAsync(scope)
            : await provider.GetChildrenAsync(scope);

        if (scope.Length > 0)
        {
            var scoped = new List<NavChild>(level.Count + 1) { Home(site, scope) };
            scoped.AddRange(level);
            return scoped;
        }

        return site.ServesIndexAtRoot
            ? level
            : level.Where(n => !(n.IsSection && site.IsMountSegment(n.Prefix))).ToList();
    }

    /// <summary>True for the Home link of <paramref name="scope"/> (the site root's or a space's).</summary>
    public static bool IsHome(NavChild node, string scope) =>
        !node.IsSection && node.Icon == HomeIcon &&
        string.Equals((node.Route ?? string.Empty).Trim('/'), scope, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="route"/> belongs to the space rooted at <paramref name="scope"/>.</summary>
    public static bool InScope(SiteShellState site, string scope, string? route)
    {
        string r = (route ?? string.Empty).Replace('\\', '/').Trim('/');
        if (scope.Length > 0)
        {
            return r.Equals(scope, StringComparison.OrdinalIgnoreCase)
                || r.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase);
        }

        if (site.ServesIndexAtRoot)
        {
            return true; // the index spans every space
        }

        int slash = r.IndexOf('/');
        return !site.IsMountSegment(slash >= 0 ? r[..slash] : r);
    }

    // Labelled with the space title rather than "Home": a space's own content often has a chapter
    // called Home, and two identical entries side by side read as a duplicate.
    private static NavChild Home(SiteShellState site, string scope) =>
        new(site.ResolveSpace(scope)?.Title is { Length: > 0 } title ? title : "Home", scope, null, HomeIcon, false, false);
}
