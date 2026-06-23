using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Language settings: AutoDetectRtl.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Language — AutoDetectRtl
    // ================================================================

    [ObservableProperty]
    private bool _autoDetectRtl = true;

    partial void OnAutoDetectRtlChanged(bool value)
        => _ = _settingsRepo.SetAsync("AutoDetectRtl", value ? "true" : "false");
}
