using Diginsight.SmartDocs.Web.Shared.Services;

namespace Diginsight.SmartDocs.Web.Caching;

/// <summary>Tracks invalidations, including those received before a metadata scope is first read.</summary>
public sealed class MetadataInvalidationTracker
{
    private readonly object gate = new();
    private readonly Dictionary<string, long> changes = new(StringComparer.OrdinalIgnoreCase);
    private long sequence;

    public long Generation(string metadataKey)
    {
        return Generation([metadataKey]);
    }

    public long Generation(ContentPathCacheKey key) =>
        Generation(key.Kind == "page-route-v1"
            ? PageLoader.Candidates(key.Path)
            : [key.Path]);

    private long Generation(IEnumerable<string> paths)
    {
        string[] candidates = paths.ToArray();
        lock (gate)
        {
            long result = 0;
            foreach ((string path, long version) in changes)
            {
                if (version > result && candidates.Any(candidate => ContentPathCacheKey.OnSameBranch(candidate, path)))
                {
                    result = version;
                }
            }

            return result;
        }
    }

    public void Invalidate(ContentPathInvalidationRule rule)
    {
        if (rule.Kind is not null)
        {
            return;
        }

        lock (gate)
        {
            long version = ++sequence;
            foreach (string path in rule.Paths)
            {
                if (path.Length == 0)
                {
                    changes.Clear();
                }
                else
                {
                    foreach (string child in changes.Keys
                        .Where(key => key.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)).ToArray())
                    {
                        changes.Remove(child);
                    }
                }
                changes[path] = version;
            }
        }
    }
}
