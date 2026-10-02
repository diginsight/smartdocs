using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Diginsight.SmartDocs.Web.Caching;

public sealed class BackgroundRevalidationCache(
    ILogger<BackgroundRevalidationCache> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<ContentPathCacheKey, Entry> _entries = new();
    private readonly ConcurrentDictionary<ContentPathCacheKey, byte> _pending = new();
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions { SingleReader = true });

    private long _generation;

    public async Task<T> GetAsync<T>(
        ContentPathCacheKey key,
        TimeSpan maxAge,
        Func<CancellationToken, Task<T>> valueFactory,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_entries.TryGetValue(key, out Entry? entry) && entry.Value is T cached)
        {
            if (now - entry.CreatedAt >= maxAge)
            {
                Enqueue(key, valueFactory);
            }

            return cached;
        }

        long generation = Interlocked.Read(ref _generation);
        T value = await valueFactory(cancellationToken);
        if (generation == Interlocked.Read(ref _generation))
        {
            _entries[key] = new Entry(value!, DateTimeOffset.UtcNow);
        }

        return value;
    }

    public void Invalidate(ContentPathInvalidationRule rule)
    {
        Interlocked.Increment(ref _generation);
        foreach (ContentPathCacheKey key in _entries.Keys)
        {
            if (key.IsInvalidatedBy(rule, out _))
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (WorkItem item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                object value = await item.Factory(stoppingToken);
                if (item.Generation == Interlocked.Read(ref _generation))
                {
                    _entries[item.Key] = new Entry(value, DateTimeOffset.UtcNow);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background cache revalidation failed for {Kind}:{Path}", item.Key.Kind, item.Key.Path);
            }
            finally
            {
                _pending.TryRemove(item.Key, out _);
            }
        }
    }

    private void Enqueue<T>(ContentPathCacheKey key, Func<CancellationToken, Task<T>> valueFactory)
    {
        if (!_pending.TryAdd(key, 0))
        {
            return;
        }

        long generation = Interlocked.Read(ref _generation);
        if (!_queue.Writer.TryWrite(new WorkItem(
                key,
                generation,
                async cancellationToken => (await valueFactory(cancellationToken))!)))
        {
            _pending.TryRemove(key, out _);
            throw new InvalidOperationException($"Unable to queue background revalidation for {key.Kind}:{key.Path}.");
        }
    }

    private sealed record Entry(object Value, DateTimeOffset CreatedAt);

    private sealed record WorkItem(
        ContentPathCacheKey Key,
        long Generation,
        Func<CancellationToken, Task<object>> Factory);
}
