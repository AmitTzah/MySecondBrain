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
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? ModelName { get; set; }
    public string? BranchId { get; set; }
    public string? ParentMessageId { get; set; }
    public bool IsActiveBranch { get; set; }
}

public class Persona
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public bool IsDefault { get; set; }
}

public class ModelConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public bool ThinkingEnabled { get; set; }
    public int? ThinkingTokens { get; set; }
}

public class ApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ProviderType ProviderType { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public string? Label { get; set; }
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
    public string ChatThreadId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
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
