using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;

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
}
