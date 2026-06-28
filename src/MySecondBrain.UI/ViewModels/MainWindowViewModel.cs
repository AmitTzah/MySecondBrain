using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISystemTrayService _systemTray;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private ScreenType _selectedScreen = ScreenType.Chats;

    [ObservableProperty]
    private bool _isRightPanelVisible = true;

    [ObservableProperty]
    private ChatTheme _currentChatTheme = ChatTheme.Classic;

    public ChatThreadViewModel ChatThreadViewModel { get; }

    public DataTemplate? CurrentMessageTemplate =>
        System.Windows.Application.Current?.Resources[$"{CurrentChatTheme}UserTemplate"] as DataTemplate
        ?? System.Windows.Application.Current?.Resources["ClassicUserTemplate"] as DataTemplate;

    public List<ChatTheme> ChatThemeOptions { get; } =
        [ChatTheme.Classic, ChatTheme.Compact, ChatTheme.Bubble];

    /// <summary>
    /// Called by the generated [ObservableProperty] setter whenever CurrentChatTheme changes.
    /// Notifies the template binding.
    /// </summary>
    partial void OnCurrentChatThemeChanged(ChatTheme value)
    {
        OnPropertyChanged(nameof(CurrentMessageTemplate));
    }

    partial void OnSelectedScreenChanged(ScreenType value)
    {
        // Right panel (Artifacts + Chat Nav) is only for Studio Chat screen
        // All other screens have their own layouts per vision mocks
        IsRightPanelVisible = value == ScreenType.Chats;
    }

    public MainWindowViewModel(
        ISystemTrayService systemTray,
        ILogger<MainWindowViewModel> logger,
        ChatThreadViewModel chatThreadViewModel)
    {
        _systemTray = systemTray;
        _logger = logger;
        ChatThreadViewModel = chatThreadViewModel;
    }

    [RelayCommand]
    private void SetChatTheme(string themeName)
    {
        if (Enum.TryParse<ChatTheme>(themeName, out var theme))
        {
            CurrentChatTheme = theme;
        }
    }

    [RelayCommand]
    private void Navigate(string screenName)
    {
        if (Enum.TryParse<ScreenType>(screenName, out var screen))
            SelectedScreen = screen;
        else
            _logger.LogWarning("Unrecognized screen name: {ScreenName}", screenName);
    }
}
