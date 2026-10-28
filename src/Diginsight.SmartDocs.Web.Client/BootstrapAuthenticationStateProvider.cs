using System.Security.Claims;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components.Authorization;

namespace Diginsight.SmartDocs.Web.Client;

/// <summary>
/// The reader's sign-in state in the browser, restored from the host's bootstrap record. This host
/// carries prerendered state in that record rather than in the framework's persisted-state payload,
/// so the reader travels the same way. The state only drives the shell: the server alone decides
/// what the reader may see.
/// </summary>
public sealed class BootstrapAuthenticationStateProvider(NavigationBootstrapState bootstrap) : AuthenticationStateProvider
{
    private const string AuthenticationType = "SmartDocsBootstrap";
    private const string NameClaimType = "name";

    private static readonly Task<AuthenticationState> Anonymous =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (bootstrap.Reader is not { } reader)
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(
            string.IsNullOrEmpty(reader.Name) ? [] : [new Claim(NameClaimType, reader.Name)],
            AuthenticationType,
            NameClaimType,
            ClaimTypes.Role);
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }
}
