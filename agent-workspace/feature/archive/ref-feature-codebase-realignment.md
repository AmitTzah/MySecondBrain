# Feature Reference: Codebase Realignment

## Global & Shared Documentation

| Reference | Location | Purpose |
|-----------|----------|---------|
| Abstractions (all interfaces) | [`planning/abstractions.md`](../agent-workspace/project-director/planning/abstractions.md) | `ISkillService` (§7), `ISkillLoader` (§7), `IToolExecutor` (§7), `ICitationRenderer` (§8), all tool schemas |
| Skills Integration Design | [`planning/skills-integration.md`](../agent-workspace/project-director/planning/skills-integration.md) | Full skills subsystem: discovery, activation, system prompt, per-chat controls, platform adaptation |
| Artifacts & Skills Reference | [`planning/artifacts-and-skills-reference.md`](../agent-workspace/project-director/planning/artifacts-and-skills-reference.md) | End-to-end: workspace/artifacts layout, `present_files` bridge, versioning, WebView2 rendering |
| Data Model | [`planning/data-model.md`](../agent-workspace/project-director/planning/data-model.md) | `MemoryEntry` (§14), `Skill` (§15 in-memory), AppSetting keys |
| Platform Notes | [`planning/platform-notes.md`](../agent-workspace/project-director/planning/platform-notes.md) | Embedded resource config (§Skills), WebView2 integration, bash-on-Windows, workspace isolation |
| Architecture | [`planning/architecture.md`](../agent-workspace/project-director/planning/architecture.md) | Component diagram, tool orchestrator, content renderer registry, citation pipeline |
| Tech Stack | [`planning/tech-stack.md`](../agent-workspace/project-director/planning/tech-stack.md) | WebView2 runtime dependency, NuGet packages |
| Integration Points | [`planning/integration-points.md`](../agent-workspace/project-director/planning/integration-points.md) | ISearchProvider, platform integrations |
| Agent Skills Spec | [`vision/features/agent-skills.md`](../agent-workspace/project-director/vision/features/agent-skills.md) | W1-W11: skill set, progressive disclosure, discovery, toolbar, memory, Deep Research |
| Tool Use Spec | [`vision/features/tool-use-agents.md`](../agent-workspace/project-director/vision/features/tool-use-agents.md) | H1-H11: all 10 tools, auto-approval, citations |
| Artifacts Panel Spec | [`vision/features/artifacts-side-panel.md`](../agent-workspace/project-director/vision/features/artifacts-side-panel.md) | F1-F7: WebView2 panel, present_files, versioning |
| Memory Entry Data | [`vision/data/memory-entry.md`](../agent-workspace/project-director/vision/data/memory-entry.md) | MemoryEntry attributes, lifecycle, relationships |
| Skill Data | [`vision/data/skill.md`](../agent-workspace/project-director/vision/data/skill.md) | Skill metadata (in-memory), discovery, enable/disable |
| Roadmap F10 Spec | [`roadmap.md`](../agent-workspace/project-director/roadmap.md#feature-10--codebase-realignment-architecture-evolution) | Feature 10 specification with all requirements |

---

## Step-Specific Documentation

### Step 1: Add MemoryEntry Entity + Skill Models to Core

- **Existing code to modify:**
  - [`DomainModels.cs`](../../src/MySecondBrain.Core/Models/DomainModels.cs): Current `ToolAutoApprovalSettings` at line 121 has old field names (`AutoApproveFileGenerate`, `AutoApproveFileEdit`). Replace with 10 tool-specific fields.
- **New files:**
  - `src/MySecondBrain.Core/Models/SkillModels.cs`
- **Key types from abstractions:**
  - `MemoryEntry`: id (UUID), key (string ≤200), value (string ≤10KB), sourceThreadId (UUID?), createdAt, updatedAt
  - `SkillMetadata`: Name, Description, Source, Location
  - `SkillContent`: Name, Body, Resources
  - `SkillDependencies`: Tools, Packages, System
  - `SkillActivationResult`: Success, Content, ErrorMessage
- **ToolAutoApprovalSettings new fields:**
  - `AutoApproveBash`, `AutoApproveTextEditor`, `AutoApproveWebSearch`, `AutoApproveWebFetch`, `AutoApproveWikiSearch`, `AutoApproveMemory`, `AutoApproveSkillLoad`, `AutoApproveAskUserInput`, `AutoApprovePresentFiles`, `AutoApproveImageSearch`
  - Remove: `AutoApproveFileGenerate`, `AutoApproveFileEdit`

### Step 2: Add ISkillService + ISkillLoader Interfaces

- **New files:**
  - `src/MySecondBrain.Core/Interfaces/ISkillService.cs`
  - `src/MySecondBrain.Core/Interfaces/ISkillLoader.cs`
- **Key types:**
  - `ISkillService` full spec: [`abstractions.md §7`](../agent-workspace/project-director/planning/abstractions.md) — `DiscoverAsync`, `GetCatalog`, `LoadAsync`, `ListResourcesAsync`, `IsActivated`, `MarkActivated`, `ResetActivationTracking`, `GetDependencies`
  - `ISkillLoader` full spec: [`abstractions.md §7`](../agent-workspace/project-director/planning/abstractions.md) — `ActivateSkillAsync`, `GetToolDefinition`, `IsValidSkill`

### Step 3: Add ISearchProvider Image Search Extension

- **Existing file to modify:**
  - [`ISearchProvider.cs`](../../src/MySecondBrain.Core/Interfaces/ISearchProvider.cs)
- **New DTOs:**
  - `ImageSearchResults`: Query, Items (list of `ImageSearchResultItem`), TotalEstimatedResults
  - `ImageSearchResultItem`: Title, ThumbnailUrl, SourceUrl, Width, Height
- **Existing implementations to update (stub only):**
  - [`GoogleCustomSearchProvider.cs`](../../src/MySecondBrain.Services/Search/GoogleCustomSearchProvider.cs)
  - [`BingSearchProvider.cs`](../../src/MySecondBrain.Services/Search/BingSearchProvider.cs)

### Step 4: Add MemoryEntry EF Core Entity + Migration

- **New file:**
  - `src/MySecondBrain.Data/Entities/MemoryEntryEntity.cs`
- **Existing file to modify:**
  - [`AppDbContext.cs`](../../src/MySecondBrain.Data/AppDbContext.cs): Add `DbSet<MemoryEntryEntity>` and Fluent API config
- **Migration command:** `dotnet ef migrations add AddMemoryEntry --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI`
- **Entity configuration:**
  - Table: `MemoryEntries`
  - PK: `Id` (string, GUID)
  - Index on `Key` (for fast lookup)
  - Index on `CreatedAt`
  - FK: `SourceThreadId` → `ChatThread.Id`, optional, `OnDelete(DeleteBehavior.SetNull)`

### Step 5: Rename Tool Executors (Terminal→Bash, FileGenerate+FileEdit→TextEditor)

- **Files to DELETE:**
  - [`TerminalToolExecutor.cs`](../../src/MySecondBrain.Services/Tools/TerminalToolExecutor.cs)
  - [`FileGenerateToolExecutor.cs`](../../src/MySecondBrain.Services/Tools/FileGenerateToolExecutor.cs)
  - [`FileEditToolExecutor.cs`](../../src/MySecondBrain.Services/Tools/FileEditToolExecutor.cs)
- **New files:**
  - [`BashToolExecutor.cs`](../../src/MySecondBrain.Services/Tools/BashToolExecutor.cs)
  - [`TextEditorToolExecutor.cs`](../../src/MySecondBrain.Services/Tools/TextEditorToolExecutor.cs)
- **Tool specs from abstractions.md §7:**
  - `bash`: `bash_20250124` schema, Risk=Medium, CanAutoApprove=false, RequiresUserConfirmation=true
  - `text_editor`: `text_editor_20250728` schema, commands: view/create/str_replace/insert, Risk=Low, CanAutoApprove=true

### Step 6: Add 6 New Tool Executors

- **New files (all in `src/MySecondBrain.Services/Tools/`):**
  - `WebFetchToolExecutor.cs`: `web_fetch`, Risk=Low, CanAutoApprove=true
  - `MemoryToolExecutor.cs`: `memory`, `memory_20250818` schema, Risk=Low, CanAutoApprove=true
  - `SkillLoadToolExecutor.cs`: `skill_load`, Risk=Low, CanAutoApprove=true. Constructor takes `ISkillLoader`
  - `AskUserInputToolExecutor.cs`: `ask_user_input`, Risk=Low, CanAutoApprove=true. Constructor takes `IConfirmationService`
  - `PresentFilesToolExecutor.cs`: `present_files`, Risk=Low, CanAutoApprove=true
  - `ImageSearchToolExecutor.cs`: `image_search`, Risk=Low, CanAutoApprove=true. Constructor takes `ISearchProvider`
- **Tool specs:** [`abstractions.md §7`](../agent-workspace/project-director/planning/abstractions.md) — Anthropic-matched tools (bash, text_editor, web_search, web_fetch, memory) + custom tools (wiki_search, skill_load, ask_user_input, present_files, image_search)

### Step 7: Implement ISkillService (AgentSkillService) + ISkillLoader (StructuredSkillLoader)

- **New files:**
  - `src/MySecondBrain.Services/Skills/AgentSkillService.cs`
  - `src/MySecondBrain.Services/Skills/StructuredSkillLoader.cs`
- **Existing file to modify:**
  - [`MySecondBrain.UI.csproj`](../../src/MySecondBrain.UI/MySecondBrain.UI.csproj): Add `<EmbeddedResource Include="Skills\anthropic\**\*" />`
- **Skills integration spec:** [`skills-integration.md`](../agent-workspace/project-director/planning/skills-integration.md) — §4 (activation), §7 (discovery), §8 (system prompt), §10 (platform adaptation)
- **Discovery paths:**
  1. Embedded: `Skills/anthropic/` in `MySecondBrain.UI.dll`
  2. User: `%LOCALAPPDATA%/MySecondBrain/skills/`
  3. Cross-client: `%USERPROFILE%/.agents/skills/`
  4. Cross-client: `%USERPROFILE%/.claude/skills/`
- **YAML parsing:** Simple regex-based extraction of `name:` and `description:` from frontmatter between `---` markers. Full YAML library not needed for two-field extraction.
- **XML wrapping format:**
  ```xml
  <skill_content name="xlsx">
  [SKILL.md body, YAML stripped]
  <skill_resources>
    <file>scripts/recalc.py</file>
  </skill_resources>
  </skill_content>
  ```
- **Integration test coverage:** Tests in `ProviderIntegrationTests.cs` verify actual filesystem discovery with temp directories, YAML frontmatter parsing from real files, and collision resolution with user/cross-client overrides on disk.

### Step 8: Add CitationRenderer (Priority 350) and Register in DI

- **New file:**
  - `src/MySecondBrain.UI/Controls/CitationRenderer.cs`
- **Existing file to modify:**
  - [`DependencyInjectionConfig.cs`](../../src/MySecondBrain.UI/DependencyInjectionConfig.cs): Add `services.AddSingleton<IContentBlockRenderer, CitationRenderer>()` at priority 350
- **Spec from abstractions.md §8:**
  - Priority: 350 (between ArtifactReferenceRenderer=300 and ImageRenderer=400)
  - `CanRender()`: Detects Markdig `Footnote` and `FootnoteLink` nodes
  - `RenderAsync()`: Inline `[N]` markers → clickable superscript `Hyperlink`; `[^N]:` footnotes → styled `Paragraph`
  - Click action: `FrameworkElement.BringIntoView()` to footnote anchor
  - Graceful degradation: missing footnote → plain text; missing URL → plain text title
- **Unit test coverage:** Tests in `ToolExecutorTests.cs` cover `CanRender()` detection of Markdig nodes, `RenderAsync()` Hyperlink/Paragraph output, and graceful degradation paths.

### Step 9: Update DI Registrations (10 Tools + Skills)

- **Existing file to modify:**
  - [`DependencyInjectionConfig.cs`](../../src/MySecondBrain.UI/DependencyInjectionConfig.cs)
- **Changes:**
  - Lines 123-127: Replace 5 old `IToolExecutor` registrations with 10 new ones
  - After line 91: Add `services.AddSingleton<ISkillService, AgentSkillService>();` and `services.AddSingleton<ISkillLoader, StructuredSkillLoader>();`
  - Note: `CitationRenderer` was already registered in Step 8 — do not re-add
- **10 IToolExecutor registrations:**
  1. `BashToolExecutor`
  2. `TextEditorToolExecutor`
  3. `WebSearchToolExecutor` (existing, no change)
  4. `WebFetchToolExecutor`
  5. `WikiSearchToolExecutor` (existing, no change)
  6. `MemoryToolExecutor`
  7. `SkillLoadToolExecutor`
  8. `AskUserInputToolExecutor`
  9. `PresentFilesToolExecutor`
  10. `ImageSearchToolExecutor`
- **Existing tests to update:**
  - [`DiContainerViewModelPlatformTests.cs`](../../tests/unit/MySecondBrain.Tests.Unit/DiContainerViewModelPlatformTests.cs): Line 81-82, change `Assert.Equal(5, toolExecutors.Count)` to `Assert.Equal(10, toolExecutors.Count)`

### Step 10: Add WebView2 NuGet + ArtifactsWebView2Host Control

- **NuGet package:** `Microsoft.Web.WebView2.Wpf` (latest stable)
- **New file:**
  - `src/MySecondBrain.UI/Controls/ArtifactsWebView2Host.cs`
- **Existing file to modify:**
  - [`MySecondBrain.UI.csproj`](../../src/MySecondBrain.UI/MySecondBrain.UI.csproj): Add WebView2 package reference
- **Spec from skills-integration.md §10:** WebView2 for artifacts panel. Theme bridge via `CoreWebView2.ExecuteScriptAsync()`. Browser-native rendering: Prism.js (syntax highlighting), marked.js (Markdown), diff2html.js (diffs).
- **Platform notes:** WebView2 runtime pre-installed on Windows 11, auto-installed on Windows 10. Adds ~100MB to install size.

### Step 11: Implement Per-Chat Toolbar Toggles

- **Existing files to modify:**
  - [`ChatView.xaml`](../../src/MySecondBrain.UI/Views/ChatView.xaml)
  - [`ChatView.xaml.cs`](../../src/MySecondBrain.UI/Views/ChatView.xaml.cs)
- **Spec from skills-integration.md §9:**
  - Toolbar: `[Persona ▼] [🧠] [🔇] [🔧 Tools ▼] [📚 Skills ▼] [🧠 Mem] [📎] [📋]`
  - Tools dropdown: checkboxes for each tool
  - Skills dropdown: checkboxes for each skill + "All on/off"
  - Memory toggle: on/off
  - New chats inherit global defaults from Settings → Tools / Skills / Memory

### Step 12: Implement Additive System Prompt Construction

- **Existing files to modify:**
  - The system prompt construction logic (likely in `ChatThreadService` or a system prompt builder)
- **Spec from skills-integration.md §8:**
  - Assembly rules (see plan.md Step 12)
  - Tools array assembly (see plan.md Step 12)
  - Skill catalog XML format:
    ```xml
    <available_skills>
      <skill>
        <name>xlsx</name>
        <description>Create/edit Excel spreadsheets...</description>
      </skill>
    </available_skills>
    ```
  - Platform context includes bash availability detection

### Step 13: Structural Refactoring — Extract SystemPromptCoordinator from ChatThreadViewModel

No specific external reference needed.

### Step 14: Implement Workspace Isolation for Bash Tool

- **Existing file to modify:**
  - [`BashToolExecutor.cs`](../../src/MySecondBrain.Services/Tools/BashToolExecutor.cs) (created in Step 5)
- **Spec from skills-integration.md §10 and platform-notes.md §4:**
  - Workspace path: `%LOCALAPPDATA%/MySecondBrain/workspace/`
  - Path blocking patterns: `C:\`, `%`, `~`
  - Bash detection: `C:\Program Files\Git\bin\bash.exe` and `wsl --status`
  - Shell adaptation: cmd.exe default, .sh scripts → bash.exe → wsl, heredocs → text_editor
  - 24h auto-cleanup on app startup

### Step 15: Update Knowledge Files

- **Existing files to modify:**
  - [`agent-workspace/knowledge/architecture.md`](../../agent-workspace/knowledge/architecture.md)
  - [`agent-workspace/knowledge/database.md`](../../agent-workspace/knowledge/database.md)
  - [`agent-workspace/knowledge/frontend-ui.md`](../../agent-workspace/knowledge/frontend-ui.md)
  - [`agent-workspace/knowledge/api-routes.md`](../../agent-workspace/knowledge/api-routes.md)
- **Classification: HUMAN/SHT REQUIRED** — content verification requires manual review of updated documentation.

### Step 16: Run Full Test Suite & E2E Verification

- **Commands to run:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration`
  - `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug`
- **Expected:** All 70+ E2E tests pass, all unit tests pass, all integration tests pass. Exit code 0 for all suites.
