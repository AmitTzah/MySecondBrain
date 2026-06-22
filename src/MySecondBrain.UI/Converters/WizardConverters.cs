using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrushes = System.Windows.Media.Brushes;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// StepDotConverter: multi-purpose converter for step indicator dots.
/// ConverterParameter values:
///   "bg" → takes bool (completed): AccentBrush if completed, Transparent otherwise
///   "border" → takes int (currentStep): AccentBrush if step matches, BorderBrush otherwise
///   "icon" → takes bool (completed): "✓" if completed, empty string otherwise
///   "fg" → takes bool (completed): AccentForeground if completed, Transparent otherwise
/// </summary>
public class StepDotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var param = parameter as string;

        // "bg" parameter: takes bool (completed)
        if (param == "bg")
        {
            if (value is bool completed && completed)
                return WpfApplication.Current.TryFindResource("AccentBrush") ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
            return WpfBrushes.Transparent;
        }

        // "border" parameter: takes string "currentStep:stepIndex" (e.g., "0:2")
        // value is CurrentStep (int)
        if (param == "border" && value is int currentStep)
        {
            return WpfApplication.Current.TryFindResource("AccentBrush") ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
        }

        // "icon" parameter: takes bool (completed)
        if (param == "icon")
        {
            if (value is bool completed && completed)
                return "✓";
            return "";
        }

        // "fg" parameter: takes bool (completed)
        if (param == "fg")
        {
            if (value is bool completed && completed)
                return WpfApplication.Current.TryFindResource("AccentForeground") ?? WpfBrushes.White;
            return WpfBrushes.Transparent;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// ProviderTypeDisplayConverter: converts ProviderType enum to display string.
/// </summary>
public class ProviderTypeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        return value.ToString()!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// StepVisibilityConverter: shows the content panel whose step matches CurrentStep.
/// ConverterParameter is the step number string (e.g., "0", "1").
/// </summary>
public class StepVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepStr
            && int.TryParse(stepStr, out var step))
        {
            return currentStep == step ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// EmptyStateVisibilityConverter: Visible when count is 0 (empty), Collapsed otherwise.
/// Used to show "no items yet" empty state messages.
/// </summary>
public class EmptyStateVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count && count == 0)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// NullToVisibilityConverter: Collapsed when value is null or empty, Visible otherwise.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return Visibility.Collapsed;
        if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// InvertedVisibilityConverter: Visible when true/false inverted.
/// </summary>
public class InvertedVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
