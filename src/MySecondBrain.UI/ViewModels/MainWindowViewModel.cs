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

    public MainWindowViewModel(
        IThemeProvider themeProvider,
        ISystemTrayService systemTray,
        ILogger<MainWindowViewModel> logger)
    {
        _themeProvider = themeProvider;
        _systemTray = systemTray;
        _logger = logger;
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
}
