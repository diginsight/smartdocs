namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// One node's best-known recursive article count plus the newest article seen in its subtree
/// (with that article's author, for the footer's "Last Change" line) and how much of the subtree
/// the value actually observed.
/// </summary>
public readonly record struct FolderArticleStats(
    int Count,
    DateTimeOffset? LatestUtc,
    string? LatestAuthor,
    Coverage Coverage = Coverage.Complete);

/// <summary>
/// Per-circuit status-bar aggregator for the footer article counter.
/// <para>
/// Values are <em>server-authoritative</em>: every root's recursive count arrives either on the
/// nav level fetch or on a metadata push. The client never derives a count from the nodes it has
/// rendered, so a partially loaded tree can never produce a partial total.
/// </para>
/// <para>
/// The total is the sum of the latest value recorded per root, so re-recording a root just replaces
/// its previous contribution (idempotent, never double counts). Its coverage is the weakest root's:
/// one unknown root makes the whole total a lower bound.
/// </para>
/// </summary>
public sealed class NavStats
{
    private readonly Dictionary<string, (string Label, FolderArticleStats Stats)> _roots = new(StringComparer.OrdinalIgnoreCase);
    private bool _refreshPending;

    // The footer's section line resolves by priority:
    //   1. the item currently hovered / keyboard-focused in the sidebar ("marked for selection"), then
    //   2. the containing section of the selected (navigated) article — the persistent baseline.
    // Keeping the two tiers separate stops the selected article (whose OnParametersSetAsync re-fires on
    // every re-render) from clobbering the transient hover highlight.
    private string? _selKey, _selLabel;    // selected article's section (baseline)
    private int? _selCount;
    private Coverage _selCoverage;
    private string? _hovKey, _hovLabel;    // hovered / focused item's section (override)
    private int? _hovCount;
    private Coverage _hovCoverage;

    // The server's own value for the site root. It supersedes the sum of the root sections, which
    // omits standalone top-level articles and is therefore only ever a lower bound.
    private int? _siteCount;
    private Coverage _siteCoverage = Coverage.None;
    private int _rootsSum;
    private bool _anyRootKnown;

    /// <summary>Best-known site-wide article total.</summary>
    public int TotalArticles => _siteCount ?? _rootsSum;

    /// <summary>Coverage of <see cref="TotalArticles"/> — only Complete when it is a true total.</summary>
    public Coverage TotalCoverage =>
        _siteCount is not null ? _siteCoverage
        : _anyRootKnown ? Coverage.Partial
        : Coverage.None;

    /// <summary>
    /// Records the server's authoritative site-root aggregate. Once this arrives the footer stops
    /// deriving the total from the root sections.
    /// </summary>
    public void SetTotal(FolderArticleStats value)
    {
        if (_siteCount == value.Count && _siteCoverage == value.Coverage && LatestUtc == value.LatestUtc)
        {
            return;
        }

        _siteCount = value.Count;
        _siteCoverage = value.Coverage;
        if (value.LatestUtc is { } l && (LatestUtc is null || l > LatestUtc))
        {
            LatestUtc = l;
            LatestAuthor = value.LatestAuthor;
        }

        HasData = true;
        ScheduleRefresh();
    }

    /// <summary>Newest article date across all roots (UTC).</summary>
    public DateTimeOffset? LatestUtc { get; private set; }

    /// <summary>Author of the newest article, when known (only for articles already discovered).</summary>
    public string? LatestAuthor { get; private set; }

    /// <summary>True once at least one root has reported, so the footer can stop showing "…".</summary>
    public bool HasData { get; private set; }

    /// <summary>
    /// Label of the section shown in the footer: the hovered/focused item's section when one is
    /// highlighted, otherwise the selected article's section. Null when neither is known (root).
    /// </summary>
    public string? ActiveSectionLabel => _hovKey is not null ? _hovLabel : _selLabel;

    /// <summary>Article count matching <see cref="ActiveSectionLabel"/>, or null if unknown/root.</summary>
    public int? ActiveSectionCount => _hovKey is not null ? _hovCount : _selCount;

    /// <summary>Coverage of <see cref="ActiveSectionCount"/>.</summary>
    public Coverage ActiveSectionCoverage => _hovKey is not null ? _hovCoverage : _selCoverage;

    /// <summary>Raised (debounced) after the aggregate or active section changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Records (or overwrites) a root node's recursive subtree count and recomputes the aggregate.
    /// A single <see cref="Changed"/> event fires after the burst settles (50 ms quiet window).
    /// </summary>
    /// <remarks>
    /// No lock needed: NavStats is scoped (one per circuit/tab) and all callers run on the
    /// circuit's synchronization context (single-threaded in WASM, serialized in Server).
    /// </remarks>
    public void SetRoot(string key, string label, FolderArticleStats value)
    {
        _roots[key ?? string.Empty] = (label, value);

        int total = 0;
        DateTimeOffset? latest = null;
        string? author = null;
        int known = 0;
        foreach (var (_, stats) in _roots.Values)
        {
            if (stats.Coverage != Coverage.None)
            {
                known++;
                total += stats.Count;      // an unknown root is absent from the sum, never a zero
            }

            if (stats.LatestUtc is { } l && (latest is null || l > latest))
            {
                latest = l;
                author = stats.LatestAuthor;
            }
        }

        bool changed = !HasData || total != _rootsSum || latest != LatestUtc || author != LatestAuthor
                       || (known > 0) != _anyRootKnown;

        _rootsSum = total;
        _anyRootKnown = known > 0;
        LatestUtc = latest ?? LatestUtc;
        LatestAuthor = author ?? LatestAuthor;
        HasData = true;

        // Keep whichever tier(s) reference this root in sync with its freshly reported count/label.
        if (key is not null)
        {
            if (string.Equals(key, _hovKey, StringComparison.OrdinalIgnoreCase))
            {
                changed |= _hovLabel != label || _hovCount != value.Count || _hovCoverage != value.Coverage;
                _hovLabel = label;
                _hovCount = value.Count;
                _hovCoverage = value.Coverage;
            }
            if (string.Equals(key, _selKey, StringComparison.OrdinalIgnoreCase))
            {
                changed |= _selLabel != label || _selCount != value.Count || _selCoverage != value.Coverage;
                _selLabel = label;
                _selCount = value.Count;
                _selCoverage = value.Coverage;
            }
        }

        if (changed) ScheduleRefresh();
    }

    /// <summary>
    /// Records the selected (navigated) article's containing section — the persistent baseline the
    /// footer shows whenever nothing is hovered/focused. Idempotent, so the active article re-asserting
    /// it on every re-render is a no-op and never fires a redundant refresh.
    /// </summary>
    public void SetSelectedSection(string? key, string? label, int? count, Coverage coverage = Coverage.Complete)
    {
        if (string.Equals(_selKey, key, StringComparison.OrdinalIgnoreCase)
            && _selLabel == label && _selCount == count && _selCoverage == coverage)
        {
            return;
        }

        // The baseline is only visible when no override is active, so only then does it warrant a redraw.
        bool visible = _hovKey is null;
        _selKey = key;
        _selLabel = label;
        _selCount = count;
        _selCoverage = coverage;
        if (visible) Changed?.Invoke();
    }

    /// <summary>
    /// Records the sidebar item currently hovered or keyboard-focused ("marked for selection") — the
    /// highest-priority override. Sections report themselves (including the folder itself); articles
    /// report their containing section.
    /// </summary>
    public void SetHoverSection(string? key, string? label, int? count, Coverage coverage = Coverage.Complete)
    {
        if (key is null) return;
        if (string.Equals(_hovKey, key, StringComparison.OrdinalIgnoreCase)
            && _hovLabel == label && _hovCount == count && _hovCoverage == coverage)
        {
            return;
        }

        _hovKey = key;
        _hovLabel = label;
        _hovCount = count;
        _hovCoverage = coverage;
        Changed?.Invoke();
    }

    /// <summary>
    /// Clears the hover/focus override when the pointer or focus leaves an item, so the footer reverts
    /// to the selected article's section. Guarded by key so a stale leave (fired after a newer item was
    /// already entered) is ignored.
    /// </summary>
    public void ClearHoverSection(string? key)
    {
        if (_hovKey is null) return;
        if (key is not null && !string.Equals(_hovKey, key, StringComparison.OrdinalIgnoreCase)) return;

        _hovKey = null;
        _hovLabel = null;
        _hovCount = null;
        _hovCoverage = Coverage.None;
        Changed?.Invoke();
    }

    // Coalesce a burst of SetRoot calls into one Changed event. The flag prevents multiple
    // in-flight delays; the subscriber always reads the latest aggregate when it fires.
    private void ScheduleRefresh()
    {
        if (_refreshPending) return;
        _refreshPending = true;
        _ = RaiseAfterSettleAsync();
    }

    private async Task RaiseAfterSettleAsync()
    {
        await Task.Delay(50).ConfigureAwait(false);
        _refreshPending = false;
        Changed?.Invoke();
    }
}
