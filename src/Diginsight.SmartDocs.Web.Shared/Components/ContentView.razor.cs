using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Diginsight.SmartDocs.Web.Shared.Components;

public partial class ContentView
{
    [Parameter] public string? Path { get; set; }

    [PersistentState] public string? PersistedPageRoute { get; set; }
    [PersistentState] public RenderedPage? PersistedPage { get; set; }

    private RenderedPage? _page;
    private bool _loading = true;
    private IReadOnlyList<Crumb> _trail = Array.Empty<Crumb>();
    private NavLeaf? _prev;
    private NavLeaf? _next;
    private IReadOnlyList<NavChild>? _sectionChildren;

    // True at the site root when no space claims it: the root then shows the generated space index.
    private bool _isSpaceIndex;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _prev = _next = null;
        _trail = Array.Empty<Crumb>();
        _sectionChildren = null;
        Toc.SetEntries(Array.Empty<TocEntry>());

        _isSpaceIndex = Norm(Path).Length == 0 && Site.ServesIndexAtRoot;
        if (_isSpaceIndex)
        {
            _page = null;
            _loading = false;
            Article.Clear();
            return;
        }

        string requestedRoute = Norm(Path);
        if (string.Equals(PersistedPageRoute, requestedRoute, StringComparison.OrdinalIgnoreCase))
        {
            _page = PersistedPage;
        }
        else if (string.Equals(Bootstrap.PageRoute, requestedRoute, StringComparison.OrdinalIgnoreCase))
        {
            _page = Bootstrap.Page;
        }
        else
        {
            _page = await Loader.LoadAsync(Path);
        }

        PersistedPageRoute = requestedRoute;
        PersistedPage = _page;
        Bootstrap.PageRoute = requestedRoute;
        Bootstrap.Page = _page;
        _loading = false;
        Toc.SetEntries(_page?.Toc ?? Array.Empty<TocEntry>());

        // Push current article metadata to the footer status bar.
        if (_page is not null && !string.IsNullOrWhiteSpace(_page.Title))
        {
            Article.Set(_page.Title, _page.WordCount);
        }
        else
        {
            Article.Clear();
        }

        // When no markdown content exists, check if this is a section with children
        // and show a section landing page instead of "Not found".
        if (_page is null && !string.IsNullOrEmpty(Path))
        {
            string prefix = Path.Replace('\\', '/').Trim('/') + "/";
            var children = await NavProvider.GetChildrenAsync(prefix);
            if (children.Count > 0)
            {
                _sectionChildren = children;
            }
        }

        // Breadcrumb is built from cheap per-level nav (the active-branch levels are already cached)
        // plus the article title — no dependency on the whole-tree flat index, so first paint (and
        // prerender) is never blocked by a cold index walk.
        string route = Norm(Path);
        _trail = route.Length == 0 ? Array.Empty<Crumb>() : await BuildTrailFromRouteAsync(route);

        // Prev/next only for an article that exists — a route that rendered nothing has no place in
        // the reading order. It loads in the background so it never blocks the article.
        if (_page is not null)
        {
            _ = LoadPrevNextAsync(Path);
        }
    }

    // After each render on the interactive client, turn any ```mermaid blocks into SVG. OnAfterRender
    // never fires during static prerender, so JS interop is safe here.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_loading || _page is null)
        {
            return;
        }

        try
        {
            await global::Microsoft.JSInterop.JSRuntimeExtensions.InvokeVoidAsync(JS, "appUi.renderMermaid");
        }
        catch
        {
            // Interop can be unavailable during prerender or mid-navigation teardown; never fatal.
        }
    }

    // Prev/next follow the menu's reading order — depth-first over the levels, the order the flat
    // index used to list — but are computed from levels instead of the whole-tree index: the levels
    // of the active branch, already loaded for the breadcrumb and the sidebar, plus, at a folder's
    // edge, the neighbouring section's levels. The walk stays inside the reader's space.
    private async Task LoadPrevNextAsync(string? forPath)
    {
        try
        {
            (NavLeaf? prev, NavLeaf? next) = await FindNeighboursAsync(Norm(forPath));
            if (Norm(forPath) != Norm(Path))
            {
                return; // navigated away while the neighbours were loading
            }

            _prev = prev;
            _next = next;

            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // Background prev/next is best-effort; never surface a fault (e.g. disposed mid-navigation).
        }
    }

    private const int MaxTreeDepth = 32;

    private async Task<(NavLeaf? Prev, NavLeaf? Next)> FindNeighboursAsync(string route)
    {
        if (route.Length == 0)
        {
            return (null, null);
        }

        // Locate the article: from the space's top level down the branch that contains it, keeping the
        // level and position at each depth.
        var branch = new List<(IReadOnlyList<NavChild> Level, int Index)>();
        string prefix = ScopePrefix(route);
        for (int depth = 0; depth < MaxTreeDepth; depth++)
        {
            IReadOnlyList<NavChild> level = await LevelAsync(prefix);
            int leaf = IndexOf(level, n => IsLeaf(n) && Norm(n.Route) == route);
            if (leaf >= 0)
            {
                branch.Add((level, leaf));
                return (await StepAsync(branch, -1), await StepAsync(branch, +1));
            }

            int section = IndexOf(level, n => n.IsSection && n.Prefix is { Length: > 0 } p && IsAncestorOrSelf(Norm(p), route));
            if (section < 0)
            {
                return (null, null); // not in the menu (hidden, or a section's own landing page)
            }

            branch.Add((level, section));
            prefix = level[section].Prefix!;
        }

        return (null, null);
    }

    // The nearest article before (direction -1) or after (+1) the located one: siblings first, then
    // the ancestors' siblings, descending into any section found on the way.
    private async Task<NavLeaf?> StepAsync(List<(IReadOnlyList<NavChild> Level, int Index)> branch, int direction)
    {
        for (int frame = branch.Count - 1; frame >= 0; frame--)
        {
            (IReadOnlyList<NavChild> level, int index) = branch[frame];
            for (int i = index + direction; i >= 0 && i < level.Count; i += direction)
            {
                if (await EdgeLeafAsync(level[i], direction, 0) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    // The first (+1) or last (-1) article under a node, in reading order.
    private async Task<NavLeaf?> EdgeLeafAsync(NavChild node, int direction, int depth)
    {
        if (IsLeaf(node))
        {
            return new NavLeaf(node.Text, node.Route!, string.Empty, node.Date, node.Author);
        }

        if (!node.IsSection || node.Prefix is not { Length: > 0 } prefix || depth >= MaxTreeDepth)
        {
            return null;
        }

        IReadOnlyList<NavChild> level = await NavProvider.GetChildrenAsync(prefix);
        for (int i = direction > 0 ? 0 : level.Count - 1; i >= 0 && i < level.Count; i += direction)
        {
            if (await EdgeLeafAsync(level[i], direction, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // A level as the reading order sees it. At the site root a root-mounted space shares its level
    // with the other spaces' mount points, which belong to the space switcher, not to this space.
    private async Task<IReadOnlyList<NavChild>> LevelAsync(string prefix)
    {
        IReadOnlyList<NavChild> level = await NavProvider.GetChildrenAsync(prefix);
        return prefix.Length == 0
            ? level.Where(n => !(n.IsSection && Site.IsMountSegment(n.Prefix))).ToList()
            : level;
    }

    // An entry the reading order visits: an article, or a folder collapsed into a single link.
    private static bool IsLeaf(NavChild n) => !n.IsSection && !string.IsNullOrEmpty(n.Route);

    private static bool IsAncestorOrSelf(string ancestor, string route) =>
        route == ancestor || route.StartsWith(ancestor + "/", StringComparison.Ordinal);

    private static int IndexOf(IReadOnlyList<NavChild> level, Func<NavChild, bool> match)
    {
        for (int i = 0; i < level.Count; i++)
        {
            if (match(level[i]))
            {
                return i;
            }
        }

        return -1;
    }

    // Builds a breadcrumb from a route's ancestor levels. Each level is cheap and cached, so this
    // works for section pages the flat index does not enumerate as leaves.
    private async Task<IReadOnlyList<Crumb>> BuildTrailFromRouteAsync(string route)
    {
        string[] segs = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var crumbs = new List<Crumb>();
        string parent = string.Empty;
        for (int i = 0; i < segs.Length; i++)
        {
            string prefix = parent.Length == 0 ? segs[i] : parent + "/" + segs[i];
            IReadOnlyList<NavChild> level = await NavProvider.GetChildrenAsync(parent);
            NavChild? node = null;
            foreach (NavChild n in level)
            {
                if (Norm(n.Prefix) == prefix || Norm(n.Route) == prefix)
                {
                    node = n;
                    break;
                }
            }

            bool last = i == segs.Length - 1;
            string text = node?.Text ?? _page?.Title ?? segs[i].Replace('-', ' ').Replace('_', ' ');
            string? crumbRoute = !last && node?.Route is { Length: > 0 } r ? "/" + r.TrimStart('/') : null;
            crumbs.Add(new Crumb(text, crumbRoute));
            parent = prefix;
        }

        return crumbs;
    }

    private static string Norm(string? route) =>
        (route ?? string.Empty).Replace('\\', '/').Trim('/').ToLowerInvariant();

    // Nav prefix of the space owning a route: a prefixed space's route-base segment, as configured,
    // or empty for the root-mounted space.
    private string ScopePrefix(string? route) =>
        Site.ResolveSpace(route) is { IsRootMounted: false } space ? space.Segment : string.Empty;

    /// <summary>Derive a human-readable title from the current path for section landing pages.</summary>
    private string SectionTitle()
    {
        string path = (Path ?? string.Empty).Replace('\\', '/').Trim('/');

        // A space's own landing, when its content has no index page: the space title.
        if (Site.ResolveSpace(path) is { IsRootMounted: false } space &&
            string.Equals(space.Segment, path, StringComparison.OrdinalIgnoreCase))
        {
            return space.Title;
        }

        string lastSeg = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        // Strip numeric prefix (e.g. "02.01-azure" → "azure") and title-case
        int dash = lastSeg.IndexOf('-');
        string raw = dash >= 0 ? lastSeg[(dash + 1)..] : lastSeg;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(raw.Replace('-', ' ').Replace('_', ' '));
    }

    public void Dispose() => Toc.SetEntries(Array.Empty<TocEntry>());
}
