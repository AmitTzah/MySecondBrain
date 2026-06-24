using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using Message = MySecondBrain.Data.Entities.Message;

namespace MySecondBrain.Tests.Unit;

public class DbContextSchemaTests : DataLayerTestBase
{
    // ════════════════════════════════════════════════════════════════
    // DbContext model validation, FK relationships, indexes, and seed data
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that EnsureCreated succeeds, confirming the model is valid.
    /// </summary>
    [Fact]
    public void DbContext_ModelValidation_EnsureCreatedSucceeds()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new AppDbContext(options);
        using (connection)
        {
            db.Database.EnsureCreated();

            // Verify all 15 entity types are mapped (BackupSnapshot deferred to W3.16)
            var entityTypes = db.Model.GetEntityTypes().ToList();
            Assert.Equal(15, entityTypes.Count);
        }
    }

    /// <summary>
    /// Verifies all 15 DbSet properties exist on AppDbContext.
    /// </summary>
    [Fact]
    public void DbContext_AllDbSets_Present()
    {
        var expectedDbSets = new[]
        {
            "ApiKeys", "Settings", "Artifacts", "ChatThreads", "MediaItems",
            "Messages", "MessageDrafts", "MemoryEntries", "ModelConfigurations", "Personas",
            "PromptTemplates", "TextActions", "UsageRecords",
            "WikiFiles", "WikiVersionSnapshots"
        };

        var dbSetProperties = typeof(AppDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name)
            .ToHashSet();

        Assert.Equal(15, dbSetProperties.Count);

        foreach (var expected in expectedDbSets)
        {
            Assert.True(dbSetProperties.Contains(expected),
                $"AppDbContext is missing DbSet<{expected}> property.");
        }
    }

    /// <summary>
    /// Verifies all FK relationships have correct OnDelete behavior.
    /// </summary>
    [Fact]
    public void DbContext_OnModelCreating_ForeignKeys_HaveCorrectDeleteBehavior()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var model = db.Model;

            // Helper: get FK delete behavior by entity type and FK property name
            DeleteBehavior GetDeleteBehavior(Type entityType, string fkPropertyName)
            {
                var entity = model.FindEntityType(entityType)!;
                var fk = entity.GetForeignKeys()
                    .Single(fk => fk.Properties.Any(p => p.Name == fkPropertyName));
                return fk.DeleteBehavior;
            }

            // ChatThread → Persona: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(ChatThread), nameof(ChatThread.PersonaId)));

            // ChatThread → ModelConfiguration: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(ChatThread), nameof(ChatThread.ModelConfigId)));

            // Message → ChatThread: Cascade
            Assert.Equal(DeleteBehavior.Cascade,
                GetDeleteBehavior(typeof(Message), nameof(Message.ThreadId)));

            // Message → Message (self): Restrict
            Assert.Equal(DeleteBehavior.Restrict,
                GetDeleteBehavior(typeof(Message), nameof(Message.ParentMessageId)));

            // Message → Persona: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(Message), nameof(Message.PersonaId)));

            // Message → ModelConfiguration: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(Message), nameof(Message.ModelConfigId)));

            // Persona → ModelConfiguration: Restrict
            Assert.Equal(DeleteBehavior.Restrict,
                GetDeleteBehavior(typeof(Persona), nameof(Persona.DefaultModelConfigId)));

            // ModelConfiguration → ApiKey: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(ModelConfiguration), nameof(ModelConfiguration.ApiKeyId)));

            // Artifact → ChatThread: Cascade
            Assert.Equal(DeleteBehavior.Cascade,
                GetDeleteBehavior(typeof(Artifact), nameof(Artifact.ThreadId)));

            // MediaItem → ChatThread: Cascade
            Assert.Equal(DeleteBehavior.Cascade,
                GetDeleteBehavior(typeof(MediaItem), nameof(MediaItem.ThreadId)));

            // MediaItem → Message: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(MediaItem), nameof(MediaItem.MessageId)));

            // UsageRecord → Message (1:1): Cascade
            Assert.Equal(DeleteBehavior.Cascade,
                GetDeleteBehavior(typeof(UsageRecord), nameof(UsageRecord.MessageId)));

            // UsageRecord → ChatThread: Cascade
            Assert.Equal(DeleteBehavior.Cascade,
                GetDeleteBehavior(typeof(UsageRecord), nameof(UsageRecord.ThreadId)));

            // UsageRecord → Persona: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(UsageRecord), nameof(UsageRecord.PersonaId)));

            // UsageRecord → ModelConfiguration: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(UsageRecord), nameof(UsageRecord.ModelConfigId)));

            // TextAction → ModelConfiguration: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(TextAction), nameof(TextAction.ModelConfigId)));

            // WikiVersionSnapshot → WikiFile: Cascade
            Assert.Equal(DeleteBehavior.Cascade,
                GetDeleteBehavior(typeof(WikiVersionSnapshot), nameof(WikiVersionSnapshot.WikiFilePath)));

            // MemoryEntry → ChatThread: SetNull
            Assert.Equal(DeleteBehavior.SetNull,
                GetDeleteBehavior(typeof(MemoryEntryEntity), nameof(MemoryEntryEntity.SourceThreadId)));

            // Verify total FK count across all entities
            var totalFks = model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys())
                .Count();
            Assert.Equal(18, totalFks);
        }
    }

    /// <summary>
    /// Verifies indexes are configured on frequently queried columns.
    /// </summary>
    [Fact]
    public void DbContext_OnModelCreating_Indexes_Configured()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var model = db.Model;

            // Message.ThreadId index
            var messageEntity = model.FindEntityType(typeof(Message))!;
            var threadIdIndex = messageEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(Message.ThreadId)));
            Assert.True(threadIdIndex, "Expected index on Message.ThreadId");

            // Message.CreatedAt index
            var createdAtIndex = messageEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(Message.CreatedAt)));
            Assert.True(createdAtIndex, "Expected index on Message.CreatedAt");

            // ChatThread indexes
            var chatThreadEntity = model.FindEntityType(typeof(ChatThread))!;

            var lastActivityAtIndex = chatThreadEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(ChatThread.LastActivityAt)));
            Assert.True(lastActivityAtIndex, "Expected index on ChatThread.LastActivityAt");

            var isTransientIndex = chatThreadEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(ChatThread.IsTransient)));
            Assert.True(isTransientIndex, "Expected index on ChatThread.IsTransient");

            var isDeletedIndex = chatThreadEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(ChatThread.IsDeleted)));
            Assert.True(isDeletedIndex, "Expected index on ChatThread.IsDeleted");

            // MemoryEntry indexes
            var memoryEntryEntity = model.FindEntityType(typeof(MemoryEntryEntity))!;

            var keyIndex = memoryEntryEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(MemoryEntryEntity.Key)));
            Assert.True(keyIndex, "Expected index on MemoryEntryEntity.Key");

            var memoryCreatedAtIndex = memoryEntryEntity.GetIndexes()
                .Any(i => i.Properties.Any(p => p.Name == nameof(MemoryEntryEntity.CreatedAt)));
            Assert.True(memoryCreatedAtIndex, "Expected index on MemoryEntryEntity.CreatedAt");
        }
    }

    /// <summary>
    /// Verifies seed data: 2 built-in Personas and 10 built-in TextActions.
    /// </summary>
    [Fact]
    public void DbContext_SeedData_Personas_And_TextActions_Present()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var personas = db.Personas.Where(p => p.IsBuiltIn).ToList();
            Assert.Equal(2, personas.Count);

            var generalAssistant = personas.Single(p => p.Id == "00000000000000000000000000000001");
            Assert.Equal("General Assistant", generalAssistant.DisplayName);
            Assert.Equal("You are a helpful, thoughtful assistant.", generalAssistant.SystemPrompt);
            Assert.Equal("Standard", generalAssistant.DefaultChatMode);
            Assert.True(generalAssistant.IsBuiltIn);

            var codeHelper = personas.Single(p => p.Id == "00000000000000000000000000000002");
            Assert.Equal("Code Helper", codeHelper.DisplayName);
            Assert.Contains("expert software developer", codeHelper.SystemPrompt);
            Assert.Equal("Standard", codeHelper.DefaultChatMode);
            Assert.True(codeHelper.IsBuiltIn);

            var textActions = db.TextActions.Where(ta => ta.IsBuiltIn).ToList();
            Assert.Equal(10, textActions.Count);

            // Verify each action by ID, DisplayName, CaptureScope, ApplyMode, and Hotkey
            var expectedActions = new (string Id, string DisplayName, string CaptureScope, string ApplyMode, string? Hotkey)[]
            {
                ("a000000000000000000000000000001", "Rewrite",             "selection",       "replaceSelection",       "Alt+Q"),
                ("a000000000000000000000000000002", "Summarize",           "selection",       "showOnly",               "Alt+W"),
                ("a000000000000000000000000000003", "Explain",             "selection",       "showOnly",               "Alt+E"),
                ("a000000000000000000000000000004", "Translate",           "selection",       "replaceSelection",       "Alt+R"),
                ("a000000000000000000000000000005", "Fix Grammar",         "selection",       "replaceSelection",       null),
                ("a000000000000000000000000000006", "Enhance Prompt",      "selection",       "replaceSelection",       null),
                ("a000000000000000000000000000007", "Continue Writing",    "focusedElement",  "insertAtCursor",         "Alt+C"),
                ("a000000000000000000000000000008", "Improve Flow",        "focusedElement",  "replaceFocusedElement",  null),
                ("a000000000000000000000000000009", "Summarize Page",      "fullDocument",    "showOnly",               null),
                ("a000000000000000000000000000010", "Explain Screen",      "fullDocument,screenshot", "showOnly",         null),
            };

            foreach (var (id, displayName, captureScope, applyMode, hotkey) in expectedActions)
            {
                var action = textActions.Single(ta => ta.Id == id);
                Assert.Equal(displayName, action.DisplayName);
                Assert.Equal(captureScope, action.CaptureScope);
                Assert.Equal(applyMode, action.ApplyMode);
                Assert.Equal(hotkey, action.Hotkey);
                Assert.True(action.IsBuiltIn);
                Assert.NotNull(action.SystemPrompt);
                Assert.NotEmpty(action.SystemPrompt);
            }
        }
    }

    /// <summary>
    /// Verifies MessageDrafts entity is registered and its table is created.
    /// </summary>
    [Fact]
    public void DbContext_MessageDrafts_TableExists()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var draft = new MessageDrafts
            {
                ThreadId = "test-thread-001",
                Content = "Hello, world!",
                CursorPosition = 13
            };
            db.MessageDrafts.Add(draft);
            db.SaveChanges();

            var retrieved = db.MessageDrafts.Find("test-thread-001");
            Assert.NotNull(retrieved);
            Assert.Equal("Hello, world!", retrieved!.Content);
            Assert.Equal(13, retrieved.CursorPosition);
        }
    }

    /// <summary>
    /// Verifies AppSetting entity is registered and its table is created.
    /// </summary>
    [Fact]
    public void DbContext_AppSettings_TableExists()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var setting = new AppSetting
            {
                Key = "theme",
                Value = "dark",
                ValueType = "String"
            };
            db.Settings.Add(setting);
            db.SaveChanges();

            var retrieved = db.Settings.Find("theme");
            Assert.NotNull(retrieved);
            Assert.Equal("dark", retrieved!.Value);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Migration, FTS5 virtual tables, and content-sync triggers
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that Migrate() succeeds and creates all 16 tables
    /// (14 entity tables + 2 FTS5 virtual tables).
    /// </summary>
    [Fact]
    public void Migration_Apply_AllTablesCreated()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            // Query sqlite_master to get all table names
            var tables = new List<string>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tables.Add(reader.GetString(0));

            // Verify all expected tables exist (14 entity tables + 2 FTS5 virtual tables).
            // Exact count varies by SQLite version due to FTS5 internal shadow tables.
            Assert.Contains("ApiKeys", tables);
            Assert.Contains("Settings", tables);
            Assert.Contains("Artifacts", tables);
            Assert.Contains("ChatThreads", tables);
            Assert.Contains("MediaItems", tables);
            Assert.Contains("Messages", tables);
            Assert.Contains("MessageDrafts", tables);
            Assert.Contains("ModelConfigurations", tables);
            Assert.Contains("Personas", tables);
            Assert.Contains("PromptTemplates", tables);
            Assert.Contains("TextActions", tables);
            Assert.Contains("UsageRecords", tables);
            Assert.Contains("WikiFiles", tables);
            Assert.Contains("WikiVersionSnapshots", tables);
            Assert.Contains("MemoryEntries", tables);
            Assert.Contains("MessageFts", tables);
            Assert.Contains("WikiFileFts", tables);
        }
    }

    /// <summary>
    /// Verifies FTS5 virtual tables exist and are queryable.
    /// </summary>
    [Fact]
    public void Migration_Fts5_VirtualTablesExist()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            // Verify FTS5 tables are registered as virtual tables
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND sql LIKE 'CREATE VIRTUAL TABLE%'";
            var ftsTables = new List<string>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    ftsTables.Add(reader.GetString(0));
            }

            Assert.Contains("MessageFts", ftsTables);
            Assert.Contains("WikiFileFts", ftsTables);

            // Verify tables are queryable (empty after migration)
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts";
            var messageCount = (long)cmd.ExecuteScalar()!;
            Assert.Equal(0, messageCount);

            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts";
            var wikiCount = (long)cmd.ExecuteScalar()!;
            Assert.Equal(0, wikiCount);
        }
    }

    /// <summary>
    /// Verifies FTS5 content-sync triggers: inserting a Message
    /// automatically populates the MessageFts index.
    /// </summary>
    [Fact]
    public void Migration_Fts5_ContentSync_MessageInsertTrigger()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            // Create a ChatThread first (FK required)
            var thread = new ChatThread
            {
                Id = "thread-fts-test-001",
                ChatMode = "Standard",
                Title = "FTS Test Thread"
            };
            db.ChatThreads.Add(thread);
            db.SaveChanges();

            // Insert a message
            var message = new Message
            {
                Id = "msg-fts-test-001",
                ThreadId = thread.Id,
                Role = "User",
                Content = "The quick brown fox jumps over the lazy dog",
                BranchId = Guid.NewGuid().ToString("N")
            };
            db.Messages.Add(message);
            db.SaveChanges();

            // FTS5 should now contain this message
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts WHERE MessageFts MATCH 'brown fox'";
            var count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// Verifies FTS5 content-sync triggers: updating a Message
    /// updates the FTS index.
    /// </summary>
    [Fact]
    public void Migration_Fts5_ContentSync_MessageUpdateTrigger()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var thread = new ChatThread
            {
                Id = "thread-fts-update-001",
                ChatMode = "Standard"
            };
            db.ChatThreads.Add(thread);
            db.SaveChanges();

            var message = new Message
            {
                Id = "msg-fts-update-001",
                ThreadId = thread.Id,
                Role = "User",
                Content = "Original content about cats",
                BranchId = Guid.NewGuid().ToString("N")
            };
            db.Messages.Add(message);
            db.SaveChanges();

            // Verify original content is indexed
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts WHERE MessageFts MATCH 'cats'";
            Assert.Equal(1, (long)cmd.ExecuteScalar()!);

            // Update the message content
            message.Content = "Updated content about dogs";
            db.SaveChanges();

            // Old content should no longer match
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts WHERE MessageFts MATCH 'cats'";
            Assert.Equal(0, (long)cmd.ExecuteScalar()!);

            // New content should match
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts WHERE MessageFts MATCH 'dogs'";
            Assert.Equal(1, (long)cmd.ExecuteScalar()!);
        }
    }

    /// <summary>
    /// Verifies FTS5 content-sync triggers: deleting a Message
    /// removes it from the FTS index.
    /// </summary>
    [Fact]
    public void Migration_Fts5_ContentSync_MessageDeleteTrigger()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var thread = new ChatThread
            {
                Id = "thread-fts-delete-001",
                ChatMode = "Standard"
            };
            db.ChatThreads.Add(thread);
            db.SaveChanges();

            var message = new Message
            {
                Id = "msg-fts-delete-001",
                ThreadId = thread.Id,
                Role = "User",
                Content = "Delete me please",
                BranchId = Guid.NewGuid().ToString("N")
            };
            db.Messages.Add(message);
            db.SaveChanges();

            // Verify it's in the FTS index
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts WHERE MessageFts MATCH 'Delete'";
            Assert.Equal(1, (long)cmd.ExecuteScalar()!);

            // Delete the message
            db.Messages.Remove(message);
            db.SaveChanges();

            // Should no longer be in FTS index
            cmd.CommandText = "SELECT COUNT(*) FROM MessageFts WHERE MessageFts MATCH 'Delete'";
            Assert.Equal(0, (long)cmd.ExecuteScalar()!);
        }
    }

    /// <summary>
    /// Verifies FTS5 content-sync triggers for WikiFiles.
    /// </summary>
    [Fact]
    public void Migration_Fts5_ContentSync_WikiFileInsertTrigger()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var wikiFile = new WikiFile
            {
                FilePath = "/notes/fts-test.md",
                FileName = "fts-test.md",
                Content = "This wiki page discusses machine learning and neural networks",
                H1Title = "Machine Learning"
            };
            db.WikiFiles.Add(wikiFile);
            db.SaveChanges();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts WHERE WikiFileFts MATCH 'machine learning'";
            var count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// Verifies FTS5 content-sync triggers: updating a WikiFile
    /// updates the FTS index.
    /// </summary>
    [Fact]
    public void Migration_Fts5_ContentSync_WikiFileUpdateTrigger()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var wikiFile = new WikiFile
            {
                FilePath = "/notes/fts-update-test.md",
                FileName = "fts-update-test.md",
                Content = "Original wiki content about Python programming",
                H1Title = "Python"
            };
            db.WikiFiles.Add(wikiFile);
            db.SaveChanges();

            // Verify original content is indexed
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts WHERE WikiFileFts MATCH 'Python'";
            Assert.Equal(1, (long)cmd.ExecuteScalar()!);

            // Update the wiki file content
            wikiFile.Content = "Updated wiki content about Rust programming";
            db.SaveChanges();

            // Old content should no longer match
            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts WHERE WikiFileFts MATCH 'Python'";
            Assert.Equal(0, (long)cmd.ExecuteScalar()!);

            // New content should match
            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts WHERE WikiFileFts MATCH 'Rust'";
            Assert.Equal(1, (long)cmd.ExecuteScalar()!);
        }
    }

    /// <summary>
    /// Verifies FTS5 content-sync triggers: deleting a WikiFile
    /// removes it from the FTS index.
    /// </summary>
    [Fact]
    public void Migration_Fts5_ContentSync_WikiFileDeleteTrigger()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var wikiFile = new WikiFile
            {
                FilePath = "/notes/fts-delete-test.md",
                FileName = "fts-delete-test.md",
                Content = "Delete me from the wiki",
                H1Title = "Delete Test"
            };
            db.WikiFiles.Add(wikiFile);
            db.SaveChanges();

            // Verify it's in the FTS index
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts WHERE WikiFileFts MATCH 'Delete'";
            Assert.Equal(1, (long)cmd.ExecuteScalar()!);

            // Delete the wiki file
            db.WikiFiles.Remove(wikiFile);
            db.SaveChanges();

            // Should no longer be in FTS index
            cmd.CommandText = "SELECT COUNT(*) FROM WikiFileFts WHERE WikiFileFts MATCH 'Delete'";
            Assert.Equal(0, (long)cmd.ExecuteScalar()!);
        }
    }

    /// <summary>
    /// Verifies seed data is present after migration.
    /// </summary>
    [Fact]
    public void Migration_SeedData_Personas_And_TextActions_Present()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            var personas = db.Personas.Where(p => p.IsBuiltIn).ToList();
            Assert.Equal(2, personas.Count);

            var generalAssistant = personas.Single(p => p.Id == "00000000000000000000000000000001");
            Assert.Equal("General Assistant", generalAssistant.DisplayName);

            var codeHelper = personas.Single(p => p.Id == "00000000000000000000000000000002");
            Assert.Equal("Code Helper", codeHelper.DisplayName);

            var textActions = db.TextActions.Where(ta => ta.IsBuiltIn).ToList();
            Assert.Equal(10, textActions.Count);
            Assert.Contains(textActions, ta => ta.DisplayName == "Rewrite");
            Assert.Contains(textActions, ta => ta.DisplayName == "Enhance Prompt");

            // Verify CaptureScope, ApplyMode, and Hotkey on a representative sample
            var rewrite = textActions.Single(ta => ta.DisplayName == "Rewrite");
            Assert.Equal("selection", rewrite.CaptureScope);
            Assert.Equal("replaceSelection", rewrite.ApplyMode);
            Assert.Equal("Alt+Q", rewrite.Hotkey);

            var continueWriting = textActions.Single(ta => ta.DisplayName == "Continue Writing");
            Assert.Equal("focusedElement", continueWriting.CaptureScope);
            Assert.Equal("insertAtCursor", continueWriting.ApplyMode);
            Assert.Equal("Alt+C", continueWriting.Hotkey);

            var explainScreen = textActions.Single(ta => ta.DisplayName == "Explain Screen");
            Assert.Equal("fullDocument,screenshot", explainScreen.CaptureScope);
            Assert.Equal("showOnly", explainScreen.ApplyMode);
            Assert.Null(explainScreen.Hotkey);

            var fixGrammar = textActions.Single(ta => ta.DisplayName == "Fix Grammar");
            Assert.Equal("selection", fixGrammar.CaptureScope);
            Assert.Equal("replaceSelection", fixGrammar.ApplyMode);
            Assert.Null(fixGrammar.Hotkey);
        }
    }

    /// <summary>
    /// Verifies that the migration history table contains the InitialCreate entry.
    /// </summary>
    [Fact]
    public void Migration_HistoryTable_HasInitialCreateEntry()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20260618101823_InitialCreate'";
            var count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// Verifies that the migration history table contains the AddMemoryEntry entry.
    /// </summary>
    [Fact]
    public void Migration_HistoryTable_HasAddMemoryEntry()
    {
        var (db, connection) = CreateTestDbContextWithMigration();
        using (db)
        using (connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddMemoryEntry'";
            var count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1, count);
        }
    }
}
