using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class MediaItem
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path on disk.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Image, Audio, or Video.
    /// </summary>
    [MaxLength(20)]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// e.g., "image/png".
    /// </summary>
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// UserUpload, AIGenerated, WebcamCapture, or Screenshot.
    /// </summary>
    [MaxLength(30)]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// FK to ChatThread.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// FK to Message (containing message).
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Prompt used (AI-generated only).
    /// </summary>
    public string? GeneratedPrompt { get; set; }

    public bool IsSavedToDisk { get; set; }

    public bool IsSavedToWiki { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public ChatThread Thread { get; set; } = null!;

    public Message? Message { get; set; }
}
