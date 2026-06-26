using System.Windows;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.UI.Views;

public partial class LockedChatPasswordDialog : Window
{
    private readonly LockedChatViewModel _viewModel;

    public LockedChatPasswordDialog(LockedChatViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordInput.Password;
        if (_viewModel.IsLockMode)
        {
            _viewModel.ConfirmPassword = ConfirmPasswordInput.Password;
        }

        var (success, result) = await _viewModel.ConfirmAsync();
        if (success)
        {
            DialogResult = true;
            Close();
        }
        else if (result is string error)
        {
            _viewModel.ErrorMessage = error;
            _viewModel.HasError = true;
            PasswordInput.Clear();
            ConfirmPasswordInput.Clear();
        }
    }
}
