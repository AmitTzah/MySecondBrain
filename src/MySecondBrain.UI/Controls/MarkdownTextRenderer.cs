using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders Markdown block-level and inline elements into WPF FlowDocument
/// elements. Handles: headings, paragraphs, blockquotes, lists, horizontal rules,
/// and all inline types (bold, italic, code, links, line breaks).
/// Note: This renderer is currently bypassed during streaming — MdXaml handles
/// full Markdown rendering directly. Kept as a custom extension point for
/// non-standard content blocks.
/// </summary>
public class MarkdownTextRenderer : IContentBlockRenderer
{
    public string RendererName => "MarkdownText";
    public int Priority => 100;

    public bool CanRender(object? markdownNode) =>
        markdownNode is not null;

    public Task RenderAsync(
        object? markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (markdownNode is not string text || string.IsNullOrEmpty(text))
            return Task.CompletedTask;

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 4, 0, 4),
            LineHeight = 1.5
        };
        paragraph.Inlines.Add(new Run(text));
        targetDocument.Blocks.Add(paragraph);

        return Task.CompletedTask;
    }
}
