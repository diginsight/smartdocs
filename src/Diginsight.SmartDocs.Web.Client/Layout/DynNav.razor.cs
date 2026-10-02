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
    [PersistentState] public string? PersistedScope { get; set; }
    [PersistentState] public NavChild[]? PersistedRoot { get; set; }

    private const int MaxResults = 200;
    private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    private IReadOnlyList<NavChild>? _root;
    private string _current = string.Empty;

    // Nav prefix of the space the reader is in (its route-base segment), or empty at the site root.
    // The tree shows only that space; moving to another space re-roots it.
    private string _scope = string.Empty;
    private bool _scrollPending;
    private CancellationTokenSource? _searchDebounce;

    private string _query = string.Empty;

    // What the reader has typed and what the list is actually showing are not the same thing while
    // a burst of typing is still in flight; the input follows _query, the results follow _applied.
    private string _applied = string.Empty;
    private IReadOnlyList<NavLeaf>? _index;
    private bool _indexing;

    // Live nav-metadata push (WASM only; null during server prerender). Replaces the old cold-start
    // count polling: the server pushes root counts once warm-up finishes and per-folder counts on
    // every content change.
    private NavHubClient? _hub;
    private bool _hubInitialized;

    protected override async Task OnInitializedAsync()
    {
        _current = CurrentRoute();
        _scope = SpaceScope.PrefixFor(Site, _current);
        NavMgr.LocationChanged += OnLocationChanged;
        if (string.Equals(PersistedScope, _scope, OIC) && PersistedRoot is not null)
        {
            _root = PersistedRoot;
        }
        else if (Bootstrap.TryGetLevel(_scope, out IReadOnlyList<NavChild> bootstrapped))
        {
            _root = bootstrapped;
        }
        else
        {
            _root = await SpaceScope.LoadRootAsync(Provider, Site, _scope);
        }

        PersistedScope = _scope;
        PersistedRoot = _root.ToArray();
        Bootstrap.SetLevel(_scope, _root);
        IEnumerable<string> recordPrefixes = _root
            .Where(static child => child.IsSection && child.Prefix is not null)
            .Select(static child => child.Prefix!)
            .Prepend(string.Empty);
        FolderRecord?[] records = await Task.WhenAll(
            recordPrefixes.Select(prefix => Provider.GetFolderAsync(prefix)));
        Bootstrap.SetFolderRecords(records.OfType<FolderRecord>());
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
        }
    }

    private async Task InitializeHubAfterIdleAsync()
    {
        if (!OperatingSystem.IsBrowser() || _hubInitialized)
        {
            return;
        }

        _hubInitialized = true;
        await JS.InvokeVoidAsync("appUi.waitForIdle");

        _hub = Services.GetService<NavHubClient>();
        if (_hub is not null)
        {
            _hub.MetadataChanged += OnAggregatesPushed;
            _hub.CountsReady += OnAggregatesPushed;
            _hub.Reconnected += OnHubReconnected;
            await _hub.StartAsync();
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
    private void OnAggregatesPushed(IReadOnlyList<FolderRecord> records)
        => _ = InvokeAsync(async () =>
        {
            // The empty prefix is the site root — the authoritative whole-site total. Applied before
            // any await so the footer updates immediately instead of queueing behind a tree
            // re-render, which can span dozens of open sections.
            foreach (FolderRecord record in records.Where(record => record.Prefix.Length == 0))
            {
                Stats.SetTotal(new FolderArticleStats(
                    record.ArticleCount,
                    record.LatestArticleUtc,
                    null,
                    record.Coverage));
            }

            (Provider as HttpNavProvider)?.ApplyFolderRecords(records);
            _root = await SpaceScope.LoadRootAsync(Provider, Site, _scope);
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
            _root = await SpaceScope.LoadRootAsync(Provider, Site, _scope, refresh: true);
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

    // Every keystroke refilters the whole library and repaints the list — a single letter matches
    // enough to fill it — so a burst of typing repaints once at the end rather than once per letter.
    private const int SearchDebounceMs = 120;

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        _query = e.Value?.ToString() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_query) && _index is null && !_indexing)
        {
            _indexing = true;
            _index = await Provider.GetIndexAsync();
            _indexing = false;
        }

        CancellationTokenSource cts = new ();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _searchDebounce, cts);
        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        // Clearing the box must not wait: the tree should come back the moment the text goes.
        if (_query.Length == 0)
        {
            _applied = string.Empty;
            return;
        }

        try
        {
            await Task.Delay(SearchDebounceMs, cts.Token);
            _applied = _query;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // A later keystroke owns the repaint.
        }
    }

    private void ClearSearch() => _query = _applied = string.Empty;

    // Esc exits search mode and drops back to the tree, revealing/scrolling the active article.
    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && !string.IsNullOrEmpty(_query))
        {
            _query = _applied = string.Empty;
            _scrollPending = true;
        }
    }

    // Menu search stays inside the current space, like the tree it replaces.
    private List<NavLeaf> Filter(IReadOnlyList<NavLeaf> index, string query) =>
        index.Where(l => SpaceScope.InScope(Site, _scope, l.Route)
                         && (l.Text.Contains(query, OIC) || l.Path.Contains(query, OIC)))
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

        string scope = SpaceScope.PrefixFor(Site, _current);
        if (!string.Equals(scope, _scope, OIC))
        {
            _scope = scope;
            _ = InvokeAsync(async () =>
            {
                IReadOnlyList<NavChild> root = await SpaceScope.LoadRootAsync(Provider, Site, scope);
                if (!string.Equals(scope, _scope, OIC))
                {
                    return; // a later navigation already re-rooted the tree
                }

                _root = root;
                PublishRootStats();
                _scrollPending = true;
                StateHasChanged();
            });
        }

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
        if (firstRender)
        {
            await InitializeHubAfterIdleAsync();
        }

        if (_scrollPending && _root is { Count: > 0 } && string.IsNullOrEmpty(_applied) && !Sidebar.Collapsed)
        {
            _scrollPending = false;
            try { await JS.InvokeVoidAsync("appUi.scrollActiveNavIntoView"); } catch { /* prerender */ }
        }
    }

    public void Dispose()
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        NavMgr.LocationChanged -= OnLocationChanged;
        if (_hub is not null)
        {
            _hub.MetadataChanged -= OnAggregatesPushed;
            _hub.CountsReady -= OnAggregatesPushed;
            _hub.Reconnected -= OnHubReconnected;
        }
    }
}
