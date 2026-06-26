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

    // Organization fields
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public string? ColorLabel { get; set; }
    public string? Tags { get; set; } // JSON array as string
    public string? FolderId { get; set; }

    // Locked chat fields
    public bool IsLocked { get; set; }
    public string? LockSalt { get; set; } // Base64
    public string? LockNonce { get; set; } // Base64

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

    // New fields
    public bool IsFavorited { get; set; }
    public string? ThinkingContent { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
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
    public string? PersonaId { get; set; }
    public string? ModelConfigId { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── Step 2: Enriched fields ─────────────────────────────────────

    /// <summary>Cache read/hit tokens (provider-agnostic).</summary>
    public int CacheReadTokens { get; set; }

    /// <summary>Cache creation/write tokens.</summary>
    public int CacheCreationTokens { get; set; }

    /// <summary>Time from request sent to full response complete, in milliseconds.</summary>
    public int LatencyMs { get; set; }

    /// <summary>Which interaction tier: 1=Hotkey, 2=CommandBar, 3=Studio.</summary>
    public int Tier { get; set; } = 3;

    /// <summary>Null if successful. "auth", "rate_limit", "network", "timeout", "server", "unknown".</summary>
    public string? ErrorType { get; set; }

    /// <summary>Human-readable error message. Null if successful.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>HTTP status code from the provider. Null if successful.</summary>
    public int? ErrorStatusCode { get; set; }

    /// <summary>Path to per-chat raw JSON log.</summary>
    public string? RawJsonPath { get; set; }
}

/// <summary>
/// Settings for auto-approving tool calls without user confirmation.
/// </summary>
public class ToolAutoApprovalSettings
{
    public bool AutoApproveBash { get; set; }

    // ── File operation tools ────────────────────────────────────
    // Read operations are auto-approved by default in workspace.
    // Write/apply operations always require user confirmation.
    public bool AutoApproveReadFile { get; set; }
    public bool AutoApproveListFiles { get; set; }
    public bool AutoApproveSearchFiles { get; set; }
    public bool AutoApproveApplyDiff { get; set; }
    public bool AutoApproveWriteToFile { get; set; }

    // ── Knowledge and communication tools ────────────────────────
    public bool AutoApproveWebSearch { get; set; }
    public bool AutoApproveWebFetch { get; set; }
    public bool AutoApproveWikiSearch { get; set; }
    public bool AutoApproveMemory { get; set; }
    public bool AutoApproveSkillLoad { get; set; }
    public bool AutoApproveAskUserInput { get; set; }
    public bool AutoApprovePresentFiles { get; set; }
    public bool AutoApproveImageSearch { get; set; }

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
    public string ChatMode { get; set; } = "Standard";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A key-value memory entry using Anthropic's memory_20250818 schema.
/// Thread-scoped; persisted to SQLite via the MemoryEntryEntity.
/// </summary>
public class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? SourceThreadId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Maximum length for the Key property (200 characters).
    /// MUST be kept in sync with MemoryEntryEntityConsts.KeyMaxLength in the Data project.</summary>
    public const int KeyMaxLength = 200;

    /// <summary>Maximum length for the Value property (10,240 characters, ~10KB for ASCII content).
    /// MUST be kept in sync with MemoryEntryEntityConsts.ValueMaxLength in the Data project.</summary>
    public const int ValueMaxLength = 10240;
}
