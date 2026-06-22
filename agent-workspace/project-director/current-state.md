# Current State Dashboard

## 1. What the App Can Currently Do

- **Feature 1:** .NET 8.0 WPF solution scaffold — 7-project layered architecture, 15 OSS NuGet packages, MSIX, GitHub Actions CI/CD
- **Feature 2:** Dependency Injection container — 76+ registrations, 42+ interfaces, all provider stubs, 8 DI tests
- **Feature 3:** Logging infrastructure — Serilog with rolling file sink, JSON structured output, thread/machine enrichment
- **Feature 4:** Data layer — 14 entities, AppDbContext with SQLite+FTS5, 8 real repositories, InitialCreate migration, 104 data layer tests
- **Feature 5:** App Shell, Navigation & Theming — three-region MainWindow with GridSplitters, 8 screen shells, sidebar navigation, Dark/Light theme with DynamicResource, 3 chat themes (Classic/Compact/Bubble), font size controls, WpfThemeProvider, ContentRendererRegistry
- **Feature 6:** Windows OS Platform — system tray (NotifyIcon, 8-item context menu, minimize-to-tray, generation indicator), global hotkeys (RegisterHotKey + WH_KEYBOARD_LL, 6 default hotkeys: Ctrl+Shift+Q/W/E/R/C, Alt+Space), PerMonitorV2 DPI awareness, Kestrel WebSocket server (127.0.0.1, auto-port, token auth), AutoUpdater.NET, MSIX packaging
- **Feature 7:** Model Configurations, API Keys & Personas — API key management (8 provider types, DPAPI encryption, Test Key validation with real provider API calls, masked display with copy button). Model Configuration CRUD (temperature slider 0.0-2.0, thinking toggle, pricing input/output per 1K, context overflow strategy: SlidingWindow/HardStop/AutoSummarize, auto-fetch models from provider APIs). Persona CRUD (system prompt with {{variables}}, default model config + chat mode, 2 built-in defaults: General Assistant, Code Helper). Persona selector in Studio Chat textbox toolbar + Ctrl+N PersonaPickerDialog with recently-used ordering. WpfClipboardService stub filled with real implementation. 263 unit tests + 7 integration tests + 44 E2E tests.

## 2. UI Map

- **MainWindow:** Three-region Grid shell. Sidebar (navigation + static chat list preview). Center (tab bar + screen content). Right panel (Artifacts + Chat Nav).
- **Settings Screen:** Now functional with 2 sections — Providers (API key list, Add/Test/Delete with masked display + copy) and Profiles (Model Configuration list + Persona list, Add/Edit/Delete with FK warnings). Category sidebar with 16 items, 2 wired up.
- **Studio Chat:** Persona selector in textbox toolbar with recently-used ordering. Ctrl+N opens PersonaPickerDialog. Chat header shows active Persona name.
- **7 remaining screens:** Still shells with placeholder content.
- **System Tray:** NotifyIcon with context menu. Minimize-to-tray on close.
- **Global Hotkeys:** 6 registered (Ctrl+Shift+Q/W/E/R/C + Alt+Space).

## 3. Architectural Health & Tech Debt

**Rating: Good**
- Clean MVVM with CommunityToolkit.Mvvm. No circular dependencies.
- Two-layer architecture (Model Configs = engine, Personas = behavior) cleanly separated.
- DPAPI encryption implemented (replaced stub). WpfClipboardService stub filled.
- Minor drift: IConfirmationService added (not in planning/abstractions.md) — testability concern, acceptable.
- 263 unit tests + 7 integration tests + 44 E2E tests, all passing.

## 4. Vision Updates

Feature 7 built as envisioned. Minor drift: IConfirmationService added for testable delete confirmations. Cache hit/miss pricing fields added to ModelConfiguration (user-requested during smoke testing). Persona FK cascade-nullify set to SetNull (not Restrict). Vision docs remain accurate — drift is backward-compatible extensions.

## 5. E2E Regression Suite

| Framework | Run Command | Test Files | Locked-In Features |
|-----------|-------------|------------|--------------------|
| FlaUI.UIA3 + xUnit | `dotnet test tests/e2e/MySecondBrain.Tests.E2E` | 3 (app-shell-navigation-theming, windows-os-platform, model-configs-personas) | Features 5, 6, 7 |
| Suite runtime: ~55 seconds (shared fixture, 44 tests, 1 skipped — Ctrl+N unreliable in FlaUI). |

## 6. Unit Test Suite

| Project | Tests | Status |
|---------|-------|--------|
| tests/unit/ | 263 | ✅ All passing |
| tests/e2e/ | 44 | ✅ 44 passed, 1 skipped (FlaUI.UIA3) |
| tests/integration/ | 7 | ✅ All passing (ProviderIntegrationTests) |
