using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Startup settings: launch on Windows startup, restore last session, minimize to tray.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Startup
    // ================================================================

    [ObservableProperty]
    private bool _launchOnWindowsStartup;

    partial void OnLaunchOnWindowsStartupChanged(bool value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null)
                return;

            if (value)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("MySecondBrain", $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue("MySecondBrain") is not null)
                    key.DeleteValue("MySecondBrain");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Windows startup registry key");
        }
    }

    [ObservableProperty]
    private bool _restoreLastSession;

    partial void OnRestoreLastSessionChanged(bool value)
        => _ = _settingsRepo.SetAsync("RestoreLastSession", value ? "true" : "false");

    [ObservableProperty]
    private bool _minimizeToTray = true;

    partial void OnMinimizeToTrayChanged(bool value)
        => _ = _settingsRepo.SetAsync("MinimizeToTray", value ? "true" : "false");
}
