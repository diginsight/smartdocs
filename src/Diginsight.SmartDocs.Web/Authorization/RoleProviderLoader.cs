using System.Reflection;
using System.Runtime.Loader;
using Diginsight.SmartDocs.Authorization;
using Diginsight.SmartDocs.Web.Endpoints;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Diginsight.SmartDocs.Web.Authorization;

public static class RoleProviderLoader
{
    public static IServiceCollection AddRoleProvider(this IServiceCollection services,
        IConfiguration configuration, SiteOptions site, SignInConfiguration signIn,
        ILogger logger, string? baseDirectory = null)
    {
        IConfigurationSection section = configuration.GetSection(RoleProviderOptions.Section);
        RoleProviderOptions options = section.Get<RoleProviderOptions>() ?? new();
        options.Validate(site.Authorization, signIn.Enabled);
        services.AddSingleton(options);

        foreach (SpaceOptions space in site.Spaces)
        {
            if (space.RoleScope?.Any(char.IsControl) == true)
            {
                throw new InvalidOperationException($"Site:Spaces:{space.Id}:RoleScope must not contain control characters.");
            }
            if (options.IsPlugin == string.IsNullOrWhiteSpace(space.RoleScope))
            {
                logger.LogWarning("Space {Space} has {RoleScopeState} RoleScope while the role source is {RoleSource}",
                    space.Id, string.IsNullOrWhiteSpace(space.RoleScope) ? "no" : "a",
                    options.IsPlugin ? "a plugin" : "Entra ID");
            }
        }

        if (!options.IsPlugin)
        {
            services.AddSingleton<IRoleProvider, EntraIdRoleProvider>();
            return services;
        }

        try
        {
            string path = ResolvePath(baseDirectory ?? AppContext.BaseDirectory, options.Assembly!);
            var context = new RoleProviderLoadContext(path);
            Assembly assembly = context.LoadFromAssemblyPath(path);
            Type type = assembly.GetType(options.Type!, throwOnError: true)!;
            if (!type.IsVisible || type.IsAbstract || !typeof(IRoleProviderModule).IsAssignableFrom(type) ||
                type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException("Type must be a public, concrete IRoleProviderModule with a public parameterless constructor.");
            }

            var module = (IRoleProviderModule)Activator.CreateInstance(type)!;
            module.Register(services, section.GetSection("Settings"));
            if (services.Count(static d => d.ServiceType == typeof(IRoleProvider)) != 1)
            {
                throw new InvalidOperationException("Type must register exactly one IRoleProvider.");
            }

            services.AddSingleton<RoleProviderSignInHook>();
            services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .PostConfigure<RoleProviderSignInHook>(static (oidc, hook) => hook.Configure(oidc));
            logger.LogInformation("Loaded role provider assembly {Assembly} version {Version} at {Location}, module {Type}",
                assembly.GetName().Name, assembly.GetName().Version, assembly.Location, type.FullName);
            return services;
        }
        catch (Exception ex)
        {
            // A configured plugin must never silently select the token's roles instead.
            throw new InvalidOperationException(
                $"{RoleProviderOptions.Section}:Assembly or :Type failed to load or register the role provider.", ex);
        }
    }

    private static string ResolvePath(string baseDirectory, string configuredPath)
    {
        string normalized = configuredPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || normalized.Split(Path.DirectorySeparatorChar).Any(static p => p is ".." || p.Contains(':')))
        {
            throw new InvalidOperationException("Assembly must be a relative path without '..' or rooted segments.");
        }

        string root = Path.GetFullPath(Path.Combine(baseDirectory, "plugins"));
        string path = Path.GetFullPath(Path.Combine(baseDirectory, normalized));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new InvalidOperationException("Assembly must name an existing file inside the host's plugins folder.");
        }

        // Deployment owns this tree; do not allow a junction to defeat its path boundary.
        for (string? current = path; current is not null && current.Length >= root.Length; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Assembly path must not traverse symbolic links or junctions.");
            }
        }
        return path;
    }
}

public sealed class RoleProviderLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: false)
{
    private static readonly HashSet<string> Shared = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(static p => Path.GetFileNameWithoutExtension(p))
        .Append(typeof(IRoleProvider).Assembly.GetName().Name!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    private readonly AssemblyDependencyResolver resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && Shared.Contains(assemblyName.Name))
        {
            return null;
        }
        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}

public sealed class EntraIdRoleProvider : IRoleProvider
{
    public Task<ResolvedRoles> ResolveAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(new ResolvedRoles(RoleSet.FromPrincipal(user).Roles, []));
}
