# Current State Dashboard

## 1. What the App Can Currently Do

- **Feature 1:** .NET 8.0 WPF solution scaffold — 7-project layered architecture, 15 OSS NuGet packages, MSIX, GitHub Actions CI/CD
- **Feature 2:** Dependency Injection container — 76+ registrations, 42+ interfaces, all provider stubs, 8 DI tests
- **Feature 3:** Logging infrastructure — Serilog with rolling file sink, JSON structured output, thread/machine enrichment
- **Feature 4:** Data layer — 14 entities, AppDbContext with SQLite+FTS5, 8 real repositories, InitialCreate migration, 104 data layer tests
- **Feature 5:** App Shell, Navigation & Theming — three-region MainWindow (sidebar + content + right panel) with GridSplitters, 8 screen shells with placeholder content, sidebar navigation (Chats/Wiki/Media/Artifacts/Usage/Settings), Dark/Light theme with DynamicResource instant toggle, 3 chat visual themes (Classic/Compact/Bubble), font size quick-adjust (A⁻/A⁺, 10-24px), WpfThemeProvider with persistence, ContentRendererRegistry with 7 renderers

## 2. UI Map

- **MainWindow:** Three-region Grid shell. Left: sidebar (navigation + static chat list preview). Center: content area with tab bar + screen content. Right: collapsible panel (Artifacts top, Chat Navigation bottom).
- **8 Screens:** ChatView, WikiBrowserView, MediaLibraryView, GlobalArtifactsBrowserView, UsageDashboardView, SettingsView, OnboardingWizardView, ModelComparisonView — all with placeholder content.
- **Navigation:** ContentControl + ScreenTemplateSelector pattern. Sidebar RadioButtons switch SelectedScreen enum.
- **Themes:** Dark.xaml + Light.xaml (25+ resource keys). Instant toggle via ☀/🌙 button. Persisted via ISettingsRepository.
- **Chat Themes:** 3 DataTemplate variants (Classic/Compact/Bubble) switchable via ComboBox. Persisted.

## 3. Architectural Health & Tech Debt

**Rating: Good**
- Clean MVVM with CommunityToolkit.Mvvm source generators. No circular dependencies.
- Three-region Grid shell extends Wave 1 infrastructure; does not modify it.
- Well-documented deferrals: tab system → F6, chat list logic → F11, Trash → F12, font family/weight UI → F8.
- `MergedDictionaries.Clear()` during theme swap — documented constraint (safe now, needs update if future features add more merged dictionaries).
- Static chat list preview + enhanced screen shells (cosmetic drift — documented and intentional, not structural).
- 114 unit tests + 18 E2E tests covering all 6 acceptance criteria.

## 4. Vision Updates

Feature 5 built as envisioned — no vision changes. Screen shells went beyond structural placeholders (enhanced visual fidelity), classified as minor cosmetic drift.

## 5. E2E Regression Suite

| Framework | Run Command | Test Files | Locked-In Features |
|-----------|-------------|------------|--------------------|
| FlaUI.UIA3 + xUnit | `dotnet test tests/e2e/MySecondBrain.Tests.E2E` | 1 (feature-app-shell-navigation-theming) | Feature 5 |
| Suite runtime: ~22-26 seconds (shared fixture, 18 tests). ⚠️ Features 1-4: E2E not applicable (infrastructure — no runnable UI). |

## 6. Unit Test Suite

| Project | Tests | Status |
|---------|-------|--------|
| tests/unit/ | 114 | ✅ All passing |
| tests/e2e/ | 18 | ✅ All passing (FlaUI.UIA3) |
| tests/integration/ | 0 | ⚠️ Empty project — deferred to Feature 16 (testing infrastructure) |
