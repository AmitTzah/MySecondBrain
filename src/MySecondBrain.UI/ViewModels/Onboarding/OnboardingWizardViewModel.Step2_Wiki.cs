using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Step 2 — Wiki: choose an existing wiki folder or create a new one.
/// </summary>
public partial class OnboardingWizardViewModel
{
    [ObservableProperty]
    private string _wikiDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _wikiFileCount = string.Empty;

    [ObservableProperty]
    private bool _gitVersionControlEnabled;

    [ObservableProperty]
    private bool _gitAutoCommitEnabled = true;

    [ObservableProperty]
    private bool _isWikiCreating;

    [ObservableProperty]
    private string _wikiStatusMessage = string.Empty;

    [RelayCommand]
    private async Task ChooseExistingWikiFolderAsync()
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
                    WikiStatusMessage = "Selected directory does not exist.";
                    return;
                }

                WikiDirectoryPath = path;
                await _settingsRepo.SetAsync("WikiDirectoryPath", path);

                var mdCount = Directory.GetFiles(path, "*.md", SearchOption.AllDirectories).Length;
                WikiFileCount = $"{mdCount} .md file(s) found";
                WikiStatusMessage = "Indexing wiki files...";

                try
                {
                    await _wikiService.IndexAllAsync(CancellationToken.None);
                    WikiStatusMessage = "Wiki indexed successfully.";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wiki indexing failed during onboarding");
                    WikiStatusMessage = "Wiki directory set. Indexing will run in background.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to choose wiki folder");
            WikiStatusMessage = "Could not open folder picker.";
        }
    }

    [RelayCommand]
    private async Task CreateNewWikiFolderAsync()
    {
        if (IsWikiCreating) return;
        IsWikiCreating = true;

        try
        {
            var wikiPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MySecondBrain-Wiki");

            Directory.CreateDirectory(wikiPath);

            var indexPath = Path.Combine(wikiPath, "index.md");
            if (!File.Exists(indexPath))
            {
                await File.WriteAllTextAsync(indexPath,
                    "# My Wiki\n\nWelcome to your personal wiki. Add .md files here to build your second brain.\n");
            }

            WikiDirectoryPath = wikiPath;
            WikiFileCount = "1 .md file (index.md)";
            WikiStatusMessage = "Created with starter index.md";

            await _settingsRepo.SetAsync("WikiDirectoryPath", wikiPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create wiki folder");
            WikiStatusMessage = "Could not create wiki folder.";
        }
        finally
        {
            IsWikiCreating = false;
        }
    }

    partial void OnGitVersionControlEnabledChanged(bool value)
    {
        _ = _settingsRepo.SetAsync("GitVersionControlEnabled", value ? "true" : "false");
    }
}
