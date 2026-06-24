using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using MySecondBrain.Data.Repositories;
using Message = MySecondBrain.Data.Entities.Message;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Unit;

public class SettingsWikiUsageRepositoryTests : DataLayerTestBase
{
    // Step 6 tests: SettingsRepository

    [Fact]
    public async Task SettingsRepository_GetAsync_ReturnsValue()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.Settings.Add(new AppSetting { Key = "theme", Value = "dark" });
            await db.SaveChangesAsync();

            var repo = new SettingsRepository(db);
            var value = await repo.GetAsync("theme");
            Assert.Equal("dark", value);
        }
    }

    [Fact]
    public async Task SettingsRepository_GetAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            Assert.Null(await repo.GetAsync("nonexistent"));
        }
    }

    [Fact]
    public async Task SettingsRepository_GetAsync_Typed_DeserializesJson()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.Settings.Add(new AppSetting { Key = "tool_settings", Value = "{\"AutoApproveBash\":false,\"AutoApproveTextEditor\":true,\"AutoApproveWebSearch\":true,\"AutoApproveWebFetch\":false,\"AutoApproveWikiSearch\":false,\"AutoApproveMemory\":false,\"AutoApproveSkillLoad\":false,\"AutoApproveAskUserInput\":false,\"AutoApprovePresentFiles\":false,\"AutoApproveImageSearch\":false,\"MaxConsecutiveAutoApprovals\":5}" });
            await db.SaveChangesAsync();

            var repo = new SettingsRepository(db);
            var result = await repo.GetAsync<CoreModels.ToolAutoApprovalSettings>("tool_settings");
            Assert.NotNull(result);
            Assert.True(result!.AutoApproveWebSearch);
            Assert.False(result.AutoApproveBash);
            Assert.True(result.AutoApproveTextEditor);
            Assert.False(result.AutoApproveWebFetch);
            Assert.False(result.AutoApproveWikiSearch);
            Assert.False(result.AutoApproveMemory);
            Assert.False(result.AutoApproveSkillLoad);
            Assert.False(result.AutoApproveAskUserInput);
            Assert.False(result.AutoApprovePresentFiles);
            Assert.False(result.AutoApproveImageSearch);
            Assert.Equal(5, result.MaxConsecutiveAutoApprovals);
        }
    }

    [Fact]
    public async Task SettingsRepository_SetAsync_CreatesNewSetting()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            await repo.SetAsync("new_key", "new_value");

            var retrieved = await repo.GetAsync("new_key");
            Assert.Equal("new_value", retrieved);
        }
    }

    [Fact]
    public async Task SettingsRepository_SetAsync_UpdatesExistingSetting()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            await repo.SetAsync("theme", "light");
            await repo.SetAsync("theme", "dark");

            var retrieved = await repo.GetAsync("theme");
            Assert.Equal("dark", retrieved);
        }
    }

    [Fact]
    public async Task SettingsRepository_SetAsync_Typed_SerializesJson()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            var settings = new CoreModels.ToolAutoApprovalSettings
            {
                AutoApproveBash = false,
                AutoApproveTextEditor = true,
                AutoApproveWebSearch = true,
                AutoApproveWebFetch = false,
                AutoApproveWikiSearch = false,
                AutoApproveMemory = false,
                AutoApproveSkillLoad = false,
                AutoApproveAskUserInput = false,
                AutoApprovePresentFiles = false,
                AutoApproveImageSearch = false,
                MaxConsecutiveAutoApprovals = 10
            };
            await repo.SetAsync("typed_key", settings);

            var result = await repo.GetAsync<CoreModels.ToolAutoApprovalSettings>("typed_key");
            Assert.NotNull(result);
            Assert.True(result!.AutoApproveWebSearch);
            Assert.False(result.AutoApproveBash);
            Assert.True(result.AutoApproveTextEditor);
            Assert.False(result.AutoApproveWebFetch);
            Assert.False(result.AutoApproveWikiSearch);
            Assert.False(result.AutoApproveMemory);
            Assert.False(result.AutoApproveSkillLoad);
            Assert.False(result.AutoApproveAskUserInput);
            Assert.False(result.AutoApprovePresentFiles);
            Assert.False(result.AutoApproveImageSearch);
            Assert.Equal(10, result.MaxConsecutiveAutoApprovals);
        }
    }

    [Fact]
    public async Task SettingsRepository_DeleteAsync_RemovesSetting()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            await repo.SetAsync("to_delete", "value");
            await repo.DeleteAsync("to_delete");

            Assert.Null(await repo.GetAsync("to_delete"));
        }
    }

    [Fact]
    public async Task SettingsRepository_DeleteAsync_NonexistentKey_DoesNotThrow()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            await repo.DeleteAsync("no_such_key");
            // Should not throw
        }
    }

    [Fact]
    public async Task SettingsRepository_GetAllAsync_ReturnsAllSettings()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new SettingsRepository(db);
            await repo.SetAsync("key1", "val1");
            await repo.SetAsync("key2", "val2");
            await repo.SetAsync("key3", "val3");

            var all = await repo.GetAllAsync();
            Assert.Equal(3, all.Count);
            Assert.Equal("val1", all["key1"]);
            Assert.Equal("val2", all["key2"]);
            Assert.Equal("val3", all["key3"]);
        }
    }

    [Fact]
    public async Task SettingsRepository_GetAsync_InvalidJson_ReturnsDefault()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.Settings.Add(new AppSetting { Key = "bad_json", Value = "{not valid json}" });
            await db.SaveChangesAsync();

            var repo = new SettingsRepository(db);
            var result = await repo.GetAsync<CoreModels.ToolAutoApprovalSettings>("bad_json");
            Assert.Null(result);
        }
    }

    // Step 6 tests: WikiIndexRepository

    [Fact]
    public async Task WikiIndexRepository_GetAllAsync_ReturnsAllFiles()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/a.md", FileName = "a.md" });
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/b.md", FileName = "b.md" });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var results = await repo.GetAllAsync();
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetByPathAsync_ReturnsCorrectFile()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/test.md",
                FileName = "test.md",
                H1Title = "Test Page"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var result = await repo.GetByPathAsync("/notes/test.md");
            Assert.NotNull(result);
            Assert.Equal("/notes/test.md", result!.RelativePath);
            Assert.Equal("Test Page", result.Title);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetByPathAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new WikiIndexRepository(db);
            Assert.Null(await repo.GetByPathAsync("/no/such/file.md"));
        }
    }

    [Fact]
    public async Task WikiIndexRepository_UpsertAsync_CreatesNewFile()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new WikiIndexRepository(db);
            var file = new CoreModels.WikiFile
            {
                RelativePath = "/notes/new.md",
                Title = "New Page",
                LastModified = DateTimeOffset.UtcNow
            };
            var result = await repo.UpsertAsync(file);
            Assert.NotNull(result);
            Assert.Equal("/notes/new.md", result.RelativePath);

            var retrieved = await repo.GetByPathAsync("/notes/new.md");
            Assert.NotNull(retrieved);
            Assert.Equal("New Page", retrieved!.Title);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_UpsertAsync_UpdatesExistingFile()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/existing.md",
                FileName = "existing.md",
                H1Title = "Old Title"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var updated = new CoreModels.WikiFile
            {
                RelativePath = "/notes/existing.md",
                Title = "New Title",
                LastModified = DateTimeOffset.UtcNow
            };
            await repo.UpsertAsync(updated);

            var retrieved = await repo.GetByPathAsync("/notes/existing.md");
            Assert.NotNull(retrieved);
            Assert.Equal("New Title", retrieved!.Title);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_DeleteAsync_RemovesFile()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/to_delete.md", FileName = "to_delete.md" });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            await repo.DeleteAsync("/notes/to_delete.md");

            Assert.Null(await repo.GetByPathAsync("/notes/to_delete.md"));
        }
    }

    [Fact]
    public async Task WikiIndexRepository_DeleteAsync_NonexistentFile_DoesNotThrow()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new WikiIndexRepository(db);
            await repo.DeleteAsync("/no/such/file.md");
            // Should not throw
        }
    }

    [Fact]
    public async Task WikiIndexRepository_SearchAsync_FindsByFts5()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/ai.md",
                FileName = "ai.md",
                Content = "Artificial intelligence and machine learning are transforming technology"
            });
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/cooking.md",
                FileName = "cooking.md",
                Content = "How to bake the perfect sourdough bread"
            });
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/deep-learning.md",
                FileName = "deep-learning.md",
                Content = "Deep learning is a subset of machine learning"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var results = await repo.SearchAsync("machine learning", 10);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, f => f.RelativePath == "/notes/ai.md");
            Assert.Contains(results, f => f.RelativePath == "/notes/deep-learning.md");
        }
    }

    [Fact]
    public async Task WikiIndexRepository_SearchAsync_NoMatch_ReturnsEmpty()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/test.md",
                FileName = "test.md",
                Content = "Just some random content"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var results = await repo.SearchAsync("zzzznonexistent", 10);
            Assert.Empty(results);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetBacklinksAsync_ReturnsLinkingFiles()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/target.md",
                FileName = "target.md",
                CrossLinksIn = "[\"/notes/a.md\",\"/notes/b.md\"]"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);

            // CrossLinksIn becomes Backlinks in domain model
            var target = await repo.GetByPathAsync("/notes/target.md");
            Assert.NotNull(target);
            Assert.Equal(2, target!.Backlinks.Count);
            Assert.Contains("/notes/a.md", target.Backlinks);
            Assert.Contains("/notes/b.md", target.Backlinks);

            // GetBacklinksAsync checks CrossLinksOut for links TO targetPath
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/linker.md",
                FileName = "linker.md",
                CrossLinksOut = "[\"/notes/target.md\"]"
            });
            await db.SaveChangesAsync();

            var backlinks = await repo.GetBacklinksAsync("/notes/target.md");
            Assert.Single(backlinks);
            Assert.Equal("/notes/linker.md", backlinks[0].RelativePath);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetRelatedSectionsAsync_ReturnsSharedLinkFiles()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // File A links to targets X and Y
            // File B links to target X (shared with A)
            // File C links to target Z (no overlap with A)
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/file_a.md",
                FileName = "file_a.md",
                CrossLinksOut = "[\"/notes/x.md\",\"/notes/y.md\"]"
            });
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/file_b.md",
                FileName = "file_b.md",
                CrossLinksOut = "[\"/notes/x.md\"]"
            });
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/file_c.md",
                FileName = "file_c.md",
                CrossLinksOut = "[\"/notes/z.md\"]"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var related = await repo.GetRelatedSectionsAsync("/notes/file_a.md", 10);
            Assert.Single(related);
            Assert.Equal("/notes/file_b.md", related[0].RelativePath);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetOrphansAsync_ReturnsFilesWithNoIncomingLinks()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/linked.md",
                FileName = "linked.md",
                CrossLinksIn = "[\"/notes/a.md\"]"
            });
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/orphan.md",
                FileName = "orphan.md",
                CrossLinksIn = null
            });
            db.WikiFiles.Add(new WikiFile
            {
                FilePath = "/notes/also_orphan.md",
                FileName = "also_orphan.md",
                CrossLinksIn = "[]"
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var orphans = await repo.GetOrphansAsync();
            Assert.Equal(2, orphans.Count);
            Assert.Contains(orphans, f => f.RelativePath == "/notes/orphan.md");
            Assert.Contains(orphans, f => f.RelativePath == "/notes/also_orphan.md");
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetSnapshotsAsync_ReturnsOrderedSnapshots()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/snapped.md", FileName = "snapped.md" });
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/other.md", FileName = "other.md" });
            await db.SaveChangesAsync();

            db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                WikiFilePath = "/notes/snapped.md",
                Content = "Version 1",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            });
            db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                WikiFilePath = "/notes/snapped.md",
                Content = "Version 2",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            });
            db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                WikiFilePath = "/notes/other.md",
                Content = "Other file",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var snapshots = await repo.GetSnapshotsAsync("/notes/snapped.md");
            Assert.Equal(2, snapshots.Count);
            Assert.Equal(1, snapshots[0].VersionNumber);
            Assert.Equal(2, snapshots[1].VersionNumber);
            Assert.Equal("Version 1", snapshots[0].Content);
            Assert.Equal("Version 2", snapshots[1].Content);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_CreateSnapshotAsync_CreatesSnapshot()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/snap_new.md", FileName = "snap_new.md" });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            var snapshot = new CoreModels.WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                FilePath = "/notes/snap_new.md",
                VersionNumber = 1,
                Content = "Snapshot content",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await repo.CreateSnapshotAsync(snapshot);

            var snapshots = await repo.GetSnapshotsAsync("/notes/snap_new.md");
            Assert.Single(snapshots);
            Assert.Equal("Snapshot content", snapshots[0].Content);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_GetSnapshotAsync_ReturnsCorrectVersion()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/versioned.md", FileName = "versioned.md" });
            await db.SaveChangesAsync();

            db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                WikiFilePath = "/notes/versioned.md",
                Content = "V1",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
            });
            db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                WikiFilePath = "/notes/versioned.md",
                Content = "V2",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            });
            db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
            {
                Id = Guid.NewGuid().ToString("N"),
                WikiFilePath = "/notes/versioned.md",
                Content = "V3",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            });
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);

            var v2 = await repo.GetSnapshotAsync("/notes/versioned.md", 2);
            Assert.NotNull(v2);
            Assert.Equal(2, v2!.VersionNumber);
            Assert.Equal("V2", v2.Content);

            var outOfRange = await repo.GetSnapshotAsync("/notes/versioned.md", 99);
            Assert.Null(outOfRange);

            var zero = await repo.GetSnapshotAsync("/notes/versioned.md", 0);
            Assert.Null(zero);
        }
    }

    [Fact]
    public async Task WikiIndexRepository_PruneSnapshotsAsync_EnforcesPerFileLimit()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            db.WikiFiles.Add(new WikiFile { FilePath = "/notes/prune_test.md", FileName = "prune_test.md" });
            await db.SaveChangesAsync();

            // Create 5 snapshots for a single file, limit to 3
            for (int i = 0; i < 5; i++)
            {
                db.WikiVersionSnapshots.Add(new WikiVersionSnapshot
                {
                    Id = Guid.NewGuid().ToString("N"),
                    WikiFilePath = "/notes/prune_test.md",
                    Content = $"Content {i}",
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-5 + i)
                });
            }
            await db.SaveChangesAsync();

            var repo = new WikiIndexRepository(db);
            await repo.PruneSnapshotsAsync("/notes/prune_test.md", 3, long.MaxValue);

            var remaining = await repo.GetSnapshotsAsync("/notes/prune_test.md");
            Assert.Equal(3, remaining.Count);
        }
    }

    // Step 6 tests: UsageRepository

    [Fact]
    public async Task UsageRepository_RecordUsageAsync_InsertsRecord()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Prerequisite records for FK constraints
            db.ChatThreads.Add(new ChatThread { Id = "thread-1", ChatMode = "Standard" });
            db.Messages.Add(new Message { Id = "msg-1", ThreadId = "thread-1", Role = "Assistant", Content = "test", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var record = new CoreModels.UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = "thread-1",
                MessageId = "msg-1",
                ModelIdentifier = "gpt-4o",
                ProviderType = CoreModels.ProviderType.OpenAI,
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await repo.RecordUsageAsync(record);

            var records = await db.UsageRecords.ToListAsync();
            Assert.Single(records);
            Assert.Equal("gpt-4o", records[0].ModelIdentifier);
            Assert.Equal(100, records[0].PromptTokens);
            Assert.Equal(50, records[0].CompletionTokens);
            Assert.Equal(150, records[0].TotalTokens);
        }
    }

    [Fact]
    public async Task UsageRepository_GetUsageAsync_FiltersByDateRange()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var now = DateTimeOffset.UtcNow;
            // Prerequisite records for FK constraints
            db.ChatThreads.Add(new ChatThread { Id = "thread-a", ChatMode = "Standard" });
            db.ChatThreads.Add(new ChatThread { Id = "thread-b", ChatMode = "Standard" });
            db.ChatThreads.Add(new ChatThread { Id = "thread-c", ChatMode = "Standard" });
            db.Messages.Add(new Message { Id = "msg-a", ThreadId = "thread-a", Role = "Assistant", Content = "a", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-b", ThreadId = "thread-b", Role = "Assistant", Content = "b", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-c", ThreadId = "thread-c", Role = "Assistant", Content = "c", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-a", ThreadId = "thread-a",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                CreatedAt = now.AddDays(-5)
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-b", ThreadId = "thread-b",
                Provider = "Anthropic", ModelIdentifier = "claude-3",
                CreatedAt = now.AddDays(-3)
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-c", ThreadId = "thread-c",
                Provider = "Google", ModelIdentifier = "gemini-pro",
                CreatedAt = now.AddDays(-1)
            });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var results = await repo.GetUsageAsync(now.AddDays(-4), now);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.ModelIdentifier == "claude-3");
            Assert.Contains(results, r => r.ModelIdentifier == "gemini-pro");
        }
    }

    [Fact]
    public async Task UsageRepository_GetSummaryAsync_AggregatesCorrectly()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var now = DateTimeOffset.UtcNow;
            db.ChatThreads.Add(new ChatThread { Id = "thread-1", ChatMode = "Standard" });
            db.Messages.Add(new Message { Id = "msg-1", ThreadId = "thread-1", Role = "Assistant", Content = "1", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-2", ThreadId = "thread-1", Role = "Assistant", Content = "2", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-1", ThreadId = "thread-1",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150,
                EstimatedCost = 0.001m, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-2", ThreadId = "thread-1",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                PromptTokens = 200, CompletionTokens = 100, TotalTokens = 300,
                EstimatedCost = 0.002m, CreatedAt = now
            });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var summary = await repo.GetSummaryAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(2, summary.TotalRequests);
            Assert.Equal(300, summary.TotalPromptTokens);
            Assert.Equal(150, summary.TotalCompletionTokens);
            Assert.Equal(450, summary.TotalTokens);
            Assert.Equal(0.003m, summary.EstimatedCost);
        }
    }

    [Fact]
    public async Task UsageRepository_GetSummaryAsync_EmptyRange_ReturnsZeros()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new UsageRepository(db);
            var summary = await repo.GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
            Assert.Equal(0, summary.TotalRequests);
            Assert.Equal(0, summary.TotalTokens);
            Assert.Equal(0, summary.EstimatedCost);
        }
    }

    [Fact]
    public async Task UsageRepository_GetByProviderAsync_GroupsCorrectly()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var now = DateTimeOffset.UtcNow;
            db.ChatThreads.Add(new ChatThread { Id = "thread-1", ChatMode = "Standard" });
            db.Messages.Add(new Message { Id = "msg-1", ThreadId = "thread-1", Role = "Assistant", Content = "1", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-2", ThreadId = "thread-1", Role = "Assistant", Content = "2", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-3", ThreadId = "thread-1", Role = "Assistant", Content = "3", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-1", ThreadId = "thread-1",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                TotalTokens = 100, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-2", ThreadId = "thread-1",
                Provider = "OpenAI", ModelIdentifier = "gpt-3.5",
                TotalTokens = 50, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-3", ThreadId = "thread-1",
                Provider = "Anthropic", ModelIdentifier = "claude-3",
                TotalTokens = 200, CreatedAt = now
            });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var results = await repo.GetByProviderAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(2, results.Count);

            var openAi = results.Single(r => r.ProviderType == CoreModels.ProviderType.OpenAI);
            Assert.Equal(2, openAi.RequestCount);
            Assert.Equal(150, openAi.TotalTokens);

            var anthropic = results.Single(r => r.ProviderType == CoreModels.ProviderType.Anthropic);
            Assert.Equal(1, anthropic.RequestCount);
            Assert.Equal(200, anthropic.TotalTokens);
        }
    }

    [Fact]
    public async Task UsageRepository_GetByModelAsync_GroupsCorrectly()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var now = DateTimeOffset.UtcNow;
            db.ChatThreads.Add(new ChatThread { Id = "thread-1", ChatMode = "Standard" });
            db.Messages.Add(new Message { Id = "msg-1", ThreadId = "thread-1", Role = "Assistant", Content = "1", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-2", ThreadId = "thread-1", Role = "Assistant", Content = "2", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-3", ThreadId = "thread-1", Role = "Assistant", Content = "3", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-1", ThreadId = "thread-1",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                TotalTokens = 500, EstimatedCost = 0.005m, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-2", ThreadId = "thread-1",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                TotalTokens = 300, EstimatedCost = 0.003m, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-3", ThreadId = "thread-1",
                Provider = "Anthropic", ModelIdentifier = "claude-3",
                TotalTokens = 100, EstimatedCost = 0.001m, CreatedAt = now
            });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var results = await repo.GetByModelAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(2, results.Count);

            var gpt4o = results.Single(r => r.ModelId == "gpt-4o");
            Assert.Equal(2, gpt4o.RequestCount);
            Assert.Equal(800, gpt4o.TotalTokens);
            Assert.Equal(0.008m, gpt4o.EstimatedCost);

            var claude = results.Single(r => r.ModelId == "claude-3");
            Assert.Equal(1, claude.RequestCount);
            Assert.Equal(100, claude.TotalTokens);
        }
    }

    [Fact]
    public async Task UsageRepository_GetByChatAsync_GroupsCorrectly()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var now = DateTimeOffset.UtcNow;
            db.ChatThreads.Add(new ChatThread { Id = "thread-a", Title = "Chat A", ChatMode = "Standard" });
            db.ChatThreads.Add(new ChatThread { Id = "thread-b", Title = "Chat B", ChatMode = "Standard" });
            db.Messages.Add(new Message { Id = "msg-1", ThreadId = "thread-a", Role = "Assistant", Content = "1", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-2", ThreadId = "thread-a", Role = "Assistant", Content = "2", BranchId = Guid.NewGuid().ToString("N") });
            db.Messages.Add(new Message { Id = "msg-3", ThreadId = "thread-b", Role = "Assistant", Content = "3", BranchId = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();

            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-1", ThreadId = "thread-a",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                TotalTokens = 100, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-2", ThreadId = "thread-a",
                Provider = "OpenAI", ModelIdentifier = "gpt-4o",
                TotalTokens = 200, CreatedAt = now
            });
            db.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MessageId = "msg-3", ThreadId = "thread-b",
                Provider = "Anthropic", ModelIdentifier = "claude-3",
                TotalTokens = 50, CreatedAt = now
            });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var results = await repo.GetByChatAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(2, results.Count);

            var chatA = results.Single(r => r.ChatThreadId == "thread-a");
            Assert.Equal("Chat A", chatA.ChatTitle);
            Assert.Equal(2, chatA.RequestCount);
            Assert.Equal(300, chatA.TotalTokens);

            var chatB = results.Single(r => r.ChatThreadId == "thread-b");
            Assert.Equal("Chat B", chatB.ChatTitle);
            Assert.Equal(1, chatB.RequestCount);
            Assert.Equal(50, chatB.TotalTokens);
        }
    }

    [Fact]
    public async Task UsageRepository_GetFeedbackSummaryAsync_CountsCorrectly()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var now = DateTimeOffset.UtcNow;
            var thread = new ChatThread { Id = Guid.NewGuid().ToString("N"), ChatMode = "Standard", Title = "Feedback Thread" };
            db.ChatThreads.Add(thread);
            await db.SaveChangesAsync();

            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = thread.Id,
                Role = "Assistant",
                Content = "Good response",
                Feedback = "thumbs_up",
                BranchId = Guid.NewGuid().ToString("N"),
                CreatedAt = now
            });
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = thread.Id,
                Role = "Assistant",
                Content = "Bad response",
                Feedback = "thumbs_down",
                BranchId = Guid.NewGuid().ToString("N"),
                CreatedAt = now
            });
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = thread.Id,
                Role = "Assistant",
                Content = "No feedback",
                Feedback = null,
                BranchId = Guid.NewGuid().ToString("N"),
                CreatedAt = now
            });
            await db.SaveChangesAsync();

            var repo = new UsageRepository(db);
            var result = await repo.GetFeedbackSummaryAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(1, result.PositiveCount);
            Assert.Equal(1, result.NegativeCount);
            Assert.Equal(0.5, result.AverageRating); // 1 positive / 2 rated = 0.5
        }
    }

    [Fact]
    public async Task UsageRepository_GetFeedbackSummaryAsync_NoFeedback_ReturnsZeros()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new UsageRepository(db);
            var result = await repo.GetFeedbackSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            Assert.Equal(0, result.PositiveCount);
            Assert.Equal(0, result.NegativeCount);
            Assert.Equal(0, result.AverageRating);
        }
    }
}
