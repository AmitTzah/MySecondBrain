using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders Markdown images as WPF Image controls with click-to-enlarge.
/// Note: This renderer is currently bypassed during streaming — MdXaml handles
/// full Markdown rendering directly. Kept as a custom extension point for
/// non-standard content blocks.
/// </summary>
public class ImageRenderer : IContentBlockRenderer
{
    public string RendererName => "Image";
    public int Priority => 400;

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

        var container = new BlockUIContainer();

        var image = new System.Windows.Controls.Image
        {
            MaxWidth = Math.Min(context.AvailableWidth * 0.8, 600),
            MaxHeight = 400,
            Margin = new Thickness(0, 8, 0, 8),
            ToolTip = url,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        try
        {
            var source = new BitmapImage();
            source.BeginInit();

            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            {
                source.UriSource = absoluteUri;
            }
            else if (Uri.TryCreate(url, UriKind.Relative, out var relativeUri))
            {
                var basePath = context.ArtifactDirectory;
                if (!string.IsNullOrEmpty(basePath))
                {
                    var fullPath = System.IO.Path.Combine(basePath, url);
                    if (System.IO.File.Exists(fullPath))
                    {
                        source.UriSource = new Uri(fullPath, UriKind.Absolute);
                    }
                }
            }

            source.EndInit();
            source.Freeze();
            image.Source = source;
        }
        catch
        {
            image.Source = null;
        }

        if (image.Source is not null)
        {
            image.MouseLeftButtonUp += (_, _) =>
            {
                try
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var clickUri))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = clickUri.AbsoluteUri,
                            UseShellExecute = true
                        });
                    }
                }
                catch { }
            };
        }

        container.Child = image;
        targetDocument.Blocks.Add(container);

        return Task.CompletedTask;
    }
}
