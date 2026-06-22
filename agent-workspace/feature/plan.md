# Feature Implementation Plan: Settings, Onboarding & Diagnostics

## 1. Overall Project Context

MySecondBrain is a native Windows 10/11 desktop application built on **.NET 8.0 WPF** with CommunityToolkit.Mvvm — a unified, provider-agnostic AI chat hub paired with a personal wiki. The solution spans 7 projects (Core, Data, Services, UI, Package, Unit Tests, Integration Tests) with a strict compile-time dependency chain: Core ← Data ← Services ← UI. Architecture follows MVVM with source generators, Provider/Adapter pattern for all external integrations, Repository pattern over EF Core SQLite, and Plugin/Registry pattern for content block renderers. DI is managed by `Microsoft.Extensions.DependencyInjection` with ~81 registrations. Logging uses Serilog via `Microsoft.Extensions.Logging` bridge, writing structured JSON to rolling files in `%LOCALAPPDATA%\MySecondBrain\logs\`.

Wave 1 (Foundation) and Wave 2 (Skeleton) are complete. Features 1–7 have built: solution scaffold, DI container, Serilog logging, full data layer (14 entities, EF Core SQLite+FTS5, 8 repositories), three-region MainWindow shell with 8 screen shells + Dark/Light theming, and Windows OS platform (system tray, 6 global hotkeys via RegisterHotKey, Kestrel WebSocket server, auto-update framework), plus Model Configs/API Keys/Personas (full CRUD, DPAPI encryption, auto-fetch models). 263 unit + 7 integration + 44 E2E tests pass.

## 2. Feature-Specific Context

**Feature 8** bundles three interconnected capabilities that all share the Settings screen and `ISettingsRepository` persistence:

1. **Settings Screen (14 remaining categories):** The Settings screen already has two fully-built categories (Providers with API key CRUD, Profiles with Model Configuration + Persona CRUD). This feature adds the 14 remaining categories: Appearance (font/theme/chat theme), Wiki (directory + git config), Backup (GCS config + schedule), Text Actions (CRUD table with capture scope + apply mode), Hotkeys (assignment table + key recorder), Tools (auto-approval toggles + STT provider), Language (RTL auto-detect), Notifications (sound/streaming/mute), Startup (launch on boot/session restore/minimize to tray), Updates (check frequency), Pricing (budget/threshold), Security (encryption status/locked chats), Maintenance (database VACUUM), and Diagnostics (log level + 8 category toggles + log management).

2. **Onboarding Wizard:** A 6-screen first-launch guided setup occupying a dedicated window (no Studio chrome). Screens: Welcome → API Keys (Step 1) → Persona (Step 2) → Wiki Directory (Step 3) → Hotkeys (Step 4) → Finish. Each step is independently skippable, all progress is saved per-step to `ISettingsRepository`. The wizard is re-launchable from Settings → "Re-run Onboarding Wizard." The Finish screen's "Import from ChatGPT or Claude" button is present but non-functional until Feature 18 builds the import infrastructure.

3. **Diagnostics & Debug Logging:** A Serilog `IDestructuringPolicy` (`ApiKeyDestructuringPolicy`) enforces API key redaction across all log categories. Nine `AppSetting` keys (`LogLevel` + 8 `LogCategory_*`) control global minimum log level and per-category toggles. The Diagnostics settings category provides a log level selector (Information/Debug/Verbose), 8 checkbox toggles, "Open Logs Folder" button (launches Windows Explorer at `%LOCALAPPDATA%\MySecondBrain\logs\`), and "Clear Logs" button (deletes all log files with confirmation dialog).

**Dependencies:** Features 4–7 already provide `ISettingsRepository`, `IThemeProvider`, `IApiKeyRepository`, `IPersonaRepository`, `IModelConfigurationRepository`, `IEncryptionService`, `ILLMProviderService`, `IConfirmationService`, `IUpdateChecker`, `IGlobalHotkeyService`, `ISystemTrayService`, and Serilog logging infrastructure. Soft dependency on Feature 18 for the onboarding "Import" button functionality (the button exists but shows a "Coming soon" toast).

## 3. Architecture and Extensibility

### 3.1 Settings Category Pattern — DataTrigger Content Switching

All 16 settings categories share a single `SettingsViewModel`. Category switching uses `DataTrigger` on `SelectedSettingsCategory` to toggle visibility of category-specific `Grid` panels within `SettingsView.xaml`. This is already implemented for Providers and Profiles; the remaining 14 categories follow the same pattern:

```xml
<Grid>
    <Grid.Style>
        <Style TargetType="Grid">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding SelectedSettingsCategory}" Value="Appearance">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Grid.Style>
    <!-- Category-specific XAML content -->
</Grid>
```

**Why DataTrigger, not DataTemplateSelector:** All categories share one ViewModel. `DataTemplateSelector` keys off the bound value type, which is always `SettingsViewModel` — it cannot differentiate categories. DataTrigger on the enum property is the correct WPF pattern for sub-navigation within a single ViewModel. This is documented in the knowledge base at [frontend-ui.md §18](agent-workspace/knowledge/frontend-ui.md#18-settings-category-sub-navigation-pattern).

### 3.2 Settings Persistence Pattern

Every setting follows the same persistence pattern — read at startup (or on category activation), write on change, no "Save" button:

```
User toggles setting → ViewModel property setter →
  ISettingsRepository.SetAsync("KeyName", value) →
  Setting takes effect immediately (ThemeProvider, in-memory state, etc.)
```

Startup restore reads all persisted keys from `ISettingsRepository` and applies them. This pattern already exists for `AppTheme`, `ChatTheme`, `FontFamily`, `FontSize`, `FontWeight`, `WebSocketAuthToken`, and `MinimizeToTray`. This feature adds ~25 new setting keys following the same pattern.

### 3.3 Diagnostics Logging — Serilog IDestructuringPolicy

API key redaction uses Serilog's `IDestructuringPolicy` interface, registered once in `LoggerConfiguration` at startup:

```csharp
.Destructure.With<ApiKeyDestructuringPolicy>()
```

The policy intercepts any `string` value matching API key patterns and replaces it with `"[REDACTED]"`. This is a global enforcement mechanism — no individual logger call site needs to remember to redact. The policy applies to all 8 log categories.

Log level and category toggles are not applied via Serilog's `MinimumLevel` (which is set at configuration time and immutable). Instead, a custom `ILoggerProvider` or filtering wrapper intercepts `ILogger<T>.Log()` calls and checks `ISettingsRepository` for the current log level and category configuration at runtime. This allows immediate changes without restart.

### 3.4 Onboarding Wizard — Step State Machine

The wizard uses a simple integer step index (`CurrentStep`) with `DataTrigger`-based content switching in XAML:

```
Step -1: Welcome (initial screen)
Step 0: API Keys (Step 1 of 4)
Step 1: Persona (Step 2 of 4)  
Step 2: Wiki Directory (Step 3 of 4)
Step 3: Hotkeys (Step 4 of 4)
Step 4: Finish
```

Each step's completion status is persisted to `ISettingsRepository` with keys like `"Onboarding_Step1_Completed"`. On wizard close (X button), completed steps are saved. On next launch, the wizard resumes from the first incomplete step. The view reuses existing ViewModel logic (api key add/test/save from SettingsViewModel patterns, persona creation, etc.).

### 3.5 Extensibility Note — Language Category

The Language category displays a single toggle: "Auto-detect RTL (Hebrew)." The actual RTL rendering pipeline is part of Feature 9 (Studio Chat). This category only exposes the toggle — the rendering engine reads this setting. The enum value `SettingsCategory.Language` replaces the existing `SettingsCategory.Language` (if it already exists in the enum — need to verify). This follows the vision spec [vision-summary.md Flag #16](agent-workspace/project-director/vision/vision-summary.md).

## 4. Final Expected Project Structure

Below is the expected state after all implementation steps. `[MODIFIED]` marks existing files that gain significant new content. `[NEW]` marks files to be created.

- `src/`
  - `MySecondBrain.Core/`
    - `Models/Enums.cs` `[MODIFIED]` — add `SettingsCategory.Language` if not present, verify all 16 enum values exist
    - `Interfaces/`
      - `ISettingsRepository.cs` — unchanged (existing interface sufficient)
      - `IThemeProvider.cs` — unchanged
  - `MySecondBrain.Data/`
    - `Repositories/SettingsRepository.cs` — unchanged (existing implementation sufficient)
    - `Entities/AppSetting.cs` — unchanged
  - `MySecondBrain.Services/`
    - `[NEW] Logging/ApiKeyDestructuringPolicy.cs` — Serilog `IDestructuringPolicy` for API key redaction
    - `[NEW] Logging/RuntimeLogFilter.cs` — runtime log level/category filter wrapping `ILogger<T>`
  - `MySecondBrain.UI/`
    - `App.xaml.cs` `[MODIFIED]` — register `ApiKeyDestructuringPolicy` in Serilog config; add first-launch detection; add log level/category settings restore; wire onboarding wizard launch
    - `ViewModels/`
      - `SettingsViewModel.cs` `[MODIFIED]` — add ~60 properties and ~20 commands for 14 remaining categories + diagnostics
      - `OnboardingWizardViewModel.cs` `[MODIFIED]` — full step state machine, per-step data, navigation commands, persistence
    - `Views/`
      - `SettingsView.xaml` `[MODIFIED]` — add XAML for 14 remaining categories (Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Maintenance, Diagnostics)
      - `OnboardingWizardView.xaml` `[MODIFIED]` — full 6-screen XAML with DataTrigger switching, step dots, navigation bar
      - `OnboardingWizardView.xaml.cs` `[MODIFIED]` — wire PasswordBox, folder picker, key recorder, ViewModel init
    - `Converters/` `[MODIFIED]` — potentially add `BoolToVisibilityConverter` if not already present
- `tests/`
  - `unit/MySecondBrain.Tests.Unit/`
    - `SettingsViewModelTests.cs` `[MODIFIED]` — add tests for new categories (Appearance, Notifications, Startup, Updates, Maintenance, Diagnostics, etc.)
    - `[NEW] OnboardingWizardViewModelTests.cs` — tests for step navigation, skip behavior, data persistence
    - `[NEW] ApiKeyDestructuringPolicyTests.cs` — tests for redaction of various key formats
    - `[NEW] RuntimeLogFilterTests.cs` — tests for log level/category filtering
  - `integration/MySecondBrain.Tests.Integration/`
    - `[NEW] DiagnosticsIntegrationTests.cs` — log file creation, Open Logs Folder, Clear Logs, VACUUM
    - `[NEW] OnboardingIntegrationTests.cs` — wizard completion, settings persistence across restart

## 5. Execution Steps

### [x] Step 1: Diagnostics Infrastructure & Category UI

- **Goal:** Build the complete Diagnostics backend (ApiKeyDestructuringPolicy for API key redaction, runtime log level/category filter) and the Diagnostics settings category UI (log level dropdown, 8 per-category toggle checkboxes, "Open Logs Folder" button, "Clear Logs" button). After this step, the Diagnostics category is fully functional — the user can configure log verbosity and manage log files from the Settings screen.

- **Actions:**
  1. Create `src/MySecondBrain.Services/Logging/ApiKeyDestructuringPolicy.cs` — Serilog `IDestructuringPolicy` that checks if a value looks like an API key (length > 20, high entropy, common prefixes like `sk-`, `sk-ant-`, `AIza`) and replaces with `"[REDACTED]"`.
  2. Register `ApiKeyDestructuringPolicy` in `App.xaml.cs` `ConfigureServices` via `.Destructure.With<ApiKeyDestructuringPolicy>()` in the `LoggerConfiguration` chain.
  3. Create `src/MySecondBrain.Services/Logging/RuntimeLogFilter.cs` — a wrapper around `ILogger<T>` that checks `ISettingsRepository` at runtime for `LogLevel` (minimum level) and 8 `LogCategory_*` boolean keys, filtering log calls accordingly. Use `SourceContext` from log event to determine which category a log call belongs to.
  4. Add 9 settings keys to `App.xaml.cs` startup restore: read `LogLevel` (default `"Information"`) and 8 `LogCategory_*` booleans, apply to the runtime filter.
  5. Add ViewModel properties to `SettingsViewModel.cs`:
     - `LogLevel` (string, bound to ComboBox: "Information", "Debug", "Verbose")
     - `LogCategory_LLMApiCalls`, `LogCategory_Tier1HotkeyPipeline`, `LogCategory_Tier2CommandBar`, `LogCategory_Database`, `LogCategory_WikiFileSystem`, `LogCategory_WebSocket`, `LogCategory_StartupShutdown`, `LogCategory_SystemIntegration` (all bool, bound to CheckBox)
     - Read initial values from `ISettingsRepository` in `InitializeAsync`, persist on every change via partial `On*Changed` methods.
  6. Add commands to `SettingsViewModel.cs`:
     - `OpenLogsFolderCommand` — creates directory if missing, then `Process.Start("explorer.exe", logsFolderPath)`
     - `ClearLogsCommand` — shows confirmation via `IConfirmationService`, deletes all `*.log` and `*.json` files in logs folder, shows status message
  7. Add XAML for the Diagnostics category in `SettingsView.xaml` following the existing `DataTrigger` pattern:
     - Category header with title "🩺 Diagnostics" and description
     - Log Level `ComboBox` with items "Information", "Debug", "Verbose"
     - 8 `CheckBox` controls with labels matching the vision spec descriptions
     - "Open Logs Folder" `Button` (accent style)
     - "Clear Logs" `Button` (secondary style)
     - Status message `TextBlock` at bottom

- **Unit Tests to Write:**
  - `tests/unit/ApiKeyDestructuringPolicyTests.cs`: Test `TryDestructure` with OpenAI key (`sk-...`), Anthropic key (`sk-ant-...`), Google key (`AIza...`), short non-key strings, null value, empty string. Verify each returns `[REDACTED]` or `false` (passthrough) correctly.
  - `tests/unit/RuntimeLogFilterTests.cs`: Test that Information level passes Information logs but blocks Debug; test that disabled category blocks all levels; test that Verbose passes all levels; test category matching via `SourceContext`.
  - `tests/unit/SettingsViewModelTests.cs` (modify): Add tests for `OpenLogsFolderCommand` (verifies `Process.Start` called with correct path), `ClearLogsCommand` (verifies confirmation flow + file deletion), log level change persistence, 8 category toggle defaults (3 ON, 5 OFF).

- **Integration Tests to Write:**
  - `tests/integration/DiagnosticsIntegrationTests.cs`: Test that writing a log message creates a log file at the expected path; test that "Clear Logs" deletes all log files; test that "Open Logs Folder" creates the directory if missing; test that VACUUM reduces database size.

- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~ApiKeyDestructuringPolicyTests|FullyQualifiedName~RuntimeLogFilter|FullyQualifiedName~SettingsViewModelTests" --verbosity normal`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~DiagnosticsIntegrationTests" --verbosity normal`

- **Live Smoke Test (Mandatory):**
  1. Build and launch: `dotnet build src/MySecondBrain.UI/MySecondBrain.UI.csproj` then launch `src\MySecondBrain.UI\bin\Debug\net8.0-windows10.0.17763.0\MySecondBrain.UI.exe`
  2. Navigate to Settings → Diagnostics category
  3. Verify Log Level dropdown shows "Information" (default)
  4. Verify 3 checkboxes checked (LLM API Calls, Tier 1 Hotkey, Tier 2 Command Bar), 5 unchecked
  5. Change Log Level to "Debug" — verify the dropdown updates
  6. Toggle "Database" checkbox ON — verify it checks
  7. Click "Open Logs Folder" — verify Windows Explorer opens at `%LOCALAPPDATA%\MySecondBrain\logs\`
  8. Close the app, relaunch, navigate back to Diagnostics — verify settings persisted (Debug level + Database checkbox ON)
  9. Click "Clear Logs" — verify confirmation dialog appears, click Yes, verify "All log files cleared" message

- **Smoke Test Classification:** HUMAN/SHT REQUIRED — GUI interaction for navigation, toggle verification, file explorer launch, and restart persistence check.

- **Suggested Commit Message:** `feat: add diagnostics infrastructure with API key redaction, log level/category controls, and log file management`

---

### [x] Step 2: Appearance, Notifications, Startup, Updates Categories

- **Goal:** Build four settings categories with full ViewModel properties, persistence, and XAML. The user can configure visual appearance (Dark/Light mode, chat theme, font), notification preferences (sound, streaming, cross-tab alerts), startup behavior (launch on boot, session restore, minimize to tray), and auto-update settings (check frequency, current version display).

- **Actions:**
  1. **Appearance category — ViewModel properties:**
     - `AppTheme` (enum: Dark/Light) — already backed by `IThemeProvider`, just expose as bindable property with persistence on change
     - `ChatTheme` (enum: Classic/Compact/Bubble) — already backed by `IThemeProvider`
     - `FontFamily` (string), `FontSize` (double, 10–24), `FontWeight` (string: Normal/Bold) — already backed by `IThemeProvider`
     - Partial `On*Changed` handlers that call `_themeProvider.Set*()` and persist via `_settingsRepo.SetAsync()`
  2. **Appearance category — XAML:**
     - Dark/Light toggle (RadioButton group or ToggleButton)
     - Chat theme `ComboBox`
     - Font family `ComboBox` with system fonts (or `TextBox` with common presets)
     - Font size `Slider` (10–24) with live value display
     - Font weight `ComboBox` (Normal, Bold)
     - Live preview area with sample text ("The quick brown fox...") rendered with current settings
  3. **Notifications category — ViewModel properties:**
     - `SoundOnCompletion` (bool, default: false) — persisted via `ISettingsRepository`
     - `DisableStreaming` (bool, default: false) — persisted
     - `CrossTabCompletionAlert` (bool, default: true) — persisted
  4. **Notifications category — XAML:** Three `CheckBox` controls with descriptive labels.
  5. **Startup category — ViewModel properties:**
     - `LaunchOnWindowsStartup` (bool, default: false) — writes registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` or startup folder shortcut
     - `RestoreLastSession` (bool, default: false) — persisted
     - `MinimizeToTray` (bool, default: true) — already exists as `AppSetting` key `"MinimizeToTray"`, expose as bindable property
  6. **Startup category — XAML:** Three `CheckBox` controls.
  7. **Updates category — ViewModel properties:**
     - `UpdateCheckFrequency` (string: "OnStartup"/"Daily"/"Weekly"/"ManualOnly", default: "OnStartup") — RadioButton group
     - `CurrentVersion` (string, read-only) — from `Assembly.GetEntryAssembly().GetName().Version`
     - `CheckForUpdatesCommand` — calls `IUpdateChecker.CheckForUpdatesAsync()`, displays result in status
  8. **Updates category — XAML:** RadioButton group for frequency, read-only version display, "Check Now" button.

- **Unit Tests to Write:**
  - `tests/unit/SettingsViewModelTests.cs` (modify): Add tests for `AppTheme` change → `IThemeProvider.SetAppTheme()` called + `ISettingsRepository.SetAsync("AppTheme", ...)` called; `FontSize` clamping at boundary values; `SoundOnCompletion` persistence; `MinimizeToTray` reads existing key; `LaunchOnWindowsStartup` registry write; `CheckForUpdatesCommand` calls `IUpdateChecker`.

- **Integration Tests to Write:**
  - None — these categories are pure UI settings persistence. The `LaunchOnWindowsStartup` registry write is a platform integration tested via unit mock; actual registry verification is a smoke test concern.

- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~SettingsViewModelTests" --verbosity normal`

- **Live Smoke Test (Mandatory):**
  1. Launch the app, navigate to Settings → Appearance
  2. Toggle Dark/Light mode — verify entire app theme changes instantly
  3. Change Chat Theme to "Bubble" — verify the preview area updates (or note that chat view will use Bubble on next chat load)
  4. Drag font size slider to 18 — verify live preview text enlarges
  5. Navigate to Notifications, toggle "Sound on Completion" ON, toggle "Disable Streaming" ON
  6. Navigate to Startup, verify "Minimize to Tray" is checked by default, toggle "Launch on Windows Startup" ON
  7. Navigate to Updates, verify current version is displayed, change frequency to "Weekly"
  8. Close and relaunch the app — verify all settings persisted (Dark mode, Bubble theme, font size 18, sound ON, streaming disabled, startup settings, weekly updates)

- **Smoke Test Classification:** HUMAN/SHT REQUIRED — GUI visual verification of theme change, font rendering, and cross-category persistence.

- **Suggested Commit Message:** `feat: add Appearance, Notifications, Startup, and Updates settings categories with full persistence`

---

### [ ] Step 3: Language, Maintenance, Wiki, Backup, Tools, Pricing, Security Categories

- **Goal:** Build seven settings categories. The user can configure RTL language detection, perform database maintenance (VACUUM), set up wiki directory and git, configure backup, manage tool auto-approval defaults and STT provider, set pricing budget alerts, and view security status.

- **Actions:**
  1. **Language category — ViewModel + XAML:**
     - `AutoDetectRtl` (bool, default: true) — persisted via `ISettingsRepository`
     - Single `CheckBox` with description: "Auto-detect RTL text (Hebrew Unicode range U+0590–U+05FF)"
  2. **Maintenance category — ViewModel + XAML:**
     - `DatabaseFileSize` (string, read-only) — file size of `msb.db` in human-readable format (KB/MB/GB)
     - `ReclaimableSpace` (string, read-only) — estimated reclaimable space
     - `LastCompaction` (string, read-only) — from `ISettingsRepository` key `"LastCompaction"`
     - `CompactDatabaseCommand` — runs `PRAGMA vacuum;` via raw SQL on `AppDbContext`, captures before/after sizes, updates properties and status message. Shows progress indicator during operation.
     - XAML: Read-only file size + reclaimable space displays, "Compact Database" button with progress feedback, last compaction timestamp.
  3. **Wiki category — ViewModel + XAML:**
     - `WikiDirectoryPath` (string) — current wiki directory, persisted via `ISettingsRepository`
     - `IndexingStatus` (string, read-only) — e.g., "✓ 42 .md files indexed"
     - `GitVersionControlEnabled` (bool, default: false) — persisted
     - `ChangeWikiDirectoryCommand` — opens `System.Windows.Forms.FolderBrowserDialog` (or `Microsoft.Win32.OpenFolderDialog`), validates directory, updates path, triggers re-index
     - `ReindexWikiCommand` — calls `IWikiService.IndexAllAsync()`, shows progress
     - XAML: Path display with "Change" button, index status with "Re-index Now" button, git checkbox with "Initialize git repository" label.
  4. **Backup category — ViewModel + XAML:**
     - `BackupProviderStatus` (string, read-only) — "Google Cloud Storage: Not configured" or "✓ Configured"
     - `BackupSchedule` (string: "Daily"/"Weekly"/"ManualOnly", default: "Daily") — RadioButton group
     - `LastBackupTime` (string, read-only) — from `ISettingsRepository`
     - `BackupNowCommand` — calls `IBackupProvider.UploadAsync()`, shows progress
     - `ConfigureBackupCommand` — placeholder that shows "Backup configuration coming in Feature 16"
     - XAML: Status display, schedule radio buttons, "Backup Now" button, last backup timestamp.
  5. **Tools category — ViewModel + XAML:**
     - `WebSearchAutoApproval` (string: "Ask"/"AutoApprove"/"Disabled", default: "Ask")
     - `TerminalAutoApproval` (string, default: "Ask" — always shows Ask, cannot be AutoApproved per security)
     - `FileGenerateAutoApproval` (string, default: "Ask")
     - `FileEditAutoApproval` (string, default: "Ask")
     - `SttProvider` (string: "OpenAI Whisper"/"Local Whisper"/"Windows Speech") — ComboBox
     - `SttModel` (string) — model identifier
     - `TestMicrophoneCommand` — placeholder
     - XAML: 4 tool rows with ComboBox (Ask/AutoApprove/Disabled), STT section with provider dropdown + model input + "Test Microphone" button.
  6. **Pricing category — ViewModel + XAML:**
     - `MonthlyBudgetLimit` (decimal?, default: null = no limit)
     - `WarningThreshold` (int, default: 80, range: 50–100) — percentage
     - `BlockApiOnLimit` (bool, default: false)
     - XAML: Numeric input for budget, slider or numeric input for threshold, block toggle.
  7. **Security category — ViewModel + XAML:**
     - `EncryptionStatus` (string, read-only) — "✓ API keys encrypted via Windows DPAPI"
     - `LockedChatPasswordSet` (bool, read-only) — whether a global locked chat password exists
     - `HideLockedChats` (bool, default: false) — persisted
     - `SetGlobalPasswordCommand` — placeholder dialog
     - XAML: Status indicators (green checkmarks), "Set Global Password" / "Change Global Password" button, hide toggle.

- **Unit Tests to Write:**
  - `tests/unit/SettingsViewModelTests.cs` (modify): Test `CompactDatabaseCommand` (mocks `AppDbContext.Database.ExecuteSqlRawAsync`), test `AutoDetectRtl` persistence, test `GitVersionControlEnabled` persistence, test tool auto-approval enum binding, test pricing threshold clamping, test security status read-only values.

- **Integration Tests to Write:**
  - `tests/integration/DiagnosticsIntegrationTests.cs` (modify): Add `Vacuum_ReducesDatabaseSize` test — create large data, run VACUUM, verify size reduction. Add `WikiDirectory_Change_TriggersReindex` test.

- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~SettingsViewModelTests" --verbosity normal`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~DiagnosticsIntegrationTests" --verbosity normal`

- **Live Smoke Test (Mandatory):**
  1. Navigate to Language — verify "Auto-detect RTL" is checked by default
  2. Navigate to Maintenance — verify database file size is displayed, click "Compact Database", verify before/after sizes change and "Space reclaimed" is shown (if any)
  3. Navigate to Wiki — if no wiki directory set, verify empty state message. Click "Change", select a folder with .md files, verify indexing status updates
  4. Navigate to Backup — verify status shows "Not configured", change schedule to "Weekly"
  5. Navigate to Tools — change Web Search to "AutoApprove", verify `ISettingsRepository` persists
  6. Navigate to Pricing — set budget to 50, threshold to 90, toggle Block ON. Verify persistence on relaunch
  7. Navigate to Security — verify DPAPI encryption status shows green checkmark
  8. Close and relaunch — spot-check Language, Tools, and Pricing settings persisted

- **Smoke Test Classification:** HUMAN/SHT REQUIRED — GUI navigation, VACUUM progress verification, folder picker interaction, multi-category persistence check.

- **Suggested Commit Message:** `feat: add Language, Maintenance, Wiki, Backup, Tools, Pricing, and Security settings categories`

---

### [ ] Step 4: Text Actions & Hotkeys Categories

- **Goal:** Build the Text Actions management table (view all, create, edit, duplicate, delete) with capture scope multi-select checkboxes, apply mode radio buttons, and model config/hotkey assignment. Build the Hotkeys assignment table showing all Text Actions + Command Bar with their assigned hotkeys, "Change" button that opens a key recorder overlay, and "Reset to Defaults" link.

- **Actions:**
  1. **Text Actions ViewModel — list + CRUD:**
     - `TextActions` (`ObservableCollection<TextActionDisplayItem>`) — loaded from `ITextActionRepository`. If this interface and its `TextActionRepository` implementation do not yet exist, create them following the existing repository pattern (see `ApiKeyRepository` or `PersonaRepository` for the template). The `TextAction` entity already exists at `src/MySecondBrain.Data/Entities/TextAction.cs` with seed data in `AppDbContext.OnModelCreating`.
     - `IsEditingTextAction`, `EditingTextAction` — form state
     - `AddTextActionCommand`, `EditTextActionCommand`, `SaveTextActionCommand`, `DeleteTextActionCommand`, `DuplicateTextActionCommand`
     - Capture scope multi-select: 5 bool properties (`CaptureSelection`, `CaptureFocusedElement`, `CaptureSurroundingContext`, `CaptureFullDocument`, `CaptureScreenshot`)
     - Apply mode: `SelectedApplyMode` (string, bound to RadioButtons)
     - Model config dropdown: populated from `ModelConfigurations`
     - Hotkey assignment: `AssignedHotkey` (string), `AssignHotkeyCommand` — opens key recorder overlay
  2. **Text Actions XAML:**
     - Table/list of all TextActions with columns: Name, System Prompt (truncated), Capture Scope (badges), Apply Mode, Model Config, Hotkey. Actions: Edit, Duplicate, Delete.
     - "+ New Text Action" button
     - Edit form with: Display Name `TextBox`, System Prompt multi-line `TextBox`, Model Config `ComboBox`, Capture Scope 5 `CheckBox` controls, Apply Mode 7 `RadioButton` controls, Hotkey display + "Assign Hotkey" button, Save/Cancel buttons
  3. **Hotkeys ViewModel:**
     - `HotkeyAssignments` (`ObservableCollection<HotkeyAssignmentDisplayItem>`) — populated from TextActions + Command Bar
     - `ChangeHotkeyCommand(HotkeyAssignmentDisplayItem)` — opens key recorder overlay overlay (captures next key combo)
     - `ResetHotkeysToDefaultsCommand` — resets all hotkeys to built-in defaults (Alt+Q/W/E/R/C, Alt+Space)
     - Key conflict detection: before saving, check against existing assignments and system hotkeys
  4. **Hotkeys XAML:**
     - Table with columns: Action Name, Capture Scope, Apply Mode, Hotkey, [Change] button
     - "Reset to Defaults" hyperlink
     - Key recorder overlay: a `Popup` or `Border` that appears over the content, captures keyboard input until a valid combo is pressed or Escape cancels
  5. **TextActionDisplayItem wrapper** — similar to existing `ModelConfigurationDisplayItem`, wraps entity fields + computed display properties

- **Unit Tests to Write:**
  - `tests/unit/SettingsViewModelTests.cs` (modify): Test `AddTextActionCommand` initializes form with defaults (capture scope = selection, apply mode = replaceSelection); test `SaveTextActionCommand` validates name is required; test `DeleteTextActionCommand` shows confirmation; test `DuplicateTextActionCommand` appends "(Copy)"; test capture scope flags serialization; test apply mode serialization; test `ChangeHotkeyCommand` opens recorder; test `ResetHotkeysToDefaultsCommand` restores defaults; test hotkey conflict detection.

- **Integration Tests to Write:**
  - `tests/integration/DiagnosticsIntegrationTests.cs` (modify): Add `TextAction_CreateAndPersist` — create text action via repository, verify it appears in GetAllAsync. Add `TextAction_Delete_CascadesHotkeyUnassignment`.

- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~SettingsViewModelTests" --verbosity normal`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~TextAction" --verbosity normal`

- **Live Smoke Test (Mandatory):**
  1. Navigate to Settings → Text Actions
  2. Verify 10 built-in Text Actions are displayed in the table
  3. Click "Edit" on "Rewrite" — verify form opens with pre-filled name, system prompt, capture scope "selection", apply mode "replaceSelection", hotkey "Alt+Q"
  4. Click Cancel, then click "+ New Text Action" — verify empty form
  5. Fill name "Test Action", enter system prompt, select capture scopes, choose apply mode, click Save — verify new action appears in list
  6. Click "Duplicate" on the new action — verify "(Copy)" suffix appears
  7. Navigate to Hotkeys — verify table shows 6 default hotkeys (Alt+Q/W/E/R/C, Alt+Space)
  8. Click "Change" on Alt+Q — verify key recorder opens
  9. Press a new key combo — verify it appears in the hotkey column
  10. Click "Reset to Defaults" — verify all hotkeys restored

- **Smoke Test Classification:** HUMAN/SHT REQUIRED — GUI form interaction, key recorder overlay, table verification, multi-action workflow.

- **Suggested Commit Message:** `feat: add Text Actions management and Hotkeys assignment settings categories`

---

### [ ] Step 5: Onboarding Wizard — Full Implementation

- **Goal:** Build the complete 6-screen Onboarding Wizard with step indicator dots, Back/Next/Skip navigation, per-step content (API keys, persona selection, wiki directory, hotkeys review, finish summary), data persistence per step, and close-with-progress-saved behavior. The wizard occupies a dedicated full window with no Studio chrome.

- **Actions:**
  1. **OnboardingWizardViewModel — State Machine:**
     - `CurrentStep` (int: -1 = Welcome, 0 = API Keys, 1 = Persona, 2 = Wiki, 3 = Hotkeys, 4 = Finish)
     - `CanGoBack`, `CanGoNext`, `CanSkip` (bool, computed from CurrentStep)
     - `BackCommand`, `NextCommand`, `SkipCommand`
     - `Step1Completed`, `Step2Completed`, `Step3Completed`, `Step4Completed` (bool, persisted to `ISettingsRepository`)
     - Per-step data properties reused from SettingsViewModel patterns or implemented directly:
       - **Step 0 (API Keys):** `ApiKeyInputValue`, `SelectedProviderType`, `DisplayNameInputValue`, added keys list, `TestApiKeyCommand`, `AddKeyCommand`, `RemoveKeyCommand`
       - **Step 1 (Persona):** Three starter persona cards (General Assistant, Code Helper, Writing Coach), `SelectedStarterPersona`, `CustomPersonaName`, `CustomSystemPrompt`, `CustomChatMode`, `SavePersonaCommand`, "Create from Scratch" link
       - **Step 2 (Wiki Directory):** `WikiDirectoryPath`, `ChooseExistingFolderCommand`, `CreateNewWikiFolderCommand`, `GitVersionControlEnabled`, git sub-options (auto-commit, GitHub token/repo)
       - **Step 3 (Hotkeys):** `HotkeyAssignments` list (same as Settings Hotkeys category), `ChangeHotkeyCommand`, `ResetToDefaultsCommand`
       - **Step 4 (Finish):** Summary properties: `ConfiguredKeyCount`, `ConfiguredPersonaName`, `ConfiguredWikiPath`, `ConfiguredHotkeyCount`. `LaunchStudioCommand` — closes wizard and opens Studio. `ImportFromChatGptCommand` — shows "Coming soon" toast.
  2. **OnboardingWizardView.xaml — Full XAML with DataTrigger:**
     - Region 1 (Header): App icon + "MySecondBrain" + "Onboarding" label
     - Region 2 (Step Indicator): 4 horizontal dots (○/●/✓) with labels, only visible on steps 0–3
     - Region 3 (Content Card): `DataTrigger` on `CurrentStep` to switch content panels:
       - Screen -1 (Welcome): Large app icon, tagline, 3 feature cards, "Get Started" button
       - Screen 0 (API Keys): Provider dropdown, key input (PasswordBox), display name, "Add Key" button, added keys list with masked values + validation status, "Test All Keys" button
       - Screen 1 (Persona): 3 persona cards (clickable), customization panel (name, system prompt, chat mode), "Save Persona" button, "Create from Scratch" link
       - Screen 2 (Wiki): "Choose Existing Folder" button, "Create New Wiki Folder" button, path display with file count, git checkbox with sub-options
       - Screen 3 (Hotkeys): Hotkey table with Change buttons, "Reset to Defaults" link
       - Screen 4 (Finish): Large checkmark, summary card (keys/persona/wiki/hotkeys counts), "Launch Studio" button, "Import from ChatGPT or Claude" button (non-functional — shows toast)
     - Region 4 (Navigation Bar): Back (disabled on Welcome), Skip (hidden on Welcome/Finish), Next/Finish button
  3. **OnboardingWizardView.xaml.cs:** Wire PasswordBox changes, folder picker dialog, key recorder popup, ViewModel initialization on Loaded.
  4. **Wizard Close Behavior:** Override close (X button) — if not on Finish screen, show confirmation: "You haven't finished setup. Your progress is saved. Continue Setup / Close Anyway." Save completed step flags to `ISettingsRepository`.
  5. **Wizard Resume Logic:** On launch, check `Onboarding_Step1_Completed` through `Onboarding_Step4_Completed`. If any are false, set `CurrentStep` to the first incomplete step. If all true, wizard does not auto-open (goes to Studio).

- **Unit Tests to Write:**
  - `tests/unit/OnboardingWizardViewModelTests.cs`: Test `CurrentStep` defaults to -1 (Welcome); test `BackCommand` decrements step; test `NextCommand` increments and persists completion flag; test `SkipCommand` skips step and persists flag; test `CanGoBack` false on Welcome, true on steps 0–3; test `CanSkip` false on Welcome/Finish; test step 0 `AddKeyCommand` validates non-empty and non-duplicate; test step 1 starter persona selection populates customization panel; test step 2 `CreateNewWikiFolderCommand` creates directory + `index.md`; test `LaunchStudioCommand` fires messenger event; test `ImportFromChatGptCommand` shows toast.

- **Integration Tests to Write:**
  - `tests/integration/OnboardingIntegrationTests.cs`: Test full wizard flow — step through all 5 steps, verify `Onboarding_Step*_Completed` keys are set in `AppSettings` table; test wizard resume — set only Step 1 and 2 completed, relaunch, verify wizard opens at Step 3; test skip all steps — verify all 4 steps marked completed and wizard does not re-open.

- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~OnboardingWizardViewModelTests" --verbosity normal`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~OnboardingIntegrationTests" --verbosity normal`

- **Live Smoke Test (Mandatory):**
  1. Delete `Onboarding_Step*` keys from AppSettings table (or delete the entire database for clean first launch)
  2. Launch the app — verify Onboarding Wizard appears with Welcome screen
  3. Click "Get Started" — verify Step 1 (API Keys) with step dots showing ●○○○
  4. Add an API key (enter test key, click Add Key) — verify it appears in list with "Not tested" status
  5. Click "Test All Keys" (or individual Test) — verify status updates
  6. Click Skip — verify Step 2 (Persona) with ●●○○ dots
  7. Select "Code Helper" card — verify customization panel appears with pre-filled values
  8. Click Skip (uses General Assistant default) — verify Step 3 (Wiki) with ●●●○ dots
  9. Click "Create New Wiki Folder" — verify folder created and path displayed with index.md info
  10. Click Skip — verify Step 4 (Hotkeys) with ●●●● dots
  11. Verify default hotkey table, click Skip — verify Finish screen
  12. Verify summary shows: 1 API key, Persona: Code Helper (or General Assistant if skipped), Wiki path, 5 hotkeys
  13. Click "Import from ChatGPT or Claude" — verify "Coming soon" toast appears
  14. Click "Launch Studio" — verify Studio opens with new chat
  15. Close app, relaunch — verify wizard does NOT reappear (all steps completed)

- **Smoke Test Classification:** HUMAN/SHT REQUIRED — Full multi-step GUI wizard walkthrough, visual verification of step dots, card selection, folder creation, toast notification, and Studio transition.

- **Suggested Commit Message:** `feat: implement full 6-screen Onboarding Wizard with step state machine, progress persistence, and resume support`

---

### [ ] Step 6: First-Launch Detection, Startup Integration & Polish

- **Goal:** Wire the Onboarding Wizard into the application lifecycle. On first launch (no existing settings), auto-open the wizard. Wire the Settings → "Re-run Onboarding Wizard" link. Wire the Finish screen's "Launch Studio" to close the wizard and open the main window. Add onboarding-related settings keys. Final integration testing.

- **Actions:**
  1. **First-Launch Detection in App.xaml.cs `OnStartup`:**
     - After DI build and migration, check `ISettingsRepository.GetAsync("Onboarding_Step4_Completed")`
     - If null or false: resolve `OnboardingWizardViewModel`, show `OnboardingWizardWindow` (a new standalone `Window`, not a `UserControl` within MainWindow's ContentControl)
     - The onboarding window is a dedicated `Window` with no Studio chrome — it replaces MainWindow on first launch
     - On "Launch Studio": close wizard window, show MainWindow
     - If all 4 steps completed: skip wizard, show MainWindow directly
  2. **Create `OnboardingWizardWindow`** — a dedicated WPF `Window` (not `UserControl`) that hosts `OnboardingWizardView` as its content. Window properties: `Width="700"`, `Height="600"`, `WindowStartupLocation="CenterScreen"`, `ResizeMode="CanResize"`, `WindowStyle="SingleBorderWindow"`.
  3. **Wire Settings → "Re-run Onboarding Wizard" link:**
     - In `SettingsView.xaml`, the existing `Hyperlink` at bottom of sidebar needs a `Command` binding
     - Add `ReRunOnboardingCommand` to `SettingsViewModel` — sends a `WeakReferenceMessenger` message that `App.xaml.cs` listens for, which opens the `OnboardingWizardWindow`
  4. **Onboarding Wizard non-functional button:**
     - "Import from ChatGPT or Claude" on Finish screen — clicking shows a toast via `IConfirmationService.ShowInfoAsync("Coming Soon", "Chat import will be available in a future update.")` or a brief status message
  5. **SettingsViewModel constructor dependencies:** The following services are needed by the new category implementations and must be added to the `SettingsViewModel` constructor and DI registration (all are already registered in the DI container):
     - `IUpdateChecker` — for Updates category "Check Now" button
     - `IWikiService` — for Wiki category "Re-index Now" button
     - `IBackupProvider` — for Backup category "Backup Now" button (placeholder until Feature 16)
     - `AppDbContext` — for Maintenance category VACUUM operation
     - `ITextActionRepository` — for Text Actions CRUD (see Step 4 Action 1 note; create the repository if it does not already exist)
  6. **Settings keys registry update:**
     - Add all new `AppSetting` keys to `App.xaml.cs` and document them:
       - `LogLevel`, 8 `LogCategory_*` booleans
       - `SoundOnCompletion`, `DisableStreaming`, `CrossTabCompletionAlert`
       - `LaunchOnWindowsStartup`, `RestoreLastSession`
       - `UpdateCheckFrequency`
       - `AutoDetectRtl`, `MonthlyBudgetLimit`, `WarningThreshold`, `BlockApiOnLimit`
       - `HideLockedChats`
       - `WikiDirectoryPath`, `GitVersionControlEnabled`
       - `BackupSchedule`
       - `WebSearchAutoApproval`, `TerminalAutoApproval`, `FileGenerateAutoApproval`, `FileEditAutoApproval`, `SttProvider`, `SttModel`
       - `Onboarding_Step1_Completed`, `Onboarding_Step2_Completed`, `Onboarding_Step3_Completed`, `Onboarding_Step4_Completed`, `Onboarding_Completed`
  7. **DI Registration:** Register `RuntimeLogFilter` as a decorator/wrapper for `ILogger<T>` or as an `ILoggerProvider` in `ConfigureServices`. Register `OnboardingWizardWindow` as transient. Register any newly created repositories (e.g., `ITextActionRepository` → `TextActionRepository`) as singletons.

- **Unit Tests to Write:**
  - `tests/unit/SettingsViewModelTests.cs` (modify): Test `ReRunOnboardingCommand` sends correct messenger message.
  - `tests/unit/OnboardingWizardViewModelTests.cs` (modify): Test `LaunchStudioCommand` sends messenger message; test `ImportFromChatGptCommand` sets status message to "Coming soon".

- **Integration Tests to Write:**
  - `tests/integration/OnboardingIntegrationTests.cs` (modify): Add `FirstLaunch_Detected_WizardOpens` — set up clean settings, verify `Onboarding_Step4_Completed` is null; add `AllStepsCompleted_WizardSkipped` — set all 4 step keys to true, verify wizard does not open.

- **Automated Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~OnboardingWizardViewModelTests" --verbosity normal`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~OnboardingIntegrationTests" --verbosity normal`

- **Live Smoke Test (Mandatory):**
  1. Delete all `Onboarding_*` keys from the `AppSettings` table (or delete `msb.db`)
  2. Launch the app — verify Onboarding Wizard opens as a dedicated window (NOT inside MainWindow)
  3. Complete all 4 steps (can Skip through them quickly)
  4. Click "Launch Studio" — verify the wizard window closes and MainWindow opens
  5. In Studio, navigate to Settings — verify "Re-run Onboarding Wizard" link is visible at bottom of sidebar
  6. Click the link — verify wizard re-opens
  7. Close the wizard mid-way (X button) — verify "Continue Setup / Close Anyway" dialog
  8. Restart the app — verify wizard resumes at the first incomplete step
  9. Complete all steps again, Launch Studio
  10. Restart the app — verify wizard does NOT appear (goes directly to Studio)

- **Smoke Test Classification:** HUMAN/SHT REQUIRED — Multi-window lifecycle (wizard ↔ main window), close/resume behavior, first-launch detection, link navigation.

- **Suggested Commit Message:** `feat: wire first-launch onboarding detection, wizard re-launch from Settings, and startup integration`

---

## 6. Shared Technical Context

- **AppSetting Keys (added by this feature):** `LogLevel`, `LogCategory_LLMApiCalls`, `LogCategory_Tier1HotkeyPipeline`, `LogCategory_Tier2CommandBar`, `LogCategory_Database`, `LogCategory_WikiFileSystem`, `LogCategory_WebSocket`, `LogCategory_StartupShutdown`, `LogCategory_SystemIntegration`, `SoundOnCompletion`, `DisableStreaming`, `CrossTabCompletionAlert`, `LaunchOnWindowsStartup`, `RestoreLastSession`, `UpdateCheckFrequency`, `AutoDetectRtl`, `MonthlyBudgetLimit`, `WarningThreshold`, `BlockApiOnLimit`, `HideLockedChats`, `WikiDirectoryPath`, `GitVersionControlEnabled`, `BackupSchedule`, `WebSearchAutoApproval`, `TerminalAutoApproval`, `FileGenerateAutoApproval`, `FileEditAutoApproval`, `SttProvider`, `SttModel`, `Onboarding_Step1_Completed`, `Onboarding_Step2_Completed`, `Onboarding_Step3_Completed`, `Onboarding_Step4_Completed`, `Onboarding_Completed`, `LastCompaction`

- **Existing Keys (unchanged):** `AppTheme`, `ChatTheme`, `FontFamily`, `FontSize`, `FontWeight`, `WebSocketAuthToken`, `MinimizeToTray`, `RecentPersonaIds`

- **Serilog Configuration (modified in ConfigureServices):** Add `.Destructure.With<ApiKeyDestructuringPolicy>()` to the existing `LoggerConfiguration` chain.

- **DI Registrations (added):** `RuntimeLogFilter` as singleton for log filtering. `OnboardingWizardWindow` as transient (new window each time wizard is shown).

- **WeakReferenceMessenger Messages:** `LaunchStudioMessage` (sent from OnboardingWizardViewModel, received by App.xaml.cs to close wizard and show MainWindow). `ReRunOnboardingMessage` (sent from SettingsViewModel, received by App.xaml.cs to open wizard).

- **Enum Values:** `SettingsCategory.Language` (verify it exists in the enum; if not, add it). All 16 categories must be present: Providers, Profiles, Appearance, Wiki, Backup, TextActions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Maintenance, Diagnostics.

- **VACUUM Implementation:** `await _db.Database.ExecuteSqlRawAsync("VACUUM;")` — SQLite's VACUUM rebuilds the database file, reclaiming space from deleted rows. Requires temporary free disk space equal to database size. Before/after file sizes obtained via `new FileInfo(dbPath).Length`.
