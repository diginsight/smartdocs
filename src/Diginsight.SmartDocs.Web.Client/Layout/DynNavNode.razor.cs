using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class DynNavNode
{
    [Parameter, EditorRequired] public NavChild Node { get; set; } = default!;
    [Parameter] public string CurrentRoute { get; set; } = string.Empty;

    // Parent section info for article-hover reporting: when hovering an article, the footer shows
    // its containing section's label and count.
    [Parameter] public string? ParentPrefix { get; set; }
    [Parameter] public string? ParentLabel { get; set; }
    [Parameter] public int? ParentCount { get; set; }
    [Parameter] public Coverage ParentCoverage { get; set; } = Coverage.None;

    private bool _open;
    private IReadOnlyList<NavChild>? _children;

    // The active route this section was last auto-opened for. Prevents later re-renders from
    // re-opening a section the user has explicitly collapsed while staying on the same article.
    private string? _autoOpenedForRoute;

    private bool InActiveBranch =>
        Node.Prefix is not null && !string.IsNullOrEmpty(CurrentRoute) &&
        (string.Equals(CurrentRoute, Node.Route, StringComparison.OrdinalIgnoreCase) ||
         CurrentRoute.StartsWith(Node.Prefix + "/", StringComparison.OrdinalIgnoreCase));

    protected override void OnInitialized()
    {
        Sidebar.ExpandAllRequested += OnExpandAll;
        Sidebar.RefreshCountsRequested += OnRefreshCounts;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!Node.IsSection)
        {
            // When this article IS the active route, report its parent section as the footer baseline.
            // This handles navigation (click) where no new mouseover fires because the pointer is
            // already over the element when the DOM re-renders. It is the lowest-priority tier, so it
            // never overwrites a hover/focus highlight.
            if (ParentPrefix is not null && !string.IsNullOrEmpty(Node.Route) &&
                string.Equals(CurrentRoute, Node.Route, StringComparison.OrdinalIgnoreCase))
            {
                Stats.SetSelectedSection(ParentPrefix, ParentLabel, ParentCount, ParentCoverage);
            }

            return;
        }

        // Auto-open the active branch once per active route (so a user who then collapses it with the
        // arrow keys / twisty isn't fought by later re-renders), and keep sections open while
        // expand-all is on.
        bool wantOpen = (InActiveBranch && _autoOpenedForRoute != CurrentRoute) || Sidebar.AllExpanded;
        if (wantOpen && !_open)
        {
            _open = true;
            if (_children is null && Node.Prefix is not null)
            {
                _children = await Provider.GetChildrenAsync(Node.Prefix);
            }
        }

        // Remember we've handled this active route, whether we just opened it or the user is free to
        // keep it collapsed from here on.
        if (InActiveBranch)
        {
            _autoOpenedForRoute = CurrentRoute;
        }
    }

    // Broadcast handler: expand (true) opens + lazily loads; the child renders then cascade further.
    private async void OnExpandAll(bool expand)
    {
        if (!Node.IsSection)
        {
            return;
        }

        _open = expand;
        if (expand && _children is null && Node.Prefix is not null)
        {
            _children = await Provider.GetChildrenAsync(Node.Prefix);
        }

        await InvokeAsync(StateHasChanged);
    }

    // Folder click / Enter: closed → open + select (navigate to) the first article under it;
    // open → collapse (no navigation). Children load lazily on first open.
    private async Task OnSummaryActivate()
    {
        if (_open)
        {
            _open = false;
            return;
        }

        _open = true;
        if (_children is null && Node.Prefix is not null)
        {
            _children = await Provider.GetChildrenAsync(Node.Prefix);
        }

        NavChild? first = _children?.FirstOrDefault(c => !c.IsSection && !string.IsNullOrEmpty(c.Route));
        if (first?.Route is not null)
        {
            NavMgr.NavigateTo(first.Route);
        }
        else if (!string.IsNullOrEmpty(Node.Route))
        {
            NavMgr.NavigateTo(Node.Route);
        }
    }

    // Twisty (chevron) / arrow keys / Space: expand or collapse only — no navigation.
    private async Task ToggleOpenOnly()
    {
        _open = !_open;
        if (_open && _children is null && Node.Prefix is not null)
        {
            _children = await Provider.GetChildrenAsync(Node.Prefix);
        }
    }

    // Pointer entered or keyboard focused this item ("marked for selection") → highest-priority footer
    // override. Sections report themselves (the folder itself); articles report their parent section.
    private void OnPointerEnter()
    {
        if (Node.IsSection && Node.Prefix is not null)
        {
            Stats.SetHoverSection(Node.Prefix, Node.Text, Node.ArticleCount, Node.CountCoverage);
        }
        else if (ParentPrefix is not null)
        {
            Stats.SetHoverSection(ParentPrefix, ParentLabel, ParentCount, ParentCoverage);
        }
    }

    // Pointer/focus left this item → drop the override (guarded by key) so the footer reverts to the
    // selected article's section.
    private void OnPointerLeave()
    {
        if (Node.IsSection && Node.Prefix is not null)
        {
            Stats.ClearHoverSection(Node.Prefix);
        }
        else if (ParentPrefix is not null)
        {
            Stats.ClearHoverSection(ParentPrefix);
        }
    }

    // A metadata push updated the client cache in place → re-read this section's child level so the
    // counts handed down to children reflect the new values. Cache read, no API hit.
    private async void OnRefreshCounts()
    {
        if (!Node.IsSection || Node.Prefix is null || _children is null)
        {
            return;
        }

        _children = await Provider.GetChildrenAsync(Node.Prefix);
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Sidebar.ExpandAllRequested -= OnExpandAll;
        Sidebar.RefreshCountsRequested -= OnRefreshCounts;
    }
}
