using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders media/attachment references (audio, video, file attachments) in chat messages.
/// </summary>
public class MediaRenderer : IContentBlockRenderer
{
    public string RendererName => "Media";
    public int Priority => 500;

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is ParagraphBlock para && ContainsMediaLink(para);

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (markdownNode is not ParagraphBlock para)
            return Task.CompletedTask;

        var linkInline = para.Inline?.Descendants<LinkInline>()
            .FirstOrDefault(l => !l.IsImage && IsMediaUrl(l.Url));

        if (linkInline is null)
            return Task.CompletedTask;

        var url = linkInline.Url ?? string.Empty;
        var displayText = linkInline.FirstChild?.ToString() ?? System.IO.Path.GetFileName(url);
        var extension = System.IO.Path.GetExtension(url)?.ToLowerInvariant() ?? string.Empty;

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

        var typeLabel = string.IsNullOrEmpty(extension)
            ? "File"
            : extension.TrimStart('.').ToUpperInvariant() + " file";

        textPanel.Children.Add(new TextBlock
        {
            Text = typeLabel,
            FontSize = 10,
            Foreground = System.Windows.Media.Brushes.Gray
        });

        stackPanel.Children.Add(textPanel);
        border.Child = stackPanel;
        container.Child = border;
        targetDocument.Blocks.Add(container);

        return Task.CompletedTask;
    }

    private static bool ContainsMediaLink(ParagraphBlock para)
    {
        var links = para.Inline?.Descendants<LinkInline>();
        return links?.Any(l => !l.IsImage && IsMediaUrl(l.Url)) == true;
    }

    private static bool IsMediaUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        var ext = System.IO.Path.GetExtension(url)?.ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a"
            or ".mp4" or ".avi" or ".mov" or ".mkv" or ".webm"
            or ".pdf" or ".zip" or ".rar" or ".7z" or ".tar" or ".gz"
            or ".csv" or ".xlsx" or ".xls" or ".doc" or ".docx";
    }
}
