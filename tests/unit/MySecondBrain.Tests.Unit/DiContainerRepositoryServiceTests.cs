using Microsoft.Extensions.DependencyInjection;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.UI;

namespace MySecondBrain.Tests.Unit;

public class DiContainerRepositoryServiceTests : IDisposable
{
    private readonly IServiceProvider _provider;

    public DiContainerRepositoryServiceTests()
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        });
    }

    public void Dispose()
    {
        (_provider as IDisposable)?.Dispose();
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
}
