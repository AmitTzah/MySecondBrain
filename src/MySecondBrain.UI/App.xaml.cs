using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Data.Repositories;
using MySecondBrain.Services.Audio;
using MySecondBrain.Services.Backup;
using MySecondBrain.Services.Chat;
using MySecondBrain.Services.Chat.Import;
using MySecondBrain.Services.Encryption;
using MySecondBrain.Services.LLM;
using MySecondBrain.Services.Search;
using MySecondBrain.Services.Tools;
using MySecondBrain.Services.Update;
using MySecondBrain.Services.Wiki;
using MySecondBrain.UI.Controls;
using MySecondBrain.Services.Logging;
using MySecondBrain.UI.Services;
using MySecondBrain.UI.ViewModels;
using MySecondBrain.UI.Views;

using Serilog;
using Serilog.Formatting.Json;
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
        ConfigureServices(services);
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
                    startupLogger.LogInformation("LaunchStudioRequested: closing wizard and showing MainWindow");
                    wizardWindow.Close();
                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                    WireTrayService(mainWindow, startupLogger);
                    StartBackgroundServices(startupLogger);
                }
                catch (Exception ex)
                {
                    startupLogger.LogError(ex, "LaunchStudioRequested handler crashed");
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
        WireTrayService(mainWindow, startupLogger);
        StartBackgroundServices(startupLogger);
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
                wizardWindow.Owner = mainWindow;
                wizardWindow.ShowDialog(); // Modal — blocks until wizard closes
            });
        }
    });
}

/// <summary>
/// Wires the system tray service events after a main window is available.
/// </summary>
private void WireTrayService(Window mainWindow, ILogger<App> startupLogger)
{
    var trayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
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
private void StartBackgroundServices(ILogger<App> startupLogger)
{
    // Start the embedded Kestrel WebSocket server (non-blocking)
    _ = StartWebSocketServerAsync(startupLogger);

    // Start global hotkey service
    var hotkeyService = _serviceProvider.GetRequiredService<IGlobalHotkeyService>();
    var hotkeyCount = hotkeyService.GetRegisteredHotkeys().Count;
    startupLogger.LogInformation("Global hotkey service started with {Count} default hotkeys", hotkeyCount);
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

    private async Task StartWebSocketServerAsync(ILogger<App>? startupLogger)
    {
        try
        {
            var wsServer = _serviceProvider.GetRequiredService<ILocalWebSocketServer>();
            await wsServer.StartAsync();
            startupLogger?.LogInformation("WebSocket server lifecycle started");
        }
        catch (Exception ex)
        {
            startupLogger?.LogError(ex, "Failed to start WebSocket server");
        }
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        // === Database ===
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "msb.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddSingleton(_ =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            return new AppDbContext(options);
        });

        // === Repositories (Singleton) ===
        services.AddSingleton<IChatThreadRepository, ChatThreadRepository>();
        services.AddSingleton<IMessageRepository, MessageRepository>();
        services.AddSingleton<IPersonaRepository, PersonaRepository>();
        services.AddSingleton<IModelConfigurationRepository, ModelConfigurationRepository>();
        services.AddSingleton<IApiKeyRepository, ApiKeyRepository>();
        services.AddSingleton<IWikiIndexRepository, WikiIndexRepository>();
        services.AddSingleton<IUsageRepository, UsageRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ITextActionRepository, TextActionRepository>();

        // === Application Services (Singleton) ===
        services.AddSingleton<ILLMProviderService, LLMProviderService>();
        services.AddSingleton<IChatThreadService, ChatThreadService>();
        services.AddSingleton<IWikiService, WikiService>();
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
        services.AddSingleton<ITokenizerFactory, TokenizerFactory>();
        services.AddSingleton<IToolOrchestrator, ToolOrchestrator>();
        services.AddSingleton<IChatSearchService, Fts5ChatSearchService>();
        services.AddSingleton<IAutoCleanupService, PeriodicAutoCleanupService>();
        services.AddSingleton<IEncryptionService, DpapiEncryptionService>();
        services.AddSingleton<IChatEncryptionService, AesGcmChatEncryptionService>();
        services.AddSingleton<IWikiFileWatcher, FileSystemWatcherAdapter>();
        services.AddSingleton<ILocalWebSocketServer, KestrelWebSocketServer>();
        services.AddSingleton<ISystemTrayService, WinFormsSystemTrayService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IHwndCaptureService, Win32HwndCaptureService>();
        services.AddSingleton<ITextInjectionService, UiaTextInjectionService>();
        services.AddSingleton<ISpellCheckService, HunspellSpellCheckService>();
        services.AddSingleton<IWikiGitService, LibGit2SharpGitService>();
        services.AddSingleton<IThemeProvider, WpfThemeProvider>();

        // === Transient Services ===
        services.AddTransient<IClipboardService, WpfClipboardService>();
        services.AddTransient<IConfirmationService, WpfConfirmationService>();
        services.AddTransient<IAudioService, NaudioAudioService>();
        services.AddTransient<ICameraService, AForgeCameraService>();
        services.AddTransient<IVideoPlayerService, WpfVideoPlayerService>();

        // === Multi-Implementation Providers (Singleton) ===
        services.AddSingleton<ILLMProvider, OpenAIProvider>();
        services.AddSingleton<ILLMProvider, AnthropicProvider>();
        services.AddSingleton<ILLMProvider, GoogleProvider>();
        services.AddSingleton<ILLMProvider, OpenAICompatibleProvider>();

        services.AddSingleton<ISTTProvider, OpenAIWhisperProvider>();
        services.AddSingleton<ISTTProvider, LocalWhisperProvider>();
        services.AddSingleton<ISTTProvider, WindowsSpeechProvider>();

        services.AddSingleton<IBackupProvider, GcsBackupProvider>();
        services.AddSingleton<IBackupProvider, LocalFolderBackupProvider>();

        services.AddSingleton<ISearchProvider, GoogleCustomSearchProvider>();
        services.AddSingleton<ISearchProvider, BingSearchProvider>();

        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddSingleton<ITokenizer, AnthropicTokenizer>();
        services.AddSingleton<ITokenizer, FallbackTokenizer>();

        services.AddSingleton<IChatImporter, ChatGPTImporter>();
        services.AddSingleton<IChatImporter, ClaudeImporter>();

        services.AddSingleton<IToolExecutor, WebSearchToolExecutor>();
        services.AddSingleton<IToolExecutor, TerminalToolExecutor>();
        services.AddSingleton<IToolExecutor, FileGenerateToolExecutor>();
        services.AddSingleton<IToolExecutor, FileEditToolExecutor>();
        services.AddSingleton<IToolExecutor, WikiSearchToolExecutor>();

        services.AddSingleton<IUpdateChecker, AutoUpdaterDotNet>();
        services.AddSingleton<IUpdateChecker, MsixAppInstallerUpdater>();

        // === Content Block Renderers (Singleton) ===
        services.AddSingleton<IContentRendererRegistry, ContentRendererRegistry>();
        services.AddSingleton<IContentBlockRenderer, MarkdownTextRenderer>();
        services.AddSingleton<IContentBlockRenderer, CodeBlockRenderer>();
        services.AddSingleton<IContentBlockRenderer, ArtifactReferenceRenderer>();
        services.AddSingleton<IContentBlockRenderer, ImageRenderer>();
        services.AddSingleton<IContentBlockRenderer, MediaRenderer>();
        services.AddSingleton<IContentBlockRenderer, ThinkingRenderer>();
        services.AddSingleton<IContentBlockRenderer, ToolCallRenderer>();

        // === ViewModels (Transient) ===
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ChatThreadViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<WikiBrowserViewModel>();
        services.AddTransient<UsageDashboardViewModel>();
        services.AddTransient<MediaLibraryViewModel>();
        services.AddTransient<GlobalArtifactsBrowserViewModel>();
        services.AddTransient<Tier1OverlayViewModel>();
        services.AddTransient<Tier2CommandBarViewModel>();
        services.AddTransient<ModelComparisonViewModel>();
        services.AddTransient<OnboardingWizardViewModel>();

        // === MainWindow (Singleton — one main window) ===
        services.AddSingleton<MainWindow>();

        // === Onboarding Wizard Window (Transient — new window each time) ===
        services.AddTransient<OnboardingWizardWindow>();

        // === Logging ===
        var appVersion = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0";
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs", "msb-.log");

        var loggerConfig = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .Destructure.With<ApiKeyDestructuringPolicy>()
            .Enrich.With<ApiKeyRedactionEnricher>()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("AppVersion", appVersion)
            .WriteTo.File(
                formatter: new JsonFormatter(),
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30);

#if DEBUG
        loggerConfig = loggerConfig.WriteTo.Console();
#endif

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });
    }
}
