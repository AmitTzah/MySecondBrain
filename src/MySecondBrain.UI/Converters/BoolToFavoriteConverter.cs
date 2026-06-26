using System.Globalization;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a boolean IsFavorited value to a star symbol (★ for favorited, ☆ for not favorited).
/// </summary>
public class BoolToFavoriteConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFavorited)
            return isFavorited ? "★" : "☆";
        return "☆";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
