using System.Collections.Concurrent;
using System.Threading.Channels;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Navigation;

public interface INavigationWarmupQueue
{
    void EnqueueLevels(string prefix, int depth);

    void EnqueueAll();
}

public sealed class NavigationWarmupService(
    CachedDynamicNavBuilder nav,
    FolderMetricsIndex metrics,
    NavChangePublisher publisher,
    ForegroundRequestGate foreground,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<NavigationWarmupService> logger)
    : BackgroundService, INavigationWarmupQueue
{
    private static readonly TimeSpan FirstResponseGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaximumForegroundPause = TimeSpan.FromSeconds(2);

    private readonly Channel<WarmupRequest> _requests = Channel.CreateUnbounded<WarmupRequest>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);

    public void EnqueueLevels(string prefix, int depth)
    {
        if (depth <= 0)
        {
            return;
        }

        string normalized = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        Enqueue(new WarmupRequest($"levels:{normalized}:{depth}", normalized, depth, false));
    }

    public void EnqueueAll() =>
        Enqueue(new WarmupRequest("all", string.Empty, int.MaxValue, true));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForApplicationStartedAsync(stoppingToken);
        await foreground.WaitForFirstResponseOrGraceAsync(FirstResponseGrace, stoppingToken);
        logger.LogInformation("Navigation startup warm-up started after the application began listening");

        try
        {
            await RunStartupWarmupAsync(stoppingToken);
            logger.LogInformation("Navigation startup warm-up completed");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Navigation startup warm-up failed");
        }

        await foreach (WarmupRequest request in _requests.Reader.ReadAllAsync(stoppingToken))
        {
            _pending.TryRemove(request.Key, out _);

            try
            {
                await foreground.WaitForBackgroundTurnAsync(MaximumForegroundPause, stoppingToken);
                if (request.All)
                {
                    await nav.GetIndexAsync(stoppingToken);
                    await nav.WarmAllLevelsAsync(stoppingToken, WaitForForegroundAsync);
                }
                else
                {
                    await nav.WarmLevelsAsync(
                        request.Prefix,
                        request.Depth,
                        stoppingToken,
                        WaitForForegroundAsync);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Queued navigation warm-up failed for {Prefix}", request.Prefix);
            }
        }
    }

    private async Task RunStartupWarmupAsync(CancellationToken cancellationToken)
    {
        string snapshotPath = SnapshotPath(configuration);
        if (await metrics.LoadSnapshotAsync(snapshotPath, cancellationToken) > 0)
        {
            await publisher.PublishCountsReadyAsync();
        }

        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NavChild root in await nav.GetChildrenAsync(string.Empty, cancellationToken))
        {
            if (!root.IsSection || root.Prefix is null)
            {
                continue;
            }

            await foreground.WaitForBackgroundTurnAsync(MaximumForegroundPause, cancellationToken);
            reachable.UnionWith(await metrics.DiscoverAsync(
                root.Prefix,
                cancellationToken,
                WaitForForegroundAsync));
            await metrics.DrainAsync(cancellationToken);
            await publisher.PublishCountsReadyAsync();
        }

        metrics.PruneUnreachable(reachable);
        metrics.Invalidate(string.Empty);
        await metrics.DrainAsync(cancellationToken);

        await foreground.WaitForBackgroundTurnAsync(MaximumForegroundPause, cancellationToken);
        await nav.WarmAllLevelsAsync(cancellationToken, WaitForForegroundAsync);
        await publisher.PublishCountsReadyAsync();
        await metrics.SaveSnapshotAsync(snapshotPath, cancellationToken);
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken cancellationToken)
    {
        if (lifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration =
            lifetime.ApplicationStarted.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), started);
        await started.Task.WaitAsync(cancellationToken);
    }

    private void Enqueue(WarmupRequest request)
    {
        if (_pending.TryAdd(request.Key, 0))
        {
            _requests.Writer.TryWrite(request);
        }
    }

    private Task WaitForForegroundAsync(CancellationToken cancellationToken) =>
        foreground.WaitForBackgroundTurnAsync(MaximumForegroundPause, cancellationToken);

    private static string SnapshotPath(IConfiguration configuration) =>
        configuration["Site:MetricsSnapshotPath"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "nav-metrics-snapshot.json");

    private sealed record WarmupRequest(string Key, string Prefix, int Depth, bool All);
}
