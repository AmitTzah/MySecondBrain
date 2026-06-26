using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Repositories;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
