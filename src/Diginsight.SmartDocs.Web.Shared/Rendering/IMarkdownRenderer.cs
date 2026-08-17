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

/// <summary>A single heading in the on-page table of contents.</summary>
public sealed record TocEntry(int Level, string Text, string Id);
