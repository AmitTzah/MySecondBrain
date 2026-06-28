using System.Windows;
using System.Windows.Documents;
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
    // CanRender tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void CanRender_NonNullNode_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        Assert.True(renderer.CanRender("test"));
    }

    [Fact]
    public void CanRender_Null_ReturnsFalse()
    {
        var renderer = CreateRenderer();
        Assert.False(renderer.CanRender(null));
    }

    // ════════════════════════════════════════════════════════════════
    // RenderAsync tests
    // ════════════════════════════════════════════════════════════════

    [StaFact]
    public async Task RenderAsync_WithText_AddsParagraph()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = new RenderContext(null!, null!, null!, false, 800);

        await renderer.RenderAsync("Sample citation text", document, context, CancellationToken.None);

        var para = document.Blocks.OfType<Paragraph>().FirstOrDefault();
        Assert.NotNull(para);
        Assert.Contains(para.Inlines.OfType<Run>(), r => r.Text.Contains("citation text"));
    }

    [StaFact]
    public async Task RenderAsync_WithEmptyText_DoesNotAddBlocks()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = new RenderContext(null!, null!, null!, false, 800);

        await renderer.RenderAsync("", document, context, CancellationToken.None);

        Assert.Empty(document.Blocks);
    }

    [StaFact]
    public async Task RenderAsync_CancellationToken_Throws()
    {
        var renderer = CreateRenderer();
        var document = new FlowDocument();
        var context = new RenderContext(null!, null!, null!, false, 800);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            renderer.RenderAsync("text", document, context, cts.Token));
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
