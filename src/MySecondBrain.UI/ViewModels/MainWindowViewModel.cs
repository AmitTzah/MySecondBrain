using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IThemeProvider _themeProvider;
    private readonly ISystemTrayService _systemTray;
    private readonly ILogger<MainWindowViewModel> _logger;

    public MainWindowViewModel(
        IThemeProvider themeProvider,
        ISystemTrayService systemTray,
        ILogger<MainWindowViewModel> logger)
    {
        _themeProvider = themeProvider;
        _systemTray = systemTray;
        _logger = logger;
    }
}
