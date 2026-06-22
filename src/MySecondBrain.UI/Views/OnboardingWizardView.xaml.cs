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
    /// Uses Keyboard.IsKeyDown for modifier detection and auto-applies
    /// on valid keypress, matching SettingsView behavior.
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

        // Collect currently held modifier keys — use Keyboard.IsKeyDown for reliability
        var modifiers = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers.Add("Win");

        // Resolve the actual key: when Alt is held, WPF delivers Key.System
        // with the real key in e.SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only presses — wait for a non-modifier key
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
            return;

        // Require at least one modifier for a valid hotkey
        if (modifiers.Count == 0)
            return;

        // Format the key name
        var keyName = FormatKeyName(key);

        // Build combo string from modifiers + key
        modifiers.Add(keyName);
        var combo = string.Join("+", modifiers);

        // Update live display
        ViewModel.RecordingHotkeyCombo = combo;

        // Auto-apply on valid keypress (matching Settings behavior)
        ViewModel.ApplyRecordedHotkey(combo);
    }

    /// <summary>
    /// Converts a WPF Key enum value to a human-readable string suitable for display.
    /// </summary>
    private static string FormatKeyName(Key key)
    {
        // Digit keys D0-D9 → "0"-"9"
        if (key >= Key.D0 && key <= Key.D9)
            return key.ToString().Substring(1);

        // Numpad keys
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return "Num" + key.ToString().Substring(6);

        // Oem keys
        return key switch
        {
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemBackslash => "\\",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            _ => key.ToString(),
        };
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
