using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders Markdig AST block-level and inline elements into WPF FlowDocument
/// elements. Handles: ParagraphBlock, HeadingBlock (H1-H6), QuoteBlock,
/// ListBlock/ListItemBlock, ThematicBreakBlock, and all inline types
/// (LiteralInline, EmphasisInline, CodeInline, LinkInline, LineBreakInline).
/// </summary>
public class MarkdownTextRenderer : IContentBlockRenderer
{
    public string RendererName => "MarkdownText";
    public int Priority => 100;

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is ParagraphBlock
        or HeadingBlock
        or QuoteBlock
        or ListBlock
        or ListItemBlock
        or ThematicBreakBlock;

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        switch (markdownNode)
        {
            case HeadingBlock heading:
                RenderHeading(heading, targetDocument);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(paragraph, targetDocument);
                break;
            case QuoteBlock quote:
                RenderQuote(quote, targetDocument);
                break;
            case ListBlock list:
                RenderList(list, targetDocument);
                break;
            case ListItemBlock listItem:
                RenderListItem(listItem, targetDocument);
                break;
            case ThematicBreakBlock:
                RenderThematicBreak(targetDocument);
                break;
        }

        return Task.CompletedTask;
    }

    // ─── Headings ───────────────────────────────────────────────────

    private static void RenderHeading(HeadingBlock heading, FlowDocument target)
    {
        var fontSize = heading.Level switch
        {
            1 => 24.0, 2 => 20.0, 3 => 16.0, 4 => 14.0, 5 => 12.0, 6 => 11.0, _ => 14.0
        };

        var fontWeight = heading.Level <= 2 ? FontWeights.Bold : FontWeights.SemiBold;

        var paragraph = new Paragraph
        {
            FontSize = fontSize,
            FontWeight = fontWeight,
            Margin = new Thickness(0, heading.Level <= 2 ? 10 : 8, 0, heading.Level <= 2 ? 6 : 4)
        };

        if (heading.Inline is not null)
        {
            RenderInlines(heading.Inline, paragraph);
        }

        target.Blocks.Add(paragraph);
    }

    // ─── Paragraph ──────────────────────────────────────────────────

    private static void RenderParagraph(ParagraphBlock paragraph, FlowDocument target)
    {
        var para = new Paragraph
        {
            Margin = new Thickness(0, 4, 0, 4),
            LineHeight = 1.5
        };

        if (paragraph.Inline is not null)
        {
            RenderInlines(paragraph.Inline, para);
        }

        target.Blocks.Add(para);
    }

    // ─── Blockquote ─────────────────────────────────────────────────

    private static void RenderQuote(QuoteBlock quote, FlowDocument target)
    {
        var section = new Section
        {
            BorderBrush = System.Windows.Media.Brushes.Gray,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 4, 0, 4),
            Margin = new Thickness(0, 6, 0, 6)
        };

        foreach (var child in quote)
        {
            if (child is ParagraphBlock paraBlock)
            {
                var para = new Paragraph
                {
                    Margin = new Thickness(0, 2, 0, 2),
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
                if (paraBlock.Inline is not null)
                    RenderInlines(paraBlock.Inline, para);
                section.Blocks.Add(para);
            }
            else if (child is ListBlock listBlock)
            {
                RenderList(listBlock, section);
            }
        }

        target.Blocks.Add(section);
    }

    // ─── List ───────────────────────────────────────────────────────

    private static void RenderList(ListBlock listBlock, FlowDocument target)
    {
        var isOrdered = listBlock.IsOrdered;
        var wpfList = new List
        {
            MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(20, 4, 0, 4)
        };

        foreach (var child in listBlock)
        {
            if (child is ListItemBlock item)
            {
                var listItem = new ListItem();
                RenderListItemContent(item, listItem);
                wpfList.ListItems.Add(listItem);
            }
        }

        target.Blocks.Add(wpfList);
    }

    private static void RenderList(ListBlock listBlock, Section section)
    {
        var isOrdered = listBlock.IsOrdered;
        var wpfList = new List
        {
            MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(20, 4, 0, 4)
        };

        foreach (var child in listBlock)
        {
            if (child is ListItemBlock item)
            {
                var listItem = new ListItem();
                RenderListItemContent(item, listItem);
                wpfList.ListItems.Add(listItem);
            }
        }

        section.Blocks.Add(wpfList);
    }

    private static void RenderListItem(ListItemBlock item, FlowDocument target)
    {
        var listItem = new ListItem();
        RenderListItemContent(item, listItem);
        var wrapper = new List { MarkerStyle = TextMarkerStyle.None, Margin = new Thickness(20, 2, 0, 2) };
        wrapper.ListItems.Add(listItem);
        target.Blocks.Add(wrapper);
    }

    private static void RenderListItemContent(ListItemBlock item, ListItem listItem)
    {
        foreach (var child in item)
        {
            if (child is ParagraphBlock para)
            {
                var paraBlock = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                if (para.Inline is not null)
                    RenderInlines(para.Inline, paraBlock);
                listItem.Blocks.Add(paraBlock);
            }
            else if (child is ListBlock nestedList)
            {
                var nested = new List
                {
                    MarkerStyle = nestedList.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Circle,
                    Margin = new Thickness(20, 2, 0, 2)
                };
                foreach (var nestedChild in nestedList)
                {
                    if (nestedChild is ListItemBlock nestedItem)
                    {
                        var nestedListItem = new ListItem();
                        RenderListItemContent(nestedItem, nestedListItem);
                        nested.ListItems.Add(nestedListItem);
                    }
                }
                listItem.Blocks.Add(nested);
            }
        }
    }

    // ─── Thematic Break (Horizontal Rule) ──────────────────────────

    private static void RenderThematicBreak(FlowDocument target)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 8, 0, 8),
            Padding = new Thickness(0),
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        target.Blocks.Add(paragraph);
    }

    // ─── Inline Rendering ──────────────────────────────────────────

    private static void RenderInlines(ContainerInline container, Paragraph targetPara)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    targetPara.Inlines.Add(new Run(lit.Content.ToString()));
                    break;

                case LineBreakInline:
                    targetPara.Inlines.Add(new LineBreak());
                    break;

                case EmphasisInline emphasis:
                    RenderEmphasis(emphasis, targetPara);
                    break;

                case CodeInline code:
                    targetPara.Inlines.Add(new Run(code.Content)
                    {
                        FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace"),
                        Background = System.Windows.Media.Brushes.LightGray,
                        FontSize = 12
                    });
                    break;

                case LinkInline link when link.Url is not null:
                    RenderLink(link, targetPara);
                    break;

                case ContainerInline containerInline:
                    foreach (var child in containerInline)
                    {
                        switch (child)
                        {
                            case LiteralInline nestedLit:
                                targetPara.Inlines.Add(new Run(nestedLit.Content.ToString()));
                                break;
                            case LineBreakInline:
                                targetPara.Inlines.Add(new LineBreak());
                                break;
                            case EmphasisInline nestedEm:
                                RenderEmphasis(nestedEm, targetPara);
                                break;
                            case CodeInline nestedCode:
                                targetPara.Inlines.Add(new Run(nestedCode.Content)
                                {
                                    FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace"),
                                    Background = System.Windows.Media.Brushes.LightGray,
                                    FontSize = 12
                                });
                                break;
                            case LinkInline nestedLink when nestedLink.Url is not null:
                                RenderLink(nestedLink, targetPara);
                                break;
                        }
                    }
                    break;
            }
        }
    }

    private static void RenderEmphasis(EmphasisInline emphasis, Paragraph targetPara)
    {
        var isBold = emphasis.DelimiterCount >= 2;
        var isItalic = emphasis.DelimiterCount % 2 == 1; // odd counts include italic

        var span = new Span();
        if (isBold)
            span.FontWeight = FontWeights.Bold;
        if (isItalic)
            span.FontStyle = FontStyles.Italic;

        // Render children individually to preserve nested formatting
        foreach (var child in emphasis)
        {
            switch (child)
            {
                case LiteralInline lit:
                    span.Inlines.Add(new Run(lit.Content.ToString()));
                    break;
                case CodeInline code:
                    span.Inlines.Add(new Run(code.Content)
                    {
                        FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace"),
                        Background = System.Windows.Media.Brushes.LightGray,
                        FontSize = 12
                    });
                    break;
                case LineBreakInline:
                    span.Inlines.Add(new LineBreak());
                    break;
                case EmphasisInline nestedEm:
                    // Recursively render nested emphasis within a nested span
                    var nestedSpan = new Span();
                    foreach (var nestedChild in nestedEm)
                    {
                        if (nestedChild is LiteralInline nestedLit)
                            nestedSpan.Inlines.Add(new Run(nestedLit.Content.ToString()));
                    }
                    span.Inlines.Add(nestedSpan);
                    break;
            }
        }

        targetPara.Inlines.Add(span);
    }

    private static void RenderLink(LinkInline link, Paragraph targetPara)
    {
        var displayText = link.FirstChild?.ToString() ?? link.Url;
        var url = link.Url ?? string.Empty;

        var hyperlink = new Hyperlink(new Run(displayText))
        {
            NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null,
            ToolTip = url,
            FontWeight = FontWeights.SemiBold
        };

        if (hyperlink.NavigateUri is not null)
        {
            hyperlink.RequestNavigate += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Silently ignore
                }
            };
        }

        targetPara.Inlines.Add(hyperlink);
    }
}
