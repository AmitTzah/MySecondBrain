using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class ApiKey
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Provider identifier: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAICompatible.
    /// </summary>
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CustomProviderName { get; set; }

    [MaxLength(500)]
    public string? CustomEndpointUrl { get; set; }

    /// <summary>
    /// Encrypted via DPAPI at rest. Never displayed in full after save.
    /// </summary>
    public string KeyValue { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public DateTimeOffset? LastTestedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<ModelConfiguration> ModelConfigurations { get; set; } = new List<ModelConfiguration>();
}
