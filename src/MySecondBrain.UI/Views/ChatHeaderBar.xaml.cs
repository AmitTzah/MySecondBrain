using System.Windows;
using System.Windows.Controls.Primitives;

namespace MySecondBrain.UI.Views;

/// <summary>
/// Interaction logic for ChatHeaderBar.xaml
/// Provides the full chat header layout with persona name, theme selector,
/// API history, token bar, cost, source banner, font controls, dark mode,
/// pin, help, and three-dot menu. Handles popup toggling for system message
/// editor, help overlay, and context menu display.
/// </summary>
public partial class ChatHeaderBar
{
    public ChatHeaderBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the system message editor popover when the persona name is clicked.
    /// Setting the ViewModel property triggers the popup binding.
    /// </summary>
    private void PersonaNameBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ChatThreadViewModel vm)
        {
            vm.EditSystemMessageCommand.Execute(null);
        }
    }

    /// <summary>
    /// Opens the help context menu when the ? button is clicked.
    /// </summary>
    private void HelpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (HelpBtn.ContextMenu is not null)
        {
            HelpBtn.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// Opens the three-dot context menu when the ⋯ button is clicked.
    /// </summary>
    private void ThreeDotBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ThreeDotBtn.ContextMenu is not null)
        {
            ThreeDotBtn.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// Exposes the help overlay popup so the keyboard shortcuts popup can
    /// be toggled from the ViewModel via code-behind interaction.
    /// </summary>
    public Popup HelpOverlay => HelpOverlayPopup;
}
