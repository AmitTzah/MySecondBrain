using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;

using Serilog;

namespace MySecondBrain.UI;

/// <summary>
/// Static helpers for App startup orchestration.
/// Extracted from App.xaml.cs to keep the App class focused on startup lifecycle.
/// </summary>
internal static class AppStartupHelpers
{
    /// <summary>
    /// Wires the system tray service events after a main window is available.
    /// </summary>
    internal static void WireTrayService(IServiceProvider serviceProvider, Window mainWindow, ILogger<App> startupLogger)
    {
        var trayService = serviceProvider.GetRequiredService<ISystemTrayService>();
        var mainWindowViewModel = mainWindow.DataContext as MainWindowViewModel;
        if (mainWindowViewModel is null)
            startupLogger.LogWarning("MainWindow.DataContext is not a MainWindowViewModel");

        trayService.Show();

        trayService.OpenStudioRequested += (_, _) =>
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            });
        };

        trayService.NewChatRequested += (_, _) =>
        {
            startupLogger.LogInformation("New chat requested — not yet implemented");
        };

        trayService.CommandBarRequested += (_, _) =>
        {
            startupLogger.LogInformation("Command bar requested — not yet implemented");
        };

        trayService.SettingsRequested += (_, _) =>
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                if (mainWindowViewModel is not null)
                    mainWindowViewModel.SelectedScreen = ScreenType.Settings;
            });
        };

        trayService.ExitRequested += (_, _) => App.Current.Shutdown();
    }

    /// <summary>
    /// Starts background services (WebSocket server, global hotkeys) after the main window is shown.
    /// </summary>
    internal static void StartBackgroundServices(IServiceProvider serviceProvider, ILogger<App> startupLogger)
    {
        try
        {
            // Start the embedded Kestrel WebSocket server (non-blocking)
            _ = StartWebSocketServerAsync(serviceProvider, startupLogger);

            // Start global hotkey service
            var hotkeyService = serviceProvider.GetRequiredService<IGlobalHotkeyService>();
            var hotkeyCount = hotkeyService.GetRegisteredHotkeys().Count;
            startupLogger.LogInformation("Global hotkey service started with {Count} default hotkeys", hotkeyCount);
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "StartBackgroundServices failed");
        }
    }

    /// <summary>
    /// Starts the embedded Kestrel WebSocket server in a fire-and-forget task.
    /// </summary>
    internal static async Task StartWebSocketServerAsync(IServiceProvider serviceProvider, ILogger<App>? startupLogger)
    {
        try
        {
            var wsServer = serviceProvider.GetRequiredService<ILocalWebSocketServer>();
            await wsServer.StartAsync();
            startupLogger?.LogInformation("WebSocket server lifecycle started");
        }
        catch (Exception ex)
        {
            startupLogger?.LogError(ex, "Failed to start WebSocket server");
        }
    }
}
