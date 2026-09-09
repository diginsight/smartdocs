using System.Text.Json;

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

/// <summary>The HTML body and normalized article metadata produced from a Markdown document.</summary>
public sealed record RenderedPage(string Html, PageMetadata Metadata)
{
    public string Title => Metadata.Title;
    public IReadOnlyList<TocEntry> Toc => Metadata.Toc;
    public int WordCount => Metadata.WordCount;
}

/// <summary>Normalized article metadata returned with rendered HTML.</summary>
public sealed record PageMetadata(
    string Title,
    string? Author,
    string? Date,
    IReadOnlyList<string> Categories,
    string? Description,
    IReadOnlyList<TocEntry> Toc,
    int WordCount,
    IReadOnlyDictionary<string, JsonElement> Extensions);

/// <summary>A single heading in the on-page table of contents.</summary>
public sealed record TocEntry(int Level, string Text, string Id);
