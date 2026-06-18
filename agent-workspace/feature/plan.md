# Feature Implementation Plan: App Shell, Navigation & Theming

## 1. Overall Project Context

MySecondBrain is a .NET 8.0 WPF native Windows desktop app that unifies all LLM interactions with a personal wiki/second-brain knowledge management system. It is local-first (SQLite + file-based wiki), BYO API keys, with a three-tier window model (Tier 1: overlay pill for hotkey rewrite, Tier 2: command bar, Tier 3: main studio).

The solution uses a 7-project layered architecture (`Core → Data → Services → UI + Tests + Package`), CommunityToolkit.Mvvm 8.x with source generators for MVVM, EF Core 8.x + SQLite with FTS5 for the data layer, Markdig for Markdown, Serilog via Microsoft.Extensions.Logging bridge for logging, and Microsoft.Extensions.DependencyInjection + Hosting for DI. The UI layer uses WPF ResourceDictionary with DynamicResource for theming.

Features 1-4 are complete: solution scaffold, DI container (76+ registrations, 42+ interfaces), Serilog logging with rolling file sink, and the data layer (14 EF Core entities, AppDbContext with SQLite+FTS5, 8 repositories, initial migration with auto-migrate, seed data). All 114 unit tests pass. All service, ViewModel, repository, and renderer classes exist as stubs ready to be filled in.

## 2. Feature-Specific Context

**Feature 5: App Shell, Navigation & Theming** is the first UI feature in Wave 2 (Skeleton). It builds the foundational app shell that hosts ALL other screens. Without this feature, no other UI feature can be visually tested.

The feature delivers: (a) a three-region MainWindow shell (sidebar | content area | right panel) with GridSplitters, (b) 8 empty screen UserControl shells with placeholder content matching vision layouts, (c) sidebar navigation with 6 icon+label items switching the center content area, (d) tabbed chat content area (tab bar for ChatThread views), (e) a Dark/Light theme system using WPF ResourceDictionary swap with DynamicResource for instant toggle, (f) 3 chat visual themes (Classic/Compact/Bubble) as DataTemplate variants, (g) font settings (family, size, weight) persisted via ISettingsRepository, (h) filled-in WpfThemeProvider and ContentRendererRegistry implementations, and (i) startup theme/font restoration.

This feature depends on F2 (DI Container) and F4 (Data Layer). It is the dependency for all subsequent screen-building features (F6 Studio Chat, F7 Wiki Browser, etc.).

### 2.1 Explicit Deferrals to Later Features

The following vision-spec items are NOT delivered by this feature. They are documented here to prevent confusion during implementation and review:

| Vision Spec Item | Deferred To | Justification |
|---|---|---|
| Tab drag-drop reorder, Ctrl+W close, Ctrl+Shift+T reopen (C10) | Feature 6 (Studio Chat Workspace) | Requires ChatThread entities, ChatThreadService, and message rendering — not available yet. F5 creates the tab bar UI shell only. |
| Sidebar chat list with date groups, favorites, tags, pins, folders (L1-L14) | Feature L (Chat Organization & Search) | Requires full ChatThread data and repository operations. F5 builds the screen-navigation sidebar only. |
| Trash tab in sidebar (U2) | Feature U (Soft-Delete Trash) | Requires IsDeleted/DeletedAt fields on ChatThread entity and soft-delete logic. |
| Timeline tab in sidebar (L5) | Feature L (Chat Organization & Search) | Requires Tier 1/2 transient thread data that doesn't exist yet. |
| Font family/weight UI controls (A3) | Feature 8 (Settings — Appearance) | Font persistence infrastructure is built here; UI controls for family/weight selection belong in the Settings screen. |
| Right panel content — Artifacts list (F2) + Chat Navigation (D6) | Features F (Artifacts) + D (Branching) | F5 creates the right panel container with two-section placeholder structure. Actual artifact list and chat navigation rendering require those features. |

## 3. Architecture and Extensibility

### 3.1 Three-Region Shell (Grid + GridSplitters)

The MainWindow uses a single `Grid` with three column definitions and two `GridSplitter` controls. All three regions use `DynamicResource` for colors so theming works immediately:

```
┌──────────────┬───┬──────────────────────┬───┬───────────┐
│   SIDEBAR    │ ◄ │     CENTER AREA      │ ► │  RIGHT    │
│  280px       │   │     (flex)           │   │  PANEL    │
│  min 150px   │   │                      │   │  320px    │
│  max 50%     │   │                      │   │  min 200px│
└──────────────┴───┴──────────────────────┴───┴───────────┘
```

### 3.2 Screen Navigation Pattern

Screen switching uses a `ContentControl` + `DataTemplateSelector` pattern (implicit `DataTemplate` by `x:Type` does NOT work with enums). The `MainWindowViewModel` exposes a `SelectedScreen` enum property. The sidebar nav items bind to `RelayCommand` that sets `SelectedScreen`. A `ScreenTemplateSelector` (subclass of `DataTemplateSelector`) maps each `ScreenType` enum value to the correct `UserControl`. This is a standard WPF MVVM pattern — no custom router, no `TabControl`.

```csharp
// Navigation pattern
public enum ScreenType { Chats, Wiki, Media, Artifacts, Usage, Settings }

[ObservableProperty]
private ScreenType _selectedScreen = ScreenType.Chats;
```

The tab bar is chat-thread-only and lives inside `ChatView.xaml`, NOT in the MainWindow shell. Other screens (Wiki, Settings, etc.) render as single views without tabs.

**Tab System Deferral:** The full tab system (drag-drop reorder, Ctrl+W close, Ctrl+Shift+T reopen, scrollable overflow per vision spec C10) is deferred to Feature 6 (Studio Chat Workspace). This feature (F5) only creates the tab bar UI shell with placeholder tabs and a "+" button — purely visual, no tab open/close/reorder logic. This is intentional: the tab system requires ChatThread data entities, ChatThreadService, and message rendering infrastructure that don't exist yet.

### 3.3 Theme System — Extensibility

The theme system uses WPF `ResourceDictionary` with `DynamicResource` references. Two dictionaries (`Dark.xaml`, `Light.xaml`) share identical resource keys. `WpfThemeProvider` implements `IThemeProvider` and swaps the merged dictionary at runtime via `Application.Current.Resources.MergedDictionaries.Clear()` + `Add()`. This is the classic .NET 8.0 WPF approach — no third-party library needed.

**Mitigation — MergedDictionaries.Clear():** `Clear()` removes ALL merged dictionaries, which could wipe out custom styles added later. In this feature, only the theme dictionary is merged at this stage, so `Clear()` is safe. If future features add additional merged dictionaries to `Application.Resources`, the swap logic must be updated to target only the theme dictionary (e.g., by key lookup and replace rather than clear-all). This is documented here as a known constraint.

Adding a third theme (e.g., "HighContrast") requires only: (a) create `HighContrast.xaml` with the same resource keys, (b) add `HighContrast` to the `AppTheme` enum (already in Core), (c) update the switch in `WpfThemeProvider.SetAppTheme()`. Zero XAML changes to any screen or control.

The 3 chat visual themes (Classic/Compact/Bubble) are `DataTemplate` variants selected by `ChatTheme` enum. Adding a fourth chat theme requires: (a) add to `ChatTheme` enum, (b) create the `DataTemplate`, (c) update the switch in `WpfThemeProvider.GetChatMessageTemplate()`.

**Font Family/Weight UI Deferral:** Vision spec A3 calls for customizable font family, size, and weight. Only font size has quick-adjust UI controls (A⁻/A⁺ buttons, C25) in this feature. Font family and weight persistence infrastructure is built here, but their UI controls are deferred to Feature 8 (Settings — Appearance category, A3). The infrastructure (persistence via ISettingsRepository, DynamicResource keys, SetFontSettings method) is complete so that Feature 8 only needs to add UI.

**Sidebar Chat List / Trash / Timeline Deferral:** The vision spec calls for a sidebar with chat list (L1), Trash tab (U2), and Timeline tab (L5). These are Features L (Chat Organization & Search) and U (Soft-Delete Trash), NOT part of this app shell feature. This feature builds the main screen-navigation sidebar (Chats/Wiki/Media/Artifacts/Usage/Settings). The chat-list sidebar, Trash, and Timeline are sub-views within the ChatView that will be added in those respective features.

### 3.4 Renderer Registry — Plugin/Registry Pattern

`ContentRendererRegistry` resolves renderers by priority via `IEnumerable<IContentBlockRenderer>` DI injection. Adding a new content block type requires: (a) implement `IContentBlockRenderer` in `UI/Controls/`, (b) register `services.AddSingleton<IContentBlockRenderer, NewRenderer>()`, (c) the registry auto-discovers it. Zero changes to existing renderers or the registry.

## 4. Final Expected Project Structure

- `src/MySecondBrain.UI/`
  - `App.xaml` — **[MODIFIED]** Merged Dark.xaml into Application.Resources
  - `App.xaml.cs` — **[MODIFIED]** Apply saved theme/font on startup, wire MainWindow with DataContext
  - `MainWindow.xaml` — **[MODIFIED]** Three-region Grid shell with GridSplitters, ContentControl for screens
  - `MainWindow.xaml.cs` — **[MODIFIED]** Constructor injection of MainWindowViewModel
  - `Themes/`
    - `Dark.xaml` — **[NEW]** Dark theme ResourceDictionary (25+ color/brush resources)
    - `Light.xaml` — **[NEW]** Light theme ResourceDictionary (25+ color/brush resources)
  - `Views/`
    - `ChatView.xaml` — **[NEW]** Chat screen shell with tab bar, chat header, conversation area placeholder, input area placeholder
    - `WikiBrowserView.xaml` — **[NEW]** Wiki Browser shell with file tree + Markdown viewer + info panel placeholders
    - `MediaLibraryView.xaml` — **[NEW]** Media Library shell with filter bar + grid placeholder
    - `GlobalArtifactsBrowserView.xaml` — **[NEW]** Global Artifacts Browser shell with search/filter + table placeholder
    - `UsageDashboardView.xaml` — **[NEW]** Usage Dashboard shell with summary cards + chart placeholders
    - `SettingsView.xaml` — **[NEW]** Settings shell with category sidebar + content area placeholders
    - `ModelComparisonView.xaml` — **[NEW]** Model Comparison shell with setup + results phase placeholders
    - `OnboardingWizardView.xaml` — **[NEW]** Onboarding Wizard shell with step indicator + content card placeholder
  - `ViewModels/`
    - `MainWindowViewModel.cs` — **[MODIFIED]** Added SelectedScreen, navigation commands, theme/font properties
    - `ChatThreadViewModel.cs` — No changes (stub, used by ChatView)
    - `SettingsViewModel.cs` — No changes (stub)
    - `WikiBrowserViewModel.cs` — No changes (stub)
    - `UsageDashboardViewModel.cs` — No changes (stub)
    - `MediaLibraryViewModel.cs` — No changes (stub)
    - `GlobalArtifactsBrowserViewModel.cs` — No changes (stub)
    - `ModelComparisonViewModel.cs` — No changes (stub)
    - `OnboardingWizardViewModel.cs` — No changes (stub)
  - `Services/`
    - `WpfThemeProvider.cs` — **[MODIFIED]** Full implementation: ResourceDictionary swap, theme/font persistence via ISettingsRepository, events
  - `Controls/`
    - `ContentRendererRegistry.cs` — **[MODIFIED]** Fixed priorities to match knowledge base, added priority sorting
    - `MarkdownTextRenderer.cs` — No code changes (Priority=100 is correct)
    - `CodeBlockRenderer.cs` — **[MODIFIED]** Priority changed from 90→200
    - `ArtifactReferenceRenderer.cs` — No code changes (check Priority=300)
    - `ImageRenderer.cs` — No code changes (check Priority=400)
    - `MediaRenderer.cs` — No code changes (check Priority=500)
    - `ThinkingRenderer.cs` — **[MODIFIED]** Priority changed from 50→600
    - `ToolCallRenderer.cs` — **[MODIFIED]** Priority changed from 40→700

---

## 5. Execution Steps

### [x] Step 1: Dark & Light Theme ResourceDictionaries + App.xaml wiring

**Goal:** Create `Dark.xaml` and `Light.xaml` ResourceDictionaries with 25+ color/brush resources, merge `Dark.xaml` into `Application.Resources` in `App.xaml`, and apply `DynamicResource` to MainWindow background.

**Actions:**
1. Create `src/MySecondBrain.UI/Themes/Dark.xaml` with 25+ `SolidColorBrush` and `sys:Double` resources using dark color values (see reference.md for full catalog)
2. Create `src/MySecondBrain.UI/Themes/Light.xaml` with identical resource keys and light color values
3. Modify `src/MySecondBrain.UI/App.xaml` — add `<ResourceDictionary.MergedDictionaries>` containing `Dark.xaml` as the default
4. Modify `src/MySecondBrain.UI/MainWindow.xaml` — change `Window Background` to `{DynamicResource AppBackground}`

**Resource keys to define (both files, same keys):**
- `AppBackground`, `AppForeground`, `SidebarBackground`, `SidebarForeground`, `ContentBackground`, `ContentForeground`, `PanelBackground`, `PanelForeground`, `TabBarBackground`, `TabActiveBackground`, `TabInactiveBackground`, `HeaderBackground`, `InputBackground`, `AccentBrush`, `AccentForeground`, `BorderBrush`, `SubtleBrush`, `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `ScrollBarBrush`, `GridSplitterBrush`, `NavActiveBackground`, `NavInactiveForeground`, `FontFamily`, `FontSize`

**Automated Testing:** `dotnet build` — verify solution compiles with new Theme files. Not applicable for unit tests — verified by manual smoke test only.

**Live Smoke Test (Mandatory):**
1. Build the project: `dotnet build`
2. Launch `MySecondBrain.UI.exe` (or F5 from Visual Studio)
3. **Observe:** The MainWindow background is dark gray (#1E1E1E), NOT the default white (#FFFFFF)
4. **Observe:** Window title bar reads "MySecondBrain"
5. The window has no other content yet — just a dark background

**Suggested Commit Message:** `feat: add Dark and Light theme ResourceDictionaries with 25+ color/brush resources`

---

### [x] Step 2: MainWindow Three-Region Grid Shell with GridSplitters & Sidebar Nav

**Goal:** Replace the empty `<Grid>` in MainWindow.xaml with a three-column Grid layout containing: (a) sidebar column with 6 nav items (Chats/Wiki/Media/Artifacts/Usage/Settings), (b) center content column with a `ContentControl`, (c) right panel column with two-section placeholder structure (Artifacts header + Chat Navigation header with a resizable divider) — all separated by `GridSplitter` controls.

**Actions:**
1. Replace `MainWindow.xaml` `<Grid>` with a 5-column Grid: `280, Auto(4), *, Auto(4), 320`
2. Add sidebar `StackPanel` (Column 0) with 6 `RadioButton` nav items styled via `NavButtonStyle`, each showing emoji + label text, bound to `NavigateCommand` with `CommandParameter`
3. Add first `GridSplitter` (Column 1): `Width="4"`, `Background="{DynamicResource GridSplitterBrush}"`, `ResizeBehavior="PreviousAndNext"`
4. Add center `ContentControl` (Column 2): `Content="{Binding SelectedScreen}"`, uses `ScreenTemplateSelector` (wired in Step 3), placeholder TextBlock "Select a screen"
5. Add second `GridSplitter` (Column 3): same properties as first
6. Add right panel (Column 4): vertical `Grid` with two rows split by a horizontal `GridSplitter`:
   - Top row (2*): "📄 Artifacts" section header + placeholder "No artifacts in this chat yet"
   - Horizontal `GridSplitter` (Height="4") between rows
   - Bottom row (*): "🧭 Chat Navigation" section header + placeholder "No messages to navigate"
7. Set column min/max: sidebar `MinWidth="150" MaxWidth="500"`, right panel `MinWidth="200" MaxWidth="500"`
8. Modify `MainWindow.xaml.cs` — add constructor injection of `MainWindowViewModel` and set `DataContext`

**Automated Testing:** `dotnet build` — verify solution compiles with new Grid layout and all bindings resolve. Not applicable for unit tests — verified by manual smoke test only.

**Live Smoke Test (Mandatory):**
1. Build and launch the app
2. **Observe:** Three columns visible — left sidebar (dark), center area (dark), right panel (dark)
3. **Observe:** Two vertical `GridSplitter` handles (4px wide), mouse cursor changes to ↔ resize on hover
4. **Drag left GridSplitter:** Sidebar resizes between 150px and 500px
5. **Drag right GridSplitter:** Right panel resizes between 200px and 500px
6. **Observe:** Sidebar shows 6 navigation items: 💬 Chats, 📝 Wiki, 🖼️ Media, 📄 Artifacts, 📊 Usage, ⚙️ Settings
7. **Click any nav item:** The item highlights with accent background color
8. **Observe:** Right panel shows two sections — "📄 Artifacts" (top) and "🧭 Chat Navigation" (bottom) with a draggable horizontal divider between them

**Suggested Commit Message:** `feat: implement three-region MainWindow shell with GridSplitters, sidebar nav, and right panel sections`

---

### Step 3: Screen Navigation System + 8 Screen UserControl Shells

**Goal:** Implement `MainWindowViewModel.SelectedScreen` enum property with navigation commands. Create a `ScreenTemplateSelector` (subclass of `DataTemplateSelector`) to map `ScreenType` → `UserControl`. Wire `ContentControl` in center area with the selector. Create all 8 screen UserControl shells, each with placeholder content matching its vision layout structure and `DynamicResource` colors. Wire each View's `DataContext` to its corresponding ViewModel via DI.

**Actions:**
1. Add to `MainWindowViewModel.cs`: `ScreenType` enum (`Chats, Wiki, Media, Artifacts, Usage, Settings`), `[ObservableProperty] SelectedScreen`, `[RelayCommand] Navigate(string screenName)` using `Enum.TryParse`
2. Create `ScreenTemplateSelector.cs` in `Views/` — maps each `ScreenType` value to the matching `DataTemplate` containing the UserControl (see reference.md for full implementation)
3. Modify `MainWindow.xaml` — replace center placeholder with `ContentControl` using `ScreenTemplateSelector`, define 6 `DataTemplate` resources each wrapping the appropriate View UserControl
4. Create 8 `UserControl` files in `Views/` (see files list below), each with:
   - `DynamicResource` colors on all backgrounds/foregrounds
   - Placeholder structure matching vision spec layout
   - `DataContext` set to the corresponding ViewModel stub (via XAML or DI resolution)
5. Verify all 8 Views + ScreenTemplateSelector compile

**Files:**
- `src/MySecondBrain.UI/ViewModels/MainWindowViewModel.cs` (MODIFY)
- `src/MySecondBrain.UI/Views/ScreenTemplateSelector.cs` (NEW)
- `src/MySecondBrain.UI/MainWindow.xaml` (MODIFY)
- `src/MySecondBrain.UI/Views/ChatView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/WikiBrowserView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/MediaLibraryView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/GlobalArtifactsBrowserView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/UsageDashboardView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/SettingsView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/ModelComparisonView.xaml` (NEW)
- `src/MySecondBrain.UI/Views/OnboardingWizardView.xaml` (NEW)

**Each screen shell contains (matching vision spec region structure):**
- `ChatView`: Tab bar (placeholder tabs + "+" button), chat header bar (Persona name, context bar, A⁻/A⁺, ☀/🌙, 📌, ⋯), conversation area placeholder ("No chats yet"), input area placeholder (textbox + Send button)
- `WikiBrowserView`: File tree placeholder ("No .md files found"), Markdown viewer placeholder ("Select a file"), info panel placeholder (Related/Backlinks/File Info tabs)
- `MediaLibraryView`: Filter bar placeholder (type/source/date/sort), media grid placeholder ("No media files yet")
- `GlobalArtifactsBrowserView`: Search/filter bar placeholder, artifacts table placeholder ("No artifacts yet")
- `UsageDashboardView`: Summary cards row (4 cards), chart grid (4 chart placeholders), breakdown table placeholder
- `SettingsView`: Category sidebar (16 categories), content area placeholder ("Select a category")
- `ModelComparisonView`: Setup phase placeholder (Persona checklist + prompt input), results phase placeholder (side-by-side panels)
- `OnboardingWizardView`: Step indicator (4 dots), content card placeholder ("Welcome to MySecondBrain")

**Automated Testing:** `dotnet build` — verify all 8 new UserControls and ScreenTemplateSelector compile. `dotnet test` — verify all 114 existing unit tests still pass (no regressions).

**Live Smoke Test (Mandatory):**
1. Build and launch the app
2. **Observe:** Center area shows ChatView with tab bar, chat header, conversation placeholder, and input area
3. **Click 📝 Wiki** in sidebar
4. **Observe:** Center area switches to WikiBrowserView with file tree + viewer + info panel placeholders
5. **Click 🖼️ Media:** See MediaLibraryView with filter bar + grid
6. **Click 📄 Artifacts:** See GlobalArtifactsBrowserView with search + table
7. **Click 📊 Usage:** See UsageDashboardView with 4 summary cards + 4 chart boxes
8. **Click ⚙️ Settings:** See SettingsView with 16-category sidebar + content area
9. **Click 💬 Chats:** Returns to ChatView
10. **Observe:** All screens use dark theme colors (no white backgrounds anywhere)

**Suggested Commit Message:** `feat: add screen navigation system with ScreenTemplateSelector and 8 UserControl shells`

---

### Step 4: WpfThemeProvider Implementation + Theme Toggle Button

**Goal:** Fill in the `WpfThemeProvider` stub with a full implementation: `SetAppTheme()` swaps the `ResourceDictionary` at runtime via `MergedDictionaries.Clear()` + `Add()`, `SetFontSettings()` updates font DynamicResource values, events fire on theme changes, and all settings persist via `ISettingsRepository`. Add the ☀/🌙 theme toggle button to ChatView's chat header bar wired to `MainWindowViewModel.ToggleThemeCommand`.

**Actions:**
1. Implement `WpfThemeProvider.cs` fully (see reference.md for complete code):
   - Constructor: inject `ISettingsRepository`, default to `AppTheme.Dark`, `ChatTheme.Classic`, `Segoe UI` 14px
   - `SetAppTheme(AppTheme)`: guard against no-op, build `ResourceDictionary` from `Themes/Dark.xaml` or `Themes/Light.xaml`, `merged.Clear()` + `merged.Add(dict)`, fire `AppThemeChanged` event, persist to `ISettingsRepository` key `"AppTheme"`
   - `SetFontSettings(family, size, weight)`: validate 10-24px range, update `Application.Current.Resources["FontFamily"]`, `["FontSize"]`, `["FontWeight"]`, persist all three keys (`"FontFamily"`, `"FontSize"`, `"FontWeight"`)
   - `SetChatTheme(ChatTheme)`: guard against no-op, persist key `"ChatTheme"`, fire `ChatThemeChanged`
   - `GetChatMessageTemplate(ChatTheme)`: resolve named `DataTemplate` from application resources
   - Property getters: `CurrentAppTheme`, `CurrentChatTheme`, `FontFamily`, `FontSize`, `FontWeight`
2. Add to `MainWindowViewModel.cs`:
   - Inject `IThemeProvider`, expose `CurrentAppTheme`, `CurrentChatTheme` properties
   - `[RelayCommand] ToggleTheme()` — flips Dark↔Light, calls `_themeProvider.SetAppTheme()`
   - `[RelayCommand] IncreaseFont()` / `DecreaseFont()` — with 10-24 clamping (full commands added in Step 6)
   - `[ObservableProperty] FontSizeDisplay` — synced to `_themeProvider.FontSize`
3. Modify `ChatView.xaml` — add ☀/🌙 `Button` in chat header bar bound to `ToggleThemeCommand`, content toggles based on `CurrentAppTheme`

**Files:**
- `src/MySecondBrain.UI/Services/WpfThemeProvider.cs` (MODIFY — full implementation)
- `src/MySecondBrain.UI/ViewModels/MainWindowViewModel.cs` (MODIFY — add theme/font properties, ToggleThemeCommand, IncreaseFontCommand, DecreaseFontCommand)
- `src/MySecondBrain.UI/Views/ChatView.xaml` (MODIFY — wire toggle button to command)

**Automated Testing:** `dotnet build` — verify solution compiles with full WpfThemeProvider. `dotnet test` — verify all 114 existing unit tests still pass (no regressions). Not applicable for theme-switching unit tests — WPF ResourceDictionary requires a running Application, verified by manual smoke test only.

**Live Smoke Test (Mandatory):**
1. Build and launch the app — see Dark theme
2. **Locate the ☀ button** in the ChatView's chat header bar (right side, near the pin icon)
3. **Click ☀**
4. **Observe:** Entire application instantly switches to Light theme — sidebar, content area, right panel, all text and backgrounds change
5. The button icon changes to 🌙 (indicating light mode is active, click to return to dark)
6. **Click 🌙:** App instantly returns to Dark theme
7. **Click rapidly 5 times:** No flicker, no delay, instant each time

**Suggested Commit Message:** `feat: implement WpfThemeProvider with runtime theme swapping, font persistence, and toggle button`

---

### Step 5: Apply Saved Theme & Font Settings on Startup

**Goal:** In `App.xaml.cs` `OnStartup`, after DI container build and before `MainWindow.Show()`, read `"AppTheme"`, `"FontFamily"`, `"FontSize"`, and `"FontWeight"` from `ISettingsRepository`. Apply saved theme via `IThemeProvider.SetAppTheme()` and saved font settings via `IThemeProvider.SetFontSettings()`. If no saved preference exists, default to Dark theme with Segoe UI 14px Normal weight.

**Actions:**
1. In `App.xaml.cs` `OnStartup`, after `_serviceProvider = services.BuildServiceProvider()` and `db.Database.Migrate()`:
   - Resolve `IThemeProvider` and `ISettingsRepository` from DI
   - Read `"AppTheme"` key — if found and parsable as `AppTheme`, call `themeProvider.SetAppTheme(theme)`
   - Read `"FontFamily"`, `"FontSize"`, and `"FontWeight"` keys — if all found and parsable, call `themeProvider.SetFontSettings(savedFontFamily, fontSize, fontWeight)` (do NOT hardcode `FontWeights.Normal`; restore the actual saved weight)
   - If no saved font weight, default to `FontWeights.Normal`
2. Ensure `MainWindow.Show()` is called AFTER all restoration is complete

**Files:**
- `src/MySecondBrain.UI/App.xaml.cs` (MODIFY — add theme/font restoration)

**Startup sequence (updated — FontWeight restored, not hardcoded):**
```csharp
// After _serviceProvider = services.BuildServiceProvider();
// After db.Database.Migrate();

var themeProvider = _serviceProvider.GetRequiredService<IThemeProvider>();
var settings = _serviceProvider.GetRequiredService<ISettingsRepository>();

// Restore theme
var savedTheme = await settings.GetAsync("AppTheme");
if (savedTheme is not null && Enum.TryParse<AppTheme>(savedTheme, out var theme))
    themeProvider.SetAppTheme(theme);

// Restore font (family, size, AND weight)
var savedFontFamily = await settings.GetAsync("FontFamily");
var savedFontSize = await settings.GetAsync("FontSize");
var savedFontWeight = await settings.GetAsync("FontWeight");
var fontSize = 14.0;
var fontWeight = FontWeights.Normal;
if (savedFontSize is not null)
    double.TryParse(savedFontSize, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize);
if (savedFontWeight is not null && Enum.TryParse<FontWeight>(savedFontWeight, out var parsedWeight))
    fontWeight = parsedWeight;
if (savedFontFamily is not null)
    themeProvider.SetFontSettings(savedFontFamily, fontSize, fontWeight);

// Then: var mainWindow = _serviceProvider.GetRequiredService<MainWindow>(); mainWindow.Show();
```

**Automated Testing:** `dotnet build` — verify compile. `dotnet test` — verify all 114 existing unit tests still pass. Not applicable for theme restoration unit tests — startup sequence requires Application runtime, verified by manual smoke test only.

**Live Smoke Test (Mandatory):**
1. Build and launch the app — see Dark theme (default)
2. Click ☀ to switch to Light theme
3. Close the app (click X on window)
4. Launch the app again
5. **Observe:** App launches with Light theme (the saved preference), NOT Dark
6. Switch back to Dark, close, relaunch — see Dark
7. Check SQLite: `SELECT Value FROM Settings WHERE Key = 'AppTheme'` — returns `"Dark"` or `"Light"`
8. Check SQLite: `SELECT Value FROM Settings WHERE Key IN ('FontFamily', 'FontSize', 'FontWeight')` — all three keys present

**Suggested Commit Message:** `feat: restore saved theme and font settings on startup with all font properties persisted`

---

### Step 6: Font Size Quick-Adjust Buttons + Font Settings Persistence

**Goal:** Wire the A⁻ and A⁺ buttons in ChatView's chat header bar to `MainWindowViewModel` commands that call `IThemeProvider.SetFontSettings()`. Display current font size between the buttons. Font changes apply to `FontSize` DynamicResource, affecting all chat message text. Persist via `ISettingsRepository`. Enforce the 10-24px range from vision spec A3.

**Actions:**
1. Add to `MainWindowViewModel.cs`:
   - `[ObservableProperty] double _fontSizeDisplay` — synced from `_themeProvider.FontSize`
   - `[RelayCommand] IncreaseFont()` — `Math.Min(_themeProvider.FontSize + 1, 24)`, calls `SetFontSettings` with current family and weight, updates `FontSizeDisplay`
   - `[RelayCommand] DecreaseFont()` — `Math.Max(_themeProvider.FontSize - 1, 10)`, same pattern
2. Modify `ChatView.xaml` — wire A⁻/A⁺ `Button` elements in chat header bar to `DecreaseFontCommand`/`IncreaseFontCommand`, display `{Binding FontSizeDisplay}` as a `TextBlock` between them

**Files:**
- `src/MySecondBrain.UI/ViewModels/MainWindowViewModel.cs` (MODIFY)
- `src/MySecondBrain.UI/Views/ChatView.xaml` (MODIFY)

**Automated Testing:** `dotnet build` — verify compile. `dotnet test` — verify all 114 existing unit tests still pass. Not applicable for font-size UI unit tests — WPF Button commands require a running Application, verified by manual smoke test only.

**Live Smoke Test (Mandatory):**
1. Build and launch the app
2. **Observe:** "14" displayed between A⁻ and A⁺ buttons in the chat header bar
3. **Click A⁺:** Display changes to "15". All placeholder text using `{DynamicResource FontSize}` grows slightly
4. **Click A⁻ 5 times:** Display shows "10". Text shrinks
5. **Click A⁺ 14 times:** Display caps at "24", does not go higher
6. Close the app, relaunch
7. **Observe:** Font size displays "24" — the persisted value — not the default "14"
8. Check SQLite: `SELECT Value FROM Settings WHERE Key = 'FontSize'` — returns `"24"`

**Suggested Commit Message:** `feat: add font size quick-adjust buttons with persistence and 10-24px clamping`

---

### Step 7: Three Chat Visual Theme DataTemplates (Classic/Compact/Bubble)

**Goal:** Create three distinct `DataTemplate` resources in `ChatView.xaml` for the three chat visual themes: Classic, Compact, and Bubble. Wire a `ComboBox` in the chat header to switch `IThemeProvider.CurrentChatTheme`. Each template renders hardcoded sample user and assistant messages so the visual difference is immediately apparent.

**Actions:**
1. Add to `MainWindowViewModel.cs`:
   - `[ObservableProperty] ChatTheme _currentChatTheme = ChatTheme.Classic`
   - `[RelayCommand] SetChatTheme(string themeName)` — parses enum, calls `_themeProvider.SetChatTheme()`, updates `CurrentChatTheme`
   - Populate a static `ChatThemeOption[]` for ComboBox binding: `["Classic", "Compact", "Bubble"]`
2. Add three `DataTemplate` resources to `ChatView.xaml` Resources (see reference.md for full XAML):
   - `ClassicMessageTemplate`: Role label + timestamp header, user right/assistant left alignment
   - `CompactMessageTemplate`: Colored dot inline, minimal spacing, no header
   - `BubbleMessageTemplate`: Speech bubbles with tails, rounded corners, timestamp inside
3. Each template binds to hardcoded sample data (e.g., `{Binding Role}`, `{Binding Content}`, `{Binding Timestamp}`) from a local sample model
4. Add a `ComboBox` to the chat header bar bound to `CurrentChatTheme` with `ChatThemeOptions` ItemsSource
5. Implement `WpfThemeProvider.GetChatMessageTemplate(ChatTheme)` — resolves named DataTemplate from `Application.Current.Resources`
6. Implement `WpfThemeProvider.SetChatTheme(ChatTheme)` — guard no-op, persist key `"ChatTheme"`, fire `ChatThemeChanged`

**Files:**
- `src/MySecondBrain.UI/Views/ChatView.xaml` (MODIFY)
- `src/MySecondBrain.UI/Services/WpfThemeProvider.cs` (MODIFY)
- `src/MySecondBrain.UI/ViewModels/MainWindowViewModel.cs` (MODIFY)

**Automated Testing:** `dotnet build` — verify compile with DataTemplates and ComboBox binding. `dotnet test` — verify all 114 existing unit tests still pass (no regressions). Not applicable for DataTemplate rendering unit tests — WPF DataTemplate resolution requires visual tree, verified by manual smoke test only.

**Live Smoke Test (Mandatory):**
1. Build and launch the app — ChatView shows sample messages in Classic layout (role labels above messages, user aligned right)
2. **Locate the ChatTheme dropdown** in the chat header bar (labeled "Theme: Classic")
3. **Switch to Compact**
4. **Observe:** Messages change to compact style — small colored dots replace role labels, vertical spacing shrinks significantly
5. **Switch to Bubble**
6. **Observe:** Messages change to speech-bubble style with rounded corners and tails, user bubbles on right, assistant on left
7. **Switch back to Classic:** Original layout returns
8. Close app, relaunch
9. **Observe:** Previously selected theme persists on relaunch

**Suggested Commit Message:** `feat: add three chat visual themes (Classic/Compact/Bubble) with DataTemplate switching and persistence`

---

### Step 8: ContentRendererRegistry Priority Fix + DI Resolution Verification

**Goal:** Fix the `Priority` values in 6 renderer stubs that don't match the knowledge base specification. Add priority-based sorting to `ContentRendererRegistry` constructor. Verify via unit test that all 7 renderers resolve from DI with correct priorities.

**Actions:**
1. Fix `Priority` property in 6 renderer stub files (see table below)
2. Modify `ContentRendererRegistry.cs` constructor: add `.OrderBy(r => r.Priority)` when populating `_renderers`
3. Add unit test to `DataLayerTests.cs` (or new `RendererTests.cs`):
   - Build DI container via `App.ConfigureServices`
   - Resolve `IContentRendererRegistry`
   - Assert `GetRenderers().Count == 7`
   - Assert each renderer by name and priority in ascending order
4. Run `dotnet test` with filter to confirm the test passes

**Files:**
- `src/MySecondBrain.UI/Controls/CodeBlockRenderer.cs` (MODIFY — Priority: 90→200)
- `src/MySecondBrain.UI/Controls/ArtifactReferenceRenderer.cs` (MODIFY — Priority: 80→300)
- `src/MySecondBrain.UI/Controls/ImageRenderer.cs` (MODIFY — Priority: 70→400)
- `src/MySecondBrain.UI/Controls/MediaRenderer.cs` (MODIFY — Priority: 60→500)
- `src/MySecondBrain.UI/Controls/ThinkingRenderer.cs` (MODIFY — Priority: 50→600)
- `src/MySecondBrain.UI/Controls/ToolCallRenderer.cs` (MODIFY — Priority: 40→700)
- `src/MySecondBrain.UI/Controls/ContentRendererRegistry.cs` (MODIFY — add `.OrderBy(r => r.Priority)` in constructor)
- `tests/unit/MySecondBrain.Tests.Unit/DataLayerTests.cs` (MODIFY — add ContentRendererRegistry resolution test)

**Correct priority order (matching knowledge base §10.4):**
| Renderer | Correct Priority | Current (Wrong) |
|----------|-----------------|-----------------|
| MarkdownTextRenderer | 100 | 100 ✓ |
| CodeBlockRenderer | 200 | 90 ✗ |
| ArtifactReferenceRenderer | 300 | 80 ✗ |
| ImageRenderer | 400 | 70 ✗ |
| MediaRenderer | 500 | 60 ✗ |
| ThinkingRenderer | 600 | 50 ✗ |
| ToolCallRenderer | 700 | 40 ✗ |

**Automated Testing:** `dotnet test tests/unit/MySecondBrain.Tests.Unit/ --filter "FullyQualifiedName~ContentRendererRegistry"` — expects 1 test pass, 0 fail. Also run full suite: `dotnet test` — verify all 115 tests pass (114 existing + 1 new).

**Live Smoke Test (Mandatory):**
1. Build the solution: `dotnet build`
2. Run the targeted test: `dotnet test tests/unit/MySecondBrain.Tests.Unit/ --filter "FullyQualifiedName~ContentRendererRegistry"`
3. **Observe:** Test output shows 1 test passed, 0 failed
4. **Observe:** Test confirms 7 renderers resolved, sorted by correct priority order (100, 200, 300, 400, 500, 600, 700)
5. Run full test suite: `dotnet test`
6. **Observe:** All 115 tests pass (114 existing + 1 new ContentRendererRegistry test)

**Suggested Commit Message:** `fix: correct ContentRendererRegistry renderer priorities, add priority sorting, and unit test`

---

## 6. Shared Technical Context

- [Initial State]: Dark.xaml and Light.xaml define 25+ resource keys. All XAML uses `DynamicResource` for themeable values.
- [After Step 1]: App.xaml merges Dark.xaml at startup. Resource keys: `AppBackground`, `AppForeground`, `SidebarBackground`, `SidebarForeground`, `ContentBackground`, `ContentForeground`, `PanelBackground`, `PanelForeground`, `TabBarBackground`, `TabActiveBackground`, `TabInactiveBackground`, `HeaderBackground`, `InputBackground`, `AccentBrush`, `AccentForeground`, `BorderBrush`, `SubtleBrush`, `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `ScrollBarBrush`, `GridSplitterBrush`, `NavActiveBackground`, `NavInactiveForeground`, `FontFamily`, `FontSize`.
- [After Step 2]: MainWindow Grid columns: `280, Auto(4), *, Auto(4), 320`. Sidebar min 150/max 500, Right panel min 200/max 500 with two-section layout (Artifacts top + Chat Navigation bottom, resizable divider). GridSplitters `ResizeBehavior="PreviousAndNext"`.
- [After Step 3]: `MainWindowViewModel.SelectedScreen` enum: `Chats, Wiki, Media, Artifacts, Usage, Settings`. ContentControl in center with `ScreenTemplateSelector` (DataTemplateSelector pattern — implicit DataTemplate by x:Type does NOT work with enums). 8 screen shells in `Views/` directory.
- [After Step 4]: `WpfThemeProvider` fully implemented. Theme persistence via `ISettingsRepository` key `"AppTheme"`. `SetAppTheme()` clears and re-adds MergedDictionaries. Known constraint: `Clear()` removes ALL merged dictionaries — safe now but must be updated if future features add other merged dictionaries.
- [After Step 5]: Startup sequence: DI build → Migrate → Restore theme from ISettingsRepository → Restore font (family, size, AND weight) → Show MainWindow. Persistence keys: `"AppTheme"`, `"FontFamily"`, `"FontSize"`, `"FontWeight"`.
- [After Step 6]: Font persistence keys: `"FontFamily"` (string), `"FontSize"` (string, e.g. "14"). Clamped to 10-24 range. Displayed in ChatView header.
- [After Step 7]: ChatTheme persistence key: `"ChatTheme"`. Three DataTemplates: Classic (role+timestamp header, left/right alignment), Compact (color dots, minimal spacing), Bubble (speech bubbles with tails).
- [After Step 8]: Renderer priorities: 100(MarkdownText), 200(CodeBlock), 300(ArtifactReference), 400(Image), 500(Media), 600(Thinking), 700(ToolCall). Registry sorts by `.OrderBy(r => r.Priority)` on construction. Unit test verifies 7 renderers resolved in correct priority order.
