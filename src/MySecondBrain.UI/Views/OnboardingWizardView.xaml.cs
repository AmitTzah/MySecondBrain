using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MySecondBrain.UI.ViewModels;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class OnboardingWizardView : WpfUserControl
{
    private OnboardingWizardViewModel ViewModel => (OnboardingWizardViewModel)DataContext;
    private Window? _parentWindow;

    public OnboardingWizardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _parentWindow = Window.GetWindow(this);
        if (_parentWindow is not null)
        {
            _parentWindow.KeyDown += Window_KeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_parentWindow is not null)
        {
            _parentWindow.KeyDown -= Window_KeyDown;
            _parentWindow = null;
        }
    }

    /// <summary>
    /// Global key handler for the wizard window — handles hotkey recording.
    /// </summary>
    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (!ViewModel.IsRecordingHotkey)
        {
            if (e.Key == Key.Escape)
                return;
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            ViewModel.CancelHotkeyRecording();
            return;
        }

        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ViewModel.RecordingHotkeyCombo))
        {
            ViewModel.ApplyRecordedHotkey(ViewModel.RecordingHotkeyCombo);
            return;
        }

        // Build combo string from modifiers + key
        var modifiers = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers.Add("Win");

        var key = e.Key switch
        {
            Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin => null,
            Key.System => null,
            _ => e.Key.ToString(),
        };

        if (key is null && modifiers.Count == 0)
            return;

        if (key is not null && modifiers.Count > 0)
        {
            var combo = string.Join("+", modifiers) + "+" + key;
            ViewModel.RecordingHotkeyCombo = combo;
        }
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
