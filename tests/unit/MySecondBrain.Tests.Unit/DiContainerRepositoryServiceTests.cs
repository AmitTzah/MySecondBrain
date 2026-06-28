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
    public void DiContainer_ShouldResolveAllToolExecutors()
    {
        var executors = _provider.GetRequiredService<IEnumerable<IToolExecutor>>().ToList();

        // Verify count: was 10 (9 existing + text_editor), now 14 (9 existing + 5 new file operation executors, text_editor removed)
        Assert.Equal(14, executors.Count);

        // Verify all expected tool names are present
        var toolNames = executors.Select(e => e.ToolName).OrderBy(n => n).ToList();
        var expectedNames = new[]
        {
            "apply_diff",
            "ask_user_input",
            "bash",
            "image_search",
            "list_files",
            "memory",
            "present_files",
            "read_file",
            "search_files",
            "skill_load",
            "web_fetch",
            "web_search",
            "wiki_search",
            "write_to_file"
        };

        Assert.Equal(expectedNames.Length, toolNames.Count);
        for (int i = 0; i < expectedNames.Length; i++)
        {
            Assert.Equal(expectedNames[i], toolNames[i]);
        }

        // Verify TextEditorToolExecutor is absent
        Assert.DoesNotContain(executors, e => e.ToolName == "text_editor");
    }

    [Fact]
    public void DiContainer_ToolOrchestrator_GetAvailableToolDefinitions_Returns14Definitions()
    {
        var orchestrator = _provider.GetRequiredService<IToolOrchestrator>();
        var definitions = orchestrator.GetAvailableToolDefinitions();

        Assert.Equal(14, definitions.Count);

        var definitionNames = definitions.Select(d => d.Name).OrderBy(n => n).ToList();
        var expectedNames = new[]
        {
            "apply_diff",
            "ask_user_input",
            "bash",
            "image_search",
            "list_files",
            "memory",
            "present_files",
            "read_file",
            "search_files",
            "skill_load",
            "web_fetch",
            "web_search",
            "wiki_search",
            "write_to_file"
        };

        for (int i = 0; i < expectedNames.Length; i++)
        {
            Assert.Equal(expectedNames[i], definitionNames[i]);
        }
    }
}
