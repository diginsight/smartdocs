using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.SmartCache;
using Diginsight.SmartCache.Externalization.Http;
using Diginsight.SmartCache.Externalization.Redis;
using Diginsight.SmartCache.Externalization.ServiceBus;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Components;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Endpoints;
using Diginsight.SmartDocs.Web.Navigation;
using Diginsight.SmartDocs.Web.Rendering;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Diginsight.SmartDocs.Web.Sites;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components.Web;

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
            services.AddScoped(sp =>
            {
                HttpRequest? request = sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request;
                Uri baseAddress = request is null
                    ? new Uri("http://localhost/")
                    : new Uri($"{request.Scheme}://{request.Host}{request.PathBase}/");
                return new HttpClient { BaseAddress = baseAddress };
            });
            services.AddDynamicLogLevel<DefaultDynamicLogLevelInjector>();
            services.AddParallelService(configuration);

            // Razor Components host with interactive WebAssembly components (prerendered by default).
            services.AddScoped<NavigationBootstrapState>();
            services.AddRazorComponents()
                .AddInteractiveWebAssemblyComponents()
                .RegisterPersistentService<NavigationBootstrapState>(RenderMode.InteractiveWebAssembly);

            // Dynamic responses worth compressing: navigation JSON and Markdown source. HTML is left
            // out on purpose — a prerendered page carries an antiforgery token next to reflected input,
            // the combination compression side channels exploit. Static web assets are served
            // precompressed by MapStaticAssets and pass through untouched.
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = ["application/json", "text/markdown", "text/plain", "image/svg+xml"];
            });

            // The site and the spaces it publishes. Bound eagerly rather than through IOptions because
            // the route table and the per-space content sources are built during startup, before any
            // request exists — and because a misconfigured space must stop the host here, loudly,
            // rather than surface later as an empty sidebar.
            services.Configure<SiteOptions>(configuration.GetSection("Site"));
            SiteOptions siteOptions = configuration.GetSection("Site").Get<SiteOptions>()
                ?? throw new InvalidOperationException("Missing 'Site' configuration section.");

            // Populated after the container is built (see BrandingAssetResolver), so the scoped
            // factory below — which only ever runs once a request is being served — reads the
            // resolved value and the prerendered header already carries the mark.
            var brandingAssets = new BrandingAssets();
            services.AddSingleton(brandingAssets);
            services.AddScoped(sp =>
            {
                SiteShellOptions shell = SiteShellOptions.From(siteOptions, brandingAssets.LogoUrl);
                sp.GetRequiredService<NavigationBootstrapState>().Site = shell;
                var state = new SiteShellState();
                state.Apply(shell);
                return state;
            });
            services.AddScoped(_ => new ThemeState(ThemeCatalog.Resolve(siteOptions.Themes)));
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

            // The library caps its memory cache at 10,000,000 units of estimated size, which a startup
            // warm-up of raw article headers alone nearly filled. The cap is a deployment decision, so
            // it comes from configuration; without a value the library default stays in force.
            if (configuration.GetValue<long?>("Diginsight:SmartCache:SizeLimit") is > 0 and long sizeLimit)
            {
                smartCacheBuilder.SetSizeLimit(sizeLimit);
            }

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

            // One physical source per space, joined into a single path namespace by the mounted
            // source (each prefixed space under its route-base segment), with one SmartCache
            // decorator in front of the whole namespace. Every cache key, nav level, metrics cell and
            // invalidation rule is a content path, and a mounted path is unique across spaces — so
            // caching the joined namespace, rather than each space separately, is what keeps two
            // spaces' "index.md" from sharing an entry.
            // How stale an answer may be when no invalidation call arrived. Bound eagerly, like the
            // site options above, because the content sources are built here rather than resolved.
            var freshness = new ContentFreshnessOptions();
            configuration.GetSection("Diginsight:SmartCache:SmartDocs").Bind(freshness);
            services.AddSingleton(freshness);

            services.AddSingleton(sp => new SpaceContentRegistry(
                spaceRegistry.All.Select(space =>
                {
                    IContentSource physical = CreatePhysicalContentSource(sp, space);
                    return new SpaceContentAccess(space, physical, (IContentLister)physical);
                })));
            services.AddSingleton(sp => new SpaceMountedContentSource(
                spaceRegistry,
                sp.GetRequiredService<SpaceContentRegistry>(),
                sp.GetRequiredService<ILogger<SpaceMountedContentSource>>()));
            services.AddSingleton(sp =>
            {
                SpaceMountedContentSource mounted = sp.GetRequiredService<SpaceMountedContentSource>();
                return new CachedContentSource(
                    mounted,
                    mounted,
                    sp.GetRequiredService<ISmartCache>(),
                    sp.GetRequiredService<BackgroundRevalidationCache>(),
                    freshness,
                    sp.GetRequiredService<ILogger<CachedContentSource>>());
            });
            services.AddSingleton<IContentSource>(sp => sp.GetRequiredService<CachedContentSource>());
            services.AddSingleton<IContentLister>(sp => sp.GetRequiredService<CachedContentSource>());

            services.AddSingleton<IMarkdownRenderer, MarkdigMarkdownRenderer>();
            services.AddSingleton<RenderedPageProvider>();
            services.AddSingleton<IRenderedPageResolver>(sp => sp.GetRequiredService<RenderedPageProvider>());
            services.AddScoped<PageLoader>();
            services.AddScoped<TocState>();
            services.AddScoped<SidebarState>();
            services.AddScoped<NavStats>();
            services.AddScoped<ArticleState>();
            // Dynamic, spec-compliant menu built on demand from the live content hierarchy. No
            // IMemoryCache is registered here: SmartCache's builder registers its own, and SmartCache
            // itself keeps a private memory cache, so nothing in this application resolves one.
            services.AddSingleton<FolderMetricsIndex>();
            services.AddSingleton<BackgroundRevalidationCache>();
            services.AddHostedService(
                sp => sp.GetRequiredService<BackgroundRevalidationCache>());
            // The inner builder gets a lazy handle on the decorator that wraps it, so the whole-tree
            // walk behind GetIndexAsync reads each level through the cache instead of re-listing the
            // tree. Lazy breaks the cycle: it is never forced in the constructor, only on first walk.
            services.AddSingleton<DynamicNavBuilder>(sp => new DynamicNavBuilder(
                sp.GetRequiredService<IContentLister>(),
                sp.GetRequiredService<FolderMetricsIndex>(),
                sp.GetRequiredService<IParallelService>(),
                new Lazy<INavBuilder>(() => sp.GetRequiredService<CachedDynamicNavBuilder>()),
                sp.GetRequiredService<ILogger<DynamicNavBuilder>>(),
                spaceRegistry,
                siteOptions.AssetFolders));
            services.AddSingleton<CachedDynamicNavBuilder>(sp => new CachedDynamicNavBuilder(
                sp.GetRequiredService<DynamicNavBuilder>(),
                sp.GetRequiredService<ISmartCache>(),
                sp.GetRequiredService<BackgroundRevalidationCache>(),
                sp.GetRequiredService<IParallelService>(),
                freshness,
                sp.GetRequiredService<ILogger<CachedDynamicNavBuilder>>()));
            services.AddSingleton<INavBuilder>(sp => sp.GetRequiredService<CachedDynamicNavBuilder>());
            services.AddSingleton(sp => new FolderRecordProvider(
                sp.GetRequiredService<INavBuilder>(),
                sp.GetRequiredService<IContentLister>(),
                sp.GetRequiredService<FolderMetricsIndex>(),
                sp.GetRequiredService<ISmartCache>(),
                sp.GetRequiredService<BackgroundRevalidationCache>(),
                freshness,
                siteOptions));
            services.AddScoped<INavProvider, ServerNavProvider>();

            // Live navigation metadata push: SignalR hub + the publisher that broadcasts folder
            // aggregates on content change and once the startup warm-up has computed the counts.
            services.AddSignalR();
            services.AddSingleton<NavChangePublisher>();
            services.AddSingleton<ForegroundRequestGate>();
            services.AddSingleton<NavigationWarmupService>();
            services.AddSingleton<INavigationWarmupQueue>(
                sp => sp.GetRequiredService<NavigationWarmupService>());
            services.AddHostedService(
                sp => sp.GetRequiredService<NavigationWarmupService>());

            builder.UseDiginsightServiceProvider(true);

            app = builder.Build();
            logger.LogDebug("Host built");

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error", createScopeForErrors: true);
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseResponseCompression();

            app.UseMiddleware<NavigationBootstrapMiddleware>();

            app.Use(async (context, next) =>
            {
                using IDisposable request = app.Services.GetRequiredService<ForegroundRequestGate>().Enter();
                await next(context);
            });

            // A route that names nothing in the content — the browser's /favicon.ico, a mistyped link,
            // or a crawler's misresolved relative link — is answered with a 404 here, before the page
            // router would prerender a "Not found" page for it with status 200.
            app.UsePageRouteGuard();

            app.UseAntiforgery();

            // Map fingerprinted static assets (app.css + the WASM _framework payload). Must run before
            // AddInteractiveWebAssemblyRenderMode so the client bootstrap (blazor.web.js) is served.
            app.MapStaticAssets();

            // Content passthrough + dynamic navigation APIs (see the *Endpoints classes).
            app.MapContentEndpoints();
            app.MapNavEndpoints();
            app.MapSiteEndpoints();
            app.MapTestContentEndpoints(app.Configuration);
            app.MapHub<NavHub>(NavHubContract.Route);

            app.MapRazorComponents<App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Diginsight.SmartDocs.Web.Client.Marker).Assembly);
        }

        // Drain results must reach the hub before any content write can happen.
        app.Services.GetRequiredService<NavChangePublisher>().Wire();

        // Locate the publisher's mark before the first request, so the prerendered header already
        // carries it. One read of one asset; a space that cannot answer is skipped with a warning.
        BrandingAssetResolver.Resolve(app.Services, logger);

        app.Run();
    }
}
