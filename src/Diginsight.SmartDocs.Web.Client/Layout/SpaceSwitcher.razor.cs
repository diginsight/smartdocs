using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Components.Routing;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class SpaceSwitcher
{
    private SpaceShellInfo? _current;

    protected override void OnInitialized()
    {
        Resolve(Navigation.Uri);
        Navigation.LocationChanged += OnLocationChanged;
        Site.Changed += OnSiteChanged;
    }

    private void Resolve(string uri) =>
        _current = Site.ResolveSpace(Navigation.ToBaseRelativePath(uri).Split('?', '#')[0]);

    private bool IsCurrent(SpaceShellInfo space) =>
        _current is not null && string.Equals(_current.Id, space.Id, StringComparison.OrdinalIgnoreCase);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        SpaceShellInfo? before = _current;
        Resolve(e.Location);
        if (!ReferenceEquals(before, _current))
        {
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnSiteChanged()
    {
        Resolve(Navigation.Uri);
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        Site.Changed -= OnSiteChanged;
    }
}
