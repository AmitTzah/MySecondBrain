# Current State Dashboard

## 1. What the App Can Currently Do

- **Feature 1:** .NET 8.0 WPF solution scaffold — 7-project layered architecture, 15 OSS NuGet packages, MSIX, GitHub Actions CI/CD
- **Feature 2:** Dependency Injection container — 76+ registrations, 42+ interfaces, all provider stubs, 8 DI tests
- **Feature 3:** Logging infrastructure — Serilog with rolling file sink, JSON structured output, thread/machine enrichment
- **Feature 4:** Data layer — 14 entities, AppDbContext with SQLite+FTS5, 8 real repositories, InitialCreate migration, 104 data layer tests
- **Feature 5:** App Shell, Navigation & Theming — three-region MainWindow with GridSplitters, 8 screen shells, sidebar navigation, Dark/Light theme with DynamicResource, 3 chat themes (Classic/Compact/Bubble), font size controls, WpfThemeProvider, ContentRendererRegistry
- **Feature 6:** Windows OS Platform — system tray (NotifyIcon, 8-item context menu, minimize-to-tray, generation indicator), global hotkeys (RegisterHotKey + WH_KEYBOARD_LL, 6 default hotkeys: Ctrl+Shift+Q/W/E/R/C, Alt+Space), PerMonitorV2 DPI awareness, Kestrel WebSocket server (127.0.0.1, auto-port, token auth), AutoUpdater.NET, MSIX packaging
- **Feature 7:** Model Configurations, API Keys & Personas — API key management (8 provider types, DPAPI encryption, Test Key validation with real provider API calls, masked display with copy button). Model Configuration CRUD (temperature slider 0.0-2.0, thinking toggle, pricing input/output per 1K, context overflow strategy: SlidingWindow/HardStop/AutoSummarize, auto-fetch models from provider APIs). Persona CRUD (system prompt with {{variables}}, default model config + chat mode, 2 built-in defaults: General Assistant, Code Helper). Persona selector in Studio Chat textbox toolbar + Ctrl+N PersonaPickerDialog with recently-used ordering.
- **Feature 8:** Settings, Onboarding & Diagnostics — Full Settings screen with all 16 categories wired up: Providers, Profiles, Appearance (Dark/Light theme, font family/size/weight, chat visual theme), Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Diagnostics, Maintenance. Category sidebar remembers selection within a ViewModel instance. Appearance theme toggle with instant DynamicResource switch. Startup behavior (launch on boot, session restore). Auto-update settings. Database maintenance (VACUUM with before/after size display). Onboarding Wizard — 5-step guided first-launch setup (Welcome → API Keys → Persona → Wiki Directory → Hotkeys → Finish), each step skippable, re-launchable from Settings, with dedicated OnboardingWizardWindow. Diagnostics — global log level selector (Information/Debug/Verbose), 8 per-category toggles (LLM API Calls, Tier 1 Hotkey Pipeline, Tier 2 Command Bar, Database, Wiki & File System, WebSocket, Startup & Shutdown, System Integration), Open Logs Folder/ Clear Logs buttons, API key redaction via Serilog ILogEventEnricher, RuntimeLogFilter for dynamic category control at runtime. TextAction entity with repository.
- **Feature 9:** E2E Test Suite Rewrite & Authoring Guide — Complete rewrite of all E2E tests from scratch. Switched from IClassFixture (app restarts per class = dead time) to ICollectionFixture (single launch for all 62 tests). MSB_DB_PATH environment variable for separate test database (e2e-test.db), deleted on teardown. Self-cleaning tests: every data-creating test deletes via app's 🗑️ delete buttons within the same [Fact] body. E2eTestBase abstract class with 8 shared UIA helpers. 20+ AutomationIds added to XAML for robust element discovery. E2E authoring guide at external-docs/e2e-authoring-guide.md encodes all conventions for future features. HandsOffCountdown window for E2E-friendly startup. 429 unit + 25 integration + 62 E2E tests.
- **Feature 10:** Codebase Realignment — 10-tool surface (bash, text_editor, web_search, web_fetch, memory, wiki_search, skill_load, ask_user_input, present_files, image_search) replacing old 5-tool surface. Agent Skills subsystem (ISkillService + ISkillLoader, 11 Anthropic skills as embedded resources, skill discovery from 4 locations). WebView2 artifacts panel with theme bridge. Per-chat toolbar toggles for tools/skills/memory. Additive system prompt construction (SystemPromptCoordinator). Workspace isolation (BashToolExecutor with %LOCALAPPDATA%/workspace, path blocking). MemoryEntry entity (SQLite, Anthropic memory_20250818 schema). CitationRenderer (priority 350). 597 unit + 36 integration + 62 E2E tests passing.

## 2. UI Map

- **MainWindow:** Three-region Grid shell. Sidebar (navigation + static chat list preview). Center (tab bar + screen content). Right panel (Artifacts + Chat Nav).
- **Settings Screen:** Fully functional. 16 categories in sidebar, all wired up with real controls. Providers, Profiles, Appearance (Dark/Light theme toggle, font settings, chat visual theme picker), Diagnostics (log level, per-category toggles, log file management), Maintenance (database VACUUM), and more.
- **Onboarding Wizard:** Dedicated OnboardingWizardWindow with 6 screens (Welcome + 4 steps + Finish). First-launch detection via ShutdownMode=OnExplicitShutdown. Step dots, skip/back/next navigation, re-launch from Settings.
- **Studio Chat:** Persona selector in textbox toolbar with recently-used ordering. Ctrl+N opens PersonaPickerDialog. Chat header shows active Persona name.
- **6 remaining screens:** Still shells with placeholder content (Wiki Browser, Media Library, Global Artifacts Browser, Usage Dashboard, Model Comparison, Studio Chat workspace — awaiting Wave 3 features 9-16).
- **System Tray:** NotifyIcon with context menu. Minimize-to-tray on close. Generation indicator.
- **Global Hotkeys:** 6 registered (Ctrl+Shift+Q/W/E/R/C + Alt+Space).

## 3. Architectural Health & Tech Debt

**Rating: Excellent**
- Clean MVVM with CommunityToolkit.Mvvm. No circular dependencies.
- E2E test suite completely rewritten: ICollectionFixture (single launch), E2eTestBase (shared helpers), self-cleaning tests (🗑️ buttons), MSB_DB_PATH (separate test DB). Eliminated all previous E2E technical debt (fragile Dispose() cleanup, static counters, duplicated helpers, IClassFixture restarts).
- ApiKeyRedactionEnricher (ILogEventEnricher) replaces ApiKeyDestructuringPolicy — architectural improvement.
- RuntimeLogFilter enables dynamic per-category log level control at runtime.
- SettingsViewModel uses Transient lifetime with WeakReferenceMessenger for DI-friendly cross-window communication.
- Minor: ApiKeyDestructuringPolicy.cs is dead code (superseded by ApiKeyRedactionEnricher) — low priority cleanup.
- 429 unit tests + 25 integration tests + 62 E2E tests, all passing.

## 4. Vision Updates

Feature 8: Built as envisioned with minor architectural improvement (ApiKeyRedactionEnricher replaced IDestructuringPolicy). Feature 9: E2E Test Suite Rewrite — testing infrastructure only, no user-facing changes. Vision docs remain accurate — no re-plan needed.

**2026-06-26 — Ad-hoc fix:** Onboarding wizard X-button close now calls `Application.Current.Shutdown()` instead of leaving orphaned dotnet.exe processes. Root cause: `ShutdownMode="OnExplicitShutdown"` combined with no explicit shutdown on wizard abandonment. Fix in [`App.xaml.cs`](src/MySecondBrain.UI/App.xaml.cs:161-197): `studioWasLaunched` flag + `Closed` event handler + static `ShouldShutdownOnWizardClose()` helper. 2 new unit tests in [`AppShutdownTests.cs`](tests/unit/MySecondBrain.Tests.Unit/AppShutdownTests.cs).

**2026-06-26 — Wiki search design refinements (Feature 19):** Three enhancements folded into vision and planning docs: (1) FTS5 porter stemming for plural/tense matching in wiki search, (2) `wiki_search` tool returns index.md catalog alongside FTS5 matches for complete wiki awareness in a single call, (3) trigram fuzzy fallback on zero results with "Did you mean...?" suggestions. Updated: [`personal-wiki.md`](vision/features/personal-wiki.md) (N2, N3), [`tool-use-agents.md`](vision/features/tool-use-agents.md) (H10), [`roadmap.md`](roadmap.md) (F19), [`abstractions.md`](planning/abstractions.md) (IWikiService, WikiSearchToolExecutor), [`architecture.md`](planning/architecture.md) (WikiSearchService).

## 5. E2E Regression Suite

| Framework | Run Command | Test Files | Locked-In Features |
|-----------|-------------|------------|--------------------|
| FlaUI.UIA3 + xUnit (ICollectionFixture) | `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug` | 8 (AppShellNavigationTheming, PlatformServices, SystemTrayHotkey, ModelConfigsApiKeys, Personas, SettingsDiagnostics, AppearanceOnboarding, OnboardingWizard) | Features 5, 6, 7, 8, 9 |
| Suite: 62 tests, single app launch, ~3 min runtime. Self-cleaning (🗑️ delete buttons). Separate test DB via MSB_DB_PATH env var. Authoring guide: planning/e2e-authoring-guide.md. |

## 6. Unit Test Suite

| Project | Tests | Status |
|---------|-------|--------|
| tests/unit/ | 678 | ✅ All passing |
| tests/e2e/ | 62 | ✅ 62 passed (FlaUI.UIA3, ICollectionFixture) |
| tests/integration/ | 25 | ✅ All passing |
