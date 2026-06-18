using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MySecondBrain.Data.Entities;

[Index(nameof(DisplayName), IsUnique = true)]
public class Persona
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Supports {{variables}} resolved at send time.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// FK to ModelConfiguration.
    /// </summary>
    public string? DefaultModelConfigId { get; set; }

    /// <summary>
    /// Standard or TextCompletion.
    /// </summary>
    [MaxLength(50)]
    public string DefaultChatMode { get; set; } = "Standard";

    public bool IsBuiltIn { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ModelConfiguration? DefaultModelConfig { get; set; }

    public ICollection<ChatThread> ChatThreads { get; set; } = new List<ChatThread>();

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();
}
