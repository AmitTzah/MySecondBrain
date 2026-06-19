# Current State Dashboard

## 1. What the App Can Currently Do

- **Feature 1:** .NET 8.0 WPF solution scaffold — 7-project layered architecture, 15 OSS NuGet packages, MSIX, GitHub Actions CI/CD
- **Feature 2:** Dependency Injection container — 76+ registrations, 42+ interfaces, all provider stubs, 8 DI tests
- **Feature 3:** Logging infrastructure — Serilog with rolling file sink, JSON structured output, thread/machine enrichment
- **Feature 4:** Data layer — 14 entities, AppDbContext with SQLite+FTS5, 8 real repositories, InitialCreate migration, 104 data layer tests
- **Feature 5:** App Shell, Navigation & Theming — three-region MainWindow with GridSplitters, 8 screen shells, sidebar navigation, Dark/Light theme with DynamicResource, 3 chat themes (Classic/Compact/Bubble), font size controls, WpfThemeProvider, ContentRendererRegistry
- **Feature 6:** Windows OS Platform — system tray (NotifyIcon, 8-item context menu, minimize-to-tray, generation indicator), global hotkeys (RegisterHotKey + WH_KEYBOARD_LL, 6 default hotkeys: Ctrl+Shift+Q/W/E/R/C, Alt+Space), PerMonitorV2 DPI awareness, Kestrel WebSocket server (127.0.0.1, auto-port, token auth), AutoUpdater.NET, MSIX packaging

## 2. UI Map

- **MainWindow:** Three-region Grid shell. Sidebar (navigation + static chat list preview). Center (tab bar + screen content). Right panel (Artifacts + Chat Nav).
- **8 Screens:** All exist as shells with placeholder content.
- **System Tray:** NotifyIcon with context menu. Minimize-to-tray on close.
- **Global Hotkeys:** 6 registered (Ctrl+Shift+Q/W/E/R/C + Alt+Space).

## 3. Architectural Health & Tech Debt

**Rating: Good**
- Clean MVVM with CommunityToolkit.Mvvm. No circular dependencies.
- Platform services properly abstracted behind interfaces.
- 146 unit tests + 18 E2E tests, all passing.
- Minor drift: default hotkeys changed from Alt+Q/W/E/R to Ctrl+Shift+Q/W/E/R (user-requested, avoids AutoHotkey conflicts).

## 4. Vision Updates

Feature 6 built as envisioned. Minor drift: hotkey modifiers changed per user request. Vision docs updated accordingly.

## 5. E2E Regression Suite

| Framework | Run Command | Test Files | Locked-In Features |
|-----------|-------------|------------|--------------------|
| FlaUI.UIA3 + xUnit | `dotnet test tests/e2e/MySecondBrain.Tests.E2E` | 1 (feature-app-shell-navigation-theming) | Features 5, 6 |
| Suite runtime: ~30 seconds (shared fixture, 18 tests). |

## 6. Unit Test Suite

| Project | Tests | Status |
|---------|-------|--------|
| tests/unit/ | 146 | ✅ All passing |
| tests/e2e/ | 18 | ✅ All passing (FlaUI.UIA3) |
| tests/integration/ | 0 | ⚠️ Empty — deferred |
