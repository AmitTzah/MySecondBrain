using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Update settings: check frequency, current version, check for updates command.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Updates
    // ================================================================

    [ObservableProperty]
    private string _updateCheckFrequency = "OnStartup";

    public IReadOnlyList<string> UpdateCheckFrequencyOptions { get; } =
    [
        "OnStartup",
        "Daily",
        "Weekly",
        "ManualOnly",
    ];

    partial void OnUpdateCheckFrequencyChanged(string value)
        => _ = _settingsRepo.SetAsync("UpdateCheckFrequency", value);

    public string CurrentVersion { get; }

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Checking for updates...";

        try
        {
            var result = await _updateChecker.CheckForUpdatesAsync(CancellationToken.None);

            if (result.ErrorMessage is not null)
            {
                UpdateStatusMessage = $"Update check failed: {result.ErrorMessage}";
            }
            else if (result.UpdateAvailable && result.Update is not null)
            {
                UpdateStatusMessage =
                    $"Update {result.Update.Version} is available. " +
                    $"Release date: {result.Update.ReleaseDate:yyyy-MM-dd}. " +
                    $"{(result.Update.IsMandatory ? "This is a mandatory update." : "")}";
            }
            else
            {
                UpdateStatusMessage = "You're up to date!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            UpdateStatusMessage = "Could not check for updates. Check your internet connection.";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}
