using System.Windows;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.UI.Views;

public partial class OnboardingWizardWindow : Window
{
    private readonly OnboardingWizardViewModel _viewModel;
    private bool _isCloseConfirmed;

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
    }
}
