using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Wiki directory, indexing, and git version control settings.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Wiki — Directory, indexing, git
    // ================================================================

    [ObservableProperty]
    private string _wikiDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _indexingStatus = string.Empty;

    [ObservableProperty]
    private bool _gitVersionControlEnabled;

    [ObservableProperty]
    private string _gitStatusMessage = string.Empty;

    partial void OnGitVersionControlEnabledChanged(bool value)
    {
        _ = _settingsRepo.SetAsync("GitVersionControlEnabled", value ? "true" : "false");

        if (value && !string.IsNullOrEmpty(WikiDirectoryPath))
        {
            var gitDir = Path.Combine(WikiDirectoryPath, ".git");
            GitStatusMessage = Directory.Exists(gitDir)
                ? "✓ Git repository detected"
                : "Git repository will be initialized on next re-index.";
        }
        else
        {
            GitStatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ChangeWikiDirectoryAsync()
    {
        try
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select wiki directory containing .md files",
                UseDescriptionForTitle = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var path = dialog.SelectedPath;
                if (!Directory.Exists(path))
                {
                    StatusMessage = "Selected directory does not exist.";
                    return;
                }

                WikiDirectoryPath = path;
                await _settingsRepo.SetAsync("WikiDirectoryPath", path);
                StatusMessage = $"Wiki directory changed to {path}";

                await ReindexWikiAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change wiki directory");
            StatusMessage = "Could not open folder picker.";
        }
    }

    [RelayCommand]
    private async Task ReindexWikiAsync()
    {
        if (string.IsNullOrEmpty(WikiDirectoryPath))
        {
            StatusMessage = "No wiki directory configured. Set one first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Indexing wiki files...";

        try
        {
            await _wikiService.IndexAllAsync(CancellationToken.None);

            var mdCount = Directory.GetFiles(WikiDirectoryPath, "*.md", SearchOption.AllDirectories).Length;

            var indexPath = Path.Combine(WikiDirectoryPath, "index.md");
            var indexCreated = false;
            if (!File.Exists(indexPath))
            {
                await File.WriteAllTextAsync(indexPath, @"# Wiki

Welcome to your MySecondBrain wiki. Add `.md` files here and they will be indexed automatically.
");
                indexCreated = true;
            }

            IndexingStatus = indexCreated
                ? $"✓ {mdCount} .md files indexed, index.md created"
                : $"✓ {mdCount} .md files indexed";
            StatusMessage = $"Wiki re-indexed: {mdCount} files found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki re-index failed");
            StatusMessage = "Wiki re-index failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
