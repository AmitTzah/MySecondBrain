using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

/// <summary>
/// WPF implementation of IConfirmationService using MessageBox.
/// </summary>
public class WpfConfirmationService : IConfirmationService
{
    public bool Confirm(string message, string title)
    {
        return System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
    }
}
