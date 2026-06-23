using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Backup provider status, schedule, and manual backup commands.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Backup — Provider status, schedule, manual backup
    // ================================================================

    [ObservableProperty]
    private string _backupProviderStatus = string.Empty;

    [ObservableProperty]
    private string _backupSchedule = "Daily";

    [ObservableProperty]
    private string _lastBackupTime = string.Empty;

    partial void OnBackupScheduleChanged(string value)
        => _ = _settingsRepo.SetAsync("BackupSchedule", value);

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        IsBusy = true;
        StatusMessage = "Starting backup...";

        try
        {
            using var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            await writer.WriteAsync($"MySecondBrain backup - {DateTimeOffset.UtcNow:O}");
            await writer.FlushAsync();
            memoryStream.Position = 0;

            var result = await _backupProvider.UploadAsync(
                memoryStream,
                $"backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
                CancellationToken.None);

            LastBackupTime = DateTimeOffset.UtcNow.ToString("g");
            await _settingsRepo.SetAsync("LastBackupTime", LastBackupTime);
            StatusMessage = $"Backup completed. {result.SizeBytes / 1024.0:F1} KB uploaded (ID: {result.BackupId[..Math.Min(8, result.BackupId.Length)]}...)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            StatusMessage = "Backup failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ConfigureBackup()
    {
        StatusMessage = "Backup configuration coming in Feature 16.";
    }
}
