using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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

    // String comparison used because ToolCallBlock is a type from an assembly
    // not directly referenced by this project. Pattern matching via `is ToolCallBlock`
    // would require adding that assembly reference.
    public bool CanRender(object? markdownNode) =>
        markdownNode is not null && markdownNode.GetType().Name == "ToolCallBlock";

    public Task RenderAsync(
        object? markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var fullText = markdownNode?.ToString() ?? string.Empty;
        var colonIndex = fullText.IndexOf(':');
        var toolName = colonIndex > 0 ? fullText[..colonIndex].Trim() : fullText;
        var parameters = colonIndex > 0 && colonIndex < fullText.Length - 1
            ? fullText[(colonIndex + 1)..].Trim()
            : string.Empty;

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

        border.Child = stackPanel;
        blockContainer.Child = border;
        targetDocument.Blocks.Add(blockContainer);

        return Task.CompletedTask;
    }
}
