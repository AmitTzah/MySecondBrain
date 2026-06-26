using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a boolean to <see cref="Visibility"/>. Supports an optional
/// ConverterParameter "Inverse" to flip the logic (true → Collapsed, false → Visible).
/// Also handles null (Collapsed) and int (0 → Visible with Inverse, otherwise Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isInverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

        // Handle int values (e.g., ObservableCollection.Count)
        if (value is int count)
        {
            if (isInverse)
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Handle null
        if (value is null)
            return isInverse ? Visibility.Visible : Visibility.Collapsed;

        // Handle bool
        if (value is bool b)
        {
            if (isInverse)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
