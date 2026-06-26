using System.Globalization;
using System.Windows.Data;
using MySecondBrain.Core.Models;
// Resolve ambiguity with System.Windows.Forms.Message
using Message = MySecondBrain.Core.Models.Message;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a <see cref="Message"/> to its token usage / cost / generation time display string.
/// Format: "1,234 prompt + 567 completion = 1,801 tokens · $0.042 · Generated in 3.2s"
/// </summary>
public class MessageTokenDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Message msg)
            return string.Empty;

        if (msg.PromptTokens is null && msg.CompletionTokens is null && msg.TotalTokens is null)
        {
            // No token info — show cost and time if available
            var parts = new List<string>();
            if (msg.EstimatedCost is not null)
                parts.Add($"${msg.EstimatedCost:F4}");
            if (msg.GenerationTimeMs is not null)
                parts.Add($"Generated in {msg.GenerationTimeMs / 1000.0:F1}s");
            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }

        var tokens = new List<string>();
        if (msg.PromptTokens is not null)
            tokens.Add($"{msg.PromptTokens:N0} prompt");
        if (msg.CompletionTokens is not null)
            tokens.Add($"{msg.CompletionTokens:N0} completion");
        if (msg.TotalTokens is not null)
            tokens.Add($"= {msg.TotalTokens:N0} tokens");

        var result = string.Join(" + ", tokens);
        if (msg.EstimatedCost is not null)
            result += $" · ${msg.EstimatedCost:F4}";
        if (msg.GenerationTimeMs is not null)
            result += $" · Generated in {msg.GenerationTimeMs / 1000.0:F1}s";
        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
