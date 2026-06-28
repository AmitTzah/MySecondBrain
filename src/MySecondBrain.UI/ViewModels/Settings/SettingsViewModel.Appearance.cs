using CommunityToolkit.Mvvm.ComponentModel;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Appearance settings: ChatTheme only.
/// WPF-UI handles the application theme (Light mode only).
/// Font settings have been removed — WPF-UI Fluent styles define typography.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Appearance — ChatTheme
    // ================================================================

    [ObservableProperty]
    private ChatTheme _chatTheme = ChatTheme.Classic;

    public IReadOnlyList<ChatTheme> ChatThemeOptions { get; } =
    [
        ChatTheme.Classic,
        ChatTheme.Compact,
        ChatTheme.Bubble,
    ];

    partial void OnChatThemeChanged(ChatTheme value)
    {
        _ = _settingsRepo.SetAsync("ChatTheme", value.ToString());
    }
}
