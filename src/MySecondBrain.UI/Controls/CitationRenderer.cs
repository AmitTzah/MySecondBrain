using System.Windows.Documents;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders citation/footnote references in chat messages.
/// Note: This renderer is currently bypassed during streaming — MdXaml handles
/// full Markdown rendering directly. Kept as a custom extension point for
/// non-standard content blocks.
/// </summary>
public class CitationRenderer : IContentBlockRenderer
{
    public string RendererName => "Citation";
    public int Priority => 350;

    public bool CanRender(object? markdownNode) =>
        markdownNode is not null;

    public Task RenderAsync(
        object? markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var text = markdownNode?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(text))
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 4)
            };
            paragraph.Inlines.Add(new Run(text));
            targetDocument.Blocks.Add(paragraph);
        }

        return Task.CompletedTask;
    }
}
