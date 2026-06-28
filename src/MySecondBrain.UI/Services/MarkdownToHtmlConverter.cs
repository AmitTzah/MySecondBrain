using System.Text;
using System.Text.RegularExpressions;

namespace MySecondBrain.UI.Services;

/// <summary>
/// Lightweight Markdown-to-HTML converter for clipboard operations.
/// Handles common inline patterns (bold, italic, code, links) and basic block
/// elements (headings, paragraphs, code blocks).
/// </summary>
public static class MarkdownToHtmlConverter
{
    private static readonly string AmpEntity = "&" + "amp;";
    private static readonly string LtEntity = "&" + "lt;";
    private static readonly string GtEntity = "&" + "gt;";
    private static readonly string QuotEntity = "&" + "quot;";

    /// <summary>
    /// Converts Markdown text to HTML.
    /// </summary>
    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var lines = markdown.Split('\n');
        var html = new StringBuilder();
        var inCodeBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Fenced code blocks (```)
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    html.AppendLine("</code></pre>");
                    inCodeBlock = false;
                }
                else
                {
                    html.AppendLine("<pre><code>");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                html.AppendLine(EscapeHtml(line));
                continue;
            }

            // Headings
            var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (headingMatch.Success)
            {
                var level = headingMatch.Groups[1].Length;
                var content = RenderInlines(headingMatch.Groups[2].Value);
                html.AppendLine($"<h{level}>{content}</h{level}>");
                continue;
            }

            // Horizontal rules
            if (Regex.IsMatch(line.Trim(), @"^[-*_]{3,}$"))
            {
                html.AppendLine("<hr />");
                continue;
            }

            // Empty line = paragraph break
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Regular paragraph
            var paragraph = RenderInlines(line.Trim());
            html.AppendLine($"<p>{paragraph}</p>");
        }

        if (inCodeBlock)
        {
            html.AppendLine("</code></pre>");
        }

        return html.ToString().TrimEnd();
    }

    private static string RenderInlines(string text)
    {
        // Escape HTML first
        var escaped = EscapeHtml(text);

        // Bold (**text** or __text__) — process before inline code so that
        // bold/italic markers inside `code` spans are not affected
        escaped = Regex.Replace(escaped, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        escaped = Regex.Replace(escaped, @"__(.+?)__", "<strong>$1</strong>");

        // Italic (*text* or _text_)
        escaped = Regex.Replace(escaped, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<em>$1</em>");
        escaped = Regex.Replace(escaped, @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", "<em>$1</em>");

        // Links [text](url)
        // Note: Does not handle URLs containing literal ')' characters.
        // Acceptable for a lightweight clipboard converter.
        escaped = Regex.Replace(escaped, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");

        // Inline code (`code`) — last so that markers inside generated HTML tags
        // from bold/italic/link processing are not matched
        escaped = Regex.Replace(escaped, @"`([^`]+)`", "<code>$1</code>");

        return escaped;
    }

    private static string EscapeHtml(string text)
    {
        var result = text.Replace("&", AmpEntity);
        result = result.Replace("<", LtEntity);
        result = result.Replace(">", GtEntity);
        result = result.Replace("\"", QuotEntity);
        return result;
    }
}
