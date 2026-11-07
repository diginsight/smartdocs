using Diginsight.SmartDocs.Web.Caching;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>Refreshes this host's projections and connected readers after a companion invalidation.</summary>
public sealed class RemoteNavigationInvalidationService(
    RemoteContentInvalidations remote,
    CachedDynamicNavBuilder nav,
    NavChangePublisher publisher,
    INavigationWarmupQueue warmup,
    ILogger<RemoteNavigationInvalidationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (ContentPathInvalidationRule rule in remote.ReadAllAsync(stoppingToken))
        {
            try
            {
                nav.ReceiveInvalidation(rule);
                foreach (string path in rule.ChangedPaths)
                {
                    await publisher.PublishChangeAsync(path);
                }
                warmup.EnqueueAll();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not refresh navigation after a remote invalidation");
            }
        }
    }
}
