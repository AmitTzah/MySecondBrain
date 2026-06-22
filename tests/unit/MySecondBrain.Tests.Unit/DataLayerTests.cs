using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using MySecondBrain.Data.Repositories;
using Message = MySecondBrain.Data.Entities.Message;
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
            [typeof(ModelConfiguration)] = 16,   // Id, DisplayName, Provider, ApiKeyId?, ModelIdentifier?, Temperature, MaxOutputTokens, MaxContextWindow, ThinkingEnabled, PricingInputPer1K?, PricingOutputPer1K?, PricingCacheHitPer1K?, PricingCacheMissPer1K?, ContextOverflowStrategy, CreatedAt, UpdatedAt
            [typeof(Persona)] = 8,               // Id, DisplayName, SystemPrompt?, DefaultModelConfigId?, DefaultChatMode, IsBuiltIn, CreatedAt, UpdatedAt
            [typeof(PromptTemplate)] = 7,        // Id, Name, Text, Tags?, FolderId?, CreatedAt, UpdatedAt
            [typeof(TextAction)] = 10,           // Id, DisplayName, SystemPrompt, ModelConfigId?, Hotkey?, CaptureScope, ApplyMode, IsBuiltIn, CreatedAt, UpdatedAt
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
        Assert.Equal(131072, modelConfig.MaxOutputTokens);
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

        var textAction = new TextAction();
        Assert.Equal("selection", textAction.CaptureScope);
        Assert.Equal("replaceSelection", textAction.ApplyMode);
        Assert.Equal(string.Empty, textAction.SystemPrompt);
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
            var persona = new CoreModels.Persona { DisplayName = "Test Persona", SystemPrompt = "You are a test." };
            var result = await repo.CreateAsync(persona);
            Assert.NotNull(result);
            Assert.Equal("Test Persona", result.DisplayName);
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
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Find Me" });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Find Me", found!.DisplayName);
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
            await repo.CreateAsync(new CoreModels.Persona { DisplayName = "A" });
            await repo.CreateAsync(new CoreModels.Persona { DisplayName = "B" });
            var results = await repo.GetAllAsync();
            Assert.Equal(4, results.Count); // 2 seed personas + 2 created
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
            Assert.True(defaultPersona!.IsBuiltIn);
            Assert.Equal("00000000000000000000000000000001", defaultPersona.Id);
            Assert.Equal("General Assistant", defaultPersona.DisplayName);

            // Add a non-default persona and verify built-in is still returned as default
            await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Custom", IsBuiltIn = false });
            var stillDefault = await repo.GetDefaultAsync();
            Assert.NotNull(stillDefault);
            Assert.True(stillDefault!.IsBuiltIn);
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
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Only Persona", IsBuiltIn = false });
            var defaultPersona = await repo.GetDefaultAsync();
            Assert.NotNull(defaultPersona);
            Assert.Equal(created.Id, defaultPersona!.Id);
            Assert.Equal("Only Persona", defaultPersona.DisplayName);
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
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Original" });
            created.DisplayName = "Updated";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated", updated!.DisplayName);
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
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "To Delete" });
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
                DisplayName = "GPT-4o",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelIdentifier = "gpt-4o",
                Temperature = 0.8,
                MaxOutputTokens = 8192
            };
            var result = await repo.CreateAsync(config);
            Assert.NotNull(result);
            Assert.Equal("GPT-4o", result.DisplayName);
            Assert.Equal(CoreModels.ProviderType.OpenAI, result.ProviderType);
            Assert.Equal("gpt-4o", result.ModelIdentifier);
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
                DisplayName = "Claude Sonnet",
                ProviderType = CoreModels.ProviderType.Anthropic,
                ModelIdentifier = "claude-sonnet-4-20250514"
            });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Claude Sonnet", found!.DisplayName);
            Assert.Equal(CoreModels.ProviderType.Anthropic, found.ProviderType);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_DeleteAsync_ReferencedByPersona_NullifiesFk()
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
            await repo.DeleteAsync(configEntity.Id);

            // Config should be deleted
            Assert.Null(await db.ModelConfigurations.FindAsync(configEntity.Id));

            // Persona FK should be nullified
            var persona = await db.Personas.FindAsync(personaEntity.Id);
            Assert.NotNull(persona);
            Assert.Null(persona!.DefaultModelConfigId);
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
                DisplayName = "Unreferenced Config",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelIdentifier = "gpt-4o"
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
                DisplayName = "GPT-4o", ProviderType = CoreModels.ProviderType.OpenAI, ModelIdentifier = "gpt-4o"
            });
            await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                DisplayName = "Claude", ProviderType = CoreModels.ProviderType.Anthropic, ModelIdentifier = "claude-3"
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
                DisplayName = "Original Config",
                ProviderType = CoreModels.ProviderType.Google,
                ModelIdentifier = "gemini-pro",
                Temperature = 0.5,
                MaxOutputTokens = 2048,
                MaxContextWindow = 64000,
                ThinkingEnabled = false,
                ContextOverflowStrategy = "SlidingWindow"
            });
            created.DisplayName = "Updated Config";
            created.ProviderType = CoreModels.ProviderType.Anthropic;
            created.ModelIdentifier = "claude-3-opus";
            created.Temperature = 0.9;
            created.MaxOutputTokens = 8192;
            created.MaxContextWindow = 128000;
            created.ThinkingEnabled = true;
            created.ApiKeyId = null; // Nullable FK; set to null to avoid FK constraint
            created.PricingInputPer1K = 2.50m;
            created.PricingOutputPer1K = 10.00m;
            created.ContextOverflowStrategy = "AutoSummarize";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated Config", updated!.DisplayName);
            Assert.Equal(CoreModels.ProviderType.Anthropic, updated.ProviderType);
            Assert.Equal("claude-3-opus", updated.ModelIdentifier);
            Assert.Equal(0.9, updated.Temperature);
            Assert.Equal(8192, updated.MaxOutputTokens);
            Assert.Equal(128000, updated.MaxContextWindow);
            Assert.True(updated.ThinkingEnabled);
            Assert.Null(updated.ApiKeyId); // Set to null in update to avoid FK constraint
            Assert.Equal(2.50m, updated.PricingInputPer1K);
            Assert.Equal(10.00m, updated.PricingOutputPer1K);
            Assert.Equal("AutoSummarize", updated.ContextOverflowStrategy);
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
            db.Settings.Add(new AppSetting { Key = "tool_settings", Value = "{\"AutoApproveWebSearch\":true,\"AutoApproveFileGenerate\":false,\"AutoApproveFileEdit\":true,\"AutoApproveWikiSearch\":false,\"MaxConsecutiveAutoApprovals\":5}" });
            await db.SaveChangesAsync();

            var repo = new SettingsRepository(db);
            var result = await repo.GetAsync<CoreModels.ToolAutoApprovalSettings>("tool_settings");
            Assert.NotNull(result);
            Assert.True(result!.AutoApproveWebSearch);
            Assert.False(result.AutoApproveFileGenerate);
            Assert.True(result.AutoApproveFileEdit);
            Assert.False(result.AutoApproveWikiSearch);
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
                AutoApproveWebSearch = true,
                AutoApproveFileGenerate = false,
                AutoApproveFileEdit = true,
                AutoApproveWikiSearch = false,
                MaxConsecutiveAutoApprovals = 10
            };
            await repo.SetAsync("typed_key", settings);

            var result = await repo.GetAsync<CoreModels.ToolAutoApprovalSettings>("typed_key");
            Assert.NotNull(result);
            Assert.True(result!.AutoApproveWebSearch);
            Assert.False(result.AutoApproveFileGenerate);
            Assert.True(result.AutoApproveFileEdit);
            Assert.False(result.AutoApproveWikiSearch);
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

    // ════════════════════════════════════════════════════════════════
    // Step 1 tests: Domain model field mappings for
    // ApiKey, ModelConfiguration, Persona repositories
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApiKeyRepository_CreateAsync_WithAllFields_ReturnsMappedKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var key = new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAICompatible,
                EncryptedValue = "encrypted-test-value",
                Label = "Custom OpenAI",
                CustomProviderName = "My Local AI",
                CustomEndpointUrl = "https://localhost:8080/v1",
                IsValid = true,
                LastTestedAt = DateTimeOffset.UtcNow.AddDays(-1)
            };
            var result = await repo.CreateAsync(key);

            Assert.NotNull(result);
            Assert.Equal(CoreModels.ProviderType.OpenAICompatible, result.ProviderType);
            Assert.Equal("encrypted-test-value", result.EncryptedValue);
            Assert.Equal("Custom OpenAI", result.Label);
            Assert.Equal("My Local AI", result.CustomProviderName);
            Assert.Equal("https://localhost:8080/v1", result.CustomEndpointUrl);
            Assert.True(result.IsValid);
            Assert.NotNull(result.LastTestedAt);
            Assert.NotEqual(default, result.CreatedAt);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_UpdateAsync_UpdatesNewFields()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "initial-value",
                Label = "Initial"
            });

            created.Label = "Updated";
            created.ProviderType = CoreModels.ProviderType.Anthropic;
            created.EncryptedValue = "updated-value";
            created.CustomProviderName = "CustomName";
            created.CustomEndpointUrl = "https://custom.endpoint";
            created.IsValid = true;
            created.LastTestedAt = DateTimeOffset.UtcNow;

            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);

            Assert.Equal("Updated", updated!.Label);
            Assert.Equal(CoreModels.ProviderType.Anthropic, updated.ProviderType);
            Assert.Equal("updated-value", updated.EncryptedValue);
            Assert.Equal("CustomName", updated.CustomProviderName);
            Assert.Equal("https://custom.endpoint", updated.CustomEndpointUrl);
            Assert.True(updated.IsValid);
            Assert.NotNull(updated.LastTestedAt);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_CreateAsync_WithAllFields_ReturnsMappedConfig()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create a prerequisite ApiKey to satisfy FK constraint
            var apiKeyEntity = new ApiKey
            {
                Id = "test-key-for-config",
                DisplayName = "Test Key",
                Provider = "OpenAI",
                KeyValue = "test-value"
            };
            db.ApiKeys.Add(apiKeyEntity);
            await db.SaveChangesAsync();

            var repo = new ModelConfigurationRepository(db);
            var config = new CoreModels.ModelConfiguration
            {
                DisplayName = "GPT-4o Custom",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelIdentifier = "gpt-4o",
                Temperature = 0.7,
                MaxOutputTokens = 4096,
                MaxContextWindow = 128000,
                ThinkingEnabled = true,
                ApiKeyId = "test-key-for-config",
                PricingInputPer1K = 2.50m,
                PricingOutputPer1K = 10.00m,
                ContextOverflowStrategy = "AutoSummarize"
            };
            var result = await repo.CreateAsync(config);

            Assert.NotNull(result);
            Assert.Equal("GPT-4o Custom", result.DisplayName);
            Assert.Equal(CoreModels.ProviderType.OpenAI, result.ProviderType);
            Assert.Equal("gpt-4o", result.ModelIdentifier);
            Assert.Equal(0.7, result.Temperature);
            Assert.Equal(4096, result.MaxOutputTokens);
            Assert.Equal(128000, result.MaxContextWindow);
            Assert.True(result.ThinkingEnabled);
            Assert.Equal("test-key-for-config", result.ApiKeyId);
            Assert.Equal(2.50m, result.PricingInputPer1K);
            Assert.Equal(10.00m, result.PricingOutputPer1K);
            Assert.Equal("AutoSummarize", result.ContextOverflowStrategy);
            Assert.NotEqual(default, result.CreatedAt);
            Assert.NotEqual(default, result.UpdatedAt);
        }
    }

    [Fact]
    public async Task PersonaRepository_CreateAsync_WithAllFields_ReturnsMappedPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create a prerequisite ModelConfiguration to satisfy FK constraint (Restrict)
            var configEntity = new ModelConfiguration
            {
                Id = "test-config-for-persona",
                DisplayName = "Prerequisite Config",
                Provider = "OpenAI"
            };
            db.ModelConfigurations.Add(configEntity);
            await db.SaveChangesAsync();

            var repo = new PersonaRepository(db);
            var persona = new CoreModels.Persona
            {
                DisplayName = "Custom Persona",
                SystemPrompt = "You are a specialized assistant.",
                DefaultModelConfigId = "test-config-for-persona",
                DefaultChatMode = "TextCompletion",
                IsBuiltIn = false
            };
            var result = await repo.CreateAsync(persona);

            Assert.NotNull(result);
            Assert.Equal("Custom Persona", result.DisplayName);
            Assert.Equal("You are a specialized assistant.", result.SystemPrompt);
            Assert.Equal("test-config-for-persona", result.DefaultModelConfigId);
            Assert.Equal("TextCompletion", result.DefaultChatMode);
            Assert.False(result.IsBuiltIn);
            Assert.NotEqual(default, result.CreatedAt);
            Assert.NotEqual(default, result.UpdatedAt);
        }
    }

    [Fact]
    public async Task PersonaRepository_UpdateAsync_UpdatesDefaultModelConfigIdAndChatMode()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create a prerequisite ModelConfiguration to satisfy FK constraint (Restrict)
            var configEntity = new ModelConfiguration
            {
                Id = "test-config-for-update",
                DisplayName = "Config for Update Test",
                Provider = "Anthropic"
            };
            db.ModelConfigurations.Add(configEntity);
            await db.SaveChangesAsync();

            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona
            {
                DisplayName = "Original",
                SystemPrompt = "Original prompt",
                DefaultModelConfigId = null,
                DefaultChatMode = "Standard"
            });

            created.DisplayName = "Updated";
            created.SystemPrompt = "Updated prompt";
            created.DefaultModelConfigId = "test-config-for-update";
            created.DefaultChatMode = "TextCompletion";

            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);

            Assert.Equal("Updated", updated!.DisplayName);
            Assert.Equal("Updated prompt", updated.SystemPrompt);
            Assert.Equal("test-config-for-update", updated.DefaultModelConfigId);
            Assert.Equal("TextCompletion", updated.DefaultChatMode);
        }
    }
}
