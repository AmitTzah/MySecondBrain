using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Maintenance settings: database file size, reclaimable space, compaction.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Maintenance — Database compaction
    // ================================================================

    [ObservableProperty]
    private string _databaseFileSize = string.Empty;

    [ObservableProperty]
    private string _reclaimableSpace = string.Empty;

    [ObservableProperty]
    private string _lastCompaction = string.Empty;

    [ObservableProperty]
    private bool _isCompacting;

    [ObservableProperty]
    private bool _isBusy;

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    [RelayCommand]
    private async Task CompactDatabaseAsync()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "msb.db");

        if (!File.Exists(dbPath))
        {
            StatusMessage = "Database file not found.";
            return;
        }

        var beforeSize = new FileInfo(dbPath).Length;
        DatabaseFileSize = FormatFileSize(beforeSize);

        IsCompacting = true;
        StatusMessage = "Compacting database...";

        try
        {
            await _db.Database.ExecuteSqlRawAsync("VACUUM;");
            var afterSize = new FileInfo(dbPath).Length;
            var reclaimed = beforeSize - afterSize;

            ReclaimableSpace = FormatFileSize(reclaimed);
            DatabaseFileSize = FormatFileSize(afterSize);
            LastCompaction = DateTimeOffset.UtcNow.ToString("g");
            await _settingsRepo.SetAsync("LastCompaction", LastCompaction);

            StatusMessage = reclaimed > 0
                ? $"Compaction complete. Reclaimed {FormatFileSize(reclaimed)}."
                : "Compaction complete. No reclaimable space.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VACUUM failed");
            StatusMessage = "Compaction failed. Check available disk space.";
        }
        finally
        {
            IsCompacting = false;
        }
    }
}
