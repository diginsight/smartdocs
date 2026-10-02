using Diginsight.Components;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;
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
/// <param name="levelSource">
/// The decorated builder, used by the whole-tree walk behind <see cref="GetIndexAsync"/> so each
/// level it visits comes from (and populates) the level cache. Recursing through this class's own
/// <see cref="GetChildrenAsync"/> would silently opt the walk out of the decorator wrapping it, and
/// re-list the entire tree on every index build. <see cref="Lazy{T}"/> because the decorator holds
/// this instance: the cycle is only legal because it is resolved on first use, never in the constructor.
/// </param>
/// <param name="spaces">
/// The configured spaces. At the site root, a folder that is a space's mount point is labelled with
/// the space title, ordered by configuration position, and always kept as a section — its name is a
/// route base chosen by configuration, not an authored folder the naming rules were written for.
/// </param>
/// <param name="assetFolderNames">
/// Folder names treated as asset folders in addition to <see cref="NavRules.AssetFolderNames"/>,
/// from <c>Site:AssetFolders</c>.
/// </param>
public sealed class DynamicNavBuilder(
    IContentLister lister,
    FolderMetricsIndex metrics,
    IParallelService parallelService,
    Lazy<INavBuilder> levelSource,
    ILogger<DynamicNavBuilder> logger,
    SpaceRegistry? spaces = null,
    IEnumerable<string>? assetFolderNames = null) : INavBuilder
{
    private readonly HashSet<string> assetFolders =
        new(NavRules.AssetFolderNames.Concat(assetFolderNames ?? []), StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<NavChild>> GetChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { prefix });

        prefix = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        return await BuildLevelAsync(prefix, ct);
    }

    public async Task<IReadOnlyList<NavLeaf>> GetIndexAsync(CancellationToken ct = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger);

        var leaves = new List<NavLeaf>();
        await WalkAsync(string.Empty, string.Empty, leaves, ct);
        return leaves;
    }

    /// <summary>
    /// Flattens the tree into navigable leaves (menu search / prev-next). Counting is the index's job.
    /// Recursion goes through <c>levelSource</c>, not <see cref="GetChildrenAsync"/>: every level the
    /// walk touches is one the menu will ask for anyway, so taking them from the cache turns a second
    /// full listing of the tree into cache hits and leaves the level cache warm behind it.
    /// </summary>
    private async Task WalkAsync(string prefix, string path, List<NavLeaf> leaves, CancellationToken ct)
    {
        foreach (NavChild n in await levelSource.Value.GetChildrenAsync(prefix, ct))
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
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { prefix });

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
        if (prefix.Length == 0 && entry.IsFolder && spaces?.MountedAt(entry.Name) is { } mounted)
        {
            return ScoreMount(mounted, entry);
        }

        if (NavRules.IsExcludedName(entry.Name) || IsInfrastructure(prefix, entry))
        {
            return null;
        }

        if (entry.IsFolder)
        {
            // Asset folders hold images and media, never pages: they are not sections, not links,
            // and not crawled. The rule used to apply only when deciding whether a parent had
            // meaningful subfolders, so an image tree with nested folders became a menu section.
            if (assetFolders.Contains(entry.Name))
            {
                return null;
            }

            // One listing per folder, shared by the metadata lookup and the classification below.
            // Both used to list it independently, and the metadata read used to be a blind probe for
            // a file that exists in a handful of folders out of hundreds — so the overwhelmingly
            // common answer was a not-found round trip. The listing already carries that answer.
            IReadOnlyList<ChildEntry> kids = await lister.ListChildrenAsync(entry.Path, ct) ?? [];

            FolderMeta meta = await ReadFolderMetaAsync(kids, ct);
            if (meta.Hidden)
            {
                return null; // metadata.yml opted the folder out of navigation
            }

            NavChild? folderNode = await ClassifyFolderAsync(entry, kids, meta, ct);
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
            ArticleHead head = await lister.ReadArticleHeadAsync(entry.Path, ct);
            if (head.Hidden)
            {
                return null;
            }

            string label = head.Title ?? NavRules.Label(Path.GetFileNameWithoutExtension(entry.Name));
            return (NavRules.SortKey(entry.Name),
                new NavChild(label, Route(entry.Path), null, null, false, false,
                    Date: head.Date, Author: head.Author));
        }

        return null;
    }

    /// <summary>A space's mount point at the site root: always a section, labelled and ordered by configuration.</summary>
    private (SortTuple Key, NavChild Node) ScoreMount(SpaceOptions space, ChildEntry entry)
    {
        string label = string.IsNullOrWhiteSpace(space.Title) ? NavRules.Label(entry.Name) : space.Title;
        (int? articleCount, DateTimeOffset? latestUtc, Coverage coverage) = FolderAggregate(entry.Path, FolderMeta.None);
        var node = new NavChild(label, Route(entry.Path), entry.Path, "journal-bookmark", true, true,
            ArticleCount: articleCount, LatestArticleUtc: latestUtc, CountCoverage: coverage);

        // Group 0 with a negative weight: mounts come first, in configuration order, ahead of a
        // root-mounted space's own numbered sections.
        return (new SortTuple(0, spaces!.IndexOf(space) - 10_000, entry.Name.ToLowerInvariant()), node);
    }

    /// <summary>Decides whether a folder is a section, a collapsed single link, or nothing.</summary>
    /// <param name="kids">The folder's already-materialised children, listed once by the caller.</param>
    private async Task<NavChild?> ClassifyFolderAsync(ChildEntry folder, IReadOnlyList<ChildEntry> kids, FolderMeta meta, CancellationToken ct)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { folder });

        var subFolders = kids.Where(k => k.IsFolder && !NavRules.IsExcludedName(k.Name)
                                         && !assetFolders.Contains(k.Name) && !BuildOutput.Contains(k.Name)).ToList();
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
        ArticleHead singleHead = await lister.ReadArticleHeadAsync(single.Path, ct);
        if (articles.Count == 1 && singleHead.Hidden)
        {
            return index is null ? null
                : new NavChild(meta.Label ?? NavRules.Label(folder.Name), Route(folder.Path), null, meta.Icon, false, false);
        }

        string? title = singleHead.Title;
        string label = meta.Label ?? (title is not null
            ? NavRules.WithDatePrefix(folder.Name, title)
            : NavRules.Label(folder.Name));
        string route = single == index ? Route(folder.Path) : Route(single.Path);
        return new NavChild(label, route, null, meta.Icon, false, false,
            Date: singleHead.Date, Author: singleHead.Author);
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

    /// <summary>
    /// Reads a folder's optional <c>metadata.yml</c> overrides from its already-listed children.
    /// Absent file → no overrides, and no read at all: the listing is authoritative about what the
    /// folder contains, so asking the store for a file it did not mention can only answer "no".
    /// </summary>
    private async Task<FolderMeta> ReadFolderMetaAsync(IReadOnlyList<ChildEntry> kids, CancellationToken ct)
    {
        ChildEntry? entry = kids.FirstOrDefault(
            k => !k.IsFolder && string.Equals(k.Name, "metadata.yml", StringComparison.OrdinalIgnoreCase));

        return entry is null ? FolderMeta.None : await lister.ReadFolderMetaAsync(entry.Path, ct);
    }

    // Project and infrastructure folders at the top of a space — a space can be a repository clone,
    // whose root holds source, scripts and build output beside the content.
    private static readonly HashSet<string> RootInfra = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "deploy", "docs", "scripts", "readme_files", "bin", "obj", "node_modules",
        "99.00-temp",
    };

    // Build output, excluded at any depth: a code sample's bin or obj folder is never content.
    private static readonly HashSet<string> BuildOutput = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules",
    };

    private bool IsInfrastructure(string prefix, ChildEntry entry) =>
        (entry.IsFolder && BuildOutput.Contains(entry.Name)) ||
        (IsSpaceRoot(prefix) && RootInfra.Contains(entry.Name));

    // The site root is the root-mounted space's top level; a mount segment is a prefixed space's.
    private bool IsSpaceRoot(string prefix) =>
        prefix.Length == 0 || spaces?.MountedAt(prefix) is not null;

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
