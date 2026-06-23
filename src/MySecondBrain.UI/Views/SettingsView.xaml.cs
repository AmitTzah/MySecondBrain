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
        try
        {
            if (!DesignerProperties.GetIsInDesignMode(this) && App.Current is not null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
            }
        }
        catch
        {
            // DI container may not be initialized during testing; fall through
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
            if (sender is Border border)
            {
                border.Focus();
            }
        }
    }

    /// <summary>
    /// Prevents click-through events from passing through the semi-transparent overlay.
    /// </summary>
    private void OnRecordingOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// Captures keyboard input during hotkey recording.
    /// </summary>
    private void OnRecordingKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_viewModel?.IsRecordingHotkey != true)
            return;

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            _viewModel.CancelHotkeyRecording();
            return;
        }

        var modifiers = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers.Add("Win");

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
            return;

        if (modifiers.Count == 0)
            return;

        var keyName = FormatKeyName(key);
        var combo = string.Join("+", modifiers.Concat(new[] { keyName }));

        _viewModel.RecordingHotkeyCombo = combo;
        _viewModel.ApplyHotkeyChange(combo);

        if (_viewModel.IsEditingTextAction)
            _viewModel.ApplyRecordedHotkey(combo);
    }

    private static string FormatKeyName(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
            return key.ToString().Substring(1);

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return "Num" + key.ToString().Substring(6);

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
}
