using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MySecondBrain.UI.Styles;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a token usage percentage (0.0 to 1.0) to a Color/Brush.
/// Green when under 70%, Yellow at 70-90%, Red when over 90%.
/// Accepts double, float, or int (treated as raw count, use ConverterParameter for max).
/// </summary>
public class TokenCountToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double percentage;

        if (value is double d)
            percentage = d;
        else if (value is float f)
            percentage = f;
        else if (value is int i)
        {
            // If parameter is provided, treat it as max - compute percentage
            if (parameter is string maxStr && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var max) && max > 0)
                percentage = i / max;
            else
                percentage = 0;
        }
        else
            percentage = 0;

        // Clamp
        percentage = Math.Clamp(percentage, 0, 1);

        var color = TokenColors.GetColor(percentage);

        if (targetType == typeof(System.Windows.Media.Color))
            return color;

        if (targetType == typeof(System.Windows.Media.Brush) || targetType == typeof(SolidColorBrush))
            return TokenColors.GetBrush(percentage);

        // Default: return Color
        return color;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
