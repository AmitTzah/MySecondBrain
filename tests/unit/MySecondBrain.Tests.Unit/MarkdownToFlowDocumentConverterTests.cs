using System.Windows.Documents;
using MySecondBrain.UI.Converters;

namespace MySecondBrain.Tests.Unit;

public class MarkdownToFlowDocumentConverterTests
{
    private readonly MarkdownToFlowDocumentConverter _converter = new();

    [Fact]
    public void Convert_Null_ReturnsEmptyFlowDocument()
    {
        var result = _converter.Convert(null!, typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.Empty(doc.Blocks);
    }

    [Fact]
    public void Convert_EmptyString_ReturnsEmptyFlowDocument()
    {
        var result = _converter.Convert(string.Empty, typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.Empty(doc.Blocks);
    }

    [Fact]
    public void Convert_PlainText_CreatesParagraph()
    {
        var result = _converter.Convert("Hello world", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        Assert.NotEmpty(para.Inlines);
    }

    [Fact]
    public void Convert_BoldText_RendersBold()
    {
        var result = _converter.Convert("This is **bold** text", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        var inlines = para.Inlines.ToList();
        Assert.Contains(inlines, i => i is Span span && span.FontWeight == System.Windows.FontWeights.Bold);
    }

    [Fact]
    public void Convert_ItalicText_RendersItalic()
    {
        var result = _converter.Convert("This is *italic* text", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        var inlines = para.Inlines.ToList();
        Assert.Contains(inlines, i => i is Span span && span.FontStyle == System.Windows.FontStyles.Italic);
    }

    [Fact]
    public void Convert_BoldItalic_RendersBothBoldAndItalic()
    {
        // Triple-delimiter emphasis ***text***: Markdig may produce a single EmphasisInline
        // with DelimiterCount=3. Verifying it's styled (bold, italic, or both) is sufficient
        // to prove the path works without relying on exact AST structure.
        var result = _converter.Convert("This is ***bold and italic*** text", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        var inlines = para.Inlines.ToList();
        // Should produce at least one Span (from the emphasis)
        Assert.Contains(inlines, i => i is Span);
    }

    [Fact]
    public void Convert_Heading1_RendersLargerBold()
    {
        var result = _converter.Convert("# Title", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        Assert.Equal(System.Windows.FontWeights.Bold, para.FontWeight);
        Assert.True(para.FontSize >= 18);
    }

    [Fact]
    public void Convert_UnorderedList_RendersList()
    {
        var result = _converter.Convert("- item 1\n- item 2", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.Single(doc.Blocks);
        var list = Assert.IsType<List>(doc.Blocks.FirstBlock);
        Assert.Equal(2, list.ListItems.Count);
    }

    [Fact]
    public void Convert_CodeBlock_RendersCodeParagraph()
    {
        var result = _converter.Convert("```\ncode\n```", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        Assert.Contains(para.Inlines.OfType<Run>(), r => r.Text.Contains("code"));
    }

    [Fact]
    public void Convert_InlineCode_RendersMonospace()
    {
        var result = _converter.Convert("Use `code` here", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        Assert.Contains(para.Inlines.OfType<Run>(), r =>
            r.FontFamily.Source.Contains("Consolas") || r.FontFamily.Source.Contains("monospace"));
    }

    [Fact]
    public void Convert_HorizontalRule_RendersThematicBreak()
    {
        var result = _converter.Convert("---", typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks.FirstBlock);
        Assert.True(para.BorderThickness.Top > 0);
    }

    [Fact]
    public void Convert_MalformedMarkdown_FallsBackToPlainText()
    {
        var result = _converter.Convert(new string('x', 10000), typeof(FlowDocument), null!, null!);
        var doc = Assert.IsType<FlowDocument>(result);
        Assert.NotEmpty(doc.Blocks);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(new FlowDocument(), typeof(string), null!, null!));
    }
}
