using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class WikiFile
{
    /// <summary>
    /// Relative to wiki root directory. Serves as primary key.
    /// </summary>
    [Key]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// e.g., "git-cheatsheet.md".
    /// </summary>
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// First H1 heading.
    /// </summary>
    [MaxLength(500)]
    public string? H1Title { get; set; }

    /// <summary>
    /// JSON-serialized array of {level, text, anchor} objects.
    /// </summary>
    public string? Headings { get; set; }

    /// <summary>
    /// Full file content (for FTS5 search).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Approximate word count.
    /// </summary>
    public int? WordCount { get; set; }

    /// <summary>
    /// From file system.
    /// </summary>
    public DateTimeOffset? LastModifiedAt { get; set; }

    /// <summary>
    /// JSON-serialized array of file paths this file links TO.
    /// </summary>
    public string? CrossLinksOut { get; set; }

    /// <summary>
    /// JSON-serialized array of file paths linking TO this file (computed).
    /// </summary>
    public string? CrossLinksIn { get; set; }

    // Navigation

    public ICollection<WikiVersionSnapshot> WikiVersionSnapshots { get; set; } = new List<WikiVersionSnapshot>();
}
