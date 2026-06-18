# Current State Dashboard

## 1. What the App Can Currently Do

- **W1.1:** .NET 8.0 WPF solution scaffold — 7-project layered architecture, 15 OSS NuGet packages, MSIX, GitHub Actions CI/CD
- **W1.2:** Dependency Injection container — 76+ registrations, 42+ interfaces, all provider stubs, 8 DI tests
- **W1.3:** Logging infrastructure — Serilog with rolling file sink (%LOCALAPPDATA%\MySecondBrain\logs\), JSON structured output, console sink (debug), thread/machine enrichment
- **W1.4:** Data layer — 14 entities (13 vision + MessageDrafts + AppSetting), AppDbContext with SQLite + FTS5, 8 real repository implementations, InitialCreate migration with auto-migrate at startup, 104 data layer tests

## 2. UI Map

_No UI yet — infrastructure only. 11 ViewModels stubbed, MainWindow placeholder. App.xaml.cs runs database migration on startup._

## 3. Architectural Health & Tech Debt

**Rating: Excellent**
- Clean layered dependency chain: Core → Data → Services → UI
- All 14 entities properly abstracted behind repository interfaces
- 114 unit tests (0 failures), comprehensive FTS5 + FK + migration coverage
- AppSetting + MessageDrafts are infrastructure extensions (flagged in planning, not drift)
- 100% test pass rate after fixing pre-existing W1.3 LogFile test (ValidateOnBuild conflict with W1.4 real repos)

## 4. Vision Updates

Features 1-4 built as envisioned — no vision changes. AppSetting and MessageDrafts entities are infrastructure additions flagged in planning/data-model.md Architect Decision Flags.

## 5. E2E Regression Suite

| Framework | Run Command | Test Files | Locked-In Features |
|-----------|-------------|------------|--------------------|
| Not yet configured | — | 0 | None |
| ⚠️ Features 1-4: E2E not applicable (infrastructure — no runnable UI) |

## 6. Unit Test Suite

| Project | Tests | Status |
|---------|-------|--------|
| tests/unit/ | 114 | ✅ All passing |
| tests/integration/ | 0 | ⚠️ Empty project — deferred to W1.8 |
