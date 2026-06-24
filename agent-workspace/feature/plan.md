# Feature Implementation Plan: Codebase Realignment

## 1. Overall Project Context

MySecondBrain is a native Windows WPF (.NET 8) desktop app — a unified, provider-agnostic AI chat hub paired with a personal wiki. It has a 3-region WPF shell, DI container with 76+ registrations, Serilog logging, EF Core + SQLite with 13 entities, app shell with navigation/theming, Windows platform services (system tray, global hotkeys, WebSocket, auto-update), model configs/API keys/personas, settings/onboarding/diagnostics, and an E2E test suite.

## 2. Feature-Specific Context

**Feature 10: Codebase Realignment** is a Wave 3 (Vertical Slices) infrastructure realignment feature. It brings the existing codebase (Features 1-9 built with legacy tool names, 5-tool surface, no skills, WPF-only artifacts) into alignment with the evolved vision (10-tool surface, Agent Skills subsystem, WebView2 artifacts panel, workspace isolation, SQLite memory). This is a **non-user-facing infrastructure feature** — the app should behave identically after completion, but the internal architecture matches the evolved vision. All existing E2E tests (70+) must continue to pass.

Depends on: F4 (Data Layer), F5 (App Shell), F6 (Platform), F7 (Model Configs/Personas), F8 (Settings/Onboarding), F9 (E2E Rewrite).

## 3. Architecture and Extensibility

### Design Patterns Used

- **Provider/Adapter Pattern:** All tool executors implement `IToolExecutor`. The `IToolOrchestrator` resolves them via `IEnumerable<IToolExecutor>` from DI. Adding a new tool = implementing `IToolExecutor` + registering in DI.
- **Repository Pattern:** `MemoryEntry` follows the same EF Core repository pattern as all 13 existing entities.
- **Plugin/Registry Pattern:** Content block renderers implement `IContentBlockRenderer` with priority ordering. `CitationRenderer` at priority 350 slots between `ArtifactReferenceRenderer` (300) and `ImageRenderer` (400).
- **Service Pattern:** `ISkillService` + `ISkillLoader` are singletons consumed by `SkillLoadToolExecutor` and system prompt construction.

### Key Design Decisions

- Skills are **instructions, not executable code**. The model reads SKILL.md → uses existing tools (`bash`, `text_editor`). No new execution capabilities.
- Skills are **NOT persisted to SQLite** — re-discovered each launch from embedded resources + filesystem paths. In-memory `Skill` metadata records only.
- `MemoryEntry` uses Anthropic's `memory_20250818` schema (key/value pairs). Separate from wiki — wiki = user-authored knowledge, memory = AI-extracted facts.
- `text_editor` merges `file_generate` + `file_edit` into Anthropic's `text_editor_20250728` schema with 4 commands: `view`, `create`, `str_replace`, `insert`.
- `bash` replaces `terminal` with Anthropic's `bash_20250124` schema. Executes via `cmd.exe` on Windows (not actual bash). `.sh` scripts fall back to Git Bash/WSL.
- WebView2 for artifacts panel (browser-native rendering); WPF stays for chat conversation. Hybrid rendering model.
- Workspace isolation is app-enforced (path scanning), not OS-level sandboxing.
- Tool schemas match Anthropic's trained-in signatures where possible — models already know how to use these tools from training.

## 4. Final Expected Project Structure

```
src/
  MySecondBrain.Core/
    Interfaces/
      [NEW] ISkillService.cs              — Skill discovery, catalog, load, activation tracking
      [NEW] ISkillLoader.cs               — Skill activation, XML wrapping, deduplication
      [NEW] ISearchProvider.cs            — MODIFIED: add ImageSearchAsync method
      [MODIFIED] IToolOrchestrator.cs     — No interface change (already has correct signature)
    Models/
      [MODIFIED] DomainModels.cs          — Add MemoryEntry, Skill record; update ToolAutoApprovalSettings
      [MODIFIED] Enums.cs                 — No changes needed (ToolRiskLevel already exists)
      [NEW] SkillModels.cs                — SkillMetadata, SkillContent, SkillDependencies, SkillActivationResult

  MySecondBrain.Data/
    Entities/
      [NEW] MemoryEntryEntity.cs          — EF Core entity for MemoryEntry table
    [MODIFIED] AppDbContext.cs            — Add MemoryEntry DbSet + configuration

  MySecondBrain.Services/
    Tools/
      [RENAMED] TerminalToolExecutor.cs → BashToolExecutor.cs
      [RENAMED] FileGenerateToolExecutor.cs → DELETED (merged into TextEditorToolExecutor)
      [RENAMED] FileEditToolExecutor.cs → DELETED (merged into TextEditorToolExecutor)
      [NEW] TextEditorToolExecutor.cs     — Anthropic text_editor_20250728: view/create/str_replace/insert
      [MODIFIED] ToolOrchestrator.cs      — Manage 10 tools, updated auto-approval settings
      [NEW] WebFetchToolExecutor.cs       — HttpClient GET, URL constraint
      [NEW] MemoryToolExecutor.cs         — SQLite-backed, Anthropic memory_20250818
      [NEW] SkillLoadToolExecutor.cs      — Reads SKILL.md, XML wrapping, dedup
      [NEW] AskUserInputToolExecutor.cs   — Structured WPF confirmation dialogs
      [NEW] PresentFilesToolExecutor.cs   — Workspace→artifacts copy, WebView2 trigger
      [NEW] ImageSearchToolExecutor.cs    — Image search via ISearchProvider
    Search/
      [MODIFIED] ISearchProvider (in Core/Interfaces/) — Add ImageSearchAsync
      [MODIFIED] GoogleCustomSearchProvider.cs — Implement image search
      [MODIFIED] BingSearchProvider.cs    — Implement image search
    Skills/
      [NEW] AgentSkillService.cs          — ISkillService: discovery, catalog, activation tracking
      [NEW] StructuredSkillLoader.cs      — ISkillLoader: reads SKILL.md, XML wrapping
    [MODIFIED] MySecondBrain.Services.csproj — Add YAML parsing dependency

  MySecondBrain.UI/
    Controls/
      [NEW] CitationRenderer.cs           — Priority 350, clickable superscript citations
      [NEW] ArtifactsWebView2Host.cs      — WebView2 wrapper with theme bridge
      [RENAMED] ArtifactReferenceRenderer.cs → MODIFIED (stub → WebView2 integration)
    [MODIFIED] MySecondBrain.UI.csproj    — Add Microsoft.Web.WebView2.Wpf NuGet
    [MODIFIED] DependencyInjectionConfig.cs — 10 tool registrations + ISkillService + ISkillLoader + CitationRenderer

  tests/unit/
    [MODIFIED] DiContainerViewModelPlatformTests.cs — Update tool count from 5 to 10
    [NEW] ToolExecutorTests.cs            — Tests for renamed + new executors
    [NEW] SkillServiceTests.cs            — ISkillService discovery, catalog, dedup
    [NEW] SkillLoaderTests.cs             — ISkillLoader wrapping, enum constraint
```

---

## 5. Execution Steps

### [x] Step 1: Add MemoryEntry Entity + Skill Models to Core

- **Goal:** Add `MemoryEntry` domain model and `Skill` metadata records to Core, plus update `ToolAutoApprovalSettings`.
- **Actions:**
  - Add `MemoryEntry` class to [`DomainModels.cs`](src/MySecondBrain.Core/Models/DomainModels.cs:143) (id, key, value, sourceThreadId, createdAt, updatedAt)
  - Create [`SkillModels.cs`](src/MySecondBrain.Core/Models/SkillModels.cs) with `SkillMetadata`, `SkillContent`, `SkillDependencies`, `SkillActivationResult` records
  - Update `ToolAutoApprovalSettings` fields: rename `AutoApproveFileGenerate` → `AutoApproveTextEditor`, remove `AutoApproveFileEdit`, add `AutoApproveBash`, `AutoApproveWebFetch`, `AutoApproveMemory`, `AutoApproveSkillLoad`, `AutoApproveAskUserInput`, `AutoApprovePresentFiles`, `AutoApproveImageSearch`
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs`: Add test for `MemoryEntry` property validation (key max 200 chars, value max 10KB)
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs`: Add test for `ToolAutoApprovalSettings` new field names (10 tool fields present)
- **Integration Tests to Write:** None — pure model changes, no infrastructure interaction.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet build src/MySecondBrain.Core` — verify successful compilation with new models.
- **Suggested Commit Message:** `feat: add MemoryEntry entity and Skill models to Core`

---

### [x] Step 2: Add ISkillService + ISkillLoader Interfaces

- **Goal:** Add the two new Core interfaces for Agent Skills subsystem.
- **Actions:**
  - Create [`ISkillService.cs`](src/MySecondBrain.Core/Interfaces/ISkillService.cs) with methods: `DiscoverAsync`, `GetCatalog`, `LoadAsync`, `ListResourcesAsync`, `IsActivated`, `MarkActivated`, `ResetActivationTracking`, `GetDependencies`
  - Create [`ISkillLoader.cs`](src/MySecondBrain.Core/Interfaces/ISkillLoader.cs) with methods: `ActivateSkillAsync`, `GetToolDefinition`, `IsValidSkill`
- **Unit Tests to Write:** None — interfaces only, no implementation to test.
- **Integration Tests to Write:** None — interfaces only.
- **Automated Test Commands:**
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet build src/MySecondBrain.Core` — verify both interfaces compile.
- **Suggested Commit Message:** `feat: add ISkillService and ISkillLoader interfaces`

---

### [x] Step 3: Add ISearchProvider Image Search Extension

- **Goal:** Extend `ISearchProvider` with image search capability for the `image_search` tool.
- **Actions:**
  - Add `Task<ImageSearchResults> ImageSearchAsync(string query, int maxResults, CancellationToken ct)` method to [`ISearchProvider.cs`](src/MySecondBrain.Core/Interfaces/ISearchProvider.cs)
  - Add `ImageSearchResults` and `ImageSearchResultItem` records to `ServiceDtos.cs` or new file
  - Update `GoogleCustomSearchProvider` to implement `ImageSearchAsync` (stub — actual implementation deferred to Feature 16 Tool Use)
  - Update `BingSearchProvider` to implement `ImageSearchAsync` (stub)
- **Unit Tests to Write:** None — stub implementations only.
- **Integration Tests to Write:** None — stub implementations only.
- **Automated Test Commands:**
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet build src/MySecondBrain.Services` — verify compilation with new interface method.
- **Suggested Commit Message:** `feat: extend ISearchProvider with image search support`

---

### [x] Step 4: Add MemoryEntry EF Core Entity + Migration

- **Goal:** Add `MemoryEntry` entity to the Data project with EF Core configuration and database migration.
- **Actions:**
  - Create [`MemoryEntryEntity.cs`](src/MySecondBrain.Data/Entities/MemoryEntryEntity.cs) with EF Core entity configuration
  - Add `DbSet<MemoryEntryEntity> MemoryEntries` to [`AppDbContext.cs`](src/MySecondBrain.Data/AppDbContext.cs:23) (after line 23)
  - Add Fluent API configuration in `OnModelCreating`: indexes on `Key`, `CreatedAt`; FK to `ChatThread` (optional, SetNull on delete)
  - Create EF Core migration: `dotnet ef migrations add AddMemoryEntry`
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/DbContextSchemaTests.cs`: Verify `MemoryEntries` DbSet exists, verify FK relationship to ChatThread, verify indexes
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs`: Add MemoryEntry CRUD test via in-memory database
- **Integration Tests to Write:** None — unit tests with in-memory SQLite cover schema verification.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "MemoryEntry"` — verify all MemoryEntry tests pass.
  - Run `dotnet ef migrations list --project src/MySecondBrain.Data` — verify `AddMemoryEntry` migration appears.
- **Suggested Commit Message:** `feat: add MemoryEntry entity with EF Core migration`

---

### [x] Step 5: Rename Tool Executors (Terminal→Bash, FileGenerate+FileEdit→TextEditor)

- **Goal:** Rename `TerminalToolExecutor` to `BashToolExecutor`, merge `FileGenerateToolExecutor` + `FileEditToolExecutor` into `TextEditorToolExecutor`.
- **Actions:**
  - Delete [`TerminalToolExecutor.cs`](src/MySecondBrain.Services/Tools/TerminalToolExecutor.cs) and [`FileGenerateToolExecutor.cs`](src/MySecondBrain.Services/Tools/FileGenerateToolExecutor.cs) and [`FileEditToolExecutor.cs`](src/MySecondBrain.Services/Tools/FileEditToolExecutor.cs)
  - Create [`BashToolExecutor.cs`](src/MySecondBrain.Services/Tools/BashToolExecutor.cs): `ToolName = "bash"`, `RequiresUserConfirmation = true`, `RiskLevel = Medium`, `CanAutoApprove = false`. Stub implementations for `ValidateAsync`, `ExecuteAsync`, `GetConfirmationDescription`.
  - Create [`TextEditorToolExecutor.cs`](src/MySecondBrain.Services/Tools/TextEditorToolExecutor.cs): `ToolName = "text_editor"`, `RequiresUserConfirmation = false` (true for delete), `RiskLevel = Low`, `CanAutoApprove = true`. Stub implementations. Accepts `command` parameter: `view`/`create`/`str_replace`/`insert`.
  - No workspace isolation yet (that's Step 13).
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test `BashToolExecutor.ToolName` returns `"bash"`, test `RiskLevel` is `Medium`, test `CanAutoApprove` is `false`, test `RequiresUserConfirmation` is `true`
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test `TextEditorToolExecutor.ToolName` returns `"text_editor"`, test `RiskLevel` is `Low`, test `CanAutoApprove` is `true`
- **Integration Tests to Write:** None — stub implementations only.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet build src/MySecondBrain.Services` — verify compilation without old executor files.
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "BashToolExecutor|TextEditorToolExecutor"` — verify tests pass.
- **Suggested Commit Message:** `refactor: rename Terminal→Bash, merge FileGenerate+FileEdit→TextEditor`

---

### [x] Step 6: Add 6 New Tool Executors (WebFetch, Memory, SkillLoad, AskUserInput, PresentFiles, ImageSearch)

- **Goal:** Create stub implementations for all 6 new `IToolExecutor` implementations.
- **Actions:**
  - Create [`WebFetchToolExecutor.cs`](src/MySecondBrain.Services/Tools/WebFetchToolExecutor.cs): `ToolName = "web_fetch"`, `RiskLevel = Low`, `CanAutoApprove = true`
  - Create [`MemoryToolExecutor.cs`](src/MySecondBrain.Services/Tools/MemoryToolExecutor.cs): `ToolName = "memory"`, `RiskLevel = Low`, `CanAutoApprove = true`
  - Create [`SkillLoadToolExecutor.cs`](src/MySecondBrain.Services/Tools/SkillLoadToolExecutor.cs): `ToolName = "skill_load"`, `RiskLevel = Low`, `CanAutoApprove = true`. Depends on `ISkillLoader`.
  - Create [`AskUserInputToolExecutor.cs`](src/MySecondBrain.Services/Tools/AskUserInputToolExecutor.cs): `ToolName = "ask_user_input"`, `RiskLevel = Low`, `CanAutoApprove = true`. Depends on `IConfirmationService`.
  - Create [`PresentFilesToolExecutor.cs`](src/MySecondBrain.Services/Tools/PresentFilesToolExecutor.cs): `ToolName = "present_files"`, `RiskLevel = Low`, `CanAutoApprove = true`
  - Create [`ImageSearchToolExecutor.cs`](src/MySecondBrain.Services/Tools/ImageSearchToolExecutor.cs): `ToolName = "image_search"`, `RiskLevel = Low`, `CanAutoApprove = true`. Depends on `ISearchProvider`.
  - All with stub `ValidateAsync`, `ExecuteAsync`, `GetConfirmationDescription` implementations (return defaults).
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test each new executor returns correct `ToolName`, `RiskLevel`, `CanAutoApprove`, `RequiresUserConfirmation`
- **Integration Tests to Write:** None — stub implementations only.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet build src/MySecondBrain.Services` — verify all 6 new files compile.
- **Suggested Commit Message:** `feat: add 6 new tool executor stubs (web_fetch, memory, skill_load, ask_user_input, present_files, image_search)`

---

### [x] Step 7: Implement ISkillService (AgentSkillService) + ISkillLoader (StructuredSkillLoader)

- **Goal:** Implement the Agent Skills subsystem — skill discovery, catalog, loading, activation tracking.
- **Actions:**
  - Create [`AgentSkillService.cs`](src/MySecondBrain.Services/Skills/AgentSkillService.cs):
    - `DiscoverAsync()`: Scan 4 locations — embedded resources (`Skills/anthropic/` in MySecondBrain.UI.dll), `%LOCALAPPDATA%/MySecondBrain/skills/`, `%USERPROFILE%/.agents/skills/`, `%USERPROFILE%/.claude/skills/`
    - Parse YAML frontmatter from each `SKILL.md` (lenient validation using simple regex — full YAML library not needed for two-field extraction)
    - Name collision resolution: user overrides built-in, cross-client overrides user
    - Track activation state in `ConcurrentDictionary<string, bool>`
    - `GetCatalog()`: Return `IReadOnlyList<SkillMetadata>` of enabled skills
    - `LoadAsync()`: Read full SKILL.md body (strip YAML frontmatter), return `SkillContent`
  - Create [`StructuredSkillLoader.cs`](src/MySecondBrain.Services/Skills/StructuredSkillLoader.cs):
    - `ActivateSkillAsync()`: Check deduplication → call `ISkillService.LoadAsync()` → wrap in `<skill_content name="skill-name">...</skill_content>` XML with `<skill_resources>` listing
    - `GetToolDefinition()`: Return `skill_load` tool schema with `enum` constraint populated from enabled skill names
    - `IsValidSkill()`: Check against discovered skill names
  - Configure skills as embedded resources in [`MySecondBrain.UI.csproj`](src/MySecondBrain.UI/MySecondBrain.UI.csproj) — add `<EmbeddedResource Include="Skills\anthropic\**\*" />`
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/SkillServiceTests.cs`: Test discovery from embedded resources, test catalog returns enabled skills, test deduplication (second activation skipped), test collision resolution, test invalid YAML skipped, test missing description skipped
  - `tests/unit/MySecondBrain.Tests.Unit/SkillLoaderTests.cs`: Test XML wrapping format, test enum constraint populated from enabled skills, test `IsValidSkill` with valid/invalid names, test `ActivateSkillAsync` returns `SkillActivationResult`
- **Integration Tests to Write:**
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs`: Test skill discovery from temp directory with valid SKILL.md files on actual filesystem
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs`: Test YAML frontmatter parsing from actual filesystem files (valid and invalid formats)
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs`: Test collision resolution with user-level and cross-client-level overrides on disk
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "SkillService|SkillLoader"` — verify all tests pass.
- **Suggested Commit Message:** `feat: implement AgentSkillService and StructuredSkillLoader`

---

### [x] Step 8: Add CitationRenderer (Priority 350) and Register in DI

- **Goal:** Create `CitationRenderer` to handle inline citation markers (`[1]`, `[2]`) and footnote definitions (`[^1]:`), and register it in the DI container.
- **Actions:**
  - Create [`CitationRenderer.cs`](src/MySecondBrain.UI/Controls/CitationRenderer.cs):
    - `RendererName = "Citation"`, `Priority = 350`
    - `CanRender()`: Detect Markdig `Footnote` and `FootnoteLink` nodes
    - `RenderAsync()`: Render inline markers as clickable superscript `Hyperlink`; render footnote definitions as styled `Paragraph` with index, bold linked title, domain, date
    - Click action: `FrameworkElement.BringIntoView()` to navigate to footnote
    - Graceful degradation: missing footnotes → plain text; missing URLs → plain text title
  - Register `CitationRenderer` in [`DependencyInjectionConfig.cs`](src/MySecondBrain.UI/DependencyInjectionConfig.cs) as `IContentBlockRenderer` at priority 350
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Verify `CitationRenderer.Priority` is 350
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test `CanRender()` returns `true` for Markdig `Footnote` and `FootnoteLink` nodes
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test `CanRender()` returns `false` for non-citation nodes (Paragraph, Heading, CodeBlock)
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test `RenderAsync()` produces `Hyperlink` element with superscript styling for inline citation markers `[1]`, `[2]`
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test `RenderAsync()` produces styled `Paragraph` for footnote definitions `[^1]:`
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test graceful degradation — missing footnote anchor → plain text fallback, missing URL → plain text title
- **Integration Tests to Write:** None — rendering verification is manual.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
  - Launch app, send a message that produces citations (e.g., web_search results), verify superscript citation markers appear and are clickable. Verify footnote definitions render with styled paragraphs. Verify graceful degradation when footnote URL is missing.
- **Suggested Commit Message:** `feat: add CitationRenderer with priority 350 for footnote-style citations`

---

### [x] Step 9: Update DI Registrations (10 Tools + Skills)

- **Goal:** Update [`DependencyInjectionConfig.cs`](src/MySecondBrain.UI/DependencyInjectionConfig.cs) to register all 10 tool executors and skill services.
- **Actions:**
  - Remove old registrations: `TerminalToolExecutor`, `FileGenerateToolExecutor`, `FileEditToolExecutor`
  - Add new registrations: `BashToolExecutor`, `TextEditorToolExecutor`, `WebFetchToolExecutor`, `MemoryToolExecutor`, `SkillLoadToolExecutor`, `AskUserInputToolExecutor`, `PresentFilesToolExecutor`, `ImageSearchToolExecutor`
  - Add `ISkillService` → `AgentSkillService` (singleton)
  - Add `ISkillLoader` → `StructuredSkillLoader` (singleton)
  - Total `IToolExecutor` registrations: 10 (bash, text_editor, web_search, web_fetch, wiki_search, memory, skill_load, ask_user_input, present_files, image_search)
  - Total content block renderer registrations: 8 (CitationRenderer was registered in Step 8)
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/DiContainerViewModelPlatformTests.cs`: Update `IToolExecutor` count assertion from 5 to 10
  - `tests/unit/MySecondBrain.Tests.Unit/DiContainerViewModelPlatformTests.cs`: Add assertions for `ISkillService` and `ISkillLoader` resolution
  - `tests/unit/MySecondBrain.Tests.Unit/DiContainerRepositoryServiceTests.cs`: Add assertion for 10 tool executor types
- **Integration Tests to Write:** None — DI container unit tests cover registration correctness.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "DiContainer"` — verify DI tests pass with updated counts.
- **Suggested Commit Message:** `feat: update DI registrations for 10 tools and skills subsystem`

---

### [x] Step 10: Add WebView2 NuGet + ArtifactsWebView2Host Control

- **Goal:** Add WebView2 dependency and create the host WPF control for artifacts rendering.
- **Actions:**
  - Add `Microsoft.Web.WebView2.Wpf` NuGet package to [`MySecondBrain.UI.csproj`](src/MySecondBrain.UI/MySecondBrain.UI.csproj)
  - Create [`ArtifactsWebView2Host.cs`](src/MySecondBrain.UI/Controls/ArtifactsWebView2Host.cs):
    - WPF `UserControl` wrapping `WebView2`
    - `NavigateToArtifact(string filePath)`: Load artifact via `file:///` URL
    - Theme bridge: `CoreWebView2.ExecuteScriptAsync()` to toggle dark/light CSS class
    - Support for: code (Prism.js), Markdown (marked.js), diffs (diff2html.js), interactive HTML, SVG/PDF
    - Fallback: WPF `TextBlock` with plain text if WebView2 runtime unavailable
  - Update [`ArtifactReferenceRenderer.cs`](src/MySecondBrain.UI/Controls/ArtifactReferenceRenderer.cs) to reference WebView2 host instead of stub
- **Unit Tests to Write:** None — WebView2 is a UI control, tested via E2E.
- **Integration Tests to Write:** None — UI control.
- **Automated Test Commands:**
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
  - Launch app, verify artifacts panel renders in right panel. Verify theme toggle updates WebView2 content.
- **Suggested Commit Message:** `feat: add WebView2 artifacts panel with theme bridge`

---

### [x] Step 11: Implement Per-Chat Toolbar Toggles (Tools, Skills, Memory)

- **Goal:** Add toolbar toggles in the chat textbox area for per-chat tool/skill/memory control.
- **Actions:**
  - Add "🔧 Tools ▼" dropdown to [`ChatView.xaml`](src/MySecondBrain.UI/Views/ChatView.xaml) with per-tool checkboxes (bash, text_editor, web_search, web_fetch, wiki_search, memory, skill_load, ask_user_input, present_files, image_search)
  - Add "📚 Skills ▼" dropdown with per-skill checkboxes + "All on/off"
  - Add "🧠 Mem" toggle (default OFF)
  - New chats inherit global defaults from Settings → Tools / Skills / Memory
  - Disabled tools are removed from the API tools array entirely (not hidden)
  - Update [`ChatView.xaml.cs`](src/MySecondBrain.UI/Views/ChatView.xaml.cs) with toggle event handlers
- **Unit Tests to Write:** None — UI controls tested via E2E.
- **Integration Tests to Write:** None — UI-only feature.
- **Automated Test Commands:**
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
  - Launch app, open a chat, verify Tools/Skills/Memory toggles appear in toolbar. Toggle tools off → verify they are removed from system prompt (observable via API call or debug log).
- **Suggested Commit Message:** `feat: add per-chat toolbar toggles for tools, skills, and memory`

---

### [ ] Step 12: Implement Additive System Prompt Construction

- **Goal:** Replace simple variable replacement with additive system prompt assembly per the skills integration spec.
- **Actions:**
  - Create system prompt assembly service (or update existing prompt construction in ChatThreadService/LLMProviderService):
    ```
    System prompt =
        [persona.system_message]           ← only if non-empty
        [behavioral_instructions]          ← always
        [date_time_context]                ← always
        [platform_context]                 ← always (Windows, cmd.exe, workspace path, bash availability)
        [<available_skills> block]         ← only if ≥1 skill enabled
        [skill_usage_instructions]         ← only if ≥1 skill enabled
    ```
  - Tools array assembled additively per enabled tools (disabled tools removed entirely)
  - `ask_user_input` always in tools array (needed for confirmations)
  - `skill_load` only if ≥1 skill enabled
  - Edge case: empty persona + everything disabled = no system prompt, empty tools array
  - Detect bash availability (Git Bash at `C:\Program Files\Git\bin\bash.exe`, WSL via `wsl --status`) at startup; include in platform context
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ChatThreadViewModelTests.cs`: Test system prompt assembly with all enabled, with some disabled, with everything disabled (empty prompt + empty tools)
  - `tests/unit/MySecondBrain.Tests.Unit/ChatThreadViewModelTests.cs`: Test skill catalog XML format when skills enabled
  - `tests/unit/MySecondBrain.Tests.Unit/ChatThreadViewModelTests.cs`: Test `ask_user_input` always present in tools array
- **Integration Tests to Write:** None — system prompt construction is pure logic.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "SystemPrompt|Prompt"` — verify all prompt tests pass.
- **Suggested Commit Message:** `feat: implement additive system prompt construction with skill catalog`

---

### [ ] Step 13: Implement Workspace Isolation for Bash Tool

- **Goal:** Add workspace isolation to `BashToolExecutor` — all commands execute in `%LOCALAPPDATA%/MySecondBrain/workspace/`.
- **Actions:**
  - Update [`BashToolExecutor.cs`](src/MySecondBrain.Services/Tools/BashToolExecutor.cs):
    - Set `Process.StartInfo.WorkingDirectory` to workspace path before execution
    - Scan command for absolute paths (`C:\`, `%`, `~`) and block pre-execution
    - Wiki directory read-only from bash
    - Implement 24h auto-cleanup of workspace files on app startup
    - Detect bash.exe/WSL at startup, report in tool description
  - Platform adaptation: `.sh` scripts → try Git Bash → try WSL → error if neither available; heredocs redirected to `text_editor`
  - Create workspace directory on first use if not exists
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test workspace directory is set as working directory
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test absolute path detection (`C:\`, `%`, `~` patterns)
  - `tests/unit/MySecondBrain.Tests.Unit/ToolExecutorTests.cs`: Test wiki directory blocked for writes
- **Integration Tests to Write:**
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs`: Test bash executes command in workspace, test workspace directory exists after execution, test absolute path blocked
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "BashToolExecutor|Workspace"` — verify tests pass.
- **Suggested Commit Message:** `feat: implement workspace isolation for bash tool`

---

### [ ] Step 14: Update Knowledge Files

- **Goal:** Update the agent-workspace knowledge files to reflect the new architecture.
- **Actions:**
  - Update [`agent-workspace/knowledge/architecture.md`](agent-workspace/knowledge/architecture.md): 10-tool surface, skills subsystem, WebView2 artifacts panel
  - Update [`agent-workspace/knowledge/database.md`](agent-workspace/knowledge/database.md): MemoryEntry entity, 13→15 entity count
  - Update [`agent-workspace/knowledge/frontend-ui.md`](agent-workspace/knowledge/frontend-ui.md): WebView2 artifacts panel, per-chat toolbar toggles
  - Update [`agent-workspace/knowledge/api-routes.md`](agent-workspace/knowledge/api-routes.md): skill_load tool schema, 10-tool surface
- **Unit Tests to Write:** None — documentation only.
- **Integration Tests to Write:** None — documentation only.
- **Automated Test Commands:**
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
  - Open each of the 4 knowledge files and verify content reflects the new architecture: 10-tool surface, skills subsystem, WebView2 artifacts panel, MemoryEntry entity. Verify file size > 0 and content is meaningful.
- **Suggested Commit Message:** `docs: update knowledge files for 10-tool surface, skills, and WebView2`

---

### [ ] Step 15: Run Full Test Suite & E2E Verification

- **Goal:** Verify no regressions — all 70+ existing E2E tests pass, all unit tests pass, DI container resolves correctly.
- **Actions:**
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit` — verify all unit tests pass
  - Run `dotnet test tests/integration/MySecondBrain.Tests.Integration` — verify all integration tests pass
  - Run `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug` — verify all 70+ E2E tests pass
  - Fix any failures caused by tool renames or DI changes
- **Unit Tests to Write:** None — this step is verification only.
- **Integration Tests to Write:** None — verification only.
- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration`
  - `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug`
  - `dotnet build MySecondBrain.sln`
- **Smoke Test Classification:** Model
  - Verify `dotnet test` exit code is 0 for unit, integration, and E2E test suites.
- **Suggested Commit Message:** `test: verify full test suite passes after codebase realignment`

---

## 6. Shared Technical Context

- **Project Test Commands:**
  - Unit: `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - Integration: `dotnet test tests/integration/MySecondBrain.Tests.Integration`
  - E2E: `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug`
  - Build: `dotnet build MySecondBrain.sln`

- **Key Architecture Notes:**
  - All tool executors implement `IToolExecutor` — resolved via `IEnumerable<IToolExecutor>` in `ToolOrchestrator`
  - `ToolCall` and `ToolResult` records defined in `ServiceDtos.cs` (check `src/MySecondBrain.Core/Models/ServiceDtos.cs`)
  - `ToolDefinition` record used for API tool schemas
  - Skills are NOT persisted to SQLite — re-discovered each launch
  - `MemoryEntry` IS persisted to SQLite with Anthropic `memory_20250818` schema
  - WebView2 runtime: pre-installed on Win11, auto-installed on Win10
  - `DependencyInjectionConfig.cs` is the single source of truth for all DI registrations
  - `AppDbContext` at `src/MySecondBrain.Data/AppDbContext.cs` has Fluent API configuration in `OnModelCreating`
  - Content renderers are in `src/MySecondBrain.UI/Controls/` with priority ordering

- **Step 1 — MemoryEntry & Skill Models (completed):**
  - `MemoryEntry` in `DomainModels.cs`: Id (string GUID), Key (string ≤200), Value (string ≤10KB), SourceThreadId (string?), CreatedAt, UpdatedAt. Constants: KeyMaxLength=200, ValueMaxLength=10240.
  - `SkillModels.cs`: `SkillMetadata(Name, Description, Source, Location)`, `SkillContent(Name, Body, Resources)`, `SkillDependencies(Tools?, Packages?, System?)`, `SkillActivationResult(Success, Content?, ErrorMessage?)` — all immutable records.
  - `ToolAutoApprovalSettings`: 10 bool fields (AutoApproveBash, TextEditor, WebSearch, WebFetch, WikiSearch, Memory, SkillLoad, AskUserInput, PresentFiles, ImageSearch) + MaxConsecutiveAutoApprovals=10. Removed: AutoApproveFileGenerate, AutoApproveFileEdit.

- **File Deletion Notes:**
  - `TerminalToolExecutor.cs` → DELETED (replaced by `BashToolExecutor.cs`)
  - `FileGenerateToolExecutor.cs` → DELETED (merged into `TextEditorToolExecutor.cs`)
  - `FileEditToolExecutor.cs` → DELETED (merged into `TextEditorToolExecutor.cs`)
