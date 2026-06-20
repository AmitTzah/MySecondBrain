using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MySecondBrain.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.InitializeCommand.Execute(null);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Wire up PasswordBox changes to ViewModel
        ApiKeyPasswordBox.PasswordChanged += OnApiKeyPasswordChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ApiKeyInputValue)
            && string.IsNullOrEmpty(_viewModel?.ApiKeyInputValue))
        {
            // Clear PasswordBox when ViewModel clears the input value
            ApiKeyPasswordBox.Password = string.Empty;
        }
        else if (e.PropertyName == nameof(SettingsViewModel.IsEditingKey)
                 && _viewModel?.IsEditingKey == true)
        {
            // When entering edit mode, clear the PasswordBox
            ApiKeyPasswordBox.Password = string.Empty;
        }
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ApiKeyInputValue = ApiKeyPasswordBox.Password;
        }
    }
}
