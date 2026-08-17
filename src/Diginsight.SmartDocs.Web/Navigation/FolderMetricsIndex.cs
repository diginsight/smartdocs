using System.Collections.Concurrent;
using System.Text.Json;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.Extensions.Logging;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Server-authoritative projection of per-folder derived metrics (today: recursive article count and
/// newest-article date), maintained by invalidation + refold rather than by delta accumulation.
/// <para>
/// <b>Invalidate</b> is synchronous, lock-free and O(depth) — it only stamps the changed folder and
/// its ancestors. <b>Drain</b> is debounced, non-reentrant, and processes the dirty set
/// <em>deepest-first</em> so every fold sees settled children. A fold reads its children's cells
/// (no I/O) and re-lists only the folder that actually changed, so the drain's cost is proportional
/// to the number of directly changed folders — not to the size of the subtree.
/// </para>
/// <para>
/// Every value carries a <see cref="Coverage"/>. A fold that cannot observe every descendant yields
/// a <see cref="Coverage.Partial"/> lower bound, and a lower bound may never replace a
/// <see cref="Coverage.Complete"/> value — which is what stops a partially loaded or partially
/// scanned tree from ever displaying a wrong total.
/// </para>
/// </summary>
public sealed class FolderMetricsIndex(
    IServiceProvider services,
    ILogger<FolderMetricsIndex> logger) : IDisposable
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(400);

    private readonly ConcurrentDictionary<string, Cell> _cells = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _drainGate = new(1, 1);
    private readonly object _timerGate = new();

    private long _stamp;
    private Timer? _timer;
    private INavBuilder? _nav;

    private INavBuilder Nav => _nav ??= services.GetRequiredService<INavBuilder>();

    /// <summary>Raised after a drain with the prefixes whose value actually changed (never the whole dirty set).</summary>
    public event Func<IReadOnlyList<string>, Task>? Changed;

    /// <summary>Monotonic index version; bumps on every drain that changed something.</summary>
    public long Version { get; private set; }

    /// <summary>
    /// Issues the next stamp. Monotonic by construction, but reads back as epoch milliseconds so it
    /// stays diagnosable in logs; it degrades to <c>prev + 1</c> only on ties and backward clock steps.
    /// </summary>
    public long NextStamp()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (true)
        {
            long prev = Interlocked.Read(ref _stamp);
            long next = now > prev ? now : prev + 1;
            if (Interlocked.CompareExchange(ref _stamp, next, prev) == prev)
            {
                return next;
            }
        }
    }

    /// <summary>Current value for a folder, or null when the folder is not tracked yet.</summary>
    public FolderMetrics? TryGet(string prefix)
    {
        prefix = Normalize(prefix);
        return _cells.TryGetValue(prefix, out Cell? c) && c.Coverage != Coverage.None
            ? new FolderMetrics(c.Count, c.Latest, c.Coverage)
            : null;
    }

    /// <summary>Snapshot of every tracked folder — used by the diagnostics endpoint.</summary>
    public IReadOnlyDictionary<string, FolderCellView> Dump() =>
        _cells.ToDictionary(
            kv => kv.Key,
            kv => new FolderCellView(kv.Value.Count, kv.Value.Latest, kv.Value.Coverage,
                kv.Value.Invalidated, kv.Value.Settled, kv.Value.SettledAtUtc),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Marks <paramref name="prefix"/> and every ancestor up to the root as needing a refold.
    /// Synchronous, lock-free, no I/O — safe to call from a request path or a file-watcher callback.
    /// </summary>
    public void Invalidate(string prefix)
    {
        long stamp = NextStamp();
        string p = Normalize(prefix);

        while (true)
        {
            Cell cell = _cells.GetOrAdd(p, static _ => new Cell());
            InterlockedMax(ref cell.Invalidated, stamp);

            if (p.Length == 0)
            {
                break;
            }

            p = ParentOf(p);
        }

        ScheduleDrain();
    }

    /// <summary>Marks every tracked folder dirty (startup after loading a seed, or a manual rebuild).</summary>
    public void InvalidateAll()
    {
        long stamp = NextStamp();
        foreach (Cell cell in _cells.Values)
        {
            InterlockedMax(ref cell.Invalidated, stamp);
        }

        ScheduleDrain();
    }

    // Stamps the strict ancestors of a prefix (not the prefix itself).
    private void StampAncestors(string prefix, long stamp)
    {
        string p = prefix;
        while (p.Length > 0)
        {
            p = ParentOf(p);
            InterlockedMax(ref _cells.GetOrAdd(p, static _ => new Cell()).Invalidated, stamp);
        }
    }

    /// <summary>Queues a debounced drain. Coalesces any number of invalidations into one pass.</summary>
    public void ScheduleDrain()
    {
        lock (_timerGate)
        {
            _timer ??= new Timer(static state => _ = ((FolderMetricsIndex)state!).DrainAsync(), this, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(DebounceWindow, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Folds every dirty folder, deepest-first, then publishes the prefixes whose value changed.
    /// Non-reentrant: a concurrent call waits, and anything that arrives mid-drain is picked up by
    /// the trailing re-schedule.
    /// </summary>
    public async Task<IReadOnlyList<string>> DrainAsync(CancellationToken ct = default)
    {
        await _drainGate.WaitAsync(ct);
        try
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            List<string> dirty = _cells
                .Where(kv => kv.Value.Invalidated > kv.Value.Settled)
                .Select(kv => kv.Key)
                .OrderByDescending(Depth)                     // deepest-first ⇒ children settle before parents
                .ToList();

            if (dirty.Count == 0)
            {
                return Array.Empty<string>();
            }

            var changed = new List<string>();
            foreach (string prefix in dirty)
            {
                Cell cell = _cells[prefix];
                long observed = Interlocked.Read(ref cell.Invalidated);   // capture BEFORE

                (int count, DateTimeOffset? latest, Coverage coverage) = await FoldAsync(prefix, ct);

                if (Supersedes(coverage, cell.Coverage) &&
                    (cell.Count != count || cell.Latest != latest || cell.Coverage != coverage))
                {
                    cell.Count = count;
                    cell.Latest = latest;
                    cell.Coverage = coverage;
                    changed.Add(prefix);

                    // Our value moved, so every ancestor's is now suspect. Deepest-first ordering
                    // means they are folded later in this same pass, so one pass still converges;
                    // an ancestor that already settled in an earlier drain is picked up by the
                    // trailing re-schedule. Without this a node and its parent can disagree.
                    StampAncestors(prefix, NextStamp());
                }

                // Settle only on a fold that observed everything. An incomplete fold (a child cell
                // not discovered yet, or itself still partial) stays dirty so the trailing
                // re-schedule refolds it once its children have settled — otherwise a node folded
                // mid-scan would keep a lower bound forever.
                if (coverage == Coverage.Complete && Interlocked.Read(ref cell.Invalidated) == observed)
                {
                    cell.Settled = observed;
                    cell.SettledAtUtc = DateTimeOffset.UtcNow;
                }
            }

            if (changed.Count > 0)
            {
                Version = NextStamp();
                if (Changed is { } handler)
                {
                    await handler(changed);
                }
            }

            if (_cells.Any(kv => kv.Value.Invalidated > kv.Value.Settled))
            {
                ScheduleDrain();      // arrived mid-drain → next pass
            }

            logger.LogDebug("Drain folded {Dirty} folders, {Changed} changed", dirty.Count, changed.Count);
            return changed;
        }
        finally
        {
            _drainGate.Release();
        }
    }

    /// <summary>
    /// Registers every section prefix under <paramref name="root"/> so the drain has something to
    /// fold, marking each newly discovered folder dirty. Costs one level listing per folder — this
    /// is the expensive scan, and it runs in the background. Returns the prefixes it reached.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> DiscoverAsync(string root, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { root });

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await WalkAsync(Normalize(root), NextStamp(), visited, ct);
        return visited;
    }

    /// <summary>
    /// Drops cells whose folder no longer exists, so a deleted branch does not linger in the index
    /// or the snapshot. Only safe to call with the reachable set from a completed full discovery.
    /// </summary>
    public int PruneUnreachable(IReadOnlyCollection<string> reachable)
    {
        var keep = new HashSet<string>(reachable, StringComparer.OrdinalIgnoreCase) { string.Empty };
        int removed = 0;
        foreach (string prefix in _cells.Keys.Where(k => !keep.Contains(k)).ToArray())
        {
            if (_cells.TryRemove(prefix, out _))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            logger.LogInformation("Pruned {Count} stale nav metric cells", removed);
        }

        return removed;
    }

    private async Task WalkAsync(string prefix, long stamp, HashSet<string> visited, CancellationToken ct)
    {
        if (!visited.Add(prefix)) { return; }

        // Only newly discovered folders are marked dirty. Re-walking a branch that is already
        // tracked must not re-dirty it, or the startup scan would keep invalidating what the
        // concurrent drain has just settled.
        if (_cells.TryAdd(prefix, new Cell { Invalidated = stamp }))
        {
            ScheduleDrain();
        }

        foreach (NavChild child in await Nav.GetChildrenAsync(prefix, ct))
        {
            if (child.IsSection && child.Prefix is not null)
            {
                await WalkAsync(Normalize(child.Prefix), stamp, visited, ct);
            }
        }
    }

    /// <summary>
    /// Recomputes one folder from its direct children: section children contribute their own cell
    /// (no I/O), article leaves contribute 1. Only this folder's level is listed.
    /// </summary>
    private async Task<(int Count, DateTimeOffset? Latest, Coverage Coverage)> FoldAsync(string prefix, CancellationToken ct)
    {
        IReadOnlyList<NavChild> level = await Nav.GetChildrenAsync(prefix, ct);

        int count = 0;
        DateTimeOffset? latest = null;
        bool allKnown = true;
        bool anyContribution = false;

        foreach (NavChild child in level)
        {
            if (child.IsSection && child.Prefix is not null)
            {
                if (!_cells.TryGetValue(Normalize(child.Prefix), out Cell? sub) || sub.Coverage == Coverage.None)
                {
                    allKnown = false;         // unknown child contributes NOTHING (not zero)
                    continue;
                }

                if (sub.Coverage == Coverage.Partial)
                {
                    allKnown = false;
                }

                count += sub.Count;
                latest = Newer(latest, sub.Latest);
                anyContribution = true;
            }
            else if (!string.IsNullOrEmpty(child.Route))
            {
                count++;
                latest = Newer(latest, child.Date);
                anyContribution = true;
            }
        }

        Coverage coverage = allKnown ? Coverage.Complete
            : anyContribution ? Coverage.Partial
            : Coverage.None;

        return (count, latest, coverage);
    }

    // Complete outranks Partial outranks None: a lower bound may never replace a true total.
    private static bool Supersedes(Coverage incoming, Coverage current) => incoming >= current;

    private static DateTimeOffset? Newer(DateTimeOffset? a, DateTimeOffset? b) =>
        b is { } y && (a is not { } x || y > x) ? b : a;

    private static void InterlockedMax(ref long target, long value)
    {
        while (true)
        {
            long prev = Interlocked.Read(ref target);
            if (prev >= value || Interlocked.CompareExchange(ref target, value, prev) == prev)
            {
                return;
            }
        }
    }

    private static int Depth(string prefix) =>
        prefix.Length == 0 ? 0 : prefix.Count(c => c == '/') + 1;

    private static string ParentOf(string prefix)
    {
        int cut = prefix.LastIndexOf('/');
        return cut < 0 ? string.Empty : prefix[..cut];
    }

    private static string Normalize(string? prefix) =>
        (prefix ?? string.Empty).Replace('\\', '/').Trim('/');

    // ---- persistence ------------------------------------------------------------------------

    /// <summary>
    /// Writes the whole index to a single JSON artifact. One file, not one per folder: restart needs
    /// a single read, and no derived value is ever written into an authored content file.
    /// </summary>
    public async Task SaveSnapshotAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var payload = _cells
                .Where(kv => kv.Value.Coverage != Coverage.None)
                .ToDictionary(kv => kv.Key, kv => new SnapshotEntry(kv.Value.Count, kv.Value.Latest, kv.Value.Coverage));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(payload), ct);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav metrics snapshot save failed");
        }
    }

    /// <summary>
    /// Seeds the index from a previous run so a restart shows the last known counts immediately
    /// instead of climbing from nothing. Every seeded cell is left <em>dirty</em>, so a restart is
    /// simply a global invalidation over a warm seed — not a separate code path.
    /// </summary>
    public async Task<int> LoadSnapshotAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            var payload = JsonSerializer.Deserialize<Dictionary<string, SnapshotEntry>>(
                await File.ReadAllTextAsync(path, ct));
            if (payload is null)
            {
                return 0;
            }

            long stamp = NextStamp();
            foreach ((string prefix, SnapshotEntry entry) in payload)
            {
                Cell cell = _cells.GetOrAdd(prefix, static _ => new Cell());
                cell.Count = entry.Count;
                cell.Latest = entry.Latest;
                cell.Coverage = entry.Coverage;
                cell.Invalidated = stamp;
                cell.Settled = 0;
            }

            logger.LogInformation("Nav metrics seeded from snapshot: {Count} folders", payload.Count);
            return payload.Count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nav metrics snapshot load failed");
            return 0;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _drainGate.Dispose();
    }

    private sealed class Cell
    {
        public int Count;
        public DateTimeOffset? Latest;
        public Coverage Coverage = Coverage.None;
        public long Invalidated;
        public long Settled;
        public DateTimeOffset? SettledAtUtc;
    }

    private sealed record SnapshotEntry(int Count, DateTimeOffset? Latest, Coverage Coverage);
}

/// <summary>A folder's current derived metrics as served to the navigation builder.</summary>
public readonly record struct FolderMetrics(int Count, DateTimeOffset? Latest, Coverage Coverage);

/// <summary>Diagnostic view of one cell, including its provenance stamps.</summary>
public sealed record FolderCellView(
    int Count,
    DateTimeOffset? Latest,
    Coverage Coverage,
    long Invalidated,
    long Settled,
    DateTimeOffset? SettledAtUtc)
{
    public bool IsDirty => Invalidated > Settled;
}
