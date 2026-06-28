using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.Chat;

namespace MySecondBrain.Tests.Unit;

public class ChatTitleGeneratorTests
{
    private readonly Mock<ILLMProviderService> _llmServiceMock = new();
    private readonly Mock<ILogger<ChatTitleGenerator>> _loggerMock = new();

    private readonly Persona _persona = new()
    {
        Id = "persona-001",
        DisplayName = "General Assistant",
    };

    private readonly ModelConfiguration _config = new()
    {
        Id = "config-001",
        DisplayName = "GPT-4o",
        ProviderType = ProviderType.OpenAI,
        ModelIdentifier = "gpt-4o",
    };

    private ChatTitleGenerator CreateGenerator()
    {
        return new ChatTitleGenerator(_llmServiceMock.Object, _loggerMock.Object);
    }

    // ================================================================
    // Successful Title Generation
    // ================================================================

    [Fact]
    public async Task GenerateTitleAsync_ReturnsLLMTitle()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(
                "The Meaning of Life",
                null,
                null,
                "stop",
                new UsageInfo(20, 5, 25)));

        // Act
        var title = await generator.GenerateTitleAsync(
            "What is the meaning of life?",
            "42",
            _persona,
            _config,
            CancellationToken.None);

        // Assert
        Assert.Equal("The Meaning of Life", title);
    }

    [Fact]
    public async Task GenerateTitleAsync_StripsQuotes()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(
                "\"Hello World\"",
                null,
                null,
                "stop",
                new UsageInfo(20, 5, 25)));

        // Act
        var title = await generator.GenerateTitleAsync(
            "Hello World",
            "Response",
            _persona,
            _config,
            CancellationToken.None);

        // Assert — quotes stripped
        Assert.Equal("Hello World", title);
    }

    // ================================================================
    // Fallback on LLM Failure
    // ================================================================

    [Fact]
    public async Task GenerateTitleAsync_LLMFailure_FallsBackToFirst50Chars()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        // Use a message of 55 characters to exercise the [..50] truncation
        var longMessage = "What's the best way to learn programming in 2024 quickly?"; // 55 chars

        // Act
        var title = await generator.GenerateTitleAsync(
            longMessage,
            "Start with Python",
            _persona,
            _config,
            CancellationToken.None);

        // Assert — falls back to first 50 chars (truncation path exercised)
        Assert.Equal(50, title.Length);
        Assert.Equal("What's the best way to learn programming in 2024 q", title);
    }

    [Fact]
    public async Task GenerateTitleAsync_EmptyResponse_FallsBack()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(
                "",
                null,
                null,
                "stop",
                new UsageInfo(20, 5, 25)));

        // Act
        var title = await generator.GenerateTitleAsync(
            "Hello",
            "Hi there!",
            _persona,
            _config,
            CancellationToken.None);

        // Assert — empty response triggers fallback
        Assert.Equal("Hello", title);
    }

    // ================================================================
    // Edge Cases
    // ================================================================

    [Fact]
    public async Task GenerateTitleAsync_EmptyUserMessage_FallsBackToNewChat()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No LLM"));

        // Act
        var title = await generator.GenerateTitleAsync(
            "",
            "",
            _persona,
            _config,
            CancellationToken.None);

        // Assert
        Assert.Equal("New Chat", title);
    }

    [Fact]
    public async Task GenerateTitleAsync_WhitespaceMessage_FallsBackToNewChat()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No LLM"));

        // Act
        var title = await generator.GenerateTitleAsync(
            "   ",
            "",
            _persona,
            _config,
            CancellationToken.None);

        // Assert
        Assert.Equal("New Chat", title);
    }

    [Fact]
    public async Task GenerateTitleAsync_TitleExceeds100Chars_FallsBack()
    {
        // Arrange
        var generator = CreateGenerator();
        var longTitle = new string('A', 101);
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(
                longTitle,
                null,
                null,
                "stop",
                new UsageInfo(20, 5, 25)));

        // Act
        var title = await generator.GenerateTitleAsync(
            "Short message",
            "Short response",
            _persona,
            _config,
            CancellationToken.None);

        // Assert — over 100 chars triggers fallback
        Assert.Equal("Short message", title);
    }

    [Fact]
    public async Task GenerateTitleAsync_ShortUserMessage_FallbackShorterThan50()
    {
        // Arrange
        var generator = CreateGenerator();
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No LLM"));

        // Act
        var title = await generator.GenerateTitleAsync(
            "Hi",
            "Hello!",
            _persona,
            _config,
            CancellationToken.None);

        // Assert — short message returns full message
        Assert.Equal("Hi", title);
    }
}
