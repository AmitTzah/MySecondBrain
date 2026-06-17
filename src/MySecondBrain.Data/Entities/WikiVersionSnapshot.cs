using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MySecondBrain.Data.Entities;

public class WikiVersionSnapshot
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// FK to WikiFile. Uses FilePath as the principal key since WikiFile's PK is its file path.
    /// </summary>
    [ForeignKey(nameof(WikiFile))]
    public string WikiFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Full file content at snapshot time.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// WriteToWiki, ManualEdit, or Restore.
    /// </summary>
    [MaxLength(30)]
    public string Source { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    public WikiFile WikiFile { get; set; } = null!;
}
