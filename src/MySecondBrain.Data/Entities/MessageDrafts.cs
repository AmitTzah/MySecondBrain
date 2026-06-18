using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class MessageDrafts
{
    /// <summary>
    /// Thread ID serves as the primary key (one draft per thread).
    /// </summary>
    [Key]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Current textbox content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Current cursor position in the textbox.
    /// </summary>
    public int CursorPosition { get; set; }

    /// <summary>
    /// When the draft was last saved.
    /// </summary>
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
