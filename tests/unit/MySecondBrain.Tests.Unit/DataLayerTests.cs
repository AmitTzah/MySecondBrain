using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using MySecondBrain.Data.Repositories;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Unit;

public class DataLayerTests
{
    /// <summary>
    /// Creates an in-memory SQLite AppDbContext for testing.
    /// Keeps the connection open so the database persists for the lifetime of the context.
    /// Caller is responsible for disposing both the context and the connection.
    /// </summary>
    private static (AppDbContext Db, SqliteConnection Connection) CreateTestDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    /// <summary>
    /// Validates that each entity class has the correct number of scalar properties
    /// (non-navigation, non-inherited) matching the vision data spec.
    /// </summary>
    [Fact]
    public void EntityPropertyCounts_MatchVisionSpecs()
    {
        var expectations = new Dictionary<Type, int>
        {
            // Scalar (non-navigation) property counts per vision spec reference.md checklist
            [typeof(ApiKey)] = 9,               // Id, DisplayName, Provider, CustomProviderName?, CustomEndpointUrl?, KeyValue, IsValid, LastTestedAt?, CreatedAt
            [typeof(Artifact)] = 7,              // Id, Name, Type, ThreadId, VersionCount, CreatedAt, UpdatedAt
            [typeof(ChatThread)] = 23,           // Id, Title?, IsTransient, PersonaId?, ModelConfigId?, SystemMessage?, ChatMode, ThinkingEnabled, IsMuted, IsFavorite, IsPinned, IsArchived, ColorLabel?, Tags?, FolderId?, IsDeleted, DeletedAt?, SourceHWND?, SourceAppName?, SourceDocTitle?, OriginalHighlightedText?, CreatedAt, LastActivityAt
            [typeof(MediaItem)] = 15,            // Id, FileName, FilePath, MediaType, MimeType, FileSize, Source, ThreadId, MessageId?, GeneratedPrompt?, IsSavedToDisk, IsSavedToWiki, IsDeleted, DeletedAt?, CreatedAt
            [typeof(Message)] = 17,              // Id, ThreadId, Role, Content, RawContent?, PersonaId?, ModelConfigId?, TokenCount?, EstimatedCost?, GenerationTimeMs?, Feedback?, ParentMessageId?, VersionNumber, BranchId, IsActiveBranch, IsDirectTransformation?, CreatedAt
            [typeof(ModelConfiguration)] = 14,   // Id, DisplayName, Provider, ApiKeyId?, ModelIdentifier?, Temperature, MaxOutputTokens, MaxContextWindow, ThinkingEnabled, PricingInputPer1K?, PricingOutputPer1K?, ContextOverflowStrategy, CreatedAt, UpdatedAt
            [typeof(Persona)] = 8,               // Id, DisplayName, SystemPrompt?, DefaultModelConfigId?, DefaultChatMode, IsBuiltIn, CreatedAt, UpdatedAt
            [typeof(PromptTemplate)] = 7,        // Id, Name, Text, Tags?, FolderId?, CreatedAt, UpdatedAt
            [typeof(TextAction)] = 8,            // Id, DisplayName, SystemPrompt?, ModelConfigId?, Hotkey?, IsBuiltIn, CreatedAt, UpdatedAt
            [typeof(UsageRecord)] = 12,          // Id, MessageId, ThreadId, PersonaId?, ModelConfigId?, Provider, ModelIdentifier, PromptTokens, CompletionTokens, TotalTokens, EstimatedCost?, CreatedAt
            [typeof(WikiFile)] = 9,              // FilePath (PK), FileName, H1Title?, Headings?, Content?, WordCount?, LastModifiedAt?, CrossLinksOut?, CrossLinksIn?
            [typeof(WikiVersionSnapshot)] = 5,   // Id, WikiFilePath, Content, Source, CreatedAt
            [typeof(MessageDrafts)] = 4,         // ThreadId, Content, CursorPosition, SavedAt
            [typeof(AppSetting)] = 4,            // Key, Value, ValueType, UpdatedAt
        };

        foreach (var (entityType, expectedCount) in expectations)
        {
            var scalarProps = entityType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => !IsNavigationProperty(p))
                .ToList();

            var actualCount = scalarProps.Count;

            Assert.True(actualCount == expectedCount,
                $"{entityType.Name}: expected {expectedCount} scalar properties, found {actualCount}. " +
                $"Properties: {string.Join(", ", scalarProps.Select(p => p.Name))}");
        }
    }

    /// <summary>
    /// Determines whether a property is a navigation property (reference or collection)
    /// based on its type. Navigation properties point to other entities.
    /// </summary>
    private static bool IsNavigationProperty(PropertyInfo property)
    {
        var propType = property.PropertyType;

        // Reference navigation: property type is an entity class
        if (propType.IsClass && propType != typeof(string) && propType.Namespace == typeof(ApiKey).Namespace)
            return true;

        // Collection navigation: ICollection<T> where T is an entity
        if (propType.IsGenericType)
        {
            var genericTypeDef = propType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(ICollection<>))
            {
                var elementType = propType.GenericTypeArguments[0];
                if (elementType.Namespace == typeof(ApiKey).Namespace)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that all primary keys use the string GUID convention (Guid.NewGuid().ToString("N")).
    /// </summary>
    [Fact]
    public void EntityPrimaryKeys_AreStringType()
    {
        var entitiesWithStringPk = new[]
        {
            typeof(ApiKey), typeof(Artifact), typeof(ChatThread), typeof(MediaItem),
            typeof(Message), typeof(ModelConfiguration), typeof(Persona),
            typeof(PromptTemplate), typeof(TextAction), typeof(UsageRecord),
            typeof(WikiFile), typeof(WikiVersionSnapshot),
            typeof(MessageDrafts), typeof(AppSetting)
        };

        foreach (var entityType in entitiesWithStringPk)
        {
            var keyProp = entityType.GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null);

            Assert.NotNull(keyProp);
            Assert.True(keyProp!.PropertyType == typeof(string),
                $"{entityType.Name}.{keyProp.Name} should be string, but is {keyProp.PropertyType.Name}");
        }
    }

    /// <summary>
    /// Validates that WikiFile uses FilePath as its primary key (natural key, not GUID).
    /// </summary>
    [Fact]
    public void WikiFile_PrimaryKey_IsFilePath()
    {
        var keyProp = typeof(WikiFile).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null);

        Assert.NotNull(keyProp);
        Assert.Equal(nameof(WikiFile.FilePath), keyProp!.Name);
    }

    /// <summary>
    /// Validates MessageDrafts uses ThreadId as PK (not a generated GUID).
    /// </summary>
    [Fact]
    public void MessageDrafts_PrimaryKey_IsThreadId()
    {
        var keyProp = typeof(MessageDrafts).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null);

        Assert.NotNull(keyProp);
        Assert.Equal(nameof(MessageDrafts.ThreadId), keyProp!.Name);
    }

    /// <summary>
    /// Validates MediaItem has soft-delete columns.
    /// </summary>
    [Fact]
    public void MediaItem_HasSoftDeleteColumns()
    {
        var props = typeof(MediaItem).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains(nameof(MediaItem.IsDeleted), props);
        Assert.Contains(nameof(MediaItem.DeletedAt), props);
    }

    /// <summary>
    /// Validates ChatThread has ModelConfigId FK.
    /// </summary>
    [Fact]
    public void ChatThread_HasModelConfigId()
    {
        var props = typeof(ChatThread).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains(nameof(ChatThread.ModelConfigId), props);
    }

    /// <summary>
    /// Validates entity default values for common patterns.
    /// </summary>
    [Fact]
    public void EntityDefaultValues_AreCorrect()
    {
        var chatThread = new ChatThread();
        Assert.Equal("Standard", chatThread.ChatMode);
        Assert.False(chatThread.IsTransient);
        Assert.False(chatThread.IsDeleted);

        var message = new Message();
        Assert.Equal(1, message.VersionNumber);
        Assert.True(message.IsActiveBranch);
        Assert.NotEmpty(message.BranchId);

        var persona = new Persona();
        Assert.Equal("Standard", persona.DefaultChatMode);
        Assert.False(persona.IsBuiltIn);

        var modelConfig = new ModelConfiguration();
        Assert.Equal(1.0, modelConfig.Temperature);
        Assert.Equal(4096, modelConfig.MaxOutputTokens);
        Assert.Equal("SlidingWindow", modelConfig.ContextOverflowStrategy);

        var appSetting = new AppSetting();
        Assert.Equal("String", appSetting.ValueType);

        var artifact = new Artifact();
        Assert.Equal(1, artifact.VersionCount);

        var wikiVersionSnapshot = new WikiVersionSnapshot();
        Assert.NotEmpty(wikiVersionSnapshot.Id);
        Assert.True(wikiVersionSnapshot.CreatedAt <= DateTimeOffset.UtcNow);

        var messageDrafts = new MessageDrafts();
        Assert.Equal(string.Empty, messageDrafts.ThreadId);
        Assert.True(messageDrafts.SavedAt <= DateTimeOffset.UtcNow);

        var promptTemplate = new PromptTemplate();
        Assert.NotEmpty(promptTemplate.Id);
    }

    // ════════════════════════════════════════════════════════════════
    // Step 2 tests: DbContext model validation, FK relationships,
    // indexes, and seed data
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

            // Verify all 14 entity types are mapped (BackupSnapshot deferred to W3.16)
            var entityTypes = db.Model.GetEntityTypes().ToList();
            Assert.Equal(14, entityTypes.Count);
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
            "Messages", "MessageDrafts", "ModelConfigurations", "Personas",
            "PromptTemplates", "TextActions", "UsageRecords",
            "WikiFiles", "WikiVersionSnapshots"
        };

        var dbSetProperties = typeof(AppDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name)
            .ToHashSet();

        Assert.Equal(14, dbSetProperties.Count);

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

            // Verify total FK count across all entities
            var totalFks = model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys())
                .Count();
            Assert.Equal(17, totalFks);
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
        }
    }

    /// <summary>
    /// Verifies seed data: 2 built-in Personas and 6 built-in TextActions.
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
            Assert.Equal(6, textActions.Count);

            var expectedActions = new Dictionary<string, string>
            {
                ["a000000000000000000000000000001"] = "Rewrite",
                ["a000000000000000000000000000002"] = "Summarize",
                ["a000000000000000000000000000003"] = "Explain",
                ["a000000000000000000000000000004"] = "Translate",
                ["a000000000000000000000000000005"] = "Fix Grammar",
                ["a000000000000000000000000000006"] = "Enhance Prompt",
            };

            foreach (var (id, displayName) in expectedActions)
            {
                var action = textActions.Single(ta => ta.Id == id);
                Assert.Equal(displayName, action.DisplayName);
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
    // Step 3 tests: Migration, FTS5 virtual tables, and content-sync triggers
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an in-memory SQLite context and applies the full migration
    /// (including FTS5 virtual tables and seed data).
    /// </summary>
    private static (AppDbContext Db, SqliteConnection Connection) CreateTestDbContextWithMigration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.Migrate();
        return (db, connection);
    }

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
            Assert.Equal(6, textActions.Count);
            Assert.Contains(textActions, ta => ta.DisplayName == "Rewrite");
            Assert.Contains(textActions, ta => ta.DisplayName == "Enhance Prompt");
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


    // ════════════════════════════════════════════════════════════════
    // Step 4 tests: ChatThreadRepository and MessageRepository
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

    // ════════════════════════════════════════════════════════════════
    // Step 5 tests: PersonaRepository, ModelConfigurationRepository,
    // ApiKeyRepository
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PersonaRepository_CreateAsync_ReturnsCreatedPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var persona = new CoreModels.Persona { Name = "Test Persona", SystemPrompt = "You are a test." };
            var result = await repo.CreateAsync(persona);
            Assert.NotNull(result);
            Assert.Equal("Test Persona", result.Name);
            Assert.Equal("You are a test.", result.SystemPrompt);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetByIdAsync_ReturnsCorrectPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { Name = "Find Me" });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Find Me", found!.Name);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task PersonaRepository_GetAllAsync_ReturnsAllPersonas()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            await repo.CreateAsync(new CoreModels.Persona { Name = "A" });
            await repo.CreateAsync(new CoreModels.Persona { Name = "B" });
            var results = await repo.GetAllAsync();
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetDefaultAsync_ReturnsBuiltInOrFirst()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            // Seed data has 2 built-in personas; ordered by Id, "General Assistant" (000...01) comes first
            var defaultPersona = await repo.GetDefaultAsync();
            Assert.NotNull(defaultPersona);
            Assert.True(defaultPersona!.IsDefault);
            Assert.Equal("00000000000000000000000000000001", defaultPersona.Id);
            Assert.Equal("General Assistant", defaultPersona.Name);

            // Add a non-default persona and verify built-in is still returned as default
            await repo.CreateAsync(new CoreModels.Persona { Name = "Custom", IsDefault = false });
            var stillDefault = await repo.GetDefaultAsync();
            Assert.NotNull(stillDefault);
            Assert.True(stillDefault!.IsDefault);
            Assert.Equal("00000000000000000000000000000001", stillDefault.Id);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetDefaultAsync_FallbackToFirstAvailable()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Delete all seed personas so no built-in exists
            var allSeed = db.Personas.ToList();
            db.Personas.RemoveRange(allSeed);
            await db.SaveChangesAsync();

            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { Name = "Only Persona", IsDefault = false });
            var defaultPersona = await repo.GetDefaultAsync();
            Assert.NotNull(defaultPersona);
            Assert.Equal(created.Id, defaultPersona!.Id);
            Assert.Equal("Only Persona", defaultPersona.Name);
        }
    }

    [Fact]
    public async Task PersonaRepository_UpdateAsync_UpdatesName()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { Name = "Original" });
            created.Name = "Updated";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated", updated!.Name);
        }
    }

    [Fact]
    public async Task PersonaRepository_DeleteAsync_RemovesPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { Name = "To Delete" });
            await repo.DeleteAsync(created.Id);
            Assert.Null(await repo.GetByIdAsync(created.Id));
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_CreateAsync_ReturnsCreatedConfig()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var config = new CoreModels.ModelConfiguration
            {
                Name = "GPT-4o",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelId = "gpt-4o",
                Temperature = 0.8,
                MaxTokens = 8192
            };
            var result = await repo.CreateAsync(config);
            Assert.NotNull(result);
            Assert.Equal("GPT-4o", result.Name);
            Assert.Equal(CoreModels.ProviderType.OpenAI, result.ProviderType);
            Assert.Equal("gpt-4o", result.ModelId);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_GetByIdAsync_ReturnsCorrectConfig()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                Name = "Claude Sonnet",
                ProviderType = CoreModels.ProviderType.Anthropic,
                ModelId = "claude-sonnet-4-20250514"
            });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Claude Sonnet", found!.Name);
            Assert.Equal(CoreModels.ProviderType.Anthropic, found.ProviderType);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_DeleteAsync_ReferencedByPersona_ThrowsInvalidOperationException()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create a model config and a persona referencing it
            var configEntity = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Referenced Config",
                Provider = "OpenAI"
            };
            db.ModelConfigurations.Add(configEntity);

            var personaEntity = new Persona
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Test Persona",
                DefaultModelConfigId = configEntity.Id
            };
            db.Personas.Add(personaEntity);
            await db.SaveChangesAsync();

            var repo = new ModelConfigurationRepository(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.DeleteAsync(configEntity.Id));
            Assert.Contains("Referenced Config", ex.Message);
            Assert.Contains("Persona", ex.Message);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_DeleteAsync_NoReferences_Succeeds()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                Name = "Unreferenced Config",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelId = "gpt-4o"
            });
            // Should not throw
            await repo.DeleteAsync(created.Id);
            Assert.Null(await repo.GetByIdAsync(created.Id));
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_GetAllAsync_ReturnsAllConfigs()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                Name = "GPT-4o", ProviderType = CoreModels.ProviderType.OpenAI, ModelId = "gpt-4o"
            });
            await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                Name = "Claude", ProviderType = CoreModels.ProviderType.Anthropic, ModelId = "claude-3"
            });
            var results = await repo.GetAllAsync();
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_UpdateAsync_UpdatesAllProperties()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                Name = "Original Config",
                ProviderType = CoreModels.ProviderType.Google,
                ModelId = "gemini-pro",
                Temperature = 0.5,
                MaxTokens = 2048,
                ThinkingEnabled = false,
                ThinkingTokens = 64000
            });
            created.Name = "Updated Config";
            created.ProviderType = CoreModels.ProviderType.Anthropic;
            created.ModelId = "claude-3-opus";
            created.Temperature = 0.9;
            created.MaxTokens = 8192;
            created.ThinkingEnabled = true;
            created.ThinkingTokens = 32000;
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated Config", updated!.Name);
            Assert.Equal(CoreModels.ProviderType.Anthropic, updated.ProviderType);
            Assert.Equal("claude-3-opus", updated.ModelId);
            Assert.Equal(0.9, updated.Temperature);
            Assert.Equal(8192, updated.MaxTokens);
            Assert.True(updated.ThinkingEnabled);
            Assert.Equal(32000, updated.ThinkingTokens);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_CreateAsync_ReturnsCreatedKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var key = new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "encrypted-key-value",
                Label = "My OpenAI Key"
            };
            var result = await repo.CreateAsync(key);
            Assert.NotNull(result);
            Assert.Equal(CoreModels.ProviderType.OpenAI, result.ProviderType);
            Assert.Equal("encrypted-key-value", result.EncryptedValue);
            Assert.Equal("My OpenAI Key", result.Label);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_GetByIdAsync_ReturnsCorrectKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.Anthropic,
                EncryptedValue = "anthropic-key",
                Label = "Claude Key"
            });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal(CoreModels.ProviderType.Anthropic, found!.ProviderType);
            Assert.Equal("Claude Key", found.Label);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_GetAllAsync_ReturnsAllKeys()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "key1",
                Label = "Key 1"
            });
            await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.Google,
                EncryptedValue = "key2",
                Label = "Key 2"
            });
            var results = await repo.GetAllAsync();
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task ApiKeyRepository_UpdateAsync_UpdatesAllProperties()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "original-value",
                Label = "Original Label"
            });
            created.Label = "Updated Label";
            created.ProviderType = CoreModels.ProviderType.Anthropic;
            created.EncryptedValue = "updated-value";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated Label", updated!.Label);
            Assert.Equal(CoreModels.ProviderType.Anthropic, updated.ProviderType);
            Assert.Equal("updated-value", updated.EncryptedValue);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_DeleteAsync_RemovesKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "to-delete",
                Label = "Delete Me"
            });
            await repo.DeleteAsync(created.Id);
            Assert.Null(await repo.GetByIdAsync(created.Id));
        }
    }

    [Fact]
    public async Task ApiKeyRepository_DeleteAsync_NullifiesModelConfigReferences()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create an API key and a ModelConfiguration referencing it
            var keyEntity = new ApiKey
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Test Key",
                Provider = "OpenAI",
                KeyValue = "test-value"
            };
            db.ApiKeys.Add(keyEntity);

            var configEntity = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Config With Key",
                Provider = "OpenAI",
                ApiKeyId = keyEntity.Id
            };
            db.ModelConfigurations.Add(configEntity);
            await db.SaveChangesAsync();

            var repo = new ApiKeyRepository(db);
            await repo.DeleteAsync(keyEntity.Id);

            // Key should be deleted
            Assert.Null(await repo.GetByIdAsync(keyEntity.Id));

            // ModelConfiguration FK should be nullified
            var config = await db.ModelConfigurations.FindAsync(configEntity.Id);
            Assert.NotNull(config);
            Assert.Null(config!.ApiKeyId);
        }
    }
}
