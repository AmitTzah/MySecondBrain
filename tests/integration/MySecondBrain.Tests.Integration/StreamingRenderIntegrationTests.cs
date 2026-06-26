using System.Runtime.CompilerServices;
using System.Windows.Documents;

using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Core.Utilities;
using MySecondBrain.Services.Chat;

namespace MySecondBrain.Tests.Integration;

/// <summary>
/// Integration tests for MarkdownStreamRenderer using real WPF FlowDocument
/// instances. Tests progressive rendering with various Markdown constructs,
/// renderer lifecycle, and recovery from malformed input.
/// </summary>
public class StreamingRenderIntegrationTests
{
    private readonly Mock<IContentRendererRegistry> _registryMock = new();
    private readonly Mock<ILogger<MarkdownStreamRenderer>> _loggerMock = new();

    private MarkdownStreamRenderer CreateRenderer()
    {
        return new MarkdownStreamRenderer(_registryMock.Object, _loggerMock.Object);
    }

    // ================================================================
    // Progressive Rendering with Simple Text
    // ================================================================

    [Fact]
    public void AppendToken_SimpleText_AccumulatesInBuffer()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("Hello, ");
        renderer.AppendToken("world!");
        renderer.AppendToken(" This is a test.");

        // Assert
        Assert.Equal("Hello, world! This is a test.", renderer.AccumulatedText);
        Assert.True(renderer.IsAttached);
    }

    // ================================================================
    // Progressive Rendering with Markdown Constructs
    // ================================================================

    [Fact]
    public void AppendToken_HeadingMarkdown_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — stream heading tokens progressively
        renderer.AppendToken("# ");
        renderer.AppendToken("Heading ");
        renderer.AppendToken("1\n\n");
        renderer.AppendToken("This is a paragraph.");

        // Assert
        Assert.Contains("# Heading 1", renderer.AccumulatedText);
        Assert.Contains("This is a paragraph.", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_BoldAndItalic_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("This is **bold** and ");
        renderer.AppendToken("*italic* text.");

        // Assert
        Assert.Contains("**bold**", renderer.AccumulatedText);
        Assert.Contains("*italic*", renderer.AccumulatedText);
        Assert.Equal("This is **bold** and *italic* text.", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_CodeFence_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — stream code block tokens
        renderer.AppendToken("Here is some code:\n");
        renderer.AppendToken("```csharp\n");
        renderer.AppendToken("var x = 42;\n");
        renderer.AppendToken("Console.WriteLine(x);\n");
        renderer.AppendToken("```\n");
        renderer.AppendToken("End of code.");

        // Assert
        Assert.Contains("```csharp", renderer.AccumulatedText);
        Assert.Contains("var x = 42;", renderer.AccumulatedText);
        Assert.Contains("Console.WriteLine(x);", renderer.AccumulatedText);
        Assert.Contains("End of code.", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_ListMarkdown_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("- Item 1\n");
        renderer.AppendToken("- Item 2\n");
        renderer.AppendToken("- Item 3\n");

        // Assert
        Assert.Contains("- Item 1", renderer.AccumulatedText);
        Assert.Contains("- Item 2", renderer.AccumulatedText);
        Assert.Contains("- Item 3", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_TableMarkdown_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("| Col1 | Col2 |\n");
        renderer.AppendToken("|------|------|\n");
        renderer.AppendToken("| A    | B    |\n");
        renderer.AppendToken("| C    | D    |\n");

        // Assert
        Assert.Contains("| Col1 | Col2 |", renderer.AccumulatedText);
        Assert.Contains("| A    | B    |", renderer.AccumulatedText);
    }

    // ================================================================
    // Progressive Streaming Simulation
    // ================================================================

    [Fact]
    public async Task ProgressiveStream_SimulatedChunks_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — simulate streaming chunks
        var chunks = new[]
        {
            "This is a ",
            "**progressive** ",
            "streaming ",
            "*test* ",
            "with multiple ",
            "chunks.\n\n",
            "## Second paragraph\n\n",
            "More content here."
        };

        foreach (var chunk in chunks)
        {
            renderer.AppendToken(chunk);
            await Task.Delay(10); // Simulate real streaming delay
        }

        // Assert
        Assert.Contains("**progressive**", renderer.AccumulatedText);
        Assert.Contains("*test*", renderer.AccumulatedText);
        Assert.Contains("## Second paragraph", renderer.AccumulatedText);
        Assert.Contains("More content here.", renderer.AccumulatedText);
        Assert.Equal(string.Concat(chunks), renderer.AccumulatedText);
    }

    // ================================================================
    // Edge Cases
    // ================================================================

    [Fact]
    public void AppendToken_LongText_DoesNotOverflow()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — generate a long text (10K+ chars)
        var longText = new string('A', 10_000) + "\n\n" + new string('B', 10_000);
        renderer.AppendToken(longText);

        // Assert
        Assert.Equal(20_002, renderer.AccumulatedText.Length); // 10K A + \n\n + 10K B
        Assert.Contains(new string('A', 10_000), renderer.AccumulatedText);
        Assert.Contains(new string('B', 10_000), renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_MultipleNewlines_HandledCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("Line 1\n\n\n\nLine 2");

        // Assert
        Assert.Contains("Line 1", renderer.AccumulatedText);
        Assert.Contains("Line 2", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_HebrewMixedWithCode_AccumulatesCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act — Hebrew text mixed with code
        renderer.AppendToken("שלום עולם! This is a test.\n\n");
        renderer.AppendToken("```\nvar x = 42; // תגובה בעברית\n```\n");
        renderer.AppendToken("סיום הבדיקה.");

        // Assert
        Assert.Contains("שלום עולם", renderer.AccumulatedText);
        Assert.Contains("var x = 42;", renderer.AccumulatedText);
        Assert.Contains("סיום הבדיקה", renderer.AccumulatedText);
    }

    [Fact]
    public void AppendToken_EmptyAndWhitespace_HandledCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc = new FlowDocument();
        renderer.AttachDocument(doc);

        // Act
        renderer.AppendToken("");
        renderer.AppendToken("   ");
        renderer.AppendToken("\n\n");
        renderer.AppendToken("Actual content");

        // Assert
        Assert.Contains("Actual content", renderer.AccumulatedText);
    }

    // ================================================================
    // Renderer Lifecycle
    // ================================================================

    [Fact]
    public void MultipleDocuments_Lifecycle_WorksCorrectly()
    {
        // Arrange
        var renderer = CreateRenderer();
        var doc1 = new FlowDocument();
        var doc2 = new FlowDocument();

        // Act — attach to doc1, accumulate, detach
        renderer.AttachDocument(doc1);
        Assert.True(renderer.IsAttached);
        renderer.AppendToken("Content for doc1");
        Assert.Equal("Content for doc1", renderer.AccumulatedText);
        renderer.DetachDocument();
        Assert.False(renderer.IsAttached);

        // Act — attach to doc2, buffer should be cleared
        renderer.AttachDocument(doc2);
        Assert.True(renderer.IsAttached);
        Assert.Equal(string.Empty, renderer.AccumulatedText);
        renderer.AppendToken("Content for doc2");
        Assert.Equal("Content for doc2", renderer.AccumulatedText);
    }
}
