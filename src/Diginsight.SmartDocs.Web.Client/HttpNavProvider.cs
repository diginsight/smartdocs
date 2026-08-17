using System.Net.Http.Json;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Client;

/// <summary>WASM <see cref="INavProvider"/> — fetches one level per prefix from the nav API, cached in-memory.</summary>
public sealed class HttpNavProvider(HttpClient http) : INavProvider
{
    // Cache the in-flight TASK (not just the result) so concurrent callers for the same prefix
    // (sidebar + both top-bar halves during the initial render) share ONE HTTP request instead of
    // each firing their own. WASM is single-threaded, so a plain Dictionary is safe here.
    private readonly Dictionary<string, Task<IReadOnlyList<NavChild>>> _children = new();
    private Task<IReadOnlyList<NavLeaf>>? _index;

    public Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        prefix ??= string.Empty;
        if (_children.TryGetValue(prefix, out Task<IReadOnlyList<NavChild>>? existing))
        {
            return existing;
        }

        Task<IReadOnlyList<NavChild>> task = FetchChildrenAsync(prefix, ct);
        _children[prefix] = task;
        return task;
    }

    /// <summary>Drops the cached task for <paramref name="prefix"/> so the next fetch re-hits the API.</summary>
    public Task<IReadOnlyList<NavChild>> RefreshChildrenAsync(string prefix, CancellationToken ct = default)
    {
        prefix ??= string.Empty;
        _children.Remove(prefix);
        return GetChildrenAsync(prefix, ct);
    }

    public async Task<FolderArticleStats?> GetTotalAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync("_nav/total", ct);
            return response.IsSuccessStatusCode && response.Content.Headers.ContentLength != 0
                ? await response.Content.ReadFromJsonAsync<FolderArticleStats>(cancellationToken: ct)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Applies server-pushed absolute folder aggregates to the in-memory cache: for every already
    /// loaded level, any child whose <c>Prefix</c> matches a delta has its <c>ArticleCount</c> and
    /// <c>LatestArticleUtc</c> replaced (records copied via <c>with</c>). No HTTP — this is the live,
    /// poll-free update path. Returns true if any cached entry changed.
    /// </summary>
    public bool ApplyAggregates(IReadOnlyList<NavAggregateDelta> deltas)
    {
        if (deltas is null || deltas.Count == 0)
        {
            return false;
        }

        var byPrefix = new Dictionary<string, NavAggregateDelta>(StringComparer.OrdinalIgnoreCase);
        foreach (NavAggregateDelta d in deltas)
        {
            byPrefix[d.Prefix] = d;
        }

        bool anyChanged = false;
        foreach (string levelPrefix in _children.Keys.ToArray())
        {
            Task<IReadOnlyList<NavChild>> task = _children[levelPrefix];
            if (task is not { IsCompletedSuccessfully: true })
            {
                continue; // don't touch in-flight levels; they'll fetch the fresh count on completion
            }

            IReadOnlyList<NavChild> current = task.Result;
            List<NavChild>? updated = null;
            for (int i = 0; i < current.Count; i++)
            {
                NavChild child = current[i];
                if (child.Prefix is { } p && byPrefix.TryGetValue(p, out NavAggregateDelta? d) && d is not null
                    && (child.ArticleCount != d.ArticleCount || child.LatestArticleUtc != d.LatestUtc
                        || child.CountCoverage != d.Coverage))
                {
                    updated ??= new List<NavChild>(current);
                    updated[i] = child with
                    {
                        ArticleCount = d.ArticleCount,
                        LatestArticleUtc = d.LatestUtc,
                        CountCoverage = d.Coverage,
                    };
                }
            }

            if (updated is not null)
            {
                _children[levelPrefix] = Task.FromResult<IReadOnlyList<NavChild>>(updated);
                anyChanged = true;
            }
        }

        return anyChanged;
    }

    private async Task<IReadOnlyList<NavChild>> FetchChildrenAsync(string prefix, CancellationToken ct)
    {
        try
        {
            List<NavChild>? result = await http.GetFromJsonAsync<List<NavChild>>(
                $"_nav/children?prefix={Uri.EscapeDataString(prefix)}", ct);
            return result ?? new List<NavChild>();
        }
        catch
        {
            _children.Remove(prefix); // drop the failed task so a later call can retry
            return Array.Empty<NavChild>();
        }
    }

    public Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
        => _index ??= FetchIndexAsync(ct);

    private async Task<IReadOnlyList<NavLeaf>> FetchIndexAsync(CancellationToken ct)
    {
        try
        {
            List<NavLeaf>? result = await http.GetFromJsonAsync<List<NavLeaf>>("_nav/index", ct);
            return result ?? new List<NavLeaf>();
        }
        catch
        {
            _index = null; // drop the failed task so a later call can retry
            return Array.Empty<NavLeaf>();
        }
    }
}
