using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Diginsight.SmartDocs.Web.Shared.Rendering;

/// <summary>
/// Renders <c>```mermaid</c> fenced blocks as <c>&lt;pre class="mermaid"&gt;</c> elements holding the
/// raw diagram source, so the client-side Mermaid script can turn them into SVG. Without this, Markdig's
/// default output (<c>&lt;pre&gt;&lt;code class="language-mermaid"&gt;</c>) shows the diagram source as text.
/// </summary>
public sealed class MermaidCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
    private readonly CodeBlockRenderer _defaultRenderer;

    public MermaidCodeBlockRenderer(CodeBlockRenderer? defaultRenderer)
    {
        _defaultRenderer = defaultRenderer ?? new CodeBlockRenderer();
    }

    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is FencedCodeBlock fenced && IsMermaid(fenced.Info))
        {
            renderer.Write("<pre class=\"mermaid\">");
            renderer.WriteEscape(GetText(fenced));
            renderer.Write("</pre>");
            renderer.EnsureLine();
            return;
        }

        _defaultRenderer.Write(renderer, obj);
    }

    private static bool IsMermaid(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return false;
        }

        string lang = info.Trim();
        int space = lang.IndexOfAny([' ', '\t']);
        if (space > 0)
        {
            lang = lang[..space];
        }

        return lang.Equals("mermaid", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetText(LeafBlock block)
    {
        var builder = new System.Text.StringBuilder();
        Markdig.Helpers.StringLine[] lines = block.Lines.Lines;
        int count = block.Lines.Count;
        for (int i = 0; i < count; i++)
        {
            builder.Append(lines[i].Slice.ToString());
            builder.Append('\n');
        }

        return builder.ToString();
    }
}

/// <summary>Markdig extension that swaps in <see cref="MermaidCodeBlockRenderer"/> for code blocks.</summary>
public sealed class MermaidExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, Markdig.Renderers.IMarkdownRenderer renderer)
    {
        if (renderer is not HtmlRenderer html)
        {
            return;
        }

        CodeBlockRenderer? existing = html.ObjectRenderers.FindExact<CodeBlockRenderer>();
        if (existing is not null)
        {
            html.ObjectRenderers.Remove(existing);
        }

        html.ObjectRenderers.AddIfNotAlready(new MermaidCodeBlockRenderer(existing));
    }
}

/// <summary>Pipeline-builder sugar: <c>.UseMermaid()</c>.</summary>
public static class MermaidPipelineBuilderExtensions
{
    public static MarkdownPipelineBuilder UseMermaid(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready<MermaidExtension>();
        return pipeline;
    }
}
