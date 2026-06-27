using System.Globalization;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a <see cref="DateTimeOffset"/> to a relative time string
/// like "just now", "2 min ago", "1h ago", "Yesterday", "Jun 15" or "Jun 15, 2025".
///
/// Implements <see cref="IMultiValueConverter"/> so that a periodically-updated
/// <c>TimeRefreshToken</c> (second binding value) forces WPF to re-evaluate the
/// conversion, keeping relative timestamps fresh as time passes.
/// </summary>
public class RelativeTimeConverter : IValueConverter, IMultiValueConverter
{
    // ── IValueConverter (single binding — static, one-shot) ──────────

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dt)
            return string.Empty;

        return ComputeRelativeTime(dt);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    // ── IMultiValueConverter (dual binding — refreshes when TimeRefreshToken changes) ──

    /// <summary>
    /// Expects values[0] = DateTimeOffset CreatedAt, values[1] = long TimeRefreshToken (ignored, only used for re-trigger).
    /// </summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is DateTimeOffset dt)
            return ComputeRelativeTime(dt);

        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    // ── Shared logic ─────────────────────────────────────────────────

    private static string ComputeRelativeTime(DateTimeOffset dt)
    {
        var now = DateTimeOffset.UtcNow;
        var diff = now - dt;

        return diff.TotalSeconds switch
        {
            < 60 => "just now",
            < 120 => "1 min ago",
            < 3600 => $"{(int)diff.TotalMinutes} min ago",
            < 7200 => "1h ago",
            < 86400 => $"{(int)diff.TotalHours}h ago",
            < 172800 => "Yesterday",
            < 604800 => $"{(int)diff.TotalDays}d ago",
            _ => dt.Year == now.Year
                ? dt.ToString("MMM dd")
                : dt.ToString("MMM dd, yyyy")
        };
    }
}
