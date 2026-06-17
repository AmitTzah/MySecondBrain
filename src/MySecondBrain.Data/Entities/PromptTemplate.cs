using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class PromptTemplate
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Contains {{variables}} like {{clipboard}}, {{selected_text}}, {{date}}, {{current_wiki_file}}.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized array of organization tags.
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Parent prompt folder identifier.
    /// </summary>
    public string? FolderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
