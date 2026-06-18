# Feature Implementation Plan: Dependency Injection Container

## 1. Overall Project Context

MySecondBrain is a local-first Windows 10/11 desktop application built on .NET 8.0 WPF that serves as a universal AI chat client and personal knowledge management system. It uses a 7-project layered architecture (Core → Data → Services → UI, plus 2 test projects and MSIX packaging), provider-agnostic LLM abstraction via the Provider/Adapter pattern, Entity Framework Core + SQLite for local storage, and the MVVM pattern via CommunityToolkit.Mvvm for all UI. The application is single-user, BYO API keys (encrypted via DPAPI), and stores wiki content as plain `.md` files with a SQLite index.

All 7 projects were scaffolded in Feature 1. The `App.xaml.cs` contains a minimal DI bootstrap (registers only `MainWindow`). `AppDbContext` exists with a fallback `OnConfiguring` that points to `"Data Source=msb.db"` (needs updating for `%LOCALAPPDATA%`). All 15+ OSS NuGet packages are resolved, including `Microsoft.Extensions.DependencyInjection` in the UI and Services projects, `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging` in the Services project, and `CommunityToolkit.Mvvm` in the UI project.

Full architecture: [`agent-workspace/project-director/planning/architecture.md`](../project-director/planning/architecture.md)

## 2. Feature-Specific Context

**Feature 2 of 245 — Wave 1: Foundation.** This feature wires the `Microsoft.Extensions.DependencyInjection` container with all service, repository, and ViewModel registrations per the lifetimes defined in [`platform-notes.md` §3](../project-director/planning/platform-notes.md#3-dependency-injection--microsoftextensionsdependencyinjection). It depends on Feature 1 (solution scaffold).

The DI container is the backbone of the entire application. Every subsequent feature (LLM providers, chat, wiki, tools, UI) consumes services and repositories through constructor injection from this container. Getting the registrations right now ensures all future features "just work" when they pull `IChatThreadService` or `ILLMProviderFactory` from their constructors.

Since the implementation classes don't exist yet, they are created as **stubs** — classes that implement the interface contract but return `null`, empty collections, or `Task.CompletedTask`. Stubs are placed in the correct namespace/directory per the architecture conventions so future features simply need to fill in the method bodies without moving files.

**What must be created:**
- **31 interfaces** in `Core/Interfaces/` with full method signatures from [`abstractions.md`](../project-director/planning/abstractions.md)
- **~10 DTO records** in `Core/Models/` (StreamChunk, ChatRequest, ChatResponse, etc.)
- **12 entity classes** in `Data/Entities/` (ChatThread, Message, Persona, etc.)
- **8 repository stubs** in `Data/Repositories/`
- **23 service/provider stubs** in `Services/` subdirectories
- **11 ViewModel stubs** in `UI/ViewModels/`
- **8 content block renderer stubs** (7 renderers + 1 registry) in `UI/Controls/`
- **15 UI-specific service stubs** in `UI/Services/`
- **Modified:** `App.xaml.cs` — expand `ConfigureServices` with 70+ registrations
- **Modified:** `AppDbContext.cs` — update connection string to `%LOCALAPPDATA%\MySecondBrain\msb.db`
- **New:** 8 unit tests in `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`

**DI Lifetime Rules (from platform-notes.md §3):**

| Lifetime | Used For | Rationale |
|----------|----------|-----------|
| **Singleton** | Services, repositories, theme provider, hotkey service, system tray, AppDbContext | Shared state across all windows. One database. One LLM connection pool. |
| **Transient** | ViewModels, clipboard service, audio service, camera service, video player service | Fresh state per window/tab/chat. No cross-tab state leakage. |
| **Scoped** | Not used | Single-user app with no request/response cycle. |

## 3. Architecture and Extensibility

### Design Patterns Applied

| Pattern | How It's Applied | Extensibility Benefit |
|---------|-----------------|----------------------|
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection` at center. All types resolved from container. | Adding a new service: (a) create interface in Core, (b) create implementation, (c) one `AddSingleton` line in `ConfigureServices`. |
| **Interface/Implementation Separation** | All 31 interfaces in `Core/Interfaces/`. All stubs in `Services/` or `Data/` subdirectories. | Any implementation can be swapped by changing the DI registration line — zero code changes to consumers. |
| **Provider/Adapter Pattern — Multi-Registration** | Multiple implementations of the same interface registered via repeated `AddSingleton<IX, ConcreteX>()` calls. `IEnumerable<IX>` constructor injection auto-resolves all. | Adding a new LLM provider (e.g., `GroqProvider`): (a) create adapter class, (b) `services.AddSingleton<ILLMProvider, GroqProvider>()`. Factory receives all via `IEnumerable<ILLMProvider>`. |
| **Plugin/Registry Pattern** | `ContentRendererRegistry` takes `IEnumerable<IContentBlockRenderer>` — DI auto-injects all 7 renderers. Registry sorts by priority and resolves by `CanRender()`. | Adding a new content block type (e.g., Mermaid): (a) implement `IContentBlockRenderer`, (b) `services.AddSingleton<IContentBlockRenderer, MermaidRenderer>()`. Registry auto-discovers it. |
| **Repository Pattern** | Services depend on `I*Repository` interfaces. `AppDbContext` is singleton injected into repository constructors. | Repositories can be mocked in unit tests. Switch ORM requires changing only the Data project. |

### Dependency Direction

```mermaid
graph TD
    Core[MySecondBrain.Core\n31 Interfaces, DTOs]
    Data[MySecondBrain.Data\nEntities, Repository Stubs, AppDbContext]
    Services[MySecondBrain.Services\n23 Service/Provider Stubs]
    UI[MySecondBrain.UI\nApp.xaml.cs DI Bootstrap, ViewModels, Renderers]
    UnitTests[MySecondBrain.Tests.Unit\nDI Resolution Tests]

    Core --> Data
    Core --> Services
    Core --> UnitTests
    Data --> Services
    Services --> UI
    Data --> UnitTests
    Services --> UnitTests
    UI --> UnitTests
```

### Why Stubs

Creating stubs is correct because:
1. **Parallelizable:** Stubs allow Feature 2 (DI) to complete independently. Feature 3 fills in `ChatThreadService`, Feature 4 fills in `LLMProviderService`, etc.
2. **Compile-time safety:** Full interface contracts with proper method signatures mean the compiler catches signature mismatches now.
3. **Testable:** DI resolution tests prove all registrations are correct without needing real implementations.
4. **Git-trackable:** Each feature's "fill in the stub" work is a clean diff showing actual business logic being added.

## 4. Final Expected Project Structure

```
MySecondBrain/
├── src/
│   ├── MySecondBrain.Core/
│   │   ├── Interfaces/
│   │   │   ├── ILLMProvider.cs                      [NEW]
│   │   │   ├── ILLMProviderFactory.cs               [NEW]
│   │   │   ├── ISTTProvider.cs                      [NEW]
│   │   │   ├── IBackupProvider.cs                   [NEW]
│   │   │   ├── ISearchProvider.cs                   [NEW]
│   │   │   ├── ITokenizer.cs                        [NEW]
│   │   │   ├── ITokenizerFactory.cs                 [NEW]
│   │   │   ├── IChatImporter.cs                     [NEW]
│   │   │   ├── IToolExecutor.cs                     [NEW]
│   │   │   ├── IToolOrchestrator.cs                 [NEW]
│   │   │   ├── IContentBlockRenderer.cs             [NEW]
│   │   │   ├── IContentRendererRegistry.cs          [NEW]
│   │   │   ├── IThemeProvider.cs                    [NEW]
│   │   │   ├── IUpdateChecker.cs                    [NEW]
│   │   │   ├── IChatThreadRepository.cs             [NEW]
│   │   │   ├── IMessageRepository.cs                [NEW]
│   │   │   ├── IPersonaRepository.cs                [NEW]
│   │   │   ├── IModelConfigurationRepository.cs     [NEW]
│   │   │   ├── IApiKeyRepository.cs                 [NEW]
│   │   │   ├── IWikiIndexRepository.cs              [NEW]
│   │   │   ├── IUsageRepository.cs                  [NEW]
│   │   │   ├── ISettingsRepository.cs               [NEW]
│   │   │   ├── ILLMProviderService.cs               [NEW]
│   │   │   ├── IChatThreadService.cs                [NEW]
│   │   │   ├── IWikiService.cs                      [NEW]
│   │   │   ├── IEncryptionService.cs                [NEW]
│   │   │   ├── IChatEncryptionService.cs            [NEW]
│   │   │   ├── IClipboardService.cs                 [NEW]
│   │   │   ├── IWikiFileWatcher.cs                  [NEW]
│   │   │   ├── ILocalWebSocketServer.cs             [NEW]
│   │   │   ├── ISystemTrayService.cs                [NEW]
│   │   │   ├── IGlobalHotkeyService.cs              [NEW]
│   │   │   ├── IHwndCaptureService.cs               [NEW]
│   │   │   ├── ITextInjectionService.cs             [NEW]
│   │   │   ├── IAudioService.cs                     [NEW]
│   │   │   ├── ICameraService.cs                    [NEW]
│   │   │   ├── IVideoPlayerService.cs               [NEW]
│   │   │   ├── ISpellCheckService.cs                [NEW]
│   │   │   ├── IWikiGitService.cs                   [NEW]
│   │   │   ├── IChatSearchService.cs                [NEW]
│   │   │   └── IAutoCleanupService.cs               [NEW]
│   │   └── Models/
│   │       ├── StreamChunk.cs                       [NEW]
│   │       ├── ChatRequest.cs                       [NEW]
│   │       ├── ChatResponse.cs                      [NEW]
│   │       ├── ChatMessage.cs                       [NEW]
│   │       ├── ToolDefinition.cs                    [NEW]
│   │       ├── ToolCallDelta.cs                     [NEW]
│   │       ├── ToolCall.cs                          [NEW]
│   │       ├── UsageInfo.cs                         [NEW]
│   │       ├── ModelInfo.cs                         [NEW]
│   │       └── Enums.cs                             [NEW]
│   │
│   ├── MySecondBrain.Data/
│   │   ├── AppDbContext.cs                          [MODIFIED]
│   │   ├── Entities/
│   │   │   ├── ChatThread.cs / Message.cs / Persona.cs / ModelConfiguration.cs
│   │   │   ├── ApiKey.cs / Artifact.cs / MediaItem.cs / PromptTemplate.cs
│   │   │   ├── TextAction.cs / UsageRecord.cs / WikiFile.cs / WikiVersionSnapshot.cs
│   │   │   └── [all NEW]
│   │   └── Repositories/
│   │       ├── ChatThreadRepository.cs / MessageRepository.cs / PersonaRepository.cs
│   │       ├── ModelConfigurationRepository.cs / ApiKeyRepository.cs
│   │       ├── WikiIndexRepository.cs / UsageRepository.cs / SettingsRepository.cs
│   │       └── [all NEW — stubs]
│   │
│   ├── MySecondBrain.Services/
│   │   ├── Chat/   → ChatThreadService.cs, Fts5ChatSearchService.cs [NEW]
│   │   ├── LLM/    → 13 provider/tokenizer stubs [NEW]
│   │   ├── Wiki/   → WikiService.cs, FileSystemWatcherAdapter.cs [NEW]
│   │   ├── Tools/  → ToolOrchestrator + 5 executors [NEW]
│   │   ├── Backup/ → 2 backup providers [NEW]
│   │   ├── Audio/  → NaudioAudioService.cs [NEW]
│   │   ├── Encryption/ → 2 encryption services [NEW]
│   │   └── Update/ → 2 update checkers [NEW]
│   │
│   ├── MySecondBrain.UI/
│   │   ├── App.xaml.cs                              [MODIFIED — full DI]
│   │   ├── ViewModels/                              → 11 ViewModel stubs [NEW]
│   │   ├── Controls/                                → 8 content renderer stubs [NEW]
│   │   └── Services/                                → 15 UI service stubs [NEW]
│   │
│   └── MySecondBrain.Package/                       [UNCHANGED]
│
└── tests/
    └── unit/
        └── MySecondBrain.Tests.Unit/
            └── DiContainerTests.cs                  [NEW]
```

---

## 5. Execution Steps

### [x] Step 1a: Create All Interfaces + DTO Records + Enums in Core Project

- **Goal:** Create every new C# file in `MySecondBrain.Core` — all interface contracts and DTO records — so the Core project compiles with zero errors. This is the foundation that Data, Services, and UI all depend on.

- **Actions:**
  - Create ~41 interface files in `src/MySecondBrain.Core/Interfaces/` with full method signatures from [`abstractions.md`](../project-director/planning/abstractions.md) §1–§13:
    - **Provider:** `ILLMProvider`, `ILLMProviderFactory`, `ISTTProvider`, `IBackupProvider`, `ISearchProvider`, `ITokenizer`, `ITokenizerFactory`, `IChatImporter`, `IToolExecutor`, `IToolOrchestrator`, `IContentBlockRenderer`, `IContentRendererRegistry`, `IThemeProvider`, `IUpdateChecker`
    - **Repository:** `IChatThreadRepository`, `IMessageRepository`, `IPersonaRepository`, `IModelConfigurationRepository`, `IApiKeyRepository`, `IWikiIndexRepository`, `IUsageRepository`, `ISettingsRepository`
    - **Service:** `ILLMProviderService`, `IChatThreadService`, `IWikiService`, `IEncryptionService`, `IChatEncryptionService`, `IClipboardService`, `IWikiFileWatcher`, `ILocalWebSocketServer`, `ISystemTrayService`, `IGlobalHotkeyService`, `IHwndCaptureService`, `ITextInjectionService`, `IAudioService`, `ICameraService`, `IVideoPlayerService`, `ISpellCheckService`, `IWikiGitService`, `IChatSearchService`, `IAutoCleanupService`
  - Create DTO records and enums in `src/MySecondBrain.Core/Models/`:
    - **LLM streaming DTOs:** `StreamChunk`, `ChatRequest`, `ChatResponse`, `ChatMessage`, `ToolDefinition`, `ToolCallDelta`, `ToolCall`, `UsageInfo`, `ModelInfo`
    - **Provider DTOs:** `STTResult`, `BackupResult`, `BackupInfo`, `SearchResults`, `SearchResultItem`, `ToolValidationResult`, `ToolResult`, `RenderContext`, `ImportResult`, `ImportedChatThread`, `ImportedMessage`, `ImportWarning`, `ImportValidationResult`, `UpdateCheckResult`, `UpdateInfo`, `HwndCaptureResult`, `TextInjectionResult`, `PlaybackPositionEventArgs`, `VideoErrorEventArgs`, `WikiFileChangedEventArgs`, `HotkeyAssignment`, `HotkeyTriggeredEventArgs`, `ChatSearchResult`, `CleanupCompletedEventArgs`, `GitLogEntry`, `GitCommitEventArgs`
    - **Enums:** `ProviderType`, `STTProviderType`, `BackupProviderType`, `SearchProviderType`, `ChatSortOrder`, `ToolRiskLevel`, `AppTheme`, `ChatTheme`, `WikiFileChangeType`, `ContextOverflowStrategy`, plus any enums referenced by interfaces (place in `Enums.cs` or co-located with related interface)
  - Add any required NuGet package references to Core.csproj (e.g., `Markdig` for `MarkdownObject` if used in DTOs/renderer interfaces)
  - Remove `.gitkeep` files from `src/MySecondBrain.Core/Interfaces/` and `src/MySecondBrain.Core/Models/`

- **Automated Testing:** The Core project has no project dependencies — it must compile standalone.

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build src/MySecondBrain.Core/MySecondBrain.Core.csproj
  ```
  Verify: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- **Suggested Commit Message:** `feat(core): create all interfaces and DTO records for DI container`

---

### [x] Step 1b: Create Entity Classes + Update AppDbContext in Data Project

- **Goal:** Create 12 EF Core entity classes in `Data/Entities/` and update `AppDbContext.cs` with `DbSet<T>` properties and `%LOCALAPPDATA%` connection string. The Data project must compile after this step.

- **Actions:**
  - Create 12 entity classes in `src/MySecondBrain.Data/Entities/` with `[Key]` attributes, navigation properties, and fields per [`data-model.md`](../project-director/planning/data-model.md):
    - `ApiKey.cs`, `Persona.cs`, `ModelConfiguration.cs`, `ChatThread.cs`, `Message.cs`, `Artifact.cs`, `MediaItem.cs`, `PromptTemplate.cs`, `TextAction.cs`, `UsageRecord.cs`, `WikiFile.cs`, `WikiVersionSnapshot.cs`
  - Update `src/MySecondBrain.Data/AppDbContext.cs`:
    - Add `DbSet<T>` property for each of the 12 entities
    - Change `OnConfiguring` fallback path from `"Data Source=msb.db"` to `%LOCALAPPDATA%\MySecondBrain\msb.db`
    - Ensure directory is created if missing: `Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySecondBrain"))`
  - Remove `.gitkeep` from `src/MySecondBrain.Data/Entities/`

- **Automated Testing:** Data project depends on Core (already built in 1a). Must compile.

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build src/MySecondBrain.Data/MySecondBrain.Data.csproj
  ```
  Verify: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- **Suggested Commit Message:** `feat(data): create entity classes and update AppDbContext with DbSet and %LOCALAPPDATA% path`

---

### [x] Step 1c: Create Repository Stubs in Data Project

- **Goal:** Create 8 repository stub classes in `Data/Repositories/` that implement the repository interfaces from Core, constructor-inject `AppDbContext`, and return `null`/`Task.CompletedTask`. The Data project must still compile.

- **Actions:**
  - Create 8 stub repository classes in `src/MySecondBrain.Data/Repositories/`:
    - `ChatThreadRepository.cs` (implements `IChatThreadRepository`)
    - `MessageRepository.cs` (implements `IMessageRepository`)
    - `PersonaRepository.cs` (implements `IPersonaRepository`)
    - `ModelConfigurationRepository.cs` (implements `IModelConfigurationRepository`)
    - `ApiKeyRepository.cs` (implements `IApiKeyRepository`)
    - `WikiIndexRepository.cs` (implements `IWikiIndexRepository`)
    - `UsageRepository.cs` (implements `IUsageRepository`)
    - `SettingsRepository.cs` (implements `ISettingsRepository`)
  - Each stub: constructor receives `AppDbContext`, stores in private field. Every interface method returns `null`, `Task.FromResult<T?>(null)`, `Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>())`, or `Task.CompletedTask` as appropriate for the return type.
  - Remove `.gitkeep` from `src/MySecondBrain.Data/Repositories/`

- **Automated Testing:** Data project still compiles (extension of step 1b).

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build src/MySecondBrain.Data/MySecondBrain.Data.csproj
  ```
  Verify: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- **Suggested Commit Message:** `feat(data): create 8 repository stubs implementing Core repository interfaces`

---

### [x] Step 1d: Create Service/Provider Stubs in Services Project

- **Goal:** Create 35 service and provider stub classes in `Services/` subdirectories that implement service interfaces from Core. Each stub constructor-injects its dependencies via constructor injection. The Services project must compile.

- **Actions:**
  - Create 23 stub classes in appropriate subdirectories under `src/MySecondBrain.Services/`:
    - **Chat/** — `ChatThreadService.cs`, `Fts5ChatSearchService.cs`, `PeriodicAutoCleanupService.cs`
    - **LLM/** — `LLMProviderService.cs`, `LLMProviderFactory.cs`, `TokenizerFactory.cs`, `OpenAIProvider.cs`, `AnthropicProvider.cs`, `GoogleProvider.cs`, `OpenAICompatibleProvider.cs`, `OpenAIWhisperProvider.cs`, `LocalWhisperProvider.cs`, `WindowsSpeechProvider.cs`, `SharpTokenTokenizer.cs`, `AnthropicTokenizer.cs`, `FallbackTokenizer.cs`
    - **Wiki/** — `WikiService.cs`, `FileSystemWatcherAdapter.cs`
    - **Tools/** — `ToolOrchestrator.cs`, `WebSearchToolExecutor.cs`, `TerminalToolExecutor.cs`, `FileGenerateToolExecutor.cs`, `FileEditToolExecutor.cs`, `WikiSearchToolExecutor.cs`
    - **Backup/** — `GcsBackupProvider.cs`, `LocalFolderBackupProvider.cs`
    - **Encryption/** — `DpapiEncryptionService.cs`, `AesGcmChatEncryptionService.cs`
    - **Update/** — `AutoUpdaterDotNet.cs`, `MsixAppInstallerUpdater.cs`
    - **Search/** — `GoogleCustomSearchProvider.cs`, `BingSearchProvider.cs`
    - **Chat/Import/** — `ChatGPTImporter.cs`, `ClaudeImporter.cs`
    - **Audio/** — `NaudioAudioService.cs`
  - Each stub: constructor-injects required dependencies. Method bodies return `null`, empty collections, or `Task.CompletedTask`.
  - Remove all `.gitkeep` files from populated subdirectories under `src/MySecondBrain.Services/`.

- **Automated Testing:** Services project depends on Core + Data. Must compile.

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build src/MySecondBrain.Services/MySecondBrain.Services.csproj
  ```
  Verify: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- **Suggested Commit Message:** `feat(services): create 35 service and provider stubs across all service subdirectories`

---

### [x] Step 1e: Create ViewModel Stubs + Content Renderers + UI Service Stubs in UI Project

- **Goal:** Create 11 ViewModel stubs, 8 content block renderer stubs, and 15 UI-specific service stubs in the UI project. The UI project must compile.

- **Actions:**
  - Create 11 ViewModel stubs in `src/MySecondBrain.UI/ViewModels/`, each inheriting `ObservableObject` (CommunityToolkit.Mvvm), constructor-injecting required services:
    - `MainWindowViewModel.cs`, `ChatThreadViewModel.cs`, `SettingsViewModel.cs`, `WikiBrowserViewModel.cs`, `UsageDashboardViewModel.cs`, `MediaLibraryViewModel.cs`, `GlobalArtifactsBrowserViewModel.cs`, `Tier1OverlayViewModel.cs`, `Tier2CommandBarViewModel.cs`, `ModelComparisonViewModel.cs`, `OnboardingWizardViewModel.cs`
  - Create 8 content block renderer stubs in `src/MySecondBrain.UI/Controls/`:
    - 7 renderers implementing `IContentBlockRenderer`: `MarkdownTextRenderer.cs`, `CodeBlockRenderer.cs`, `ArtifactReferenceRenderer.cs`, `ImageRenderer.cs`, `MediaRenderer.cs`, `ThinkingRenderer.cs`, `ToolCallRenderer.cs`
    - 1 registry implementing `IContentRendererRegistry`: `ContentRendererRegistry.cs`
  - Create 15 UI-specific service stubs in `src/MySecondBrain.UI/Services/` (services that depend on WPF/Windows types and cannot live in the Services project):
    - `WpfClipboardService.cs`, `GlobalHotkeyService.cs`, `WpfThemeProvider.cs`, `WinFormsSystemTrayService.cs`, `Win32HwndCaptureService.cs`, `UiaTextInjectionService.cs`, `WpfVideoPlayerService.cs`, `AForgeCameraService.cs`, `HunspellSpellCheckService.cs`, `KestrelWebSocketServer.cs`, `LibGit2SharpGitService.cs`, plus any remaining platform-specific stubs
  - Add any required NuGet package references to UI.csproj (e.g., `Markdig` if used by renderer stubs via `MarkdownObject`)
  - Remove `.gitkeep` files from `src/MySecondBrain.UI/ViewModels/`, `src/MySecondBrain.UI/Controls/`, and `src/MySecondBrain.UI/Services/`

- **Automated Testing:** UI project depends on Core + Services (transitively). Must compile. No DI wiring yet — that's Step 2.

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build src/MySecondBrain.UI/MySecondBrain.UI.csproj
  ```
  Verify: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- **Suggested Commit Message:** `feat(ui): create ViewModels, content renderers, and UI service stubs`

---

### [x] Step 2: Wire Full DI Container in App.xaml.cs

- **Goal:** Expand [`App.xaml.cs`](../../src/MySecondBrain.UI/App.xaml.cs) `ConfigureServices` with all 70+ registrations per lifetime rules. Register `AppDbContext` as singleton with `%LOCALAPPDATA%` path via factory delegate. Register all multi-implementation interfaces. Make `ConfigureServices` `public static` for testability. Add `Microsoft.Extensions.Logging`.

- **Actions:**
  - Make `ConfigureServices` `public static void ConfigureServices(IServiceCollection services)`
  - `AppDbContext`: factory delegate creating `DbContextOptions<AppDbContext>` with SQLite at `%LOCALAPPDATA%\MySecondBrain\msb.db`, creating directory if needed
  - 8 repositories: `AddSingleton<IRepo, Repo>()`
  - 19 application services: `AddSingleton<IService, Service>()`
  - 4 transient services (Clipboard, Audio, Camera, VideoPlayer): `AddTransient`
  - Multi-implementation providers: 4× `AddSingleton<ILLMProvider, *>()`, 3× `ISTTProvider`, 2× `IBackupProvider`, 2× `ISearchProvider`, 3× `ITokenizer`, 2× `IChatImporter`, 5× `IToolExecutor`, 2× `IUpdateChecker`
  - `ContentRendererRegistry` + 7× `IContentBlockRenderer`: all `AddSingleton`
  - 11 ViewModels: `AddTransient`
  - `MainWindow`: `AddSingleton`
  - Logging: `services.AddLogging(b => { b.AddConsole(); b.AddDebug(); })`
  - Add all necessary `using` directives

- **Automated Testing:** Run `dotnet build MySecondBrain.sln`. Must pass with 0 errors and 0 warnings.

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build MySecondBrain.sln
  ```
  Verify: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- **Suggested Commit Message:** `feat: wire full DI container with 70+ registrations in App.xaml.cs`

---

### [x] Step 3: Unit Tests for DI Container Resolution + Final Verification

- **Goal:** Write xUnit tests that build the same `ServiceCollection` as `App.ConfigureServices`, call `BuildServiceProvider` with `ValidateOnBuild = true`, and verify every registered type resolves. Then run the full solution build in Release configuration and run all tests.

- **Actions:**
  - Create `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`
  - Build `ServiceCollection` via `App.ConfigureServices(services)` in test constructor
  - Test `CanResolve_AllSingletonServices` — `GetRequiredService<T>()` for every singleton service (one assert per type)
  - Test `CanResolve_AllRepositories` — all 8 repositories
  - Test `CanResolve_AllViewModels` — all 11 ViewModels
  - Test `CanResolve_AllProviders` — all multi-implementation providers
  - Test `ContentRendererRegistry_HasSevenRenderers` — resolves `IContentRendererRegistry`, asserts `GetRenderers().Count == 7`
  - Test `CanResolve_MainWindow` — resolves `MainWindow`
  - Test `CanResolve_AppDbContext` — resolves `AppDbContext`
  - Test `CanResolve_Logger` — resolves `ILogger<DiContainerTests>`
  - Run full solution build: `dotnet build MySecondBrain.sln --configuration Release`
  - Run all tests: `dotnet test MySecondBrain.sln --configuration Release --no-build`

- **Automated Testing:** This step IS the automated testing. All 8 tests must pass.

- **Live Smoke Test (Mandatory):**
  ```bash
  dotnet build MySecondBrain.sln --configuration Release && dotnet test MySecondBrain.sln --configuration Release --no-build --verbosity normal
  ```
  Verify build: `Build succeeded. 0 Warning(s) 0 Error(s)`. Verify tests: `8 passed, 0 failed, 0 skipped`.

- **Suggested Commit Message:** `feat: add DI container resolution unit tests, final build verification`

---

## 6. Shared Technical Context

- **[Step 1a]:** Created 41 interfaces in `Core/Interfaces/`, 4 model files in `Core/Models/`. Core.csproj now targets `net8.0-windows` with `UseWPF=true` + `Markdig` NuGet.
- **[Step 1b]:** Created 12 EF Core entities in `Data/Entities/` with full navigation properties and FK relationships. `AppDbContext` has 12 `DbSet<T>` properties and `OnConfiguring` uses `%LOCALAPPDATA%\MySecondBrain\msb.db`. Key FK chains: ChatThread→Message (1:many), Message→Message (self-ref parent/child for branching), WikiFile→WikiVersionSnapshot (FilePath PK), Persona→ModelConfiguration, ApiKey→ModelConfiguration. `OnModelCreating` configures WikiVersionSnapshot FK to WikiFile.FilePath.
- **Target Framework:** Core+Data: `net8.0-windows`; Services: `net8.0` (pending update); UI/Tests: `net8.0-windows10.0.17763.0`.
- **Nullable/ImplicitUsings/TreatWarningsAsErrors:** Enabled solution-wide via `Directory.Build.props`.
- **NuGet packages already available:** `Microsoft.Extensions.DependencyInjection 8.0.*` (UI, Services), `Microsoft.Extensions.Hosting 8.0.*` (Services), `Microsoft.Extensions.Logging 8.0.*` (Services), `CommunityToolkit.Mvvm 8.*` (UI), `Markdig *` (Core), `xunit 2.*`, `Moq 4.*` (Tests.Unit).
- **Project reference chain:** Core ← Data ← Services ← UI. Tests reference all production projects.
- **AppDbContext access:** Factory delegate in DI creates singleton with SQLite path at `%LOCALAPPDATA%\MySecondBrain\msb.db`. Directory created if missing.
- **Multi-implementation pattern:** Multiple `AddSingleton<IX, ConcreteX>()` calls. Consumers use `IEnumerable<IX>` constructor injection.
- **Stub pattern:** All implementation classes return `null`, empty collections, or `Task.CompletedTask`.
- **ConfigureServices visibility:** `public static` so unit tests can invoke it via `App.ConfigureServices(services)`.
- **Platform-specific service placement:** Services that depend on WPF/Windows types live in `UI/Services/`.
- **Entity vs. DTO separation:** Domain models in `Core/Models/` are shared POCOs; EF entities in `Data/Entities/` are independent with navigation properties. String-based enums (Provider, Role, etc.) for flexibility.
