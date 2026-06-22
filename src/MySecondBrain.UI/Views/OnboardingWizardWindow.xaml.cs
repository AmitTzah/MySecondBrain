using System.Windows;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.UI.Views;

public partial class OnboardingWizardWindow : Window
{
    private readonly OnboardingWizardViewModel _viewModel;
    private bool _isCloseConfirmed;

    /// <summary>
    /// Tracks whether the LaunchStudioRequested event has been handled to
    /// avoid multiple close attempts when the event fires more than once.
    /// </summary>
    private bool _launchStudioHandled;

    public OnboardingWizardWindow(OnboardingWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Wire close-with-progress-saved behavior.
        // Use _isCloseConfirmed flag to prevent infinite recursion:
        // Closing -> confirm dialog -> Close() -> Closing again.
        Closing += (_, args) =>
        {
            if (_isCloseConfirmed)
                return;

            if (_viewModel.CurrentStep < 4 && _viewModel.CurrentStep >= 0)
            {
                args.Cancel = true;
                _viewModel.RequestCloseCommand.Execute(null);
            }
        };

        // After the ViewModel confirms close, actually close the window
        viewModel.CloseConfirmed += () =>
        {
            _isCloseConfirmed = true;
            Dispatcher.Invoke(Close);
        };

        // When "Launch Studio" is clicked (from first-launch or re-run),
        // close this window so the main window is revealed.
        viewModel.LaunchStudioRequested += () =>
        {
            if (_launchStudioHandled) return;
            _launchStudioHandled = true;
            _isCloseConfirmed = true; // Skip the closing confirmation
            Dispatcher.Invoke(Close);
        };
    }
}
