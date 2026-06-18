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

    protected override void OnStartup(StartupEventArgs e)
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

        startupLogger.LogInformation("MySecondBrain started");

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        (_serviceProvider as IDisposable)?.Dispose();
        base.OnExit(e);
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
