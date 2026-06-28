using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders media/attachment references (audio, video, file attachments) in chat messages.
/// Note: This renderer is currently bypassed during streaming — MdXaml handles
/// full Markdown rendering directly. Kept as a custom extension point for
/// non-standard content blocks.
/// </summary>
public class MediaRenderer : IContentBlockRenderer
{
    public string RendererName => "Media";
    public int Priority => 500;

    public bool CanRender(object? markdownNode) =>
        markdownNode is not null;

    public Task RenderAsync(
        object? markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var url = markdownNode?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(url))
            return Task.CompletedTask;

        var extension = System.IO.Path.GetExtension(url)?.ToLowerInvariant() ?? string.Empty;
        var displayText = System.IO.Path.GetFileName(url);

        var container = new BlockUIContainer();

        var border = new Border
        {
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(8),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 248, 248))
        };

        var stackPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

        var icon = extension switch
        {
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" => "\U0001f3b5",
            ".mp4" or ".avi" or ".mov" or ".mkv" or ".webm" => "\U0001f3ac",
            ".pdf" => "\U0001f4c4",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "\U0001f4e6",
            ".csv" or ".xlsx" or ".xls" => "\U0001f4ca",
            _ => "\U0001f4ce"
        };

        stackPanel.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 20,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock
        {
            Text = displayText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        });

        textPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(extension)
                ? "File"
                : extension.TrimStart('.').ToUpperInvariant() + " file",
            FontSize = 10,
            Foreground = System.Windows.Media.Brushes.Gray
        });

        stackPanel.Children.Add(textPanel);
        border.Child = stackPanel;
        container.Child = border;
        targetDocument.Blocks.Add(container);

        return Task.CompletedTask;
    }
}
