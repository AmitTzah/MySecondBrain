using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Data;
using MySecondBrain.UI;
using MySecondBrain.UI.Services;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class DiContainerTests
{
    private readonly IServiceProvider _provider;

    public DiContainerTests()
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void CanResolve_AllRepositories()
    {
        Assert.NotNull(_provider.GetRequiredService<IChatThreadRepository>());
        Assert.NotNull(_provider.GetRequiredService<IMessageRepository>());
        Assert.NotNull(_provider.GetRequiredService<IPersonaRepository>());
        Assert.NotNull(_provider.GetRequiredService<IModelConfigurationRepository>());
        Assert.NotNull(_provider.GetRequiredService<IApiKeyRepository>());
        Assert.NotNull(_provider.GetRequiredService<IWikiIndexRepository>());
        Assert.NotNull(_provider.GetRequiredService<IUsageRepository>());
        Assert.NotNull(_provider.GetRequiredService<ISettingsRepository>());

        // Verify singleton lifetime: same instance on repeated resolution
        var first = _provider.GetRequiredService<IChatThreadRepository>();
        var second = _provider.GetRequiredService<IChatThreadRepository>();
        Assert.Same(first, second);
    }

    [Fact]
    public void CanResolve_AllServices()
    {
        // Singleton services
        Assert.NotNull(_provider.GetRequiredService<ILLMProviderService>());
        Assert.NotNull(_provider.GetRequiredService<IChatThreadService>());
        Assert.NotNull(_provider.GetRequiredService<IWikiService>());
        Assert.NotNull(_provider.GetRequiredService<ILLMProviderFactory>());
        Assert.NotNull(_provider.GetRequiredService<ITokenizerFactory>());
        Assert.NotNull(_provider.GetRequiredService<IToolOrchestrator>());
        Assert.NotNull(_provider.GetRequiredService<IChatSearchService>());
        Assert.NotNull(_provider.GetRequiredService<IAutoCleanupService>());
        Assert.NotNull(_provider.GetRequiredService<IEncryptionService>());
        Assert.NotNull(_provider.GetRequiredService<IChatEncryptionService>());
        Assert.NotNull(_provider.GetRequiredService<IWikiFileWatcher>());
        Assert.NotNull(_provider.GetRequiredService<ILocalWebSocketServer>());
        Assert.NotNull(_provider.GetRequiredService<ISystemTrayService>());
        Assert.NotNull(_provider.GetRequiredService<IGlobalHotkeyService>());
        Assert.NotNull(_provider.GetRequiredService<IHwndCaptureService>());
        Assert.NotNull(_provider.GetRequiredService<ITextInjectionService>());
        Assert.NotNull(_provider.GetRequiredService<ISpellCheckService>());
        Assert.NotNull(_provider.GetRequiredService<IWikiGitService>());
        Assert.NotNull(_provider.GetRequiredService<IThemeProvider>());
        // Transient services
        Assert.NotNull(_provider.GetRequiredService<IClipboardService>());
        Assert.NotNull(_provider.GetRequiredService<IAudioService>());
        Assert.NotNull(_provider.GetRequiredService<ICameraService>());
        Assert.NotNull(_provider.GetRequiredService<IVideoPlayerService>());
        // Content renderer registry
        Assert.NotNull(_provider.GetRequiredService<IContentRendererRegistry>());

        // Verify singleton lifetime: IChatThreadService resolves to same instance
        var first = _provider.GetRequiredService<IChatThreadService>();
        var second = _provider.GetRequiredService<IChatThreadService>();
        Assert.Same(first, second);
    }

    [Fact]
    public void CanResolve_AllViewModels()
    {
        Assert.NotNull(_provider.GetRequiredService<MainWindowViewModel>());
        Assert.NotNull(_provider.GetRequiredService<ChatThreadViewModel>());
        Assert.NotNull(_provider.GetRequiredService<SettingsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<WikiBrowserViewModel>());
        Assert.NotNull(_provider.GetRequiredService<UsageDashboardViewModel>());
        Assert.NotNull(_provider.GetRequiredService<MediaLibraryViewModel>());
        Assert.NotNull(_provider.GetRequiredService<GlobalArtifactsBrowserViewModel>());
        Assert.NotNull(_provider.GetRequiredService<Tier1OverlayViewModel>());
        Assert.NotNull(_provider.GetRequiredService<Tier2CommandBarViewModel>());
        Assert.NotNull(_provider.GetRequiredService<ModelComparisonViewModel>());
        Assert.NotNull(_provider.GetRequiredService<OnboardingWizardViewModel>());
    }

    [Fact]
    public void CanResolve_AllProviders()
    {
        // ILLMProvider: 4 implementations
        var llmProviders = _provider.GetServices<ILLMProvider>().ToList();
        Assert.Equal(4, llmProviders.Count);

        // ISTTProvider: 3 implementations
        var sttProviders = _provider.GetServices<ISTTProvider>().ToList();
        Assert.Equal(3, sttProviders.Count);

        // IBackupProvider: 2 implementations
        var backupProviders = _provider.GetServices<IBackupProvider>().ToList();
        Assert.Equal(2, backupProviders.Count);

        // ISearchProvider: 2 implementations
        var searchProviders = _provider.GetServices<ISearchProvider>().ToList();
        Assert.Equal(2, searchProviders.Count);

        // ITokenizer: 3 implementations
        var tokenizers = _provider.GetServices<ITokenizer>().ToList();
        Assert.Equal(3, tokenizers.Count);

        // IChatImporter: 2 implementations
        var importers = _provider.GetServices<IChatImporter>().ToList();
        Assert.Equal(2, importers.Count);

        // IToolExecutor: 5 implementations
        var toolExecutors = _provider.GetServices<IToolExecutor>().ToList();
        Assert.Equal(5, toolExecutors.Count);

        // IUpdateChecker: 2 implementations
        var updateCheckers = _provider.GetServices<IUpdateChecker>().ToList();
        Assert.Equal(2, updateCheckers.Count);

        // IContentBlockRenderer: 7 implementations
        var renderers = _provider.GetServices<IContentBlockRenderer>().ToList();
        Assert.Equal(7, renderers.Count);
    }

    [Fact]
    public void ContentRendererRegistry_ResolvesAllSevenRenderersInCorrectPriorityOrder()
    {
        // Arrange
        var registry = _provider.GetRequiredService<IContentRendererRegistry>();

        // Act
        var renderers = registry.GetRenderers();

        // Assert
        Assert.Equal(7, renderers.Count);
        Assert.Equal("MarkdownText", renderers[0].RendererName);
        Assert.Equal(100, renderers[0].Priority);
        Assert.Equal("CodeBlock", renderers[1].RendererName);
        Assert.Equal(200, renderers[1].Priority);
        Assert.Equal("ArtifactReference", renderers[2].RendererName);
        Assert.Equal(300, renderers[2].Priority);
        Assert.Equal("Image", renderers[3].RendererName);
        Assert.Equal(400, renderers[3].Priority);
        Assert.Equal("Media", renderers[4].RendererName);
        Assert.Equal(500, renderers[4].Priority);
        Assert.Equal("Thinking", renderers[5].RendererName);
        Assert.Equal(600, renderers[5].Priority);
        Assert.Equal("ToolCall", renderers[6].RendererName);
        Assert.Equal(700, renderers[6].Priority);
    }

    [StaFact]
    public void CanResolve_MainWindow()
    {
        var mainWindow = _provider.GetRequiredService<MainWindow>();
        Assert.NotNull(mainWindow);
    }

    [Fact]
    public void CanResolve_AppDbContext()
    {
        var dbContext = _provider.GetRequiredService<AppDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void CanResolve_Logger()
    {
        var logger = _provider.GetRequiredService<ILogger<DiContainerTests>>();
        Assert.NotNull(logger);
    }

    [Fact]
    public void CanResolve_KestrelWebSocketServer()
    {
        var server = _provider.GetRequiredService<ILocalWebSocketServer>();
        Assert.NotNull(server);
        Assert.IsType<KestrelWebSocketServer>(server);

        // Verify singleton lifetime
        var first = _provider.GetRequiredService<ILocalWebSocketServer>();
        var second = _provider.GetRequiredService<ILocalWebSocketServer>();
        Assert.Same(first, second);
    }
}
