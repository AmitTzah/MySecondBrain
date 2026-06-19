using System;
using System.Globalization;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Compares a bound enum value (e.g., ScreenType.Chats) against the ConverterParameter string.
/// Returns true when they match, enabling RadioButton.IsChecked to follow SelectedScreen.
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
        throw new NotSupportedException();
    }
}
