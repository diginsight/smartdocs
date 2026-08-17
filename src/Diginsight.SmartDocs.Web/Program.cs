using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.SmartCache;
using Diginsight.SmartCache.Externalization.Http;
using Diginsight.SmartCache.Externalization.Redis;
using Diginsight.SmartCache.Externalization.ServiceBus;
using Diginsight.SmartDocs.Web.Components;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Endpoints;
using Diginsight.SmartDocs.Web.Navigation;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web;

public class Program 
{
    private static readonly string SmartCacheServiceBusSubscriptionName = Guid.NewGuid().ToString("N");

    public static void Main(string[] args)
    {
        // Diginsight early logging (console + log4net to %USERPROFILE%\LogFiles\Diginsight\Diginsight.SmartDocs.Web.<date>.log).
        using var observabilityManager = new ObservabilityManager();
        LoggerFactoryStaticAccessor.LoggerFactory = observabilityManager.LoggerFactory;
        ILogger logger = observabilityManager.LoggerFactory.CreateLogger(typeof(Program));

        WebApplication app;
        using (var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { args }))
        {
            var builder = WebApplication.CreateBuilder(args);

            // Merge external/environment configuration (e.g. the Testmc overlay from the sibling
            // smartdocs.internal repo, selected via AppsettingsEnvironmentName + ExternalConfigurationFolder).
            builder.Host.ConfigureAppConfiguration2(observabilityManager.LoggerFactory);

            IServiceCollection services = builder.Services;
            IConfiguration configuration = builder.Configuration;
            IWebHostEnvironment environment = builder.Environment;

            // Diginsight telemetry integrated with OpenTelemetry (+ log4net file logging).
            services.AddAspNetCoreObservability(configuration, environment, out IOpenTelemetryOptions openTelemetryOptions);
            observabilityManager.AttachTo(services);
            services.AddHttpObservability(openTelemetryOptions);

            services.TryAddSingleton<EarlyLoggingManager>(observabilityManager);
            services.AddHttpContextAccessor();
            services.AddDynamicLogLevel<DefaultDynamicLogLevelInjector>();
            services.AddParallelService(configuration);

            // Razor Components host with interactive WebAssembly components (prerendered by default).
            services.AddRazorComponents()
                .AddInteractiveWebAssemblyComponents();

            // The site and the spaces it publishes. Bound eagerly rather than through IOptions because
            // the route table and the per-space content sources are built during startup, before any
            // request exists — and because a misconfigured space must stop the host here, loudly,
            // rather than surface later as an empty sidebar.
            services.Configure<SiteOptions>(configuration.GetSection("Site"));
            SiteOptions siteOptions = configuration.GetSection("Site").Get<SiteOptions>()
                ?? throw new InvalidOperationException("Missing 'Site' configuration section.");
            var spaceRegistry = new SpaceRegistry(siteOptions.Spaces);
            services.AddSingleton(spaceRegistry);
            logger.LogInformation(
                "Site '{Title}' publishes {Count} space(s): {Spaces}",
                siteOptions.Title,
                spaceRegistry.All.Count,
                string.Join(", ", spaceRegistry.All.Select(static s => $"{s.Id} @ {(s.IsRootMounted ? "/" : s.NormalizedRouteBase)}")));

            // Physical server-side content source for one space: FileSystem (repo clone) or Blob
            // (storage), selected per space. Returned as the concrete type so the caller keeps both
            // the reader and the lister without a downcast.
            static IContentSource CreatePhysicalContentSource(IServiceProvider sp, SpaceOptions space)
            {
                if (string.Equals(space.Source, "FileSystem", StringComparison.OrdinalIgnoreCase))
                {
                    IWebHostEnvironment env = sp.GetRequiredService<IWebHostEnvironment>();
                    string root = Path.GetFullPath(Path.Combine(env.ContentRootPath, space.FileSystem.RootPath));
                    return new FileSystemContentSource(root,
                        sp.GetRequiredService<ILogger<FileSystemContentSource>>());
                }

                return new BlobContentSource(space.Blob.AccountUri, space.Blob.ContainerName,
                    sp.GetRequiredService<ILogger<BlobContentSource>>());
            }

            // SmartCache over the content source (Diginsight convention). Core options bind from
            // Diginsight:SmartCache (MaxAge / AbsoluteExpiration / SlidingExpiration + class-aware
            // overrides like MaxAge@CachedContentSource). Always on; distributed sync is opt-in:
            //   • Diginsight:SmartCache:ServiceBus (ConnectionString + TopicName) → Service Bus companion
            //   • Diginsight:SmartCache:Redis:Configuration → Redis passive backing store
            services.ConfigureClassAware<SmartCacheCoreOptions>(configuration.GetSection("Diginsight:SmartCache"));

            SmartCacheBuilder smartCacheBuilder = services
                .AddSmartCache(configuration, environment, observabilityManager.LoggerFactory)
                .AddHttp();

            // Distributed cross-instance invalidation via Service Bus is opt-in: only wire the Service
            // Bus companion when it is actually configured. Otherwise AddSmartCache's default
            // (single-instance, in-process) companion is kept — required for the DI container to
            // resolve ICacheCompanion when running standalone (e.g. local dev, no Service Bus).
            IConfigurationSection serviceBusSection = configuration.GetSection("Diginsight:SmartCache:ServiceBus");
            bool serviceBusConfigured =
                !string.IsNullOrEmpty(serviceBusSection[nameof(SmartCacheServiceBusOptions.ConnectionString)])
                && !string.IsNullOrEmpty(serviceBusSection[nameof(SmartCacheServiceBusOptions.TopicName)]);
            if (serviceBusConfigured)
            {
                smartCacheBuilder.SetServiceBusCompanion(
                    static (_, _) => true,
                    sbo =>
                    {
                        serviceBusSection.Bind(sbo);
                        sbo.SubscriptionName = SmartCacheServiceBusSubscriptionName;
                    });
            }

            // Opt-in Redis passive backing store (distributed, multi-instance).
            string? smartCacheRedis = configuration["Diginsight:SmartCache:Redis:Configuration"];
            if (!string.IsNullOrWhiteSpace(smartCacheRedis))
            {
                smartCacheBuilder.AddRedis(o =>
                {
                    o.Configuration = smartCacheRedis;
                    o.KeyPrefix = configuration["Diginsight:SmartCache:Redis:KeyPrefix"] ?? "smartdocs-content:";
                });
            }

            // One cached reader + one lister per space, all singletons. Content sources stay singletons
            // because the navigation builders capture them; the space is therefore chosen by an explicit
            // argument, never by a scoped factory reading the current request — that would be a captive
            // dependency on the server and would have no counterpart at all in the browser.
            services.AddSingleton(sp => new SpaceContentRegistry(
                spaceRegistry.All.Select(space =>
                {
                    IContentSource physical = CreatePhysicalContentSource(sp, space);
                    var lister = (IContentLister)physical;
                    var cached = new CachedContentSource(
                        physical,
                        lister,
                        sp.GetRequiredService<ISmartCache>(),
                        sp.GetRequiredService<ILogger<CachedContentSource>>());
                    return new SpaceContentAccess(space, cached, cached);
                })));

            // Default space: the one mounted at the site root, else the first configured. Every
            // single-space consumer (page loader, nav builder, raw content endpoint) resolves through
            // these two registrations, so a one-space site behaves exactly as it did before spaces existed.
            SpaceOptions defaultSpace = spaceRegistry.All.FirstOrDefault(static s => s.IsRootMounted)
                ?? spaceRegistry.All[0];
            services.AddSingleton<IContentSource>(sp =>
                sp.GetRequiredService<SpaceContentRegistry>().Get(defaultSpace.Id).Source);
            services.AddSingleton<IContentLister>(sp =>
                sp.GetRequiredService<SpaceContentRegistry>().Get(defaultSpace.Id).Lister);

            services.AddScoped<IMarkdownRenderer, MarkdigMarkdownRenderer>();
            services.AddScoped<PageLoader>();
            services.AddScoped<TocState>();
            services.AddScoped<ThemeState>();
            services.AddScoped<SidebarState>();
            services.AddScoped<NavStats>();
            services.AddScoped<ArticleState>();
            // Dynamic, spec-compliant menu built on demand from the live content hierarchy.
            services.AddMemoryCache();
            services.AddSingleton<FolderMetricsIndex>();
            services.AddSingleton<DynamicNavBuilder>();
            services.AddSingleton<CachedDynamicNavBuilder>(sp => new CachedDynamicNavBuilder(
                sp.GetRequiredService<DynamicNavBuilder>(),
                sp.GetRequiredService<ISmartCache>(),
                sp.GetRequiredService<IParallelService>(),
                sp.GetRequiredService<ILogger<CachedDynamicNavBuilder>>()));
            services.AddSingleton<INavBuilder>(sp => sp.GetRequiredService<CachedDynamicNavBuilder>());
            services.AddScoped<INavProvider, ServerNavProvider>();

            // Live navigation metadata push: SignalR hub + the publisher that broadcasts folder
            // aggregates on content change and once the startup warm-up has computed the counts.
            services.AddSignalR();
            services.AddSingleton<NavChangePublisher>();

            builder.UseDiginsightServiceProvider(true);

            app = builder.Build();
            logger.LogDebug("Host built");

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error", createScopeForErrors: true);
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseAntiforgery();

            // Map fingerprinted static assets (app.css + the WASM _framework payload). Must run before
            // AddInteractiveWebAssemblyRenderMode so the client bootstrap (blazor.web.js) is served.
            app.MapStaticAssets();

            // Content passthrough + dynamic navigation APIs (see the *Endpoints classes).
            app.MapContentEndpoints();
            app.MapNavEndpoints();
            app.MapTestContentEndpoints(app.Configuration);
            app.MapHub<NavHub>(NavHubContract.Route);

            app.MapRazorComponents<App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Diginsight.SmartDocs.Web.Client.Marker).Assembly);
        }

        // Drain results must reach the hub before any content write can happen.
        app.Services.GetRequiredService<NavChangePublisher>().Wire();

        // Build the navigation metrics projection in the background: seed from the previous run so
        // the counter never starts from nothing, then discover + fold the tree one root branch at a
        // time so the footer total climbs as a labelled lower bound instead of jumping.
        _ = Task.Run(async () =>
        {
            try
            {
                var cachedNav = app.Services.GetRequiredService<CachedDynamicNavBuilder>();
                var metrics = app.Services.GetRequiredService<FolderMetricsIndex>();
                var publisher = app.Services.GetRequiredService<NavChangePublisher>();

                string snapshotPath = SnapshotPath(app.Configuration);
                if (await metrics.LoadSnapshotAsync(snapshotPath) > 0)
                {
                    cachedNav.InvalidateLevels();          // levels rebuild carrying the seeded counts
                    await publisher.PublishCountsReadyAsync();
                }

                // A restart is just a global invalidation over a warm seed — same drain, no special path.
                var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var root in await cachedNav.GetChildrenAsync(string.Empty))
                {
                    if (!root.IsSection || root.Prefix is null)
                    {
                        continue;
                    }

                    reachable.UnionWith(await metrics.DiscoverAsync(root.Prefix));
                    await metrics.DrainAsync();
                    cachedNav.InvalidateLevels();
                    await publisher.PublishCountsReadyAsync();
                }

                // Folders that disappeared while the app was down must not linger in the snapshot.
                metrics.PruneUnreachable(reachable);

                // Root cell (the whole-site total) plus anything still dirty.
                metrics.Invalidate(string.Empty);
                await metrics.DrainAsync();
                cachedNav.InvalidateLevels();

                // Flattened search index + every level warm, then the authoritative final push.
                await cachedNav.GetIndexAsync();
                await cachedNav.WarmAllLevelsAsync();
                await publisher.PublishCountsReadyAsync();

                await metrics.SaveSnapshotAsync(snapshotPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nav metrics warm-up failed");
            }
        });

        app.Run();
    }

    // Single derived artifact: one read at startup instead of one per folder, and no derived value
    // is ever written into an authored content file.
    private static string SnapshotPath(IConfiguration configuration) =>
        configuration["Site:MetricsSnapshotPath"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "nav-metrics-snapshot.json");
}
