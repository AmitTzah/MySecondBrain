using System.Windows.Documents;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.Chat;

namespace MySecondBrain.Tests.Unit;

public class MarkdownStreamRendererTests
{
    private readonly Mock<IContentRendererRegistry> _registryMock = new();
    private readonly Mock<ILogger<MarkdownStreamRenderer>> _loggerMock = new();

    private MarkdownStreamRenderer CreateRenderer()
    {
        return new MarkdownStreamRenderer(_registryMock.Object, _loggerMock.Object);
    }

    // ================================================================
    // AppendToken — Progressive Text Accumulation
    // ================================================================

    [Fact]
    public void AppendToken_AccumulatesText()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("Hello ");
        renderer.AppendToken("World!");

        // Assert
        Assert.Equal("Hello World!", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_WhenNotAttached_StillAccumulates()
    {
        // Arrange
        var renderer = CreateRenderer();

        // Act — no document attached
        renderer.AppendToken("Hello ");
        renderer.AppendToken("World!");

        // Assert — text accumulates even without a document
        Assert.Equal("Hello World!", renderer.AccumulatedText);
    }

    // ================================================================
    // AttachDocument / DetachDocument
    // ================================================================

    [Fact]
    public void AttachDocument_ClearsBuffer()
    {
        // Arrange
        var renderer = CreateRenderer();
        renderer.AppendToken("Previous text");
        Assert.Equal("Previous text", renderer.AccumulatedText);

        // Act
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Assert — buffer cleared
        Assert.Equal(string.Empty, renderer.AccumulatedText);
        Assert.True(renderer.IsAttached);
    }

    [Fact]
    public void DetachDocument_StopsRendering()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);
        Assert.True(renderer.IsAttached);

        // Act
        renderer.DetachDocument();

        // Assert
        Assert.False(renderer.IsAttached);
    }

    // ================================================================
    // Empty / Edge Cases
    // ================================================================

    [Fact]
    public void AppendToken_EmptyString_DoesNotThrow()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act & Assert
        renderer.AppendToken(string.Empty);
        Assert.Equal(string.Empty, renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_NullString_DoesNotThrow()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act & Assert
        renderer.AppendToken(null!);
        // AppendToken calls StringBuilder.Append(null) which appends nothing
        Assert.Equal(string.Empty, renderer.AccumulatedText);
    }

    [Fact]
    public void MultipleAttachAndDetach_WorksCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc1 = new FlowDocument();
        var doc2 = new FlowDocument();

        // Act — attach, add text, detach, attach new doc
        renderer.AttachDocument(doc1);
        renderer.AppendToken("Text for doc1");
        renderer.DetachDocument();

        renderer.AttachDocument(doc2);
        renderer.AppendToken("Text for doc2");

        // Assert
        Assert.Equal("Text for doc2", renderer.AccumulatedText);
        Assert.True(renderer.IsAttached);
    }

    // ================================================================
    // Malformed Markdown Resilience
    // ================================================================

    [Fact]
    public void AppendToken_MalformedMarkdown_DoesNotThrow()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — partial/incomplete Markdown (unclosed code fence)
        // This should NOT crash; the renderer swallows parse failures
        renderer.AppendToken("Some text\n```\nunclosed code block\n");
        renderer.AppendToken("more text\n");
        renderer.AppendToken("```\nclosed now");

        // Assert — text still accumulated correctly despite failed intermediate renders
        Assert.Contains("unclosed code block", renderer.AccumulatedText);
        Assert.Contains("closed now", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_AfterMalformedMarkdown_Recovers()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — start with malformed, then add valid markdown
        renderer.AppendToken("```\n"); // unclosed fence
        renderer.AppendToken("Hello **world**!"); // valid markdown after (fence still open)
        renderer.AppendToken("\n```\n"); // close the fence

        // Assert — text is accumulated, renderer recovered
        Assert.Contains("Hello", renderer.AccumulatedText);
    }
}
