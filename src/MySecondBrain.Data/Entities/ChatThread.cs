using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MySecondBrain.Data.Entities;

public class ChatThread
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// true for Tier 1/2, false for Tier 3 (permanent).
    /// </summary>
    public bool IsTransient { get; set; }

    /// <summary>
    /// FK to Persona.
    /// </summary>
    public string? PersonaId { get; set; }

    /// <summary>
    /// FK to ModelConfiguration.
    /// </summary>
    public string? ModelConfigId { get; set; }

    /// <summary>
    /// Per-chat system message override.
    /// </summary>
    public string? SystemMessage { get; set; }

    /// <summary>
    /// Standard or TextCompletion.
    /// </summary>
    [MaxLength(50)]
    public string ChatMode { get; set; } = "Standard";

    public bool ThinkingEnabled { get; set; }

    public bool IsMuted { get; set; }

    // -- Organization fields --

    public bool IsFavorite { get; set; }

    public bool IsPinned { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>
    /// Hex color or preset name.
    /// </summary>
    [MaxLength(20)]
    public string? ColorLabel { get; set; }

    /// <summary>
    /// JSON-serialized array of user-defined tags.
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Parent folder identifier.
    /// </summary>
    public string? FolderId { get; set; }

    // -- Locked chat fields --

    /// <summary>
    /// Whether this chat is password-protected.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Base64-encoded cryptographic salt for password-based key derivation.
    /// </summary>
    public string? LockSalt { get; set; }

    /// <summary>
    /// Base64-encoded nonce for AES-GCM encryption of locked messages.
    /// </summary>
    public string? LockNonce { get; set; }

    // -- Soft-delete --

    /// <summary>
    /// Soft-delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Set when IsDeleted becomes true. 30-day auto-purge.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // -- Source context (Tier 1 elevation only, nullable) --

    public int? SourceHWND { get; set; }

    [MaxLength(200)]
    public string? SourceAppName { get; set; }

    [MaxLength(500)]
    public string? SourceDocTitle { get; set; }

    public string? OriginalHighlightedText { get; set; }

    // -- Timestamps --

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    [ForeignKey(nameof(PersonaId))]
    public Persona? Persona { get; set; }

    [ForeignKey(nameof(ModelConfigId))]
    public ModelConfiguration? ModelConfig { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();

    public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();

    public ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();

    public ICollection<MemoryEntryEntity> MemoryEntries { get; set; } = new List<MemoryEntryEntity>();
}
