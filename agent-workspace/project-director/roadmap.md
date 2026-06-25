# Roadmap — MySecondBrain Wave Map

MySecondBrain is a native Windows desktop application — a unified, provider-agnostic AI chat hub that replaces all LLM chat platforms, paired with a personal wiki for turning conversations into lasting knowledge. Features are organized in four waves: Foundation (infrastructure), Skeleton (app shell), Vertical Slices (end-to-end user-facing features), and Cross-Cutting (polish and hardening).

Source documents:
- Vision: [`vision/vision-summary.md`](vision/vision-summary.md), [`vision/feature-inventory.md`](vision/feature-inventory.md)
- Planning: [`planning/architecture.md`](planning/architecture.md), [`planning/abstractions.md`](planning/abstractions.md), [`planning/data-model.md`](planning/data-model.md), [`planning/tech-stack.md`](planning/tech-stack.md), [`planning/platform-notes.md`](planning/platform-notes.md)
- Tech sourcing: [`tech-sourcing.md`](tech-sourcing.md)

---

## Wave 1: Foundation — 4 Features

Core infrastructure that everything else depends on. Built first, extended by all subsequent waves.

### Feature 1 — Solution Scaffold & CI/CD ✅

.NET 8.0 WPF solution with 7 projects (Core, Data, Services, UI, Package, Tests.Unit, Tests.Integration), 15 OSS NuGet packages, MSIX packaging placeholder, and GitHub Actions CI/CD skeleton.

Dependencies: none.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via CI build pipeline.
Status: Built.

### Feature 2 — Dependency Injection Container ✅

All service, repository, and ViewModel registrations via `Microsoft.Extensions.DependencyInjection`. 76+ registrations, 42+ C# interfaces, 13 entity stubs, 8 repository stubs, all provider stubs, 11 ViewModels, and 7 content block renderers.

Dependencies: 1.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via 8 unit tests.
Status: Built.

### Feature 3 — Logging Infrastructure ✅

Serilog with rolling file sink to `%LOCALAPPDATA%\MySecondBrain\logs\`, JSON structured output, and thread/machine enrichment.

Dependencies: 2.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via unit tests.
Status: Built.

### Feature 4 — Data Layer: All Entities, DbContext & Repositories ✅

EF Core `AppDbContext` with SQLite, all 13 entity classes, FTS5 virtual tables, 8 repository implementations, repository interfaces, initial database migration, and migration strategy.

Dependencies: 2.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via 104 data layer unit tests.
Status: Built.

---

## Wave 2: Skeleton — 2 Features

App shell, navigation, all empty screens, theming, and Windows platform infrastructure. Every screen exists as a navigable shell with placeholder content.

### Feature 5 — App Shell, Navigation & Theming

MainWindow three-region shell (sidebar + content + right panel), tabbed navigation with drag-drop reorder, screen routing, and sidebar shell with chat list/Trash/Timeline tabs. All 8 empty screen shells: Studio Chat, Onboarding Wizard, Model Comparison, Settings, Wiki Browser, Usage Dashboard, Media Library, and Global Artifacts Browser. Dark/Light theme system with `DynamicResource` instant toggle, 3 chat visual themes (Classic, Compact, Bubble), and font settings (family, size 10–24px, weight).

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (shell layout: sidebar, tab bar, right panel).

Dependencies: 2, 4.
E2E: Launch app via FlaUI, verify MainWindow title is "MySecondBrain" and three-region shell renders (NavChats sidebar radio button, ChatView content area, RightPanelSplitter with 📄 Artifacts header visible, SidebarSplitter visible). Click each navigation button (NavChats, NavWiki, NavMedia, NavArtifacts, NavUsage, NavSettings) and verify the corresponding view UserControl appears and the right panel hides on non-chat screens. Click ThemeToggleBtn to switch between Dark/Light themes and verify the icon toggles with round-trip consistency. Expand ChatThemeCombo and verify Classic, Compact, and Bubble options are present and individually selectable. Click IncreaseFontBtn and DecreaseFontBtn and verify FontSizeDisplay updates; rapid clicks do not crash the app. Verify all 7 ContentBlockRenderer implementations have correct ascending priority values (MarkdownText=100 through ToolCall=700).
Vision groups: none (infrastructure — enables all screens).

### Feature 6 — Windows OS Platform Infrastructure

System tray integration (`NotifyIcon` with context menu, minimize-to-tray, generation indicator), global hotkey registration (`RegisterHotKey` with `WH_KEYBOARD_LL` fallback for Tier 1 and Tier 2), per-monitor DPI awareness (`PerMonitorV2`), local WebSocket server (Kestrel on 127.0.0.1 with token auth), auto-update framework (`AutoUpdater.NET`), and MSIX packaging with code signing and `.appinstaller` generation.

Dependencies: 2, 5.
E2E: Verify ISystemTrayService resolves as WinFormsSystemTrayService from DI with an 8-item context menu in order (New Chat, Open Studio, Command Bar, separator, Recent Chats, Settings, separator, Exit) and all 5 menu click events fire correctly. Verify UpdateRecentChats populates the Recent Chats submenu with clickable items and shows a disabled placeholder when empty. Verify SetGenerationIndicator swaps the NotifyIcon between normal and generating icons, and restores correctly. Verify IGlobalHotkeyService resolves with 6 default hotkeys registered (CommandBar, Rewrite, Summarize, Explain, Translate, ContinueWriting), system hotkey conflict detection works (Win+D, Win+L, Alt+F4, Alt+Tab detected; Ctrl+Alt+Z not), and register/unregister lifecycle succeeds. Verify PerMonitorV2 is configured in the .csproj and the launched window has positive bounds >200px. Verify ILocalWebSocketServer resolves as KestrelWebSocketServer with a 64-character hex auth token, RegenerateAuthToken produces a new valid token, and the health endpoint at http://127.0.0.1:{port}/health returns 200 OK. Verify IUpdateChecker resolves with both AutoUpdaterDotNet and MsixAppInstallerUpdater implementations, CurrentVersion is non-zero, and UpdateFeedUrl is a valid HTTPS URL. Verify all 4 platform services (ISystemTrayService, IGlobalHotkeyService, ILocalWebSocketServer, IUpdateChecker) are registered as Singletons in DI and resolve concretely.
Vision groups: P (P1, P2, P5, P6, P8 — core platform infrastructure).

### Feature 5b — Visual Design System: Colors, Typography & Spacing

Define a comprehensive design token system — color palette (brand, semantic, neutral), font ramp (10–24px with line heights), spacing scale (4px base unit), corner radius tokens, shadow definitions, and transition durations. Produces an updated WPF `ResourceDictionary` with all tokens consumable via `DynamicResource` by every screen built in Wave 3. Covers both Dark and Light themes. Includes a "Design System" page in Settings → Appearance showing all tokens with live preview.

HTML mock reference: (cross-cutting — applies to all 8 vision screens; tokens designed to match the visual language implied by [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) color scheme).

Dependencies: 5.
E2E: Not applicable — design tokens verified via visual regression tests planned within Feature 21.
Vision groups: cross-cutting (affects all screens).

---

## Wave 3: Vertical Slices — 13 Features

End-to-end features spanning database → service → UI. Each feature adds new code to the Wave 1–2 foundation. Ordered by dependency chain.

### Feature 7 — Model Configurations, API Keys & Personas

API key management (add, edit, delete, test, DPAPI encryption, 8 provider types: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAI-Compatible). Model Configuration CRUD (temperature, max tokens, thinking toggle, pricing, context overflow strategy — SlidingWindow/HardStop/AutoSummarize). Auto-fetch available models from provider APIs. Persona CRUD (system prompt, default model config, chat mode). Persona selection per chat with recently-used ordering. Local open-source model support via OpenAI-Compatible provider. Default profile selection (A2). Speech-to-text provider configuration (A10 — OpenAI Whisper API, local Whisper, Windows Speech).

Dependencies: 4, 5.
E2E: Navigate to Settings → Providers, verify SettingsView loads with default Providers category. Click Add API Key, verify form title "Add API Key" appears, select OpenAI from provider dropdown, enter display name and API key, click Test Key and verify the button is re-enabled after test completes. Save the key and verify it appears in the saved keys list (masked display with copy button). Navigate to Profiles, click New Model Configuration, set display name, select API key from provider combo, set model identifier (gpt-4o) in editable combo, set temperature slider to 0.7, configure context overflow to SlidingWindow, and save. Verify saved config appears in list. Click New Persona, set name and system prompt, select the saved model configuration, choose Standard chat mode, and save. Verify saved persona appears in list. Persona picker dialog (Ctrl+N) verified via UIA element discovery (PersonaPickerDialog, PersonaPickerSearchBox, PersonaPickerList, PersonaPickerSelectBtn) — keyboard shortcut unreliable in automated FlaUI. All created entities self-deleted within each test via 🗑️ buttons + MessageBox confirmation.
Vision groups: B, A2, A10.

### Feature 8 — Settings, Onboarding & Diagnostics

Full Settings screen with 16 categories (Providers, Profiles, Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Diagnostics, Maintenance). Appearance settings (chat themes, font). Dark/Light mode toggle. Notification and streaming settings (sound, disable streaming, per-chat mute defaults). Startup behavior (launch on Windows startup, session restore). Auto-update settings. Database maintenance (VACUUM with before/after size display). Onboarding Wizard — five-step guided first-launch setup (Welcome → API Keys → Persona → Wiki Directory → Hotkeys → Finish), each step skippable, re-launchable from Settings. Onboarding Finish screen's "Import from ChatGPT or Claude" button is present but non-functional until Feature 21 builds the import infrastructure. Diagnostics & Debug Logging — global log level selector (Information/Debug/Verbose) and 8 per-category toggles (LLM API Calls, Tier 1 Hotkey Pipeline, Tier 2 Command Bar, Database, Wiki & File System, WebSocket, Startup & Shutdown, System Integration), "Open Logs Folder" and "Clear Logs" buttons, API key redaction via Serilog `IDestructuringPolicy`.

HTML mock references: [`vision/screens/settings.html`](vision/screens/settings.html), [`vision/screens/onboarding-wizard.html`](vision/screens/onboarding-wizard.html).

Dependencies: 4, 5, 6, 7. (Soft dependency on 21 for onboarding import button functionality.)
E2E: Verify SettingsView loads with correct title. Verify all 16 settings category sidebar items are present (Providers, Profiles, Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Diagnostics, Maintenance) and each shows correct header when selected. In Appearance section, verify Dark and Light theme RadioButtons exist and toggle between them. Verify "🔄 Re-run Onboarding Wizard" hyperlink exists in Settings sidebar. In Diagnostics section, verify log level ComboBox has Information/Debug/Verbose options, change log level between values with round-trip restore, toggle LLM API Calls category off then on, and verify "Open Logs Folder" and "Clear Logs" buttons exist in Log File Management section. Onboarding Wizard 5-step flow tested end-to-end with fresh test database (Feature 9): Welcome→API Keys→Persona→Wiki Directory→Hotkeys→Finish, each step skippable, re-launchable from Settings, wizard not shown after completion.
Vision groups: A (A1, A3–A9, A11), A8, V.

### Feature 9 — E2E Test Suite Rewrite & Authoring Guide

Rewrite all 70+ E2E tests from scratch: switch from `IClassFixture<E2eFixture>` (app restarts per class) to `ICollectionFixture<E2eFixture>` (one launch, all tests). Replace fragile class-level `Dispose()` cleanup with self-cleaning tests that delete created data via the app's own 🗑️ delete buttons within the same `[Fact]` body. Introduce `MSB_DB_PATH` environment variable for a separate test database (`e2e-test.db`) — fresh DB every run, deleted on teardown. Write [`planning/e2e-authoring-guide.md`](planning/e2e-authoring-guide.md) encoding all conventions: fixture pattern, test database, self-cleaning tests, no-dead-time rules, selector strategy, helper conventions, onboarding wizard testing, and MessageBox handling. Extract duplicated helpers (`FindById`, `FindByName`, `FindByNameContains`, `NavigateToSettings`, `SelectSettingsCategory`, `ConfirmMessageBox`, `SetPasswordInput`) into a shared `E2eTestBase` abstract class.

Dependencies: 4, 5, 6, 7, 8. (Tests all Wave 2-3 features F5-F8.)
E2E: Run `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug` and verify exit code 0 with all tests passing. After suite completion, verify the test database (`e2e-test.db`) contains zero user-created entities (no stale API keys, model configs, personas, or chat threads). Verify the test suite runs in a single app launch — only one `[FIXTURE] Launching app` log line and one `[FIXTURE] Cleaning up` log line in the output. Verify the E2E authoring guide exists at the expected path and covers all required sections (fixture pattern, test database, self-cleaning tests, no-dead-time, selector strategy, helper conventions, onboarding wizard testing, MessageBox handling).
Vision groups: none (cross-cutting testing infrastructure — enables quality for all future features).

### Feature 10 — Codebase Realignment (Architecture Evolution)

Align the existing codebase (Features 1-9 built with legacy tool names, 5-tool surface, no skills, WPF-only artifacts) with the updated vision's 10-tool surface, Agent Skills subsystem, WebView2 artifacts panel, and SQLite memory. This is a non-user-facing infrastructure feature — the app should behave identically after completion, but the internal architecture matches the evolved vision.

**Tool Renames & New Executors:**
- Rename `TerminalToolExecutor` → `BashToolExecutor` (Anthropic `bash_20250124` schema, workspace-isolated in `%LOCALAPPDATA%/MySecondBrain/workspace/`)
- Merge `FileGenerateToolExecutor` + `FileEditToolExecutor` → `TextEditorToolExecutor` (Anthropic `text_editor_20250728` schema: view/create/str_replace/insert)
- Add `WebFetchToolExecutor` (read-only HttpClient GET, URL must come from prior web_search results)
- Add `MemoryToolExecutor` (SQLite-backed, Anthropic `memory_20250818` schema)
- Add `SkillLoadToolExecutor` (reads SKILL.md from embedded resources/user dirs, structured XML wrapping with deduplication)
- Add `AskUserInputToolExecutor` (structured WPF confirmation dialogs)
- Add `PresentFilesToolExecutor` (copies workspace files to artifacts directory, triggers WebView2 panel refresh)
- Add `ImageSearchToolExecutor` (Google/Bing Image Search API via ISearchProvider, separate from web_search)
- Keep existing `WebSearchToolExecutor` and `WikiSearchToolExecutor` (already built, no rename needed)
- `ISearchProvider` extended with image search support
- 10 total `IToolExecutor` implementations registered in DI

**Agent Skills Subsystem:**
- Add `ISkillService` interface (discovery, catalog, load, resource listing, activation tracking, dependency detection)
- Add `ISkillLoader` interface (skill_load tool schema, structured XML wrapping, deduplication)
- Implement `AgentSkillService` — scans 4 locations (embedded resources + %LOCALAPPDATA% + .agents/skills/ + .claude/skills/), parses YAML frontmatter
- Implement `StructuredSkillLoader` — reads SKILL.md, strips frontmatter, wraps in `<skill_content>` tags with `<skill_resources>` listing
- Skill discovery at startup (embedded `Skills/anthropic/` in MySecondBrain.UI.dll + filesystem paths)
- Name collision resolution: user overrides built-in, cross-client overrides user

**WebView2 Artifacts Panel:**
- Add `Microsoft.Web.WebView2.Wpf` NuGet package to MySecondBrain.UI.csproj
- Create `ArtifactsWebView2Host` WPF control wrapping WebView2 with theme bridge (dark/light CSS toggle via JS)
- Browser-native rendering: Prism.js (syntax highlighting), marked.js (Markdown), diff2html.js (diffs)
- Workspace-to-artifact pipeline: model writes to workspace → calls present_files → app copies to artifacts dir → WebView2 renders
- Existing WPF artifact placeholders replaced with WebView2 host
- Fallback to WPF rendering if WebView2 runtime unavailable

**Per-Chat Toolbar Toggles:**
- Add Tools dropdown (🔧) with per-tool enable/disable + auto-approval (Auto-Approve/Ask/Disabled)
- Add Skills dropdown (📚) with per-skill checkboxes + "All on/off"
- Add Memory toggle (🧠 Mem) — default OFF, enables memory tool
- New chats inherit global defaults from Settings

**System Prompt Construction:**
- Additive assembly: persona.system_message + behavioral_instructions + date_time_context + platform_context + available_skills block + skill_usage_instructions
- Tools array assembled additively per enabled tools
- Empty persona + everything disabled = no system prompt, empty tools array
- Platform context includes bash availability (Git Bash/WSL detected at startup)

**Workspace Isolation:**
- All bash commands execute in `%LOCALAPPDATA%/MySecondBrain/workspace/`
- Working directory locked to workspace via `Process.StartInfo.WorkingDirectory`
- Absolute paths outside workspace blocked pre-execution (scan for `C:\`, `%`, `~`)
- Wiki directory read-only from bash
- 24h auto-cleanup of workspace files on app startup

**Data Model:**
- Add `MemoryEntry` entity to AppDbContext (SQLite table with Anthropic memory_20250818 schema)
- Add `Skill` metadata record (in-memory only, not persisted to SQLite)
- Entity count: 13 → 15 (MemoryEntry + Skill)
- EF Core migration for MemoryEntry table

**DI Registrations:**
- Register all 10 IToolExecutor implementations
- Register ISkillService (AgentSkillService) as singleton
- Register ISkillLoader (StructuredSkillLoader) as singleton
- Update IToolOrchestrator to manage 10 tools
- Register WebView2 host control in DI

**Unit Tests:**
- Update tests for renamed tool executors (BashToolExecutor, TextEditorToolExecutor)
- Add tests for new tool executors (WebFetch, Memory, SkillLoad, AskUserInput, PresentFiles, ImageSearch)
- Add tests for ISkillService (discovery, catalog, deduplication)
- Add tests for ISkillLoader (wrapping, enum constraint)
- Update DI resolution tests for 10 IToolExecutor implementations

**Knowledge Files:**
- Update `agent-workspace/knowledge/architecture.md` with 10-tool surface, skills subsystem, WebView2
- Update `agent-workspace/knowledge/database.md` with MemoryEntry entity
- Update `agent-workspace/knowledge/frontend-ui.md` with WebView2 artifacts panel, per-chat toolbar toggles
- Update `agent-workspace/knowledge/api-routes.md` with skill_load tool schema

Dependencies: 4, 5, 6, 7, 8, 9.
E2E: Not applicable — infrastructure realignment with no user-facing behavior changes. Verified via existing E2E test suite (all 70+ tests continue to pass) + new/updated unit tests for renamed and added tool executors, skill service, and DI registrations.
Vision groups: H, W, F (infrastructure — aligns codebase with updated vision before building new user-facing features).

### Feature 11 — Codebase Realignment: 14-Tool Surface & Vision Alignment

Bridge the gap between the current codebase (Feature 10's 10-tool Anthropic surface, shared workspace, basic UsageRecord) and the updated vision (14-tool provider-agnostic surface, per-chat workspace, enriched UsageRecord, API History, file viewer tabs, System Info). This is a non-user-facing infrastructure feature — the app should behave identically after completion, but the internal architecture matches the 2026-06-25 vision update.

**Tool Executors (5 NEW, 1 DELETED, 2 MODIFIED):**
- DELETE `TextEditorToolExecutor.cs` — replaced by 5 file operation executors
- NEW `ReadFileToolExecutor.cs` — path validation, approval gate, binary detection, blocked paths
- NEW `ListFilesToolExecutor.cs` — directory listing with recursive option, structured JSON output
- NEW `SearchFilesToolExecutor.cs` — regex search across files, file_pattern glob filter
- NEW `ApplyDiffToolExecutor.cs` — SEARCH/REPLACE block parser, workspace+artifacts scope
- NEW `WriteToFileToolExecutor.cs` — create/overwrite with safety flag, auto-create directories
- MODIFY `BashToolExecutor.cs` → per-chat `workspace/{chat-id}/` (shared workspace → per-chat subdirectory)
- MODIFY `PresentFilesToolExecutor.cs` → per-chat `artifacts/{chat-id}/` (shared artifacts → per-chat subdirectory)

**Tool Orchestrator:**
- Update `IToolOrchestrator` + `ToolOrchestrator.cs` for 14 tools (10 → 14)
- Implement parallel execution (`Task.WhenAll` for independent tools, max 10 concurrent)
- Sequential display with "⚡ Running [N] tools in parallel…" indicator
- Map tool names to new executors

**Tool Schemas:**
- 5 new JSON schemas (`read_file`, `list_files`, `search_files`, `apply_diff`, `write_to_file`)
- Remove `text_editor` schema
- All schemas provider-agnostic (Roo Code pattern — not Anthropic-specific)

**System Prompt:**
- Update `SystemPromptBuilder.cs` for 14 tools, dynamic enable/disable
- Remove `text_editor` instructions
- Add workspace isolation instructions (per-chat paths)

**Approval System:**
- Settings → Tools: 14 tools with out-of-workspace approval for `read_file`/`list_files`/`search_files`
- Blocked paths registry (`C:\Windows\`, `C:\Program Files\`, `.env`)
- Per-chat toolbar: 14-tool dropdown with auto-approval submenus

**Workspace Management:**
- Per-chat `workspace/{chat-id}/` creation on chat creation
- Per-chat `artifacts/{chat-id}/` directory
- Chat deletion: delete `workspace/{chat-id}/` + `artifacts/{chat-id}/` + SQLite records
- Startup: orphan workspace cleanup (directories with no matching chat in SQLite)

**Data Layer:**
- `UsageRecord` entity: migration for 8 new columns (`cacheReadTokens`, `cacheCreationTokens`, `latencyMs`, `tier`, `errorType`, `errorMessage`, `errorStatusCode`, `rawJsonPath`)
- `TextAction` entity: migration for `ChatMode` field (enum: Standard/TextCompletion)
- `Artifact` entity: update `filePath` to include `{chat-id}` prefix
- ILLMProvider: capture cache tokens with provider-specific field mappings, latency, tier, error info

**Skills:**
- Remove cross-client path scanning (`.agents/`, `.claude/`)
- Only scan embedded + `%LOCALAPPDATA%/MySecondBrain/skills/`

**UI:**
- Studio Chat header: "📡 API History" button (C38), "?" help icon (C40)
- Tab bar: file viewer tab support (📄 icon, "Read-Only" badge, C39)
- Input toolbar: 14-tool dropdown with out-of-workspace read approval
- Tool call renderer: 5 new cards (`ReadFileToolCard`, `ListFilesToolCard`, `SearchFilesToolCard`, `ApplyDiffToolCard`, `WriteToFileToolCard`) + parallel execution indicator; remove `TextEditorToolCard`
- Settings: System Info category (19th, Z), updated Tools section (14 tools + out-of-workspace approval)
- Usage Dashboard: filter bar (provider/model/tier), cache breakdown, latency distribution (avg/p50/p95/p99), per-provider/model tables

**API History:**
- Every `ILLMProvider` call appends to `_api_history.json` in per-chat workspace
- JSON format matches vision spec (role/content/ts structure with tool_use/tool_result/reasoning blocks)
- Cleanup on chat deletion

**Knowledge Files:**
- Update all 4 knowledge files to reflect new 14-tool architecture, per-chat workspace, enriched UsageRecord, API History, file viewer tabs

**Unit Tests:**
- Tests for 5 new tool executors (ReadFile, ListFiles, SearchFiles, ApplyDiff, WriteToFile)
- Updated tests for modified executors (BashToolExecutor per-chat workspace, PresentFilesToolExecutor per-chat artifacts)
- Updated DI resolution tests (14 tools, 5 new executors registered)
- `UsageRecord` schema tests for 8 new columns
- `TextAction` schema tests for `ChatMode` field

**E2E Tests:**
- Update existing E2E tests that reference old tool names/selectors (`text_editor` → new tools)
- Verify file viewer tabs, API History button, System Info category exist via AutomationId discovery
- Existing E2E suite (all 70+ tests) continues to pass

Dependencies: 4, 5, 6, 7, 8, 9, 10.
E2E: Not applicable — infrastructure realignment with no new user-facing behavior beyond what was already verified. Verified via existing E2E suite + new/updated unit tests.
Vision groups: H (14-tool surface), X (API History), Y (File Viewer Tabs), Z (App Data Locations), C (Studio Chat header updates), S (enriched UsageRecord), K (TextAction.chatMode).

---

### Feature 12 — Studio Chat — Core Workspace (formerly F11)

Conversation view with VirtualizingStackPanel. Full Markdown rendering (Markdig → WPF FlowDocument — headings, bold, italic, code blocks with AvalonEdit syntax highlighting for 100+ languages, lists, links, tables, blockquotes). Streaming token-by-token progressive rendering with auto-scroll management. Message actions (Send/Stop with partial response preservation, Regenerate, Continue Generation). Copy MD and Copy Rich per message. Auto-generated chat titling via AI. Error handling with specific messages and Retry. Scroll-to-bottom floating button. Clear conversation with undo. Chat header three-dot menu (⋯). Message selection mode with bulk actions. Offline/network status indicator. Close confirmation during active generation. Pin window / Always on top. Dark/Light quick toggle. Font size quick adjust. Chat header full layout (Persona name, context bar, cost, 📡 API History button, "?" help icon, source banner, font size, dark mode, pin, ⋯ menu). File viewer tabs in tab bar (📄 icon + "Read-Only" badge, C39). Incognito/temporary chat toggle. Locked chats (AES-256-GCM, password-protected). Chat summarization via AI. Message favoriting (★). Cross-tab completion alert (green dot). Right panel layout (Artifacts panel top + Chat Nav bottom, resizable divider). [Apply] button shell in chat header (grayed-out validation deferred to Feature 18).

**Language & RTL (Q):** English LTR (default, all UI labels). Hebrew RTL auto-detection (Unicode range U+0590–U+05FF, >30% threshold → RTL on message container). Mixed LTR/RTL rendering per segment (WPF FlowDocument BiDi, code blocks always LTR).

Chat Modes (E): Standard chat mode. Text Completion mode. Thinking toggle with reasoning display. Mute notifications per chat. Dynamic system message editing from Persona header popover.

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (conversation area, thinking blocks, message actions, right panel artifacts + chat nav).

Dependencies: 4, 5, 7, 11.
E2E: Create a new chat with a persona that has a configured API key. Type a message requesting code generation and press Enter. Verify streaming response begins token-by-token with progressive Markdown rendering including a fenced code block with syntax highlighting and a Copy button on hover. Verify Stop button is visible during generation. After completion, verify message footer shows generation time. Click Copy MD on the assistant message, paste into Notepad, verify raw Markdown was copied. Click Copy Rich and verify formatted content on clipboard. Click Regenerate and verify a new response replaces the old one with the original preserved as a branch. Verify chat header shows auto-generated title, persona name, 📡 API History button, "?" help icon, and context window bar. Open the ⋯ menu and verify Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, and Edit System Message options are present.
Vision groups: C (core messaging/rendering), E, Q.

### Feature 13 — Studio Chat — Input, Media & Prompts

Drag & drop files/media into textbox. Paste image from clipboard (Ctrl+V) with thumbnail preview. **Attach File button (📎) in textbox toolbar** — opens standard Windows file picker, multi-select, displays selected files as attachment cards below textbox. **Model-aware file type compatibility** — checks active Model Configuration's provider/model capabilities; warns if attached file types are unsupported (e.g., "⚠️ [Model] does not support [file type]. Attached as metadata only."). Audio input via microphone (NAudio → configured STT provider). Camera capture (webcam photo for vision models). Spell check with Hunspell (red squiggly, right-click suggestions, custom dictionary). Multiple chat tabs (drag-drop reorder, close, reopen). Token usage and context display (real-time local tokenizer, color gradient bar). Keyboard shortcuts (Ctrl+N/W/Tab/F/S). Resizable panels (sidebar, right panel). Textbox toolbar with Persona selector, thinking toggle, mute toggle, 14-tool dropdown (🔧 with out-of-workspace read approval), Skills dropdown (📚), Memory toggle (🧠 Mem), and prompt library access. Auto-save drafts every 5 seconds with crash recovery. Textbox input direction auto-detection based on first strong directional character typed (Hebrew → RTL, English → LTR).

Prompt Library (J): Saved reusable prompts with dynamic variables ({{clipboard}}, {{selected_text}}, {{date}}, {{current_wiki_file}}). Organize with tags and folders. Select to insert from textbox toolbar.

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (input area, toolbar, attachment row, textbox + send, token count).

Dependencies: 4, 5, 7, 11.
E2E: In an active Studio Chat, click the 📎 Attach File button in the textbox toolbar, select a .txt file and a .png image from the file picker, and verify both appear as attachment cards below the textbox with filenames and remove buttons. Verify model-aware compatibility warnings appear on unsupported file types. Paste an image from clipboard via Ctrl+V and verify it appears as a thumbnail in the attachment row. Click the microphone button, verify recording indicator pulses red, click again to stop, and verify transcribed text appears in the textbox. Open the Prompt Library, select a saved template, and verify it inserts into the textbox with variables resolved. Verify the real-time token counter in the chat header updates as text is typed, showing a colored bar transitioning from green to yellow to red as the context limit approaches.
Vision groups: C (input/media), J.

### Feature 14 — Message Branching & Chat Organization

Message Branching (D): Edit any past message (Edit in Place or Edit as Branch). **Quick Branch (↳ Branch) button** on each assistant message — creates a branch instantly without opening the editor, separate from the edit-then-branch flow. Delete any past message (branch data preserved). Branch navigation with indicator ("2/3") and cycle arrows. Chat tree visualization (nodes = messages, edges = parentMessageId). Quote from chat (selected text → quoted block in input). Chat navigation bar (collapsible scrollable message list, click to jump). Duplicate/Fork chat from any message point. Message feedback (thumbs-up/down). Undo/Redo message edits (Ctrl+Z/Y, per-chat stack, depth 50).

Chat Organization (L): Sidebar chat list with sort options, date grouping, pinned section. Chat favoriting with filter. Full-text chat search via SQLite FTS5 (snippets, highlights, click to open). Sidebar filtering (permanent vs. Timeline transient). Chat tags/labels with autocomplete. Pin chats. Chat folders/collections. Chat archiving. Bulk operations. Right-click context menu. Chat color labels. Timeline tab (chronological Tier 1/2 transient action feed).

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (sidebar chat list with tags, folders, sort; branching UI with indicators; message action buttons).

Dependencies: 4, 5, 11.
E2E: In a chat with multiple messages, click the edit icon on a past user message, modify text, select Edit as Branch, and verify branch indicator "2/2" appears. Click the ↳ Branch button on an assistant message and verify a new branch is created instantly. Click branch navigation arrows to cycle between versions and verify subsequent messages re-render. Open the Chat Tree visualization, verify nodes represent messages, click a node to navigate to that branch point. Select text in a past message, click Quote, and verify the selected text is inserted as a Markdown blockquote in the textbox. In the sidebar, favorite a chat via star icon, assign tags, pin a chat to the top, and verify all persist. Press Ctrl+Shift+F to open global search, type a query, verify FTS5 results appear with highlighted snippets grouped by chat, click a result to navigate to the matching message.
Vision groups: D, L.

### Feature 15 — Data Lifecycle & Soft-Delete Trash

Data Lifecycle (O): Unified ChatThread model (Tier 1/2/3 all create identical ChatThread + Message data). IsTransient flagging. Chat elevation (sending reply in transient thread in Studio flips to permanent; elevation triggers on send action, even if API call fails). 7-day auto-cleanup of transient threads with exceptions (favorited, tagged, pinned, archived, has user replies or artifacts — auto-elevated). Garbage collection policy. Database compaction (VACUUM).

Soft-Delete Trash (U): Soft-delete on chat deletion (30-day Trash). Trash view with Restore and Delete Permanently. 30-day auto-purge. Restore preserves folder, tags, pinned status. Permanent delete with garbage collection cascade (including workspace/{chat-id}/, artifacts/{chat-id}/, and _api_history.json). Empty Trash with confirmation.

Dependencies: 4, 5, 14.
E2E: Right-click a chat in the sidebar and select Delete. Verify confirmation dialog warns about 30-day retention. Click Move to Trash and verify toast confirms the action. Navigate to 🗑️ Trash tab, verify the deleted chat appears with deletion date, Restore button, and Delete Permanently button. Click Restore and verify the chat reappears in the main list with its original folder, tags, and pinned status preserved. Delete another chat, navigate to Trash, click Delete Permanently, verify confirmation dialog warns about permanent data loss, confirm, and verify the chat is gone. Click Empty Trash, verify confirmation with item count, confirm, and verify all items are permanently removed.
Vision groups: O, U.

### Feature 16 — Artifacts & Media Library

**Artifacts (F):** AI-generated named artifacts (code, docs, config files) with type inferred from language/extension. Side panel artifact list in right panel (click to view content). Version history per artifact (v1, v2, v3…). Diff view between any two versions (DiffPlex, side-by-side or unified). Version switching with branching on revert. Artifact viewer (syntax highlighting for code, rendered view for Markdown). "Save to Disk" and "Save to Wiki" buttons. Global Artifacts Browser screen (cross-chat listing with search, sort, and filter). Per-chat artifacts directory `artifacts/{chat-id}/`.

**Media Library (G):** Browsable gallery grid of all media across chats (virtualized). Filtering by type (image, audio, video), source chat, and date range. Search by filename. Media actions (view/play, download, copy to clipboard, open in system app, delete, navigate to source chat). AI image generation inline in chat. AI audio generation with inline player. Inline media rendering in messages (images clickable for full resolution, audio mini player with play/pause/seek, video with embedded player). "Save to Disk" and "View in Library" buttons on all media.

HTML mock references: [`vision/screens/global-artifacts-browser.html`](vision/screens/global-artifacts-browser.html), [`vision/screens/media-library.html`](vision/screens/media-library.html).

Dependencies: 4, 5, 11.
E2E: Trigger an AI generation that produces an artifact (e.g., "Create a Python Flask app in an artifact"). Verify the artifact appears in the right panel under 📄 Artifacts with its name and type. Click the artifact to view its content with syntax highlighting. Request a change and verify version v2 is created. Open version history, select v1 and v2, click Compare, and verify DiffPlex side-by-side diff shows red/green changes. Click Save to Disk and verify the file is written. Navigate to Global Artifacts Browser via sidebar, verify all artifacts across chats are listed with name, type, parent chat, date, and version count, and filter by type. Navigate to Media Library screen, verify a gallery grid of all media items with filtering by type, click an image to view full resolution, and verify View in Library navigates to the source chat.
Vision groups: F, G.

### Feature 17 — Tool Use, Agent Capabilities & Skills

14-tool provider-agnostic agent surface: read_file, list_files, search_files, apply_diff, write_to_file (file operations — replacing text_editor), bash (per-chat workspace-isolated cmd.exe with Git Bash/WSL fallback), web_search (Google Custom Search / Bing API), web_fetch (read-only HttpClient GET), image_search (Google/Bing Image Search API), memory (SQLite-backed, Anthropic memory_20250818 schema, per-chat toggle), wiki_search (local SQLite FTS5, read-only, zero API cost), skill_load (activates Agent Skills with enum-constrained schema), ask_user_input (structured WPF confirmation dialogs, always available), present_files (bridges per-chat workspace to per-chat artifacts). Parallel tool execution (Task.WhenAll for independent tools, max 10 concurrent). Tool auto-approval settings (global defaults in Settings → Tools + per-chat overrides in textbox toolbar). Out-of-workspace read approval for read_file/list_files/search_files (Auto-Approve/Ask/Disabled per tool). Hard-coded overrides: bash writes outside workspace and apply_diff/write_to_file outside workspace ALWAYS require confirmation or are blocked. Blocked paths always denied.

Skills subsystem: 11 built-in Anthropic skills (xlsx, docx, pdf, pptx, algorithmic-art, canvas-design, frontend-design, theme-factory, web-artifacts-builder, webapp-testing, skill-creator) shipped as embedded resources. Progressive disclosure: catalog (~80 tokens/skill) in system prompt → full SKILL.md on skill_load → resources on demand. Skill discovery from 2 locations: embedded resources + %LOCALAPPDATA%/MySecondBrain/skills/ only. Community skills with source annotation. Per-chat Skills dropdown with individual toggles. skill-creator meta-skill for user-created skills. Deep Research as skill (not custom state machine) — model follows research protocol using web_search + web_fetch + bash, progress visible naturally as tool calls stream. Memory tool — SQLite-backed discrete fact entries, per-chat toggle, user management in Settings → Memory. Per-chat toolbar: Tools dropdown (🔧), Skills dropdown (📚), Memory toggle (🧠 Mem).

WebView2 artifacts panel: embedded Microsoft Edge WebView2 control for browser-native rendering (syntax highlighting via Prism.js/highlight.js, Markdown via marked.js, diff views via diff2html.js, interactive React/Tailwind artifacts from web-artifacts-builder skill). Workspace-to-artifact pipeline: model creates files in per-chat workspace via write_to_file/apply_diff/bash → calls present_files → app copies to per-chat artifacts directory → renders in WebView2 panel. Version tracking by filename within chat. Chat conversation stays WPF-native (FlowDocument + ContentBlockRenderers).

System prompt additive assembly: persona.system_message + behavioral_instructions + date_time_context + platform_context + available_skills block (only if ≥1 skill enabled) + skill_usage_instructions. 14 tools array additively assembled per enabled tools. Empty persona + everything disabled = no system prompt, empty tools array.

Dependencies: 4, 5, 7, 11.
E2E: In Studio Chat with tools enabled, send a message requesting a web search. Verify the tool call appears as a styled system message showing the search query, results feed back to the AI, and a summarized response appears. Request the AI to create an Excel file (triggering the xlsx skill), verify skill_load tool call appears, then bash and write_to_file tool calls create the file. Click present_files and verify the artifact appears in the WebView2 side panel with syntax highlighting. Send a message requesting image search, verify image results appear with thumbnails. Toggle the Memory toggle on, ask the AI to remember a fact, verify memory tool call stores it, then ask the AI what it knows about you and verify it retrieves the fact. Toggle Skills dropdown to disable xlsx, send a spreadsheet request, verify the skill catalog does not include xlsx and skill_load enum does not list it. Trigger Deep Research with a query, verify the research protocol runs via web_search → web_fetch → bash tool calls with natural progress visibility (no custom state machine UI), and the final cited report appears as an artifact in the WebView2 panel. Verify parallel execution indicator "⚡ Running [N] tools in parallel…" appears when multiple independent tools are requested.
Vision groups: H, W, F (artifacts panel).

### Feature 18 — Text Actions & Three-Tier System

Text Actions CRUD with four-dimensional configuration: capture scope (any combination of selection, focusedElement, surroundingContext, fullDocument, screenshot), system prompt + model config + chatMode (Standard/TextCompletion), and apply mode (replaceSelection, insertAtCursor, replaceFocusedElement, appendToFocusedElement, prependToFocusedElement, clipboardOnly, showOnly). "Continue Writing" defaults to TextCompletion mode. Ten built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt, Continue Writing, Improve Flow, Summarize Page, Explain Screen. Textbox toolbar with Text Actions dropdown and preview popup.

**Tier 1 — Global Hotkey Text Actions:** Three-phase flow per TextAction configuration. Capture Phase — graduated UIA pipeline per captureScope flags (TextPattern → ValuePattern → TreeWalker → DocumentRange → screenshot via PrintWindow/BitBlt). Result Phase — editable AI output with Accept/Discard/Open in Studio/Save to Wiki/Retry and Additional Instructions field. Apply Phase — per applyMode injection (HWND, UIA TextPattern/ValuePattern, clipboard) with layered fallbacks and confirmation toast. Orthogonal elevation actions (Open in Studio, Save to Wiki) available regardless of apply mode.

**Tier 2 — Command Bar (Alt+Space):** Spotlight-style centered overlay with inline state (input field + Q&A display + Pop-out/Close/Copy controls) and popped-out state (floating resizable mini-window with Open in Studio, Pin, Minimize, Close). Elevation to Studio creates permanent ChatThread. Dismissal saves as transient thread.

**Tier 3 — Studio Chat:** Full workspace (built in Feature 12). All three tiers share the same ChatThreadService and data model. Elevation from Tier 1/2 flips IsTransient to false.

HTML mock reference: (no dedicated screen mock — Tier 1/2 are overlay windows; Text Actions configuration lives in [`vision/screens/settings.html`](vision/screens/settings.html) categories ⚡ Text Actions and ⌨️ Hotkeys).

Dependencies: 4, 5, 6, 7, 11.
E2E: Press Alt+Space to invoke Tier 2 Command Bar. Verify a centered overlay appears with placeholder "Ask anything…". Type a question, press Enter, and verify the bar expands with streaming compact Markdown response. Click the Pop-out button and verify the bar detaches into a floating resizable mini-window with Open in Studio, Pin, Minimize, and Close controls. Click Open in Studio and verify the conversation becomes a permanent ChatThread. Press Alt+Q (Rewrite) in a text editor with text selected, verify the Thinking… pill overlay appears near cursor, then the result popup shows the Text Action name, source application, editable transformed text, and Accept/Discard/Open in Studio/Save to Wiki/Retry buttons. Click Accept and verify the result applies to the source editor per the apply mode with a confirmation toast. Navigate to Settings → Text Actions, verify 10 built-in defaults are listed. Create a custom Text Action with chatMode TextCompletion, capture scope fullDocument+screenshot, and apply mode clipboardOnly, assign a hotkey, and verify it appears in the list.
Vision groups: K, P9.

### Feature 19 — Personal Wiki / Second Brain

Wiki directory configuration (user selects directory of .md files, FileSystemWatcher with 500ms debounce and polling fallback monitors external changes). Wiki indexing (Markdig AST walker extracts headings, cross-links, word count, plain text content; stored in SQLite wiki index tables with FTS5 for full-text search). Wiki search (dedicated scope with results showing filenames, headings, snippets; click opens in Wiki Browser). Wiki Browser screen — three-region split: file tree (collapsible directory tree), Markdown viewer (rendered content with "Open in External Editor" button), and info panel (Related Sections tab + Backlinks tab + File Info tab with word count, reading time, heading count). **💬 Discuss with AI button** in Wiki Browser — creates a new chat (or opens existing) with the current file's full content pre-loaded as context, enabling wiki→chat→Write to Wiki loop.

**Write to Wiki Pipeline:** "Discuss then confirm" model. Trigger from toolbar or context menu. Pipeline: target file selection → AI generates polished .md with cross-links → Preview Panel (editable, with Append-Only toggle for dated-heading appends) → Save/Refine in Chat/Append Only/Cancel. For updates to existing files: mandatory Diff Viewer before save.

**Versioning & Git:** Automatic pre-modification snapshots (max 30 per file, 50MB total cap, recoverable from Wiki Browser). Optional git version control — initialize git repo from Onboarding Wizard or Settings, auto-commit on file change with 30-second debounce, optional GitHub remote push with DPAPI-encrypted personal access token. Snapshots and git coexist (snapshots for instant undo, git for cross-session history).

**Knowledge Features:** @ mentions for wiki files (type @ in textbox → quick-search dropdown → inject full content or summarized excerpt if >8K tokens). AI wiki access restrictions (no deletions, no renaming, write only via N5 pipeline, wiki directory read-only from bash and write_to_file/apply_diff). wiki_search tool (H10) queries local SQLite FTS5 wiki index for AI agent use. AI cross-linking — tiered pipeline: AI reads auto-generated index.md → selects candidates → requests full content → generates draft with suggested links → user reviews and accepts. Backlinks suggested after save. Auto-generated index.md at wiki root (directory tree, all headings with links, cross-links, recently modified, orphan pages). AI memory is handled by the separate `memory` tool (SQLite-backed, Anthropic schema) — NOT `_memory.md` wiki file. Find and replace across all wiki files with preview of changes and regex support (snapshots provide undo).

HTML mock reference: [`vision/screens/wiki-browser.html`](vision/screens/wiki-browser.html).

Dependencies: 4, 5, 11.
E2E: In Settings → Wiki, select a wiki directory containing .md files and verify the path displays. Navigate to Wiki Browser via sidebar, verify the three-region split: left file tree, center Markdown viewer rendering the selected file with headings/links/code blocks, right info panel with Related Sections, Backlinks, and File Info tabs. Click a file in the tree and verify File Info tab shows word count, reading time, and heading count. Click Open in External Editor and verify the file opens in the system default .md editor. Click 💬 Discuss with AI and verify a new Studio chat opens with the file's content pre-loaded as context. In Studio, have a conversation, click Write to Wiki, choose Create new wiki file, enter a filename, and verify AI generates a polished .md summary with suggested cross-links highlighted. Review and edit in the Preview Panel, click Save to Wiki, and verify the file is written with a confirmation toast. For an update to an existing file, verify the mandatory Diff Viewer appears before Commit to Wiki is clickable.
Vision groups: N.

---

## Wave 4: Cross-Cutting + Polish — 4 Features

Features that span across vertical slices. Smaller independent features combined with optimization, hardening, polish, and motion design.

### Feature 20 — Model Comparison, Backup & Recovery

**Model Comparison (M):** Send same prompt to 2–4 Personas simultaneously. Side-by-side comparison with independent streaming panels (horizontal or vertical layout). Each panel shows Persona name, response time, token count, and cost. Broadcast mode toggle (typing in one input sends to all). Accept result appends to permanent ChatThread; others saved as branches or discarded. "Accept All as Branches" option.

**Backup & Recovery (R):** Full backup of SQLite database, wiki .md files, and artifacts. Google Cloud Storage backup (zip → upload via GCS SDK, DPAPI-encrypted credentials). Local folder backup alternative (zero-dependency). Backup schedule (daily, weekly, manual; default: daily). Manual "Backup Now" button with progress. Restore from backup (browse list, download, replace, restart with confirmation dialog and warning).

HTML mock reference: [`vision/screens/model-comparison.html`](vision/screens/model-comparison.html).

Dependencies: 4, 5, 7, 12.
E2E: In Studio Chat, click the ⚖ Compare button in the textbox toolbar. Select 2-3 Personas via checkboxes (verify Start Comparison is disabled until ≥2 selected), enter a prompt, choose horizontal layout, and click Start Comparison. Verify side-by-side panels stream responses independently with persona name, model name, and real-time metrics. Toggle 🔗 Broadcast mode on and verify per-panel inputs are replaced by a single centered broadcast input. Send a follow-up and verify it goes to all panels. Click Accept on one panel and verify the accepted conversation is appended to the originating chat with other conversations auto-saved as branches, confirmed by toast. Navigate to Settings → Backup, configure a local folder backup destination, click Backup Now, verify progress bar completes with a confirmation toast. Set backup schedule to Daily. Click Restore from Backup, verify available backups are listed with dates and sizes, select one, confirm the warning dialog, and verify restore completes.
Vision groups: M, R.

### Feature 21 — Data Portability, Analytics, Localization & Hardening

**Import & Export (I):** Export chat as Markdown (QuestPDF for PDF, JSON). Import from ChatGPT export JSON and Claude export JSON with duplicate detection. Imported chats created as new ChatThreads.

**Usage & Pricing Dashboard (S):** Enriched usage overview screen with summary cards (total tokens, total cost, cache hit rate %, avg latency, most used model, total API calls). Provider and model filter dropdowns (multi-select). Time range filters (Today, This Week, This Month, Custom Range, All Time). Usage charts (LiveCharts2 — line chart tokens/time, bar chart cost/time, cache breakdown, latency distribution with avg/p50/p95/p99). Per-provider and per-model breakdown tables (sortable). Per-chat breakdown table (sortable, click to open chat). Budget alerts (monthly spending limit, 80% warning toast, option to block API calls at 100%). AI feedback summary (aggregated thumbs-up/down per Persona and Model, approval percentages, trend chart, rankings).

**Settings expansion:** Settings → System Info category (19th, Z) — comprehensive reference table of all app data locations with paths, purposes, sizes, and editability badges. Settings → Tools updated for 14 tools with out-of-workspace read approval per read-tool.

**Platform Refinements (P):** Session restore (reopen all chats and tabs from previous session on launch, respects startup setting). HWND validation for Tier 1 [Apply] button state (grayed out when source window closed). Clipboard format preservation refinements (HTML/RTF format-aware capture and restoration for Tier 1).

**Testing, Performance & Security:** E2E testing with FlaUI for WPF automation (full onboarding, send message + streaming, branching workflow, Write to Wiki pipeline, Tier 1 hotkey flow, model comparison, import/export). Visual regression testing (screenshot-based comparison for all 8 screens in dark and light themes). Performance optimization (SQLite FTS5 tuning, `VirtualizingStackPanel` with Recycling for all large lists, image thumbnail caching, R2R compilation, memory management, `IDisposable` audit). Accessibility (keyboard navigation audit, high contrast mode support, screen reader labels via `AutomationProperties`, live regions for streaming and errors). Security hardening (DPAPI and AES-GCM audit, WebSocket auth review, SQL injection surface check, file path traversal prevention, crash reporting with graceful shutdown). Empty state and error state coverage for all screens and panels (descriptive messages with actionable buttons, no blank screens or generic errors).

HTML mock references: [`vision/screens/usage-dashboard.html`](vision/screens/usage-dashboard.html), [`vision/screens/settings.html`](vision/screens/settings.html) (import/export, analytics sections).

Dependencies: all Wave 3 features.
E2E: In a chat with multiple messages, press Ctrl+S, select Markdown format, pick a save location, and verify the exported .md file contains all messages with roles, timestamps, and code blocks. Export as PDF and verify the .pdf renders with formatting preserved. Navigate to Settings → Import, select a ChatGPT export JSON file, verify the preview shows chat title, message count, and date range, click Import, and verify a new ChatThread appears in the sidebar with messages and timestamps preserved. Repeat with a Claude export JSON. Navigate to Usage Dashboard, verify summary cards display total tokens, total cost, cache hit rate, avg latency, most used model, and total API calls. Select provider/model filters and verify tables update. Select This Month filter, verify line chart (tokens/time), bar chart (cost/time), cache breakdown, and latency distribution with avg/p50/p95/p99 update. Click a row in the per-chat breakdown table to open that chat. Verify AI Feedback Summary shows approval percentages per Persona and Model with trend charts. Navigate to Settings → System Info, verify the app data locations table with paths, sizes, and Open in Explorer buttons. Close and relaunch the app, verify session restore reopens all previously open chats and tabs when enabled in Settings → Startup. Verify the Tier 1 [Apply] button is grayed out with "Source application is no longer available" when the source window has been closed.
Vision groups: I, S, Z, P (P3, P4, P7 — platform refinements).

### Feature 22 — UI Polish: All Screens Visual Refinement Pass

Apply the Visual Design System (Feature 5b) consistently across all 8 screens. Implement proper spacing, typography, color application, empty state illustrations, and responsive panel behavior. Ensure every screen looks professional and polished — no placeholder gray-on-gray look. Addresses: card/panel elevation and borders, consistent header styling, focus indicators, scrollbar styling, and sidebar visual hierarchy. Includes visual regression test snapshots for all 8 screens in both Dark and Light themes.

HTML mock references: all 8 vision screens as visual targets — [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html), [`vision/screens/wiki-browser.html`](vision/screens/wiki-browser.html), [`vision/screens/media-library.html`](vision/screens/media-library.html), [`vision/screens/global-artifacts-browser.html`](vision/screens/global-artifacts-browser.html), [`vision/screens/usage-dashboard.html`](vision/screens/usage-dashboard.html), [`vision/screens/settings.html`](vision/screens/settings.html), [`vision/screens/onboarding-wizard.html`](vision/screens/onboarding-wizard.html), [`vision/screens/model-comparison.html`](vision/screens/model-comparison.html).

Dependencies: all Wave 3 features, 5b.
E2E: Not applicable — visual consistency verified via planned visual regression test snapshots for all 8 screens in Dark and Light themes.
Vision groups: cross-cutting (affects all screens).

### Feature 23 — UI Polish: Micro-interactions & Motion Design

Add subtle motion and interaction feedback throughout the app: hover transitions on buttons/list items (150ms color fade), smooth panel resize animations, tab open/close transitions, message appear/fade-in during streaming, scroll-to-bottom smooth behavior, toast notification slide-in/out, thinking block expand/collapse animation, sidebar collapse/expand transition, overlay fade-in for Tier 1 pill and Tier 2 command bar. All animations respect the Windows "Turn off all unnecessary animations" accessibility setting (`SystemParametersInfo` SPI_GETCLIENTAREAANIMATION).

HTML mock reference: (cross-cutting — tooltip toast behavior and hover states shown in [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html); animation timing inspired by Windows 11 Fluent design language).

Dependencies: 5b, 21.
E2E: Not applicable — motion design verified via automated animation timing tests and visual inspection; all animations respect Windows accessibility setting.
Vision groups: cross-cutting (affects all screens).

---

## Feature Count Summary

| Wave | Description | Features |
|------|-------------|----------|
| Wave 1 | Foundation — Infrastructure, data model, abstractions | 4 (built) |
| Wave 2 | Skeleton — App shell, navigation, theming, Windows infrastructure, design system | 3 |
| Wave 3 | Vertical Slices — All user-facing features (DB → service → UI) + Codebase Realignments | 13 |
| Wave 4 | Cross-Cutting + Polish — Smaller features, optimization, hardening, polish, motion | 4 |
| **Total** | | **24** |

---

## Dependency Flow Diagram

```
Wave 1: Foundation (4 built)
  F1 ✅  Solution Scaffold & CI/CD
  F2 ✅  DI Container
  F3 ✅  Logging Infrastructure
  F4 ✅  Data Layer (entities, DbContext, repos)
    │
    ▼
Wave 2: Skeleton + Design System (3)
  F5 ─── App Shell, Navigation & Theming
  │
  ├── F5b ── Visual Design System: Colors, Typography & Spacing
  │
  F6 ─── Windows OS Platform Infrastructure
    │
    ├──────────┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
    ▼          ▼          ▼          ▼          ▼          ▼          ▼          ▼
Wave 3: Vertical Slices + Codebase Realignments (13)
  F7 ─── Model Configs, API Keys & Personas (B + A2 + A10)
  │
  ├── F8 ─── Settings, Onboarding & Diagnostics (A + A8 + V) [soft dep on 21]
  │
  ├── F9 ─── E2E Test Suite Rewrite & Authoring Guide (tests F5-F8)
  │
  ├── F10 ── Codebase Realignment: 10-Tool Surface & Skills (H + W + F infrastructure) [depends on F4-F9]
  │
  ├── F11 ── Codebase Realignment: 14-Tool Surface & Vision Alignment (H + X + Y + Z + S + K) [depends on F4-F10]
  │
  ├── F12 ── Studio Chat — Core Workspace (C + E + Q) [depends on F11]
  │     │
  │     ├── F13 ── Studio Chat — Input, Media & Prompts (C + J)
  │     │
  │     ├── F14 ── Message Branching & Chat Organization (D + L)
  │     │     │
  │     │     └── F15 ── Data Lifecycle & Soft-Delete Trash (O + U)
  │     │
  │     ├── F16 ── Artifacts & Media Library (F + G)
  │     │
  │     └── F17 ── Tool Use, Agent Capabilities & Skills (H + W + F)
  │
  ├── F18 ── Text Actions & Three-Tier System (K + P9)
  │
  └── F19 ── Personal Wiki / Second Brain (N)
    │
    ├──────────┬──────────┬──────────┐
    ▼          ▼          ▼          ▼
Wave 4: Cross-Cutting + Polish (4)
  F20 ── Model Comparison, Backup & Recovery (M + R)
  F21 ── Data Portability, Analytics, Localization & Hardening (I + S + Z + Q + P refinements + testing)
  F22 ── UI Polish: All Screens Visual Refinement Pass
  F23 ── UI Polish: Micro-interactions & Motion Design
```

---

*Feature behavioral details are specified in [`vision/features/`](vision/features/). Screen layouts are in [`vision/screens/`](vision/screens/). Data entity schemas are in [`vision/data/`](vision/data/). Architecture and abstractions are in [`planning/`](planning/).*
