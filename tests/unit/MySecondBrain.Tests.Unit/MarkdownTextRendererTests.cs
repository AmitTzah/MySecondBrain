using System.Windows;
using System.Windows.Documents;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.Controls;

namespace MySecondBrain.Tests.Unit;

public class MarkdownTextRendererTests
{
    private static MarkdownTextRenderer CreateRenderer() => new();

    private static RenderContext CreateRenderContext() =>
        new(null!, null!, null!, false, 800);

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
    public void CanRender_NonNullNode_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        Assert.True(renderer.CanRender("text"));
    }

    [Fact]
    public void CanRender_Null_ReturnsFalse()
    {
        var renderer = CreateRenderer();
        Assert.False(renderer.CanRender(null));
    }

    // ════════════════════════════════════════════════════════════════
    // RenderAsync - Basic text rendering
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_WithText_AddsParagraph()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = CreateRenderContext();

        await renderer.RenderAsync("Hello world", document, context, CancellationToken.None);

        var para = document.Blocks.FirstBlock as Paragraph;
        Assert.NotNull(para);
        var runs = para.Inlines.OfType<Run>().ToList();
        Assert.Contains(runs, r => r.Text.Contains("Hello world"));
    }

    [StaFact]
    public async Task RenderAsync_WithNull_DoesNotAddBlocks()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = CreateRenderContext();

        await renderer.RenderAsync(null, document, context, CancellationToken.None);

        Assert.Empty(document.Blocks);
    }

    [StaFact]
    public async Task RenderAsync_WithEmptyString_DoesNotAddBlocks()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = CreateRenderContext();

        await renderer.RenderAsync("", document, context, CancellationToken.None);

        Assert.Empty(document.Blocks);
    }

    [StaFact]
    public async Task RenderAsync_WithNonStringObject_DoesNotAddBlocks()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = CreateRenderContext();

        await renderer.RenderAsync(42, document, context, CancellationToken.None);

        Assert.Empty(document.Blocks);
    }

    // ════════════════════════════════════════════════════════════════
    // Cancellation
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_CancellationToken_Throws()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = CreateRenderContext();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            renderer.RenderAsync("Some text", document, context, cts.Token));
    }
}
