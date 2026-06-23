using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Security settings: encryption status, locked chats, global password.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Security — Encryption, locked chats, password
    // ================================================================

    public string EncryptionStatus => "✓ API keys encrypted via Windows DPAPI";

    [ObservableProperty]
    private bool _lockedChatPasswordSet;

    [ObservableProperty]
    private bool _hideLockedChats;

    partial void OnHideLockedChatsChanged(bool value)
        => _ = _settingsRepo.SetAsync("HideLockedChats", value ? "true" : "false");

    [RelayCommand]
    private void SetGlobalPassword()
    {
        if (LockedChatPasswordSet)
        {
            StatusMessage = "Change password — not yet implemented.";
        }
        else
        {
            StatusMessage = "Set global password — placeholder dialog.";
        }
    }
}
