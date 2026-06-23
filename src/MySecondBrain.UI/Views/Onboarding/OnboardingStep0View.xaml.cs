using System.Windows;
using System.Windows.Controls;
using MySecondBrain.UI.ViewModels;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views.Onboarding;

public partial class OnboardingStep0View : WpfUserControl
{
    private OnboardingWizardViewModel ViewModel => (OnboardingWizardViewModel)DataContext!;

    public OnboardingStep0View()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Bridges the PasswordBox secure password to the ViewModel's ApiKeyInputValue.
    /// </summary>
    public string ApiKeyPassword
    {
        get => ViewModel.ApiKeyInputValue;
        set
        {
            ViewModel.ApiKeyInputValue = value;
            if (ApiKeyPasswordBox is not null && ApiKeyPasswordBox.Password != value)
                ApiKeyPasswordBox.Password = value;
        }
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.ApiKeyInputValue = pb.Password;
        }
    }
}
