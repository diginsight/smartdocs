namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>
/// Abstracts where raw content bytes (Markdown, images, css, …) come from, so the same
/// rendering pipeline runs unchanged over blob storage (server), the local repo clone
/// (developer machine), or an HTTP endpoint (WASM client).
/// </summary>
public interface IContentSource
{
    /// <summary>Returns the bytes for a content key, or <c>null</c> when it does not exist.</summary>
    Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default);
}

/// <summary>Raw content fetched from an <see cref="IContentSource"/>.</summary>
public sealed record ContentResult(byte[] Bytes, string? ContentType, string ETag);

/// <summary>
/// Implemented by content sources for which probing candidate file names is expensive. A route
/// like <c>/02.00-events</c> may be backed by <c>02.00-events.md</c>, <c>…/index.md</c>,
/// <c>…/overview.md</c> or a readme, and <see cref="Services.PageLoader"/> tries them in order.
/// Locally that costs nothing, but from the WASM client each miss is a round trip that also shows
/// up as a 404 in the browser console; a source that can resolve the whole route server-side in a
/// single call implements this and is used instead.
/// </summary>
public interface IContentPathResolver
{
    /// <summary>Resolves a route in one call. See <see cref="ContentResolution"/>.</summary>
    Task<ContentResolution> ResolveAsync(string? routePath, CancellationToken ct = default);
}

/// <summary>
/// The outcome of a one-call resolution. The distinction matters: a section folder legitimately has
/// no page of its own, and that is the most common click in the menu — answering it as "could not
/// resolve" would send the caller back to probing every candidate and make the frequent case the
/// expensive one. <see cref="Unhandled"/> is reserved for a resolver that genuinely cannot answer,
/// such as a client served from an older deployment than the server it is talking to.
/// </summary>
public sealed record ContentResolution(bool Handled, ResolvedContent? Content)
{
    /// <summary>The resolver could not answer; the caller should probe the candidates itself.</summary>
    public static ContentResolution Unhandled { get; } = new (false, null);

    /// <summary>The resolver is certain no file backs this route.</summary>
    public static ContentResolution Nothing { get; } = new (true, null);

    public static ContentResolution Found(string key, ContentResult content) =>
        new (true, new ResolvedContent(key, content));
}

/// <summary>A route resolved to the content key that backs it, and its bytes.</summary>
public sealed record ResolvedContent(string Key, ContentResult Content);
