using System.Diagnostics;
using System.Runtime.Loader;
using System.Security.Claims;
using Diginsight.SmartDocs.Authorization;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Endpoints;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class RoleProviderChecks
{
    public static async Task RunAsync(Action<bool, string> assert)
    {
        assert(RoleSet.Anonymous.Id == "anonymous", "anonymous role-set identifier pin");
        assert(RoleSet.SignedIn([]).Id == "s-314bc2162f251895cd4ac6a1b2eb6e07", "empty signed-in identifier pin");
        assert(RoleSet.SignedIn(["Reader"]).Id == "s-1206c16b28958ac3c90a1103468b0afc", "reader identifier pin");
        assert(RoleSet.SignedIn(["Reader", "Contributor"]).Id == "s-0789f10ec87b89ec74b84cfdacc0b8a3", "two-role identifier pin");
        RoleSet scoped = RoleSet.SignedIn(["Reader"], [new("Contributor", "repo-a"), new("Reader", "repo-b")]);
        assert(scoped.Id != RoleSet.SignedIn(["Reader"]).Id &&
            scoped.Id == RoleSet.SignedIn(["reader"], [new("reader", "REPO-B"), new("CONTRIBUTOR", " REPO-A ")]).Id,
            "scoped identifiers are order/case independent and separate from global sets");
        assert(scoped.RolesIn(null).SetEquals(["Reader"]) && scoped.RolesIn("repo-b").SetEquals(["Reader"]) &&
            scoped.RolesIn(" REPO-A ").SetEquals(["Reader", "Contributor"]), "scope lookup includes globals only where applicable");
        var extra = new ClaimsIdentity([new Claim(RoleSet.ScopedRolesClaimType, "Contributor@repo-a")], "second");
        ClaimsPrincipal principal = Principal();
        principal.AddIdentity(extra);
        principal.Identities.First().AddClaims([
            new(RoleSet.ScopedRolesClaimType, "bad"),
            new(RoleSet.ScopedRolesClaimType, "Reader@"),
            new(RoleSet.ScopedRolesClaimType, "invalid role@repo-b"),
            new(RoleSet.ScopedRolesClaimType, "Reader@repo-b\nrepo-c"),
            new(RoleSet.ScopedRolesClaimType, "Reader@repo-b\n"),
        ]);
        assert(RoleSet.FromPrincipal(principal).RolesIn("repo-a").Contains("Contributor") &&
            RoleSet.FromPrincipal(principal).ScopedRoles.Count == 2 &&
            !RoleSet.FromPrincipal(principal).RolesIn("repo-b").Contains("Reader"),
            "principal reads scoped claims across identities and ignores malformed values");
        var site = Site();
        assert(!System.Text.Json.JsonSerializer.Serialize(SpaceShellInfo.From(site.Spaces[0])).Contains("RoleScope"),
            "shell projection does not disclose RoleScope");
        await LoaderAsync(assert);
        await HookAsync(assert);
    }

    private static async Task LoaderAsync(Action<bool, string> assert)
    {
        var defaults = new ServiceCollection();
        defaults.AddRoleProvider(new ConfigurationBuilder().Build(), Site(), new(true), NullLogger.Instance);
        using (ServiceProvider provider = defaults.BuildServiceProvider())
        {
            assert(provider.GetRequiredService<IRoleProvider>() is EntraIdRoleProvider &&
                defaults.All(static d => d.ServiceType != typeof(IPostConfigureOptions<OpenIdConnectOptions>)),
                "default registers Entra provider without OIDC hook");
            ResolvedRoles roles = await provider.GetRequiredService<IRoleProvider>().ResolveAsync(Principal(), default);
            assert(roles.Global.SetEquals(["Legacy"]) && roles.Scoped.Count == 0, "Entra provider mirrors existing global role claims");
        }

        var services = new ServiceCollection();
        services.AddRoleProvider(Config(), Site(), new(true), NullLogger.Instance);
        using (ServiceProvider provider = services.BuildServiceProvider())
        {
            IRoleProvider loaded = provider.GetRequiredService<IRoleProvider>();
            assert(AssemblyLoadContext.GetLoadContext(loaded.GetType().Assembly) != AssemblyLoadContext.Default &&
                (await loaded.ResolveAsync(Principal(), default)).Scoped.Count == 1,
                "sample plugin loads outside default context with shared contract identity");
        }
        foreach (string path in new[] { @"C:\provider.dll", @"\\host\share\x.dll", @"\plugins\x.dll", @"plugins\..\x.dll",
            "outside.dll", @"plugins\missing.dll" })
        {
            Reject(Config(assembly: path), path);
        }
        foreach (string type in new[] { "Missing", "AbstractModule", "NotAModule", "EmptyModule", "DuplicateModule" })
        {
            Reject(Config(type: "SampleRoleProvider." + type), type);
        }
        foreach ((string key, string value) in new[]
        {
            ("Type", ""), ("TimeoutSeconds", "0"), ("TimeoutSeconds", "31"), ("MaxScopedRoles", "0"),
            ("DegradedSessionLifetime", "00:00:00"), ("DegradedSessionLifetime", "09:00:00"),
        })
        {
            Reject(Config(extra: new(key, value)), key);
        }
        try
        {
            new ServiceCollection().AddRoleProvider(Config(), Site(), new(false), NullLogger.Instance);
            assert(false, "plugin without sign-in must fail");
        }
        catch (InvalidOperationException ex)
        {
            assert(ex.Message.Contains("AzureAd:ClientId"), "plugin requires reader sign-in");
        }

        void Reject(IConfiguration config, string label)
        {
            try
            {
                new ServiceCollection().AddRoleProvider(config, Site(), new(true), NullLogger.Instance);
                assert(false, $"invalid provider setting must fail: {label}");
            }
            catch (InvalidOperationException ex)
            {
                assert(ex.Message.Contains(RoleProviderOptions.Section), $"startup rejects invalid provider setting: {label}");
            }
        }
    }

    private static async Task HookAsync(Action<bool, string> assert)
    {
        int previous = 0;
        var oidc = new OpenIdConnectOptions();
        oidc.Events.OnTicketReceived = context =>
        {
            previous++;
            context.Principal!.Identities.First().AddClaim(new("previous", "yes"));
            return Task.CompletedTask;
        };
        var logger = new Warnings();
        var options = new RoleProviderOptions { TimeoutSeconds = 1, MaxScopedRoles = 1 };
        var hook = new RoleProviderSignInHook(new Fake((user, _) =>
        {
            assert(previous == 1 && user.HasClaim("previous", "yes"), "previous OIDC handler runs once before provider");
            return Task.FromResult(new ResolvedRoles(new HashSet<string> { "Reader", "invalid role" },
                [new("Reader", "repo-b"), new("Contributor", "repo-a"), new("contributor", "REPO-A"), new("Admin", "unknown")]));
        }), options, Options.Create(Site()), logger);
        hook.Configure(oidc);
        TicketReceivedContext success = Context(oidc);
        await oidc.Events.OnTicketReceived(success);
        RoleSet roles = RoleSet.FromPrincipal(success.Principal);
        assert(!roles.Roles.Contains("Legacy") && roles.Roles.SetEquals(["Reader"]) &&
            roles.ScopedRoles.Count == 1 && roles.RolesIn("repo-a").Contains("Contributor") &&
            !roles.RolesIn("repo-b").Contains("OldScoped"), "hook replaces all old role types across identities and deterministically caps unique scoped roles");
        assert(logger.Count >= 3 && success.Properties!.ExpiresUtc is null, "successful hook logs dropped values and preserves full session");

        var malformed = new OpenIdConnectOptions();
        new RoleProviderSignInHook(new Fake((_, _) => Task.FromResult(
            new ResolvedRoles(new HashSet<string> { "Admin" }, null!))),
            options, Options.Create(Site()), logger).Configure(malformed);
        TicketReceivedContext partial = Context(malformed);
        partial.Principal!.AddIdentity(new ClaimsIdentity([new Claim("secondary-role", "Admin")],
            "secondary", "name", "secondary-role"));
        DateTimeOffset earlier = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30);
        partial.Properties!.ExpiresUtc = earlier;
        await malformed.Events.OnTicketReceived(partial);
        assert(RoleSet.FromPrincipal(partial.Principal).Roles.Count == 0 &&
            RoleSet.FromPrincipal(partial.Principal).ScopedRoles.Count == 0 &&
            !partial.Principal.HasClaim("secondary-role", "Admin"),
            "a malformed result after global validation grants nothing, across all identities");
        assert(partial.Properties.ExpiresUtc == earlier, "failure never extends an already shorter ticket");

        foreach (bool timeout in new[] { false, true })
        {
            var pending = new TaskCompletionSource<ResolvedRoles>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failed = new OpenIdConnectOptions();
            new RoleProviderSignInHook(new Fake((_, _) => timeout ? pending.Task :
                Task.FromException<ResolvedRoles>(new InvalidOperationException("synthetic"))), options, Options.Create(Site()), logger).Configure(failed);
            TicketReceivedContext context = Context(failed);
            var watch = Stopwatch.StartNew();
            await failed.Events.OnTicketReceived(context);
            pending.TrySetException(new InvalidOperationException("late synthetic failure"));
            assert(context.Principal!.Claims.All(static c => c.Type != RoleSet.RolesClaimType &&
                c.Type != ClaimTypes.Role && c.Type != RoleSet.ScopedRolesClaimType && c.Type != "custom-role"),
                $"provider {(timeout ? "timeout" : "failure")} strips every prior role claim");
            assert(context.Properties!.ExpiresUtc is { } expiry &&
                Math.Abs((expiry - DateTimeOffset.UtcNow - options.DegradedSessionLifetime).TotalSeconds) < 1 &&
                watch.Elapsed < TimeSpan.FromSeconds(3), "failure issues short session and enforces deadline even without cooperative cancellation");
        }

        var shortCircuit = new OpenIdConnectOptions();
        shortCircuit.Events.OnTicketReceived = context => { context.SkipHandler(); return Task.CompletedTask; };
        new RoleProviderSignInHook(new Fake((_, _) => throw new InvalidOperationException("must not run")),
            options, Options.Create(Site()), logger).Configure(shortCircuit);
        TicketReceivedContext skipped = Context(shortCircuit);
        await shortCircuit.Events.OnTicketReceived(skipped);
        assert(skipped.Result?.Skipped == true, "previous handler result short-circuits role resolution");
    }

    private static IConfiguration Config(string assembly = @"plugins\sample\SampleRoleProvider.dll",
        string type = "SampleRoleProvider.SampleModule", KeyValuePair<string, string>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            [$"{RoleProviderOptions.Section}:Assembly"] = assembly,
            [$"{RoleProviderOptions.Section}:Type"] = type,
        };
        if (extra is { } entry)
        {
            values[$"{RoleProviderOptions.Section}:{entry.Key}"] = entry.Value;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static SiteOptions Site() => new()
    {
        Spaces = [new() { Id = "a", RoleScope = "repo-a" }, new() { Id = "b", RoleScope = "repo-b" }],
    };

    private static ClaimsPrincipal Principal() => new(new ClaimsIdentity([
        new(RoleSet.RolesClaimType, "Legacy"), new(ClaimTypes.Role, "Legacy"),
        new("custom-role", "Legacy"), new(RoleSet.ScopedRolesClaimType, "OldScoped@repo-b"),
    ], "synthetic", "name", "custom-role"));

    private static TicketReceivedContext Context(OpenIdConnectOptions options)
    {
        var http = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("synthetic", null, typeof(OpenIdConnectHandler));
        return new TicketReceivedContext(http, scheme, options, new AuthenticationTicket(Principal(), new(), "synthetic"));
    }

    private sealed class Fake(Func<ClaimsPrincipal, CancellationToken, Task<ResolvedRoles>> resolve) : IRoleProvider
    {
        public Task<ResolvedRoles> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken) => resolve(user, cancellationToken);
    }

    private sealed class Warnings : ILogger<RoleProviderSignInHook>
    {
        public int Count { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) { Count++; }
        }
    }
}
