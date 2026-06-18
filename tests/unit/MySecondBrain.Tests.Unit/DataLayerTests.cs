using System.Reflection;
using MySecondBrain.Data.Entities;

namespace MySecondBrain.Tests.Unit;

public class DataLayerTests
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
}
