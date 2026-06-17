# Feature Reference: Dependency Injection Container

## Global & Shared Documentation

### Planning Documents (Read-Only Reference)

| Document | Path | Relevance |
|----------|------|-----------|
| Abstractions | [`agent-workspace/project-director/planning/abstractions.md`](../project-director/planning/abstractions.md) | 31 interfaces with full contracts for DI registration |
| Platform Notes §3 | [`agent-workspace/project-director/planning/platform-notes.md`](../project-director/planning/platform-notes.md#3-dependency-injection--microsoftextensionsdependencyinjection) | DI lifetime conventions, sample registration code |
| Architecture | [`agent-workspace/project-director/planning/architecture.md`](../project-director/planning/architecture.md) | Component diagram, dependency direction |
| Tech Stack | [`agent-workspace/project-director/planning/tech-stack.md`](../project-director/planning/tech-stack.md) | NuGet packages and versions |
| Architecture Knowledge | [`agent-workspace/knowledge/architecture.md`](../knowledge/architecture.md) | Solution structure, design patterns, DI section |

### Existing Code (Modified by This Feature)

| File | Current State | Changes Needed |
|------|--------------|----------------|
| [`src/MySecondBrain.UI/App.xaml.cs`](../../src/MySecondBrain.UI/App.xaml.cs) | Registers only `MainWindow` as singleton | Expand `ConfigureServices` with all 70+ registrations |
| [`src/MySecondBrain.Data/AppDbContext.cs`](../../src/MySecondBrain.Data/AppDbContext.cs) | Fallback `OnConfiguring` with `"Data Source=msb.db"` | Update to use `%LOCALAPPDATA%\MySecondBrain\msb.db` |

### DI Lifetime Conventions (from platform-notes.md §3)

| Lifetime | Used For | Rationale |
|----------|----------|-----------|
| **Singleton** | Services, repositories, theme provider, hotkey service, system tray, AppDbContext | Shared state across all windows. One database. One LLM connection pool. |
| **Transient** | ViewModels, clipboard service, audio service | Fresh state per window/tab/chat. No cross-tab state leakage. |
| **Scoped** | Not used | Single-user app with no request/response cycle. |

### NuGet Packages Already Referenced

| Package | Project | Version |
|---------|---------|---------|
| `Microsoft.Extensions.DependencyInjection` | UI, Services | `8.0.*` |
| `Microsoft.Extensions.Hosting` | Services | `8.0.*` |
| `Microsoft.Extensions.Logging` | Services | `8.0.*` |
| `CommunityToolkit.Mvvm` | UI | `8.*` |

### External Documentation

- [Microsoft.Extensions.DependencyInjection Reference](../external-docs/ref-microsoft-extensions-di.md) — Context7-sourced: core registration methods, EF Core DbContext registration, multiple-implementation pattern, host builder pattern

---

## Step-Specific Documentation

### Step 1: Create All Types — Interfaces, DTOs, Entities, and Stub Implementations

#### Interfaces (Core/Interfaces/)

- **Library:** None (pure C# interfaces in `MySecondBrain.Core`)
- **Import:** N/A — these are interface definitions consumed by all other projects
- **Snippet (representative interface):**

```csharp
namespace MySecondBrain.Core.Interfaces;

public interface IChatThreadRepository
{
    Task<ChatThread?> GetByIdAsync(string id);
    Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort);
    Task<IReadOnlyList<ChatThread>> GetTransientInWindowAsync();
    Task<IReadOnlyList<ChatThread>> GetTrashAsync();
    Task<IReadOnlyList<ChatThread>> SearchAsync(string query, int maxResults);
    Task<ChatThread> CreateAsync(ChatThread thread);
    Task UpdateAsync(ChatThread thread);
    Task SoftDeleteAsync(string id);
    Task PermanentDeleteAsync(string id);
    Task<int> CleanupTransientAsync(DateTimeOffset olderThan);
    Task<int> PurgeTrashAsync(DateTimeOffset olderThan);
}
```

**Full interface contracts:** See [`abstractions.md`](../project-director/planning/abstractions.md) §1–§13 for all 31 interfaces.

#### Entity Classes (Data/Entities/) and DTO Records (Core/Models/)

- **Library:** None (pure C# classes/records)
- **Import:** Entity classes reference `System.ComponentModel.DataAnnotations` for `[Key]`, `[MaxLength]` etc.
- **Snippet (representative entity):**

```csharp
namespace MySecondBrain.Data.Entities;

public class ChatThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Title { get; set; }
    public bool IsTransient { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PersonaId { get; set; }
    public string? ModelConfigId { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
```

- **Snippet (representative DTO record):**

```csharp
namespace MySecondBrain.Core.Models;

public record StreamChunk(
    string? ContentDelta,
    IReadOnlyList<ToolCallDelta>? ToolCalls,
    string? ThinkingDelta,
    string? FinishReason,
    UsageInfo? Usage,
    bool IsFinal
);
```

#### Stub Repository Implementations (Data/Repositories/)

- **Library:** `Microsoft.EntityFrameworkCore` (already referenced in Data project)
- **Import:** `using Microsoft.EntityFrameworkCore;`, `using MySecondBrain.Core.Interfaces;`
- **Snippet:**

```csharp
namespace MySecondBrain.Data.Repositories;

public class ChatThreadRepository : IChatThreadRepository
{
    private readonly AppDbContext _db;

    public ChatThreadRepository(AppDbContext db) => _db = db;

    public Task<ChatThread?> GetByIdAsync(string id) =>
        Task.FromResult<ChatThread?>(null);

    public Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort) =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    // ... remaining methods follow same pattern
}
```

#### Stub Service Implementations (Services/)

- **Library:** `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` (already in Services project)
- **Import:** `using Microsoft.Extensions.Logging;`, `using MySecondBrain.Core.Interfaces;`
- **Snippet:**

```csharp
namespace MySecondBrain.Services.Chat;

public class ChatThreadService : IChatThreadService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILLMProviderService _llmService;
    private readonly ILogger<ChatThreadService> _logger;

    public ChatThreadService(
        IChatThreadRepository threadRepo,
        IMessageRepository messageRepo,
        ILLMProviderService llmService,
        ILogger<ChatThreadService> logger)
    {
        _threadRepo = threadRepo;
        _messageRepo = messageRepo;
        _llmService = llmService;
        _logger = logger;
    }

    // Stub methods follow same pattern as repository stubs
}
```

#### Stub ViewModel and Content Block Renderer Classes (UI/)

- **Library:** `CommunityToolkit.Mvvm` (8.*, already referenced in UI project)
- **Import:** `using CommunityToolkit.Mvvm.ComponentModel;`, `using MySecondBrain.Core.Interfaces;`
- **Snippet (ViewModel):**

```csharp
namespace MySecondBrain.UI.ViewModels;

public partial class ChatThreadViewModel : ObservableObject
{
    private readonly IChatThreadService _chatService;
    private readonly ILogger<ChatThreadViewModel> _logger;

    public ChatThreadViewModel(IChatThreadService chatService, ILogger<ChatThreadViewModel> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }
}
```

- **Snippet (Content Block Renderer):**

```csharp
namespace MySecondBrain.UI.Controls;

public class MarkdownTextRenderer : IContentBlockRenderer
{
    public string RendererName => "MarkdownText";
    public int Priority => 100;

    public bool CanRender(MarkdownObject markdownNode) => false;

    public Task RenderAsync(MarkdownObject markdownNode, FlowDocument targetDocument,
        RenderContext context, CancellationToken ct) => Task.CompletedTask;
}
```

---

### Step 2: Wire Full DI Container in App.xaml.cs

- **Library:** `Microsoft.Extensions.DependencyInjection` (already referenced)
- **Import:** `using Microsoft.Extensions.DependencyInjection;`, `using Microsoft.EntityFrameworkCore;`, `using Microsoft.Extensions.Logging;`
- **Snippet — Full ConfigureServices method (must be `public static` for unit test access):**

```csharp
public static void ConfigureServices(IServiceCollection services)
{
    // === Database ===
    var dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain", "msb.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

    services.AddSingleton(sp =>
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

    // === Provider Implementations (Singleton) ===
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
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.AddDebug();
        builder.SetMinimumLevel(LogLevel.Information);
    });
}
```

**Key requirement:** `ConfigureServices` must be `public static` (not `private static`) so that unit tests in Step 3 can invoke it via `App.ConfigureServices(services)`.

---

### Step 3: Unit Tests for DI Container Resolution + Final Verification

#### Unit Tests (DiContainerTests.cs)

- **Library:** `xunit` (2.*), `Moq` (4.*), `Microsoft.Extensions.DependencyInjection`
- **Import:** `using Xunit;`, `using Moq;`, `using Microsoft.Extensions.DependencyInjection;`
- **Snippet:**

```csharp
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
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Fact]
    public void CanResolve_AllSingletonServices()
    {
        Assert.NotNull(_provider.GetRequiredService<IChatThreadService>());
        Assert.NotNull(_provider.GetRequiredService<ILLMProviderService>());
        Assert.NotNull(_provider.GetRequiredService<IWikiService>());
        // ... one assertion per service
    }

    [Fact]
    public void CanResolve_AllViewModels()
    {
        Assert.NotNull(_provider.GetRequiredService<MainWindowViewModel>());
        Assert.NotNull(_provider.GetRequiredService<ChatThreadViewModel>());
        // ... one assertion per ViewModel
    }

    [Fact]
    public void CanResolve_ContentRendererRegistry_HasSevenRenderers()
    {
        var registry = _provider.GetRequiredService<IContentRendererRegistry>();
        var renderers = registry.GetRenderers();
        Assert.Equal(7, renderers.Count);
    }
}
```

**Required tests (8 total):**
- `CanResolve_AllSingletonServices` — `GetRequiredService<T>()` for every singleton service
- `CanResolve_AllRepositories` — all 8 repositories
- `CanResolve_AllViewModels` — all 11 ViewModels
- `CanResolve_AllProviders` — all multi-implementation providers
- `ContentRendererRegistry_HasSevenRenderers` — resolves `IContentRendererRegistry`, asserts `GetRenderers().Count == 7`
- `CanResolve_MainWindow` — resolves `MainWindow`
- `CanResolve_AppDbContext` — resolves `AppDbContext`
- `CanResolve_Logger` — resolves `ILogger<DiContainerTests>`

#### Final Build & Test Verification

- **Library:** None — CLI commands only
- **Import:** N/A
- **Commands:**

```bash
dotnet restore MySecondBrain.sln
dotnet build MySecondBrain.sln --configuration Release
dotnet test MySecondBrain.sln --configuration Release --no-build --verbosity normal
```

**Expected output:** `Build succeeded. 0 Warning(s) 0 Error(s)`. Tests: `8 passed, 0 failed, 0 skipped`.
