using System.ComponentModel;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.UI;

public partial class MainWindow : Window
{
    private readonly ISystemTrayService _systemTrayService;
    private readonly bool _minimizeToTray;

    public MainWindow(
        MainWindowViewModel viewModel,
        ISystemTrayService systemTrayService,
        ISettingsRepository settingsRepository)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(systemTrayService);
        ArgumentNullException.ThrowIfNull(settingsRepository);

        InitializeComponent();
        DataContext = viewModel;
        _systemTrayService = systemTrayService;

        // Cache minimize-to-tray setting at construction (read-once during startup)
        var minimizeSetting = settingsRepository.GetAsync("MinimizeToTray")
            .GetAwaiter().GetResult();
        _minimizeToTray = minimizeSetting is null ||
            (bool.TryParse(minimizeSetting, out var val) && val);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_systemTrayService.IsVisible && _minimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
