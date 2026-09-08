namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// Reader-controlled ordering and search for navigation levels.
/// <para>
/// The server emits every level in <em>curated</em> order — the order the content hierarchy itself
/// declares through numeric filename prefixes and folder metadata. The alternative orders are a
/// pure client-side re-projection of that same list, so switching order can never change which
/// nodes exist, only their sequence.
/// </para>
/// </summary>
public static class NavOrdering
{
    /// <summary>
    /// Re-orders one level. <see cref="NavSort.Curated"/> returns the input untouched (not a copy)
    /// so the common case allocates nothing.
    /// </summary>
    /// <remarks>
    /// Sections always precede leaf articles in the non-curated orders: a level that mixes the two
    /// otherwise interleaves folders and pages, which reads as noise. Ordering is stable, so nodes
    /// that tie fall back to their curated positions.
    /// </remarks>
    public static IReadOnlyList<NavChild> Sort(IReadOnlyList<NavChild> nodes, NavSort sort)
    {
        if (sort == NavSort.Curated || nodes.Count < 2)
        {
            return nodes;
        }

        IOrderedEnumerable<NavChild> ordered = nodes.OrderByDescending(n => n.IsSection);

        return sort switch
        {
            NavSort.Count => ordered.ThenByDescending(n => n.ArticleCount ?? 0).ToList(),

            // Nodes with no known date sink to the bottom rather than sorting as "the epoch",
            // which would put undated content above genuinely old content.
            NavSort.Recent => ordered
                .ThenByDescending(n => n.LatestArticleUtc ?? n.Date ?? DateTimeOffset.MinValue)
                .ToList(),

            NavSort.Alpha => ordered.ThenBy(n => n.Text, StringComparer.OrdinalIgnoreCase).ToList(),

            _ => nodes,
        };
    }

    /// <summary>
    /// Splits a level into the reader's pinned nodes (in the curated order they appear, so pinning
    /// never scrambles the group) and the rest, with hidden nodes dropped from the rest entirely.
    /// A pinned node is always shown even if it is also marked hidden.
    /// </summary>
    public static (List<NavChild> Pinned, List<NavChild> Others) Partition(
        IReadOnlyList<NavChild> nodes, PreferencesState prefs)
    {
        List<NavChild> pinned = [];
        List<NavChild> others = [];

        foreach (NavChild n in nodes)
        {
            if (prefs.IsPinned(n.Text))
            {
                pinned.Add(n);
            }
            else if (!prefs.IsHidden(n.Text))
            {
                others.Add(n);
            }
        }

        return (pinned, others);
    }

    /// <summary>
    /// Splits a query into search tokens. Whitespace-separated; empty tokens are dropped.
    /// </summary>
    public static string[] Tokenize(string? query) =>
        (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Matches an article when <em>every</em> token appears somewhere in its title or breadcrumb.
    /// <para>
    /// AND-of-tokens rather than one contiguous substring: readers type the words they remember, not
    /// the exact title, so "copilot agent" must find "Agents in GitHub Copilot" — which a substring
    /// match cannot do.
    /// </para>
    /// </summary>
    public static bool Matches(NavLeaf leaf, string[] tokens)
    {
        foreach (string t in tokens)
        {
            if (leaf.Text.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                leaf.Path.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the non-overlapping, ascending ranges of <paramref name="text"/> covered by any token,
    /// so a renderer can wrap each one in a highlight without emitting nested or duplicate marks.
    /// </summary>
    public static List<(int Start, int Length)> HighlightRanges(string text, string[] tokens)
    {
        List<(int Start, int Length)> hits = [];
        if (string.IsNullOrEmpty(text))
        {
            return hits;
        }

        foreach (string t in tokens)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                int i = text.IndexOf(t, pos, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                {
                    break;
                }

                hits.Add((i, t.Length));
                pos = i + t.Length;
            }
        }

        if (hits.Count == 0)
        {
            return hits;
        }

        // Tokens can overlap ("azure" and "ure"); merge into disjoint ranges before rendering.
        hits.Sort((a, b) => a.Start.CompareTo(b.Start));

        List<(int Start, int Length)> merged = [hits[0]];
        for (int i = 1; i < hits.Count; i++)
        {
            (int start, int length) = merged[^1];
            int end = start + length;

            if (hits[i].Start <= end)
            {
                int newEnd = Math.Max(end, hits[i].Start + hits[i].Length);
                merged[^1] = (start, newEnd - start);
            }
            else
            {
                merged.Add(hits[i]);
            }
        }

        return merged;
    }
}
