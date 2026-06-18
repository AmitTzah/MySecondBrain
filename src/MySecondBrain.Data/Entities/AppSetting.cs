using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class AppSetting
{
    /// <summary>
    /// Unique setting key (e.g., "theme", "last-active-thread").
    /// </summary>
    [Key]
    [MaxLength(256)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Setting value. JSON-serialized for complex types.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Value type hint: "String", "Int32", "Boolean", or "Json".
    /// </summary>
    [MaxLength(20)]
    public string ValueType { get; set; } = "String";

    /// <summary>
    /// When the setting was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
