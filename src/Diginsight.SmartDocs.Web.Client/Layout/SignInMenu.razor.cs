using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;

namespace Diginsight.SmartDocs.Web.Client.Layout;

/// <summary>
/// <b>Sign in</b> for anonymous readers, and the reader's name with <b>Sign out</b> for signed-in
/// readers. Rendered only when the host signs readers in.
/// </summary>
public partial class SignInMenu
{
    private const string SignInPath = "/_auth/signin";
    private const string SignOutPath = "/_auth/signout";

    private ClaimsPrincipal? _user;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    private string DisplayName =>
        _user?.Identity?.Name is { Length: > 0 } name ? name : "Signed in";

    // The current page, as a local path the sign-in endpoint accepts as its return address.
    private string SignInUrl =>
        $"{SignInPath}?returnUrl={Uri.EscapeDataString("/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('#')[0])}";

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        Site.Changed += OnChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        _user = AuthenticationState is null ? null : (await AuthenticationState).User;
    }

    private void SignIn() => Navigation.NavigateTo(SignInUrl, forceLoad: true);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        Site.Changed -= OnChanged;
    }
}
