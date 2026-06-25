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
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// FK to ModelConfiguration.
    /// </summary>
    public string? ModelConfigId { get; set; }

    /// <summary>
    /// e.g., "Alt+Q".
    /// </summary>
    [MaxLength(50)]
    public string? Hotkey { get; set; }

    /// <summary>
    /// Comma-separated capture scope flags: selection, focusedElement, surroundingContext, fullDocument, screenshot.
    /// </summary>
    [MaxLength(100)]
    public string CaptureScope { get; set; } = "selection";

    /// <summary>
    /// Where to put the AI result: replaceSelection, insertAtCursor, replaceFocusedElement, appendToFocusedElement, prependToFocusedElement, clipboardOnly, showOnly.
    /// </summary>
    [MaxLength(50)]
    public string ApplyMode { get; set; } = "replaceSelection";

    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Chat mode for this text action: "Standard" (chat API with system prompt) or
    /// "TextCompletion" (raw prompt to raw completion).
    /// "Continue Writing" defaults to TextCompletion. All others default to Standard.
    /// </summary>
    [MaxLength(20)]
    public string ChatMode { get; set; } = "Standard";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public ModelConfiguration? ModelConfig { get; set; }
}
