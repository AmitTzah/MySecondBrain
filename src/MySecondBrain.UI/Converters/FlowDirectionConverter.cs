using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MySecondBrain.Core.Utilities;

namespace MySecondBrain.UI.Converters;

/// <summary>
/// Converts a string (message content) to a WPF <see cref="System.Windows.FlowDirection"/>
/// using <see cref="BidiHelper"/> for RTL detection.
/// </summary>
public class FlowDirectionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var content = value as string;
        return BidiHelper.GetMessageFlowDirection(content);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
