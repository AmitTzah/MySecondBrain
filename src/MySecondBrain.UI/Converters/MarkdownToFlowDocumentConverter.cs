using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using MdXaml;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a Markdown string into a WPF <see cref="FlowDocument"/> by delegating
/// to <see cref="MdXaml.Markdown.Transform"/>. Used by <see cref="Views.ChatView"/>
/// message templates to display chat content. Supports all Markdown features
/// rendered by MdXaml: headings, bold/italic, code blocks, lists, quotes, tables,
/// links, images, and more.
/// </summary>
public class MarkdownToFlowDocumentConverter : IValueConverter
{
    private readonly Markdown _mdEngine = new();

    /// <summary>
    /// Resolves the current theme font size so rendered message text
    /// reflects user preferences set via the A⁻/A⁺ buttons.
    /// </summary>
    private static double GetFontSize()
    {
        try
        {
            var provider = (App.ServiceProvider as System.IServiceProvider)
                ?.GetService(typeof(IThemeProvider)) as IThemeProvider;
            if (provider is null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[MarkdownToFlowDocumentConverter] IThemeProvider not available — using fallback font size 14");
                return 14.0;
            }
            return provider.FontSize;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MarkdownToFlowDocumentConverter] Failed to resolve font size: {ex.Message}");
            return 14.0;
        }
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string markdown || string.IsNullOrEmpty(markdown))
            return new FlowDocument();

        try
        {
            var doc = _mdEngine.Transform(markdown);
            doc.PagePadding = new Thickness(0);
            doc.FontSize = GetFontSize();
            return doc;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Fallback: render as plain text
            System.Diagnostics.Debug.WriteLine(
                $"[MarkdownToFlowDocumentConverter] MdXaml.Transform failed: {ex.Message}");
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontSize = GetFontSize()
            };
            doc.Blocks.Add(new Paragraph(new System.Windows.Documents.Run(markdown))
            {
                Margin = new Thickness(0, 2, 0, 2)
            });
            return doc;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
