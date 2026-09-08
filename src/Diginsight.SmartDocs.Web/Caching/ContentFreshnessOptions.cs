namespace Diginsight.SmartDocs.Web.Caching;

/// <summary>
/// How stale a cached answer is allowed to be when nobody has told the site that content changed.
/// <para>
/// The site learns about new content through <c>POST /_nav/invalidate</c>, which the publish
/// workflow issues after uploading. That call is a single HTTP request and can be lost — the host
/// has no watcher on the content store and no periodic rescan, so without a bound the previous
/// answer would survive until <c>Diginsight:SmartCache</c>'s week-long expirations retired it.
/// These values are that bound: the ceiling on how long a missed notification can stay invisible.
/// </para>
/// <para>
/// They map onto <c>SmartCacheOperationOptions.MaxAge</c>, which is a freshness tolerance applied
/// when the entry is <em>read</em>, not a time-to-live applied when it is written. A short value
/// therefore costs a re-read of the origin only for entries somebody actually asks for, and leaves
/// the underlying expirations alone.
/// </para>
/// </summary>
public sealed class ContentFreshnessOptions
{
    /// <summary>
    /// Tolerance for Markdown source that was found. Re-reading one article costs about 50ms, so
    /// this can be short.
    /// </summary>
    public TimeSpan Content { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Tolerance for a path that was <em>not</em> found. Deliberately shorter than
    /// <see cref="Content"/>: a stale hit shows slightly old text, whereas a stale miss hides an
    /// article that has just been published — and publishing is precisely when someone is waiting
    /// to see it. Set it to <see cref="Content"/> or higher to switch the extra lookup off.
    /// </summary>
    public TimeSpan Missing { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Tolerance for one level of the navigation tree — the children of a single folder. Building
    /// one level costs about 40ms, so this too can be short; it is what makes a newly published
    /// article appear in the menu without an invalidation call.
    /// </summary>
    public TimeSpan NavLevel { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Tolerance for the flattened index behind menu search and prev/next links. Empty by default,
    /// meaning "inherit <c>Diginsight:SmartCache:MaxAge</c>", because rebuilding it walks every
    /// article — measured at 14 to 16 seconds over 1,133 articles — and that cost is paid by
    /// whichever visitor happens to arrive first. Give it a value only where a stalled request of
    /// that length is preferable to search lagging until the next invalidation call.
    /// </summary>
    public TimeSpan? NavIndex { get; set; }
}
