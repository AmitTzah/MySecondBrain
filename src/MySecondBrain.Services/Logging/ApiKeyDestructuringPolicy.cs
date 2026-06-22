using Serilog.Core;
using Serilog.Events;

namespace MySecondBrain.Services.Logging;

/// <summary>
/// Serilog IDestructuringPolicy that redacts strings matching common API key patterns.
/// Registered via .Destructure.With{ApikeyDestructuringPolicy}() in the LoggerConfiguration chain.
/// </summary>
public class ApiKeyDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        if (value is string s && IsApiKeyString(s))
        {
            result = new ScalarValue("[REDACTED]");
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// Determines whether the given string looks like an API key.
    /// Returns true for strings >= 20 characters matching common API key prefixes.
    /// </summary>
    public static bool IsApiKeyString(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 20)
            return false;

        // OpenAI/Anthropic prefixes are case-insensitive by convention
        return value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("sk-ant-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("sk-proj-", StringComparison.OrdinalIgnoreCase)
            // Google API keys always start with uppercase "AIza" — case-sensitive match
            || value.StartsWith("AIza", StringComparison.Ordinal);
    }
}
