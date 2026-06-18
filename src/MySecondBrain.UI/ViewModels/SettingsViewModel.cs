using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IThemeProvider _themeProvider;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        ISettingsRepository settingsRepo,
        IThemeProvider themeProvider,
        ILogger<SettingsViewModel> logger)
    {
        _settingsRepo = settingsRepo;
        _themeProvider = themeProvider;
        _logger = logger;
    }
}
