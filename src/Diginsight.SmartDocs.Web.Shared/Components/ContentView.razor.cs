using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Diginsight.SmartDocs.Web.Shared.Components;

public partial class ContentView
{
    [Parameter] public string? Path { get; set; }

    private RenderedPage? _page;
    private bool _loading = true;
    private IReadOnlyList<Crumb> _trail = Array.Empty<Crumb>();
    private NavLeaf? _prev;
    private NavLeaf? _next;
    private IReadOnlyList<NavChild>? _sectionChildren;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _prev = _next = null;
        _trail = Array.Empty<Crumb>();
        _sectionChildren = null;
        Toc.SetEntries(Array.Empty<TocEntry>());
        _page = await Loader.LoadAsync(Path);
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

        // Prev/next needs the ordered flat index; load it in the background so it never blocks the
        // article — this also warms the index for menu search — and it renders itself when ready.
        _ = LoadPrevNextAsync(Path);
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

    // Prev/next comes from the ordered flat index. It runs in the background (launched from
    // OnParametersSetAsync) so a cold whole-tree index walk never blocks the article or breadcrumb;
    // it renders itself when ready.
    private async Task LoadPrevNextAsync(string? forPath)
    {
        try
        {
            IReadOnlyList<NavLeaf> index = await NavProvider.GetIndexAsync();
            if (Norm(forPath) != Norm(Path))
            {
                return; // navigated away while the index was loading
            }

            string cur = Norm(forPath);
            int idx = -1;
            for (int i = 0; i < index.Count; i++)
            {
                if (Norm(index[i].Route) == cur)
                {
                    idx = i;
                    break;
                }
            }

            _prev = idx > 0 ? index[idx - 1] : null;
            _next = idx >= 0 && idx < index.Count - 1 ? index[idx + 1] : null;

            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // Background prev/next is best-effort; never surface a fault (e.g. disposed mid-navigation).
        }
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

    /// <summary>Derive a human-readable title from the current path for section landing pages.</summary>
    private string SectionTitle()
    {
        string path = (Path ?? string.Empty).Replace('\\', '/').Trim('/');
        string lastSeg = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        // Strip numeric prefix (e.g. "02.01-azure" → "azure") and title-case
        int dash = lastSeg.IndexOf('-');
        string raw = dash >= 0 ? lastSeg[(dash + 1)..] : lastSeg;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(raw.Replace('-', ' ').Replace('_', ' '));
    }

    public void Dispose() => Toc.SetEntries(Array.Empty<TocEntry>());
}
