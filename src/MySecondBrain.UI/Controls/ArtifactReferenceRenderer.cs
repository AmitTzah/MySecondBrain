using System.Windows;
using System.IO;
using System.Windows.Documents;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders artifact file references in chat messages as clickable links that
/// navigate the <see cref="ArtifactsWebView2Host"/> panel.
/// Detects <c>present_files</c> tool call results in the Markdig AST and
/// produces WPF <see cref="Hyperlink"/> elements that raise the
/// <see cref="ArtifactNavigationRequested"/> event on click.
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

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is LinkInline link
        && link.Url is not null
        && IsArtifactReference(link);

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (markdownNode is not LinkInline link || link.Url is null)
            return Task.CompletedTask;

        var filePath = ResolveArtifactPath(link.Url, context);
        if (filePath is null)
            return Task.CompletedTask;

        var displayText = link.FirstChild?.ToString() ?? Path.GetFileName(link.Url);
        RenderArtifactLink(displayText, filePath, targetDocument);

        return Task.CompletedTask;
    }

    private static bool IsArtifactReference(LinkInline link)
    {
        var url = link.Url;
        if (string.IsNullOrEmpty(url))
            return false;

        // Artifact references have a distinctive scheme or path prefix
        return url.StartsWith("artifact://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("artifacts://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("./artifacts/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("/artifacts/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveArtifactPath(string url, RenderContext context)
    {
        // Strip URI scheme prefix to get the relative file path
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

        // Resolve against the chat's artifact directory from context
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

        // Icon prefix
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
