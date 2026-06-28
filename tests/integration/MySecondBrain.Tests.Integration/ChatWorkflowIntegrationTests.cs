using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Data.Repositories;
using MySecondBrain.Services.Chat;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Integration;

/// <summary>
/// Integration tests for ChatThreadRepository + MessageRepository
/// using a real SQLite database with full migrations applied.
/// Tests the full create-thread → add-messages → query workflow.
/// </summary>
public class ChatWorkflowIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private bool _disposed;

    public ChatWorkflowIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.Migrate();
    }

    [Fact]
    public async Task FullWorkflow_CreateThread_AddMessages_QueryActiveBranch_Search()
    {
        var threadRepo = new ChatThreadRepository(_db);
        var msgRepo = new MessageRepository(_db);

        // ── Step 1: Create a ChatThread ──────────────────────────────
        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Integration Test Chat",
            IsFavorite = true,
            IsPinned = true,
            ColorLabel = "green",
            Tags = """["int-test", "workflow"]""",
            FolderId = "integration-folder"
        });

        Assert.NotNull(thread);
        Assert.NotEmpty(thread.Id);
        Assert.Equal("Integration Test Chat", thread.Title);
        Assert.True(thread.IsFavorite);
        Assert.True(thread.IsPinned);
        Assert.Equal("green", thread.ColorLabel);
        Assert.Equal("""["int-test", "workflow"]""", thread.Tags);
        Assert.Equal("integration-folder", thread.FolderId);

        // ── Step 2: Create 3 Messages in the thread ──────────────────
        var branchA = Guid.NewGuid().ToString("N");

        var msg1 = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "User",
            Content = "What is the meaning of life?",
            BranchId = branchA,
            IsActiveBranch = true,
            ParentMessageId = null
        });
        Assert.NotNull(msg1);
        Assert.Equal("What is the meaning of life?", msg1.Content);

        var msg2 = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "Assistant",
            Content = "42",
            BranchId = branchA,
            IsActiveBranch = true,
            ParentMessageId = msg1.Id,
            IsFavorited = true,
            ThinkingContent = "This is a well-known philosophical reference."
        });
        Assert.NotNull(msg2);
        Assert.True(msg2.IsFavorited);
        Assert.Equal("This is a well-known philosophical reference.", msg2.ThinkingContent);

        var msg3 = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "User",
            Content = "Can you elaborate?",
            BranchId = branchA,
            IsActiveBranch = true,
            ParentMessageId = msg2.Id
        });

        // ── Step 3: Retrieve via GetActiveBranchAsync ────────────────
        var activeBranch = await msgRepo.GetActiveBranchAsync(thread.Id);
        Assert.Equal(3, activeBranch.Count);
        Assert.Equal("What is the meaning of life?", activeBranch[0].Content);
        Assert.Equal("42", activeBranch[1].Content);
        Assert.True(activeBranch[1].IsFavorited);
        Assert.Equal("This is a well-known philosophical reference.", activeBranch[1].ThinkingContent);
        Assert.Equal("Can you elaborate?", activeBranch[2].Content);

        // ── Step 4: FTS5 search ──────────────────────────────────────
        var searchResults = await msgRepo.SearchAsync("meaning", 10);
        Assert.NotEmpty(searchResults);
        Assert.Contains(searchResults, m => m.Content.Contains("meaning"));

        // Also search for the assistant's response text (indexed in Content column)
        var answerResults = await msgRepo.SearchAsync("42", 10);
        Assert.NotEmpty(answerResults);

        // ── Step 5: Soft-delete thread and verify exclusion ──────────
        await threadRepo.SoftDeleteAsync(thread.Id);

        var deletedThread = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(deletedThread);
        Assert.True(deletedThread!.IsDeleted);

        var permanentThreads = await threadRepo.GetAllPermanentAsync(CoreModels.ChatSortOrder.LastActivityDesc);
        Assert.DoesNotContain(permanentThreads, t => t.Id == thread.Id);

        // ── Step 6: Verify thread still retrievable via GetTrashAsync ─
        var trash = await threadRepo.GetTrashAsync();
        Assert.Contains(trash, t => t.Id == thread.Id);
    }

    [Fact]
    public async Task BranchingWorkflow_CreateBranches_SwitchActiveBranch()
    {
        var threadRepo = new ChatThreadRepository(_db);
        var msgRepo = new MessageRepository(_db);

        // Create thread
        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Branching Test",
            IsFavorite = false,
            IsPinned = false
        });

        // Branch A (active) — root belongs to Branch A
        var branchA = Guid.NewGuid().ToString("N");
        var root = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "User",
            Content = "Root question",
            BranchId = branchA,
            IsActiveBranch = true,
            ParentMessageId = null
        });

        // Branch A child (active)
        var a1 = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "Assistant",
            Content = "Branch A answer",
            BranchId = branchA,
            IsActiveBranch = true,
            ParentMessageId = root.Id
        });

        // Branch B (inactive)
        var branchB = Guid.NewGuid().ToString("N");
        var b1 = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "Assistant",
            Content = "Branch B answer",
            BranchId = branchB,
            IsActiveBranch = false,
            ParentMessageId = root.Id
        });

        // Verify active branch is A
        var activeBefore = await msgRepo.GetActiveBranchAsync(thread.Id);
        Assert.Equal(2, activeBefore.Count);
        Assert.Contains(activeBefore, m => m.Content == "Branch A answer");
        Assert.DoesNotContain(activeBefore, m => m.Content == "Branch B answer");

        // Switch to branch B
        await msgRepo.SetActiveBranch(root.Id, branchB);

        // Verify active branch is now B
        var activeAfter = await msgRepo.GetActiveBranchAsync(thread.Id);
        Assert.Equal(2, activeAfter.Count);
        Assert.Contains(activeAfter, m => m.Content == "Branch B answer");
        Assert.DoesNotContain(activeAfter, m => m.Content == "Branch A answer");

        // Verify branch count
        Assert.Equal(2, await msgRepo.GetBranchCountAsync(thread.Id));
    }

    [Fact]
    public async Task ThreadWithLockFields_PersistsAndRetrieves()
    {
        var threadRepo = new ChatThreadRepository(_db);

        // Create thread with locking fields
        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Locked Chat",
            IsLocked = true,
            LockSalt = Convert.ToBase64String(new byte[16]),
            LockNonce = Convert.ToBase64String(new byte[12])
        });

        Assert.True(thread.IsLocked);
        Assert.NotNull(thread.LockSalt);
        Assert.NotNull(thread.LockNonce);

        // Retrieve and verify
        var retrieved = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved!.IsLocked);
        Assert.Equal(thread.LockSalt, retrieved.LockSalt);
        Assert.Equal(thread.LockNonce, retrieved.LockNonce);

        // Update to unlock
        retrieved.IsLocked = false;
        retrieved.LockSalt = null;
        retrieved.LockNonce = null;
        await threadRepo.UpdateAsync(retrieved);

        var unlocked = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(unlocked);
        Assert.False(unlocked!.IsLocked);
        Assert.Null(unlocked.LockSalt);
        Assert.Null(unlocked.LockNonce);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Service-Level Integration Tests (Step 2)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task SendMessage_FullFlow_CreatesMessagesAndUpdatesThread()
    {
        // Arrange
        var threadRepo = new ChatThreadRepository(_db);
        var msgRepo = new MessageRepository(_db);
        var llmMock = new Mock<ILLMProviderService>();
        var personaRepo = new Mock<IPersonaRepository>();
        var modelConfigRepo = new Mock<IModelConfigurationRepository>();
        var usageRepo = new Mock<IUsageRepository>();
        var titleGenMock = new Mock<ILogger<ChatTitleGenerator>>();
        var lifecycleLoggerMock = new Mock<ILogger<ChatThreadLifecycleService>>();
        var messageLoggerMock = new Mock<ILogger<ChatMessageService>>();
        var branchLoggerMock = new Mock<ILogger<ChatBranchService>>();
        var draftLoggerMock = new Mock<ILogger<ChatDraftService>>();

        var persona = new Persona
        {
            Id = "00000000000000000000000000000001",
            DisplayName = "General Assistant",
            SystemPrompt = "You are a helpful assistant.",
            DefaultChatMode = "Standard",
        };

        var modelConfig = new ModelConfiguration
        {
            Id = "config-int-test-001",
            DisplayName = "GPT-4o",
            ProviderType = ProviderType.OpenAI,
            ModelIdentifier = "gpt-4o",
            PricingInputPer1K = 0.01m,
            PricingOutputPer1K = 0.03m,
        };

        var titleGenerator = new ChatTitleGenerator(llmMock.Object, titleGenMock.Object);

        personaRepo.Setup(r => r.GetByIdAsync(persona.Id)).ReturnsAsync(persona);
        personaRepo.Setup(r => r.GetDefaultAsync()).ReturnsAsync(persona);
        modelConfigRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(modelConfig);
        modelConfigRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModelConfiguration> { modelConfig });
        usageRepo.Setup(r => r.RecordUsageAsync(It.IsAny<UsageRecord>())).Returns(Task.CompletedTask);

        // Setup streaming LLM response
        llmMock.Setup(s => s.ChatStreamAsync(
                It.IsAny<ChatThread>(),
                It.IsAny<string>(),
                It.IsAny<Persona>(),
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<IReadOnlyList<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((ChatThread t, string msg, Persona p, ModelConfiguration c, IReadOnlyList<ToolDefinition>? tools, IReadOnlyList<ChatMessage>? history, CancellationToken ct) =>
                GetStreamForTest(msg, ct));

        var lifecycle = new ChatThreadLifecycleService(
            threadRepo, personaRepo.Object,
            modelConfigRepo.Object, lifecycleLoggerMock.Object);

        var messages = new ChatMessageService(
            threadRepo, msgRepo, llmMock.Object,
            usageRepo.Object, titleGenerator,
            lifecycle, messageLoggerMock.Object);

        var branches = new ChatBranchService(msgRepo, branchLoggerMock.Object);
        var drafts = new ChatDraftService(_db, draftLoggerMock.Object);

        var service = new ChatThreadService(lifecycle, messages, branches, drafts);

        // Act — create thread
        var thread = await service.CreateThreadAsync("Integration Test Service Chat", false, persona);
        Assert.NotNull(thread);
        Assert.NotEmpty(thread.Id);

        // Act — send message
        var assistantMsg = await service.SendMessageAsync(
            thread.Id, "Hello, introduce yourself in one sentence.", CancellationToken.None);

        // Assert — assistant response is persisted with non-empty content
        Assert.NotNull(assistantMsg);
        Assert.Equal("Assistant", assistantMsg.Role);
        Assert.Equal(thread.Id, assistantMsg.ThreadId);
        Assert.False(string.IsNullOrEmpty(assistantMsg.Content));
        Assert.True(assistantMsg.GenerationTimeMs > 0);

        // Assert — thread activity updated
        var updatedThread = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.True(updatedThread!.LastActivityAt >= thread.LastActivityAt);

        // Assert — both messages exist in active branch
        var activeBranch = await msgRepo.GetActiveBranchAsync(thread.Id);
        Assert.Equal(2, activeBranch.Count);
        Assert.Equal("User", activeBranch[0].Role);
        Assert.Equal("Assistant", activeBranch[1].Role);
    }

    [Fact]
    public async Task DraftWorkflow_SaveGetDelete_WorksCorrectly()
    {
        // Arrange
        var threadRepo = new ChatThreadRepository(_db);
        var msgRepo = new MessageRepository(_db);
        var llmMock = new Mock<ILLMProviderService>();
        var personaRepo = new Mock<IPersonaRepository>();
        var modelConfigRepo = new Mock<IModelConfigurationRepository>();
        var usageRepo = new Mock<IUsageRepository>();
        var titleGenMock = new Mock<ILogger<ChatTitleGenerator>>();
        var lifecycleLoggerMock = new Mock<ILogger<ChatThreadLifecycleService>>();
        var messageLoggerMock = new Mock<ILogger<ChatMessageService>>();
        var branchLoggerMock = new Mock<ILogger<ChatBranchService>>();
        var draftLoggerMock = new Mock<ILogger<ChatDraftService>>();

        var titleGenerator = new ChatTitleGenerator(llmMock.Object, titleGenMock.Object);

        var lifecycle = new ChatThreadLifecycleService(
            threadRepo, personaRepo.Object,
            modelConfigRepo.Object, lifecycleLoggerMock.Object);

        var messages = new ChatMessageService(
            threadRepo, msgRepo, llmMock.Object,
            usageRepo.Object, titleGenerator,
            lifecycle, messageLoggerMock.Object);

        var branches = new ChatBranchService(msgRepo, branchLoggerMock.Object);
        var drafts = new ChatDraftService(_db, draftLoggerMock.Object);

        var service = new ChatThreadService(lifecycle, messages, branches, drafts);

        var threadId = "draft-test-thread-001";

        // Act — save draft
        await service.SaveDraftAsync(threadId, "Draft content", 5);
        var draft1 = await service.GetDraftAsync(threadId);

        // Assert — draft saved
        Assert.NotNull(draft1);
        Assert.Equal("Draft content", draft1.Content);
        Assert.Equal(5, draft1.CursorPosition);

        // Act — update draft
        await service.SaveDraftAsync(threadId, "Updated draft content", 10);
        var draft2 = await service.GetDraftAsync(threadId);

        // Assert — draft updated
        Assert.NotNull(draft2);
        Assert.Equal("Updated draft content", draft2.Content);
        Assert.Equal(10, draft2.CursorPosition);

        // Act — delete draft
        await service.DeleteDraftAsync(threadId);
        var draft3 = await service.GetDraftAsync(threadId);

        // Assert — draft deleted
        Assert.Null(draft3);
    }

    // ═══════════════════════════════════════════════════════════════
    //  QoL Feature Integration Tests (Step 9)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task FavoriteMessage_PersistsAndRetrieves()
    {
        var threadRepo = new ChatThreadRepository(_db);
        var msgRepo = new MessageRepository(_db);

        // Create thread
        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Favoriting Test",
            IsFavorite = false,
            IsPinned = false
        });

        // Create a message
        var branchId = Guid.NewGuid().ToString("N");
        var msg = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "User",
            Content = "Test message for favoriting",
            BranchId = branchId,
            IsActiveBranch = true,
            IsFavorited = false
        });

        // Verify initially not favorited
        Assert.False(msg.IsFavorited);

        // Toggle favorite on
        msg.IsFavorited = true;
        await msgRepo.UpdateAsync(msg);

        var retrieved = await msgRepo.GetByIdAsync(msg.Id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved!.IsFavorited);

        // Toggle favorite off
        retrieved.IsFavorited = false;
        await msgRepo.UpdateAsync(retrieved);

        var retrievedAgain = await msgRepo.GetByIdAsync(msg.Id);
        Assert.NotNull(retrievedAgain);
        Assert.False(retrievedAgain!.IsFavorited);
    }

    [Fact]
    public async Task PinAndFavoriteThread_PersistsAndRetrieves()
    {
        var threadRepo = new ChatThreadRepository(_db);

        // Create thread with pin and favorite flags
        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Pinned & Favorited Thread",
            IsFavorite = true,
            IsPinned = true
        });

        Assert.True(thread.IsFavorite);
        Assert.True(thread.IsPinned);

        // Retrieve and verify
        var retrieved = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved!.IsFavorite);
        Assert.True(retrieved.IsPinned);

        // Toggle off
        retrieved.IsFavorite = false;
        retrieved.IsPinned = false;
        await threadRepo.UpdateAsync(retrieved);

        var updated = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(updated);
        Assert.False(updated!.IsFavorite);
        Assert.False(updated.IsPinned);
    }

    [Fact]
    public async Task GetAllPermanentAsync_SortsByLastActivity()
    {
        var threadRepo = new ChatThreadRepository(_db);

        // Create threads with different activity times
        var thread1 = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Oldest Activity",
            IsFavorite = false,
            IsPinned = false
        });

        // Simulate time passing by creating threads with explicit ordering
        await Task.Delay(50);
        var thread2 = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Middle Activity",
            IsFavorite = false,
            IsPinned = false
        });

        await Task.Delay(50);
        var thread3 = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Recent Activity",
            IsFavorite = false,
            IsPinned = false
        });

        // Retrieve sorted by last activity descending (most recent first)
        var sorted = await threadRepo.GetAllPermanentAsync(CoreModels.ChatSortOrder.LastActivityDesc);
        var sortedTitles = sorted.Select(t => t.Title).ToList();

        // Most recently created should appear first
        var recentIndex = sortedTitles.IndexOf("Recent Activity");
        var middleIndex = sortedTitles.IndexOf("Middle Activity");
        var oldestIndex = sortedTitles.IndexOf("Oldest Activity");

        Assert.True(recentIndex < middleIndex, "Recent Activity should appear before Middle Activity");
        Assert.True(middleIndex < oldestIndex, "Middle Activity should appear before Oldest Activity");
    }

    [Fact]
    public async Task GetAllPermanentAsync_SortsByName()
    {
        var threadRepo = new ChatThreadRepository(_db);

        // Create threads with sortable names
        await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Alpha Thread",
            IsFavorite = false,
            IsPinned = false
        });

        await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Beta Thread",
            IsFavorite = false,
            IsPinned = false
        });

        await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Gamma Thread",
            IsFavorite = false,
            IsPinned = false
        });

        // Retrieve sorted by name ascending
        var sorted = await threadRepo.GetAllPermanentAsync(CoreModels.ChatSortOrder.TitleAsc);
        var sortedTitles = sorted.Select(t => t.Title)
            .Where(t => t is "Alpha Thread" or "Beta Thread" or "Gamma Thread")
            .ToList();

        Assert.Equal("Alpha Thread", sortedTitles[0]);
        Assert.Equal("Beta Thread", sortedTitles[1]);
        Assert.Equal("Gamma Thread", sortedTitles[2]);
    }

    [Fact]
    public async Task ThreadWithOrgFields_PersistsAndUpdates()
    {
        var threadRepo = new ChatThreadRepository(_db);

        // Create thread with organization fields
        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Organized Thread",
            IsFavorite = true,
            IsPinned = true,
            IsArchived = false,
            ColorLabel = "blue",
            Tags = """["important", "work"]""",
            FolderId = "project-folder"
        });

        Assert.True(thread.IsFavorite);
        Assert.True(thread.IsPinned);
        Assert.False(thread.IsArchived);
        Assert.Equal("blue", thread.ColorLabel);
        Assert.Equal("""["important", "work"]""", thread.Tags);
        Assert.Equal("project-folder", thread.FolderId);

        // Update organization fields
        thread.IsArchived = true;
        thread.ColorLabel = "red";
        thread.Tags = """["archived"]""";
        await threadRepo.UpdateAsync(thread);

        var updated = await threadRepo.GetByIdAsync(thread.Id);
        Assert.NotNull(updated);
        Assert.True(updated!.IsArchived);
        Assert.Equal("red", updated.ColorLabel);
        Assert.Equal("""["archived"]""", updated.Tags);
    }

    [Fact]
    public async Task MessageEdit_UpdatesContent()
    {
        var threadRepo = new ChatThreadRepository(_db);
        var msgRepo = new MessageRepository(_db);

        var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread
        {
            Title = "Message Editing Test",
            IsFavorite = false,
            IsPinned = false
        });

        var branchId = Guid.NewGuid().ToString("N");
        var msg = await msgRepo.CreateAsync(new CoreModels.Message
        {
            ThreadId = thread.Id,
            Role = "User",
            Content = "Original content",
            BranchId = branchId,
            IsActiveBranch = true
        });

        Assert.Equal("Original content", msg.Content);

        // Edit message
        msg.Content = "Edited content";
        msg.RawContent = "Original content";
        await msgRepo.UpdateAsync(msg);

        var retrieved = await msgRepo.GetByIdAsync(msg.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Edited content", retrieved!.Content);
        Assert.Equal("Original content", retrieved.RawContent);
    }

    /// <summary>
    /// Helper to produce a streaming response for integration tests.
    /// </summary>
    private static async IAsyncEnumerable<StreamChunk> GetStreamForTest(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var response = $"I am an AI assistant. You said: \"{userMessage}\"";
        yield return new StreamChunk(response, null, null, null, null, false);
        yield return new StreamChunk(null, null, null, "stop",
            new UsageInfo(15, response.Length, 15 + response.Length), true);
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
