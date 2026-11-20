using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diginsight.SmartDocs.Authorization;

/// <summary>Resolves roles for a reader. Permission mappings remain the host's responsibility.</summary>
public interface IRoleProvider
{
    Task<ResolvedRoles> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}

public sealed record ResolvedRoles(IReadOnlySet<string> Global, IReadOnlyList<ScopedRole> Scoped);

public sealed record ScopedRole(string Role, string Scope);

/// <summary>Registers exactly one role provider and validates its private settings at startup.</summary>
public interface IRoleProviderModule
{
    void Register(IServiceCollection services, IConfiguration settings);
}

/// <summary>Optionally requests delegated API scopes during the host's reader sign-in.</summary>
public interface IUserDelegatedRoleProviderModule : IRoleProviderModule
{
    IReadOnlyList<string> GetDelegatedScopes(IConfiguration settings);
}
