using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Diginsight.Diagnostics;
using Diginsight.SmartCache;
using Diginsight.SmartCache.Externalization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diginsight.SmartDocs.Web.Caching;

/// <summary>Observes local and companion invalidations before they reach the underlying cache.</summary>
public sealed class CoordinatedSmartCache(
    ISmartCache inner,
    ICacheCompanion companion,
    MetadataInvalidationTracker metadata,
    RemoteContentInvalidations remote,
    ILogger<CoordinatedSmartCache> logger) : ISmartCache
{
    public Task<T> GetAsync<T>(
        object key,
        Func<CancellationToken, Task<T>> fetchAsync,
        SmartCacheOperationOptions? operationOptions = null,
        Type? callerType = null,
        CancellationToken cancellationToken = default) =>
        inner.GetAsync(key, fetchAsync, operationOptions, callerType, cancellationToken);

    public bool TryGetDirectFromMemory(object key, [NotNullWhen(true)] out Type? type, out object? value) =>
        inner.TryGetDirectFromMemory(key, out type, out value);

    public void Invalidate(IInvalidationRule rule)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(
            logger, () => new { ruleType = rule.GetType().Name });

        if (rule is ContentPathInvalidationRule paths)
        {
            metadata.Invalidate(paths);
        }
        inner.Invalidate(rule);
    }

    public void Invalidate(InvalidationDescriptor descriptor)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        bool foreign = !string.Equals(descriptor.Emitter, companion.SelfLocationId, StringComparison.Ordinal);
        if (foreign && descriptor.Rule is ContentPathInvalidationRule paths)
        {
            metadata.Invalidate(paths);
            inner.Invalidate(descriptor);
            if (paths.Kind is null)
            {
                remote.Enqueue(paths);
            }
            return;
        }
        inner.Invalidate(descriptor);
    }

    public void AddExternalMiss(CacheMissDescriptor descriptor) => inner.AddExternalMiss(descriptor);
}

public static class SmartCacheCoordination
{
    private static ILogger? cachedLogger;
    private static ILogger logger => (cachedLogger ??= Observability.LoggerFactory?.CreateLogger(typeof(SmartCacheCoordination))) ?? NullLogger.Instance;

    public static void Register(IServiceCollection services)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        ServiceDescriptor descriptor = services.Single(item => item.ServiceType == typeof(ISmartCache));
        Type implementation = descriptor.ImplementationType
            ?? throw new InvalidOperationException("The SmartCache service must have a concrete implementation type.");
        services.AddSingleton(implementation);
        services.AddSingleton<MetadataInvalidationTracker>();
        services.AddSingleton<RemoteContentInvalidations>();
        services.Replace(ServiceDescriptor.Singleton<ISmartCache>(sp => new CoordinatedSmartCache(
            (ISmartCache)sp.GetRequiredService(implementation),
            sp.GetRequiredService<ICacheCompanion>(),
            sp.GetRequiredService<MetadataInvalidationTracker>(),
            sp.GetRequiredService<RemoteContentInvalidations>(),
            sp.GetRequiredService<ILogger<CoordinatedSmartCache>>())));
    }
}

public sealed class RemoteContentInvalidations
{
    private readonly Channel<ContentPathInvalidationRule> queue = Channel.CreateUnbounded<ContentPathInvalidationRule>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(ContentPathInvalidationRule rule)
    {
        if (!queue.Writer.TryWrite(rule))
        {
            throw new InvalidOperationException("Unable to queue a remote content invalidation.");
        }
    }

    public IAsyncEnumerable<ContentPathInvalidationRule> ReadAllAsync(CancellationToken cancellationToken) =>
        queue.Reader.ReadAllAsync(cancellationToken);
}
