using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
using System.Net.Http.Json;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class MainLayout
{
    private bool _searchOpen;
    private bool _notifyOpen;
    private DotNetObjectReference<MainLayout>? _selfRef;
    private string _themeStorageKey = "lh-theme";
    private string? _defaultTheme;
    private int _themeRevision;
    private bool _themeReadPending;
    private bool _rerenderMermaid;
    private (string Key, string? Theme)? _pendingThemeWrite;

    private string BrandIconClass => string.IsNullOrWhiteSpace(Site.Branding.IconClass)
        ? "bi-lightbulb-fill"
        : Site.Branding.IconClass;

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

    protected override async Task OnInitializedAsync()
    {
        Theme.Changed += OnThemeChanged;
        Theme.PreferenceChanged += OnThemePreferenceChanged;
        Sidebar.Changed += OnSidebarChanged;

        // The footer counter is fed by the navigation menu as it loads (see NavStats): no dedicated
        // count query, and refreshes are debounced so they never impact rendering.
        Stats.Changed += OnStatsChanged;
        Article.Changed += OnArticleChanged;

        if (!Site.IsConfigured)
        {
            SiteShellOptions? site = Bootstrap.Site
                ?? await Http.GetFromJsonAsync<SiteShellOptions>("_site");
            if (site is not null)
            {
                Site.Apply(site);
                Bootstrap.Site = site;
            }
        }

        // The picker lists what this deployment offers, which only the site payload knows.
        Theme.SetCatalog(Site.Themes);
        SelectThemeContext();
        Site.Changed += OnSiteChanged;
        Navigation.LocationChanged += OnLocationChanged;
    }

    private void OnSiteChanged() => InvokeAsync(() =>
    {
        Theme.SetCatalog(Site.Themes);
        SelectThemeContext(force: true);
        StateHasChanged();
    });

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(() =>
    {
        SelectThemeContext();
        StateHasChanged();
    });

    private void SelectThemeContext(bool force = false)
    {
        SpaceShellInfo? space = Site.ResolveSpace(Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0]);
        string key = "lh-theme";
        string defaultTheme = Site.Branding.DefaultTheme;
        if (space is not null && !string.IsNullOrWhiteSpace(space.DefaultTheme))
        {
            key = $"lh-theme:{space.Id}";
            defaultTheme = space.DefaultTheme;
        }
        if (!force && key == _themeStorageKey && defaultTheme == _defaultTheme)
        {
            return;
        }

        _themeStorageKey = key;
        _defaultTheme = defaultTheme;
        _themeRevision++;
        _themeReadPending = true;
        Theme.ApplyPreference(defaultTheme);
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

    private void OnThemeChanged()
    {
        _rerenderMermaid = true;
        InvokeAsync(StateHasChanged);
    }

    private void OnThemePreferenceChanged(string? theme)
    {
        _themeRevision++;
        _themeReadPending = false;
        _pendingThemeWrite = (_themeStorageKey, theme);
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_themeReadPending)
        {
            _themeReadPending = false;
            int revision = _themeRevision;
            string? saved = null;
            try
            {
                saved = await JS.InvokeAsync<string?>("localStorage.getItem", _themeStorageKey);
            }
            catch (JSException ex)
            {
                Logger.LogWarning(ex, "Unable to read the saved theme; using the configured default");
            }

            // A navigation or reader choice while storage was read supersedes this result.
            if (revision == _themeRevision)
            {
                Theme.ApplyPreference(_defaultTheme, saved);
            }
        }

        if (_pendingThemeWrite is { } preference)
        {
            _pendingThemeWrite = null;
            try
            {
                if (preference.Theme is null)
                {
                    await JS.InvokeVoidAsync("localStorage.removeItem", preference.Key);
                }
                else
                {
                    await JS.InvokeVoidAsync("localStorage.setItem", preference.Key, preference.Theme);
                }
            }
            catch (JSException ex)
            {
                Logger.LogWarning(ex, "Unable to save the theme preference; the selection applies only to this page session");
            }
        }

        if (_rerenderMermaid)
        {
            _rerenderMermaid = false;
            await JS.InvokeVoidAsync("appUi.rerenderMermaid");
        }

        if (firstRender)
        {
            await JS.InvokeVoidAsync("appUi.initResizer");
            await JS.InvokeVoidAsync("appUi.initTocResizer");
            await JS.InvokeVoidAsync("appUi.initHeaderMetrics");
            _selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("appUi.initResponsive", _selfRef);
        }
    }

    public void Dispose()
    {
        Theme.Changed -= OnThemeChanged;
        Theme.PreferenceChanged -= OnThemePreferenceChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        Sidebar.Changed -= OnSidebarChanged;
        Site.Changed -= OnSiteChanged;
        Stats.Changed -= OnStatsChanged;
        Article.Changed -= OnArticleChanged;
        _selfRef?.Dispose();
    }
}
