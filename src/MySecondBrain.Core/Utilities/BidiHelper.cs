using System.Windows;

namespace MySecondBrain.Core.Utilities;

/// <summary>
/// Detects Hebrew (RTL) text in message content and returns the appropriate
/// WPF <see cref="FlowDirection"/> per message.
/// </summary>
public static class BidiHelper
{
    /// <summary>
    /// Returns <see cref="FlowDirection.RightToLeft"/> if more than 30% of
    /// alphabetic characters in <paramref name="content"/> fall within the
    /// Hebrew Unicode range (U+0590–U+05FF).
    /// </summary>
    public static FlowDirection GetMessageFlowDirection(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return FlowDirection.LeftToRight;

        int hebrewChars = 0, totalLetters = 0;
        foreach (var c in content)
        {
            if (char.IsLetter(c))
            {
                totalLetters++;
                if (c >= 0x0590 && c <= 0x05FF)
                    hebrewChars++;
            }
        }

        if (totalLetters == 0)
            return FlowDirection.LeftToRight;

        return (double)hebrewChars / totalLetters > 0.3
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    /// <summary>
    /// Code blocks always use <see cref="FlowDirection.LeftToRight"/>,
    /// even when the surrounding message is RTL.
    /// </summary>
    public static FlowDirection CodeBlockFlowDirection => FlowDirection.LeftToRight;
}
