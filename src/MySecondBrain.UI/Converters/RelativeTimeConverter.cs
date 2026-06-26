using System.Globalization;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a <see cref="DateTimeOffset"/> to a relative time string
/// like "just now", "2 min ago", "1h ago", "Yesterday", "Jun 15" or "Jun 15, 2025".
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dt)
            return string.Empty;

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

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
