using System.Security.Claims;
using Diginsight.SmartDocs.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleRoleProvider;

public sealed class SampleModule : IRoleProviderModule
{
    public void Register(IServiceCollection services, IConfiguration settings) =>
        services.AddSingleton<IRoleProvider, SampleProvider>();
}

public sealed class SampleProvider : IRoleProvider
{
    public Task<ResolvedRoles> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(new ResolvedRoles(new HashSet<string>(), [new("Reader", "repo-a")]));
}

public abstract class AbstractModule : IRoleProviderModule
{
    public abstract void Register(IServiceCollection services, IConfiguration settings);
}

public sealed class NotAModule;

public sealed class EmptyModule : IRoleProviderModule
{
    public void Register(IServiceCollection services, IConfiguration settings) { }
}

public sealed class DuplicateModule : IRoleProviderModule
{
    public void Register(IServiceCollection services, IConfiguration settings)
    {
        services.AddSingleton<IRoleProvider, SampleProvider>();
        services.AddSingleton<IRoleProvider, SampleProvider>();
    }
}
