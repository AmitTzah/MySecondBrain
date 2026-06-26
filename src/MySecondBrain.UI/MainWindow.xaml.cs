using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.UI.ViewModels;
using MySecondBrain.UI.Views;

namespace MySecondBrain.UI;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISystemTrayService _systemTrayService;
    private readonly IConfirmationService _confirmationService;
    private readonly ILogger<MainWindow> _logger;
    private readonly bool _minimizeToTray;

    public MainWindow(
        MainWindowViewModel viewModel,
        ISystemTrayService systemTrayService,
        ISettingsRepository settingsRepository,
        IConfirmationService confirmationService,
        ILogger<MainWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(systemTrayService);
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(confirmationService);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _systemTrayService = systemTrayService;
        _confirmationService = confirmationService;
        _logger = logger;

        // Cache minimize-to-tray setting at construction (read-once during startup)
        var minimizeSetting = settingsRepository.GetAsync("MinimizeToTray")
            .GetAwaiter().GetResult();
        _minimizeToTray = minimizeSetting is null ||
            (bool.TryParse(minimizeSetting, out var val) && val);
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        var mods = e.KeyboardDevice.Modifiers;
        var ctrl = mods.HasFlag(ModifierKeys.Control);
        var shift = mods.HasFlag(ModifierKeys.Shift);

        if (ctrl && !shift)
        {
            switch (e.Key)
            {
                case Key.N:
                    _viewModel.ChatThreadViewModel?.NewChatCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.W:
                    _viewModel.ChatThreadViewModel?.CloseTabCommand.Execute(
                        _viewModel.ChatThreadViewModel.ActiveTab);
                    e.Handled = true;
                    break;
                case Key.O:
                    // Ctrl+O: Open file in file viewer tab
                    OpenFileInViewer();
                    e.Handled = true;
                    break;
                case Key.Tab:
                    // Ctrl+Tab: switch to next tab
                    var tabs = _viewModel.ChatThreadViewModel?.ChatTabs;
                    var active = _viewModel.ChatThreadViewModel?.ActiveTab;
                    if (tabs?.Count > 0 && active is not null)
                    {
                        var idx = tabs.IndexOf(active);
                        var nextIdx = (idx + 1) % tabs.Count;
                        _viewModel.ChatThreadViewModel!.ActiveTab = tabs[nextIdx];
                    }
                    e.Handled = true;
                    break;
            }
        }
        else if (ctrl && shift)
        {
            switch (e.Key)
            {
                case Key.T:
                    _viewModel.ChatThreadViewModel?.ReopenLastClosedTabCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    // Ctrl+Shift+Tab: switch to previous tab
                    var tabs = _viewModel.ChatThreadViewModel?.ChatTabs;
                    var active = _viewModel.ChatThreadViewModel?.ActiveTab;
                    if (tabs?.Count > 0 && active is not null)
                    {
                        var idx = tabs.IndexOf(active);
                        var prevIdx = (idx - 1 + tabs.Count) % tabs.Count;
                        _viewModel.ChatThreadViewModel!.ActiveTab = tabs[prevIdx];
                    }
                    e.Handled = true;
                    break;
            }
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Opens a file picker dialog and loads the selected file into a file viewer tab.
    /// Called via Ctrl+O.
    /// </summary>
    private async void OpenFileInViewer()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open File in Viewer",
                Multiselect = false,
                CheckFileExists = true,
                Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|Markdown (*.md)|*.md|" +
                         "Code Files (*.cs;*.py;*.js;*.ts;*.json;*.xml;*.html;*.css)|*.cs;*.py;*.js;*.ts;*.json;*.xml;*.html;*.css"
            };

            if (dialog.ShowDialog(this) == true)
            {
                var vm = await FileViewerTabViewModel.FromFileAsync(dialog.FileName);
                var chatVm = _viewModel.ChatThreadViewModel;
                if (chatVm is null) return;

                // Create a tab for the file viewer
                var thread = await chatVm.NewFileViewerTab(vm);
                if (thread is not null)
                {
                    _logger.LogInformation("Opened file viewer tab: {Path}", dialog.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file in viewer");
            System.Windows.MessageBox.Show(this,
                $"Failed to open file: {ex.Message}",
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Check if any chat tab is streaming
        var chatVm = _viewModel.ChatThreadViewModel;
        if (chatVm?.ChatTabs.Any(t => t.IsStreaming) == true)
        {
            // Fall back to a simple confirmation if the service isn't available via resources
            var result = System.Windows.MessageBox.Show(
                "A response is still being generated. Are you sure you want to close?",
                "Generation in Progress",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);

        if (_systemTrayService.IsVisible && _minimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        // Only cleanup ViewModel resources on true close, not minimize-to-tray
        chatVm?.Cleanup();
    }
}
