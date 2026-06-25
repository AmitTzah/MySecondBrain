# Feature Implementation Plan: Codebase Realignment: 14-Tool Surface & Vision Alignment

## 1. Overall Project Context

MySecondBrain is a WPF (.NET 8) desktop application — a "second brain" tool providing an AI-powered chat workspace, personal wiki, artifacts, and tool-use agents. The app uses a provider-agnostic architecture with DI container (76+ registrations, 42+ interfaces), Serilog logging, EF Core SQLite with FTS5, and 15 data entities across 8 repositories. The solution is organized as a 7-project layered architecture: Core (interfaces, DTOs, enums) → Data (EF Core, entities, repositories, migrations) → Services (business logic, LLM adapters, tool executors) → UI (WPF views, ViewModels, controls, themes) → Package (MSIX packaging). All three UI tiers (Tier 1 hotkey overlay, Tier 2 Command Bar, Tier 3 Studio) share a unified ChatThread data model and ChatThreadService singleton.

## 2. Feature-Specific Context

This is a **non-user-facing infrastructure feature** — the app should behave identically after completion, but the internal architecture matches the 2026-06-25 vision update. Feature 10 delivered a 10-tool Anthropic surface with a shared workspace and basic UsageRecord. This feature (F11) bridges the gap to the updated vision: 14 provider-agnostic tools (5 file operations replacing `text_editor` + 4 Anthropic-matched + 5 custom), per-chat workspace/artifacts isolation, enriched UsageRecord (8 new columns: cache tokens, latency, tier, error info, raw JSON path), `TextAction.chatMode`, skill discovery reduced to 2 locations, parallel tool execution in `IToolOrchestrator`, and updated `SystemPromptBuilder`. This realignment MUST happen before Studio Chat (F12) starts making real API calls, since F12's `ILLMProvider` calls must populate the enriched `UsageRecord` fields and F12's chat workspace must be per-chat isolated.

## 3. Architecture and Extensibility

The feature follows established architectural patterns already in the codebase:

**Provider/Adapter Pattern (Tool Executors):** Each of the 14 tools implements [`IToolExecutor`](src/MySecondBrain.Core/Interfaces/IToolExecutor.cs) — a common contract with `ToolName`, `ValidateAsync`, `ExecuteAsync`, and `GetConfirmationDescription`. The 5 new file operation executors (`ReadFileToolExecutor`, `ListFilesToolExecutor`, `SearchFilesToolExecutor`, `ApplyDiffToolExecutor`, `WriteToFileToolExecutor`) replace the single `TextEditorToolExecutor`. Each is independently registered in DI via `services.AddSingleton<IToolExecutor, T>()`. Adding a 15th tool in the future requires only: (a) create executor class implementing `IToolExecutor`, (b) register in DI. Zero project-reference changes needed.

**Repository Pattern (UsageRecord):** The enriched `UsageRecord` entity follows the existing pattern — EF Core entity in `Data/Entities/`, domain model in `Core/Models/DomainModels.cs`, and repository mapping in `Data/Repositories/UsageRepository.cs`. New cache/latency query methods on `IUsageRepository` follow the existing `GetSummaryAsync`/`GetByProviderAsync` pattern but accept optional `provider`, `model`, and `tier` filter parameters. Adding future query dimensions follows the same pattern.

**Per-Chat Isolation Strategy:** `BashToolExecutor` workspace changes from a shared `%LOCALAPPDATA%/MySecondBrain/workspace/` to per-chat `workspace/{chat-id}/`. `PresentFilesToolExecutor` copies from per-chat workspace to per-chat artifacts `artifacts/{chat-id}/`. Both receive `chatId` as a parameter to their execution methods rather than via constructor injection (since tool executors are singletons). The `IToolExecutor` interface is not modified — `chatId` is extracted from the `ToolCall` arguments JSON at execution time (injected by the system caller, not by the LLM). This keeps the interface stable while enabling future isolation strategies.

**EF Core Migration Strategy:** Two new migrations: one for the 8 new `UsageRecord` columns, one for `TextAction.ChatMode`. Both follow the existing migration pattern — column additions only (no breaking schema changes), safe for auto-migration via `db.Database.Migrate()` at startup.

## 4. Final Expected Project Structure

```
src/
├── MySecondBrain.Core/
│   ├── Interfaces/
│   │   ├── IToolExecutor.cs              (unchanged — 14 implementations)
│   │   ├── IToolOrchestrator.cs          (MODIFY — parallel execution docs)
│   │   ├── IUsageRepository.cs           (MODIFY — cache/latency queries)
│   │   └── ... (unchanged)
│   └── Models/
│       ├── DomainModels.cs               (MODIFY — UsageRecord + TextAction + ToolAutoApprovalSettings)
│       ├── Dtos.cs                       (MODIFY — UsageRecord DTO)
│       ├── ServiceDtos.cs                (MODIFY — cache/latency DTOs)
│       └── SkillModels.cs               (unchanged)
│
├── MySecondBrain.Data/
│   ├── Entities/
│   │   ├── UsageRecord.cs                (MODIFY — 8 new columns)
│   │   ├── TextAction.cs                 (MODIFY — ChatMode column)
│   │   └── ... (unchanged)
│   ├── Repositories/
│   │   ├── UsageRepository.cs            (MODIFY — mapping + new queries)
│   │   └── ... (unchanged)
│   ├── Migrations/
│   │   ├── 20260625_EnrichUsageRecord.cs       (NEW)
│   │   ├── 20260625_AddTextActionChatMode.cs   (NEW)
│   │   └── ... (existing)
│   └── AppDbContext.cs                   (MODIFY — UsageRecord config)
│
├── MySecondBrain.Services/
│   ├── Tools/
│   │   ├── TextEditorToolExecutor.cs     (DELETE)
│   │   ├── ReadFileToolExecutor.cs       (NEW)
│   │   ├── ListFilesToolExecutor.cs      (NEW)
│   │   ├── SearchFilesToolExecutor.cs    (NEW)
│   │   ├── ApplyDiffToolExecutor.cs      (NEW)
│   │   ├── WriteToFileToolExecutor.cs    (NEW)
│   │   ├── BashToolExecutor.cs           (MODIFY — per-chat workspace)
│   │   ├── PresentFilesToolExecutor.cs   (MODIFY — per-chat artifacts)
│   │   ├── ToolOrchestrator.cs           (MODIFY — parallel execution)
│   │   └── ... (unchanged)
│   ├── Skills/
│   │   └── AgentSkillService.cs          (MODIFY — 2 locations)
│   └── SystemPromptBuilder.cs            (MODIFY — 14 tools)
│
├── MySecondBrain.UI/
│   └── DependencyInjectionConfig.cs      (MODIFY — 5 new + 1 removed tool)
│
└── MySecondBrain.Package/               (unchanged)
```

---

## 5. Execution Steps

### [x] Step 1: Delete TextEditorToolExecutor + Create 5 File Operation Executors
- **Goal:** Replace the single `text_editor` tool (Anthropic `text_editor_20250728`) with 5 provider-agnostic file operation tools: `read_file`, `list_files`, `search_files`, `apply_diff`, `write_to_file`. The app behaves identically — tool executors are stubs that return empty results — but the 14-tool surface is architecturally in place.
- **Actions:**
  - DELETE [`src/MySecondBrain.Services/Tools/TextEditorToolExecutor.cs`](src/MySecondBrain.Services/Tools/TextEditorToolExecutor.cs)
  - CREATE [`src/MySecondBrain.Services/Tools/ReadFileToolExecutor.cs`](src/MySecondBrain.Services/Tools/ReadFileToolExecutor.cs) — implements `IToolExecutor` with `ToolName = "read_file"`, `RiskLevel = Low`, `CanAutoApprove = true`, JSON schema per [abstractions.md](agent-workspace/project-director/planning/abstractions.md) (path, offset, limit). `ExecuteAsync` returns stub → "Not yet implemented — Feature 17".
  - CREATE [`src/MySecondBrain.Services/Tools/ListFilesToolExecutor.cs`](src/MySecondBrain.Services/Tools/ListFilesToolExecutor.cs) — `ToolName = "list_files"`, `RiskLevel = Low`, JSON schema (path, recursive). Stub.
  - CREATE [`src/MySecondBrain.Services/Tools/SearchFilesToolExecutor.cs`](src/MySecondBrain.Services/Tools/SearchFilesToolExecutor.cs) — `ToolName = "search_files"`, `RiskLevel = Low`, JSON schema (path, regex, file_pattern). Stub.
  - CREATE [`src/MySecondBrain.Services/Tools/ApplyDiffToolExecutor.cs`](src/MySecondBrain.Services/Tools/ApplyDiffToolExecutor.cs) — `ToolName = "apply_diff"`, `RiskLevel = Medium`, `CanAutoApprove = false`, JSON schema (path, diff). Stub.
  - CREATE [`src/MySecondBrain.Services/Tools/WriteToFileToolExecutor.cs`](src/MySecondBrain.Services/Tools/WriteToFileToolExecutor.cs) — `ToolName = "write_to_file"`, `RiskLevel = Medium`, `CanAutoApprove = false`, JSON schema (path, content, overwrite). Stub.
  - MODIFY [`src/MySecondBrain.UI/DependencyInjectionConfig.cs`](src/MySecondBrain.UI/DependencyInjectionConfig.cs) — remove `TextEditorToolExecutor` registration (line 129), add 5 new `IToolExecutor` registrations.
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/DiContainerRepositoryServiceTests.cs` — extend `DiContainer_ShouldResolveAllToolExecutors` to verify 14 executors (was 10), verify `TextEditorToolExecutor` is absent.
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `ToolExecutorCount_ShouldBe14` test.
- **Integration Tests to Write:** None — tool executors are stubs; integration tests are meaningless until Feature 17 implements them.
- **Live Smoke Test (Mandatory):** `dotnet build MySecondBrain.sln --configuration Debug` — must succeed with 0 errors. Then run the full unit test suite: `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests must pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: replace text_editor with 5 provider-agnostic file operation tool executors`

---

### [x] Step 2: Enrich UsageRecord Entity with 8 New Columns + Migration + Repository Extension
- **Goal:** Add 8 provider-agnostic fields to `UsageRecord` matching the vision data spec: `cacheReadTokens`, `cacheCreationTokens`, `latencyMs`, `tier`, `errorType`, `errorMessage`, `errorStatusCode`, `rawJsonPath`. Extend `IUsageRepository` with cache/latency query methods. The entity is ready for F12's real API calls.
- **Actions:**
  - MODIFY [`src/MySecondBrain.Data/Entities/UsageRecord.cs`](src/MySecondBrain.Data/Entities/UsageRecord.cs) — add 8 properties:
    - `CacheReadTokens` (int, default 0)
    - `CacheCreationTokens` (int, default 0)
    - `LatencyMs` (int, default 0)
    - `Tier` (int, default 3)
    - `ErrorType` (string?, MaxLength 50)
    - `ErrorMessage` (string?)
    - `ErrorStatusCode` (int?)
    - `RawJsonPath` (string?)
  - MODIFY [`src/MySecondBrain.Data/AppDbContext.cs`](src/MySecondBrain.Data/AppDbContext.cs) — add `[MaxLength]` constraints in `OnModelCreating` for new string columns.
  - CREATE EF Core migration via `dotnet ef migrations add EnrichUsageRecord --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI`
  - MODIFY [`src/MySecondBrain.Core/Models/DomainModels.cs`](src/MySecondBrain.Core/Models/DomainModels.cs) — add 8 fields to `UsageRecord` domain model class.
  - MODIFY [`src/MySecondBrain.Data/Repositories/UsageRepository.cs`](src/MySecondBrain.Data/Repositories/UsageRepository.cs) — update `MapToDomain`/`MapToEntity` for 8 new fields.
  - MODIFY [`src/MySecondBrain.Core/Interfaces/IUsageRepository.cs`](src/MySecondBrain.Core/Interfaces/IUsageRepository.cs) — add 2 methods:
    - `Task<CacheSummary> GetCacheSummaryAsync(DateTimeOffset from, DateTimeOffset to, string? provider, string? model)`
    - `Task<LatencyDistribution> GetLatencyDistributionAsync(DateTimeOffset from, DateTimeOffset to, string? provider, string? model)`
  - MODIFY [`src/MySecondBrain.Core/Models/ServiceDtos.cs`](src/MySecondBrain.Core/Models/ServiceDtos.cs) — add `CacheSummary` and `LatencyDistribution` DTO records.
  - MODIFY existing `GetUsageAsync`, `GetSummaryAsync`, `GetByProviderAsync`, `GetByModelAsync`, `GetByChatAsync` to accept optional `provider`, `model`, `tier` filter parameters.
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `UsageRecord_ShouldHave8NewColumns` test that verifies all 8 properties exist and have correct types.
  - `tests/unit/MySecondBrain.Tests.Unit/DataLayerTestBase.cs` — extend in-memory DB setup to verify new columns persist/retrieve correctly.
- **Integration Tests to Write:**
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs` — add `EnrichUsageRecord_Migration_ShouldApplyToRealSqlite` test that verifies the migration creates all 8 new columns in a real SQLite database and that default values are correct.
- **Live Smoke Test (Mandatory):** `dotnet ef migrations list --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI` — verify `EnrichUsageRecord` migration appears. `dotnet build MySecondBrain.sln --configuration Debug` — succeeds. `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests pass. `dotnet test tests/integration/MySecondBrain.Tests.Integration --configuration Debug` — all tests pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: enrich UsageRecord with cache tokens, latency, tier, error fields, and raw JSON path`

---

### [x] Step 3: Add chatMode to TextAction Entity + Migration
- **Goal:** Add `ChatMode` field to the `TextAction` entity matching the vision spec — supports `Standard` (chat API with system prompt) and `TextCompletion` (raw prompt to raw completion).
- **Actions:**
  - MODIFY [`src/MySecondBrain.Data/Entities/TextAction.cs`](src/MySecondBrain.Data/Entities/TextAction.cs) — add property:
    - `ChatMode` (string, MaxLength 20, default "Standard")
  - CREATE EF Core migration via `dotnet ef migrations add AddTextActionChatMode --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI`
  - MODIFY [`src/MySecondBrain.Core/Models/DomainModels.cs`](src/MySecondBrain.Core/Models/DomainModels.cs) — add `ChatMode` field to `TextAction` domain model (if it exists there; if using entity directly, no change needed).
  - MODIFY seed data in [`src/MySecondBrain.Data/AppDbContext.cs`](src/MySecondBrain.Data/AppDbContext.cs) — set `ChatMode = "Standard"` for 8 built-in TextActions, `ChatMode = "TextCompletion"` for "Continue Writing".
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `TextAction_ShouldHaveChatModeField` test.
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `TextAction_ContinueWriting_ShouldDefaultToTextCompletion` verifying seed data.
- **Integration Tests to Write:**
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs` — add `TextActionChatMode_Migration_ShouldApplyToRealSqlite` test verifying the column exists, default value is "Standard", and seed data correctly sets "Continue Writing" to "TextCompletion".
- **Live Smoke Test (Mandatory):** `dotnet ef migrations list --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI` — verify both migrations. `dotnet build MySecondBrain.sln --configuration Debug` — succeeds. `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests pass. `dotnet test tests/integration/MySecondBrain.Tests.Integration --configuration Debug` — all tests pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: add ChatMode field to TextAction entity with Standard/TextCompletion support`

---

### [x] Step 4: Reduce Skill Discovery to 2 Locations in AgentSkillService
- **Goal:** Remove cross-client path scanning (`.agents/`, `.claude/`) from skill discovery. Skills are now discovered from only 2 locations: embedded resources (`Skills/anthropic/` in the DLL) and `%LOCALAPPDATA%/MySecondBrain/skills/`. The `skill_load` tool enum and skill catalog are regenerated from these 2 sources only.
- **Actions:**
  - MODIFY [`src/MySecondBrain.Services/Skills/AgentSkillService.cs`](src/MySecondBrain.Services/Skills/AgentSkillService.cs) — in the `DiscoverAsync` method:
    - Remove scanning of `.agents/` and `.claude/` directories (cross-client paths).
    - Keep embedded resource scanning (`Assembly.GetManifestResourceStream`).
    - Keep user directory scanning (`%LOCALAPPDATA%/MySecondBrain/skills/`).
    - Update documentation comments to reflect 2 locations.
  - MODIFY if any constants/configuration references cross-client paths — remove them.
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `SkillDiscovery_ShouldOnlyScanTwoLocations` test using temporary directory mocking to verify only embedded and user skills directories are scanned.
- **Integration Tests to Write:** None — skill discovery is unit-testable with embedded resource mocks; no external infrastructure dependencies.
- **Live Smoke Test (Mandatory):** `dotnet build MySecondBrain.sln --configuration Debug` — succeeds. `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: reduce skill discovery to 2 locations — embedded and user skills directory only`

---

### [x] Step 5: Per-Chat Workspace Isolation (BashToolExecutor) + Per-Chat Artifacts (PresentFilesToolExecutor)
- **Goal:** Change `BashToolExecutor` from a shared workspace to per-chat isolation (`workspace/{chat-id}/`). Change `PresentFilesToolExecutor` to copy files from per-chat workspace to per-chat artifacts (`artifacts/{chat-id}/`). Both are infrastructure changes — stubs that establish the correct isolation architecture.
- **Actions:**
  - MODIFY [`src/MySecondBrain.Services/Tools/BashToolExecutor.cs`](src/MySecondBrain.Services/Tools/BashToolExecutor.cs):
    - Change `WorkspacePath` from shared `%LOCALAPPDATA%/MySecondBrain/workspace/` to a base path constant; rename to `WorkspaceBasePath`.
    - Add `GetChatWorkspacePath(string chatId)` static method: `Path.Combine(WorkspaceBasePath, chatId)`.
    - Add `private static string ExtractChatId(ToolCall toolCall)` helper — parses `chat_id` from `toolCall.Arguments` JSON (system-injected, not LLM-provided).
    - Update `ExecuteAsync` to extract `chat_id` from `ToolCall` parameters via `ExtractChatId` and derive workspace path.
    - Update startup cleanup to handle per-chat directories.
    - Update description string to mention per-chat workspace.
  - MODIFY [`src/MySecondBrain.Services/Tools/PresentFilesToolExecutor.cs`](src/MySecondBrain.Services/Tools/PresentFilesToolExecutor.cs):
    - Add `GetChatArtifactsPath(string chatId)` static method: `Path.Combine(artifactsBase, chatId)`.
    - Add `private static string ExtractChatId(ToolCall toolCall)` helper — same pattern as BashToolExecutor.
    - Update `ExecuteAsync` stub to accept `chat_id` and produce correct destination path in result.
    - Add `ArtifactsBasePath` constant: `%LOCALAPPDATA%/MySecondBrain/artifacts/`.
    - Update JSON schema to include optional `chat_id` parameter.
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `BashToolExecutor_ShouldDerivePerChatWorkspace` test verifying `GetChatWorkspacePath` produces `.../workspace/{chat-id}`.
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `PresentFiles_ShouldDerivePerChatArtifactsPath` test.
- **Integration Tests to Write:** None — stubs; real file system isolation integration tests are meaningful only when Feature 17 implements actual execution.
- **Live Smoke Test (Mandatory):** `dotnet build MySecondBrain.sln --configuration Debug` — succeeds. `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: per-chat workspace isolation for bash and present_files tool executors`

---

### [ ] Step 6: Update ToolOrchestrator for 14-Tool Registration + Parallel Execution Architecture
- **Goal:** Update `IToolOrchestrator` and `ToolOrchestrator` to reflect the 14-tool surface and establish the parallel execution architecture (`Task.WhenAll`, max 10 concurrent). The implementation remains a stub (tools return empty results) but the architecture is correct for Feature 17.
- **Actions:**
  - MODIFY [`src/MySecondBrain.Core/Interfaces/IToolOrchestrator.cs`](src/MySecondBrain.Core/Interfaces/IToolOrchestrator.cs) — update XML doc comments to document parallel execution model (Task.WhenAll, max 10 concurrent), independent tool detection, dependency sequentialization.
  - MODIFY [`src/MySecondBrain.Services/Tools/ToolOrchestrator.cs`](src/MySecondBrain.Services/Tools/ToolOrchestrator.cs):
    - Update `GetAvailableToolDefinitions()` — returns all 14 tool definitions from registered `IToolExecutor` instances.
    - Update `IsToolEnabled(string toolName)` — checks against a configurable enabled set (stub: all enabled).
    - Update `GetAutoApprovalSettings()` — replace `AutoApproveTextEditor` with file-op-specific settings (e.g., `AutoApproveReadFile`, `AutoApproveListFiles`, `AutoApproveSearchFiles` for reads; writes remain restricted).
    - Update `ProcessToolCallsAsync()` — add parallel execution scaffolding:
      - Group independent tools for concurrent execution (`Task.WhenAll`).
      - Max 10 concurrent; queue overflow.
      - Each tool execution wrapped in try/catch (one failure doesn't block others).
      - Return aggregated `ToolResult` list.
    - Add `private bool AreIndependent(ToolCall a, ToolCall b)` helper method (stub: all independent).
  - MODIFY [`src/MySecondBrain.Core/Models/DomainModels.cs`](src/MySecondBrain.Core/Models/DomainModels.cs) — update `ToolAutoApprovalSettings` class: remove `AutoApproveTextEditor`, add file-op-specific booleans (`AutoApproveReadFile`, `AutoApproveListFiles`, `AutoApproveSearchFiles`, `AutoApproveApplyDiff`, `AutoApproveWriteToFile`).
  - MODIFY [`src/MySecondBrain.UI/DependencyInjectionConfig.cs`](src/MySecondBrain.UI/DependencyInjectionConfig.cs) — verify all 14 `IToolExecutor` registrations are present after Step 1's changes.
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/DiContainerRepositoryServiceTests.cs` — extend existing DI resolution test to verify `IToolOrchestrator.GetAvailableToolDefinitions()` returns 14 definitions.
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `ToolOrchestrator_ShouldSupportParallelExecution` test verifying `ProcessToolCallsAsync` handles multiple independent calls.
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `ToolOrchestrator_AutoApprovalSettings_ShouldCategorizeFileOps` test.
- **Integration Tests to Write:** None — stubs.
- **Live Smoke Test (Mandatory):** `dotnet build MySecondBrain.sln --configuration Debug` — succeeds. `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: update ToolOrchestrator for 14-tool surface with parallel execution architecture`

---

### [ ] Step 7: Update SystemPromptBuilder for 14-Tool Surface
- **Goal:** Update the static `SystemPromptBuilder` behavioral instructions to reference all 14 tools (including the 5 new file operation tools and `image_search`), update the `BuildFilteredToolNames` to recognize the new tool names, and ensure parallel execution is mentioned in the behavioral template.
- **Actions:**
  - MODIFY [`src/MySecondBrain.Services/SystemPromptBuilder.cs`](src/MySecondBrain.Services/SystemPromptBuilder.cs):
    - Update `BehavioralInstructions` constant — change from "editing files" to "reading, listing, searching, editing, and creating files" to reflect 5 file op tools.
    - Update `BehavioralInstructions` constant — change "Execute one tool at a time unless tools are independent (then call them in parallel)" to mention "Independent tools execute in parallel via Task.WhenAll (max 10 concurrent)."
    - Update `BehavioralInstructions` constant — add explicit mention of read tool approval model: "Read tools (read_file, list_files, search_files) are auto-approved within the workspace and artifacts directories. Out-of-workspace reads trigger the approval gate."
    - Update `BuildFilteredToolNames` — ensure all 14 tool names are recognized in the tool filter map.
    - Update `BuildSystemPrompt` — no signature change needed; the tool filtering already works via `enabledToolNames`.
  - Verify [`src/MySecondBrain.UI/ViewModels/SystemPromptCoordinator.cs`](src/MySecondBrain.UI/ViewModels/SystemPromptCoordinator.cs) delegates correctly — no changes expected if `SystemPromptBuilder` API is stable.
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `SystemPromptBuilder_ShouldMentionAll14Tools` test that verifies the behavioral instructions template contains references to `read_file`, `list_files`, `search_files`, `apply_diff`, `write_to_file`, `image_search`.
  - `tests/unit/MySecondBrain.Tests.Unit/EntitySchemaTests.cs` — add `SystemPromptBuilder_ShouldFilterAll14ToolNames` test.
- **Integration Tests to Write:** None — pure string manipulation; no infrastructure dependencies.
- **Live Smoke Test (Mandatory):** `dotnet build MySecondBrain.sln --configuration Debug` — succeeds. `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug` — all tests pass.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: update SystemPromptBuilder behavioral instructions for 14-tool surface`

---

## 6. Shared Technical Context

- **Project Test Commands:**
  - Unit tests: `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug`
  - Integration tests: `dotnet test tests/integration/MySecondBrain.Tests.Integration --configuration Debug`
  - E2E tests: `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug` (or `.\tests\e2e\run-e2e-tests.ps1`)
  - EF migrations: `dotnet ef migrations add <Name> --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI`
  - Solution build: `dotnet build MySecondBrain.sln --configuration Debug`

- **Key Dependencies Between Steps:**
  - Step 1 must complete first (tool surface structural change).
  - Step 2 and 3 are independent of each other and can run in parallel after Step 1.
  - Step 4 is independent of all other steps (can run any time after Step 1).
  - Step 5 is independent but logically follows Steps 1-3.
  - Step 6 depends on Step 1 (14 tool executors registered).
  - Step 7 depends on Steps 1 and 6 (tool names and orchestration model).

- **Data Model Version:** After all steps, 2 new EF Core migrations exist: `EnrichUsageRecord` (8 columns) and `AddTextActionChatMode` (1 column). Both are additive-only — no data migration needed.

- **Smoke Test Automation:** All smoke tests are Model-classified (build + full unit test suite). No manual or HUMAN/SHT REQUIRED steps. The `state.json` has `smoke_test_automation.enabled = false` which is correct for this infrastructure-only feature.

- **14 Tool Executors Registered After Step 1:** `ApplyDiffToolExecutor`, `AskUserInputToolExecutor`, `BashToolExecutor`, `ImageSearchToolExecutor`, `ListFilesToolExecutor`, `MemoryToolExecutor`, `PresentFilesToolExecutor`, `ReadFileToolExecutor`, `SearchFilesToolExecutor`, `SkillLoadToolExecutor`, `WebFetchToolExecutor`, `WebSearchToolExecutor`, `WikiSearchToolExecutor`, `WriteToFileToolExecutor`. DI container has 14 `IToolExecutor` singleton registrations.

- [Initial State]: No shared context yet.
