using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class TextAction
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Instructs how to transform text.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// FK to ModelConfiguration.
    /// </summary>
    public string? ModelConfigId { get; set; }

    /// <summary>
    /// e.g., "Alt+Q".
    /// </summary>
    [MaxLength(50)]
    public string? Hotkey { get; set; }

    public bool IsBuiltIn { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public ModelConfiguration? ModelConfig { get; set; }
}
