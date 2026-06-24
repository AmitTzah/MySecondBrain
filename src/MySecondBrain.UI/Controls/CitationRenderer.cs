using System.Windows.Documents;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

public class CitationRenderer : IContentBlockRenderer
{
    public string RendererName => "Citation";
    public int Priority => 350;

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is FootnoteLink or Footnote;

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (markdownNode is FootnoteLink link)
        {
            RenderInlineMarker(link, targetDocument);
        }
        else if (markdownNode is Footnote footnote)
        {
            RenderFootnoteDefinition(footnote, targetDocument);
        }

        return Task.CompletedTask;
    }

    private static void RenderInlineMarker(FootnoteLink link, FlowDocument document)
    {
        var footnote = link.Footnote;
        // Strip leading '^' from label if present — Markdig stores it as part of the label
        var label = (footnote?.Label ?? link.Index.ToString()).TrimStart('^');

        var span = new Span
        {
            Typography = { Variants = FontVariants.Superscript },
            FontSize = 10
        };

        // Graceful degradation: if footnote reference is null, render as plain text
        if (footnote is not null)
        {
            var hyperlink = new Hyperlink(new Run($"[{label}]"));
            hyperlink.Click += (_, _) =>
            {
                var target = document.Blocks
                    .OfType<Paragraph>()
                    .FirstOrDefault(p => p.Tag is string tag && tag == $"fn:{label}");
                target?.BringIntoView();
            };
            span.Inlines.Add(hyperlink);
        }
        else
        {
            // Graceful degradation: missing footnote reference → plain text
            span.Inlines.Add(new Run($"[{label}]"));
        }

        // Add span to the last paragraph (the one currently being built)
        var lastPara = document.Blocks.LastOrDefault() as Paragraph;
        if (lastPara is not null)
        {
            lastPara.Inlines.Add(span);
        }
        else
        {
            document.Blocks.Add(new Paragraph(span));
        }
    }

    private static void RenderFootnoteDefinition(Footnote footnote, FlowDocument document)
    {
        // Strip leading '^' from label if present — Markdig stores it as part of the label
        var label = (footnote.Label ?? "").TrimStart('^');

        var (title, url, domain, dateAccessed) = ParseFootnoteContent(footnote);

        var paragraph = new Paragraph
        {
            Tag = $"fn:{label}",
            Margin = new Thickness(0, 4, 0, 4)
        };

        // Index number (bold)
        paragraph.Inlines.Add(new Run($"[{label}] ") { FontWeight = FontWeights.Bold });

        // Title — linked if URL is a valid absolute URI (graceful degradation)
        if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            paragraph.Inlines.Add(new Hyperlink(new Run(title)) { NavigateUri = uri });
        }
        else
        {
            paragraph.Inlines.Add(new Run(title));
        }

        // Domain
        if (!string.IsNullOrEmpty(domain))
        {
            paragraph.Inlines.Add(new Run($" — {domain}"));
        }

        // Date accessed
        if (!string.IsNullOrEmpty(dateAccessed))
        {
            paragraph.Inlines.Add(new Run($" — accessed {dateAccessed}"));
        }

        document.Blocks.Add(paragraph);
    }

    private static (string title, string url, string domain, string dateAccessed)
        ParseFootnoteContent(Footnote footnote)
    {
        var firstPara = footnote.OfType<ParagraphBlock>().FirstOrDefault();
        var url = string.Empty;
        var title = string.Empty;

        // Extract URL and link text from Markdig LinkInline if present
        if (firstPara?.Inline is not null)
        {
            var linkInline = firstPara.Inline.Descendants<LinkInline>().FirstOrDefault();
            if (linkInline is not null)
            {
                url = linkInline.Url ?? string.Empty;
                title = linkInline.FirstChild?.ToString() ?? url;
            }
        }

        // Get full plain text from the footnote
        var fullText = string.Join(" ", footnote.Descendants<LiteralInline>()
            .Select(l => l.Content.ToString()));

        // Fallback if no link inline was found
        if (string.IsNullOrEmpty(title))
        {
            title = fullText.Trim('"');
        }

        var (domain, dateAccessed) = ParseDomainAndDate(fullText, url);

        return (title, url, domain, dateAccessed);
    }

    /// <summary>
    /// Extracts domain and date-accessed from footnote text.
    /// Expected format: " — domain — accessed date" (e.g., " — iter.org — accessed 2026-06-15").
    /// When no URL is present, fullText includes the title and is parsed entirely.
    /// When a URL is present, only the text after the URL is parsed for domain/date.
    /// </summary>
    private static (string domain, string dateAccessed) ParseDomainAndDate(string fullText, string url)
    {
        // When URL is present, domain/date follow after it; otherwise parse from full text
        var textToParse = string.IsNullOrEmpty(url)
            ? fullText
            : ExtractAfterLink(fullText, url);

        var parts = textToParse.Split(" — ", StringSplitOptions.TrimEntries);

        var domain = parts.Length >= 1 ? parts[0] : string.Empty;
        var dateAccessed = string.Empty;

        if (parts.Length >= 2 && parts[1].StartsWith("accessed", StringComparison.OrdinalIgnoreCase))
        {
            dateAccessed = parts[1]["accessed".Length..].Trim();
        }

        return (domain, dateAccessed);
    }

    private static string ExtractAfterLink(string text, string url)
    {
        var idx = text.IndexOf(url, StringComparison.Ordinal);
        return idx < 0 ? text : text[(idx + url.Length)..].Trim();
    }
}
