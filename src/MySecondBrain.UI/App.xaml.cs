using System.Globalization;
using System.Windows;

using CommunityToolkit.Mvvm.Messaging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.UI.Services;
using MySecondBrain.UI.ViewModels;
using MySecondBrain.UI.Views;

using Serilog;
// Resolves ambiguity with System.Windows.Forms.Application from UseWindowsForms=true
using Application = System.Windows.Application;

namespace MySecondBrain.UI;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;
    private static readonly FontWeightConverter s_fontWeightConverter = new();

    /// <summary>
    /// Provides access to the application's DI service provider from non-DI-aware code (e.g., UserControls in DataTemplates).
    /// </summary>
    public static IServiceProvider ServiceProvider =>
        ((App)Current)._serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var startupLogger = _serviceProvider.GetRequiredService<ILogger<App>>();

        try
        {
            var db = _serviceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            startupLogger.LogInformation("Database migration applied successfully");
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Database migration failed");
            throw; // Re-throw — app cannot function without database
        }

        try
        {
            // Restore saved theme and font settings
            var themeProvider = _serviceProvider.GetRequiredService<IThemeProvider>();
            var settings = _serviceProvider.GetRequiredService<ISettingsRepository>();

            var savedTheme = await settings.GetAsync("AppTheme");
            if (savedTheme is not null && Enum.TryParse<AppTheme>(savedTheme, out var theme))
                themeProvider.SetAppTheme(theme);

            var savedFontFamily = await settings.GetAsync("FontFamily");
            var savedFontSize = await settings.GetAsync("FontSize");
            var savedFontWeight = await settings.GetAsync("FontWeight");

            if (savedFontFamily is not null && savedFontSize is not null
                && double.TryParse(savedFontSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize))
            {
                var fontWeight = FontWeights.Normal;
                if (savedFontWeight is not null)
                {
                    try
                    {
                        if (s_fontWeightConverter.ConvertFromString(savedFontWeight) is FontWeight fw)
                            fontWeight = fw;
                    }
                    catch (Exception ex)
                    {
                        startupLogger.LogWarning(ex, "Failed to parse saved FontWeight '{Value}', falling back to Normal", savedFontWeight);
                    }
                }
                themeProvider.SetFontSettings(savedFontFamily, fontSize, fontWeight);
            }

            // Restore saved chat theme
            var savedChatTheme = await settings.GetAsync("ChatTheme");
            if (savedChatTheme is not null && Enum.TryParse<ChatTheme>(savedChatTheme, out var chatTheme))
                themeProvider.SetChatTheme(chatTheme);

            // Restore saved log level
            var savedLogLevel = await settings.GetAsync("LogLevel");
            if (savedLogLevel is not null)
                startupLogger.LogInformation("Log level restored to {LogLevel}", savedLogLevel);

            // Restore saved log category toggles (log which ones are active for diagnostics)
            var logCategoryKeys = new[]
            {
                "LogCategory_LLMApiCalls",
                "LogCategory_Tier1HotkeyPipeline",
                "LogCategory_Tier2CommandBar",
                "LogCategory_Database",
                "LogCategory_WikiFileSystem",
                "LogCategory_WebSocket",
                "LogCategory_StartupShutdown",
                "LogCategory_SystemIntegration",
            };

            foreach (var key in logCategoryKeys)
            {
                var value = await settings.GetAsync(key);
                if (value is not null)
                    startupLogger.LogDebug("Log category {Key} = {Value}", key, value);
            }
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Failed to restore theme/font settings, continuing with defaults");
        }

        // Log DPI awareness mode and per-monitor DPI info for diagnostics
        try
        {
            startupLogger.LogInformation("DPI mode: PerMonitorV2 (ApplicationHighDpiMode in .csproj)");
            var screenCount = System.Windows.Forms.Screen.AllScreens.Length;
            startupLogger.LogInformation("Screen count: {Count}", screenCount);
            for (var i = 0; i < screenCount; i++)
            {
                var s = System.Windows.Forms.Screen.AllScreens[i];
                startupLogger.LogInformation(
                    "Screen[{Idx}]: bounds=({L},{T})-({R},{B}), primary={Primary}, device={Dev}",
                    i, s.Bounds.Left, s.Bounds.Top, s.Bounds.Right, s.Bounds.Bottom,
                    s.Primary, s.DeviceName?.TrimEnd('\0'));
            }

            // WPF PerMonitorV2 handles DPI scaling automatically via device-independent pixels.
            // The HwndSource will fire DpiChanged events per-monitor when the window moves.
            startupLogger.LogInformation(
                "DPI scaling: WPF device-independent pixels handle per-monitor scaling natively");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to log DPI diagnostics");
        }

        startupLogger.LogInformation("MySecondBrain started");

        // ================================================================
        // First-Launch Detection
        // ================================================================
        var settingsRepo = _serviceProvider.GetRequiredService<ISettingsRepository>();
        var onboardingCompleted = await settingsRepo.GetAsync("Onboarding_Completed");

        if (onboardingCompleted != "true")
        {
            // First launch or incomplete onboarding — show wizard as the only window
            var wizardWindow = _serviceProvider.GetRequiredService<OnboardingWizardWindow>();
            var wizardVm = (OnboardingWizardViewModel)wizardWindow.DataContext;

            wizardVm.LaunchStudioRequested += () =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        startupLogger.LogInformation("LaunchStudio step 1: closing wizard");
                        wizardWindow.Close();
                        startupLogger.LogInformation("LaunchStudio step 2: resolving MainWindow");
                        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                        startupLogger.LogInformation("LaunchStudio step 3: showing MainWindow");
                        mainWindow.Show();
                        startupLogger.LogInformation("LaunchStudio step 4: wiring tray");
                        AppStartupHelpers.WireTrayService(_serviceProvider, mainWindow, startupLogger);
                        startupLogger.LogInformation("LaunchStudio step 5: starting background services");
                        AppStartupHelpers.StartBackgroundServices(_serviceProvider, startupLogger);
                        startupLogger.LogInformation("LaunchStudio complete — MainWindow shown");
                    }
                    catch (Exception ex)
                    {
                        startupLogger.LogError(ex, "LaunchStudioRequested handler crashed at step X");
                        throw;
                    }
                });
            };

            wizardWindow.Show();
        }
        else
        {
            // Onboarding complete — normal launch
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            AppStartupHelpers.WireTrayService(_serviceProvider, mainWindow, startupLogger);
            AppStartupHelpers.StartBackgroundServices(_serviceProvider, startupLogger);
        }

        // ================================================================
        // Wire Re-run Onboarding from Settings
        // ================================================================
        WeakReferenceMessenger.Default.Register<ReRunOnboardingMessage>(this, (_, _) =>
        {
            // Find the main window if it exists
            var mainWindow = Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow is not null)
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    var wizardWindow = _serviceProvider.GetRequiredService<OnboardingWizardWindow>();
                    var wizardVm = (OnboardingWizardViewModel)wizardWindow.DataContext;

                    wizardVm.LaunchStudioRequested += () =>
                    {
                        Current.Dispatcher.Invoke(() =>
                        {
                            startupLogger.LogInformation("LaunchStudio re-run: closing modal wizard");
                            wizardWindow.Close();
                        });
                    };

                    wizardWindow.Owner = mainWindow;
                    wizardWindow.ShowDialog(); // Modal — blocks until wizard closes

                    // Notify Settings → Providers tab to refresh its API key list,
                    // since the onboarding wizard may have added new keys.
                    startupLogger.LogInformation(
                        "[DIAG] About to send RefreshApiKeysMessage — open windows: {WindowCount}",
                        Current.Windows.Count);
                    WeakReferenceMessenger.Default.Send(new RefreshApiKeysMessage());
                    startupLogger.LogInformation("Re-run onboarding completed — RefreshApiKeysMessage sent");
                });
            }
        });
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Gracefully stop the WebSocket server before disposing DI container.
        // Use static Serilog Log rather than ILogger<T> because the DI container
        // is being torn down and ILogger<T> may be unavailable during teardown.
        try
        {
            var wsServer = _serviceProvider.GetService<ILocalWebSocketServer>();
            if (wsServer is not null)
            {
                // 5-second timeout to prevent indefinite blocking on shutdown
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await wsServer.StopAsync(timeoutCts.Token);
                Log.Information("WebSocket server stopped on shutdown");
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("WebSocket server stop timed out after 5 seconds");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping WebSocket server on shutdown");
        }

        Log.CloseAndFlush();
        (_serviceProvider as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
