
using System.ComponentModel;
using System.Windows;

using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.UI.Views.Settings;

public partial class ProvidersSettingsControl
{
    private SettingsViewModel? _viewModel;

    public ProvidersSettingsControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as SettingsViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Wire up PasswordBox changes to ViewModel
        ApiKeyPasswordBox.PasswordChanged += OnApiKeyPasswordChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        if (e.PropertyName == nameof(SettingsViewModel.ApiKeyInputValue))
        {
            if (string.IsNullOrWhiteSpace(_viewModel.ApiKeyInputValue))
            {
                ApiKeyPasswordBox.Password = string.Empty;
            }
            else
            {
                ApiKeyPasswordBox.Password = _viewModel.ApiKeyInputValue;
            }
        }
        else if (e.PropertyName == nameof(SettingsViewModel.IsEditingKey)
                 && _viewModel.IsEditingKey == true)
        {
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
