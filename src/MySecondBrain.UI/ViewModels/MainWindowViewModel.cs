using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IThemeProvider _themeProvider;
    private readonly ISystemTrayService _systemTray;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private ScreenType _selectedScreen = ScreenType.Chats;

    [ObservableProperty]
    private bool _isRightPanelVisible = true;

    [ObservableProperty]
    private string _themeToggleIcon = "☀";

    [ObservableProperty]
    private string _fontSizeDisplay = string.Empty;

    [ObservableProperty]
    private ChatTheme _currentChatTheme = ChatTheme.Classic;

    public AppTheme CurrentAppTheme => _themeProvider.CurrentAppTheme;

    public DataTemplate? CurrentMessageTemplate =>
        _themeProvider.GetChatMessageTemplate(CurrentChatTheme);

    public List<ChatTheme> ChatThemeOptions { get; } =
        [ChatTheme.Classic, ChatTheme.Compact, ChatTheme.Bubble];

    /// <summary>
    /// Called by the generated [ObservableProperty] setter whenever CurrentChatTheme changes.
    /// Persists the selection via the theme provider and notifies the template binding.
    /// </summary>
    partial void OnCurrentChatThemeChanged(ChatTheme value)
    {
        _themeProvider.SetChatTheme(value);
        OnPropertyChanged(nameof(CurrentMessageTemplate));
    }

    partial void OnSelectedScreenChanged(ScreenType value)
    {
        // Right panel (Artifacts + Chat Nav) is only for Studio Chat screen
        // All other screens have their own layouts per vision mocks
        IsRightPanelVisible = value == ScreenType.Chats;
    }

    public MainWindowViewModel(
        IThemeProvider themeProvider,
        ISystemTrayService systemTray,
        ILogger<MainWindowViewModel> logger)
    {
        _themeProvider = themeProvider;
        _systemTray = systemTray;
        _logger = logger;
        _currentChatTheme = _themeProvider.CurrentChatTheme;
        FontSizeDisplay = _themeProvider.FontSize.ToString("F0");

        _themeProvider.ChatThemeChanged += OnChatThemeChanged;
    }

    private void OnChatThemeChanged(object? sender, ChatTheme theme)
    {
        CurrentChatTheme = theme;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var newTheme = _themeProvider.CurrentAppTheme == AppTheme.Light
            ? AppTheme.Dark : AppTheme.Light;
        _themeProvider.SetAppTheme(newTheme);
        ThemeToggleIcon = newTheme == AppTheme.Dark ? "🌙" : "☀";
        OnPropertyChanged(nameof(CurrentAppTheme));
    }

    [RelayCommand]
    private void IncreaseFont()
    {
        var current = _themeProvider.FontSize;
        if (current >= 24) return;
        var newSize = current + 1;
        _themeProvider.SetFontSettings(_themeProvider.FontFamily, newSize, _themeProvider.FontWeight);
        FontSizeDisplay = newSize.ToString("F0");
    }

    [RelayCommand]
    private void DecreaseFont()
    {
        var current = _themeProvider.FontSize;
        if (current <= 10) return;
        var newSize = current - 1;
        _themeProvider.SetFontSettings(_themeProvider.FontFamily, newSize, _themeProvider.FontWeight);
        FontSizeDisplay = newSize.ToString("F0");
    }

    [RelayCommand]
    private void SetChatTheme(string themeName)
    {
        if (Enum.TryParse<ChatTheme>(themeName, out var theme))
        {
            CurrentChatTheme = theme;
        }
    }
}
