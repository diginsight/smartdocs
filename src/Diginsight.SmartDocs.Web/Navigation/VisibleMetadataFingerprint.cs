using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>Fingerprints navigation metadata within one space without exposing its contents to clients.</summary>
public sealed class VisibleMetadataFingerprint(
    IContentLister content,
    ContentMetadataProvider metadata,
    SpaceRegistry spaces,
    SiteOptions site,
    MetadataInvalidationTracker invalidations,
    ContentFreshnessOptions freshness,
    TimeProvider clock)
{
    private readonly HashSet<string> assetFolders =
        new(NavRules.AssetFolderNames.Concat(site.AssetFolders), StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> ForSpaceAsync(SpaceOptions space, CancellationToken ct = default)
    {
        string root = space.NormalizedRouteBase.Trim('/');
        TimeSpan maxAge = freshness.Access < freshness.Structure ? freshness.Access : freshness.Structure;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            long generation = invalidations.Generation(root);
            long timestamp = clock.GetTimestamp();
            Entry entry = entries.AddOrUpdate(space.Id,
                _ => NewEntry(),
                (_, previous) => previous.Generation > generation ||
                    (previous.Generation == generation &&
                        clock.GetElapsedTime(previous.Timestamp, timestamp) < maxAge)
                    ? previous : NewEntry());

            Fingerprint result;
            try
            {
                result = await entry.Value.Value.WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                Remove(space.Id, entry);
                throw;
            }

            if (entry.Generation != invalidations.Generation(root))
            {
                Remove(space.Id, entry);
                continue;
            }

            if (!result.Cacheable)
            {
                Remove(space.Id, entry);
            }
            return result.Value;

            Entry NewEntry() => new(generation, timestamp,
                new Lazy<Task<Fingerprint>>(() => ComputeAsync(space, root)));
        }
    }

    private async Task<Fingerprint> ComputeAsync(SpaceOptions space, string root)
    {
        bool cacheable = true;
        var records = new List<string>();
        Add(root, await metadata.GetForSpaceAsync(space.Id));
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(root);

        while (pending.TryDequeue(out string? folder))
        {
            if (!visited.Add(folder))
            {
                continue;
            }

            IReadOnlyList<ChildEntry> children = await content.ListChildrenAsync(folder);
            foreach (ChildEntry child in children)
            {
                if (child.IsFolder)
                {
                    if (!NavRules.IsExcludedName(child.Name) && !assetFolders.Contains(child.Name) &&
                        spaces.TryResolve(child.Path, out SpaceOptions owner, out _) &&
                        string.Equals(owner.Id, space.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Enqueue(child.Path);
                    }
                }
                else if (folder != root && MetadataFiles.IsCanonicalKey(child.Name))
                {
                    Add(child.Path, await metadata.GetForMetadataKeyAsync(child.Path));
                }
            }
        }

        records.Sort(StringComparer.Ordinal);
        string value = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', records))));
        return new Fingerprint(value, cacheable);

        void Add(string path, MetadataOutcome outcome)
        {
            cacheable &= outcome.Status != MetadataStatus.Unreadable;
            records.Add($"{path.Length}:{path}|{outcome.Status}|{outcome.Fingerprint}|{outcome.Document?.Access?.Fingerprint}");
        }
    }

    private void Remove(string spaceId, Entry entry) =>
        ((ICollection<KeyValuePair<string, Entry>>)entries).Remove(new(spaceId, entry));

    private sealed record Fingerprint(string Value, bool Cacheable);

    private sealed class Entry(long generation, long timestamp, Lazy<Task<Fingerprint>> value)
    {
        public long Generation { get; } = generation;
        public long Timestamp { get; } = timestamp;
        public Lazy<Task<Fingerprint>> Value { get; } = value;
    }
}
