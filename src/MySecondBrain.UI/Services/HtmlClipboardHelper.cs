using System.Text;

namespace MySecondBrain.UI.Services;

/// <summary>
/// Helper for preparing HTML content for the clipboard.
/// The Windows clipboard expects HTML with specific header metadata
/// (CF_HTML format) including version, start/end offsets, and context
/// selections. This helper wraps raw HTML in the required format.
/// </summary>
internal static class HtmlClipboardHelper
{
    /// <summary>
    /// Wraps an HTML fragment in the CF_HTML clipboard format so that
    /// rich editors (Word, Outlook, etc.) can paste it as formatted content.
    /// </summary>
    public static string WrapHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        // Normalize the HTML fragment into a minimal document
        var fullHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body>
    {html}
</body>
</html>";

        // CF_HTML header template with offset placeholders
        // Offsets are byte counts in UTF-8 encoding from the start of the header string
        var header = @"Version:1.0
StartHTML:{0:000000}
EndHTML:{1:000000}
StartFragment:{2:000000}
EndFragment:{3:000000}
StartSelection:{2:000000}
EndSelection:{3:000000}
";

        var headerBytes = Encoding.UTF8.GetByteCount(header);
        var fullHtmlBytes = Encoding.UTF8.GetByteCount(fullHtml);

        // Find the fragment boundaries in the full HTML
        const string fragmentMarker = "<body>";
        var fragmentStart = fullHtml.IndexOf(fragmentMarker, StringComparison.Ordinal);
        if (fragmentStart < 0)
            throw new InvalidOperationException("Failed to locate fragment start marker in HTML");

        fragmentStart = fragmentStart + fragmentMarker.Length;
        var afterStart = fullHtml[fragmentStart..];

        var fragmentEndMarker = "</body>";
        var fragmentEnd = afterStart.IndexOf(fragmentEndMarker, StringComparison.Ordinal);
        if (fragmentEnd < 0)
            fragmentEnd = fullHtml.Length;
        else
            fragmentEnd = fragmentStart + fragmentEnd;

        // Calculate byte offsets
        var headerUtf8 = Encoding.UTF8.GetBytes(header);
        var fullHtmlUtf8 = Encoding.UTF8.GetBytes(fullHtml);

        var startHtmlOffset = headerUtf8.Length + 1; // +1 for newline separating header from HTML
        var startFragmentOffset = startHtmlOffset + Encoding.UTF8.GetByteCount(fullHtml[..fragmentStart]);
        var endFragmentOffset = startHtmlOffset + Encoding.UTF8.GetByteCount(fullHtml[..fragmentEnd]);
        var endHtmlOffset = startHtmlOffset + fullHtmlUtf8.Length;

        // Build the final CF_HTML string
        return string.Format(
            header + "\n" + fullHtml,
            startHtmlOffset,
            endHtmlOffset,
            startFragmentOffset,
            endFragmentOffset);
    }
}
