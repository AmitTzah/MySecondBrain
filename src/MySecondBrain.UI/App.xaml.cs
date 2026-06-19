using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;
using System.Windows;
// Resolves ambiguity with System.Windows.Forms.Application from UseWindowsForms=true
using Application = System.Windows.Application;
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
using MySecondBrain.UI.Services;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.UI;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;
    private static readonly FontWeightConverter s_fontWeightConverter = new();

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
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Failed to restore theme/font settings, continuing with defaults");
        }

        startupLogger.LogInformation("MySecondBrain started");

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Show the system tray icon and wire basic events
        var trayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
        trayService.Show();
        trayService.OpenStudioRequested += (s, args) =>
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            });
        };
        trayService.ExitRequested += (s, args) => App.Current.Shutdown();

        // Start the embedded Kestrel WebSocket server (non-blocking)
        _ = StartWebSocketServerAsync(startupLogger);
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
