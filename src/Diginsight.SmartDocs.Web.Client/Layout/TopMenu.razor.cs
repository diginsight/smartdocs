using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class TopMenu : IDisposable
{
    [PersistentState] public string? PersistedScope { get; set; }
    [PersistentState] public NavChild[]? PersistedRoot { get; set; }

    public enum Group { Left, Right }

    [Parameter] public Group Placement { get; set; } = Group.Left;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // Top-level items shown in the bar (the Home link + top-level sections), built live from the
    // content hierarchy via the runtime nav provider. Their labels/icons/short/visibility/side come
    // straight from the folder metadata carried on each NavChild.
    private IReadOnlyList<NavChild>? _root;

    // Immediate children of each displayed section, cached in-memory for the dropdown.
    private readonly Dictionary<string, IReadOnlyList<NavChild>> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _loading = new(StringComparer.OrdinalIgnoreCase);

    // Prefix of the section whose dropdown is pinned open by a CLICK (hover opens independently via CSS).
    private string? _openKey;

    // Nav prefix of the space the reader is in; the band shows that space's top level only.
    private string _scope = string.Empty;

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        _scope = SpaceScope.PrefixFor(Site, Navigation.ToBaseRelativePath(Navigation.Uri));
        await LoadAsync(_scope);
    }

    private async Task LoadAsync(string scope)
    {
        IReadOnlyList<NavChild> root;
        if (string.Equals(PersistedScope, scope, StringComparison.OrdinalIgnoreCase) &&
            PersistedRoot is not null)
        {
            root = PersistedRoot;
        }
        else if (Bootstrap.TryGetLevel(scope, out IReadOnlyList<NavChild> bootstrapped))
        {
            root = bootstrapped;
        }
        else
        {
            root = await SpaceScope.LoadRootAsync(NavProvider, Site, scope);
        }
        if (!string.Equals(scope, _scope, StringComparison.OrdinalIgnoreCase))
        {
            return; // a later navigation already re-rooted the band
        }

        // The top bar shows the Home link plus top-level sections; other root links
        // (e.g. Getting Started, Documentation Index) stay in the sidebar only.
        _root = root.Where(c => c.IsSection || SpaceScope.IsHome(c, scope)).ToList();
        PersistedScope = scope;
        PersistedRoot = _root.ToArray();
        Bootstrap.SetLevel(scope, root);

        StateHasChanged();
    }

    // Click toggles a pinned-open dropdown (lazy-loading its children if the prefetch has not landed).
    private async Task ToggleAsync(NavChild node)
    {
        if (node.Prefix is null)
        {
            return;
        }

        _openKey = _openKey == node.Prefix ? null : node.Prefix;
        if (_openKey is not null)
        {
            await LoadChildrenAsync(node);
        }
    }

    private Task LoadChildrenAsync(NavChild node)
    {
        if (node.Prefix is not { } prefix || _children.ContainsKey(prefix))
        {
            return Task.CompletedTask;
        }

        if (_loading.TryGetValue(prefix, out Task? pending))
        {
            return pending;
        }

        Task load = LoadCoreAsync(prefix);
        _loading[prefix] = load;
        return load;
    }

    private async Task LoadCoreAsync(string prefix)
    {
        try
        {
            _children[prefix] = await NavProvider.GetChildrenAsync(prefix);
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _loading.Remove(prefix);
        }
    }

    private bool IsOpen(NavChild node) => node.Prefix is not null && _openKey == node.Prefix;

    // Close the pinned dropdown after a navigation (e.g. selecting a dropdown link), and re-root the
    // band when the navigation crossed into another space.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        string scope = SpaceScope.PrefixFor(Site, Navigation.ToBaseRelativePath(e.Location));
        bool rescoped = !string.Equals(scope, _scope, StringComparison.OrdinalIgnoreCase);
        if (_openKey is null && !rescoped)
        {
            return;
        }

        _openKey = null;
        if (rescoped)
        {
            _scope = scope;
            InvokeAsync(() => LoadAsync(scope));
            return;
        }

        InvokeAsync(StateHasChanged);
    }

    // Folders marked `topbar-hidden` in metadata.yml are dropped from the top bar (still in the sidebar).
    // A node is LEFT when its metadata says `topbar-align: left`; link items with no folder prefix
    // (e.g. Home) default left; unmarked section folders default right.
    private static bool IsLeft(NavChild c) =>
        c.TopbarAlign is { } align
            ? align.Equals("left", StringComparison.OrdinalIgnoreCase)
            : string.IsNullOrEmpty(c.Prefix);

    private IEnumerable<NavChild> DisplayNodes()
    {
        if (_root is null)
        {
            return Enumerable.Empty<NavChild>();
        }

        IEnumerable<NavChild> items = _root.Where(c => !c.TopbarHidden);
        return Placement == Group.Right ? items.Where(c => !IsLeft(c)) : items.Where(IsLeft);
    }

    // Compact top-level label from folder metadata (`short:`); falls back to the full name.
    private static string ShortFor(NavChild c) =>
        !string.IsNullOrEmpty(c.Short) ? c.Short! : c.Text;

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;
}
