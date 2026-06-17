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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public Message Message { get; set; } = null!;

    public ChatThread Thread { get; set; } = null!;

    public Persona? Persona { get; set; }

    public ModelConfiguration? ModelConfig { get; set; }
}
