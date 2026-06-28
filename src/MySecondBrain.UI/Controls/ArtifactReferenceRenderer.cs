using System.Windows;
using System.IO;
using System.Windows.Documents;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders artifact file references in chat messages as clickable links that
/// navigate the <see cref="ArtifactsWebView2Host"/> panel.
/// Note: This renderer is currently bypassed during streaming — MdXaml handles
/// full Markdown rendering directly. Kept as a custom extension point for
/// non-standard content blocks.
/// </summary>
public class ArtifactReferenceRenderer : IContentBlockRenderer
{
    /// <summary>
    /// Raised when the user clicks an artifact reference link.
    /// Subscribe from the panel hosting <see cref="ArtifactsWebView2Host"/>
    /// to call <c>NavigateToArtifact(filePath)</c>.
    /// </summary>
    public static event EventHandler<ArtifactNavigationEventArgs>? ArtifactNavigationRequested;

    public string RendererName => "ArtifactReference";
    public int Priority => 300;

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

        var filePath = ResolveArtifactPath(url, context);
        if (filePath is null)
            return Task.CompletedTask;

        var displayText = Path.GetFileName(url);
        RenderArtifactLink(displayText, filePath, targetDocument);

        return Task.CompletedTask;
    }

    private static string? ResolveArtifactPath(string url, RenderContext context)
    {
        var relativePath = url;

        if (relativePath.StartsWith("artifact://", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["artifact://".Length..];

        else if (relativePath.StartsWith("artifacts://", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["artifacts://".Length..];

        else if (relativePath.StartsWith("./artifacts/", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["./artifacts/".Length..];

        else if (relativePath.StartsWith("/artifacts/", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["/artifacts/".Length..];

        else if (relativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["artifacts/".Length..];

        var artifactDir = context.ArtifactDirectory;
        if (string.IsNullOrEmpty(artifactDir))
            return null;

        return System.IO.Path.Combine(artifactDir, relativePath);
    }

    private static void RenderArtifactLink(string displayText, string filePath, FlowDocument document)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 4, 0, 4)
        };

        paragraph.Inlines.Add(new Run("📄 ") { FontSize = 14 });

        var hyperlink = new Hyperlink(new Run(displayText))
        {
            ToolTip = $"Open artifact: {filePath}",
            Tag = filePath,
            FontWeight = FontWeights.SemiBold
        };

        hyperlink.Click += OnArtifactLinkClick;

        paragraph.Inlines.Add(hyperlink);
        document.Blocks.Add(paragraph);
    }

    private static void OnArtifactLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink hyperlink && hyperlink.Tag is string filePath)
        {
            ArtifactNavigationRequested?.Invoke(null, new ArtifactNavigationEventArgs(filePath));
        }
    }
}

/// <summary>
/// Event arguments for artifact navigation requests from chat message links.
/// </summary>
public class ArtifactNavigationEventArgs : EventArgs
{
    public string FilePath { get; }

    public ArtifactNavigationEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}
