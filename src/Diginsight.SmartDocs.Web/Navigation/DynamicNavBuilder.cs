using Diginsight.Components;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.Extensions.Logging;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Builds one level of the site menu on demand from the live content hierarchy, applying the
/// sidebar spec rules (exclusions, single-article collapse, index/readme representation,
/// date-preserving labels, newest-first ordering, icon heuristic). This class contains pure
/// navigation-building logic with no caching concern — use <see cref="CachedDynamicNavBuilder"/>
/// as the decorator that adds SmartCache.
/// <para>
/// Recursive folder counts are NOT computed here: they are read from <see cref="FolderMetricsIndex"/>,
/// the single authoritative projection, so a level built before the scan settles carries
/// <see cref="Coverage.None"/> rather than a misleading zero.
/// </para>
/// </summary>
public sealed class DynamicNavBuilder(
    IContentLister lister,
    FolderMetricsIndex metrics,
    IParallelService parallelService,
    ILogger<DynamicNavBuilder> logger) : INavBuilder
{
    public async Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        prefix = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        return await BuildLevelAsync(prefix, ct);
    }

    public async Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var leaves = new List<NavLeaf>();
        await WalkAsync(string.Empty, string.Empty, leaves, ct);
        return leaves;
    }

    /// <summary>Flattens the tree into navigable leaves (menu search / prev-next). Counting is the index's job.</summary>
    private async Task WalkAsync(string prefix, string path, List<NavLeaf> leaves, CancellationToken ct)
    {
        foreach (NavChild n in await GetChildrenAsync(prefix, ct))
        {
            if (n.IsSection && n.Prefix is not null)
            {
                string childPath = path.Length == 0 ? n.Text : $"{path} › {n.Text}";
                await WalkAsync(n.Prefix, childPath, leaves, ct);
            }
            else if (!string.IsNullOrEmpty(n.Route))
            {
                leaves.Add(new NavLeaf(n.Text, n.Route, path, n.Date, n.Author));
            }
        }
    }

    private async Task<IReadOnlyList<NavChild>> BuildLevelAsync(string prefix, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        // A content source that has nothing (or hasn't settled) may yield null; treat it as empty
        // so the level renders as "no children yet" instead of throwing on the LINQ below.
        IReadOnlyList<ChildEntry> raw = await lister.ListChildrenAsync(prefix, ct) ?? [];

        // Each sibling's metadata/frontmatter read is independent; final order comes from the sort
        // below regardless of completion order, so scoring can run concurrently.
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parallelService.MediumConcurrency, CancellationToken = ct };
        IEnumerable<Func<Task<(SortTuple Key, NavChild Node)?>>> entryTasks = raw.Select(entry => (Func<Task<(SortTuple Key, NavChild Node)?>>)(() => ScoreEntryAsync(prefix, entry, ct)));
        IEnumerable<(SortTuple Key, NavChild Node)?> scoredResults = await parallelService.WhenAllAsync(entryTasks, parallelOptions) ?? [];

        var scored = scoredResults.Where(x => x is not null).Select(x => x!.Value).ToList();

        List<NavChild> result = scored
            .OrderBy(x => x.Key.Group).ThenBy(x => x.Key.Num).ThenBy(x => x.Key.Text, StringComparer.Ordinal)
            .Select(x => x.Node)
            .ToList();

        // The site root gets a leading Home link.
        if (prefix.Length == 0)
        {
            result.Insert(0, new NavChild("Home", string.Empty, null, "house-fill", false, false));
        }

        activity?.SetOutput(new { count = result.Count });
        return result;
    }

    /// <summary>Per-entry scoring extracted out of <see cref="BuildLevelAsync"/> so siblings can be scored concurrently.</summary>
    private async Task<(SortTuple Key, NavChild Node)?> ScoreEntryAsync(string prefix, ChildEntry entry, CancellationToken ct)
    {
        if (NavRules.IsExcludedName(entry.Name) || IsTempRoot(prefix, entry.Name))
        {
            return null;
        }

        if (entry.IsFolder)
        {
            FolderMeta meta = await ReadFolderMetaAsync(entry.Path, ct);
            if (meta.Hidden)
            {
                return null; // metadata.yml opted the folder out of navigation
            }

            NavChild? folderNode = await ClassifyFolderAsync(entry, meta, ct);
            if (folderNode is null)
            {
                return null;
            }

            SortTuple key = meta.Order is double order
                ? new SortTuple(0, order, entry.Name.ToLowerInvariant())
                : NavRules.SortKey(entry.Name);
            return (key, folderNode);
        }

        if (NavRules.IsMarkdown(entry.Name) && !NavRules.IsIndexName(entry.Name))
        {
            string? head = await lister.ReadHeadAsync(entry.Path, ct);
            FrontMatterInfo fm = FrontMatter.Parse(head);
            if (fm.Hidden)
            {
                return null;
            }

            string label = FrontMatter.ResolveTitle(head)
                ?? NavRules.Label(Path.GetFileNameWithoutExtension(entry.Name));
            return (NavRules.SortKey(entry.Name),
                new NavChild(label, Route(entry.Path), null, null, false, false,
                    Date: FrontMatter.ParseDate(fm.Date), Author: fm.Author));
        }

        return null;
    }

    /// <summary>Decides whether a folder is a section, a collapsed single link, or nothing.</summary>
    private async Task<NavChild?> ClassifyFolderAsync(ChildEntry folder, FolderMeta meta, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { folder });

        IReadOnlyList<ChildEntry> kids = await lister.ListChildrenAsync(folder.Path, ct) ?? [];

        var subFolders = kids.Where(k => k.IsFolder && !NavRules.IsExcludedName(k.Name) && !NavRules.IsAssetFolder(k.Name)).ToList();
        var articles = kids.Where(k => !k.IsFolder && NavRules.IsMarkdown(k.Name)
                                       && !NavRules.IsExcludedName(k.Name) && !NavRules.IsIndexName(k.Name)).ToList();
        ChildEntry? index = kids.FirstOrDefault(k => !k.IsFolder && NavRules.IsIndexName(k.Name));

        string icon = meta.Icon ?? NavRules.IconFor(folder.Name, folder.Name);

        // Section: has meaningful subfolders, or more than one article.
        if (subFolders.Count > 0 || articles.Count > 1)
        {
            string? href = Route(folder.Path);
            (int? articleCount, DateTimeOffset? latestUtc, Coverage coverage) = FolderAggregate(folder.Path, meta);
            return new NavChild(meta.Label ?? NavRules.Label(folder.Name), href, folder.Path, icon, true, true,
                meta.Short, meta.TopbarHidden, meta.TopbarAlign,
                ArticleCount: articleCount, LatestArticleUtc: latestUtc, CountCoverage: coverage);
        }

        // Collapse: exactly one article (or only an index/readme) → single link.
        ChildEntry? single = articles.Count == 1 ? articles[0] : index;
        if (single is null)
        {
            return null; // no publishable content
        }

        // Collapsed folders render as article links: no folder symbol unless metadata.yml sets one.
        string? head = await lister.ReadHeadAsync(single.Path, ct);
        FrontMatterInfo singleFm = FrontMatter.Parse(head);
        if (articles.Count == 1 && singleFm.Hidden)
        {
            return index is null ? null
                : new NavChild(meta.Label ?? NavRules.Label(folder.Name), Route(folder.Path), null, meta.Icon, false, false);
        }

        string? title = FrontMatter.ResolveTitle(head);
        string label = meta.Label ?? (title is not null
            ? NavRules.WithDatePrefix(folder.Name, title)
            : NavRules.Label(folder.Name));
        string route = single == index ? Route(folder.Path) : Route(single.Path);
        return new NavChild(label, route, null, meta.Icon, false, false,
            Date: FrontMatter.ParseDate(singleFm.Date), Author: singleFm.Author);
    }

    /// <summary>
    /// Folder aggregates, in precedence order: the authoritative index value, else the
    /// <c>metadata.yml</c> seed (a lower bound that travels with the content), else unknown.
    /// Unknown is never rendered as zero.
    /// </summary>
    private (int? Count, DateTimeOffset? Latest, Coverage Coverage) FolderAggregate(string folderPath, FolderMeta meta)
    {
        if (metrics.TryGet(folderPath) is { } m)
        {
            return (m.Count, m.Latest, m.Coverage);
        }

        return meta.ArticleCount is { } seed
            ? (seed, meta.LatestArticleUtc, Coverage.Partial)
            : (null, null, Coverage.None);
    }

    /// <summary>Reads a folder's optional <c>metadata.yml</c> overrides (absent file → no overrides).</summary>
    private async Task<FolderMeta> ReadFolderMetaAsync(string folderPath, CancellationToken ct)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { folderPath });

        string dir = (folderPath ?? string.Empty).Replace('\\', '/').Trim('/');
        string key = dir.Length == 0 ? "metadata.yml" : $"{dir}/metadata.yml";
        string? text = await lister.ReadHeadAsync(key, ct);
        return FolderMeta.Parse(text);
    }

    // Root-level folders that are project/infrastructure, not site content (only relevant when the
    // content source is the repo filesystem; the blob container holds content only).
    private static readonly HashSet<string> RootInfra = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "deploy", "docs", "scripts", "readme_files", "bin", "obj", "node_modules",
        "99.00-temp",
    };

    private static bool IsTempRoot(string prefix, string name) =>
        prefix.Length == 0 && RootInfra.Contains(name);

    private static string Route(string path)
    {
        string r = path.Replace('\\', '/').Trim('/');
        foreach (string ext in new[] { ".md", ".qmd" })
        {
            if (r.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return r[..^ext.Length];
            }
        }

        return r;
    }
}
