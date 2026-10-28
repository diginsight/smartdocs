using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Composes the site-wide counts a reader may see from the shared <see cref="FolderMetricsIndex"/>.
/// <para>
/// The index holds one cell per space (a mount folder, plus the root-own cell for a root-mounted
/// space), so a reader's total is the sum of the cells of the spaces they see. Nothing here is cached
/// and nothing derived from a reader is stored: the composition runs at every read and every push.
/// </para>
/// </summary>
public sealed class ReaderCountComposer(FolderMetricsIndex metrics, SpaceRegistry spaces)
{
    /// <summary>
    /// The site-wide counts for a reader with <paramref name="visibility"/>, or null while the index
    /// has no root cell. A reader who sees every space gets the root cell unchanged; otherwise the
    /// counts add up the visible spaces' cells, the newest date is the newest of them, and the
    /// coverage is the weakest of them, so a partly known space never reads as a complete total.
    /// </summary>
    public FolderMetrics? ComposeRoot(ReaderVisibility visibility)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        if (metrics.TryGet(string.Empty) is not { } root)
        {
            return null;
        }

        if (visibility.SeesEverything)
        {
            return root;
        }

        int count = 0;
        DateTimeOffset? latest = null;
        Coverage? weakest = null;
        foreach (SpaceOptions space in spaces.All)
        {
            if (!visibility.Spaces.TryGetValue(space.Id, out SpaceVisibility? verdict) || !verdict.IsVisible)
            {
                continue;
            }

            FolderMetrics cell = (space.IsRootMounted
                ? metrics.TryGetRootOwn()
                : metrics.TryGet(space.NormalizedRouteBase.Trim('/'))) ?? default;
            count += cell.Count;
            if (cell.Latest is { } date && (latest is not { } known || date > known))
            {
                latest = date;
            }

            weakest = weakest is { } w && w < cell.Coverage ? w : cell.Coverage;
        }

        return new FolderMetrics(count, latest, weakest ?? root.Coverage);
    }

    /// <summary><paramref name="record"/> with the counts of its root composed for the reader. Other records pass through.</summary>
    public FolderRecord ComposeRootRecord(FolderRecord record, ReaderVisibility visibility)
    {
        if (record.Prefix.Length > 0 || visibility.SeesEverything || ComposeRoot(visibility) is not { } composed)
        {
            return record;
        }

        return record with
        {
            ArticleCount = composed.Count,
            LatestArticleUtc = composed.Latest,
            Coverage = composed.Coverage,
        };
    }
}
