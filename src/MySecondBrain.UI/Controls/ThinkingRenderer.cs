using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders thinking content as a collapsible Expander block.
/// </summary>
public class ThinkingRenderer : IContentBlockRenderer
{
    public string RendererName => "Thinking";
    public int Priority => 600;

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is not null && markdownNode.GetType().Name == "ThinkingBlock";

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var content = ExtractThinkingContent(markdownNode);

        var blockContainer = new BlockUIContainer();

        var expander = new Expander
        {
            Header = context.IsStreaming
                ? "🧠 Thinking..."
                : "🧠 Thinking complete",
            IsExpanded = false,
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 210)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 150)),
            BorderThickness = new Thickness(1),
            FontSize = 12
        };

        var contentText = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            Margin = new Thickness(4)
        };

        expander.Content = contentText;
        blockContainer.Child = expander;
        targetDocument.Blocks.Add(blockContainer);

        return Task.CompletedTask;
    }

    private static string ExtractThinkingContent(MarkdownObject node)
    {
        foreach (var child in node.Descendants<ParagraphBlock>())
        {
            if (child.Inline is not null)
            {
                var text = string.Join("", child.Inline.Descendants<Markdig.Syntax.Inlines.LiteralInline>()
                    .Select(l => l.Content.ToString()));
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }

        var literals = node.Descendants<Markdig.Syntax.Inlines.LiteralInline>()
            .Select(l => l.Content.ToString());
        return string.Join("", literals);
    }
}
