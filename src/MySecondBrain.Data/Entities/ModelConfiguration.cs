using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MySecondBrain.Data.Entities;

[Index(nameof(DisplayName), IsUnique = true)]
public class ModelConfiguration
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Must match a configured ApiKey provider.
    /// </summary>
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// FK to ApiKey.
    /// </summary>
    public string? ApiKeyId { get; set; }

    /// <summary>
    /// e.g., "gpt-4o", "claude-sonnet-4-20250514".
    /// </summary>
    [MaxLength(100)]
    public string? ModelIdentifier { get; set; }

    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 1.0;

    public int MaxOutputTokens { get; set; } = 4096;

    public int MaxContextWindow { get; set; } = 128000;

    public bool ThinkingEnabled { get; set; }

    /// <summary>
    /// USD per 1000 input tokens.
    /// </summary>
    public decimal? PricingInputPer1K { get; set; }

    /// <summary>
    /// USD per 1000 output tokens.
    /// </summary>
    public decimal? PricingOutputPer1K { get; set; }

    /// <summary>
    /// SlidingWindow, HardStop, or AutoSummarize.
    /// </summary>
    [MaxLength(50)]
    public string ContextOverflowStrategy { get; set; } = "SlidingWindow";

    // Navigation
    public ApiKey? ApiKey { get; set; }

    public ICollection<Persona> Personas { get; set; } = new List<Persona>();

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<TextAction> TextActions { get; set; } = new List<TextAction>();

    public ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();
}
