using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        // Resolve the ViewModel from DI before InitializeComponent so
        // XAML bindings resolve immediately. When App.Current is null
        // (e.g., in unit tests or design mode), fall back to inherited
        // DataContext.
        try
        {
            if (!DesignerProperties.GetIsInDesignMode(this) && App.Current is not null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
            }
        }
        catch
        {
            // DI container may not be initialized during testing;
            // fall through to inherited DataContext
        }

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
        if (e.PropertyName == nameof(SettingsViewModel.ApiKeyInputValue))
        {
            if (string.IsNullOrWhiteSpace(_viewModel?.ApiKeyInputValue))
            {
                // Clear PasswordBox when ViewModel clears the input value
                ApiKeyPasswordBox.Password = string.Empty;
            }
            else
            {
                // Sync non-empty ViewModel value to PasswordBox (e.g., during edit pre-fill)
                ApiKeyPasswordBox.Password = _viewModel.ApiKeyInputValue;
            }
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
