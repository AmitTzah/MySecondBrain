using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders tool call results as styled card elements.
/// </summary>
public class ToolCallRenderer : IContentBlockRenderer
{
    public string RendererName => "ToolCall";
    public int Priority => 700;

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is not null && markdownNode.GetType().Name == "ToolCallBlock";

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var toolName = ExtractToolName(markdownNode);
        var parameters = ExtractParameters(markdownNode);
        var result = ExtractResult(markdownNode);

        var blockContainer = new BlockUIContainer();

        var border = new Border
        {
            Margin = new Thickness(0, 6, 0, 6),
            Padding = new Thickness(10),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 255))
        };

        var stackPanel = new StackPanel();

        var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"\U0001f527 {toolName}",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 120))
        });
        stackPanel.Children.Add(headerPanel);

        if (!string.IsNullOrEmpty(parameters))
        {
            var paramsBlock = new TextBlock
            {
                Text = parameters,
                FontSize = 11,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 4),
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            stackPanel.Children.Add(paramsBlock);
        }

        if (!string.IsNullOrEmpty(result))
        {
            var resultExpander = new Expander
            {
                Header = "Result",
                IsExpanded = false,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var resultBlock = new TextBlock
            {
                Text = result,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(4)
            };

            resultExpander.Content = resultBlock;
            stackPanel.Children.Add(resultExpander);
        }

        border.Child = stackPanel;
        blockContainer.Child = border;
        targetDocument.Blocks.Add(blockContainer);

        return Task.CompletedTask;
    }

    private static string ExtractToolName(MarkdownObject node)
    {
        var firstLiteral = node.Descendants<Markdig.Syntax.Inlines.LiteralInline>().FirstOrDefault();
        var text = firstLiteral?.Content.ToString() ?? string.Empty;
        var colonIndex = text.IndexOf(':');
        return colonIndex > 0 ? text[..colonIndex].Trim() : text;
    }

    private static string ExtractParameters(MarkdownObject node)
    {
        var literals = node.Descendants<Markdig.Syntax.Inlines.LiteralInline>()
            .Select(l => l.Content.ToString());
        var fullText = string.Join("", literals);
        var colonIndex = fullText.IndexOf(':');
        if (colonIndex > 0 && colonIndex < fullText.Length - 1)
        {
            var afterColon = fullText[(colonIndex + 1)..].Trim();
            return afterColon.Length > 200 ? afterColon[..200] + "..." : afterColon;
        }
        return string.Empty;
    }

    private static string ExtractResult(MarkdownObject node)
    {
        var paras = node.Descendants<ParagraphBlock>().Skip(1).ToList();
        var resultTexts = paras
            .Select(p => string.Join("", p.Inline?.Descendants<Markdig.Syntax.Inlines.LiteralInline>()
                .Select(l => l.Content.ToString()) ?? []))
            .Where(t => !string.IsNullOrEmpty(t));
        return string.Join("\n", resultTexts);
    }
}
