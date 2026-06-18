using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class Artifact
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// e.g., "app.py". Filename-safe, max 255 chars.
    /// </summary>
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Inferred from extension or declared.
    /// </summary>
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// FK to ChatThread.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    public int VersionCount { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public ChatThread Thread { get; set; } = null!;
}
