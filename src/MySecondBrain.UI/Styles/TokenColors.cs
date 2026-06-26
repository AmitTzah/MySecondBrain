using System.Windows.Media;

namespace MySecondBrain.UI.Styles;

/// <summary>
/// Shared token threshold colors used by both TokenContextBar and
/// TokenCountToColorConverter to ensure visual consistency.
/// </summary>
internal static class TokenColors
{
    /// <summary>Used when context fill is under 70%.</summary>
    public static readonly System.Windows.Media.Color Green = System.Windows.Media.Color.FromRgb(76, 175, 80);

    /// <summary>Used when context fill is between 70% and 90%.</summary>
    public static readonly System.Windows.Media.Color Yellow = System.Windows.Media.Color.FromRgb(255, 193, 7);

    /// <summary>Used when context fill exceeds 90%.</summary>
    public static readonly System.Windows.Media.Color Red = System.Windows.Media.Color.FromRgb(244, 67, 54);

    public static readonly SolidColorBrush GreenBrush = new(Green);
    public static readonly SolidColorBrush YellowBrush = new(Yellow);
    public static readonly SolidColorBrush RedBrush = new(Red);

    /// <summary>
    /// Returns the appropriate color for the given usage percentage.
    /// </summary>
    public static System.Windows.Media.Color GetColor(double percentage)
    {
        return percentage switch
        {
            < 0.7 => Green,
            < 0.9 => Yellow,
            _ => Red
        };
    }

    /// <summary>
    /// Returns the appropriate brush for the given usage percentage.
    /// </summary>
    public static SolidColorBrush GetBrush(double percentage)
    {
        return percentage switch
        {
            < 0.7 => GreenBrush,
            < 0.9 => YellowBrush,
            _ => RedBrush
        };
    }
}
