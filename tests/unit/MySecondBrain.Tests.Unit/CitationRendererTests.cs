using System.Windows;
using System.Windows.Documents;
using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.Controls;

namespace MySecondBrain.Tests.Unit;

public class CitationRendererTests
{
    private static CitationRenderer CreateRenderer() => new();

    // ════════════════════════════════════════════════════════════════
    // Metadata tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void RendererName_ReturnsCitation()
    {
        var renderer = CreateRenderer();
        Assert.Equal("Citation", renderer.RendererName);
    }

    [Fact]
    public void Priority_Returns350()
    {
        var renderer = CreateRenderer();
        Assert.Equal(350, renderer.Priority);
    }

    [Fact]
    public void ImplementsIContentBlockRenderer()
    {
        var renderer = CreateRenderer();
        Assert.IsAssignableFrom<IContentBlockRenderer>(renderer);
    }

    // ════════════════════════════════════════════════════════════════
    // CanRender tests — using real Markdig AST from parsed markdown
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void CanRender_FootnoteLink_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        var link = ExtractFirstNode<FootnoteLink>("Text[^1]\n\n[^1]: Footnote");
        Assert.True(renderer.CanRender(link));
    }

    [Fact]
    public void CanRender_Footnote_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        var footnote = ExtractFirstNode<Footnote>("Text[^1]\n\n[^1]: Footnote");
        Assert.True(renderer.CanRender(footnote));
    }

    [Fact]
    public void CanRender_Paragraph_ReturnsFalse()
    {
        var renderer = CreateRenderer();
        var doc = ParseWithFootnotes("Just plain text");
        var para = doc.Descendants<ParagraphBlock>().First();
        Assert.False(renderer.CanRender(para));
    }

    [Fact]
    public void CanRender_Heading_ReturnsFalse()
    {
        var renderer = CreateRenderer();
        var doc = ParseWithFootnotes("# Heading");
        var heading = doc.Descendants<HeadingBlock>().First();
        Assert.False(renderer.CanRender(heading));
    }

    [Fact]
    public void CanRender_CodeBlock_ReturnsFalse()
    {
        var renderer = CreateRenderer();
        var doc = ParseWithFootnotes("```\ncode\n```");
        var code = doc.Descendants<FencedCodeBlock>().First();
        Assert.False(renderer.CanRender(code));
    }

    // ════════════════════════════════════════════════════════════════
    // RenderAsync — RenderInlineMarker tests
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_FootnoteLink_AddsSuperscriptHyperlink()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        // Simulate the text paragraph that contains the citation marker
        var contentPara = new Paragraph();
        document.Blocks.Add(contentPara);

        // Parse markdown to get a real FootnoteLink with a valid Footnote reference
        var link = ExtractFirstNode<FootnoteLink>("Text[^1]\n\n[^1]: Source Title");
        var context = CreateRenderContext();

        await renderer.RenderAsync(link, document, context, CancellationToken.None);

        // The span should be added to the last paragraph (contentPara)
        var lastPara = document.Blocks.LastBlock as Paragraph;
        Assert.NotNull(lastPara);
        var span = lastPara.Inlines.FirstInline as Span;
        Assert.NotNull(span);
        Assert.Equal(FontVariants.Superscript, span.Typography.Variants);

        // Verify the hyperlink contains the citation label
        var hyperlink = span.Inlines.FirstInline as Hyperlink;
        Assert.NotNull(hyperlink);
        var run = hyperlink.Inlines.FirstInline as Run;
        Assert.NotNull(run);
        Assert.Equal("[1]", run.Text);
    }

    [StaFact]
    public async Task RenderAsync_FootnoteLink_AddsToExistingParagraph()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        var contentPara = new Paragraph();
        contentPara.Inlines.Add(new Run("Existing text "));
        document.Blocks.Add(contentPara);

        var link = ExtractFirstNode<FootnoteLink>("Text[^1]\n\n[^1]: Source Title");
        var context = CreateRenderContext();

        await renderer.RenderAsync(link, document, context, CancellationToken.None);

        // Verify the span was appended to the existing paragraph
        var lastPara = document.Blocks.LastBlock as Paragraph;
        Assert.NotNull(lastPara);
        // First inline is the "Existing text " Run, second is the citation span
        Assert.Equal(2, lastPara.Inlines.Count);
        var span = lastPara.Inlines.LastInline as Span;
        Assert.NotNull(span);
        Assert.NotEmpty(span.Inlines.OfType<Hyperlink>());
    }

    [StaFact]
    public async Task RenderAsync_FootnoteLink_WithNoContentParagraph_CreatesNewParagraph()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        // No paragraphs in the document initially

        var link = ExtractFirstNode<FootnoteLink>("Text[^1]\n\n[^1]: Source Title");
        var context = CreateRenderContext();

        await renderer.RenderAsync(link, document, context, CancellationToken.None);

        // A new paragraph should have been created
        Assert.Single(document.Blocks);
        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        var span = para.Inlines.FirstInline as Span;
        Assert.NotNull(span);
        Assert.NotEmpty(span.Inlines.OfType<Hyperlink>());
    }

    // ════════════════════════════════════════════════════════════════
    // RenderAsync — RenderFootnoteDefinition tests
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_Footnote_AddsStyledParagraph()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        var footnote = ExtractFirstNode<Footnote>("Text[^1]\n\n[^1]: Source Title");
        var context = CreateRenderContext();

        await renderer.RenderAsync(footnote, document, context, CancellationToken.None);

        // Verify a paragraph with the footnote tag was added
        var para = document.Blocks.OfType<Paragraph>()
            .FirstOrDefault(p => p.Tag is string tag && tag.StartsWith("fn:"));
        Assert.NotNull(para);
        Assert.True(para.Margin.Top > 0);
    }

    [StaFact]
    public async Task RenderAsync_Footnote_IncludesIndexNumber()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        var footnote = ExtractFirstNode<Footnote>("Text[^2]\n\n[^2]: Another source");
        var context = CreateRenderContext();

        await renderer.RenderAsync(footnote, document, context, CancellationToken.None);

        var para = document.Blocks.OfType<Paragraph>()
            .FirstOrDefault(p => p.Tag is string tag && tag.StartsWith("fn:"));
        Assert.NotNull(para);

        // First inline should be bold index
        var run = para.Inlines.FirstInline as Run;
        Assert.NotNull(run);
        Assert.Contains("2", run.Text);
        Assert.Equal(FontWeights.Bold, run.FontWeight);
    }

    // ════════════════════════════════════════════════════════════════
    // RenderAsync — Graceful degradation tests
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_Footnote_WithoutUrl_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        // Footnote plain text with no URL — tests graceful degradation
        var footnote = ExtractFirstNode<Footnote>(
            "Text[^1]\n\n[^1]: \"Test Title\" — example.com — accessed 2026-06-15");
        var context = CreateRenderContext();

        var ex = await Record.ExceptionAsync(() =>
            renderer.RenderAsync(footnote, document, context, CancellationToken.None));
        Assert.Null(ex);

        var para = document.Blocks.OfType<Paragraph>()
            .FirstOrDefault(p => p.Tag is string tag && tag.StartsWith("fn:"));
        Assert.NotNull(para);
    }

    [StaFact]
    public async Task RenderAsync_Footnote_WithDomainAndDate_IncludesBoth()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        var footnote = ExtractFirstNode<Footnote>(
            "Text[^1]\n\n[^1]: \"Sample\" — test.org — accessed 2026-06-20");
        var context = CreateRenderContext();

        await renderer.RenderAsync(footnote, document, context, CancellationToken.None);

        var para = document.Blocks.OfType<Paragraph>()
            .FirstOrDefault(p => p.Tag is string tag && tag.StartsWith("fn:"));
        Assert.NotNull(para);

        var text = string.Concat(para.Inlines.OfType<Run>().Select(r => r.Text));
        Assert.Contains("test.org", text);
        Assert.Contains("accessed 2026-06-20", text);
    }

    // ════════════════════════════════════════════════════════════════
    // RenderAsync — Multi-element integration test
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_MultipleCitations_AllRenderWithoutError()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();

        // Use two separate single-footnote parses to avoid Markdig multi-footnote parsing edge cases
        var fn1 = ExtractFirstNode<Footnote>(
            "Text[^a]\n\n[^a]: \"First source\" — a.com — accessed 2026-06-15");
        var fn2 = ExtractFirstNode<Footnote>(
            "Text[^b]\n\n[^b]: \"Second source\" — b.com — accessed 2026-06-16");
        var link1 = ExtractFirstNode<FootnoteLink>(
            "Text[^a]\n\n[^a]: \"First source\" — a.com — accessed 2026-06-15");
        var link2 = ExtractFirstNode<FootnoteLink>(
            "Text[^b]\n\n[^b]: \"Second source\" — b.com — accessed 2026-06-16");

        var context = CreateRenderContext();

        // Content paragraph
        var contentPara = new Paragraph();
        document.Blocks.Add(contentPara);

        // Render inline markers into the content paragraph
        await renderer.RenderAsync(link1, document, context, CancellationToken.None);
        await renderer.RenderAsync(link2, document, context, CancellationToken.None);

        // Then render footnote definitions
        await renderer.RenderAsync(fn1, document, context, CancellationToken.None);
        await renderer.RenderAsync(fn2, document, context, CancellationToken.None);

        // The content paragraph should have 2 citation spans
        var spans = contentPara.Inlines.OfType<Span>().ToList();
        Assert.Equal(2, spans.Count);
        Assert.All(spans, s => Assert.NotEmpty(s.Inlines.OfType<Hyperlink>()));

        // Footnote definition paragraphs should exist with correct tags
        var fnParas = document.Blocks.OfType<Paragraph>()
            .Where(p => p.Tag is string tag && tag.StartsWith("fn:"))
            .ToList();
        Assert.Equal(2, fnParas.Count);
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses markdown with footnotes extension and returns the first
    /// descendant of type T from the AST.
    /// </summary>
    private static T ExtractFirstNode<T>(string markdown) where T : MarkdownObject
    {
        var doc = ParseWithFootnotes(markdown);
        var node = doc.Descendants<T>().FirstOrDefault();
        Assert.NotNull(node);
        return node;
    }

    /// <summary>
    /// Parses markdown using the footnotes extension enabled pipeline.
    /// </summary>
    private static MarkdownDocument ParseWithFootnotes(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseFootnotes()
            .Build();
        return Markdown.Parse(markdown, pipeline);
    }

    private static RenderContext CreateRenderContext()
    {
        return new RenderContext(
            null!, // ChatThread
            null!, // Message
            null!, // IThemeProvider
            false,
            800);
    }
}
