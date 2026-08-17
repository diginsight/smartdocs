using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class MainLayout
{
    private bool _searchOpen;
    private bool _notifyOpen;
    private DotNetObjectReference<MainLayout>? _selfRef;

    private string SectionLine
    {
        get
        {
            if (Stats.ActiveSectionLabel is { } label && Stats.ActiveSectionCount is { } count)
            {
                return Stats.ActiveSectionCoverage switch
                {
                    Coverage.Complete => $"{label}: {count:N0} articles",
                    Coverage.Partial => $"{label}: \u2265 {count:N0} articles",
                    _ => $"{label}: \u2026",
                };
            }

            return string.Empty;
        }
    }

    // "…" while nothing is known and "≥ N" while the scan is still running: a lower bound is never
    // presented as a total, and unknown is never presented as zero.
    private string TotalArticlesText
    {
        get
        {
            if (!Stats.HasData || Stats.TotalCoverage == Coverage.None)
            {
                return "…";
            }

            return Stats.TotalCoverage == Coverage.Complete
                ? $"{Stats.TotalArticles:N0} articles"
                : $"\u2265 {Stats.TotalArticles:N0} articles";
        }
    }

    private string ArticleLine => Article.Title is { } title ? $"Article: {title}" : string.Empty;

    private string ArticleMetaLine
    {
        get
        {
            if (Article.WordCount is { } wc)
            {
                return $"Words: {wc:N0}";
            }

            return string.Empty;
        }
    }

    protected override void OnInitialized()
    {
        Theme.Changed += OnThemeChanged;
        Sidebar.Changed += OnSidebarChanged;

        // The footer counter is fed by the navigation menu as it loads (see NavStats): no dedicated
        // count query, and refreshes are debounced so they never impact rendering.
        Stats.Changed += OnStatsChanged;
        Article.Changed += OnArticleChanged;
    }

    private void OnArticleChanged() => InvokeAsync(StateHasChanged);

    private void OnStatsChanged() => InvokeAsync(StateHasChanged);

    // Called from JS when the viewport crosses the responsive breakpoint: narrow → collapse the
    // sidebar to the icon rail (still usable via the hover flyout); wide → expand it.
    [JSInvokable]
    public Task SetSidebarCollapsed(bool collapsed)
    {
        Sidebar.SetCollapsed(collapsed);
        return Task.CompletedTask;
    }

    private void OnSidebarChanged() => InvokeAsync(StateHasChanged);

    private async void OnThemeChanged()
    {
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", "lh-theme", Theme.ThemeId);
            await JS.InvokeVoidAsync("appUi.rerenderMermaid");
        }
        catch
        {
            /* JS not available during prerender */
        }

        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            string? saved = await JS.InvokeAsync<string?>("localStorage.getItem", "lh-theme");
            Theme.SetTheme(saved);
            await JS.InvokeVoidAsync("appUi.initResizer");
            await JS.InvokeVoidAsync("appUi.initTocResizer");
            _selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("appUi.initResponsive", _selfRef);
        }
    }

    public void Dispose()
    {
        Theme.Changed -= OnThemeChanged;
        Sidebar.Changed -= OnSidebarChanged;
        Stats.Changed -= OnStatsChanged;
        Article.Changed -= OnArticleChanged;
        _selfRef?.Dispose();
    }
}
