using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Services.Chat;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Unit;

public class ChatThreadServiceTests : IDisposable
{
    private readonly Mock<IChatThreadRepository> _threadRepoMock = new();
    private readonly Mock<IMessageRepository> _messageRepoMock = new();
    private readonly Mock<ILLMProviderService> _llmServiceMock = new();
    private readonly Mock<IPersonaRepository> _personaRepoMock = new();
    private readonly Mock<IModelConfigurationRepository> _modelConfigRepoMock = new();
    private readonly Mock<IUsageRepository> _usageRepoMock = new();
    private readonly AppDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly ChatTitleGenerator _titleGenerator;
    private readonly Mock<ILogger<ChatTitleGenerator>> _titleLoggerMock = new();
    private readonly Mock<ILogger<ChatThreadLifecycleService>> _lifecycleLoggerMock = new();
    private readonly Mock<ILogger<ChatMessageService>> _messageLoggerMock = new();
    private readonly Mock<ILogger<ChatBranchService>> _branchLoggerMock = new();
    private readonly Mock<ILogger<ChatDraftService>> _draftLoggerMock = new();
    private ChatThreadService? _service;

    private readonly Persona _defaultPersona = new()
    {
        Id = "persona-001",
        DisplayName = "General Assistant",
        SystemPrompt = "You are a helpful assistant.",
        DefaultModelConfigId = "config-001",
        DefaultChatMode = "Standard",
    };

    private readonly ModelConfiguration _defaultModelConfig = new()
    {
        Id = "config-001",
        DisplayName = "GPT-4o",
        ProviderType = ProviderType.OpenAI,
        ModelIdentifier = "gpt-4o",
        Temperature = 1.0,
        MaxOutputTokens = 4096,
        MaxContextWindow = 128000,
    };

    public ChatThreadServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _titleGenerator = new ChatTitleGenerator(_llmServiceMock.Object, _titleLoggerMock.Object);
    }

    private ChatThreadService CreateService()
    {
        var lifecycle = new ChatThreadLifecycleService(
            _threadRepoMock.Object,
            _personaRepoMock.Object,
            _modelConfigRepoMock.Object,
            _lifecycleLoggerMock.Object);

        var messages = new ChatMessageService(
            _threadRepoMock.Object,
            _messageRepoMock.Object,
            _llmServiceMock.Object,
            _usageRepoMock.Object,
            _titleGenerator,
            lifecycle,
            _messageLoggerMock.Object);

        var branches = new ChatBranchService(
            _messageRepoMock.Object,
            _branchLoggerMock.Object);

        var drafts = new ChatDraftService(
            _db,
            _draftLoggerMock.Object);

        _service = new ChatThreadService(lifecycle, messages, branches, drafts);
        return _service;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    // ================================================================
    // CreateThreadAsync
    // ================================================================

    [Fact]
    public async Task CreateThreadAsync_WithPersona_CreatesThreadWithDefaults()
    {
        // Arrange
        var service = CreateService();
        _threadRepoMock.Setup(r => r.CreateAsync(It.IsAny<ChatThread>()))
            .ReturnsAsync((ChatThread t) => t);

        // Act
        var result = await service.CreateThreadAsync("Test Chat", false, _defaultPersona);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Chat", result.Title);
        Assert.False(result.IsTransient);
        Assert.Equal(_defaultPersona.Id, result.PersonaId);
        Assert.Equal(_defaultPersona.DefaultModelConfigId, result.ModelConfigId);
        Assert.NotEmpty(result.Id);

        _threadRepoMock.Verify(r => r.CreateAsync(It.IsAny<ChatThread>()), Times.Once);
    }

    [Fact]
    public async Task CreateThreadAsync_Transient_CreatesTransientThread()
    {
        // Arrange
        var service = CreateService();
        _threadRepoMock.Setup(r => r.CreateAsync(It.IsAny<ChatThread>()))
            .ReturnsAsync((ChatThread t) => t);

        // Act
        var result = await service.CreateThreadAsync(null, true, _defaultPersona);

        // Assert
        Assert.True(result.IsTransient);
        Assert.Null(result.Title);
    }

    // ================================================================
    // SendMessageAsync
    // ================================================================

    [Fact]
    public async Task SendMessageAsync_CreatesUserAndAssistantCoreMessages()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-001";
        var thread = new ChatThread { Id = threadId, PersonaId = _defaultPersona.Id };

        _threadRepoMock.Setup(r => r.GetByIdAsync(threadId)).ReturnsAsync(thread);
        _personaRepoMock.Setup(r => r.GetByIdAsync(_defaultPersona.Id)).ReturnsAsync(_defaultPersona);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync(_defaultModelConfig.Id)).ReturnsAsync(_defaultModelConfig);
        _messageRepoMock.Setup(r => r.CreateAsync(It.IsAny<CoreModels.Message>())).ReturnsAsync((CoreModels.Message m) => m);
        _messageRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CoreModels.Message>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.GetActiveBranchAsync(threadId)).ReturnsAsync(new List<CoreModels.Message>());
        _threadRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ChatThread>())).Returns(Task.CompletedTask);
        _usageRepoMock.Setup(r => r.RecordUsageAsync(It.IsAny<UsageRecord>())).Returns(Task.CompletedTask);

        // Setup streaming LLM response
        var streamChunks = GetStreamChunks("Hello! I'm an AI assistant.", "stop");
        _llmServiceMock.Setup(s => s.ChatStreamAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamChunks);

        // Act
        var result = await service.SendMessageAsync(threadId, "Hello!", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Assistant", result.Role);
        Assert.Equal("Hello! I'm an AI assistant.", result.Content);
        Assert.Equal(threadId, result.ThreadId);
        Assert.True(result.GenerationTimeMs >= 0);

        // Verify user message was created
        _messageRepoMock.Verify(r => r.CreateAsync(It.Is<CoreModels.Message>(m =>
            m.Role == "User" && m.Content == "Hello!")), Times.Once);

        // Verify assistant message was created
        _messageRepoMock.Verify(r => r.CreateAsync(It.Is<CoreModels.Message>(m =>
            m.Role == "Assistant")), Times.Once);

        // Verify thread activity updated
        _threadRepoMock.Verify(r => r.UpdateAsync(It.Is<ChatThread>(t =>
            t.Id == threadId)), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendMessageAsync_Cancelled_PreservesPartialResponse()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-002";
        var thread = new ChatThread { Id = threadId, PersonaId = _defaultPersona.Id };

        _threadRepoMock.Setup(r => r.GetByIdAsync(threadId)).ReturnsAsync(thread);
        _personaRepoMock.Setup(r => r.GetByIdAsync(_defaultPersona.Id)).ReturnsAsync(_defaultPersona);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync(_defaultModelConfig.Id)).ReturnsAsync(_defaultModelConfig);
        _messageRepoMock.Setup(r => r.CreateAsync(It.IsAny<CoreModels.Message>())).ReturnsAsync((CoreModels.Message m) => m);
        _messageRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CoreModels.Message>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.GetActiveBranchAsync(threadId)).ReturnsAsync(new List<CoreModels.Message>());
        _threadRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ChatThread>())).Returns(Task.CompletedTask);
        _usageRepoMock.Setup(r => r.RecordUsageAsync(It.IsAny<UsageRecord>())).Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Setup streaming that throws OperationCanceledException after yielding partial content
        _llmServiceMock.Setup(s => s.ChatStreamAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => CancellingStream("Partial response...", cts));

        // Act — pass the token so the service can observe cancellation
        var result = await service.SendMessageAsync(threadId, "Hello!", cts.Token);

        // Assert — partial content preserved even though OperationCanceledException was thrown
        Assert.NotNull(result);
        Assert.Equal("Partial response...", result.Content);
    }

    [Fact]
    public async Task SendMessageAsync_WithAutoTitle_SetsTitleOnFirstMessage()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-003";
        var thread = new ChatThread { Id = threadId, PersonaId = _defaultPersona.Id, Title = null };

        _threadRepoMock.Setup(r => r.GetByIdAsync(threadId)).ReturnsAsync(thread);
        _personaRepoMock.Setup(r => r.GetByIdAsync(_defaultPersona.Id)).ReturnsAsync(_defaultPersona);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync(_defaultModelConfig.Id)).ReturnsAsync(_defaultModelConfig);
        _messageRepoMock.Setup(r => r.CreateAsync(It.IsAny<CoreModels.Message>())).ReturnsAsync((CoreModels.Message m) => m);
        _messageRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CoreModels.Message>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.GetActiveBranchAsync(threadId)).ReturnsAsync(new List<CoreModels.Message>());
        _threadRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ChatThread>())).Returns(Task.CompletedTask);
        _usageRepoMock.Setup(r => r.RecordUsageAsync(It.IsAny<UsageRecord>())).Returns(Task.CompletedTask);

        // Setup LLM streaming
        var streamChunks = GetStreamChunks("42", "stop");
        _llmServiceMock.Setup(s => s.ChatStreamAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamChunks);

        // Title generator calls ChatAsync which throws -> fallback to first 50 chars
        _llmServiceMock.Setup(s => s.ChatAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        // Act
        var result = await service.SendMessageAsync(threadId, "What is the meaning of life?", CancellationToken.None);

        // Assert — title falls back to first 50 chars of user message
        Assert.NotNull(thread.Title);
        Assert.Equal("What is the meaning of life?", thread.Title);
    }

    // ================================================================
    // GetActiveBranchMessagesAsync
    // ================================================================

    [Fact]
    public async Task GetActiveBranchMessagesAsync_ReturnsOrderedMessages()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-004";
        var messages = new List<CoreModels.Message>
        {
            new() { Id = "m1", ThreadId = threadId, Role = "User", Content = "Hello", CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-10) },
            new() { Id = "m2", ThreadId = threadId, Role = "Assistant", Content = "Hi there!", CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-5) },
        };

        _messageRepoMock.Setup(r => r.GetActiveBranchAsync(threadId)).ReturnsAsync(messages);

        // Act
        var result = await service.GetActiveBranchMessagesAsync(threadId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Hello", result[0].Content);
        Assert.Equal("Hi there!", result[1].Content);
    }

    // ================================================================
    // RegenerateAsync
    // ================================================================

    [Fact]
    public async Task RegenerateAsync_CreatesNewVersionAndCallsLLM()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-005";
        var messageId = "msg-001";
        var existingMsg = new CoreModels.Message
        {
            Id = messageId,
            ThreadId = threadId,
            Role = "Assistant",
            Content = "Old response",
            VersionNumber = 1,
            BranchId = "branch-001",
            ParentMessageId = "user-msg-001",
        };
        var thread = new ChatThread { Id = threadId, PersonaId = _defaultPersona.Id };

        _messageRepoMock.Setup(r => r.GetByIdAsync(messageId)).ReturnsAsync(existingMsg);
        _threadRepoMock.Setup(r => r.GetByIdAsync(threadId)).ReturnsAsync(thread);
        _personaRepoMock.Setup(r => r.GetByIdAsync(_defaultPersona.Id)).ReturnsAsync(_defaultPersona);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync(_defaultModelConfig.Id)).ReturnsAsync(_defaultModelConfig);
        _messageRepoMock.Setup(r => r.CreateAsync(It.IsAny<CoreModels.Message>())).ReturnsAsync((CoreModels.Message m) => m);
        _messageRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CoreModels.Message>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.GetActiveBranchAsync(threadId)).ReturnsAsync(new List<CoreModels.Message>());
        _threadRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ChatThread>())).Returns(Task.CompletedTask);
        _usageRepoMock.Setup(r => r.RecordUsageAsync(It.IsAny<UsageRecord>())).Returns(Task.CompletedTask);

        var streamChunks = GetStreamChunks("Regenerated response", "stop");
        _llmServiceMock.Setup(s => s.ChatStreamAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamChunks);

        // Act
        var result = await service.RegenerateAsync(messageId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Regenerated response", result.Content);
        Assert.Equal(2, result.VersionNumber); // incremented
        Assert.Equal("branch-001", result.BranchId);
        Assert.True(result.IsActiveBranch);

        // Old message deactivated
        Assert.False(existingMsg.IsActiveBranch);
    }

    [Fact]
    public async Task RegenerateAsync_NonAssistantMessage_Throws()
    {
        // Arrange
        var service = CreateService();
        var userMsg = new CoreModels.Message { Id = "msg-001", Role = "User", Content = "Hello" };
        _messageRepoMock.Setup(r => r.GetByIdAsync("msg-001")).ReturnsAsync(userMsg);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegenerateAsync("msg-001", CancellationToken.None));
    }

    // ================================================================
    // ContinueGenerationAsync
    // ================================================================

    [Fact]
    public async Task ContinueGenerationAsync_AppendsToLastAssistantMessage()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-006";
        var lastMsg = new CoreModels.Message
        {
            Id = "msg-last",
            ThreadId = threadId,
            Role = "Assistant",
            Content = "First part",
            BranchId = "branch-001",
        };
        var thread = new ChatThread { Id = threadId, PersonaId = _defaultPersona.Id };

        _threadRepoMock.Setup(r => r.GetByIdAsync(threadId)).ReturnsAsync(thread);
        _messageRepoMock.Setup(r => r.GetActiveBranchAsync(threadId)).ReturnsAsync(new List<CoreModels.Message> { lastMsg });
        _personaRepoMock.Setup(r => r.GetByIdAsync(_defaultPersona.Id)).ReturnsAsync(_defaultPersona);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync(_defaultModelConfig.Id)).ReturnsAsync(_defaultModelConfig);
        _messageRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CoreModels.Message>())).Returns(Task.CompletedTask);
        _threadRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ChatThread>())).Returns(Task.CompletedTask);
        _usageRepoMock.Setup(r => r.RecordUsageAsync(It.IsAny<UsageRecord>())).Returns(Task.CompletedTask);

        var streamChunks = GetStreamChunks(" continued text", "stop");
        _llmServiceMock.Setup(s => s.ChatStreamAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamChunks);

        // Act
        var result = await service.ContinueGenerationAsync(threadId, CancellationToken.None);

        // Assert — the old content is preserved and new content appended
        Assert.Equal("First part continued text", result.Content);
    }

    // ================================================================
    // ElevateToPermanentAsync
    // ================================================================

    [Fact]
    public async Task ElevateToPermanentAsync_FlipsTransientFlag()
    {
        // Arrange
        var service = CreateService();
        var thread = new ChatThread { Id = "thread-010", IsTransient = true };
        _threadRepoMock.Setup(r => r.GetByIdAsync("thread-010")).ReturnsAsync(thread);
        _threadRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ChatThread>())).Returns(Task.CompletedTask);

        // Act
        await service.ElevateToPermanentAsync("thread-010");

        // Assert
        Assert.False(thread.IsTransient);
        _threadRepoMock.Verify(r => r.UpdateAsync(It.Is<ChatThread>(t => !t.IsTransient)), Times.Once);
    }

    // ================================================================
    // SaveDraftAsync / GetDraftAsync / DeleteDraftAsync
    // ================================================================

    [Fact]
    public async Task SaveDraftAndGetDraft_PersistsAndRetrieves()
    {
        // Arrange
        var service = CreateService();

        // Act — save draft
        await service.SaveDraftAsync("thread-draft-1", "Hello, this is a draft!", 5);
        var draft = await service.GetDraftAsync("thread-draft-1");

        // Assert
        Assert.NotNull(draft);
        Assert.Equal("thread-draft-1", draft.ThreadId);
        Assert.Equal("Hello, this is a draft!", draft.Content);
        Assert.Equal(5, draft.CursorPosition);
    }

    [Fact]
    public async Task SaveDraftAsync_UpsertsExistingDraft()
    {
        // Arrange
        var service = CreateService();
        await service.SaveDraftAsync("thread-draft-2", "First draft", 0);

        // Act — upsert
        await service.SaveDraftAsync("thread-draft-2", "Updated draft", 10);
        var draft = await service.GetDraftAsync("thread-draft-2");

        // Assert
        Assert.NotNull(draft);
        Assert.Equal("Updated draft", draft.Content);
        Assert.Equal(10, draft.CursorPosition);
    }

    [Fact]
    public async Task DeleteDraftAsync_RemovesDraft()
    {
        // Arrange
        var service = CreateService();
        await service.SaveDraftAsync("thread-draft-3", "Draft to delete", 0);

        // Act
        await service.DeleteDraftAsync("thread-draft-3");
        var draft = await service.GetDraftAsync("thread-draft-3");

        // Assert
        Assert.Null(draft);
    }

    // ================================================================
    // GetChatTreeAsync
    // ================================================================

    [Fact]
    public async Task GetChatTreeAsync_ReturnsAllBranchesAsNodes()
    {
        // Arrange
        var service = CreateService();
        var threadId = "thread-tree-1";
        var messages = new List<CoreModels.Message>
        {
            new() { Id = "m1", ThreadId = threadId, Role = "User", Content = "Root", ParentMessageId = null, BranchId = "b1", IsActiveBranch = true },
            new() { Id = "m2", ThreadId = threadId, Role = "Assistant", Content = "Branch A response", ParentMessageId = "m1", BranchId = "b1", IsActiveBranch = true },
            new() { Id = "m3", ThreadId = threadId, Role = "Assistant", Content = "Branch B response", ParentMessageId = "m1", BranchId = "b2", IsActiveBranch = false },
        };

        _messageRepoMock.Setup(r => r.GetAllBranchesForThreadAsync(threadId)).ReturnsAsync(messages);

        // Act
        var result = await service.GetChatTreeAsync(threadId);

        // Assert
        Assert.Equal(threadId, result.ThreadId);
        Assert.Equal(3, result.Nodes.Count);
        Assert.Contains(result.Nodes, n => n.MessageId == "m1" && n.ParentMessageId == null);
        Assert.Contains(result.Nodes, n => n.ContentPreview == "Branch B response");
    }

    // ================================================================
    // SearchMessagesAsync
    // ================================================================

    [Fact]
    public async Task SearchMessagesAsync_ReturnsSearchResults()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<CoreModels.Message>
        {
            new() { Id = "m1", ThreadId = "t1", Role = "User", Content = "meaning of life", CreatedAt = DateTimeOffset.UtcNow },
        };

        _messageRepoMock.Setup(r => r.SearchAsync("meaning", 10)).ReturnsAsync(messages);

        // Act
        var results = await service.SearchMessagesAsync("meaning", 10);

        // Assert
        Assert.Single(results);
        Assert.Equal("m1", results[0].MessageId);
        Assert.Contains("meaning", results[0].Snippet);
        Assert.Equal("User", results[0].Role);
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static async IAsyncEnumerable<StreamChunk> GetStreamChunks(string fullText, string finishReason)
    {
        yield return new StreamChunk(fullText, null, null, null, null, false);
        yield return new StreamChunk(null, null, null, finishReason, new UsageInfo(10, fullText.Length, 10 + fullText.Length), true);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Simulates an LLM stream that yields partial content then cancels.
    /// </summary>
    private static async IAsyncEnumerable<StreamChunk> CancellingStream(
        string partialText,
        CancellationTokenSource cts,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new StreamChunk(partialText, null, null, null, null, false);
        cts.Cancel();
        // The next MoveNextAsync call will observe cancellation and throw
        await Task.Delay(1, ct);
    }
}
