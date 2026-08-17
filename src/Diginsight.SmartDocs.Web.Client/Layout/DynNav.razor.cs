using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class DynNav
{
    private const int MaxResults = 200;
    private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    private IReadOnlyList<NavChild>? _root;
    private string _current = string.Empty;
    private bool _scrollPending;

    private string _query = string.Empty;
    private IReadOnlyList<NavLeaf>? _index;
    private bool _indexing;

    // Live nav-metadata push (WASM only; null during server prerender). Replaces the old cold-start
    // count polling: the server pushes root counts once warm-up finishes and per-folder counts on
    // every content change.
    private NavHubClient? _hub;

    protected override async Task OnInitializedAsync()
    {
        _current = CurrentRoute();
        NavMgr.LocationChanged += OnLocationChanged;
        _root = await Provider.GetChildrenAsync(string.Empty);
        PublishRootStats();
        _scrollPending = true;

        // Subscribe to the metadata hub so folder counts and the footer total arrive by push — no
        // polling. Only in the browser; the server prerender has no hub registered.
        if (OperatingSystem.IsBrowser())
        {
            FolderArticleStats? total = await Provider.GetTotalAsync();
            if (total is { } site)
            {
                Stats.SetTotal(site);
            }
            if (total is not { Coverage: Coverage.Complete })
            {
                _ = ConvergeTotalAsync();
            }

            _hub = Services.GetService<NavHubClient>();
            if (_hub is not null)
            {
                _hub.MetadataChanged += OnAggregatesPushed;
                _hub.CountsReady += OnAggregatesPushed;
                _hub.Reconnected += OnHubReconnected;
                await _hub.StartAsync();
            }
        }
    }

    // SignalR remains the immediate update path, but correctness must not depend on its first
    // handshake. Pull the cheap site-root cell until startup discovery marks it complete.
    private async Task ConvergeTotalAsync()
    {
        for (int attempt = 0; attempt < 48; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            FolderArticleStats? total = await Provider.GetTotalAsync();
            if (total is not { } site)
            {
                continue;
            }

            Stats.SetTotal(site);
            if (site.Coverage == Coverage.Complete)
            {
                break;
            }
        }
    }

    // The footer total is the sum of the roots' own server-computed counts — never a sum of the
    // nodes the client happens to have rendered. One unknown root makes the total a lower bound.
    private void PublishRootStats()
    {
        if (_root is null)
        {
            return;
        }

        foreach (NavChild n in _root.Where(n => n.IsSection && n.Prefix is not null))
        {
            Stats.SetRoot(n.Prefix!, n.Text,
                new FolderArticleStats(n.ArticleCount ?? 0, n.LatestArticleUtc, null, n.CountCoverage));
        }
    }

    // Server pushed updated absolute folder aggregates (either a content change or the warm-up
    // CountsReady). Apply them to the cached tree locally (no refetch), seed the footer total from
    // the authoritative root values (works even when the tree isn't rendered), and nudge open
    // sections to re-read their now-updated cached counts.
    private void OnAggregatesPushed(IReadOnlyList<NavAggregateDelta> deltas)
        => _ = InvokeAsync(async () =>
        {
            // The empty prefix is the site root — the authoritative whole-site total. Applied before
            // any await so the footer updates immediately instead of queueing behind a tree
            // re-render, which can span dozens of open sections.
            foreach (NavAggregateDelta d in deltas.Where(d => d.Prefix.Length == 0))
            {
                Stats.SetTotal(new FolderArticleStats(d.ArticleCount, d.LatestUtc, d.Author, d.Coverage));
            }

            (Provider as HttpNavProvider)?.ApplyAggregates(deltas);
            _root = await Provider.GetChildrenAsync(string.Empty);
            PublishRootStats();

            Sidebar.RequestCountsRefresh();
            StateHasChanged();
        });

    // Reconnected after a drop → messages may have been missed while offline, so re-pull the root
    // level fresh from the origin and re-sync open sections. This is the only remaining fallback
    // (no interval polling).
    private void OnHubReconnected()
        => _ = InvokeAsync(async () =>
        {
            if (await Provider.GetTotalAsync() is { } total)
            {
                Stats.SetTotal(total);
            }
            _root = await Provider.RefreshChildrenAsync(string.Empty);
            PublishRootStats();
            Sidebar.RequestCountsRefresh();
            StateHasChanged();
        });

    private bool IsActiveRail(NavChild n) =>
        n.Prefix is not null && !string.IsNullOrEmpty(_current) &&
        _current.StartsWith(n.Prefix, StringComparison.OrdinalIgnoreCase);

    // Rail icon: navigate to the section's landing route if it has one; the sidebar stays collapsed
    // (hovering the rail opens the temporary flyout for full browsing).
    private void OnRailClick(NavChild n)
    {
        if (!string.IsNullOrEmpty(n.Route))
        {
            NavMgr.NavigateTo(n.Route);
        }
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        _query = e.Value?.ToString() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_query) && _index is null && !_indexing)
        {
            _indexing = true;
            _index = await Provider.GetIndexAsync();
            _indexing = false;
        }

        StateHasChanged();
    }

    private void ClearSearch() => _query = string.Empty;

    // Esc exits search mode and drops back to the tree, revealing/scrolling the active article.
    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && !string.IsNullOrEmpty(_query))
        {
            _query = string.Empty;
            _scrollPending = true;
        }
    }

    private static List<NavLeaf> Filter(IReadOnlyList<NavLeaf> index, string query) =>
        index.Where(l => l.Text.Contains(query, OIC) || l.Path.Contains(query, OIC))
             .Take(MaxResults)
             .ToList();

    // Wraps every case-insensitive occurrence of the query in a highlight <mark>.
    private RenderFragment Highlight(string text, string query) => builder =>
    {
        query = query?.Trim() ?? string.Empty;
        if (query.Length == 0 || string.IsNullOrEmpty(text))
        {
            builder.AddContent(0, text);
            return;
        }

        int seq = 0;
        int pos = 0;
        while (pos < text.Length)
        {
            int idx = text.IndexOf(query, pos, OIC);
            if (idx < 0)
            {
                builder.AddContent(seq++, text[pos..]);
                break;
            }

            if (idx > pos)
            {
                builder.AddContent(seq++, text[pos..idx]);
            }

            builder.OpenElement(seq++, "mark");
            builder.AddAttribute(seq++, "class", "nav-search-hl");
            builder.AddContent(seq++, text.Substring(idx, query.Length));
            builder.CloseElement();
            pos = idx + query.Length;
        }
    };

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _current = CurrentRoute();
        _scrollPending = true;
        InvokeAsync(StateHasChanged);
    }

    private string CurrentRoute()
    {
        string rel = NavMgr.ToBaseRelativePath(NavMgr.Uri);
        int cut = rel.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0)
        {
            rel = rel[..cut];
        }

        return rel.Trim('/');
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_scrollPending && _root is { Count: > 0 } && string.IsNullOrEmpty(_query) && !Sidebar.Collapsed)
        {
            _scrollPending = false;
            try { await JS.InvokeVoidAsync("appUi.scrollActiveNavIntoView"); } catch { /* prerender */ }
        }
    }

    public void Dispose()
    {
        NavMgr.LocationChanged -= OnLocationChanged;
        if (_hub is not null)
        {
            _hub.MetadataChanged -= OnAggregatesPushed;
            _hub.CountsReady -= OnAggregatesPushed;
            _hub.Reconnected -= OnHubReconnected;
        }
    }
}
