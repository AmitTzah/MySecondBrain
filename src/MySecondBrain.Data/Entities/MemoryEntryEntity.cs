using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MySecondBrain.Data.Entities;

/// <summary>
/// A key-value memory entry using Anthropic's memory_20250124 schema.
/// Thread-scoped; persisted to SQLite.
/// </summary>
public class MemoryEntryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Memory key — indexed for fast lookup. Max 200 characters.
    /// </summary>
    [MaxLength(MemoryEntryEntityConsts.KeyMaxLength)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Memory value — up to ~10KB of text content. Max 10,240 characters.
    /// </summary>
    [MaxLength(MemoryEntryEntityConsts.ValueMaxLength)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional FK to ChatThread — the thread this memory belongs to.
    /// Nullable; threads can be deleted without cascading to memory entries.
    /// </summary>
    public string? SourceThreadId { get; set; }

    // -- Timestamps --

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    [ForeignKey(nameof(SourceThreadId))]
    public ChatThread? SourceThread { get; set; }
}

/// <summary>
/// Constant values used by MemoryEntryEntity for validation and schema configuration.
/// MUST be kept in sync with CoreModels.MemoryEntry.KeyMaxLength / ValueMaxLength.
/// </summary>
internal static class MemoryEntryEntityConsts
{
    /// <summary>Maximum length for the Key property (200 characters).</summary>
    public const int KeyMaxLength = 200;

    /// <summary>Maximum length for the Value property (10,240 characters, ~10KB for ASCII content).</summary>
    public const int ValueMaxLength = 10240;
}
