using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Editable classification for an app data location.
/// </summary>
public enum LocationEditability
{
    AppManaged,
    Caution,
    UserEditable
}

/// <summary>
/// Represents a single app data file or directory displayed in the System Info settings panel.
/// </summary>
public partial class DataLocationInfo : ObservableObject
{
    private readonly ILogger? _logger;

    public DataLocationInfo(
        string displayPath,
        string? expandedPath,
        string purpose,
        LocationEditability editability,
        ILogger? logger = null)
    {
        DisplayPath = displayPath;
        ExpandedPath = expandedPath;
        Purpose = purpose;
        Editability = editability;
        _logger = logger;
    }

    /// <summary>
    /// Human-readable path using %LOCALAPPDATA% / %USERPROFILE% notation.
    /// </summary>
    public string DisplayPath { get; }

    /// <summary>
    /// The actual expanded file system path (shown in tooltip).
    /// </summary>
    public string? ExpandedPath { get; }

    /// <summary>
    /// What this file/folder is used for.
    /// </summary>
    public string Purpose { get; }

    /// <summary>
    /// Whether and how the user can edit this location.
    /// </summary>
    public LocationEditability Editability { get; }

    /// <summary>
    /// Whether this is a directory (vs a file). Affects "Open in Explorer" behavior.
    /// When true, opens the directory itself. When false, opens parent folder with file selected.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Unique key for AutomationProperties.AutomationId.
    /// </summary>
    public string AutomationKey { get; init; } = string.Empty;

    /// <summary>
    /// Display icon for editability status.
    /// </summary>
    public string EditabilityIcon => Editability switch
    {
        LocationEditability.AppManaged => "❌",
        LocationEditability.Caution => "⚠️",
        LocationEditability.UserEditable => "✅",
        _ => "❓",
    };

    /// <summary>
    /// Display label for editability status.
    /// </summary>
    public string EditabilityLabel => Editability switch
    {
        LocationEditability.AppManaged => "No — app-managed",
        LocationEditability.Caution => "Caution — editable but risky",
        LocationEditability.UserEditable => "Yes — user-editable",
        _ => "Unknown",
    };

    /// <summary>
    /// Tooltip explaining the editability classification.
    /// </summary>
    public string EditabilityTooltip => Editability switch
    {
        LocationEditability.AppManaged => "Do not modify manually. This is managed internally by the application.",
        LocationEditability.Caution => "Editable but changes may affect application behavior. Understand the consequences before modifying.",
        LocationEditability.UserEditable => "Your own files — safe to modify, add, or remove.",
        _ => string.Empty,
    };

    [ObservableProperty]
    private string _sizeOnDisk = "Calculating…";

    [ObservableProperty]
    private bool _isSizeCalculated;

    /// <summary>
    /// Resolves the actual path, calculates disk size, and updates <see cref="SizeOnDisk"/>.
    /// </summary>
    public async Task CalculateSizeAsync()
    {
        var path = ExpandedPath;
        if (string.IsNullOrEmpty(path))
        {
            SizeOnDisk = "—";
            IsSizeCalculated = true;
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    long totalSize = 0;
                    try
                    {
                        totalSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                            .Sum(f => f.Length);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        SizeOnDisk = "⚠️ Cannot access";
                        IsSizeCalculated = true;
                        return;
                    }
                    SizeOnDisk = FormatSize(totalSize);
                }
                else if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    SizeOnDisk = FormatSize(fi.Length);
                }
                else
                {
                    SizeOnDisk = "—";
                }
                IsSizeCalculated = true;
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to calculate size for path {Path}", path);
            SizeOnDisk = "⚠️ Cannot access";
            IsSizeCalculated = true;
        }
    }

    /// <summary>
    /// Opens the location in Windows Explorer.
    /// For files: opens parent folder with the file selected.
    /// For directories: opens the directory itself.
    /// </summary>
    [RelayCommand]
    private void OpenInExplorer()
    {
        var path = ExpandedPath;
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (IsDirectory)
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select, " + path);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open path in Explorer: {Path}", path);
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
