using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Notification settings: sound on completion, disable streaming, cross-tab alert.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Notifications
    // ================================================================

    [ObservableProperty]
    private bool _soundOnCompletion;

    partial void OnSoundOnCompletionChanged(bool value)
        => _ = _settingsRepo.SetAsync("SoundOnCompletion", value ? "true" : "false");

    [ObservableProperty]
    private bool _disableStreaming;

    partial void OnDisableStreamingChanged(bool value)
        => _ = _settingsRepo.SetAsync("DisableStreaming", value ? "true" : "false");

    [ObservableProperty]
    private bool _crossTabCompletionAlert = true;

    partial void OnCrossTabCompletionAlertChanged(bool value)
        => _ = _settingsRepo.SetAsync("CrossTabCompletionAlert", value ? "true" : "false");
}
