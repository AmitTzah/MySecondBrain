# Roadmap — MySecondBrain Wave Map

Lightweight feature decomposition. Features are grouped into 4 architectural waves. Each Wave 3 feature is a vertical slice (DB → service → UI) that **adds** new code to pre-built Wave 1-2 infrastructure — Wave 3 features do NOT modify Wave 1-2 code.

Source documents:
- Vision: [`vision/vision-summary.md`](vision/vision-summary.md), [`vision/feature-inventory.md`](vision/feature-inventory.md)
- Planning: [`planning/architecture.md`](planning/architecture.md), [`planning/abstractions.md`](planning/abstractions.md), [`planning/data-model.md`](planning/data-model.md), [`planning/tech-stack.md`](planning/tech-stack.md), [`planning/platform-notes.md`](planning/platform-notes.md)
- Tech sourcing: [`tech-sourcing.md`](tech-sourcing.md)
- External reference docs: `agent-workspace/external-docs/ref-*.md`

---

## Wave 1: Foundation — Features 1–40

Infrastructure that everything else depends on. Core abstractions, data model, project scaffolding.

### Project Scaffolding & Build Pipeline

- **Feature 1:** .NET 8.0 WPF Solution Scaffold — Create solution structure with all projects (core library, WPF app, test projects), NuGet package references for all 15 OSS libraries, MSIX packaging project, and CI/CD build pipeline. Depends on: none.
- **Feature 2:** Dependency Injection Container — Wire `Microsoft.Extensions.DependencyInjection` with all service, repository, and ViewModel registrations per lifetimes defined in [`planning/platform-notes.md`](planning/platform-notes.md). Includes singleton `AppDbContext`, service singletons, ViewModel transient registrations, and content block renderer registry. Depends on: none.
- **Feature 3:** Logging Infrastructure — Serilog or `Microsoft.Extensions.Logging` sink to rolling file in `%LOCALAPPDATA%\MySecondBrain\logs\`. Structured logging for all service calls, API errors, and background tasks. Depends on: Feature 2.

### Data Layer — All 13 Entities

- **Feature 4:** EF Core DbContext & SQLite Connection — `AppDbContext` with `Microsoft.Data.Sqlite` connection string pointing to `%LOCALAPPDATA%\MySecondBrain\msb.db`. Configured for singleton lifetime with `SemaphoreSlim` write serialization. SQLite FTS5 extension enabled. Depends on: Feature 2.
- **Feature 5:** ApiKey Entity & Repository — Uses `ApiKey` entity. Implements `IApiKeyRepository` with CRUD. DPAPI encryption/decryption at service layer (not repository). Depends on: Feature 4.
- **Feature 6:** ModelConfiguration Entity & Repository — Uses `ModelConfiguration` entity. Implements `IModelConfigurationRepository` with CRUD. FK to ApiKey. Context overflow strategy enum. Pricing fields. Depends on: Feature 4, Feature 5.
- **Feature 7:** Persona Entity & Repository — Uses `Persona` entity. Implements `IPersonaRepository` with CRUD. FK to ModelConfiguration. Built-in default personas (General Assistant). System prompt with `{{variable}}` support. Depends on: Feature 4, Feature 6.
- **Feature 8:** ChatThread Entity & Repository — Uses `ChatThread` entity. Implements `IChatThreadRepository` with full CRUD, transient/permanent queries, trash view, and soft-delete. Source context fields (HWND, app name, doc title). Organization fields (tags, folder, color label, pinned, archived). Depends on: Feature 4, Feature 7.
- **Feature 9:** Message Entity & Repository — Uses `Message` entity. Implements `IMessageRepository` with branching queries (recursive CTE for active branch resolution), branch navigation, and FTS5 search. Version-chain model: `branchId`, `versionNumber`, `isActiveBranch`, `parentMessageId`. Depends on: Feature 4, Feature 8.
- **Feature 10:** Artifact Entity & Repository — Uses `Artifact` entity with embedded versions. FK to ChatThread. Version history sub-entity. Depends on: Feature 4, Feature 8.
- **Feature 11:** MediaItem Entity & Repository — Uses `MediaItem` entity. FK to ChatThread and optional FK to Message. Source tracking (UserUpload, AIGenerated, WebcamCapture, Screenshot). Saved-to-disk/wiki flags. Depends on: Feature 4, Feature 8.
- **Feature 12:** PromptTemplate Entity & Repository — Uses `PromptTemplate` entity. Independent entity (no FKs). `{{variable}}` placeholder support: `{{clipboard}}`, `{{selected_text}}`, `{{date}}`, `{{current_wiki_file}}`. Tags and folder organization. Depends on: Feature 4.
- **Feature 13:** TextAction Entity & Repository — Uses `TextAction` entity. FK to ModelConfiguration. Hotkey assignment. Built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt. Depends on: Feature 4, Feature 6.
- **Feature 14:** UsageRecord Entity & Repository — Uses `UsageRecord` entity. Implements `IUsageRepository` with aggregation queries (by time range, provider, model, chat, persona). Append-only (immutable). Denormalized `threadId` and `personaId` for dashboard query performance. Depends on: Feature 4, Feature 8, Feature 9.
- **Feature 15:** WikiFile Index Entity & Repository — Uses `WikiFile` entity (index, not source of truth — `.md` files on disk are authoritative). Implements `IWikiIndexRepository` with search (FTS5), backlinks query, related sections, orphans detection, and cross-links management. Depends on: Feature 4.
- **Feature 16:** WikiVersionSnapshot Entity & Repository — Uses `WikiVersionSnapshot` entity. FK to WikiFile. Retention rules: max 30 per file, 50MB total cap. Oldest auto-pruned. Depends on: Feature 4, Feature 15.
- **Feature 17:** BackupSnapshot Entity & Repository — Uses `BackupSnapshot` entity. Standalone (no FKs). Tracks backup metadata (GCS path, size, type, status). Depends on: Feature 4.

### Entity Framework Migrations

- **Feature 18:** Initial Database Migration — EF Core migration creating all 13 entity tables with FKs, indexes, and FTS5 virtual tables. Includes seed data for built-in Personas and TextActions. Depends on: Features 5–17.
- **Feature 19:** Migration Strategy for Auto-Updates — EF Core migration pipeline integrated with auto-update. Schema version check on startup. Automatic migration application on version bump. Depends on: Feature 18.

### Core Abstraction Interfaces

- **Feature 20:** ILLMProvider & Provider Factory — Core LLM abstraction interface with `ChatAsync()`, `ChatStreamAsync()`, `ListModelsAsync()`, `ValidateKeyAsync()`. `ILLMProviderFactory` resolves provider at runtime from `ModelConfiguration`. Normalized `StreamChunk` DTO (content delta, tool calls, thinking tokens, finish reason, usage). Depends on: none. Reference pattern: [`external-docs/ref-cherry-studio-llm-providers.md`](../external-docs/ref-cherry-studio-llm-providers.md).
- **Feature 21:** OpenAIProvider Implementation — Wraps official `OpenAI` NuGet SDK. Covers OpenAI, DeepSeek, Mistral, and any OpenAI-compatible endpoint. Implements full `StreamChunk` normalization for SSE streaming, tool-call deltas, and thinking tokens. Depends on: Feature 20.
- **Feature 22:** AnthropicProvider Implementation — Wraps community `Anthropic.SDK`. Normalizes Anthropic Messages API to common `StreamChunk`. Handles thinking/reasoning token extraction and tool-use blocks. Depends on: Feature 20.
- **Feature 23:** GoogleProvider Implementation — Wraps `Google.Cloud.AIPlatform.V1` SDK. Normalizes Gemini streaming to common `StreamChunk`. Depends on: Feature 20.
- **Feature 24:** OpenAICompatibleProvider Implementation — Generic adapter using `HttpClient` + `System.Text.Json` for any OpenAI-API-compatible endpoint (local Ollama, LM Studio, custom endpoints). User-configurable endpoint URL and optional API key. Depends on: Feature 20.
- **Feature 25:** ITokenizer & TokenizerFactory — Abstraction for per-model token counting. `SharpTokenTokenizer` (OpenAI models), `AnthropicTokenizer`, `FallbackTokenizer` (chars/4). `ITokenizerFactory` resolves correct tokenizer by model ID and provider. Depends on: none.
- **Feature 26:** ISTTProvider — Speech-to-text abstraction: `TranscribeAsync()`, `TranscribeStreamAsync()`, `IsAvailableAsync()`. Implementations: `OpenAIWhisperProvider` (cloud), `LocalWhisperProvider` (Whisper.net, on-device), `WindowsSpeechProvider` (built-in, zero-cost). Depends on: none.
- **Feature 27:** IBackupProvider — Backup abstraction: `UploadAsync()`, `ListBackupsAsync()`, `DownloadAsync()`, `DeleteAsync()`, `ValidateCredentialsAsync()`. Implementations: `GcsBackupProvider` (Google Cloud Storage), `LocalFolderBackupProvider` (local folder). Depends on: none.
- **Feature 28:** ISearchProvider — Web search abstraction: `SearchAsync()`, `IsAvailableAsync()`. Implementations: `GoogleCustomSearchProvider`, `BingSearchProvider`. Returns structured `SearchResults` with title, URL, snippet. Depends on: none.
- **Feature 29:** IToolExecutor & IToolOrchestrator — Tool execution abstraction: `ValidateAsync()`, `ExecuteAsync()`, `GetConfirmationDescription()`. Five executors: `WebSearchToolExecutor`, `TerminalToolExecutor`, `FileGenerateToolExecutor`, `FileEditToolExecutor`, `WikiSearchToolExecutor`. `IToolOrchestrator` manages function-calling loop: validate → confirm → execute → feed back. Depends on: Feature 28, Feature 20.
- **Feature 30:** IChatImporter — Chat import abstraction: `ImportAsync()`, `ValidateAsync()`. Implementations: `ChatGPTImporter` (ChatGPT export JSON), `ClaudeImporter` (Claude export JSON). Parses to `ChatThread` + `Message` model with duplicate detection. Depends on: Feature 8, Feature 9.
- **Feature 31:** IContentBlockRenderer & Registry — Plugin/registry pattern for heterogeneous chat message rendering. `IContentBlockRenderer` with `CanRender()` and `RenderAsync()`. `IContentRendererRegistry` resolves renderer by Markdig AST node. Seven renderers defined but not implemented: `MarkdownTextRenderer`, `CodeBlockRenderer`, `ArtifactReferenceRenderer`, `ImageRenderer`, `MediaRenderer`, `ThinkingRenderer`, `ToolCallRenderer`. Depends on: none.
- **Feature 32:** IThemeProvider — Theme abstraction: `AppTheme` (Dark/Light), `ChatTheme` (Classic/Compact/Bubble). Font settings (family, size 10-24px, weight). `ResourceDictionary` swap for instant theme toggle without restart. `DynamicResource` references throughout XAML. Depends on: none.
- **Feature 33:** IUpdateChecker — Auto-update abstraction: `CheckForUpdatesAsync()`, `DownloadUpdateAsync()`, `InstallAsync()`. Implementation: `AutoUpdaterDotNet` wrapping `AutoUpdater.NET`. Checks remote JSON/XML feed, downloads MSIX, triggers install. Depends on: none.

### Encryption (moved before services that consume it)

- **Feature 34:** Encryption Services — `IEncryptionService` wrapping DPAPI `ProtectedData` for API keys, GCS credentials, GitHub PAT. `IChatEncryptionService` wrapping `AesGcm` (.NET 8+) with PBKDF2 key derivation for locked chats. Depends on: none.

### Repository Interfaces

- **Feature 35:** Repository Interfaces (all 8) — All repository interfaces defined in [`planning/abstractions.md`](planning/abstractions.md): `IChatThreadRepository`, `IMessageRepository`, `IPersonaRepository`, `IModelConfigurationRepository`, `IApiKeyRepository`, `IWikiIndexRepository`, `IUsageRepository`, `ISettingsRepository`. Depends on: Features 5–17 (corresponding entities).

### Core Service Interfaces

- **Feature 36:** ILLMProviderService — High-level LLM wrapper: builds conversation context, calls provider via `ILLMProviderFactory`, handles context overflow strategies (SlidingWindow/HardStop/AutoSummarize), real-time token counting. Depends on: Feature 20, Feature 25.
- **Feature 37:** IChatThreadService — Central chat service: CRUD for threads and messages, branching operations, transient/permanent lifecycle, elevation, auto-save drafts, search. Consumed by all three UI tiers. Depends on: Feature 35, Feature 36.
- **Feature 38:** IWikiService — Wiki service: indexing (Markdig AST walk), search (FTS5), file tree, backlinks/related sections, Write to Wiki pipeline (N5), versioning (snapshots), auto-generated index.md, git integration. Depends on: Feature 15, Feature 16, Feature 35.

### Testing & DevOps (Wave 1)

- **Feature 39:** Unit Test Project Setup — xUnit or NUnit test project with in-memory SQLite for repository tests. Moq or NSubstitute for mocking. Test coverage reporting via Coverlet. CI step in build pipeline. Depends on: Feature 1.
- **Feature 40:** EF Core Integration Tests — Tests for all 13 repository implementations using physical SQLite (not in-memory) to validate FTS5 queries, recursive CTEs, and cascading deletes. Depends on: Features 5–17.

---

## Wave 2: Skeleton — Features 41–61

App shell, navigation, and empty screens. All 8 screens exist as navigable shells with placeholder content.

### App Shell & Navigation

- **Feature 41:** MainWindow Shell — WPF `Window` with three-region layout: collapsible left sidebar, central content area (tabbed), resizable right panel (Artifacts top + Chat Nav bottom). `GridSplitter`s between regions. Minimum widths enforced (sidebar 150px, right panel 200px). Depends on: Feature 2.
- **Feature 42:** Screen Routing & Navigation — Navigation service mapping screen identifiers to ViewModels/Views. Tab management: open, close, reorder by drag-drop, Ctrl+Tab/Shift+Tab cycling, Ctrl+W close, Ctrl+Shift+T reopen. Tab state persistence. Depends on: Feature 41.
- **Feature 43:** Sidebar Shell — Collapsible left sidebar with chat list (sorted, grouped by date), Trash view toggle, Timeline tab (transient actions), and search entry point. Empty states per [`vision/edge-cases.md`](vision/edge-cases.md). Depends on: Feature 41.

### Empty Screen Shells (8 Screens)

- **Feature 44:** Studio Chat Screen (empty shell) — Tabbed chat workspace placeholder. Textbox area with send button. Message list area with `VirtualizingStackPanel`. Chat header bar placeholder (Persona name, context, cost, controls). All C1-C37 features come in Wave 3. Depends on: Feature 42.
- **Feature 45:** Onboarding Wizard Screen (empty shell) — Multi-step wizard shell with step indicator (Welcome → API Keys → Persona → Wiki → Hotkeys → Finish). Skip-per-step support. Re-launchable from Settings. Depends on: Feature 42.
- **Feature 46:** Model Comparison Screen (empty shell) — Side-by-side panel layout (2-4 panels, horizontal/vertical split). Per-panel header, input area, and response area. Broadcast mode toggle placeholder. Depends on: Feature 42.
- **Feature 47:** Settings Screen (empty shell) — Section-based settings shell with 14 categories listed as scrollable sections. Each section as an `Expander` or group header. Category list: Providers, Profiles, Appearance, Wiki, Backup, Hotkeys, Tools, Language, Notifications, Startup, Updates, Security, Maintenance. Depends on: Feature 42.
- **Feature 48:** Wiki Browser Screen (empty shell) — Three-region split layout: collapsible file tree (left), Markdown viewer (center), info panel (right) with three tabs (Related Sections, Backlinks, File Info). `GridSplitter` between regions. Depends on: Feature 42.
- **Feature 49:** Usage Dashboard Screen (empty shell) — Dashboard layout with summary cards row (total tokens, total cost, active models), chart areas (line, bar, pie), and per-chat breakdown table placeholder. Time range filter bar. Depends on: Feature 42.
- **Feature 50:** Media Library Screen (empty shell) — Gallery grid layout with filter bar (type, source chat, date range). Search box. Thumbnail grid with virtualization. Depends on: Feature 42.
- **Feature 51:** Global Artifacts Browser Screen (empty shell) — List/grid layout for cross-chat artifacts. Search, sort, filter bar. Artifact detail viewer placeholder. Depends on: Feature 42.

### Windows Shell Infrastructure

- **Feature 52:** System Tray Integration — `NotifyIcon` via WinForms interop. Minimize to tray on close (configurable). Left-click restores. Right-click context menu: New Chat, Open Studio, Command Bar, Recent Chats, Settings, Exit. Generation indicator (pulsing green dot on icon). `ISystemTrayService`. Depends on: Feature 2.
- **Feature 53:** Global Hotkey Registration — `IGlobalHotkeyService` using `RegisterHotKey` (primary) + `WH_KEYBOARD_LL` fallback. Register Alt+Q/W/E/R for Tier 1 and Alt+Space for Tier 2. Conflict detection on assignment. `WM_HOTKEY` message pump integration. Depends on: Feature 2.
- **Feature 54:** Per-Monitor DPI Awareness — `PerMonitorV2` in `app.manifest`. SVG icons via `SharpVectors` for crisp scaling. Multi-resolution bitmap assets. DPI-aware overlay positioning. Depends on: Feature 41.

### Theming Infrastructure

- **Feature 55:** Dark/Light Theme System — WPF `ResourceDictionary` with `DynamicResource` references. Two top-level dictionaries: `Dark.xaml` (default), `Light.xaml`. Instant toggle via `IThemeProvider.SetAppTheme()` without restart. Sun/Moon icon in chat header for quick toggle. Depends on: Feature 32.
- **Feature 56:** Chat Visual Themes — Three `DataTemplate` variants for message rendering: Classic (role label + timestamp header, distinct backgrounds), Compact (minimal header, color accent only), Bubble (speech-bubble tails, alternating sides). `ChatTheme` property on `IThemeProvider` drives selection. Depends on: Feature 55.
- **Feature 57:** Font Settings — Configurable font family, size (10-24px range), and weight for chat messages. A⁻/A⁺ quick adjust buttons in chat header. Settings persistence in SQLite. Depends on: Feature 55.

### Auto-Update & WebSocket

- **Feature 58:** Auto-Update Framework — `IUpdateChecker` implementation using `AutoUpdater.NET`. Remote feed check (JSON/XML) on configurable schedule (startup/daily/weekly/manual). Download with progress, install MSIX, restart app. Depends on: Feature 33.
- **Feature 59:** Local WebSocket Server — ASP.NET Core Kestrel embedded, listening on `127.0.0.1` only (configurable port). Token-based authentication (auto-generated, displayed in Settings). JSON protocol over WebSocket for external integrations (Word Add-in). `ILocalWebSocketServer` service. Depends on: Feature 2.

### Testing & DevOps (Wave 2)

- **Feature 60:** Integration Test Framework Setup — Test fixtures for service-level integration tests. Shared test database (SQLite file per test run). Test helpers for seeding test data (personas, chats, messages). Depends on: Feature 39.
- **Feature 61:** MSIX Packaging & Code Signing Pipeline — MSIX build in CI/CD. EV Code Signing Certificate integration. `.appinstaller` file generation for auto-update. Version stamping from git tags. Depends on: Feature 1.

---

## Wave 3: Vertical Slices — Features 62–225

End-to-end features (DB → service → UI). Each feature ADDS new code to pre-built Wave 1-2 infrastructure. Features do NOT modify Wave 1-2 code. Ordered by dependency chain.

### Batch 3A — Core Configuration & Personas (no Wave 3 dependencies)

- **Feature 62:** API Key Management (B1) — Add, view, edit, delete, test API keys in Settings → Providers. Encrypted at rest via DPAPI (`IEncryptionService`). Provider types: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAICompatible. "Test Key" validates against provider API via `ILLMProvider.ValidateKeyAsync()`. Key value never displayed in full after save. Uses Wave 1 entity: Feature 5 (ApiKey). Extends Wave 1 abstraction: Feature 20 (ILLMProvider), Feature 34 (IEncryptionService).
- **Feature 63:** Model Configurations CRUD (B2) — Create, edit, delete named Model Configurations in Settings → Profiles. Fields: display name, provider, API key (FK), model identifier, temperature (0.0-2.0), max output tokens, max context window, thinking on/off, pricing (input/output per 1K tokens), context overflow strategy (SlidingWindow/HardStop/AutoSummarize). Uses Wave 1 entity: Feature 6 (ModelConfiguration). Extends Wave 1 abstraction: Feature 35 (IApiKeyRepository).
- **Feature 64:** Auto-Fetch Available Models (B7) — Fetch model list from provider API via `ILLMProvider.ListModelsAsync()` when API key is added. Cached in SQLite, refreshable via button. Manual model identifier entry also supported. Uses Wave 1 entity: Feature 6 (ModelConfiguration). Extends Wave 1 abstraction: Feature 20 (ILLMProvider).
- **Feature 65:** "OpenAI-Compatible" Provider Type (B6) — Generic provider with display name, endpoint URL, optional API key. Enables any OpenAI-API-compatible service and local open-source models (Ollama, LM Studio). Uses Wave 1 entity: Feature 5 (ApiKey), Feature 6 (ModelConfiguration). Extends Wave 1 abstraction: Feature 24 (OpenAICompatibleProvider implementation).
- **Feature 66:** Personas CRUD (B3) — Create, edit, delete Personas in Settings → Profiles. Fields: display name, system prompt (≤32K chars, supports `{{variables}}`), default Model Configuration (FK), default chat mode (Standard/TextCompletion). Built-in defaults: "General Assistant." Uses Wave 1 entity: Feature 7 (Persona). Extends Wave 1 abstraction: Feature 35 (IModelConfigurationRepository).

### Batch 3B — Settings & Core Workspace Foundation

- **Feature 67:** Global Settings Screen (A1) — Full Settings screen with all 14 categories (Providers, Profiles, Appearance, Wiki, Backup, Hotkeys, Tools, Language, Notifications, Startup, Updates, Security, Maintenance). Each category as a section with its own subsection ViewModel. Settings persisted via `ISettingsRepository`. Uses Wave 1 entity: settings key-value store. Extends Wave 1 abstraction: Feature 35 (ISettingsRepository).
- **Feature 68:** Default Profile Selection (A2) — User selects which Persona is auto-assigned when creating a new chat. Dropdown in Settings → Profiles. Falls back to first available Persona or built-in "General Assistant." Uses Wave 1 entity: Feature 7 (Persona). Extends Wave 1 abstraction: Feature 35 (IPersonaRepository).
- **Feature 69:** Appearance Settings (A3) — Chat theme selector (Classic/Compact/Bubble). Font family, size (10-24px), weight controls with live preview. Independent of dark/light mode. Uses Wave 2 feature: Feature 56 (Chat Visual Themes), Feature 57 (Font Settings). Extends Wave 1 abstraction: Feature 32 (IThemeProvider).
- **Feature 70:** Dark/Light Mode Toggle (A5) — App-wide toggle with Sun/Moon quick-toggle in chat header. Persisted preference. Dark mode default. Independent of chat visual themes. Uses Wave 2 feature: Feature 55 (Dark/Light Theme System). Extends Wave 1 abstraction: Feature 32 (IThemeProvider).
- **Feature 71:** Notification & Streaming Settings (A4) — Toggle sound on assistant completion. Option to disable streaming entirely (fallback to non-streaming `ChatAsync`). Per-chat mute toggle. Uses Wave 1 entity: Feature 8 (ChatThread, isMuted). Extends Wave 1 abstraction: Feature 35 (ISettingsRepository).
- **Feature 72:** Startup Behavior (A6) — Option to launch on Windows startup (registry `Run` key). Option to restore last session (reopen all chats/tabs from previous session). Session state persisted to SQLite. Extends Wave 1 abstraction: Feature 35 (ISettingsRepository).
- **Feature 73:** Auto-Update Settings (A7) — Check frequency selector (startup/daily/weekly/manual). "Check Now" button. Release notes display. Download progress bar. Uses Wave 2 feature: Feature 58 (Auto-Update Framework). Extends Wave 1 abstraction: Feature 33 (IUpdateChecker).
- **Feature 74:** Speech-to-Text Provider Configuration (A10) — Select STT provider (OpenAI Whisper API, local Whisper, Windows built-in). Configure model size for local Whisper. Extends Wave 1 abstraction: Feature 26 (ISTTProvider).

### Batch 3C — Persona-Driven Chat Core

- **Feature 75:** Persona Selection per Chat (B4) — New chat (Ctrl+N) picks Persona from dropdown list. Pre-configures system prompt, model, and chat mode. Switchable anytime from textbox toolbar. Recently used Personas at top. Uses Wave 1 entity: Feature 7 (Persona), Feature 8 (ChatThread), Feature 6 (ModelConfiguration). Extends Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 76:** Local Open-Source Model Support (B5) — Connect via OpenAI-Compatible provider pointing to local endpoint (e.g., `localhost:1234`). Supports Ollama, LM Studio, text-generation-webui. Special error handling: "Is the local model server running?" Uses Wave 1 entity: Feature 6 (ModelConfiguration), Feature 5 (ApiKey). Extends Wave 1 abstraction: Feature 24 (OpenAICompatibleProvider implementation).
- **Feature 77:** Context Window Overflow Strategy (B8) — Per-config strategy applied at send time. SlidingWindow: drop oldest messages from context. HardStop: block send + warn at 90% full. AutoSummarize: AI summarizes oldest 50% into summary block (separate API call). Changeable mid-chat. Uses Wave 1 entity: Feature 6 (ModelConfiguration), Feature 9 (Message). Extends Wave 1 abstraction: Feature 36 (ILLMProviderService).

### Batch 3D — Studio Chat Core

- **Feature 78:** Conversation View (C1) — Scrolling message history via `VirtualizingStackPanel` with recycling. Messages visually distinguished per theme. Each message shows Persona/model name and relative timestamp (hover for full). Uses Wave 1 entity: Feature 9 (Message). Extends Wave 1 abstraction: Feature 37 (IChatThreadService), Feature 31 (IContentBlockRenderer). Reference pattern: [`external-docs/ref-chatgpt-desktop-chat-ux.md`](../external-docs/ref-chatgpt-desktop-chat-ux.md).
- **Feature 79:** Message Content Rendering (C2) — Convert Markdig AST to WPF `FlowDocument` via `IContentRendererRegistry`. All Markdown constructs: headings, bold, italic, code blocks with syntax highlighting, lists, links, tables, blockquotes. Images inline (clickable), audio (mini player), video (embedded player). Uses Wave 1 abstraction: Feature 31 (IContentRendererRegistry), all 7 IContentBlockRenderer implementations (Feature 31).
- **Feature 80:** Code Block Rendering (C3) — Syntax highlighting via AvalonEdit `HighlightingManager` (100+ languages) by declared language. Copy button on hover at top-right. Language label top-left. Horizontal scrolling for long lines (no wrapping). Always LTR regardless of content language. Uses Wave 1 abstraction: Feature 31 (CodeBlockRenderer).
- **Feature 81:** Streaming Response Display (C4) — Token-by-token progressive rendering via `MarkdownStreamRenderer`. Each `StreamChunk.ContentDelta` → Markdig incremental parse → WPF `FlowDocument` update in-place. Code block fence detection mid-stream. Generation time displayed on completion. Uses Wave 1 abstraction: Feature 20 (ILLMProvider), Feature 31 (IContentRendererRegistry).
- **Feature 82:** Message Actions — Send/Stop (C5) — Send on Enter or button click. During generation, Send button transforms into spinner + red "Stop" button. Clicking Stop preserves partial response as assistant message. Uses Wave 1 abstraction: Feature 37 (IChatThreadService), Feature 36 (ILLMProviderService).
- **Feature 83:** Conditional [Apply] Button — Tier 1 Elevation (C5a) — When chat originated from Tier 1 text transformation: source indicator banner in header (source app + document title). [Apply Latest] button in header. Per-message [Apply] on direct transformations. Grayed out if source app window is closed. Uses Wave 1 entity: Feature 8 (ChatThread, source HWND/app name/doc title). Extends Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 84:** Copy MD / Copy Rich (C6) — Two per-message buttons: "Copy MD" (raw Markdown to clipboard as plain text) and "Copy Rich" (HTML/RTF to clipboard). Copy entire conversation via three-dot menu option. Extends platform abstraction: IClipboardService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 85:** Chat Titling (C7) — Auto-generate chat title from first user message via AI (lightweight `ChatAsync` call to current Persona with titling prompt). Manually editable title field in chat header (click to edit). Uses Wave 1 entity: Feature 8 (ChatThread, title). Extends Wave 1 abstraction: Feature 36 (ILLMProviderService).
- **Feature 86:** Continue Generation (C8) — "Continue" button appears below last message when it's from assistant. Sends continuation request appending "continue" to context. Uses Wave 1 abstraction: Feature 37 (IChatThreadService), Feature 36 (ILLMProviderService).

### Batch 3E — Chat Input & Media

- **Feature 87:** Drag & Drop Files/Media (C9) — Drag files/images/video/audio into textbox. Images attached for vision models. Text files read into prompt content. Other files: filename + metadata included in prompt. Attachment thumbnails below input. Uses Wave 1 entity: Feature 11 (MediaItem). Extends Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 88:** Paste Image from Clipboard — Ctrl+V (C9a) — Detect image on clipboard, paste as thumbnail below input with filename. Click thumbnail to open in new image viewer tab. Send with next message for vision models. Uses Wave 1 entity: Feature 11 (MediaItem). Extends platform abstraction: IClipboardService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 89:** Model-Aware File Compatibility (C9c) — Yellow warning badge on attachment when file type not supported by active model. "⚠️ [Model] does not support [file type]. Attached as metadata only." Uses Wave 1 entity: Feature 6 (ModelConfiguration). No specific abstraction extension.
- **Feature 90:** Audio Input — Microphone (C21) — Microphone button in textbox toolbar. Record via NAudio, transcribe via configured `ISTTProvider`. Editable transcribed text appears in textbox. Microphone button grayed out if no mic detected. Uses Wave 1 abstraction: Feature 26 (ISTTProvider).
- **Feature 91:** Camera Capture (C22) — Webcam button in textbox toolbar. Live preview popup + "Capture" button. Captured image attached to message for vision models. Uses AForge.NET. Uses Wave 1 entity: Feature 11 (MediaItem). Extends platform abstraction: ICameraService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 92:** Spell Check in Textbox (C34) — Red squiggly underline via Hunspell + custom WPF adorner. Right-click suggestions menu. "Add to Dictionary" (custom dictionary in SQLite). Toggle on/off in Settings. Language-aware: Hebrew text not spell-checked with English dictionary. Extends platform abstraction: ISpellCheckService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).

### Batch 3F — Multi-Tab & Workspace

- **Feature 93:** Multiple Chat Tabs (C10) — Open multiple chats in tabs. Reorder by drag-drop. Close with middle-click or Ctrl+W. Reopen closed tab with Ctrl+Shift+T. Tab shows chat title, 🕶️ incognito indicator, and generation indicator (green dot). Uses Wave 1 entity: Feature 8 (ChatThread).
- **Feature 94:** Token Usage & Context Display (C11) — Per-message token count + estimated cost shown below assistant messages. Chat header shows context size bar: "X / Y tokens" with color gradient (green → yellow → red). Cumulative cost display. Local tokenization via `SharpToken` for real-time feedback. Uses Wave 1 entity: Feature 14 (UsageRecord), Feature 9 (Message). Extends Wave 1 abstraction: Feature 25 (ITokenizer, ITokenizerFactory).
- **Feature 95:** Keyboard Shortcuts — Studio (C12) — Ctrl+N (new chat), Ctrl+W (close tab), Ctrl+Shift+T (reopen tab), Ctrl+Tab/Shift+Tab (next/prev tab), Ctrl+F (search in chat), Ctrl+Shift+F (global search), Ctrl+S (export). Ctrl+/ for keyboard shortcut reference overlay. All shortcuts configurable in Settings → Hotkeys. Extends Wave 2 feature: Feature 53 (Global Hotkey Registration).
- **Feature 96:** Resizable Panels (C13) — Sidebar, right panel (Artifacts + Chat Nav), and wiki browser regions all resizable via `GridSplitter`. Minimum widths enforced (sidebar 150px, right panel 200px). Sizes remembered across sessions. Uses Wave 2 feature: Feature 41 (MainWindow Shell).
- **Feature 97:** Error Handling & Retry (C14) — Specific error message per failure type (401 auth, 429 rate limit, 5xx server error, network timeout, connection refused). Retry button on each error. Escalating message on consecutive failures: "Still having trouble? Check your API key or try a different model." Uses Wave 1 abstraction: Feature 20 (ILLMProvider).
- **Feature 98:** Scroll-to-Bottom Button (C15) — Floating ↓ button appears when user scrolls up during streaming. Smooth animated scroll to latest message on click. Auto-hides when at bottom. Uses Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 99:** Auto-Scroll Behavior (C17) — Auto-scroll pauses when user scrolls up during generation. Resumes on scroll-to-bottom click. Handles media height changes (image/video loading) smoothly without jarring jumps. Uses Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 100:** Cross-Tab Completion Alert (C35) — Pulsing green dot on inactive tab when generation completes. Sound + brief "✓" in tab title. Configurable in Settings → Notifications. Uses `Messenger` pattern: `GenerationCompletedMessage`. No specific Wave 1 abstraction extension.
- **Feature 101:** Auto-Save Message Drafts (C36) — `PeriodicTimer` (5-second tick) serializes textbox content + cursor position to SQLite `MessageDrafts` table keyed by `ChatThread.id`. On tab open: check for draft, show "Restore draft?" dialog. On send: delete draft. On empty textbox for 5s: delete draft. Uses Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 102:** Right Panel Layout (C37) — Two vertically stacked resizable sections: Artifacts (top) + Chat Nav (bottom). Divider with `GridSplitter` for resizing. Both sections collapsible via toggle buttons. No tabs — both visible simultaneously. Uses Wave 2 feature: Feature 41 (MainWindow Shell).

### Batch 3G — Chat Modes & Controls

- **Feature 103:** Standard Chat Mode (E1) — Default conversational mode. User/assistant message alternation. Full conversation history sent as context. Persona-driven system prompt. Uses Wave 1 entity: Feature 8 (ChatThread, chatMode). Extends Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 104:** Text Completion Mode (E2) — Raw text prompt → raw completion. No conversation history sent. For text completion API endpoints. Toggle in chat header. Uses Wave 1 entity: Feature 8 (ChatThread, chatMode). Extends Wave 1 abstraction: Feature 20 (ILLMProvider).
- **Feature 105:** Thinking Toggle (E3) — Enable/disable AI extended reasoning. Shows thinking/reasoning tokens in collapsible "Thinking…" accordion above assistant response. Thinking tokens counted toward usage. Toggle grayed out for models that don't support thinking. Uses Wave 1 entity: Feature 8 (ChatThread, thinkingEnabled). Extends Wave 1 abstraction: Feature 20 (ILLMProvider), Feature 31 (ThinkingRenderer).
- **Feature 106:** Mute Notifications Toggle (E4) — Per-chat mute for sound notifications. Toggle in chat header. Overrides global notification setting (Feature 71). Uses Wave 1 entity: Feature 8 (ChatThread, isMuted). No specific abstraction extension.
- **Feature 107:** Dynamic System Message Editing (E5) — Click Persona name in chat header → popover with editable system message. Also accessible from three-dot menu and Chat Nav bar context menu. Changes take effect for subsequent messages. Current system message displayed in popover. Uses Wave 1 entity: Feature 8 (ChatThread, systemMessage). Extends Wave 1 abstraction: Feature 35 (IPersonaRepository).

### Batch 3H — Message Branching

- **Feature 108:** Edit Any Past Message (D1) — Right-click message → "Edit." Two modes: "Edit in Place" (overwrite, same `branchId`, incremented `versionNumber`) and "Edit as Branch" (new `branchId`). AI sees updated history on next send. Uses Wave 1 entity: Feature 9 (Message, branching attributes). Extends Wave 1 abstraction: Feature 35 (IMessageRepository).
- **Feature 109:** Delete Any Past Message (D2) — Right-click message → "Delete." Removes message from active conversation history. Branch data preserved (orphaned branches accessible via chat tree). Uses Wave 1 entity: Feature 9 (Message). Extends Wave 1 abstraction: Feature 35 (IMessageRepository).
- **Feature 110:** Branch Navigation (D3) — Branch indicator on messages with multiple versions (e.g., "2/3"). ← → arrow buttons cycle through branches. Subsequent messages re-render on branch switch. Uses Wave 1 entity: Feature 9 (Message, branchId, versionNumber, isActiveBranch). Extends Wave 1 abstraction: Feature 35 (IMessageRepository, SetActiveBranch).
- **Feature 111:** Chat Tree Visualization (D4) — Visual tree/graph of all branches in a chat. Nodes = messages, edges = parentMessageId relationships grouped by branchId. Active branch highlighted. Click node to navigate to that branch point. Scrollable/zoomable canvas. Uses Wave 1 entity: Feature 9 (Message, recursive CTE query). Extends Wave 1 abstraction: Feature 35 (IMessageRepository, GetAllBranchesForThreadAsync).
- **Feature 112:** Quote from Chat (D5) — Select text in any message → right-click → "Quote." Inserts selected text as quoted block (`> selected text`) in textbox. Truncated at 2,000 characters if longer. Uses Wave 1 entity: Feature 9 (Message). No specific abstraction extension.
- **Feature 113:** Chat Navigation Bar (D6) — Collapsible panel (right side, below Artifacts) with scrollable message list. Each entry: role icon + first 60 chars of content. Click to jump/scroll to that message. Current position highlighted. Uses Wave 1 entity: Feature 9 (Message). Extends Wave 1 abstraction: Feature 35 (IMessageRepository).
- **Feature 114:** Duplicate / Fork Chat (D7) — Right-click message → "Fork from here" or three-dot menu → "Duplicate Chat." Creates new `ChatThread` with all messages up to fork point. Original thread unchanged. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 9 (Message). No specific abstraction extension.
- **Feature 115:** Message Feedback (D8) — Thumbs-up/down buttons on each assistant message. Stored with message (`feedback` field). Aggregated in Usage Dashboard (Feature 201). Uses Wave 1 entity: Feature 9 (Message, feedback). No specific abstraction extension.
- **Feature 116:** Undo/Redo Message Edits (D9) — Ctrl+Z/Ctrl+Y for message edits. Per-chat undo stack (depth: 50 operations). Persists until chat tab closed. Covers edit, delete, and branch switch operations. Uses Wave 1 entity: Feature 9 (Message). No specific abstraction extension.

### Batch 3I — Chat Organization & Search

- **Feature 117:** Sidebar Chat List (L1) — All permanent chats sorted by selected order (Most Recent default). Pinned chats at top in separate section. Grouped by date (Today, Yesterday, This Week, Older). Each entry: title, first-line preview, relative timestamp, star icon, tags, color dot. Uses Wave 1 entity: Feature 8 (ChatThread). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).
- **Feature 118:** Chat Favoriting (L2) — Star/unstar chats from sidebar and chat header. Filter toggle for favorites only. Starred chats appear in both "All" and "Favorites" views. Uses Wave 1 entity: Feature 8 (ChatThread, isFavorite). No specific abstraction extension.
- **Feature 119:** Full-Text Chat Search (L3) — Search bar in sidebar. Queries SQLite FTS5 on message content. Results show: snippet with highlights, chat name, timestamp. Click opens chat and scrolls to message. Searches both permanent and transient chats in 7-day window. Uses Wave 1 entity: Feature 9 (Message, FTS5). Extends platform abstraction: IChatSearchService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 120:** Sidebar Filtering (L6) — Default view: permanent chats. Timeline tab: transient actions (Tier 1 + Tier 2). Toggle between views via sidebar header tabs. Uses Wave 1 entity: Feature 8 (ChatThread, isTransient). No specific abstraction extension.
- **Feature 121:** Chat Tags/Labels (L7) — User-defined tags on chats (e.g., "coding", "writing"). Tag input with autocomplete from existing tags. Filter by tag in sidebar. Uses Wave 1 entity: Feature 8 (ChatThread, tags). No specific abstraction extension.
- **Feature 122:** Pin Chats (L8) — Pin/unpin from right-click menu. Pinned chats appear in separate "Pinned" section above all other chats. Order preserved. Uses Wave 1 entity: Feature 8 (ChatThread, isPinned). No specific abstraction extension.
- **Feature 123:** Chat Folders/Collections (L9) — Create folders. Drag chats into folders. One chat per folder (no nesting). Sidebar grouping by folder with expand/collapse. "Unfiled" section for chats without folder. Uses Wave 1 entity: Feature 8 (ChatThread, folderId). No specific abstraction extension.
- **Feature 124:** Chat Archiving (L10) — Archive chat (hide without deleting). Accessible via "Archived" filter toggle. Excluded from auto-cleanup. Unarchive restores to previous location. Uses Wave 1 entity: Feature 8 (ChatThread, isArchived). No specific abstraction extension.
- **Feature 125:** Bulk Operations (L11) — Select multiple chats via checkboxes. Bulk actions: delete (soft), archive, export, tag, move to folder. Progress indicator for operations on 100+ chats. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.
- **Feature 126:** Chat Sorting Options (L13) — Sort selector: Most Recent (default), Name A-Z, Date Created, Last Activity. Preference remembered. Uses Wave 1 entity: Feature 8 (ChatThread). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).
- **Feature 127:** Chat Color Labels (L14) — Assign colored dot from preset palette (8 colors). Right-click → "Label" submenu. Visual identification in sidebar. Uses Wave 1 entity: Feature 8 (ChatThread, colorLabel). No specific abstraction extension.
- **Feature 128:** Right-Click Context Menu (L12) — Right-click chat in sidebar: Rename, Delete, Archive, Duplicate, Export, Pin/Unpin, Tags submenu, Move to Folder submenu, Color Label submenu. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.

### Batch 3J — Soft-Delete & Data Lifecycle

- **Feature 129:** Soft-Delete on Chat Deletion (U1) — Delete chat → `isDeleted=true`, `deletedAt=now`. Chat hidden from all lists except Trash view. Confirmation dialog. Undo via toast for 5 seconds. Uses Wave 1 entity: Feature 8 (ChatThread, isDeleted, deletedAt). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).
- **Feature 130:** Trash View (U2) — Sidebar "🗑️ Trash" item showing all soft-deleted chats. Each entry: title, deletion date, "Restore" button, "Delete Permanently" button. Days remaining until auto-purge shown. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.
- **Feature 131:** 30-Day Auto-Purge (U3) — Background task (runs on startup + daily timer) permanently deletes chats where `isDeleted=true` AND `deletedAt > 30 days ago`. Skips chats currently open in a tab. Uses Wave 1 entity: Feature 8 (ChatThread). Extends platform abstraction: IAutoCleanupService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 132:** Restore from Trash (U4) — Restore clears `isDeleted` and `deletedAt`. Chat returns to original location (folder, tags, pinned status preserved). Toast confirmation. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.
- **Feature 133:** Permanent Delete from Trash (U5) — Hard delete with O5 garbage collection cascade (exclusively linked media/artifacts deleted; shared/saved items preserved). Confirmation dialog with item count. Uses Wave 1 entity: Feature 8 (ChatThread). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).
- **Feature 134:** Empty Trash (U6) — Bulk permanent delete of all items in Trash. Confirmation dialog showing count. Progress bar for large operations. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.

### Batch 3K — Data Model Lifecycle

- **Feature 135:** Transient Thread Lifecycle (O2, O3) — Tier 1/2 create ChatThreads with `isTransient=true`. Sending reply in Studio flips `isTransient` to `false`. Auto-elevation exceptions: favorited, tagged, pinned, archived, or containing user replies/branches. Uses Wave 1 entity: Feature 8 (ChatThread, isTransient). No specific abstraction extension.
- **Feature 136:** 7-Day Auto-Cleanup (O4) — Background task deletes `isTransient=true` threads older than 7 days. Skips threads currently open in a tab. Runs on startup + hourly. Exceptions per Feature 135. Uses Wave 1 entity: Feature 8 (ChatThread). Extends platform abstraction: IAutoCleanupService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 137:** Garbage Collection Policy (O5) — On hard delete: exclusively linked MediaItems and Artifacts also deleted. Items with `isSavedToDisk=true` or `isSavedToWiki=true` preserved. Shared items (referenced by multiple threads) preserved. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 11 (MediaItem), Feature 10 (Artifact). No specific abstraction extension.
- **Feature 138:** Database Compaction (A9 / O6) — "Compact Database" button in Settings → Maintenance. Runs SQLite `VACUUM`. Displays database size before and after. ⚠️ Requires temporary free disk space equal to database size. Uses Wave 1 abstraction: Feature 4 (EF Core DbContext). No specific abstraction extension.

### Batch 3L — Three-Tier Interaction System

- **Feature 139:** Text Actions CRUD (K1) — Create, edit, delete Text Actions. Built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt. Custom actions: name + system prompt + Model Configuration. Assignable to hotkeys and available in Studio toolbar dropdown. Uses Wave 1 entity: Feature 13 (TextAction). Extends Wave 1 abstraction: Feature 35 (IModelConfigurationRepository).
- **Feature 140:** Textbox Toolbar (K2) — Controls above chat input: Persona selector dropdown, thinking toggle, mute toggle, tools toggle, auto-approval override, prompt library button, Text Actions dropdown. All controls bound to `ChatThread` and `Persona` properties. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 7 (Persona). No specific abstraction extension.
- **Feature 141:** Tier 1 — Global Hotkey Text Actions (K3) — Three-phase overlay flow. Capture phase: highlighted text captured via UIA `TextPattern.GetSelection()` + clipboard fallback, HWND via `GetForegroundWindow`, "Thinking…" pill overlay near cursor (does NOT steal focus via `WS_EX_NOACTIVATE`). Result phase: editable AI output popup with Accept/Discard/Open in Studio/Retry + Additional Instructions text field. Apply phase: text injected back via UIA `ValuePattern.SetValue()` → `WM_SETTEXT` → `SendInput` Ctrl+V fallback. Confirmation toast + Undo. Uses Wave 1 entity: Feature 13 (TextAction), Feature 8 (ChatThread), Feature 9 (Message). Extends Wave 2 abstraction: Feature 53 (IGlobalHotkeyService). Extends Wave 1 abstraction: Feature 36 (ILLMProviderService). Extends platform abstractions: IHwndCaptureService, ITextInjectionService, IClipboardService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)). Reference pattern: [`external-docs/ref-windows-mcp-automation.md`](../external-docs/ref-windows-mcp-automation.md), [`external-docs/ref-powertoys-global-hotkeys.md`](../external-docs/ref-powertoys-global-hotkeys.md).
- **Feature 142:** Tier 2 — Command Bar (K4) — Alt+Space opens centered Spotlight-style overlay. Inline state: text input field, expandable Q&A display (streaming Markdown), Pop-out/Close/Copy controls. Popped-out state: floating resizable mini-window (min 350×250px) with Open in Studio, Pin, Minimize, Close buttons. Elevation to Studio creates permanent ChatThread. Dismissal (Escape) saves as transient thread. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 9 (Message). Extends Wave 1 abstraction: Feature 53 (IGlobalHotkeyService — defined in Wave 2), Feature 37 (IChatThreadService), Feature 36 (ILLMProviderService). Reference pattern: [`external-docs/ref-powertoys-global-hotkeys.md`](../external-docs/ref-powertoys-global-hotkeys.md).
- **Feature 143:** Tier 3 — Studio Chat (K5) — Full chat workspace as defined in Batches 3D-3G. Opened via main window, system tray, or elevation from Tier 1/2. Uses Wave 1 entity: Feature 8 (ChatThread). Extends Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 144:** Tier 1/2 Elevation to Studio (O3) — "Open in Studio" in Tier 1 result popup or Tier 2 Command Bar → MainWindow opens, chat loaded in new tab, `isTransient` flips to `false`. Source context (HWND, app name, doc title, original text) preserved for [Apply] button. Uses Wave 1 entity: Feature 8 (ChatThread, isTransient, source context). Extends Wave 1 abstraction: Feature 37 (IChatThreadService). Reference flow: [`vision/flows/elevate-transient-to-permanent.md`](vision/flows/elevate-transient-to-permanent.md).

### Batch 3M — Import & Export

- **Feature 145:** Export Chat (I1) — Export current branch as Markdown, PDF, or JSON. Markdown: raw content with metadata header. PDF: QuestPDF layout from Markdig AST. JSON: full serialized ChatThread + Messages with metadata. Progress bar for large chats. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 9 (Message). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).
- **Feature 146:** Import Chats (I2) — Import from ChatGPT export JSON and Claude export JSON. File picker → validate → parse via `IChatImporter` → create ChatThreads. Duplicate detection: compare title + first message content hash. Summary: "Imported [N] chats. [M] skipped (duplicates)." Progress bar. Available during onboarding (Finish step) and from Settings. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 9 (Message), Feature 7 (Persona). Extends Wave 1 abstraction: Feature 30 (IChatImporter — ChatGPTImporter, ClaudeImporter). Reference flow: [`vision/flows/import-chatgpt.md`](vision/flows/import-chatgpt.md).

### Batch 3N — Artifacts System

- **Feature 147:** AI-Generated Artifacts (F1) — AI produces named artifacts (code, docs, config files) with type inferred from language/extension. Stored in Artifact entity with version history. Depends on: Feature 10 (Artifact Entity). No specific abstraction extension beyond the entity.
- **Feature 148:** Side Panel — Artifact List (F2) — Resizable panel right of chat. Lists all artifacts by name with type icon and version count. Click to view content in artifact viewer. Uses Wave 1 entity: Feature 10 (Artifact). No specific abstraction extension.
- **Feature 149:** Artifact Version History (F3) — Each artifact maintains versions (v1, v2, v3...). AI produces new version on changes. Version dropdown in artifact viewer. Uses Wave 1 entity: Feature 10 (Artifact, embedded versions). No specific abstraction extension.
- **Feature 150:** Diff View (F4) — Side-by-side or unified diff between any two artifact versions via DiffPlex. Red = removed, green = added. Version selectors: "Compare v[N] with v[M]." Uses Wave 1 abstraction: DiffPlex (library, not a formal abstraction in [`planning/abstractions.md`](planning/abstractions.md)).
- **Feature 151:** Version Switching & Branching (F5) — Switch active version from dropdown. Reverting to older version + making changes = new branch from that version. Uses Wave 1 entity: Feature 10 (Artifact). No specific abstraction extension.
- **Feature 152:** Artifact Viewer (F6) — Syntax highlighting via AvalonEdit for code. Rendered Markdown view for `.md` artifacts. "Save to Disk" button (save dialog). "Save to Wiki" button (launches N5 pipeline). Uses Wave 1 abstraction: Feature 31 (CodeBlockRenderer).
- **Feature 153:** Global Artifacts Browser (F7) — Cross-chat listing of all artifacts with search (by name, type), sort (by date, name, chat), and filter (by type, chat). Click to view in side panel. Virtualized list for performance. Uses Wave 1 entity: Feature 10 (Artifact). No specific abstraction extension.

### Batch 3O — Media Library

- **Feature 154:** Media Library Overview (G1) — Browsable gallery grid of all media across chats. Thumbnails for images, icons for audio/video. AI-generated and user-uploaded. Click to view/play. Virtualized grid for performance with thousands of files. Uses Wave 1 entity: Feature 11 (MediaItem). No specific abstraction extension.
- **Feature 155:** Media Library Filtering & Search (G2) — Filter by type (Image/Audio/Video), source chat, date range. Search by filename. Grid updates reactively. Uses Wave 1 entity: Feature 11 (MediaItem). No specific abstraction extension.
- **Feature 156:** Media Actions (G3) — View/play, download, copy to clipboard, open in system app, delete, "Go to Source Chat" (opens chat and scrolls to message). Uses Wave 1 entity: Feature 11 (MediaItem). No specific abstraction extension.
- **Feature 157:** Image Generation (G4) — AI generates images inline in chat. Auto-saved to Media Library with `source=AIGenerated` and prompt stored. Uses Wave 1 entity: Feature 11 (MediaItem). Extends Wave 1 abstraction: Feature 20 (ILLMProvider).
- **Feature 158:** Audio Generation (G5) — AI generates audio with inline NAudio mini player (play/pause/seek). Auto-saved to Media Library. Uses Wave 1 entity: Feature 11 (MediaItem). Extends Wave 1 abstraction: Feature 20 (ILLMProvider). Extends platform abstraction: IAudioService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 159:** Inline Media in Chat (G6) — All media renders inline in messages. Images: clickable to enlarge. Audio: NAudio mini player. Video: `MediaElement` with controls + LibVLCSharp fallback. "Save to Disk" and "View in Library" buttons. Uses Wave 1 entity: Feature 11 (MediaItem). Extends Wave 1 abstraction: Feature 31 (MediaRenderer). Extends platform abstraction: IVideoPlayerService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).

### Batch 3P — Tool Use & Agents

- **Feature 160:** Web Search Tool (H1) — AI requests web search via function calling → `WebSearchToolExecutor` executes via `ISearchProvider` → results as system message → AI continues. Search query visible as collapsible system message. Uses Wave 1 abstraction: Feature 28 (ISearchProvider), Feature 29 (IToolOrchestrator), Feature 29 (WebSearchToolExecutor).
- **Feature 161:** Terminal/Script Execution (H2) — AI requests shell command → ALWAYS shows confirmation dialog with command text, working directory, and risk level (detects `rm`, `del`, `format`, `sudo`, `reg` and flags HIGH). User must explicitly approve. Can never be auto-approved (Feature 164 override). Executes via `System.Diagnostics.Process`, captures stdout/stderr, feeds back as tool result. Uses Wave 1 abstraction: Feature 29 (IToolOrchestrator), Feature 29 (TerminalToolExecutor).
- **Feature 162:** File Generation (H3) — AI requests file creation → save dialog for target path → AI generates content → preview pane → user confirms → writes via `System.IO`. Enforces wiki directory exclusion. Overwrite warning if file exists. Uses Wave 1 abstraction: Feature 29 (IToolOrchestrator), Feature 29 (FileGenerateToolExecutor).
- **Feature 163:** File Editing (H4) — AI requests file modification → file picker → AI suggests changes → DiffPlex diff preview → user confirms → applies changes. Warning if file changed externally since AI last read it. Enforces wiki directory exclusion. Uses Wave 1 abstraction: Feature 29 (IToolOrchestrator), Feature 29 (FileEditToolExecutor), DiffPlex (library, not a formal abstraction in [`planning/abstractions.md`](planning/abstractions.md)).
- **Feature 164:** Tool Auto-Approval Settings (H5) — Global defaults (Settings → Tools) + per-chat overrides (textbox toolbar). Which tools can auto-execute without confirmation. Terminal always requires confirmation regardless of setting. Uses Wave 1 abstraction: Feature 29 (IToolOrchestrator).
- **Feature 165:** Deep Research (H6) — Custom state machine: Plan → Search → Read → Synthesize → Report. Driven by tool-use conversation loop with specialized "deep research" system prompt. Real-time progress display via system messages: "🔍 Planning research…", "📖 Reading source [N]/[M]…", "📝 Synthesizing…". User can cancel. Configurable time limit (default 30 min). Uses Wave 1 abstraction: Feature 29 (IToolOrchestrator), Feature 28 (ISearchProvider), Feature 36 (ILLMProviderService). Reference flow: [`vision/flows/deep-research.md`](vision/flows/deep-research.md).
- **Feature 166:** Wiki Search Tool (H7) — AI queries local wiki index via SQLite FTS5. Returns relevant `.md` file excerpts. AI incorporates into response. Read-only (enforced). Grayed out if wiki not configured. Uses Wave 1 abstraction: Feature 29 (IToolOrchestrator), Feature 29 (WikiSearchToolExecutor), Feature 38 (IWikiService).

### Batch 3Q — Model Comparison

- **Feature 167:** Model Comparison Mode (M1) — Send same prompt to 2-4 Personas simultaneously. Open via "⚖ Compare" button in Studio textbox toolbar or from chat header. Side-by-side transient view in dedicated Model Comparison screen. Uses Wave 1 entity: Feature 7 (Persona), Feature 6 (ModelConfiguration), Feature 8 (ChatThread). Extends Wave 1 abstraction: Feature 36 (ILLMProviderService).
- **Feature 168:** Comparison Setup (M2) — Persona selector (multi-select, 2-4). Horizontal or vertical panel layout toggle. Single shared input field (broadcast mode). Option for per-panel independent inputs (toggle broadcast off). "Start Comparison" button disabled if <2 Personas selected. Uses Wave 1 entity: Feature 7 (Persona). No specific abstraction extension.
- **Feature 169:** Comparison Results (M3) — Each panel shows independent streaming. Panel header: Persona name, response time, token count, cost. All panels stream simultaneously — no panel blocks another. Error per panel (others continue). Uses Wave 1 abstraction: Feature 36 (ILLMProviderService, parallel streams).
- **Feature 170:** Accept Comparison Result (M4) — "Accept" button on preferred panel → appends full conversation to permanent ChatThread (new or existing). Other panels saved as branches (D branching model) or discarded. "Accept All as Branches" option. Uses Wave 1 entity: Feature 8 (ChatThread), Feature 9 (Message). Extends Wave 1 abstraction: Feature 37 (IChatThreadService). Reference flow: [`vision/flows/model-comparison-flow.md`](vision/flows/model-comparison-flow.md).

### Batch 3R — Prompt Library

- **Feature 171:** Prompt Library Browser (J1) — Saved reusable prompts with `{{variables}}`: `{{clipboard}}`, `{{selected_text}}`, `{{date}}`, `{{current_wiki_file}}`. Organized with tags and folders. Select from textbox toolbar dropdown. Variables resolved at insertion time. Search/filter available. Uses Wave 1 entity: Feature 12 (PromptTemplate). No specific abstraction extension.
- **Feature 172:** Prompt Management (J2) — Create, edit, delete prompts. Folder/category organization. Accessible from textbox toolbar → "Prompt Library" button → popup browser. Uses Wave 1 entity: Feature 12 (PromptTemplate). No specific abstraction extension.

### Batch 3S — Personal Wiki / Second Brain

- **Feature 173:** Wiki Directory Configuration (N1) — Select directory of `.md` files during onboarding (Step 4) or in Settings → Wiki. `FileSystemWatcher` monitors for external changes (debounced 500ms). Polling fallback every 30s. Uses Wave 1 entity: Feature 15 (WikiFile). Extends platform abstraction: IWikiFileWatcher (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 174:** Wiki Indexing (N2) — Markdig AST walker extracts from each `.md` file: filename, H1-H6 headings (with anchors), full plain-text content, cross-links (`[text](target.md#heading)`), word count. Stored in SQLite WikiFile index table. FTS5 virtual table for full-text search. Auto-updates on file change. Uses Wave 1 abstraction: Feature 38 (IWikiService), Markdig (library, not a formal abstraction in [`planning/abstractions.md`](planning/abstractions.md)). Reference pattern: [`external-docs/ref-obsidian-wiki-system.md`](../external-docs/ref-obsidian-wiki-system.md).
- **Feature 175:** Wiki Search (N3) — Dedicated search scope for wiki entries. Queries WikiFile FTS5 index. Results: filename, matching heading, snippet with highlights. Click opens file in Wiki Browser. Uses Wave 1 abstraction: Feature 38 (IWikiService).
- **Feature 176:** Wiki Browser (N4) — Three-region split: File Tree (collapsible directory tree with `.md` files), Markdown Viewer (Markdig-rendered content + "Open in External Editor" button), Info Panel (Related Sections tab + Backlinks tab + File Info tab: word count, reading time, heading count, last modified). Uses Wave 1 abstraction: Feature 38 (IWikiService), Markdig (library, not a formal abstraction in [`planning/abstractions.md`](planning/abstractions.md)). Reference pattern: [`external-docs/ref-obsidian-wiki-system.md`](../external-docs/ref-obsidian-wiki-system.md).
- **Feature 177:** Write to Wiki — Core Workflow (N5) — "Discuss then confirm" model. Trigger: toolbar button in Studio or context menu. Pipeline: target file selection (existing or new) → AI generates polished `.md` with cross-links via `IWikiService.GenerateWikiContentAsync()` → Preview Panel (editable, rendered Markdown) → Save (creates wiki-version-snapshot first) / Refine in Chat (sends edit instructions back to chat) / Append Only (appends under dated heading) / Cancel. For updates to existing files: mandatory Diff Viewer (DiffPlex) before save. Uses Wave 1 abstraction: Feature 38 (IWikiService), Feature 36 (ILLMProviderService), DiffPlex (library, not a formal abstraction in [`planning/abstractions.md`](planning/abstractions.md)). Reference flow: [`vision/flows/write-to-wiki.md`](vision/flows/write-to-wiki.md).
- **Feature 178:** Automatic Wiki Versioning (N6) — Pre-modification snapshot created before every Write to Wiki save. Max 30 snapshots per file. Total cap 50MB across all snapshots. Oldest auto-deleted when exceeded. Recoverable from Wiki Browser → Version History tab. Uses Wave 1 entity: Feature 16 (WikiVersionSnapshot). Extends Wave 1 abstraction: Feature 38 (IWikiService).
- **Feature 179:** @ Mentions for Wiki Files (N7) — Type `@` in Studio textbox → quick-search dropdown of wiki files (queries WikiFile FTS5). Select file → injects full content (or summarized excerpt if >8K tokens). Mention appears as non-editable chip in textbox. Uses Wave 1 abstraction: Feature 38 (IWikiService).
- **Feature 180:** AI Wiki Access Restrictions (N8) — AI cannot delete or rename wiki files. Write-to-wiki only via N5 pipeline. Enforced at `IToolOrchestrator` level: wiki directory paths excluded from `FileEditToolExecutor` and `FileGenerateToolExecutor`. Read access unrestricted. No specific abstraction extension beyond Feature 29 enforcement.
- **Feature 181:** Append-Only Mode (N9) — Toggle in Write to Wiki Preview Panel. AI appends generated content under dated heading (`## YYYY-MM-DD`). Diff Viewer shows append only (existing content unchanged, new content in green). Uses Wave 1 abstraction: Feature 38 (IWikiService).
- **Feature 182:** AI Cross-Linking — Forward + Backlinks (N10) — Tiered pipeline: AI reads `index.md` (Feature 183) → selects candidate files → requests full content of candidates → generates draft with suggested `[text](target.md#heading)` links → user reviews and accepts in Preview Panel. Backlinks suggested after save: "Would you like to add a link back from [target.md]?" Uses Wave 1 abstraction: Feature 38 (IWikiService), Feature 36 (ILLMProviderService). Reference pattern: [`external-docs/ref-obsidian-wiki-system.md`](../external-docs/ref-obsidian-wiki-system.md).
- **Feature 183:** Auto-Generated index.md (N11) — Generated from local SQLite index data (no AI). Directory tree, all headings with links, cross-links, recently modified files, orphan pages. Regenerated on wiki re-index and manually via Settings → Wiki → "Regenerate index.md." Uses Wave 1 abstraction: Feature 38 (IWikiService).
- **Feature 184:** AI Memory — _memory.md (N12) — Special `_memory.md` wiki file. "Update Memory" button triggers N5 Write to Wiki pipeline with memory-specific prompt. Memory-aware toggle per chat (in textbox toolbar) injects full `_memory.md` content into system prompt. Optional max token cap (truncates with notice). Single API call per message. Uses Wave 1 abstraction: Feature 38 (IWikiService), Feature 36 (ILLMProviderService).
- **Feature 185:** Find & Replace Across Wiki (N13) — Search and replace across all wiki `.md` files. Input: find pattern (regex supported), replace text. Preview: list of files with match count and inline preview of changes. Execute: applies changes, creates snapshots (Feature 178) for undo. Uses Wave 1 abstraction: Feature 38 (IWikiService).

### Batch 3T — Windows OS Integration

- **Feature 186:** HWND Capture & Spatial Anchoring (P2, P3) — Capture active window HWND, source app name (`GetWindowThreadProcessId` + `Process.ProcessName`), document title (`GetWindowText`) before Tier 1 overlay. Save alongside ChatThread for [Apply] back to source. Missing window detection: grayed out [Apply] button. Uses Wave 1 entity: Feature 8 (ChatThread, source context). Extends platform abstraction: IHwndCaptureService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)). Reference pattern: [`external-docs/ref-windows-mcp-automation.md`](../external-docs/ref-windows-mcp-automation.md).
- **Feature 187:** Clipboard Format Preservation (P4) — During Tier 1 capture, detect available `DataFormats` (HTML, RTF, plain text). On Apply, return text in richest available format. Copy MD (raw Markdown) and Copy Rich (HTML/RTF) per message. Extends platform abstraction: IClipboardService (defined in [`planning/abstractions.md §13`](planning/abstractions.md)).
- **Feature 188:** Local WebSocket Server (P5) — ASP.NET Core Kestrel on 127.0.0.1 with configurable port. Token auth (auto-generated, regeneratable). JSON protocol for external integrations (Word Add-in). Configurable in Settings → Tools. Uses Wave 2 feature: Feature 59 (Local WebSocket Server).
- **Feature 189:** System Tray Full Integration (P6) — Full system tray with all context menu actions functional. Recent Chats submenu (last 5). Generation indicator icon animation. Minimize to tray on close behavior. Uses Wave 2 feature: Feature 52 (System Tray Integration).
- **Feature 190:** Session Restore (P7) — Restore previous session's chats and tabs on launch (if Feature 72 enabled). Active tab restored. Scroll positions and panel sizes restored. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.
- **Feature 191:** Per-Monitor DPI Awareness (P8) — Window adapts when moved between monitors with different DPI. Overlay positioning accounts for DPI differences. Crisp rendering at all scaling levels. Uses Wave 2 feature: Feature 54 (Per-Monitor DPI Awareness).

### Batch 3U — Backup & Recovery

- **Feature 192:** Google Cloud Storage Backup (R1) — Full backup: zip SQLite DB + wiki `.md` files + artifacts. Upload to GCS via `GcsBackupProvider`. Credentials (service account JSON key) encrypted via DPAPI. Progress bar during backup. Uses Wave 1 entity: Feature 17 (BackupSnapshot). Extends Wave 1 abstraction: Feature 27 (IBackupProvider, GcsBackupProvider).
- **Feature 193:** Backup Schedule (R2) — Schedule selector: daily, weekly, manual. Default: daily. Background task triggers backup at scheduled time. Missed backups run on next app launch. Uses Wave 1 abstraction: Feature 27 (IBackupProvider).
- **Feature 194:** Manual Backup (R3) — "Backup Now" button in Settings → Backup. Same pipeline as scheduled backup. Progress with estimated time. Uses Wave 1 abstraction: Feature 27 (IBackupProvider).
- **Feature 195:** Restore from Backup (R4) — Browse backup list from GCS (or local folder). Select backup → confirmation: "Restoring will replace all current data. All open chats will be closed." → download → extract → replace local files → restart app. Uses Wave 1 abstraction: Feature 27 (IBackupProvider).

### Batch 3V — Usage Dashboard

- **Feature 196:** Usage Overview Screen (S1) — Comprehensive usage stats: total tokens, total cost, active models count. Summary cards at top of Usage Dashboard. Filterable by provider, model, persona. Uses Wave 1 entity: Feature 14 (UsageRecord). Extends Wave 1 abstraction: Feature 35 (IUsageRepository).
- **Feature 197:** Time Range Filters (S2) — Filter bar: Today, This Week, This Month, Custom Range (date picker), All Time. Default: This Month. All charts and tables update reactively. Uses Wave 1 abstraction: Feature 35 (IUsageRepository).
- **Feature 198:** Usage Charts (S3) — Line chart: tokens over time. Bar chart: cost over time. Pie charts: by provider, by model. Built with LiveCharts2, WPF-native with MVVM binding. Dark/light theme-aware colors. Interactive tooltips. Uses Wave 1 abstraction: LiveCharts2 (library, not a formal abstraction in [`planning/abstractions.md`](planning/abstractions.md)), Feature 35 (IUsageRepository).
- **Feature 199:** Per-Chat Breakdown (S4) — Sortable table: chat title, token count, cost, models used. Click row to open chat. Paginated or virtualized for hundreds of chats. Uses Wave 1 abstraction: Feature 35 (IUsageRepository).
- **Feature 200:** Budget Alerts (S5) — Set monthly spending limit in Settings → Pricing. Warning toast at 80%: "⚠️ You've reached 80% of your monthly budget ($[X] of $[Y])." Option to block API calls when 100% exceeded. Uses Wave 1 abstraction: Feature 35 (IUsageRepository).
- **Feature 201:** AI Feedback Summary (S6) — Aggregated thumbs-up/down per Persona and Model. Approval percentages, trend chart, rankings. Filterable by time range. Uses Wave 1 entity: Feature 9 (Message, feedback). Extends Wave 1 abstraction: Feature 35 (IUsageRepository).

### Batch 3W — Language & RTL

- **Feature 202:** English LTR (Q1) — Default language. All UI labels in English. Left-to-right text rendering. Spell check with English dictionary (Hunspell). Depends on: all screens (Features 44–51, plus Wave 3 screens in prior batches).
- **Feature 203:** Hebrew RTL Detection & Rendering (Q2) — Auto-detect Hebrew via Unicode range (U+0590-U+05FF). If >30% Hebrew characters → `FlowDirection="RightToLeft"` on message container. Code blocks ALWAYS LTR regardless of content. Textbox input direction auto-detected from first strong directional character. Toggle "Auto-detect RTL" in Settings → Language. Depends on: all screens (Features 44–51, plus Wave 3 screens in prior batches).
- **Feature 204:** Mixed LTR/RTL Messages (Q3) — Each text segment renders in correct direction via WPF's built-in Unicode Bidi Algorithm in `FlowDocument`. Explicit `FlowDirection` on `Run` elements for mixed segments. Depends on: Feature 203 (Q2).

### Batch 3X — Onboarding Wizard

- **Feature 205:** Onboarding Wizard — Full Flow — Five-step guided setup on first launch: Welcome → API Keys → Persona → Wiki → Hotkeys → Finish → Launch Studio. Each step skippable. Closed mid-way: completed steps saved, resumes from first incomplete step on next launch. Re-launchable from Settings (pre-populates existing settings). Auto-launches when no API keys configured. Uses Wave 1 entity: Feature 5 (ApiKey), Feature 7 (Persona), Feature 6 (ModelConfiguration). Extends Wave 1 abstraction: Feature 35 (IPersonaRepository), Feature 35 (IApiKeyRepository), Feature 38 (IWikiService). Reference flow: [`vision/flows/first-launch-onboarding.md`](vision/flows/first-launch-onboarding.md).

### Batch 3Y — Additional Studio Features

- **Feature 206:** Clear Conversation (C16) — Clear all messages from chat (preserves chat container). Accessible from chat header three-dot (⋯) menu. Confirmation dialog. Undo via toast ("Conversation cleared. Undo?") or Ctrl+Z. Uses Wave 1 entity: Feature 9 (Message). Extends Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 207:** Chat Header Three-Dot Menu (C16a) — ⋯ menu in chat header: Clear Conversation, Export Chat (Feature 145), Duplicate Chat (Feature 114), Chat Tree (Feature 111), Edit System Message (Feature 107). Context-aware: some items grayed out if not applicable. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.
- **Feature 208:** Message Selection Mode (C18) — Checkboxes appear on message hover. Select multiple messages. Bulk actions bar appears: Copy Selected, Delete Selected, Quote Selected. Uses Wave 1 entity: Feature 9 (Message). No specific abstraction extension.
- **Feature 209:** Offline/Network Status Indicator (C19) — Green/Yellow/Red dot in status bar. Green = connected. Yellow = slow/intermittent. Red = offline. Offline banner below chat header: "You are offline. AI responses are unavailable." Send button disabled when offline. Local features (wiki browsing, chat history, search) remain functional. Uses platform network status API. No specific Wave 1 abstraction extension.
- **Feature 210:** Close Confirmation with Active Generation (C20) — Confirmation dialog when closing tab/window while AI is generating: "A response is still being generated. Close anyway?" Options: Close / Stay. Uses Wave 1 abstraction: Feature 37 (IChatThreadService).
- **Feature 211:** Pin Window / Always on Top (C23) — Toggle in chat header (📌 icon). Sets `Topmost=true` on MainWindow. State remembered across sessions. Uses Wave 2 feature: Feature 41 (MainWindow Shell).
- **Feature 212:** Dark/Light Mode Quick Toggle (C24) — Sun/Moon icon in chat header. Instant toggle between dark and light mode without restart. Uses Wave 2 feature: Feature 55 (Dark/Light Theme System). Extends Wave 1 abstraction: Feature 32 (IThemeProvider).
- **Feature 213:** Font Size Quick Adjust (C25) — A⁻ / A⁺ buttons in chat header. Adjust chat message font size 10-24px. Current size displayed between buttons. Uses Wave 2 feature: Feature 57 (Font Settings). Extends Wave 1 abstraction: Feature 32 (IThemeProvider).
- **Feature 214:** Model Comparison Button (C26) — "⚖ Compare" button in textbox toolbar. Opens Model Comparison screen (Batch 3Q) with current Persona pre-selected. Uses Wave 1 entity: Feature 7 (Persona). No specific abstraction extension.
- **Feature 215:** Dynamic System Message Editing Access (C27) — Click Persona name in chat header → popover with editable system message. Also in three-dot menu and Chat Nav bar. Uses Wave 1 entity: Feature 8 (ChatThread, systemMessage). No specific abstraction extension.
- **Feature 216:** Duplicate/Fork Chat Access (C28) — Right-click message → "Fork from here." Also in three-dot menu → "Duplicate Chat." Uses Wave 1 entity: Feature 8 (ChatThread), Feature 9 (Message). No specific abstraction extension.
- **Feature 217:** Chat Header Full Layout (C29) — Complete header bar: Persona name | context bar (X/Y tokens with color gradient) | cumulative cost | [Source banner if Tier 1 origin] | A⁻ font size A⁺ | ☀ dark/light | 📌 pin | ⋯ menu. Uses Wave 1 entity: Feature 8 (ChatThread). No specific abstraction extension.
- **Feature 218:** Incognito / Temporary Studio Chat (C30) — Toggle chat as temporary (IsTransient=true). 🕶️ indicator on tab. Auto-cleans after 7 days per Feature 136. Re-toggle to make permanent. Uses Wave 1 entity: Feature 8 (ChatThread, isTransient). No specific abstraction extension.
- **Feature 219:** Locked Chats (C31) — Password-protected AES-256-GCM encryption. Global default password + per-chat override. "Hide locked from sidebar" option. Locked chats require password to open tab. ⚠️ Permanent lockout if password lost — no recovery. Clear warning on setup. Uses Wave 1 abstraction: Feature 34 (IChatEncryptionService). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).
- **Feature 220:** Chat Summarization (C32) — "Summarize Chat" in three-dot menu → AI generates summary of current branch → save as artifact or export. Uses Wave 1 abstraction: Feature 36 (ILLMProviderService).
- **Feature 221:** Message Favoriting (C33) — Star (★) toggle on each message. Filter favorited in Chat Nav. Global search filter for favorited messages. Uses Wave 1 entity: Feature 9 (Message). No specific abstraction extension.
- **Feature 222:** Timeline Tab — Transient Actions (L5) — Chronological feed of all Tier 1 + Tier 2 transient actions. Shows: action type icon, preview text (first 80 chars), relative timestamp, source app name. Click to open in Studio (elevation). Auto-cleans entries older than 7 days. Uses Wave 1 entity: Feature 8 (ChatThread, isTransient=true). Extends Wave 1 abstraction: Feature 35 (IChatThreadRepository).

### Testing & DevOps (Wave 3)

- **Feature 223:** Feature-Level Unit Tests — Per-feature unit tests covering service methods, repository queries, ViewModel commands, and branching logic. Uses mocked dependencies. Depends on: each respective Wave 3 feature (Features 62–222).
- **Feature 224:** Service Integration Tests — Integration tests for `ChatThreadService`, `LLMProviderService`, `IWikiService`, `IToolOrchestrator` using real SQLite and mocked external APIs. Depends on: Feature 60 (Integration Test Framework Setup), each respective Wave 3 feature (Features 62–222).
- **Feature 225:** Streaming & Rendering Tests — Tests for progressive Markdown rendering, `StreamChunk` normalization across all 4 providers, and code block fence detection mid-stream. Depends on: Feature 81 (Streaming Response Display), Features 21–24 (Provider implementations).

---

## Wave 4: Cross-Cutting — Features 226–245

Features that span across all vertical slices. Optimization, hardening, and polish.

### Search & Performance

- **Feature 226:** Full-Text Search Optimization — Tune SQLite FTS5 tokenizer (porter stemmer, unicode61). Add content type weighting (title > message content). Search result ranking improvements. Performance testing with 100K+ messages. Depends on: Feature 119 (Full-Text Chat Search), Feature 175 (Wiki Search).
- **Feature 227:** Database Performance Optimization — Index tuning on hot query paths (branch resolution CTE, usage aggregation, FTS5 queries). Connection pooling review. Write serialization audit. VACUUM scheduling. Depends on: all Wave 3 features (Features 62–225).
- **Feature 228:** UI Virtualization & Performance — `VirtualizingStackPanel` with `VirtualizationMode="Recycling"` for all large lists (chat messages, sidebar, wiki file tree, media grid, artifacts list). Image thumbnail caching. Lazy loading for off-screen content. Startup time optimization (R2R compilation, splash screen). Depends on: all Wave 3 features (Features 62–225).
- **Feature 229:** Memory Management — `IDisposable` implementation on all ViewModels. Messenger registration cleanup. DataTemplate memory leak prevention. Long-session memory profiling (10+ hours with multiple tabs streaming). Depends on: all Wave 3 features (Features 62–225).

### Accessibility

- **Feature 230:** Keyboard Navigation Audit — Ensure every UI element is reachable via keyboard (Tab order, access keys). Focus indicators visible in both themes. Screen reader (Narrator) support via WPF `AutomationProperties`. Depends on: all Wave 3 screens.
- **Feature 231:** High Contrast Mode Support — Respect Windows High Contrast theme. Override WPF default styles when high contrast is active. Test with all Windows high contrast variants. Depends on: Feature 55 (Dark/Light Theme System).
- **Feature 232:** Screen Reader Labels & Descriptions — `AutomationProperties.Name`, `HelpText`, and `LabeledBy` on all interactive elements. Live region announcements for streaming completion, errors, and generation start. Depends on: all Wave 3 screens.

### Internationalization

- **Feature 233:** UI String Externalization — Extract all UI strings to resource files (`.resx`). Enable future translation to additional languages beyond English. String review for concatenation, pluralization, and formatting issues. Depends on: all Wave 3 features (Features 62–225).
- **Feature 234:** Hebrew UI Localization — Hebrew translations for all UI strings (buttons, labels, menus, tooltips). RTL layout for all screens with Hebrew UI. Settings → Language → עברית toggle. Depends on: Feature 203 (Hebrew RTL Detection), Feature 233 (UI String Externalization).
- **Feature 235:** Date/Time/Number Localization — Format dates, times, numbers, and currency per selected locale. Chat timestamps, usage dashboard values, token counts. Depends on: Feature 233 (UI String Externalization).

### E2E Testing

- **Feature 236:** End-to-End Test Suite — E2E test project using WinAppDriver or FlaUI for WPF automation. Tests covering: full onboarding flow, send message + receive streaming response, branching workflow, Write to Wiki pipeline, Tier 1 hotkey flow, model comparison flow, import/export flow. CI integration with headless test runner. Depends on: all Wave 3 features (Features 62–225).
- **Feature 237:** Visual Regression Testing — Screenshot-based visual comparison for all 8 screens in both dark and light themes. Reference screenshots stored in repo. CI step flags visual diffs for manual review. Depends on: all Wave 3 screens.

### Security Hardening

- **Feature 238:** Security Audit — Review DPAPI usage, AES-GCM implementation, local WebSocket auth, SQL injection surface (EF Core parameterization), file path traversal in wiki operations, and command injection in terminal tool. Penetration testing of local WebSocket server. Depends on: Feature 34 (Encryption Services), Feature 59 (Local WebSocket Server), Tool Use features (Features 160–166).
- **Feature 239:** Data Integrity Checks — Database integrity check on startup (`PRAGMA integrity_check`). Wiki index consistency verification (compare against disk). Corrupted state recovery (backup restore prompt). Depends on: all data entity features (Features 4–17).

### DevOps & Observability

- **Feature 240:** Crash Reporting — Unhandled exception handler with stack trace logging. Optional telemetry (opt-in) for crash reports. Graceful shutdown with draft save on crash. Depends on: Feature 3 (Logging Infrastructure).
- **Feature 241:** Performance Monitoring — Startup time tracking, API call latency histograms, memory usage tracking. Optional diagnostics dashboard (dev mode). Depends on: Feature 3 (Logging Infrastructure).
- **Feature 242:** CI/CD Pipeline Hardening — Multi-stage pipeline: build → unit tests → integration tests → E2E tests → code sign → MSIX package → deploy to update feed. Version stamping from git tags. Release notes generation. Depends on: Feature 61 (MSIX Packaging Pipeline), all test features (Features 223–225, 236–237).

### Final Polish

- **Feature 243:** Empty State Audit — Verify every screen and sub-panel follows the empty state pattern from [`vision/edge-cases.md`](vision/edge-cases.md): descriptive message + actionable button/link. No blank screens ever. Depends on: all Wave 3 screens.
- **Feature 244:** Error State Audit — Verify every error path (network, API key, file I/O, wiki directory missing, etc.) shows specific, actionable error messages. No generic "Something went wrong." Depends on: all Wave 3 features (Features 62–225).
- **Feature 245:** Edge Case Coverage — Implement all edge cases from [`vision/edge-cases.md`](vision/edge-cases.md) not already covered by feature implementations. Global edge cases: first-time user experience, network errors, permission errors, data limits. Per-feature-group edge cases for A-U. Depends on: all Wave 3 features (Features 62–225).

---

## Feature Count Summary

| Wave | Description | Feature Count | Feature Range |
|------|-------------|---------------|---------------|
| Wave 1 | Foundation — Infrastructure, data model, abstractions, project setup | 40 | 1–40 |
| Wave 2 | Skeleton — App shell, navigation, empty screens, theming | 21 | 41–61 |
| Wave 3 | Vertical Slices — All user-facing features (A-U) | 164 | 62–225 |
| Wave 4 | Cross-Cutting — Search, perf, a11y, i18n, E2E, security, polish | 20 | 226–245 |
| **Total** | | **245** | **1–245** |

---

## Dependency Flow Diagram

```
Wave 1 (Foundation) — Features 1–40
    │
    ├── EF Core DbContext + All 13 Entities + Repositories (4–17)
    ├── All Abstraction Interfaces (ILLMProvider, ITokenizer, etc.) (20–33)
    ├── Encryption Services (34)
    ├── Repository Interfaces (35)
    ├── All Provider Implementations (4 LLM + STT + Backup + Search + ...) (21–30)
    ├── Core Services (IChatThreadService, ILLMProviderService, IWikiService) (36–38)
    └── Testing Infrastructure (39–40)
          │
          ▼
Wave 2 (Skeleton) — Features 41–61
    │
    ├── MainWindow Shell + Screen Routing (41–42)
    ├── All 8 Empty Screen Shells (44–51)
    ├── System Tray + Global Hotkeys + DPI (52–54)
    ├── Theming Infrastructure (Dark/Light + Chat Themes) (55–57)
    ├── Auto-Update + WebSocket Server (58–59)
    └── Integration Test Framework + MSIX Pipeline (60–61)
          │
          ▼
Wave 3 (Vertical Slices) — Features 62–225
    │
    ├── 3A: B — Model Configs & Personas (CRUD) (62–66)
    ├── 3B: A — Settings & Configuration (67–74)
    ├── 3C: B — Persona-Driven Chat Core (75–77)
    ├── 3D: C — Studio Chat Core (messages, streaming, rendering) (78–86)
    ├── 3E: C — Chat Input & Media (drag-drop, paste, audio, camera, spell) (87–92)
    ├── 3F: C — Multi-Tab & Workspace (93–102)
    ├── 3G: E — Chat Modes & Controls (103–107)
    ├── 3H: D — Message Branching (108–116)
    ├── 3I: L — Chat Organization & Search (117–128)
    ├── 3J: U — Soft-Delete & Trash (129–134)
    ├── 3K: O — Data Model Lifecycle (135–138)
    ├── 3L: K — Three-Tier Interaction System (139–144)
    ├── 3M: I — Import & Export (145–146)
    ├── 3N: F — Artifacts System (147–153)
    ├── 3O: G — Media Library (154–159)
    ├── 3P: H — Tool Use & Agents (160–166)
    ├── 3Q: M — Model Comparison (167–170)
    ├── 3R: J — Prompt Library (171–172)
    ├── 3S: N — Personal Wiki / Second Brain (173–185)
    ├── 3T: P — Windows OS Integration (186–191)
    ├── 3U: R — Backup & Recovery (192–195)
    ├── 3V: S — Usage Dashboard (196–201)
    ├── 3W: Q — Language & RTL (202–204)
    ├── 3X: A8 — Onboarding Wizard (205)
    ├── 3Y: C — Additional Studio Features (206–222)
    └── Testing & DevOps (223–225)
          │
          ▼
Wave 4 (Cross-Cutting) — Features 226–245
    │
    ├── Search & Performance Optimization (226–229)
    ├── Accessibility (Keyboard, High Contrast, Screen Reader) (230–232)
    ├── Internationalization (String Externalization, Hebrew UI, Locale) (233–235)
    ├── E2E Testing (WinAppDriver/FlaUI + Visual Regression) (236–237)
    ├── Security Hardening (Audit + Data Integrity) (238–239)
    ├── DevOps & Observability (Crash Reporting, Perf Monitoring, CI/CD) (240–242)
    └── Final Polish (Empty States, Error States, Edge Cases) (243–245)
```

---

## Reference Implementation Index

| Feature Area | Reference Document | Studied For |
|-------------|-------------------|-------------|
| LLM Providers | [`external-docs/ref-cherry-studio-llm-providers.md`](../external-docs/ref-cherry-studio-llm-providers.md) | Multi-provider adapter pattern, SSE normalization, provider configuration UI |
| Global Hotkeys | [`external-docs/ref-powertoys-global-hotkeys.md`](../external-docs/ref-powertoys-global-hotkeys.md) | Spotlight-style command bar, global hotkey registration, overlay window management |
| Text Injection | [`external-docs/ref-windows-mcp-automation.md`](../external-docs/ref-windows-mcp-automation.md) | UI Automation, text injection strategies, clipboard handling, process execution |
| Chat UX | [`external-docs/ref-chatgpt-desktop-chat-ux.md`](../external-docs/ref-chatgpt-desktop-chat-ux.md) | Streaming Markdown rendering, chat workspace UX, three-tier interaction model |
| Wiki System | [`external-docs/ref-obsidian-wiki-system.md`](../external-docs/ref-obsidian-wiki-system.md) | Personal wiki file tree, Markdown rendering, backlinks, cross-linking |
| Artifacts | [`external-docs/ref-claude-desktop-artifacts.md`](../external-docs/ref-claude-desktop-artifacts.md) | Artifacts panel, version history, thinking/reasoning display |

---

*Roadmap — Lightweight wave map. Features numbered 1–245 across 4 waves. Acceptance criteria are derived by the FD Architect from vision docs. DO NOT implement from this file alone — use [`vision/feature-inventory.md`](vision/feature-inventory.md) and individual feature specs in [`vision/features/`](vision/features/) for detailed behavioral requirements.*
