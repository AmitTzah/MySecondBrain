using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class Message
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// FK to ChatThread.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// User, Assistant, or System.
    /// </summary>
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Markdown text. No hard length limit.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Raw text before Markdown rendering.
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>
    /// FK to Persona (assistant messages only).
    /// </summary>
    public string? PersonaId { get; set; }

    /// <summary>
    /// FK to ModelConfiguration (assistant messages only).
    /// </summary>
    public string? ModelConfigId { get; set; }

    /// <summary>
    /// JSON object: {prompt: int, completion: int}.
    /// </summary>
    public string? TokenCount { get; set; }

    /// <summary>
    /// USD, calculated from tokenCount × pricing.
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Milliseconds request-to-completion.
    /// </summary>
    public long? GenerationTimeMs { get; set; }

    /// <summary>
    /// thumbs_up, thumbs_down, or null.
    /// </summary>
    [MaxLength(20)]
    public string? Feedback { get; set; }

    // -- Branching --

    /// <summary>
    /// FK to self — previous message in conversation chain.
    /// </summary>
    public string? ParentMessageId { get; set; }

    /// <summary>
    /// Default: 1. Increments on edit.
    /// </summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// Groups versions together.
    /// </summary>
    public string BranchId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Whether this version is the active one.
    /// </summary>
    public bool IsActiveBranch { get; set; } = true;

    /// <summary>
    /// Enables [Apply] button for Tier 1 transformations.
    /// </summary>
    public bool? IsDirectTransformation { get; set; }

    // -- Timestamp --

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public ChatThread Thread { get; set; } = null!;

    public Persona? Persona { get; set; }

    public ModelConfiguration? ModelConfig { get; set; }

    public Message? ParentMessage { get; set; }

    public ICollection<Message> ChildMessages { get; set; } = new List<Message>();

    public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();

    public UsageRecord? UsageRecord { get; set; }
}
