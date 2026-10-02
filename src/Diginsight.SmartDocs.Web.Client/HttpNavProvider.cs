using System.Net.Http.Json;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Client;

/// <summary>WASM <see cref="INavProvider"/> — fetches one level per prefix from the nav API, cached in-memory.</summary>
public sealed class HttpNavProvider(
    HttpClient http,
    NavigationBootstrapState bootstrap) : INavProvider
{
    // Cache the in-flight TASK (not just the result) so concurrent callers for the same prefix
    // (sidebar + both top-bar halves during the initial render) share ONE HTTP request instead of
    // each firing their own. WASM is single-threaded, so a plain Dictionary is safe here.
    private readonly Dictionary<string, Task<NavLevelResponse>> _children = SeedLevels(bootstrap);
    private readonly Dictionary<string, FolderRecord> _folders =
        new(bootstrap.FolderRecords, StringComparer.OrdinalIgnoreCase);
    private Task<IReadOnlyList<NavLeaf>>? _index;

    private static Dictionary<string, Task<NavLevelResponse>> SeedLevels(
        NavigationBootstrapState bootstrap)
    {
        var seeded = new Dictionary<string, Task<NavLevelResponse>>(StringComparer.OrdinalIgnoreCase);
        foreach ((string prefix, NavChild[] level) in bootstrap.Levels)
        {
            var references = new List<NavLevelReference>(level.Length);
            var records = new List<FolderRecord>();
            foreach (NavChild child in level)
            {
                if (child.IsSection && child.Prefix is { } folderPrefix)
                {
                    references.Add(NavLevelReference.Folder(folderPrefix));
                    if (bootstrap.FolderRecords.TryGetValue(folderPrefix, out FolderRecord? record))
                    {
                        records.Add(record);
                    }
                }
                else
                {
                    references.Add(NavLevelReference.Article(child));
                }
            }

            seeded[prefix] = Task.FromResult(
                new NavLevelResponse(references, records));
        }

        return seeded;
    }

    public async Task<FolderRecord?> GetFolderAsync(string prefix, CancellationToken ct = default)
    {
        prefix = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        if (_folders.TryGetValue(prefix, out FolderRecord? cached))
        {
            return cached;
        }

        FolderRecord? record = await http.GetFromJsonAsync<FolderRecord>(
            $"_nav/folder?prefix={Uri.EscapeDataString(prefix)}",
            ct);
        if (record is not null)
        {
            ApplyFolderRecords([record]);
        }

        return record;
    }

    public Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        prefix ??= string.Empty;
        if (!_children.TryGetValue(prefix, out Task<NavLevelResponse>? level))
        {
            level = FetchChildrenAsync(prefix, ct);
            _children[prefix] = level;
        }

        return MaterializeVisibleAsync(level);
    }

    private async Task<IReadOnlyList<NavChild>> MaterializeVisibleAsync(Task<NavLevelResponse> level)
    {
        NavLevelResponse response = await level;
        var children = new List<NavChild>(response.Children.Count);
        foreach (NavLevelReference child in response.Children)
        {
            if (child.Leaf is { } leaf)
            {
                children.Add(leaf);
                continue;
            }

            if (child.FolderPrefix is not { } prefix || !_folders.TryGetValue(prefix, out FolderRecord? folder))
            {
                throw new InvalidOperationException($"Navigation level references missing folder record '{child.FolderPrefix}'.");
            }

            children.Add(new NavChild(
                folder.Label,
                folder.Route,
                folder.Prefix,
                folder.Icon,
                true,
                folder.HasChildren,
                folder.Short,
                folder.TopbarHidden,
                folder.TopbarAlign,
                ArticleCount: folder.Coverage == Coverage.None ? null : folder.ArticleCount,
                LatestArticleUtc: folder.LatestArticleUtc,
                CountCoverage: folder.Coverage));
        }

        return NavRules.WithoutEmptySections(children);
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
        FolderRecord? root = await GetFolderAsync(string.Empty, ct);
        if (root is null)
        {
            return null;
        }

        return new FolderArticleStats(root.ArticleCount, root.LatestArticleUtc, null, root.Coverage);
    }

    /// <summary>
    /// Applies server-pushed absolute folder aggregates to the in-memory cache: for every already
    /// loaded level, any child whose <c>Prefix</c> matches a delta has its <c>ArticleCount</c> and
    /// <c>LatestArticleUtc</c> replaced (records copied via <c>with</c>). No HTTP — this is the live,
    /// poll-free update path. Returns true if any cached entry changed.
    /// </summary>
    public bool ApplyFolderRecords(IReadOnlyList<FolderRecord> records)
    {
        if (records is null || records.Count == 0)
        {
            return false;
        }

        bool changed = false;
        foreach (FolderRecord record in records)
        {
            changed |= !_folders.TryGetValue(record.Prefix, out FolderRecord? current) || current != record;
            _folders[record.Prefix] = record;
            bootstrap.FolderRecords[record.Prefix] = record;
        }

        return changed;
    }

    private async Task<NavLevelResponse> FetchChildrenAsync(string prefix, CancellationToken ct)
    {
        try
        {
            NavLevelResponse? result = await http.GetFromJsonAsync<NavLevelResponse>(
                $"_nav/children?prefix={Uri.EscapeDataString(prefix)}", ct);
            if (result is null)
            {
                throw new InvalidOperationException($"Navigation endpoint returned no level for '{prefix}'.");
            }

            ApplyFolderRecords(result.Folders);
            return result;
        }
        catch
        {
            _children.Remove(prefix); // drop the failed task so a later call can retry
            throw;
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
