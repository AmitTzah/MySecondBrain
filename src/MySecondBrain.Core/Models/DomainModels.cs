namespace MySecondBrain.Core.Models;

/// <summary>
/// Domain model classes referenced by Core interfaces.
/// These are simple POCOs; EF Core entity configuration happens in the Data project.
/// </summary>

public class ChatThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Title { get; set; }
    public bool IsTransient { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PersonaId { get; set; }
    public string? ModelConfigId { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ThreadId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? RawContent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ModelName { get; set; }
    public string? BranchId { get; set; }
    public string? ParentMessageId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public bool IsActiveBranch { get; set; } = true;
    public bool? IsDirectTransformation { get; set; }
    public string? Feedback { get; set; }
    public decimal? EstimatedCost { get; set; }
    public long? GenerationTimeMs { get; set; }
}

public class Persona
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? DefaultModelConfigId { get; set; }
    public string DefaultChatMode { get; set; } = "Standard";
    public bool IsBuiltIn { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ModelConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string ModelIdentifier { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public string? ApiKeyId { get; set; }
    public double Temperature { get; set; } = 1.0;
    public int MaxOutputTokens { get; set; } = 131072;
    public int MaxContextWindow { get; set; } = 1000000;
    public bool ThinkingEnabled { get; set; }
    public int? ThinkingTokens { get; set; }
    public decimal? PricingInputPer1K { get; set; }
    public decimal? PricingOutputPer1K { get; set; }
    public decimal? PricingCacheHitPer1K { get; set; }
    public decimal? PricingCacheMissPer1K { get; set; }
    public string ContextOverflowStrategy { get; set; } = "SlidingWindow";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ProviderType ProviderType { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? CustomProviderName { get; set; }
    public string? CustomEndpointUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTestedAt { get; set; }
    public bool IsValid { get; set; }
}

public class WikiFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RelativePath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<string> Backlinks { get; set; } = new List<string>();
}

public class WikiVersionSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class UsageRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ThreadId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string ModelIdentifier { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Settings for auto-approving tool calls without user confirmation.
/// </summary>
public class ToolAutoApprovalSettings
{
    public bool AutoApproveWebSearch { get; set; }
    public bool AutoApproveFileGenerate { get; set; }
    public bool AutoApproveFileEdit { get; set; }
    public bool AutoApproveWikiSearch { get; set; }
    public int MaxConsecutiveAutoApprovals { get; set; } = 10;
}

public class TextAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string? ModelConfigId { get; set; }
    public string? Hotkey { get; set; }
    public string CaptureScope { get; set; } = "selection";
    public string ApplyMode { get; set; } = "replaceSelection";
    public bool IsBuiltIn { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
