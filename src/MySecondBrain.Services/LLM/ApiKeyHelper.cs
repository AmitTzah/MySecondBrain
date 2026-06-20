namespace MySecondBrain.Services.LLM;

/// <summary>
/// Shared helper for API key operations across LLM providers.
/// </summary>
internal static class ApiKeyHelper
{
    /// <summary>
    /// Masks an API key for safe logging: shows first 3 characters and last 4.
    /// Returns "***" for null, empty, or very short keys.
    /// </summary>
    public static string MaskKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 7)
            return "***";

        return apiKey[..3] + "..." + apiKey[^4..];
    }
}
