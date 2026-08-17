using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Diginsight.SmartDocs.Web.Shared.Rendering;

/// <summary>
/// In-process Markdown renderer (Markdig). Identical output whether it runs on the server
/// during prerender or inside the WASM client during navigation. Relative image/link URLs
/// are resolved against the source file's directory: images point at the raw-content
/// endpoint, and relative Markdown links point at app routes.
/// </summary>
public sealed class MarkdigMarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers() // stable heading ids so the on-page TOC can link to them
        .UseYamlFrontMatter() // ignore the top Quarto/YAML metadata block instead of rendering it
        .UseMermaid() // ```mermaid fenced blocks → <pre class="mermaid"> for client-side rendering
        .Build();

    private static readonly Regex WordCountRx = new(@"(?m)^\s*word_count\s*:\s*~?(\d+)", RegexOptions.Compiled);

    public RenderedPage Render(string markdown, string contentDir)
    {
        markdown ??= string.Empty;

        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);
        RewriteRelativeUrls(document, contentDir ?? string.Empty);
        IReadOnlyList<TocEntry> toc = BuildToc(document);

        // Prefer the author-declared word_count from article_metadata; fall back to computed.
        int wordCount = ParseWordCount(markdown) ?? CountWords(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        return new RenderedPage(writer.ToString(), ExtractTitle(markdown), toc, wordCount);
    }

    private static int? ParseWordCount(string markdown)
    {
        Match m = WordCountRx.Match(markdown);
        return m.Success && int.TryParse(m.Groups[1].Value, out int wc) ? wc : null;
    }

    private static int CountWords(MarkdownDocument document)
    {
        int count = 0;
        foreach (MarkdownObject node in document.Descendants())
        {
            if (node is LiteralInline literal)
            {
                ReadOnlySpan<char> span = literal.Content.ToString().AsSpan();
                bool inWord = false;
                foreach (char c in span)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        if (!inWord) { count++; inWord = true; }
                    }
                    else
                    {
                        inWord = false;
                    }
                }
            }
            else if (node is CodeInline code && code.Content.Length > 0)
            {
                count++; // treat inline code as one "word"
            }
        }

        return count;
    }

    private static IReadOnlyList<TocEntry> BuildToc(MarkdownDocument document)
    {
        var toc = new List<TocEntry>();
        foreach (HeadingBlock heading in document.Descendants<HeadingBlock>())
        {
            if (heading.Level is < 2 or > 3)
            {
                continue;
            }

            string? id = heading.GetAttributes().Id;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            toc.Add(new TocEntry(heading.Level, InlineText(heading.Inline), id));
        }

        return toc;
    }

    private static string InlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (MarkdownObject node in inline.Descendants())
        {
            switch (node)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void RewriteRelativeUrls(MarkdownDocument document, string contentDir)
    {
        foreach (MarkdownObject node in document.Descendants())
        {
            if (node is LinkInline link && !string.IsNullOrEmpty(link.Url) && IsRelative(link.Url))
            {
                link.Url = Rewrite(link.Url!, contentDir, link.IsImage);
            }
        }
    }

    private static string Rewrite(string url, string contentDir, bool isImage)
    {
        int hashIndex = url.IndexOf('#');
        string fragment = hashIndex >= 0 ? url[hashIndex..] : string.Empty;
        string path = hashIndex >= 0 ? url[..hashIndex] : url;
        string resolved = ResolvePath(contentDir, path);

        // Images (and other non-Markdown assets) are served as bytes by the content endpoint.
        if (isImage)
        {
            return "/_content-raw/" + resolved;
        }

        // Relative links to Markdown become in-app routes.
        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".qmd", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + StripExtension(resolved) + fragment;
        }

        // Any other relative asset (pdf, download, …) also goes through the content endpoint.
        return "/_content-raw/" + resolved + fragment;
    }

    private static bool IsRelative(string url)
    {
        return !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("//", StringComparison.Ordinal)
            || url.StartsWith("/", StringComparison.Ordinal)
            || url.StartsWith("#", StringComparison.Ordinal)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolvePath(string baseDir, string relative)
    {
        var segments = new List<string>();
        foreach (string segment in (baseDir + "/" + relative).Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    private static string StripExtension(string path)
    {
        int dot = path.LastIndexOf('.');
        int slash = path.LastIndexOf('/');
        return dot > slash ? path[..dot] : path;
    }

    private static string ExtractTitle(string markdown)
    {
        foreach (string line in markdown.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return "Diginsight SmartDocs";
    }
}
