using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Diginsight.Components;
using Diginsight.SmartCache;
using Diginsight.SmartCache.Externalization;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Navigation;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Diginsight.SmartDocs.Web.Sites;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;

internal static class ProviderChecks
{
    private const string Policy = """
        schema: smartdocs-metadata/v1
        label: Protected
        access:
          requires: [Read]
          permissions:
            Read: Read documentation
          roles:
            Reader: [Read]
        """;

    private const string RevokedPolicy = """
        schema: smartdocs-metadata/v1
        label: Protected
        access:
          requires: [Read]
          permissions:
            Read: Read documentation
          roles:
            Reader: []
        """;

    private const string LegacyPolicy = """
        schema: 1
        requires: [Read]
        permissions:
          Read: Read documentation
        roles:
          Reader: [Read]
        """;

    public static async Task RunAsync(Action<bool, string> assert)
    {
        await SharedAndFreshAsync(assert);
        await ConcurrentAsync(assert);
        await InvalidationRaceAsync(assert);
        await RemoteInvalidationRaceAsync(assert);
        await MetadataTreeRefreshAsync(assert);
        await FingerprintWorkloadAsync(assert);
        await FingerprintRecoveryAsync(assert);
        await FingerprintInvalidationRaceAsync(assert);
        await HubRefreshAsync(assert);
        await ScopeIsolationAsync(assert);
        await ConsumersAsync(assert);
        await DeliveryBoundariesAsync(assert);
        await FileSystemAliasesAsync(assert);
        LiveRefreshFiltering(assert);
        foreach (bool blobShaped in new[] { false, true })
        {
            await CompatibilityAsync(blobShaped, assert);
        }
    }

    private static async Task FileSystemAliasesAsync(Action<bool, string> assert)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "smartdocs-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string[] names = ["metadata.yml", "access.metadata.yml", "repository.metadata.yml"];
        try
        {
            var source = new FileSystemContentSource(root, NullLogger<FileSystemContentSource>.Instance);
            assert(await source.GetAsync("absent/unknown.md") is null,
                "missing files in missing folders remain not found");
            foreach (string name in names)
            {
                string path = Path.Combine(root, name);
                await File.WriteAllTextAsync(path, "synthetic: true");
                var buffer = new StringBuilder(1024);
                uint length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
                if (length == 0 || length >= buffer.Capacity)
                {
                    throw new IOException("Unable to resolve the synthetic Windows short-name path.");
                }

                string alias = Path.GetFileName(buffer.ToString());
                assert(await source.GetAsync(name) is not null, $"physical source permits internal lookup of {name}");
                string symlink = Path.Combine(root, "linked-" + name + ".txt");
                try
                {
                    File.CreateSymbolicLink(symlink, path);
                    assert(await source.GetAsync(Path.GetFileName(symlink)) is null &&
                        await source.ReadHeadAsync(Path.GetFileName(symlink)) is null,
                        $"physical source rejects a symbolic-link alias of {name}");
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException)
                {
                    Console.WriteLine($"SKIP: symbolic-link creation is unavailable for {name}.");
                }
                finally
                {
                    if (File.Exists(symlink))
                    {
                        File.Delete(symlink);
                    }
                }
                if (alias.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "fsutil.exe"))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    start.ArgumentList.Add("file");
                    start.ArgumentList.Add("setshortname");
                    start.ArgumentList.Add(path);
                    start.ArgumentList.Add("METAD~1.YML");
                    using Process process = Process.Start(start) ??
                        throw new InvalidOperationException("Unable to start the Windows short-name utility.");
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"SKIP: unable to assign a distinct 8.3 alias for {name} " +
                            $"(exit {process.ExitCode}): {(await error).Trim()} {(await output).Trim()}");
                        continue;
                    }

                    alias = "METAD~1.YML";
                    if (!File.Exists(Path.Combine(root, alias)))
                    {
                        throw new IOException("The assigned Windows short name did not resolve to a file.");
                    }
                }

                assert(await source.GetAsync(alias) is null && await source.ReadHeadAsync(alias) is null,
                    $"physical source rejects the 8.3 alias of {name}");

                var space = NewSpace("alias", "/alias");
                var registry = new SpaceRegistry([space]);
                var content = new SpaceContentRegistry([new(space, source, source)]);
                var mounted = new SpaceMountedContentSource(registry, content,
                    NullLogger<SpaceMountedContentSource>.Instance);
                assert(await mounted.GetAsync("alias/" + alias) is null &&
                    await mounted.ReadHeadAsync("alias/" + alias) is null,
                    $"mounted source rejects the 8.3 alias of {name}");

                var assets = new BrandingAssets();
                var site = new SiteOptions { Branding = new BrandingOptions { LogoPath = alias } };
                using ServiceProvider services = new ServiceCollection()
                    .AddSingleton(assets)
                    .AddSingleton<IOptions<SiteOptions>>(Options.Create(site))
                    .AddSingleton(registry)
                    .AddSingleton(content)
                    .BuildServiceProvider();
                BrandingAssetResolver.Resolve(services, NullLogger.Instance);
                assert(assets.LogoUrl.Length == 0, $"branding rejects the 8.3 alias of {name}");
            }
        }
        finally
        {
            foreach (string name in names)
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            Directory.Delete(root);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathName(string path, StringBuilder output, uint length);

    private static void LiveRefreshFiltering(Action<bool, string> assert)
    {
        var active = new ActiveRoleSets();
        RoleSet reader = RoleSet.SignedIn(["Reader"]);
        RoleSet anonymous = RoleSet.Anonymous;
        active.Add("reader", reader);
        active.Add("anonymous", anonymous);
        var visible = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["alpha"] = "policy-1" };
        var hidden = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        active.RememberVisibility(reader, visible);
        active.RememberVisibility(anonymous, hidden);

        assert(!active.NeedsRefresh(anonymous, hidden, "alpha"),
            "metadata changes do not notify a group that never saw the space");
        assert(active.NeedsRefresh(reader, hidden, "alpha"),
            "revoked readers receive a refresh");
        active.Refreshed(reader, hidden, "alpha");
        assert(!active.NeedsRefresh(reader, hidden, "alpha"),
            "later hidden changes do not notify revoked readers");
        assert(active.NeedsRefresh(reader, visible, "alpha"),
            "newly authorized readers receive a refresh");
        active.Refreshed(reader, visible, "alpha");
        assert(!active.NeedsRefresh(reader, visible, null),
            "whole-site invalidation without a visible policy change is quiet");
        var changed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["alpha"] = "policy-2" };
        assert(active.NeedsRefresh(reader, changed, null) && !active.NeedsRefresh(anonymous, hidden, null),
            "whole-site invalidation refreshes only groups affected by a visible metadata change");
        active.Remove("reader");
        assert(!active.NeedsRefresh(reader, changed, "alpha"), "disconnected role sets are not notified");
    }

    private static async Task ConsumersAsync(Action<bool, string> assert)
    {
        await ConsumersAsync(StringComparer.OrdinalIgnoreCase, assert);
        await ConsumersAsync(StringComparer.Ordinal, assert);
    }

    private static async Task ConsumersAsync(StringComparer comparer, Action<bool, string> assert)
    {
        using (var fixture = new ConsumerFixture(required: false, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", "---\nlabel: Legacy # literal\nrequires: ignored\n---");
            FolderMeta navigation = await fixture.Cached.ReadFolderMetaAsync("alpha/metadata.yml");
            bool visible = (await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous)).Spaces["alpha"].IsVisible;
            assert(visible && navigation.Label == "Legacy # literal" &&
                navigation.Values?.ContainsKey("requires") == false,
                "legacy navigation parity keeps reserved metadata out of the projection");
            assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1,
                "legacy navigation and authorization share one cold read and YAML parse");
            await fixture.Cached.ReadFolderMetaAsync("alpha/metadata.yml");
            await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous);
            assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1,
                "legacy warm consumers add no origin reads or YAML parses");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", Policy);
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous)).Spaces["alpha"].IsVisible,
                "anonymous reader cannot enter a role-protected space");
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn([]))).Spaces["alpha"].IsVisible,
                "signed-in reader without roles cannot enter a role-protected space");
            assert((await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["reader"]))).Spaces["alpha"].IsVisible,
                "mapped role grants access");
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Unknown"]))).Spaces["alpha"].IsVisible,
                "unmapped role grants nothing");
            FolderMeta navigation = await fixture.Cached.ReadFolderMetaAsync("alpha/metadata.yml");
            assert(navigation.Label == "Protected" &&
                navigation.Values?.Keys.All(key => !MetadataParser.IsReservedNavigationKey(key)) == true,
                "navigation consumer receives only the safe projection");
            assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1,
                "actual authorization and navigation consumers share one cold read and parse");
            await fixture.Cached.ReadFolderMetaAsync("alpha/metadata.yml");
            await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]));
            assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1,
                "actual warm consumers add no origin reads or parses");

            fixture.Source.Set("metadata.yml", RevokedPolicy);
            string scope = MetadataFiles.InvalidationScope("alpha/metadata.yml");
            fixture.Provider.Invalidate([scope]);
            fixture.Cache.Invalidate(new ContentPathInvalidationRule(scope));
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible,
                "metadata-file invalidation revokes the role on its containing scope");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", Policy);
            assert((await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible,
                "reader is visible before a policy change without invalidation");
            fixture.Source.Set("metadata.yml", RevokedPolicy);
            fixture.Clock.Advance(TimeSpan.FromSeconds(31));
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible &&
                fixture.Source.Reads == 2,
                "the first authorization after root freshness expires reloads and denies a revoked reader");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", "schema: smartdocs-metadata/v1\nlabel: Navigation only");
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous)).Spaces["alpha"].IsVisible,
                "navigation-only metadata does not satisfy a required policy");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", "schema: smartdocs-metadata/v1\naccess: {}");
            assert((await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous)).Spaces["alpha"].IsVisible,
                "explicit empty access is a valid required policy");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", "schema: smartdocs-metadata/v1\naccess: null");
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible,
                "invalid required policy fails closed");
        }

        using (var fixture = new ConsumerFixture(required: false, siteFloor: "authenticated", comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", "label: Public content");
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous)).Spaces["alpha"].IsVisible &&
                (await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn([]))).Spaces["alpha"].IsVisible,
                "configured site floor remains additive without a policy");
        }

        using (var fixture = new ConsumerFixture(required: false, spaceFloor: "authenticated", comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", "schema: smartdocs-metadata/v1\naccess: {}");
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.Anonymous)).Spaces["alpha"].IsVisible &&
                (await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn([]))).Spaces["alpha"].IsVisible,
                "configured space floor remains additive with an empty policy");
        }

        using (var fixture = new ConsumerFixture(required: true, compatibility: true, comparer: comparer))
        {
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            assert((await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible,
                "legacy-only policy satisfies the required policy in compatibility mode");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", """
                schema: smartdocs-metadata/v1
                access:
                  requires: [Read, Write]
                  permissions: {Read: Read, Write: Write}
                  roles: {Reader: [Read], Contributor: [Read, Write]}
                """);
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible &&
                (await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Contributor"]))).Spaces["alpha"].IsVisible &&
                (await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader", "Contributor"]))).Spaces["alpha"].IsVisible,
                "a contributor with both permissions covers all required permissions");
        }

        using (var fixture = new ConsumerFixture(required: true, comparer: comparer))
        {
            fixture.Source.Set("metadata.yml", """
                schema: smartdocs-metadata/v1
                access:
                  requires: [Read, Write]
                  permissions: {Read: Read, Write: Write}
                  roles: {Reader: [Read], Contributor: [Write]}
                """);
            assert(!(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader"]))).Spaces["alpha"].IsVisible &&
                !(await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Contributor"]))).Spaces["alpha"].IsVisible &&
                (await fixture.Evaluator.EvaluateAsync(RoleSet.SignedIn(["Reader", "Contributor", "Unknown"]))).Spaces["alpha"].IsVisible,
                "all-of requirements and union of mapped roles are preserved");
        }

        assert(MetadataFiles.InvalidationScope("alpha/section/metadata.yml") == "alpha/section" &&
            MetadataFiles.InvalidationScope("metadata.yml") == string.Empty &&
            MetadataFiles.InvalidationScope("alpha/access.metadata.yml") == "alpha" &&
            MetadataFiles.InvalidationScope("alpha/section/article.md") == "alpha/section/article.md",
            "carrier invalidation targets its directory while article invalidation keeps its path");
    }

    private static async Task DeliveryBoundariesAsync(Action<bool, string> assert)
    {
        using var fixture = new ConsumerFixture(required: false);
        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.Set("access.metadata.yml", LegacyPolicy);
        fixture.Source.Set("repository.metadata.yml", "visibility: private");
        fixture.Source.Set("article.md", "# Public article");
        foreach (string key in new[]
        {
            "alpha/metadata.yml", "alpha/METADATA.YML", "alpha/metadata.yml.",
            "alpha/metadata.yml::$DATA", "alpha/access.metadata.yml",
            "alpha/repository.metadata.yml", "alpha/child/../metadata.yml",
        })
        {
            assert(await fixture.Mounted.GetAsync(key) is null && await fixture.Mounted.ReadHeadAsync(key) is null,
                $"mounted source refuses raw and head delivery for {key}");
        }

        IReadOnlyList<ChildEntry> children = await fixture.Mounted.ListChildrenAsync("alpha");
        assert(children.Any(static child => child.Name == "metadata.yml") &&
            children.All(static child => child.Name != "access.metadata.yml" && child.Name != "repository.metadata.yml"),
            "listing retains canonical discovery but hides legacy private carriers");
        assert(await fixture.Mounted.GetAsync("alpha/article.md") is not null,
            "carrier guard does not hide ordinary content");

        bool defaultThrows = false;
        try
        {
            await ((IContentLister)fixture.Mounted).ReadFolderMetaAsync("alpha/metadata.yml");
        }
        catch (NotSupportedException)
        {
            defaultThrows = true;
        }
        assert(defaultThrows, "a non-provider lister cannot fall back to head-based metadata parsing");

        foreach (string key in new[] { "metadata.yml", "access.metadata.yml", "repository.metadata.yml" })
        {
            var assets = new BrandingAssets();
            var site = new SiteOptions { Branding = new BrandingOptions { LogoPath = key } };
            using ServiceProvider services = new ServiceCollection()
                .AddSingleton(assets)
                .AddSingleton<IOptions<SiteOptions>>(Options.Create(site))
                .AddSingleton(fixture.Spaces)
                .AddSingleton(fixture.Content)
                .BuildServiceProvider();
            BrandingAssetResolver.Resolve(services, NullLogger.Instance);
            assert(assets.LogoUrl.Length == 0, $"branding refuses metadata carrier {key}");
        }
    }

    private static async Task SharedAndFreshAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        fixture.Source.Set("metadata.yml", Policy);
        MetadataOutcome authorization = await fixture.Provider.GetForSpaceAsync("alpha");
        MetadataOutcome navigation = await fixture.Provider.GetForMetadataKeyAsync("alpha/metadata.yml");
        assert(authorization.Document?.Access?.Grants("reader").Contains("READ") == true, "cold authorization");
        assert(navigation.Document?.Navigation.Label == "Protected", "cold navigation");
        assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1, "one canonical cold read and parse");
        assert(fixture.Cache.TryGetDirectFromMemory(
            new ContentPathCacheKey(ContentMetadataProvider.CanonicalCacheKind, "alpha/metadata.yml"), out _, out _),
            "one shared snapshot is cached");
        await fixture.Provider.GetForSpaceAsync("alpha");
        await fixture.Provider.GetForMetadataKeyAsync("alpha/metadata.yml");
        assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1, "warm consumers add no reads or parses");

        MetadataSnapshotEnvelope envelope = MetadataSnapshotEnvelope.From(authorization, "other-host", 0);
        byte[] serialized = SmartCacheSerialization.SerializeToBytes(envelope);
        MetadataSnapshotEnvelope roundtrip = SmartCacheSerialization.Deserialize<MetadataSnapshotEnvelope>(serialized);
        MetadataOutcome restored = roundtrip.ToOutcome();
        assert(restored.Document?.Access?.Grants("READER").Contains("read") == true, "distributed snapshot restores comparers");
        assert(restored.Document?.Navigation.Values?.ContainsKey("LABEL") == true, "distributed navigation comparer");

        var foreignSource = new CountingSource(StringComparer.Ordinal);
        var foreignParser = new CountingParser();
        var space = NewSpace("foreign", "/foreign");
        var foreignProvider = NewProvider(
            [new SpaceContentAccess(space, foreignSource, foreignSource)], new PreloadedCache(roundtrip),
            foreignParser, new ContentFreshnessOptions(), new SiteOptions { LegacyMetadataCompatibility = false });
        MetadataOutcome foreign = await foreignProvider.GetForSpaceAsync("foreign");
        assert(foreign.Document?.Access?.Grants("reader").Contains("read") == true &&
            foreignSource.Reads == 0 && foreignParser.Count == 0, "second-host snapshot hit performs no origin read or parse");

        fixture.Source.Set("metadata.yml", RevokedPolicy);
        fixture.Clock.Advance(TimeSpan.FromSeconds(31));
        MetadataOutcome refreshed = await fixture.Provider.GetForSpaceAsync("alpha");
        assert(refreshed.Document?.Access?.Grants("Reader").Contains("Read") == false &&
            fixture.Source.Reads == 2 && fixture.Parser.Count == 2, "root refresh revokes grant after 30 seconds");
    }

    private static async Task ConcurrentAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.Delay = TimeSpan.FromMilliseconds(100);
        MetadataOutcome[] outcomes = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => fixture.Provider.GetForSpaceAsync("alpha")));
        assert(outcomes.All(static o => o.Status == MetadataStatus.Valid), "100 concurrent callers succeed");
        assert(fixture.Source.Reads == 1 && fixture.Parser.Count == 1, "100 concurrent callers coalesce one read and parse");
    }

    private static async Task InvalidationRaceAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.BlockNextRead();
        Task<MetadataOutcome> oldFill = fixture.Provider.GetForSpaceAsync("alpha");
        await fixture.Source.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Source.Set("metadata.yml", RevokedPolicy);
        fixture.Provider.Invalidate(["alpha"]);
        fixture.Cache.Invalidate(new ContentPathInvalidationRule("alpha"));
        fixture.Source.ReleaseRead();
        MetadataOutcome outcome = await oldFill.WaitAsync(TimeSpan.FromSeconds(10));
        assert(outcome.Document?.Access?.Grants("Reader").Contains("Read") == false, "in-flight old policy is rejected");
        assert(fixture.Source.Reads == 2 && fixture.Parser.Count == 2, "invalidation race reloads once");
        MetadataOutcome warm = await fixture.Provider.GetForSpaceAsync("alpha");
        assert(warm.Document?.Access?.Grants("Reader").Contains("Read") == false, "stale fill does not repopulate cache");
    }

    private static async Task RemoteInvalidationRaceAsync(Action<bool, string> assert)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddSmartCache(builder.Configuration, builder.Environment);
        SmartCacheCoordination.Register(builder.Services);
        using (IHost coordinatedHost = builder.Build())
        {
            assert(coordinatedHost.Services.GetRequiredService<ISmartCache>() is CoordinatedSmartCache,
                "application cache coordination resolves through dependency injection");
        }

        using var fixture = new Fixture(compatibility: false);
        var tracker = new MetadataInvalidationTracker();
        var remote = new RemoteContentInvalidations();
        var cache = new CoordinatedSmartCache(fixture.Cache, fixture.Companion, tracker, remote);
        SpaceOptions space = NewSpace("alpha", "/alpha");
        var provider = NewProvider([new(space, fixture.Source, fixture.Source)], cache, fixture.Parser,
            new ContentFreshnessOptions(), new SiteOptions { LegacyMetadataCompatibility = false }, tracker);

        var rule = new ContentPathInvalidationRule(["alpha"], changedPaths: ["alpha/metadata.yml"]);
        ContentPathInvalidationRule transmitted =
            SmartCacheSerialization.Deserialize<ContentPathInvalidationRule>(SmartCacheSerialization.SerializeToBytes(rule));
        assert(transmitted.Paths.SetEquals(["alpha"]) && transmitted.ChangedPaths.SetEquals(["alpha/metadata.yml"]),
            "distributed invalidation retains the original metadata path and normalized scope");
        var oldHostRule = new ContentPathInvalidationRule("alpha/access.metadata.yml");
        assert(oldHostRule.Paths.SetEquals(["alpha"]) &&
            oldHostRule.ChangedPaths.SetEquals(["alpha/access.metadata.yml"]),
            "legacy host carrier invalidations broaden to the shared metadata scope");

        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.BlockNextRead();
        Task<MetadataOutcome> oldFill = provider.GetForSpaceAsync("alpha");
        await fixture.Source.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Source.Set("metadata.yml", RevokedPolicy);
        cache.Invalidate(new InvalidationDescriptor("other-host", transmitted));
        fixture.Source.ReleaseRead();
        MetadataOutcome result = await oldFill.WaitAsync(TimeSpan.FromSeconds(10));
        assert(result.Document?.Access?.Grants("Reader").Contains("Read") == false && fixture.Source.Reads == 2,
            "remote invalidation rejects a policy fill that started on this host");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using IAsyncEnumerator<ContentPathInvalidationRule> notifications =
            remote.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        assert(await notifications.MoveNextAsync() &&
            notifications.Current.ChangedPaths.Contains("alpha/metadata.yml"),
            "remote invalidation queues the original path for local hub refresh");

        MetadataOutcome oldPolicy = new MetadataParser().Parse(Encoding.UTF8.GetBytes(Policy), true);
        MetadataSnapshotEnvelope stale = MetadataSnapshotEnvelope.From(oldPolicy, "other-host", 0);
        var key = new ContentPathCacheKey(ContentMetadataProvider.CanonicalCacheKind, "alpha/metadata.yml");
        cache.AddExternalMiss(new CacheMissDescriptor("other-host", key, fixture.Clock.GetUtcNow(), "other-host",
            (typeof(MetadataSnapshotEnvelope), stale)));
        int reads = fixture.Source.Reads;
        result = await provider.GetForSpaceAsync("alpha");
        assert(result.Document?.Access?.Grants("Reader").Contains("Read") == false &&
            fixture.Source.Reads == reads + 1,
            "a stale foreign snapshot arriving after invalidation is revalidated at the origin");

        long generation = tracker.Generation("alpha/metadata.yml");
        cache.Invalidate(new InvalidationDescriptor(fixture.Companion.SelfLocationId, transmitted));
        assert(tracker.Generation("alpha/metadata.yml") == generation,
            "the companion's own broadcast is not treated as a second invalidation");

        using var lateFixture = new Fixture(compatibility: false);
        var lateTracker = new MetadataInvalidationTracker();
        var lateCache = new CoordinatedSmartCache(lateFixture.Cache, lateFixture.Companion, lateTracker,
            new RemoteContentInvalidations());
        lateCache.Invalidate(new InvalidationDescriptor("other-host", transmitted));
        lateFixture.Source.Set("metadata.yml", RevokedPolicy);
        lateCache.AddExternalMiss(new CacheMissDescriptor("other-host", key, lateFixture.Clock.GetUtcNow(), "other-host",
            (typeof(MetadataSnapshotEnvelope), stale)));
        var lateProvider = NewProvider([new(space, lateFixture.Source, lateFixture.Source)], lateCache,
            lateFixture.Parser, new ContentFreshnessOptions(),
            new SiteOptions { LegacyMetadataCompatibility = false }, lateTracker);
        result = await lateProvider.GetForSpaceAsync("alpha");
        assert(result.Document?.Access?.Grants("Reader").Contains("Read") == false && lateFixture.Source.Reads == 1,
            "an invalidation received before the first scope read rejects an older foreign snapshot");
    }

    private static async Task MetadataTreeRefreshAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        var second = new CountingSource(StringComparer.Ordinal);
        SpaceOptions alpha = NewSpace("alpha", "/alpha");
        SpaceOptions beta = NewSpace("beta", "/beta");
        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.Set("guide/index.md", "# Guide");
        second.Set("metadata.yml", RevokedPolicy);
        second.Set("guide/metadata.yml", "label: Other");
        var entries = new SpaceContentAccess[]
        {
            new(alpha, fixture.Source, fixture.Source),
            new(beta, second, second),
        };
        var registry = new SpaceRegistry([alpha, beta]);
        var mounted = new SpaceMountedContentSource(registry, new SpaceContentRegistry(entries),
            NullLogger<SpaceMountedContentSource>.Instance);
        var tracker = new MetadataInvalidationTracker();
        var freshness = new ContentFreshnessOptions();
        ContentMetadataProvider provider = NewProvider(entries, fixture.Cache, fixture.Parser,
            freshness, new SiteOptions { LegacyMetadataCompatibility = false }, tracker);
        var fingerprints = new VisibleMetadataFingerprint(
            mounted, provider, registry, new SiteOptions(), tracker, freshness, fixture.Clock);

        fixture.Source.Delay = TimeSpan.FromMilliseconds(50);
        string[] initial = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => fingerprints.ForSpaceAsync(alpha)));
        string alphaBefore = initial[0];
        assert(initial.All(value => value == alphaBefore) && fixture.Source.Listings == 2,
            "100 concurrent visible-tree fingerprints coalesce one two-folder traversal");
        int warmListings = fixture.Source.Listings;
        assert(await fingerprints.ForSpaceAsync(alpha) == alphaBefore && fixture.Source.Listings == warmListings,
            "a warm visible-tree fingerprint avoids another traversal");
        fixture.Source.Delay = TimeSpan.Zero;
        fixture.Clock.Advance(TimeSpan.FromSeconds(31));
        assert(await fingerprints.ForSpaceAsync(alpha) == alphaBefore && fixture.Source.Listings == warmListings + 2,
            "the derived fingerprint expires no later than root access freshness");

        string betaBefore = await fingerprints.ForSpaceAsync(beta);
        fixture.Source.Set("guide/metadata.yml", "label: Changed");
        provider.Invalidate([string.Empty]);
        fixture.Cache.Invalidate(new ContentPathInvalidationRule(string.Empty));
        string alphaAfter = await fingerprints.ForSpaceAsync(alpha);
        string betaAfter = await fingerprints.ForSpaceAsync(beta);
        assert(alphaBefore != alphaAfter && betaBefore == betaAfter,
            "whole-site invalidation detects newly added nested metadata only in its owning space");

        var active = new ActiveRoleSets();
        RoleSet reader = RoleSet.SignedIn(["Reader"]);
        RoleSet other = RoleSet.Anonymous;
        active.Add("reader", reader);
        active.Add("other", other);
        active.RememberVisibility(reader, new Dictionary<string, string> { ["alpha"] = alphaBefore });
        active.RememberVisibility(other, new Dictionary<string, string> { ["beta"] = betaBefore });
        assert(active.NeedsRefresh(reader, new Dictionary<string, string> { ["alpha"] = alphaAfter }, null) &&
            !active.NeedsRefresh(other, new Dictionary<string, string> { ["beta"] = betaAfter }, null),
            "a nested metadata change refreshes only groups that see the affected space");

        fixture.Source.Remove("guide/metadata.yml");
        provider.Invalidate([string.Empty]);
        fixture.Cache.Invalidate(new ContentPathInvalidationRule(string.Empty));
        assert(await fingerprints.ForSpaceAsync(alpha) == alphaBefore,
            "whole-site invalidation detects nested metadata removal");
    }

    private static async Task FingerprintWorkloadAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        SpaceOptions alpha = NewSpace("alpha", "/alpha");
        fixture.Source.Set("metadata.yml", Policy);
        for (int i = 0; i < 67; i++)
        {
            string folder = $"folder-{i:D2}";
            fixture.Source.Set($"{folder}/index.md", "# Synthetic");
            if (i < 11)
            {
                fixture.Source.Set($"{folder}/metadata.yml", $"label: Synthetic {i}");
            }
        }

        var entries = new[] { new SpaceContentAccess(alpha, fixture.Source, fixture.Source) };
        var registry = new SpaceRegistry([alpha]);
        var mounted = new SpaceMountedContentSource(registry, new SpaceContentRegistry(entries),
            NullLogger<SpaceMountedContentSource>.Instance);
        var tracker = new MetadataInvalidationTracker();
        var freshness = new ContentFreshnessOptions();
        var site = new SiteOptions { LegacyMetadataCompatibility = false };
        ContentMetadataProvider provider = NewProvider(entries, fixture.Cache, fixture.Parser, freshness, site, tracker);
        var fingerprints = new VisibleMetadataFingerprint(mounted, provider, registry, site, tracker, freshness, fixture.Clock);

        var timer = Stopwatch.StartNew();
        string cold = await fingerprints.ForSpaceAsync(alpha);
        long coldMs = timer.ElapsedMilliseconds;
        timer.Restart();
        string[] warm = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => fingerprints.ForSpaceAsync(alpha)));
        long warmMs = timer.ElapsedMilliseconds;
        assert(warm.All(value => value == cold) && fixture.Source.Listings == 68 && fixture.Source.Reads == 12 &&
            fixture.Parser.Count == 12, "100 warm fingerprints reuse one 67-folder synthetic traversal");
        Console.WriteLine($"INFO: synthetic fingerprint: 68 cold listings, 12 reads/parses, {coldMs} ms cold; " +
            $"100 warm calls added zero listings/reads/parses, {warmMs} ms total.");
    }

    private static async Task FingerprintRecoveryAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        SpaceOptions alpha = NewSpace("alpha", "/alpha");
        fixture.Source.Set("metadata.yml", Policy);
        var entries = new[] { new SpaceContentAccess(alpha, fixture.Source, fixture.Source) };
        var registry = new SpaceRegistry([alpha]);
        var mounted = new SpaceMountedContentSource(registry, new SpaceContentRegistry(entries),
            NullLogger<SpaceMountedContentSource>.Instance);
        var tracker = new MetadataInvalidationTracker();
        var freshness = new ContentFreshnessOptions();
        var site = new SiteOptions { LegacyMetadataCompatibility = false };
        ContentMetadataProvider provider = NewProvider(entries, fixture.Cache, fixture.Parser, freshness, site, tracker);
        var fingerprints = new VisibleMetadataFingerprint(mounted, provider, registry, site, tracker, freshness, fixture.Clock);

        fixture.Source.FailKey = "metadata.yml";
        string unreadable = await fingerprints.ForSpaceAsync(alpha);
        fixture.Source.FailKey = null;
        string recovered = await fingerprints.ForSpaceAsync(alpha);
        assert(unreadable != recovered && fixture.Source.Reads == 2,
            "an unreadable metadata result does not persist in the derived fingerprint cache");

        tracker.Invalidate(new ContentPathInvalidationRule("alpha"));
        fixture.Cache.Invalidate(new ContentPathInvalidationRule("alpha"));
        fixture.Source.BlockNextRead();
        using var cancellation = new CancellationTokenSource();
        Task<string> canceled = fingerprints.ForSpaceAsync(alpha, cancellation.Token);
        await fixture.Source.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        bool observedCancellation = false;
        try
        {
            await canceled;
        }
        catch (OperationCanceledException)
        {
            observedCancellation = true;
        }
        Task<string> survivor = fingerprints.ForSpaceAsync(alpha);
        fixture.Source.ReleaseRead();
        assert(observedCancellation && await survivor == recovered && fixture.Source.Reads == 3,
            "canceling one fingerprint caller does not cancel the shared traversal");
    }

    private static async Task FingerprintInvalidationRaceAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        SpaceOptions alpha = NewSpace("alpha", "/alpha");
        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.Set("guide/index.md", "# Guide");
        var entries = new[] { new SpaceContentAccess(alpha, fixture.Source, fixture.Source) };
        var registry = new SpaceRegistry([alpha]);
        var mounted = new SpaceMountedContentSource(registry, new SpaceContentRegistry(entries),
            NullLogger<SpaceMountedContentSource>.Instance);
        var tracker = new MetadataInvalidationTracker();
        var freshness = new ContentFreshnessOptions();
        var site = new SiteOptions { LegacyMetadataCompatibility = false };
        ContentMetadataProvider provider = NewProvider(entries, fixture.Cache, fixture.Parser, freshness, site, tracker);
        var fingerprints = new VisibleMetadataFingerprint(mounted, provider, registry, site, tracker, freshness, fixture.Clock);

        fixture.Source.BlockListing("guide");
        Task<string> inFlight = fingerprints.ForSpaceAsync(alpha);
        await fixture.Source.ListingStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Source.Set("guide/metadata.yml", "label: New");
        provider.Invalidate(["alpha/guide/metadata.yml"]);
        fixture.Cache.Invalidate(new ContentPathInvalidationRule("alpha/guide/metadata.yml"));
        fixture.Source.ReleaseListing();
        string afterRace = await inFlight;
        provider.Invalidate(["alpha/guide/metadata.yml"]);
        fixture.Cache.Invalidate(new ContentPathInvalidationRule("alpha/guide/metadata.yml"));
        assert(afterRace == await fingerprints.ForSpaceAsync(alpha) && fixture.Source.Listings >= 6,
            "invalidation during a traversal cannot publish an older derived fingerprint");
    }

    private static async Task HubRefreshAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        fixture.Source.Set("metadata.yml", Policy);
        fixture.Source.Set("guide/index.md", "# Guide");
        SpaceOptions alpha = NewSpace("alpha", "/alpha");
        alpha.AccessFileRequired = true;
        var registry = new SpaceRegistry([alpha]);
        var content = new SpaceContentRegistry([new(alpha, fixture.Source, fixture.Source)]);
        var tracker = new MetadataInvalidationTracker();
        var cache = new CoordinatedSmartCache(fixture.Cache, fixture.Companion, tracker, new RemoteContentInvalidations());
        var site = new SiteOptions { LegacyMetadataCompatibility = false };
        var freshness = new ContentFreshnessOptions();
        var metadata = new ContentMetadataProvider(content, registry, cache, tracker, fixture.Parser, freshness,
            site, NullLogger<ContentMetadataProvider>.Instance);
        var physical = new SpaceMountedContentSource(registry, content, NullLogger<SpaceMountedContentSource>.Instance);
        using var revalidation = new BackgroundRevalidationCache(NullLogger<BackgroundRevalidationCache>.Instance);
        var cachedContent = new CachedContentSource(physical, physical, cache, revalidation, freshness, metadata,
            NullLogger<CachedContentSource>.Instance);
        var inner = new EmptyNavBuilder();
        using ServiceProvider navServices = new ServiceCollection().AddSingleton<INavBuilder>(inner).BuildServiceProvider();
        using var metrics = new FolderMetricsIndex(navServices, registry, NullLogger<FolderMetricsIndex>.Instance);
        var nav = new CachedDynamicNavBuilder(inner, cache, revalidation, fixture.Parallel, freshness,
            NullLogger<CachedDynamicNavBuilder>.Instance);
        var folders = new FolderRecordProvider(nav, cachedContent, metrics, cache, revalidation, freshness, site,
            NullLogger<FolderRecordProvider>.Instance);
        var resolver = new AccessRequirementResolver(registry,
            new AccessFileProvider(content, metadata, NullLogger<AccessFileProvider>.Instance), site.Authorization,
            NullLogger<AccessRequirementResolver>.Instance);
        var evaluator = new SpaceVisibilityEvaluator(registry, resolver, NullLogger<SpaceVisibilityEvaluator>.Instance);
        var active = new ActiveRoleSets();
        var hub = new CapturingHub();
        var publisher = new NavChangePublisher(metrics, nav, folders, hub, active, evaluator, resolver,
            new ReaderCountComposer(metrics, registry), registry,
            new VisibleMetadataFingerprint(cachedContent, metadata, registry, site, tracker, freshness, fixture.Clock),
            NullLogger<NavChangePublisher>.Instance);
        RoleSet reader = RoleSet.SignedIn(["Reader"]);
        RoleSet anonymous = RoleSet.Anonymous;
        active.Add("reader", reader);
        active.Add("anonymous", anonymous);
        await publisher.SendCurrentCountsAsync(hub.Client("reader"), reader);
        await publisher.SendCurrentCountsAsync(hub.Client("anonymous"), anonymous);
        hub.Messages.Clear();

        fixture.Source.Set("guide/metadata.yml", "label: Updated");
        nav.Invalidate();
        await publisher.PublishChangeAsync(string.Empty);
        assert(hub.Sent(ActiveRoleSets.GroupOf(reader), NavHubContract.RefreshRequired) &&
            !hub.Sent(ActiveRoleSets.GroupOf(anonymous), NavHubContract.RefreshRequired),
            "whole-site nested metadata invalidation refreshes the authorized hub group only");
        assert(hub.Messages.Where(message => message.Method == NavHubContract.RefreshRequired)
            .All(message => message.Arguments.Length == 0),
            "metadata refresh hub messages do not carry policy or repository content");

        hub.Messages.Clear();
        fixture.Source.Set("metadata.yml", RevokedPolicy);
        nav.Invalidate("alpha/metadata.yml");
        await publisher.PublishChangeAsync("alpha/metadata.yml");
        assert(hub.Sent(ActiveRoleSets.GroupOf(reader), NavHubContract.RefreshRequired) &&
            !hub.Sent(ActiveRoleSets.GroupOf(anonymous), NavHubContract.RefreshRequired),
            "a revoked reader receives the policy refresh without notifying a never-visible group");

        hub.Messages.Clear();
        fixture.Source.Set("metadata.yml", RevokedPolicy + "\n# revised\n");
        nav.Invalidate("alpha/metadata.yml");
        await publisher.PublishChangeAsync("alpha/metadata.yml");
        assert(!hub.Sent(ActiveRoleSets.GroupOf(reader), NavHubContract.RefreshRequired) &&
            !hub.Sent(ActiveRoleSets.GroupOf(anonymous), NavHubContract.RefreshRequired),
            "subsequent hidden-space metadata changes do not notify either group");

        hub.Messages.Clear();
        fixture.Source.Set("metadata.yml", Policy);
        nav.Invalidate("alpha/metadata.yml");
        await publisher.PublishChangeAsync("alpha/metadata.yml");
        assert(hub.Sent(ActiveRoleSets.GroupOf(reader), NavHubContract.RefreshRequired) &&
            !hub.Sent(ActiveRoleSets.GroupOf(anonymous), NavHubContract.RefreshRequired),
            "a newly restored reader receives a refresh");
    }

    private static async Task ScopeIsolationAsync(Action<bool, string> assert)
    {
        using var fixture = new Fixture(compatibility: false);
        var rootSource = new CountingSource(StringComparer.Ordinal);
        var secondSource = new CountingSource(StringComparer.Ordinal);
        fixture.Source.Set("metadata.yml", Policy);
        rootSource.Set("metadata.yml", RevokedPolicy);
        secondSource.Set("metadata.yml", RevokedPolicy);
        fixture.Source.Set("article.md", "mounted article");
        rootSource.Set("alpha/article.md", "shadowed root article");
        rootSource.Set("article.md", "root article");
        SpaceOptions alpha = NewSpace("alpha", "/alpha");
        SpaceOptions beta = NewSpace("beta", "/beta");
        SpaceOptions root = NewSpace("root", "/");
        var provider = NewProvider(
            [new(alpha, fixture.Source, fixture.Source), new(beta, secondSource, secondSource), new(root, rootSource, rootSource)],
            fixture.Cache, fixture.Parser, new ContentFreshnessOptions(), new SiteOptions { LegacyMetadataCompatibility = false });
        MetadataOutcome a = await provider.GetForSpaceAsync("alpha");
        MetadataOutcome b = await provider.GetForSpaceAsync("beta");
        MetadataOutcome r = await provider.GetForMetadataKeyAsync("metadata.yml");
        assert(a.Document?.Access?.Grants("Reader").Contains("Read") == true &&
            b.Document?.Access?.Grants("Reader").Contains("Read") == false &&
            r.Document?.Access?.Grants("Reader").Contains("Read") == false, "mount and root policy isolation");
        assert(fixture.Source.Reads == 1 && secondSource.Reads == 1 && rootSource.Reads == 1, "distinct physical scopes");
        var registry = new SpaceRegistry([alpha, beta, root]);
        var content = new SpaceContentRegistry(
            [new(alpha, fixture.Source, fixture.Source), new(beta, secondSource, secondSource), new(root, rootSource, rootSource)]);
        var mounted = new SpaceMountedContentSource(registry, content, NullLogger<SpaceMountedContentSource>.Instance);
        ContentResult? mountedArticle = await mounted.GetAsync("alpha/article.md");
        ContentResult? rootArticle = await mounted.GetAsync("article.md");
        assert(mountedArticle is not null && Encoding.UTF8.GetString(mountedArticle.Bytes) == "mounted article" &&
            rootArticle is not null && Encoding.UTF8.GetString(rootArticle.Bytes) == "root article",
            "prefixed mounts retain precedence over root-mounted content");
        assert(await mounted.GetAsync("metadata.yml") is null &&
            await mounted.ReadHeadAsync("metadata.yml") is null,
            "root-mounted metadata remains private");
    }

    private static async Task CompatibilityAsync(bool blobShaped, Action<bool, string> assert)
    {
        StringComparer comparer = blobShaped ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("metadata.yml", Policy);
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            MetadataOutcome equal = await fixture.Provider.GetForSpaceAsync("alpha");
            assert(equal.Status == MetadataStatus.Valid && equal.Document?.Warnings.Length == 1, "equal dual policies accepted with warning");
            assert(fixture.Source.Reads == 2 && fixture.Parser.Count == 1 && fixture.Parser.LegacyCount == 1,
                "compatibility overhead includes both parses");
        }

        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("metadata.yml", RevokedPolicy);
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            assert((await fixture.Provider.GetForSpaceAsync("alpha")).Status == MetadataStatus.Invalid, "conflicting dual policies denied");
        }

        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("metadata.yml", Policy);
            fixture.Source.Set("access.metadata.yml", "schema: 1\nrequires: [Unknown]");
            assert((await fixture.Provider.GetForSpaceAsync("alpha")).Status == MetadataStatus.Invalid, "invalid legacy policy denied");
        }

        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("metadata.yml", "schema: smartdocs-metadata/v1\naccess: null");
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            assert((await fixture.Provider.GetForSpaceAsync("alpha")).Status == MetadataStatus.Invalid, "invalid canonical policy denied");
        }

        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            MetadataOutcome legacy = await fixture.Provider.GetForSpaceAsync("alpha");
            assert(legacy.Document?.Access?.Grants("reader").Contains("Read") == true, "legacy-only policy preserved");
            assert(fixture.Source.Listings == 1, "missing canonical file costs one listing");
        }

        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("metadata.yml", Policy);
            fixture.Source.FailKey = "access.metadata.yml";
            assert((await fixture.Provider.GetForSpaceAsync("alpha")).Status == MetadataStatus.Unreadable, "unreadable legacy denied");
            fixture.Source.FailKey = null;
            MetadataOutcome recovered = await fixture.Provider.GetForSpaceAsync("alpha");
            assert(recovered.Status == MetadataStatus.Valid && fixture.Source.Reads == 4, "unreadable outcome is not cached");
        }

        using (var fixture = new Fixture(compatibility: true, comparer))
        {
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            fixture.Source.FailKey = "metadata.yml";
            assert((await fixture.Provider.GetForSpaceAsync("alpha")).Status == MetadataStatus.Unreadable, "unreadable canonical denied");
        }

        using (var fixture = new Fixture(compatibility: false, comparer))
        {
            fixture.Source.Set("access.metadata.yml", LegacyPolicy);
            MetadataOutcome missing = await fixture.Provider.GetForSpaceAsync("alpha");
            assert(missing.Status == MetadataStatus.Missing && fixture.Source.Reads == 1 &&
                fixture.Source.Listings == 1, "canonical-only mode does not read legacy policy");
        }

        if (blobShaped)
        {
            using var fixture = new Fixture(compatibility: false, comparer);
            fixture.Source.Set("METADATA.YML", Policy);
            assert((await fixture.Provider.GetForSpaceAsync("alpha")).Status == MetadataStatus.Invalid &&
                fixture.Source.Reads == 1 && fixture.Source.Listings == 1, "misnamed canonical file is rejected");
        }
    }

    private static SpaceOptions NewSpace(string id, string route) =>
        new() { Id = id, RouteBase = route, Source = "FileSystem" };

    private static ContentMetadataProvider NewProvider(
        SpaceContentAccess[] entries, ISmartCache cache, CountingParser parser,
        ContentFreshnessOptions freshness, SiteOptions site, MetadataInvalidationTracker? invalidations = null) =>
        new(new SpaceContentRegistry(entries), new SpaceRegistry(entries.Select(static e => e.Space)),
            cache, invalidations ?? new MetadataInvalidationTracker(), parser, freshness, site,
            NullLogger<ContentMetadataProvider>.Instance);

    private sealed class Fixture : IDisposable
    {
        private readonly IHost host;

        public Fixture(bool compatibility, StringComparer? comparer = null)
        {
            Clock = new ManualClock();
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<TimeProvider>(Clock);
            builder.Services.AddSmartCache(builder.Configuration, builder.Environment);
            builder.Services.AddParallelService(builder.Configuration);
            host = builder.Build();
            Cache = host.Services.GetRequiredService<ISmartCache>();
            Companion = host.Services.GetRequiredService<ICacheCompanion>();
            Parallel = host.Services.GetRequiredService<IParallelService>();
            Source = new CountingSource(comparer ?? StringComparer.Ordinal);
            Parser = new CountingParser();
            SpaceOptions space = NewSpace("alpha", "/alpha");
            Provider = NewProvider([new(space, Source, Source)], Cache, Parser,
                new ContentFreshnessOptions(), new SiteOptions { LegacyMetadataCompatibility = compatibility });
        }

        public ManualClock Clock { get; }
        public ISmartCache Cache { get; }
        public ICacheCompanion Companion { get; }
        public IParallelService Parallel { get; }
        public CountingSource Source { get; }
        public CountingParser Parser { get; }
        public ContentMetadataProvider Provider { get; }

        public void Dispose() => host.Dispose();
    }

    private sealed class ConsumerFixture : IDisposable
    {
            private readonly Fixture storage;
            private readonly BackgroundRevalidationCache revalidation;

            public ConsumerFixture(bool required, bool compatibility = false, string siteFloor = "", string spaceFloor = "",
                StringComparer? comparer = null)
            {
                storage = new Fixture(compatibility, comparer);
                var space = NewSpace("alpha", "/alpha");
                space.AccessFileRequired = required;
                space.Access = spaceFloor;
                Spaces = new SpaceRegistry([space]);
                Content = new SpaceContentRegistry([new(space, storage.Source, storage.Source)]);
                var site = new SiteOptions
                {
                    LegacyMetadataCompatibility = compatibility,
                    Authorization = new SiteAuthorizationOptions { DefaultRequirement = siteFloor },
                };
                var freshness = new ContentFreshnessOptions();
                Provider = new ContentMetadataProvider(Content, Spaces, storage.Cache, new MetadataInvalidationTracker(),
                    storage.Parser, freshness,
                    site, NullLogger<ContentMetadataProvider>.Instance);
                var policies = new AccessFileProvider(Content, Provider, NullLogger<AccessFileProvider>.Instance);
                var resolver = new AccessRequirementResolver(Spaces, policies, site.Authorization,
                    NullLogger<AccessRequirementResolver>.Instance);
                Evaluator = new SpaceVisibilityEvaluator(Spaces, resolver, NullLogger<SpaceVisibilityEvaluator>.Instance);
                Mounted = new SpaceMountedContentSource(Spaces, Content, NullLogger<SpaceMountedContentSource>.Instance);
                revalidation = new BackgroundRevalidationCache(NullLogger<BackgroundRevalidationCache>.Instance);
                Cached = new CachedContentSource(Mounted, Mounted, storage.Cache, revalidation, freshness, Provider,
                    NullLogger<CachedContentSource>.Instance);
            }

            public CountingSource Source => storage.Source;
            public CountingParser Parser => storage.Parser;
            public ManualClock Clock => storage.Clock;
            public ISmartCache Cache => storage.Cache;
            public SpaceRegistry Spaces { get; }
            public SpaceContentRegistry Content { get; }
            public ContentMetadataProvider Provider { get; }
            public SpaceVisibilityEvaluator Evaluator { get; }
            public SpaceMountedContentSource Mounted { get; }
            public CachedContentSource Cached { get; }

            public void Dispose()
            {
                revalidation.Dispose();
                storage.Dispose();
            }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan by) => Interlocked.Add(ref ticks, by.Ticks);
    }

    private sealed class CountingParser : IMetadataDocumentParser
    {
        private readonly MetadataParser inner = new();
        private int count;
        private int legacyCount;
        public int Count => Volatile.Read(ref count);
        public int LegacyCount => Volatile.Read(ref legacyCount);
        public MetadataOutcome Parse(byte[] bytes, bool isContentRoot)
        {
            Interlocked.Increment(ref count);
            return inner.Parse(bytes, isContentRoot);
        }

        public AccessFileOutcome ParseLegacyAccess(byte[] bytes)
        {
            Interlocked.Increment(ref legacyCount);
            return inner.ParseLegacyAccess(bytes);
        }
    }

    private sealed class CountingSource(StringComparer comparer) : IContentSource, IContentLister
    {
        private readonly ConcurrentDictionary<string, byte[]> files = new(comparer);
        private int reads;
        private int listings;
        private int blockNext;
        private int blockListing;
        private TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseListing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ListingStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? BlockedPrefix { get; private set; }
        public int Reads => Volatile.Read(ref reads);
        public int Listings => Volatile.Read(ref listings);
        public string? FailKey { get; set; }
        public TimeSpan Delay { get; set; }
        public void Set(string key, string value) => files[key] = Encoding.UTF8.GetBytes(value);
        public void Remove(string key) => files.TryRemove(key, out _);
        public void BlockNextRead() => Interlocked.Exchange(ref blockNext, 1);
        public void ReleaseRead() => release.TrySetResult();
        public void BlockListing(string prefix)
        {
            BlockedPrefix = prefix;
            Interlocked.Exchange(ref blockListing, 1);
        }
        public void ReleaseListing() => releaseListing.TrySetResult();

        public async Task<ContentResult?> GetAsync(string key, CancellationToken ct = default)
        {
            Interlocked.Increment(ref reads);
            if (string.Equals(key, FailKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Synthetic storage failure.");
            }

            files.TryGetValue(key, out byte[]? bytes);
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
            {
                ReadStarted.TrySetResult();
                await release.Task.WaitAsync(ct);
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            return bytes is null ? null : new ContentResult(bytes, "text/yaml", "fixture");
        }

        public async Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
        {
            Interlocked.Increment(ref listings);
            string start = prefix.Length == 0 ? string.Empty : prefix.TrimEnd('/') + "/";
            var children = new Dictionary<string, ChildEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in files.Keys.Where(key => key.StartsWith(start, StringComparison.OrdinalIgnoreCase)))
            {
                string remainder = key[start.Length..];
                int slash = remainder.IndexOf('/');
                string name = slash < 0 ? remainder : remainder[..slash];
                children[name] = new ChildEntry(name, slash >= 0, start + name);
            }
            ChildEntry[] result = children.Values.ToArray();
            if (string.Equals(prefix, BlockedPrefix, StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Exchange(ref blockListing, 0) == 1)
            {
                ListingStarted.TrySetResult();
                await releaseListing.Task.WaitAsync(ct);
            }
            return result;
        }

        public Task<string?> ReadHeadAsync(string key, CancellationToken ct = default) =>
            throw new InvalidOperationException("The metadata provider must not use a head read.");
    }

    private sealed class PreloadedCache(MetadataSnapshotEnvelope envelope) : ISmartCache
    {
        public Task<T> GetAsync<T>(object key, Func<CancellationToken, Task<T>> fetchAsync,
            SmartCacheOperationOptions? operationOptions = null, Type? callerType = null,
            CancellationToken cancellationToken = default) => Task.FromResult((T)(object)envelope);

        public bool TryGetDirectFromMemory(object key, [NotNullWhen(true)] out Type? type, out object? value)
        {
            type = typeof(MetadataSnapshotEnvelope);
            value = envelope;
            return true;
        }

        public void Invalidate(IInvalidationRule invalidationRule) { }
        public void Invalidate(InvalidationDescriptor descriptor) { }
        public void AddExternalMiss(CacheMissDescriptor descriptor) { }
    }

    private sealed class EmptyNavBuilder : INavBuilder
    {
        public Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NavChild>>([]);

        public Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NavLeaf>>([]);
    }

    private sealed class CapturingHub : IHubContext<NavHub>, IHubClients, IGroupManager
    {
        public ConcurrentBag<(string Target, string Method, object?[] Arguments)> Messages { get; } = [];
        IHubClients IHubContext<NavHub>.Clients => this;
        IGroupManager IHubContext<NavHub>.Groups => this;
        public bool Sent(string target, string method) =>
            Messages.Any(message => message.Target == target && message.Method == method);
        public IClientProxy All => Proxy("*");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("*");
        public IClientProxy Client(string connectionId) => Proxy(connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy(string.Join(",", connectionIds));
        public IClientProxy Group(string groupName) => Proxy(groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy(string.Join(",", groupNames));
        public IClientProxy User(string userId) => Proxy(userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy(string.Join(",", userIds));
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private IClientProxy Proxy(string target) => new CapturingClient(Messages, target);
    }

    private sealed class CapturingClient(
        ConcurrentBag<(string Target, string Method, object?[] Arguments)> messages, string target) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            messages.Add((target, method, args));
            return Task.CompletedTask;
        }
    }
}
