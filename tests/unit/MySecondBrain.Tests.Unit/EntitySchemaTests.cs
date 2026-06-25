using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using MySecondBrain.Data.Repositories;
using Message = MySecondBrain.Data.Entities.Message;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Unit;

public class EntitySchemaTests : DataLayerTestBase
{
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
            [typeof(UsageRecord)] = 20,          // Id, MessageId, ThreadId, PersonaId?, ModelConfigId?, Provider, ModelIdentifier, PromptTokens, CompletionTokens, TotalTokens, EstimatedCost?, CacheReadTokens, CacheCreationTokens, LatencyMs, Tier, ErrorType?, ErrorMessage?, ErrorStatusCode?, RawJsonPath?, CreatedAt
            [typeof(WikiFile)] = 9,              // FilePath (PK), FileName, H1Title?, Headings?, Content?, WordCount?, LastModifiedAt?, CrossLinksOut?, CrossLinksIn?
            [typeof(WikiVersionSnapshot)] = 5,   // Id, WikiFilePath, Content, Source, CreatedAt
            [typeof(MessageDrafts)] = 4,         // ThreadId, Content, CursorPosition, SavedAt
            [typeof(AppSetting)] = 4,            // Key, Value, ValueType, UpdatedAt
            [typeof(MemoryEntryEntity)] = 6,     // Id, Key, Value, SourceThreadId?, CreatedAt, UpdatedAt
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
            typeof(MessageDrafts), typeof(AppSetting),
            typeof(MemoryEntryEntity)
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

        var memoryEntry = new MemoryEntryEntity();
        Assert.NotEmpty(memoryEntry.Id);
        Assert.Equal(string.Empty, memoryEntry.Key);
        Assert.Equal(string.Empty, memoryEntry.Value);
        Assert.Null(memoryEntry.SourceThreadId);
        Assert.True(memoryEntry.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(memoryEntry.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    // ════════════════════════════════════════════════════════════════
    // Domain model field mappings for
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

    // ════════════════════════════════════════════════════════════════
    // MemoryEntry domain model tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void MemoryEntry_ValidationConstants_AreDefined()
    {
        Assert.Equal(200, CoreModels.MemoryEntry.KeyMaxLength);
        Assert.Equal(10240, CoreModels.MemoryEntry.ValueMaxLength);
    }

    [Fact]
    public void MemoryEntry_DefaultValues_AreCorrect()
    {
        var entry = new CoreModels.MemoryEntry();
        Assert.NotEmpty(entry.Id);
        Assert.Equal(string.Empty, entry.Key);
        Assert.Equal(string.Empty, entry.Value);
        Assert.Null(entry.SourceThreadId);
        Assert.True(entry.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(entry.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Validates MemoryEntryEntity CRUD operations via the DbContext.
    /// </summary>
    [Fact]
    public async Task MemoryEntryEntity_Crud_WithInMemoryDb()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create a ChatThread to satisfy the optional FK
            var thread = new ChatThread
            {
                Id = "test-thread-for-memory",
                Title = "Memory Test Thread"
            };
            db.ChatThreads.Add(thread);
            await db.SaveChangesAsync();

            // Create
            var entry = new MemoryEntryEntity
            {
                Key = "user-name",
                Value = "Alice",
                SourceThreadId = thread.Id
            };
            db.MemoryEntries.Add(entry);
            await db.SaveChangesAsync();
            Assert.NotEmpty(entry.Id);

            // Read
            var retrieved = await db.MemoryEntries.FindAsync(entry.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("user-name", retrieved!.Key);
            Assert.Equal("Alice", retrieved.Value);
            Assert.Equal(thread.Id, retrieved.SourceThreadId);

            // Update
            retrieved.Value = "Bob";
            retrieved.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            var updated = await db.MemoryEntries.FindAsync(entry.Id);
            Assert.NotNull(updated);
            Assert.Equal("Bob", updated!.Value);

            // Delete
            db.MemoryEntries.Remove(updated);
            await db.SaveChangesAsync();

            var deleted = await db.MemoryEntries.FindAsync(entry.Id);
            Assert.Null(deleted);
        }
    }

    /// <summary>
    /// Validates that MemoryEntry can be retrieved by its indexed Key column.
    /// </summary>
    [Fact]
    public async Task MemoryEntryEntity_CanBeRetrievedByKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var entry = new MemoryEntryEntity
            {
                Key = "user-email",
                Value = "alice@example.com"
            };
            db.MemoryEntries.Add(entry);
            await db.SaveChangesAsync();

            var retrieved = db.MemoryEntries.Single(e => e.Key == "user-email");
            Assert.NotNull(retrieved);
            Assert.Equal("alice@example.com", retrieved.Value);
        }
    }

    /// <summary>
    /// Validates that deleting a ChatThread nullifies SourceThreadId
    /// (SetNull delete behavior) rather than cascading deletion.
    /// </summary>
    [Fact]
    public async Task MemoryEntryEntity_DeletingSourceThread_SetsSourceThreadIdNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var thread = new ChatThread
            {
                Id = "thread-to-delete",
                Title = "Delete Test Thread"
            };
            db.ChatThreads.Add(thread);
            await db.SaveChangesAsync();

            var entry = new MemoryEntryEntity
            {
                Key = "delete-test",
                Value = "test-value",
                SourceThreadId = thread.Id
            };
            db.MemoryEntries.Add(entry);
            await db.SaveChangesAsync();

            // Delete the ChatThread — MemoryEntry should survive with null SourceThreadId
            db.ChatThreads.Remove(thread);
            await db.SaveChangesAsync();

            var retrieved = await db.MemoryEntries.FindAsync(entry.Id);
            Assert.NotNull(retrieved);
            Assert.Null(retrieved!.SourceThreadId);
            Assert.Equal("test-value", retrieved.Value);
        }
    }

    /// <summary>
    /// Validates that MemoryEntryEntity without a SourceThreadId can be persisted.
    /// </summary>
    [Fact]
    public async Task MemoryEntryEntity_CanBeOrphaned()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var entry = new MemoryEntryEntity
            {
                Key = "standalone-key",
                Value = "standalone-value"
            };
            db.MemoryEntries.Add(entry);
            await db.SaveChangesAsync();

            var retrieved = await db.MemoryEntries.FindAsync(entry.Id);
            Assert.NotNull(retrieved);
            Assert.Null(retrieved!.SourceThreadId);
            Assert.Equal("standalone-key", retrieved.Key);
            Assert.Equal("standalone-value", retrieved.Value);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // ToolAutoApprovalSettings domain model tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ToolAutoApprovalSettings_HasTenToolFields()
    {
        var expectedToolProperties = new[]
        {
            "AutoApproveBash",
            "AutoApproveTextEditor",
            "AutoApproveWebSearch",
            "AutoApproveWebFetch",
            "AutoApproveWikiSearch",
            "AutoApproveMemory",
            "AutoApproveSkillLoad",
            "AutoApproveAskUserInput",
            "AutoApprovePresentFiles",
            "AutoApproveImageSearch"
        };

        var props = typeof(CoreModels.ToolAutoApprovalSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var expected in expectedToolProperties)
        {
            Assert.True(props.Contains(expected),
                $"ToolAutoApprovalSettings should have property '{expected}' but it was not found.");
        }

        // Verify MaxConsecutiveAutoApprovals still exists
        Assert.True(props.Contains("MaxConsecutiveAutoApprovals"),
            "ToolAutoApprovalSettings should have MaxConsecutiveAutoApprovals property.");

        // Verify removed fields are gone
        Assert.DoesNotContain("AutoApproveFileGenerate", props);
        Assert.DoesNotContain("AutoApproveFileEdit", props);

        // Total: 10 booleans + 1 int = 11 declared properties
        Assert.Equal(11, props.Count);
    }

    [Fact]
    public void ToolAutoApprovalSettings_DefaultValues_AreCorrect()
    {
        var settings = new CoreModels.ToolAutoApprovalSettings();
        Assert.False(settings.AutoApproveBash);
        Assert.False(settings.AutoApproveTextEditor);
        Assert.False(settings.AutoApproveWebSearch);
        Assert.False(settings.AutoApproveWebFetch);
        Assert.False(settings.AutoApproveWikiSearch);
        Assert.False(settings.AutoApproveMemory);
        Assert.False(settings.AutoApproveSkillLoad);
        Assert.False(settings.AutoApproveAskUserInput);
        Assert.False(settings.AutoApprovePresentFiles);
        Assert.False(settings.AutoApproveImageSearch);
        Assert.Equal(10, settings.MaxConsecutiveAutoApprovals);
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

    /// <summary>
    /// Validates that there are exactly 14 IToolExecutor implementations in the Services assembly.
    /// Was 10 (9 existing + text_editor). Now 14 (9 existing + 5 new file operation executors, text_editor removed).
    /// </summary>
    [Fact]
    public void ToolExecutorCount_ShouldBe14()
    {
        var executorTypes = typeof(MySecondBrain.Services.Tools.WebSearchToolExecutor).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IToolExecutor).IsAssignableFrom(t))
            .ToList();

        Assert.Equal(14, executorTypes.Count);

        var executorNames = executorTypes.Select(t => t.Name).OrderBy(n => n).ToList();
        var expectedNames = new[]
        {
            "ApplyDiffToolExecutor",
            "AskUserInputToolExecutor",
            "BashToolExecutor",
            "ImageSearchToolExecutor",
            "ListFilesToolExecutor",
            "MemoryToolExecutor",
            "PresentFilesToolExecutor",
            "ReadFileToolExecutor",
            "SearchFilesToolExecutor",
            "SkillLoadToolExecutor",
            "WebFetchToolExecutor",
            "WebSearchToolExecutor",
            "WikiSearchToolExecutor",
            "WriteToFileToolExecutor"
        };

        for (int i = 0; i < expectedNames.Length; i++)
        {
            Assert.Equal(expectedNames[i], executorNames[i]);
        }

        // Verify TextEditorToolExecutor is absent
        Assert.DoesNotContain(executorTypes, t => t.Name == "TextEditorToolExecutor");
    }

    /// <summary>
    /// Validates that UsageRecord has the 8 new enriched columns from Step 2.
    /// </summary>
    [Fact]
    public void UsageRecord_ShouldHave8NewColumns()
    {
        var entityType = typeof(UsageRecord);
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => !IsNavigationProperty(p))
            .ToDictionary(p => p.Name);

        // Verify each new property exists
        Assert.True(props.ContainsKey(nameof(UsageRecord.CacheReadTokens)),
            "UsageRecord should have CacheReadTokens property");
        Assert.Equal(typeof(int), props[nameof(UsageRecord.CacheReadTokens)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.CacheCreationTokens)),
            "UsageRecord should have CacheCreationTokens property");
        Assert.Equal(typeof(int), props[nameof(UsageRecord.CacheCreationTokens)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.LatencyMs)),
            "UsageRecord should have LatencyMs property");
        Assert.Equal(typeof(int), props[nameof(UsageRecord.LatencyMs)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.Tier)),
            "UsageRecord should have Tier property");
        Assert.Equal(typeof(int), props[nameof(UsageRecord.Tier)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.ErrorType)),
            "UsageRecord should have ErrorType property");
        Assert.Equal(typeof(string), props[nameof(UsageRecord.ErrorType)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.ErrorMessage)),
            "UsageRecord should have ErrorMessage property");
        Assert.Equal(typeof(string), props[nameof(UsageRecord.ErrorMessage)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.ErrorStatusCode)),
            "UsageRecord should have ErrorStatusCode property");
        Assert.Equal(typeof(int?), props[nameof(UsageRecord.ErrorStatusCode)].PropertyType);

        Assert.True(props.ContainsKey(nameof(UsageRecord.RawJsonPath)),
            "UsageRecord should have RawJsonPath property");
        Assert.Equal(typeof(string), props[nameof(UsageRecord.RawJsonPath)].PropertyType);

        // Verify default values
        var defaultEntity = new UsageRecord();
        Assert.Equal(0, defaultEntity.CacheReadTokens);
        Assert.Equal(0, defaultEntity.CacheCreationTokens);
        Assert.Equal(0, defaultEntity.LatencyMs);
        Assert.Equal(3, defaultEntity.Tier);
        Assert.Null(defaultEntity.ErrorType);
        Assert.Null(defaultEntity.ErrorMessage);
        Assert.Null(defaultEntity.ErrorStatusCode);
        Assert.Null(defaultEntity.RawJsonPath);
    }
}
