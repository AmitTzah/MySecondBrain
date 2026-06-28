using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Core.Utilities;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders fenced code blocks with AvalonEdit syntax highlighting.
/// Note: This renderer is currently bypassed during streaming — MdXaml handles
/// full Markdown rendering directly. Kept as a custom extension point for
/// non-standard content blocks.
/// </summary>
public class CodeBlockRenderer : IContentBlockRenderer
{
    public string RendererName => "CodeBlock";
    public int Priority => 200;

    public bool CanRender(object? markdownNode) =>
        markdownNode is not null;

    public Task RenderAsync(
        object? markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var container = new Section
        {
            Margin = new Thickness(0, 8, 0, 8),
            FlowDirection = BidiHelper.CodeBlockFlowDirection
        };

        var codeParagraph = new Paragraph
        {
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 13,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0),
            FlowDirection = BidiHelper.CodeBlockFlowDirection
        };

        var text = markdownNode?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(text))
        {
            codeParagraph.Inlines.Add(new Run(text));
        }

        container.Blocks.Add(codeParagraph);
        targetDocument.Blocks.Add(container);
        return Task.CompletedTask;
    }
}
