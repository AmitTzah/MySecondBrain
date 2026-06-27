using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Utilities;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a Markdown string into a WPF <see cref="FlowDocument"/> by parsing
/// through the shared Markdig pipeline and rendering block-level elements.
/// Used by <see cref="Views.ChatView"/> message templates to display chat content.
/// Handles: Paragraph, Heading (H1-H3), Quote, List, ThematicBreak, CodeBlock.
/// Inline formatting (bold, italic, code, links) is rendered within paragraphs.
/// </summary>
public class MarkdownToFlowDocumentConverter : IValueConverter
{
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

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = GetFontSize()
        };

        try
        {
            var ast = MarkdownHelper.Parse(markdown);
            RenderBlocks(ast, doc);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Fallback: render as plain text
            System.Diagnostics.Debug.WriteLine(
                $"[MarkdownToFlowDocumentConverter] Parse failed: {ex.Message}");
            var para = new Paragraph(new Run(markdown))
            {
                Margin = new Thickness(0, 2, 0, 2)
            };
            doc.Blocks.Add(para);
        }

        return doc;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static void RenderBlocks(MarkdownDocument document, FlowDocument target)
    {
        foreach (var block in document)
        {
            switch (block)
            {
                case ParagraphBlock para:
                    RenderParagraph(para, target);
                    break;
                case HeadingBlock heading:
                    RenderHeading(heading, target);
                    break;
                case QuoteBlock quote:
                    RenderQuote(quote, target);
                    break;
                case ListBlock list:
                    RenderListBlock(list, target);
                    break;
                case ThematicBreakBlock:
                    RenderThematicBreak(target);
                    break;
                case FencedCodeBlock code:
                    RenderCodeBlock(code, target);
                    break;
            }
        }
    }

    private static void RenderParagraph(ParagraphBlock paragraph, FlowDocument target)
    {
        var para = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
        if (paragraph.Inline is not null)
            RenderInlines(paragraph.Inline, para);
        target.Blocks.Add(para);
    }

    private static void RenderHeading(HeadingBlock heading, FlowDocument target)
    {
        var fontSize = heading.Level switch { 1 => 20.0, 2 => 17.0, _ => 15.0 };
        var fontWeight = heading.Level <= 2 ? FontWeights.Bold : FontWeights.SemiBold;
        var para = new Paragraph
        {
            FontSize = fontSize,
            FontWeight = fontWeight,
            Margin = new Thickness(0, heading.Level <= 2 ? 8 : 6, 0, 4)
        };
        if (heading.Inline is not null)
            RenderInlines(heading.Inline, para);
        target.Blocks.Add(para);
    }

    private static void RenderQuote(QuoteBlock quote, FlowDocument target)
    {
        var section = new Section
        {
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 4, 0, 4),
            Margin = new Thickness(0, 4, 0, 4)
        };
        foreach (var child in quote)
        {
            if (child is ParagraphBlock paraBlock)
            {
                var para = new Paragraph
                {
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                if (paraBlock.Inline is not null)
                    RenderInlines(paraBlock.Inline, para);
                section.Blocks.Add(para);
            }
        }
        target.Blocks.Add(section);
    }

    private static void RenderListBlock(ListBlock list, FlowDocument target)
    {
        var isOrdered = list.IsOrdered;
        var wpfList = new List
        {
            MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(20, 2, 0, 2)
        };
        foreach (var child in list)
        {
            if (child is ListItemBlock item)
            {
                var listItem = new ListItem();
                foreach (var itemChild in item)
                {
                    if (itemChild is ParagraphBlock paraBlock)
                    {
                        var para = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                        if (paraBlock.Inline is not null)
                            RenderInlines(paraBlock.Inline, para);
                        listItem.Blocks.Add(para);
                    }
                }
                wpfList.ListItems.Add(listItem);
            }
        }
        target.Blocks.Add(wpfList);
    }

    private static void RenderThematicBreak(FlowDocument target)
    {
        target.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0, 6, 0, 6),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderThickness = new Thickness(0, 1, 0, 0)
        });
    }

    private static void RenderCodeBlock(FencedCodeBlock code, FlowDocument target)
    {
        var codeText = string.Join("\n", code.Lines.Lines.Select(l => l.ToString()));
        if (string.IsNullOrEmpty(codeText))
            return;

        var para = new Paragraph
        {
            FontFamily = new WpfFontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 12,
            Background = new SolidColorBrush(WpfColor.FromRgb(0xF0, 0xF0, 0xF0)),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 4, 0, 4)
        };
        para.Inlines.Add(new Run(codeText));
        target.Blocks.Add(para);
    }

    private static void RenderInlines(Markdig.Syntax.Inlines.ContainerInline container, Paragraph targetPara)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case Markdig.Syntax.Inlines.LiteralInline lit:
                    targetPara.Inlines.Add(new Run(lit.Content.ToString()));
                    break;
                case Markdig.Syntax.Inlines.LineBreakInline:
                    targetPara.Inlines.Add(new LineBreak());
                    break;
                case Markdig.Syntax.Inlines.EmphasisInline emphasis:
                    RenderEmphasis(emphasis, targetPara);
                    break;
                case Markdig.Syntax.Inlines.CodeInline code:
                    targetPara.Inlines.Add(new Run(code.Content)
                    {
                        FontFamily = new WpfFontFamily("Cascadia Code, Consolas, monospace"),
                        Background = new SolidColorBrush(WpfColor.FromRgb(0xE8, 0xE8, 0xE8)),
                        FontSize = 12
                    });
                    break;
                case Markdig.Syntax.Inlines.LinkInline link when link.Url is not null:
                    var displayText = link.FirstChild?.ToString() ?? link.Url;
                    var hyperlink = new Hyperlink(new Run(displayText))
                    {
                        NavigateUri = Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ? uri : null,
                        ToolTip = link.Url
                    };
                    targetPara.Inlines.Add(hyperlink);
                    break;
                case Markdig.Syntax.Inlines.ContainerInline containerInline:
                    // Recursively render all inline children (may include emphasis, code, links, etc.)
                    foreach (var child in containerInline)
                    {
                        switch (child)
                        {
                            case Markdig.Syntax.Inlines.LiteralInline nestedLit:
                                targetPara.Inlines.Add(new Run(nestedLit.Content.ToString()));
                                break;
                            case Markdig.Syntax.Inlines.LineBreakInline:
                                targetPara.Inlines.Add(new LineBreak());
                                break;
                            case Markdig.Syntax.Inlines.EmphasisInline nestedEm:
                                RenderEmphasis(nestedEm, targetPara);
                                break;
                            case Markdig.Syntax.Inlines.CodeInline nestedCode:
                                targetPara.Inlines.Add(new Run(nestedCode.Content)
                                {
                                    FontFamily = new WpfFontFamily("Cascadia Code, Consolas, monospace"),
                                    Background = new SolidColorBrush(WpfColor.FromRgb(0xE8, 0xE8, 0xE8)),
                                    FontSize = 12
                                });
                                break;
                            case Markdig.Syntax.Inlines.LinkInline nestedLink when nestedLink.Url is not null:
                                var nestedDisplay = nestedLink.FirstChild?.ToString() ?? nestedLink.Url;
                                var nestedHl = new Hyperlink(new Run(nestedDisplay))
                                {
                                    NavigateUri = Uri.TryCreate(nestedLink.Url, UriKind.Absolute, out var nestedUri) ? nestedUri : null,
                                    ToolTip = nestedLink.Url
                                };
                                targetPara.Inlines.Add(nestedHl);
                                break;
                        }
                    }
                    break;
            }
        }
    }

    private static void RenderEmphasis(Markdig.Syntax.Inlines.EmphasisInline emphasis, Paragraph targetPara)
    {
        var span = new Span();
        if (emphasis.DelimiterCount >= 2)
            span.FontWeight = FontWeights.Bold;
        if (emphasis.DelimiterCount is 1 or 3)
            span.FontStyle = FontStyles.Italic;

        foreach (var child in emphasis)
        {
            if (child is Markdig.Syntax.Inlines.LiteralInline lit)
                span.Inlines.Add(new Run(lit.Content.ToString()));
            else if (child is Markdig.Syntax.Inlines.CodeInline code)
                span.Inlines.Add(new Run(code.Content)
                {
                    FontFamily = new WpfFontFamily("Cascadia Code, Consolas, monospace"),
                    FontSize = 12
                });
        }
        targetPara.Inlines.Add(span);
    }
}
