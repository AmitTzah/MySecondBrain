using System.Windows;
using System.Windows.Documents;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.Controls;

namespace MySecondBrain.Tests.Unit;

public class MarkdownTextRendererTests
{
    private static MarkdownTextRenderer CreateRenderer() => new();

    private static ParagraphBlock ParseFirstParagraph(string markdown)
    {
        var doc = ParseMarkdown(markdown);
        return doc.Descendants<ParagraphBlock>().First();
    }

    private static HeadingBlock ParseFirstHeading(string markdown)
    {
        var doc = ParseMarkdown(markdown);
        return doc.Descendants<HeadingBlock>().First();
    }

    private static QuoteBlock ParseFirstQuote(string markdown)
    {
        var doc = ParseMarkdown(markdown);
        return doc.Descendants<QuoteBlock>().First();
    }

    private static ListBlock ParseFirstList(string markdown)
    {
        var doc = ParseMarkdown(markdown);
        return doc.Descendants<ListBlock>().First();
    }

    private static MarkdownDocument ParseMarkdown(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        return Markdown.Parse(markdown, pipeline);
    }

    private static RenderContext CreateRenderContext() =>
        new(null!, null!, null!, false, 800);

    private static List<Run> GetAllRuns(Paragraph para)
    {
        var runs = new List<Run>();
        CollectRuns(para.Inlines, runs);
        return runs;
    }

    private static void CollectRuns(InlineCollection inlines, List<Run> runs)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run)
                runs.Add(run);
            else if (inline is Span span)
                CollectRuns(span.Inlines, runs);
            else if (inline is Hyperlink link)
                CollectRuns(link.Inlines, runs);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Metadata tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void RendererName_ReturnsMarkdownText()
    {
        var renderer = CreateRenderer();
        Assert.Equal("MarkdownText", renderer.RendererName);
    }

    [Fact]
    public void Priority_Returns100()
    {
        var renderer = CreateRenderer();
        Assert.Equal(100, renderer.Priority);
    }

    [Fact]
    public void ImplementsIContentBlockRenderer()
    {
        var renderer = CreateRenderer();
        Assert.IsAssignableFrom<IContentBlockRenderer>(renderer);
    }

    // ════════════════════════════════════════════════════════════════
    // CanRender tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void CanRender_ParagraphBlock_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        var node = ParseFirstParagraph("Hello world");
        Assert.True(renderer.CanRender(node));
    }

    [Fact]
    public void CanRender_HeadingBlock_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        var node = ParseFirstHeading("# Heading");
        Assert.True(renderer.CanRender(node));
    }

    [Fact]
    public void CanRender_QuoteBlock_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        var node = ParseFirstQuote("> A quote");
        Assert.True(renderer.CanRender(node));
    }

    [Fact]
    public void CanRender_ListBlock_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        var node = ParseFirstList("- Item 1\n- Item 2");
        Assert.True(renderer.CanRender(node));
    }

    [Fact]
    public void CanRender_CodeBlock_ReturnsFalse()
    {
        var renderer = CreateRenderer();
        var doc = ParseMarkdown("```\ncode\n```");
        var code = doc.Descendants<FencedCodeBlock>().First();
        Assert.False(renderer.CanRender(code));
    }

    // ════════════════════════════════════════════════════════════════
    // Heading Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_HeadingH1_HasCorrectFontSize()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstHeading("# Big Heading");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        Assert.Equal(24.0, para.FontSize);
        Assert.Equal(FontWeights.Bold, para.FontWeight);
    }

    [StaFact]
    public async Task RenderAsync_HeadingH6_HasCorrectFontSize()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstHeading("###### Tiny Heading");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        Assert.Equal(11.0, para.FontSize);
    }

    [StaFact]
    public async Task RenderAsync_Heading_ContainsText()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstHeading("## Section Title");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        var runs = GetAllRuns(para);
        Assert.Contains(runs, r => r.Text.Contains("Section Title"));
    }

    // ════════════════════════════════════════════════════════════════
    // Bold and Italic Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_BoldText_RendersCorrectly()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstParagraph("This is **bold** text");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        // Bold text is wrapped in a Span with FontWeight.Bold, Run inside inherits it
        var boldSpan = para.Inlines.OfType<Span>().FirstOrDefault(s =>
            s.FontWeight == FontWeights.Bold);
        Assert.NotNull(boldSpan);
        // Verify bold text does NOT have italic (regression test for emphasis logic bug)
        Assert.NotEqual(FontStyles.Italic, boldSpan.FontStyle);
        var run = boldSpan.Inlines.OfType<Run>().FirstOrDefault();
        Assert.NotNull(run);
        Assert.Equal("bold", run.Text);
    }

    [StaFact]
    public async Task RenderAsync_ItalicText_RendersCorrectly()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstParagraph("This is *italic* text");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        // Italic text is wrapped in a Span with FontStyle.Italic
        var italicSpan = para.Inlines.OfType<Span>().FirstOrDefault(s =>
            s.FontStyle == FontStyles.Italic);
        Assert.NotNull(italicSpan);
        var run = italicSpan.Inlines.OfType<Run>().FirstOrDefault();
        Assert.NotNull(run);
        Assert.Equal("italic", run.Text);
    }

    // ════════════════════════════════════════════════════════════════
    // List Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_BulletedList_AddsListBlock()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstList("- Item A\n- Item B\n- Item C");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var list = document.Blocks.OfType<List>().FirstOrDefault();
        Assert.NotNull(list);
        Assert.Equal(3, list.ListItems.Count);
    }

    [StaFact]
    public async Task RenderAsync_NumberedList_UsesDecimalMarker()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstList("1. First\n2. Second");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var list = document.Blocks.OfType<List>().FirstOrDefault();
        Assert.NotNull(list);
        Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
    }

    // ════════════════════════════════════════════════════════════════
    // Link Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_Link_CreatesHyperlink()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstParagraph("Visit [Example](https://example.com) now");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        var hyperlink = para.Inlines.OfType<Hyperlink>().FirstOrDefault();
        Assert.NotNull(hyperlink);
        Assert.NotNull(hyperlink.NavigateUri);
        Assert.Equal("https://example.com/", hyperlink.NavigateUri.AbsoluteUri);
    }

    // ════════════════════════════════════════════════════════════════
    // Blockquote Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_Blockquote_AddsSectionWithLeftBorder()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstQuote("> A wise quote");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var section = document.Blocks.OfType<Section>().FirstOrDefault();
        Assert.NotNull(section);
        Assert.True(section.BorderThickness.Left > 0);
        Assert.True(section.Padding.Left > 0);
    }

    // ════════════════════════════════════════════════════════════════
    // Horizontal Rule Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_ThematicBreak_AddsParagraphWithTopBorder()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = CreateRenderContext();

        var doc = ParseMarkdown("---");
        var hr = doc.Descendants<ThematicBreakBlock>().First();

        await renderer.RenderAsync(hr, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        Assert.True(para.BorderThickness.Top > 0);
    }

    // ════════════════════════════════════════════════════════════════
    // Inline Code Rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_InlineCode_UsesMonospaceFont()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstParagraph("Use `code` here");
        var context = CreateRenderContext();

        await renderer.RenderAsync(node, document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        var runs = GetAllRuns(para);
        var codeRun = runs.FirstOrDefault(r =>
            r.FontFamily.ToString().Contains("Consolas"));
        Assert.NotNull(codeRun);
        Assert.Equal("code", codeRun.Text);
    }

    // ════════════════════════════════════════════════════════════════
    // Edge Cases
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_CancellationToken_Throws()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var node = ParseFirstParagraph("Some text");
        var context = CreateRenderContext();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            renderer.RenderAsync(node, document, context, cts.Token));
    }
}
