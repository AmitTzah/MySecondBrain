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

    public AppTheme CurrentAppTheme => _themeProvider.CurrentAppTheme;

    public ChatTheme CurrentChatTheme => _themeProvider.CurrentChatTheme;

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
        FontSizeDisplay = _themeProvider.FontSize.ToString("F0");
    }

    [RelayCommand]
    private void Navigate(string screenName)
    {
        if (Enum.TryParse<ScreenType>(screenName, out var screen))
        {
            SelectedScreen = screen;
        }
        else
        {
            _logger.LogWarning("Unrecognized screen name in Navigate: {ScreenName}", screenName);
        }
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
}
