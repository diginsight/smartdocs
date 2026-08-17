using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class TopMenu : IDisposable
{
    public enum Group { Left, Right }

    [Parameter] public Group Placement { get; set; } = Group.Left;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // Top-level items shown in the bar (the Home link + top-level sections), built live from the
    // content hierarchy via the runtime nav provider. Their labels/icons/short/visibility/side come
    // straight from the folder metadata carried on each NavChild.
    private IReadOnlyList<NavChild>? _root;

    // Immediate children of each displayed section, cached in-memory for the dropdown.
    private readonly Dictionary<string, IReadOnlyList<NavChild>> _children = new(StringComparer.OrdinalIgnoreCase);

    // Prefix of the section whose dropdown is pinned open by a CLICK (hover opens independently via CSS).
    private string? _openKey;

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        IReadOnlyList<NavChild> root = await NavProvider.GetChildrenAsync(string.Empty);

        // The top bar shows the Home link (empty route) plus top-level sections; other root links
        // (e.g. Getting Started, Documentation Index) stay in the sidebar only.
        _root = root.Where(c => c.IsSection || string.IsNullOrEmpty(c.Route)).ToList();

        // Render the top-level buttons NOW; then fill each dropdown as its children arrive. Without
        // this the whole menu stays blank until ALL sections' children have loaded, so a slow level
        // build (e.g. during the startup warm-up) makes the menu look broken.
        StateHasChanged();

        foreach (NavChild section in DisplayNodes().Where(c => c.IsSection && c.Prefix is not null))
        {
            _children[section.Prefix!] = await NavProvider.GetChildrenAsync(section.Prefix!);
            StateHasChanged();
        }
    }

    // Click toggles a pinned-open dropdown (lazy-loading its children if the prefetch has not landed).
    private async Task ToggleAsync(NavChild node)
    {
        if (node.Prefix is null)
        {
            return;
        }

        _openKey = _openKey == node.Prefix ? null : node.Prefix;
        if (_openKey is not null && !_children.ContainsKey(node.Prefix))
        {
            _children[node.Prefix] = await NavProvider.GetChildrenAsync(node.Prefix);
        }
    }

    private bool IsOpen(NavChild node) => node.Prefix is not null && _openKey == node.Prefix;

    // Close the pinned dropdown after a navigation (e.g. selecting a dropdown link).
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_openKey is null)
        {
            return;
        }

        _openKey = null;
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
