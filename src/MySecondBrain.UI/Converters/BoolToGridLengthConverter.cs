using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MySecondBrain.UI.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isVisible = value is true;
        var width = parameter is string s && double.TryParse(s, out var d) ? d : 320.0;
        return isVisible ? new GridLength(width) : new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
