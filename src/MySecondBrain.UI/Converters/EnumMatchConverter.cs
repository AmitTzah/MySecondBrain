using System;
using System.Globalization;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Compares a bound enum value (e.g., ScreenType.Chats) against the ConverterParameter string.
/// Returns true when they match, enabling RadioButton.IsChecked to follow SelectedScreen.
/// Supports ConvertBack for two-way binding so RadioButton clicks update the bound property.
/// </summary>
public class EnumMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string paramStr)
            return false;

        return value.ToString() == paramStr;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string paramStr)
        {
            // Parse the parameter string back to the target enum type
            if (targetType.IsEnum && Enum.TryParse(targetType, paramStr, ignoreCase: false, out var result))
                return result;

            // For string targets (e.g., UpdateCheckFrequency)
            if (targetType == typeof(string))
                return paramStr;
        }

        return System.Windows.Data.Binding.DoNothing;
    }
}
