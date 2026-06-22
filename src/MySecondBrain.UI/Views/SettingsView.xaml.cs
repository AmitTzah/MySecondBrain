using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>
    /// Called when the recording overlay becomes visible — sets keyboard focus so
    /// KeyDown events are captured immediately. Also resets the combo display.
    /// </summary>
    private void OnRecordingOverlayVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            if (_viewModel is not null)
            {
                _viewModel.RecordingHotkeyCombo = string.Empty;
            }
            // Focus the overlay Border so it receives KeyDown events
            if (sender is Border border)
            {
                border.Focus();
            }
        }
    }

    /// <summary>
    /// Prevents click-through events from passing through the semi-transparent overlay to controls beneath.
    /// </summary>
    private void OnRecordingOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// Captures keyboard input during hotkey recording. Escape cancels; a valid
    /// modifier+key combo is formatted and applied via the ViewModel.
    /// </summary>
    private void OnRecordingKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_viewModel?.IsRecordingHotkey != true)
            return;

        e.Handled = true;

        // Escape cancels recording
        if (e.Key == Key.Escape)
        {
            _viewModel.CancelHotkeyRecording();
            return;
        }

        // Collect currently held modifier keys
        var modifiers = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers.Add("Alt");
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

        var combo = string.Join("+", modifiers.Concat(new[] { keyName }));

        // Update live display
        _viewModel.RecordingHotkeyCombo = combo;

        // ApplyHotkeyChange runs first (synchronous start of async void) so the
        // _changingHotkeyItem guard is evaluated before ApplyRecordedHotkey sets
        // IsRecordingHotkey = false.
        _viewModel.ApplyHotkeyChange(combo);

        // ApplyRecordedHotkey is only relevant when the Text Actions form is open.
        if (_viewModel.IsEditingTextAction)
            _viewModel.ApplyRecordedHotkey(combo);
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
            // Both OemPipe (US layout) and OemBackslash (102-key layout)
            // produce the backslash key — covered here for keyboard-layout
            // compatibility.
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
}
