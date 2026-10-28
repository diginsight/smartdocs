namespace Diginsight.SmartDocs.Web.Shared.Rendering;

/// <summary>Renders Markdown source into HTML plus a page title.</summary>
public interface IMarkdownRenderer
{
    /// <param name="markdown">The Markdown source.</param>
    /// <param name="contentDir">
    /// The directory of the source file (e.g. <c>01.00-news/foo</c>), used to resolve
    /// relative image/link URLs. Empty for root-level content.
    /// </param>
    RenderedPage Render(string markdown, string contentDir);
}

/// <summary>The HTML body, title, table of contents, and word count produced from a Markdown document.</summary>
public sealed record RenderedPage(string Html, string Title, IReadOnlyList<TocEntry> Toc, int WordCount);

public interface IRenderedPageResolver
{
    Task<RenderedPageResolution> ResolveRenderedAsync(
        string? routePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of resolving a route to a rendered page.
/// </summary>
/// <param name="ContentKey">
/// The content key of the file that was rendered, when the server rendered it. A route can be backed
/// by several candidate files, so authorization checks this key — the file actually rendered —
/// rather than re-deriving one from the route. Null in the browser, which never sees content keys.
/// </param>
/// <param name="IsHidden">
/// True when the route resolved to a file the reader may not see. Handled and without a page, like
/// <see cref="Nothing"/>, so every caller answers it exactly as a missing page — but distinct from it,
/// because the server must not fall through to another candidate file for a hidden one.
/// </param>
public sealed record RenderedPageResolution(bool Handled, RenderedPage? Page, string? ContentKey = null, bool IsHidden = false)
{
    public static RenderedPageResolution Unhandled { get; } = new(false, null);

    public static RenderedPageResolution Nothing { get; } = new(true, null);

    /// <summary>The route renders a page the reader may not see: answered as <see cref="Nothing"/> is.</summary>
    public static RenderedPageResolution Hidden { get; } = new(true, null, null, true);

    public static RenderedPageResolution Found(RenderedPage page, string? contentKey = null) => new(true, page, contentKey);
}

/// <summary>A single heading in the on-page table of contents.</summary>
public sealed record TocEntry(int Level, string Text, string Id);
