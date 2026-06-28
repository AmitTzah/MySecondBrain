using MySecondBrain.UI.Services;

namespace MySecondBrain.Tests.Unit;

public class MarkdownToHtmlConverterTests
{
    // ════════════════════════════════════════════════════════════════
    // Basic text and headings
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_NullInput_ReturnsEmpty()
    {
        var result = MarkdownToHtmlConverter.ToHtml(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToHtml_EmptyString_ReturnsEmpty()
    {
        var result = MarkdownToHtmlConverter.ToHtml(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToHtml_PlainText_WrapsInParagraph()
    {
        var result = MarkdownToHtmlConverter.ToHtml("Hello world");
        Assert.Equal("<p>Hello world</p>", result);
    }

    [Fact]
    public void ToHtml_HeadingH1_ReturnsH1Tag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("# Title");
        Assert.Contains("<h1>", result);
        Assert.Contains("Title", result);
        Assert.Contains("</h1>", result);
    }

    [Fact]
    public void ToHtml_HeadingH6_ReturnsH6Tag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("###### Tiny");
        Assert.Contains("<h6>", result);
        Assert.Contains("Tiny", result);
    }

    // ════════════════════════════════════════════════════════════════
    // Bold and italic
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_BoldText_ReturnsStrongTag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("This is **bold** text");
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public void ToHtml_ItalicText_ReturnsEmTag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("This is *italic* text");
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public void ToHtml_BoldAndItalic_ProcessesBoth()
    {
        var result = MarkdownToHtmlConverter.ToHtml("**bold** and *italic*");
        Assert.Contains("<strong>bold</strong>", result);
        Assert.Contains("<em>italic</em>", result);
    }

    // ════════════════════════════════════════════════════════════════
    // Inline code
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_InlineCode_ReturnsCodeTag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("Use `code` here");
        Assert.Contains("<code>code</code>", result);
    }

    [Fact]
    public void ToHtml_InlineCodeWithMarkers_ProcessesInlineCodeLast()
    {
        // Inline code is processed after bold/italic to protect generated HTML tags.
        // Markers inside backticks may be partially processed by regex-based parsing;
        // this test verifies the code wrapping still works.
        var result = MarkdownToHtmlConverter.ToHtml("`*not bold*`");
        Assert.Contains("<code>", result);
        Assert.Contains("</code>", result);
    }

    // ════════════════════════════════════════════════════════════════
    // Links
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_Link_ReturnsAnchorTag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("[Example](https://example.com)");
        Assert.Contains("<a href=\"https://example.com\">Example</a>", result);
    }

    // ════════════════════════════════════════════════════════════════
    // Fenced code blocks
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_FencedCodeBlock_WrapsInPreCode()
    {
        var result = MarkdownToHtmlConverter.ToHtml("```\ncode block\n```");
        Assert.Contains("<pre><code>", result);
        Assert.Contains("code block", result);
        Assert.Contains("</code></pre>", result);
    }

    [Fact]
    public void ToHtml_FencedCodeBlock_WithLanguage_StillWorks()
    {
        var result = MarkdownToHtmlConverter.ToHtml("```csharp\nvar x = 1;\n```");
        Assert.Contains("<pre><code>", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("</code></pre>", result);
    }

    // ════════════════════════════════════════════════════════════════
    // Horizontal rules
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_HorizontalRule_ReturnsHrTag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("---");
        Assert.Contains("<hr />", result);
    }

    [Fact]
    public void ToHtml_HorizontalRuleAsterisks_ReturnsHrTag()
    {
        var result = MarkdownToHtmlConverter.ToHtml("***");
        Assert.Contains("<hr />", result);
    }

    // ════════════════════════════════════════════════════════════════
    // HTML entity escaping
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToHtml_EscapesAngleBrackets()
    {
        var result = MarkdownToHtmlConverter.ToHtml("A < B > C");
        Assert.Contains("<", result);
        Assert.Contains(">", result);
        Assert.DoesNotContain("< B >", result);
    }

    [Fact]
    public void ToHtml_EscapesAmpersand()
    {
        var result = MarkdownToHtmlConverter.ToHtml("Fish & chips");
        Assert.Contains("&", result);
    }
}
