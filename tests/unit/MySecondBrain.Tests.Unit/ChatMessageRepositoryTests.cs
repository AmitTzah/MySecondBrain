using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using MySecondBrain.Data.Repositories;
using Message = MySecondBrain.Data.Entities.Message;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Unit;

public class ChatMessageRepositoryTests : DataLayerTestBase
{
    // ════════════════════════════════════════════════════════════════
    // ChatThreadRepository and MessageRepository
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ChatThreadRepository_CreateAsync_ReturnsCreatedThread()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var thread = new CoreModels.ChatThread { Title = "Test Thread" };
            var result = await repo.CreateAsync(thread);
            Assert.NotNull(result);
            Assert.Equal("Test Thread", result.Title);
            Assert.NotEmpty(result.Id);
            Assert.False(result.IsDeleted);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetByIdAsync_ReturnsCorrectThread()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Find Me" });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Find Me", found!.Title);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetAllPermanentAsync_FiltersTransientAndDeleted()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Permanent 1" });
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Permanent 2" });
            var transient = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Transient", IsTransient = true });
            var deleted = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Deleted", IsDeleted = true });
            var results = await repo.GetAllPermanentAsync(CoreModels.ChatSortOrder.LastActivityDesc);
            Assert.Equal(2, results.Count);
            Assert.True(results.All(t => t.Id != transient.Id));
            Assert.True(results.All(t => t.Id != deleted.Id));
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetAllPermanentAsync_SortsByCreatedAsc()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var t1 = await repo.CreateAsync(new CoreModels.ChatThread { Title = "B", CreatedAt = DateTimeOffset.UtcNow.AddHours(-2) });
            var t2 = await repo.CreateAsync(new CoreModels.ChatThread { Title = "A", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) });
            var results = await repo.GetAllPermanentAsync(CoreModels.ChatSortOrder.CreatedAsc);
            Assert.Equal(2, results.Count);
            Assert.Equal(t1.Id, results[0].Id);
            Assert.Equal(t2.Id, results[1].Id);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetAllPermanentAsync_SortsByTitleAsc()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Zebra" });
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Alpha" });
            var results = await repo.GetAllPermanentAsync(CoreModels.ChatSortOrder.TitleAsc);
            Assert.Equal(2, results.Count);
            Assert.Equal("Alpha", results[0].Title);
            Assert.Equal("Zebra", results[1].Title);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetTransientInWindowAsync_ReturnsRecentTransient()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var recent = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Recent Transient", IsTransient = true, CreatedAt = DateTimeOffset.UtcNow });
            var old = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Old Transient", IsTransient = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) });
            var results = await repo.GetTransientInWindowAsync();
            Assert.Contains(results, t => t.Id == recent.Id);
            Assert.True(results.All(t => t.Id != old.Id));
        }
    }

    [Fact]
    public async Task ChatThreadRepository_GetTrashAsync_ReturnsDeletedThreads()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Active" });
            var trashed = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Trashed" });
            await repo.SoftDeleteAsync(trashed.Id);
            var results = await repo.GetTrashAsync();
            Assert.Single(results);
            Assert.Equal("Trashed", results[0].Title);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_SearchAsync_FindsByTitle()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Machine Learning Discussion" });
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Random Chat" });
            await repo.CreateAsync(new CoreModels.ChatThread { Title = "Learning Path" });
            var results = await repo.SearchAsync("Learning", 10);
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_UpdateAsync_UpdatesTitle()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var thread = await repo.CreateAsync(new CoreModels.ChatThread { Title = "Original" });
            thread.Title = "Updated";
            await repo.UpdateAsync(thread);
            var updated = await repo.GetByIdAsync(thread.Id);
            Assert.Equal("Updated", updated!.Title);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_SoftDeleteAsync_SetsDeletedFlag()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var thread = await repo.CreateAsync(new CoreModels.ChatThread { Title = "To Delete" });
            await repo.SoftDeleteAsync(thread.Id);
            var deleted = await repo.GetByIdAsync(thread.Id);
            Assert.True(deleted!.IsDeleted);
        }
    }

    [Fact]
    public async Task ChatThreadRepository_PermanentDeleteAsync_RemovesThread()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var thread = await repo.CreateAsync(new CoreModels.ChatThread { Title = "To Purge" });
            await repo.PermanentDeleteAsync(thread.Id);
            Assert.Null(await repo.GetByIdAsync(thread.Id));
        }
    }

    [Fact]
    public async Task ChatThreadRepository_CleanupTransientAsync_DeletesOldAndElevatesExceptions()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var oldCleanEntity = new ChatThread { Title = "Old Clean", IsTransient = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-14) };
            var oldFavEntity = new ChatThread { Title = "Old Favorite", IsTransient = true, IsFavorite = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-14) };
            var recentEntity = new ChatThread { Title = "Recent", IsTransient = true, CreatedAt = DateTimeOffset.UtcNow };
            db.ChatThreads.AddRange(oldCleanEntity, oldFavEntity, recentEntity);
            await db.SaveChangesAsync();

            var deletedCount = await repo.CleanupTransientAsync(DateTimeOffset.UtcNow.AddDays(-7));
            Assert.Equal(1, deletedCount);

            var elevated = await db.ChatThreads.FindAsync(oldFavEntity.Id);
            Assert.NotNull(elevated);
            Assert.False(elevated!.IsTransient);

            Assert.Null(await db.ChatThreads.FindAsync(oldCleanEntity.Id));
            Assert.NotNull(await db.ChatThreads.FindAsync(recentEntity.Id));
        }
    }

    [Fact]
    public async Task ChatThreadRepository_PurgeTrashAsync_DeletesOldTrash()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ChatThreadRepository(db);
            var oldTrashEntity = new ChatThread { Title = "Old Trash", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow.AddDays(-60) };
            var recentTrashEntity = new ChatThread { Title = "Recent Trash", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow };
            db.ChatThreads.AddRange(oldTrashEntity, recentTrashEntity);
            await db.SaveChangesAsync();

            var deletedCount = await repo.PurgeTrashAsync(DateTimeOffset.UtcNow.AddDays(-30));
            Assert.Equal(1, deletedCount);
            Assert.NotNull(await db.ChatThreads.FindAsync(recentTrashEntity.Id));
            Assert.Null(await db.ChatThreads.FindAsync(oldTrashEntity.Id));
        }
    }

    [Fact]
    public async Task MessageRepository_CreateAsync_ReturnsCreatedMessage()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "Msg Thread" });
            var repo = new MessageRepository(db);
            var result = await repo.CreateAsync(new CoreModels.Message
            {
                ThreadId = thread.Id, Role = "User", Content = "Hello, world!",
                BranchId = Guid.NewGuid().ToString("N")
            });
            Assert.NotNull(result);
            Assert.Equal("Hello, world!", result.Content);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task MessageRepository_GetByIdAsync_ReturnsCorrectMessage()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "Thread" });
            var repo = new MessageRepository(db);
            var msg = await repo.CreateAsync(new CoreModels.Message
            {
                ThreadId = thread.Id, Role = "Assistant", Content = "I am an AI.",
                BranchId = Guid.NewGuid().ToString("N")
            });
            var found = await repo.GetByIdAsync(msg.Id);
            Assert.NotNull(found);
            Assert.Equal("Assistant", found!.Role);
        }
    }

    [Fact]
    public async Task MessageRepository_GetBranchAsync_ReturnsMessagesInBranch()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "Branch Thread" });
            var repo = new MessageRepository(db);
            var branchId = Guid.NewGuid().ToString("N");
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "V1", BranchId = branchId });
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "V2", BranchId = branchId });
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "Other", BranchId = Guid.NewGuid().ToString("N") });
            var results = await repo.GetBranchAsync(branchId);
            Assert.Equal(2, results.Count);
            Assert.All(results, m => Assert.Equal(branchId, m.BranchId));
        }
    }

    [Fact]
    public async Task MessageRepository_GetBranchCountAsync_CountsDistinctBranches()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "Count Thread" });
            var repo = new MessageRepository(db);
            var b1 = Guid.NewGuid().ToString("N");
            var b2 = Guid.NewGuid().ToString("N");
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "A", BranchId = b1 });
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "B", BranchId = b1 });
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "C", BranchId = b2 });
            Assert.Equal(2, await repo.GetBranchCountAsync(thread.Id));
        }
    }

    [Fact]
    public async Task MessageRepository_GetAllBranchesForThreadAsync_ReturnsAllMessages()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "All Branch Thread" });
            var repo = new MessageRepository(db);
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "M1", BranchId = Guid.NewGuid().ToString("N") });
            await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "Assistant", Content = "M2", BranchId = Guid.NewGuid().ToString("N") });
            Assert.Equal(2, (await repo.GetAllBranchesForThreadAsync(thread.Id)).Count);
        }
    }

    [Fact]
    public async Task MessageRepository_UpdateAsync_UpdatesContent()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "Update Thread" });
            var repo = new MessageRepository(db);
            var msg = await repo.CreateAsync(new CoreModels.Message { ThreadId = thread.Id, Role = "User", Content = "Original", BranchId = Guid.NewGuid().ToString("N") });
            msg.Content = "Modified";
            await repo.UpdateAsync(msg);
            Assert.Equal("Modified", (await repo.GetByIdAsync(msg.Id))!.Content);
        }
    }

    [Fact]
    public async Task MessageRepository_GetActiveBranchAsync_ReturnsActiveChain()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var threadRepo = new ChatThreadRepository(db);
            var thread = await threadRepo.CreateAsync(new CoreModels.ChatThread { Title = "Active Branch Thread" });

            var root = new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "User", Content = "Root message", BranchId = Guid.NewGuid().ToString("N"), IsActiveBranch = true, ParentMessageId = null };
            db.Messages.Add(root);
            var child = new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "Assistant", Content = "Child message", BranchId = Guid.NewGuid().ToString("N"), IsActiveBranch = true, ParentMessageId = root.Id };
            db.Messages.Add(child);
            var sibling = new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "Assistant", Content = "Inactive sibling", BranchId = Guid.NewGuid().ToString("N"), IsActiveBranch = false, ParentMessageId = root.Id };
            db.Messages.Add(sibling);
            await db.SaveChangesAsync();

            var repo = new MessageRepository(db);
            var chain = await repo.GetActiveBranchAsync(thread.Id);
            Assert.Equal(2, chain.Count);
            Assert.Equal("Root message", chain[0].Content);
            Assert.Equal("Child message", chain[1].Content);
        }
    }

    [Fact]
    public async Task MessageRepository_SearchAsync_FindsByFts5()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var thread = new ChatThread { Id = Guid.NewGuid().ToString("N"), ChatMode = "Standard", Title = "Search Thread" };
            db.ChatThreads.Add(thread);
            db.Messages.Add(new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "User", Content = "The quick brown fox jumps over the lazy dog", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            var repo = new MessageRepository(db);
            var results = await repo.SearchAsync("brown fox", 10);
            Assert.Single(results);
            Assert.Contains("brown fox", results[0].Content);
        }
    }

    [Fact]
    public async Task MessageRepository_SetActiveBranch_SwitchesActiveBranch()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var thread = new ChatThread { Id = Guid.NewGuid().ToString("N"), ChatMode = "Standard", Title = "Switch Branch Thread" };
            db.ChatThreads.Add(thread);

            var branchA = Guid.NewGuid().ToString("N");
            var branchB = Guid.NewGuid().ToString("N");

            var root = new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "User", Content = "Root", BranchId = branchA, IsActiveBranch = true, ParentMessageId = null };
            db.Messages.Add(root);
            db.Messages.Add(new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "Assistant", Content = "Branch A response", BranchId = branchA, IsActiveBranch = true, ParentMessageId = root.Id });
            db.Messages.Add(new Message { Id = Guid.NewGuid().ToString("N"), ThreadId = thread.Id, Role = "Assistant", Content = "Branch B response", BranchId = branchB, IsActiveBranch = false, ParentMessageId = root.Id });
            await db.SaveChangesAsync();

            var repo = new MessageRepository(db);
            var chainBefore = await repo.GetActiveBranchAsync(thread.Id);
            Assert.Equal(2, chainBefore.Count);
            Assert.Contains(chainBefore, m => m.Content == "Branch A response");
            Assert.True(chainBefore.All(m => m.Content != "Branch B response"));

            await repo.SetActiveBranch(root.Id, branchB);

            var chainAfter = await repo.GetActiveBranchAsync(thread.Id);
            Assert.Equal(2, chainAfter.Count);
            Assert.Contains(chainAfter, m => m.Content == "Branch B response");
            Assert.True(chainAfter.All(m => m.Content != "Branch A response"));
        }
    }
}
