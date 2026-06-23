using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Diagnostic settings: log level, log category toggles, log file management.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Diagnostics — Log Level
    // ================================================================

    [ObservableProperty]
    private string _logLevel = "Information";

    public IReadOnlyList<string> LogLevelOptions { get; } =
    [
        "Information",
        "Debug",
        "Verbose",
    ];

    partial void OnLogLevelChanged(string value)
    {
        _ = _settingsRepo.SetAsync("LogLevel", value);
    }

    // ================================================================
    // Diagnostics — Log Category Toggles
    // ================================================================

    [ObservableProperty]
    private bool _logCategory_LLMApiCalls = true;

    [ObservableProperty]
    private bool _logCategory_Tier1HotkeyPipeline = true;

    [ObservableProperty]
    private bool _logCategory_Tier2CommandBar = true;

    [ObservableProperty]
    private bool _logCategory_Database;

    [ObservableProperty]
    private bool _logCategory_WikiFileSystem;

    [ObservableProperty]
    private bool _logCategory_WebSocket;

    [ObservableProperty]
    private bool _logCategory_StartupShutdown;

    [ObservableProperty]
    private bool _logCategory_SystemIntegration;

    partial void OnLogCategory_LLMApiCallsChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_LLMApiCalls", value ? "true" : "false");

    partial void OnLogCategory_Tier1HotkeyPipelineChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_Tier1HotkeyPipeline", value ? "true" : "false");

    partial void OnLogCategory_Tier2CommandBarChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_Tier2CommandBar", value ? "true" : "false");

    partial void OnLogCategory_DatabaseChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_Database", value ? "true" : "false");

    partial void OnLogCategory_WikiFileSystemChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_WikiFileSystem", value ? "true" : "false");

    partial void OnLogCategory_WebSocketChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_WebSocket", value ? "true" : "false");

    partial void OnLogCategory_StartupShutdownChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_StartupShutdown", value ? "true" : "false");

    partial void OnLogCategory_SystemIntegrationChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_SystemIntegration", value ? "true" : "false");

    // ================================================================
    // Diagnostics Commands
    // ================================================================

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(LogsFolderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{LogsFolderPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder");
            StatusMessage = "Could not open logs folder.";
        }
    }

#pragma warning disable CS1998
    [RelayCommand]
    private async Task ClearLogsAsync()
#pragma warning restore CS1998
    {
        var confirmed = _confirmationService.Confirm(
            "Delete all log files in the logs folder? This action cannot be undone.",
            "Clear Logs");

        if (!confirmed)
            return;

        try
        {
            if (!Directory.Exists(LogsFolderPath))
            {
                StatusMessage = "No log files to clear.";
                return;
            }

            var logFiles = Directory.GetFiles(LogsFolderPath, "*.*")
                .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalFiles = logFiles.Count;
            var inUseCount = 0;
            var otherErrorCount = 0;

            foreach (var file in logFiles)
            {
                try { File.Delete(file); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch (UnauthorizedAccessException ex)
                {
                    otherErrorCount++;
                    _logger.LogWarning(ex, "Access denied deleting log file {File}", file);
                }
                catch (IOException ex)
                {
                    inUseCount++;
                    _logger.LogWarning(ex, "Log file {File} is in use and will be rotated automatically", file);
                }
                catch (Exception ex)
                {
                    otherErrorCount++;
                    _logger.LogWarning(ex, "Failed to delete log file {File}", file);
                }
            }

            var clearedCount = totalFiles - inUseCount - otherErrorCount;

            if (otherErrorCount > 0)
            {
                StatusMessage = $"{clearedCount} log files cleared, {otherErrorCount} could not be deleted.";
                if (inUseCount > 0)
                    StatusMessage += $" {inUseCount} file(s) are in use by the app and will be rotated automatically.";
            }
            else if (inUseCount > 0)
            {
                StatusMessage = $"{clearedCount} log files cleared, {inUseCount} in use by the app (will be rotated automatically).";
            }
            else
            {
                StatusMessage = $"All {clearedCount} log files cleared.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear logs");
            StatusMessage = "Could not access logs folder.";
        }
    }
}
