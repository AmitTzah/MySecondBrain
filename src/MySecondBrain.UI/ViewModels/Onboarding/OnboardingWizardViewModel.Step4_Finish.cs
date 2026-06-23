using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Step 4 — Finish summary and launch studio.
/// </summary>
public partial class OnboardingWizardViewModel
{
    public int ConfiguredKeyCount { get; set; }
    public string ConfiguredPersonaName { get; set; } = string.Empty;
    public string ConfiguredWikiPath { get; set; } = string.Empty;
    public int ConfiguredHotkeyCount { get; set; }

    [RelayCommand]
    private async Task LaunchStudio()
    {
        // Persist hotkey changes first so they show in Settings Hotkeys tab
        await SaveHotkeysToRepositoryAsync();

        // Save all keys to the repository
        await SaveKeysToRepositoryAsync();

        // Mark onboarding as fully completed so it won't show on next launch
        await _settingsRepo.SetAsync("Onboarding_Completed", "true");

        // Fire event so App.xaml.cs can close wizard and open Studio
        LaunchStudioRequested?.Invoke();

        // Also send messenger message for loose coupling
        WeakReferenceMessenger.Default.Send(new LaunchStudioMessage());
    }

    [RelayCommand]
    private void ImportFromChatGpt()
    {
        _confirmationService.Confirm(
            "Import from ChatGPT or Claude is coming soon. Stay tuned for updates!",
            "Coming Soon");
    }
}
