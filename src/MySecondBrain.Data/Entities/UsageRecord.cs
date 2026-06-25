using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class UsageRecord
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// FK to Message.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// FK to ChatThread (denormalized for dashboard queries).
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// FK to Persona (denormalized for persona-level aggregation).
    /// </summary>
    public string? PersonaId { get; set; }

    /// <summary>
    /// FK to ModelConfiguration.
    /// </summary>
    public string? ModelConfigId { get; set; }

    /// <summary>
    /// For provider-level aggregation.
    /// </summary>
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// For model-level aggregation.
    /// </summary>
    [MaxLength(100)]
    public string ModelIdentifier { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    /// <summary>
    /// USD, from pricing config × tokens.
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    // ── Step 2: Enriched fields ─────────────────────────────────────

    /// <summary>
    /// Cache read/hit tokens (provider-agnostic). Anthropic: cache_read_input_tokens. DeepSeek: cache_hit_tokens.
    /// </summary>
    public int CacheReadTokens { get; set; }

    /// <summary>
    /// Cache creation/write tokens. Anthropic: cache_creation_input_tokens. DeepSeek: cache_miss_tokens.
    /// </summary>
    public int CacheCreationTokens { get; set; }

    /// <summary>
    /// Time from request sent to full response complete, in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Which interaction tier generated this call: 1=Hotkey, 2=CommandBar, 3=Studio.
    /// </summary>
    public int Tier { get; set; } = 3;

    /// <summary>
    /// Null if successful. "auth", "rate_limit", "network", "timeout", "server", "unknown".
    /// </summary>
    [MaxLength(50)]
    public string? ErrorType { get; set; }

    /// <summary>
    /// Human-readable error message. Null if successful.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// HTTP status code from the provider. Null if successful.
    /// </summary>
    public int? ErrorStatusCode { get; set; }

    /// <summary>
    /// Path to per-chat raw JSON log: %LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json
    /// </summary>
    public string? RawJsonPath { get; set; }

    // ────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public Message Message { get; set; } = null!;

    public ChatThread Thread { get; set; } = null!;

    public Persona? Persona { get; set; }

    public ModelConfiguration? ModelConfig { get; set; }
}
