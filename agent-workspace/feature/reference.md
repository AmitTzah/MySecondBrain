# Feature Reference: E2E Test Suite Rewrite & Authoring Guide

## Global & Shared Documentation

### E2E Authoring Guide (Behavioral Spec)
- **Path:** [`agent-workspace/external-docs/e2e-authoring-guide.md`](../../agent-workspace/external-docs/e2e-authoring-guide.md)
- **Status:** Already written. This is THE behavioral spec for this feature. All 12 sections (Fixture Pattern, Test Database, Self-Cleaning Tests, No Dead Time, Constant Visual Movement, Helper Conventions, Onboarding Wizard Testing, MessageBox Handling, Selector Strategy, Test Class Organization, Running the E2E Suite, Quick Reference Checklist) must be correctly implemented.

### FlaUI.UIA3 + xUnit
- **Packages:** `FlaUI.UIA3` 4.*, `FlaUI.Core` 4.*, `xunit` 2.*, `xunit.runner.visualstudio` 2.*, `Microsoft.NET.Test.Sdk` 17.*, `coverlet.collector` 6.*
- **Test framework:** xUnit with `[Collection("E2E")]` + `ICollectionFixture<E2eFixture>`
- **UIA provider:** UIA3 (UIAutomationClient) — drives WPF app through its UIA provider

### Key UIA Patterns
- **Element discovery:** `_fixture.MainWindow.FindFirst(TreeScope.Descendants, condition)` with polling loop (200ms intervals, 3s default timeout)
- **ConditionFactory:** `_cf.ByAutomationId("id")`, `_cf.ByName("name")`, `_cf.ByControlType(ControlType.Button)`
- **MessageBox handling:** `_fixture.Automation.GetDesktop().FindAllDescendants(_cf.ByControlType(ControlType.Window))` — MessageBox creates a separate top-level window
- **PasswordBox:** Use `SetValue()` (UIA Value pattern), not keyboard simulation
- **ComboBox items:** Only discoverable in UIA when dropdown is expanded

### AutomationId Catalog (from XAML)
- **Navigation:** `NavChats`, `NavWiki`, `NavMedia`, `NavArtifacts`, `NavUsage`, `NavSettings`
- **Chat Header:** `ThemeToggleBtn`, `ChatThemeCombo`, `DecreaseFontBtn`, `IncreaseFontBtn`, `FontSizeDisplay`, `PersonaSelector`
- **Shell:** `ChatView`, `SettingsView`, `SidebarSplitter`, `RightPanelSplitter`
- **API Key Form:** `AddApiKeyButton`, `ApiKeyFormTitle`, `ProviderTypeCombo`, `DisplayNameInput`, `ApiKeyInput` (PasswordBox), `CustomProviderNameInput`, `CustomEndpointUrlInput`, `TestKeyButton`, `SaveApiKeyButton`
- **Model Config Form:** `AddModelConfigButton`, `ModelIdentifierCombo` (editable ComboBox), `FetchModelsButton`, `SaveModelConfigButton`
- **Persona Form:** `AddPersonaButton`, `PersonaDisplayNameInput`, `PersonaSystemPromptInput`, `PersonaDefaultModelConfigCombo`, `PersonaChatModeCombo`, `SavePersonaButton`
- **Persona Picker Dialog:** `PersonaPickerDialog`, `PersonaPickerSearchBox`, `PersonaPickerList`, `PersonaPickerSelectBtn`, `PersonaPickerCancelBtn`
- **Onboarding Wizard:** `OnboardingWizardWindow`, `OnboardingWizardView`, `OnboardingWizardGrid`, `WizardGetStarted`, `WizardBack`, `WizardSkip`, `WizardNext`, `WizardAddApiKey`, `WizardTestAllKeys`, `WizardSavePersona`, `WizardCreatePersona`, `WizardResetHotkeys`, `WizardChooseWikiFolder`, `WizardCreateWikiFolder`, `WizardLaunchStudio`, `WizardImportChat`
- **Step Views:** `OnboardingStep0View` (API Keys), `OnboardingStep1View` (Persona), `OnboardingStep2View` (Wiki), `OnboardingStep3View` (Hotkeys), `OnboardingStep4View` (Finish)
- **Other Views:** `GlobalArtifactsBrowserView`, `MediaLibraryView`, `ModelComparisonView`, `UsageDashboardView`, `WikiBrowserView`

### Delete Button Pattern (🗑️)
- All 🗑️ buttons use `Content="🗑️"` with `Style="{StaticResource IconButtonStyle}"`
- ToolTips: `"Delete API key"`, `"Delete"` (plain for model configs and personas)
- Located via `FindFirstDescendant(_cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")))`
- Clicking triggers a WPF `MessageBox.Show()` with Yes/No — handle via `ConfirmMessageBox("Yes")`

### Settings Category Navigation
- 16 categories in a `ListBox` bound to `SettingsCategories` collection
- Each item is a `SettingsCategoryItem` record with `Label` property
- `SelectedSettingsCategory` drives sub-navigation via `DataTrigger` on a `ContentControl.Style`
- `SelectSettingsCategory(string categoryMatch)` helper: find `ListBoxItem` by partial name match, click it

### Onboarding Wizard State Machine
- Step index: `-1` (Welcome), `0` (API Keys), `1` (Persona), `2` (Wiki Directory), `3` (Hotkeys), `4` (Finish)
- Navigation: `WizardGetStarted` (-1→0), `WizardNext`/`WizardSkip` (0→1→2→3→4), `WizardBack` (reverse)
- Completion flags: `Onboarding_Step1_Completed` through `Onboarding_Step4_Completed`, final `Onboarding_Completed`
- Persisted via `ISettingsRepository` in test DB — survives test-to-test within the same run
- ShutdownMode: `OnExplicitShutdown` — app does NOT exit when wizard window closes

---

## Step-Specific Documentation

### Step 1: MSB_DB_PATH + E2eFixture + E2eTestBase Infrastructure

- **Library:** `System.Environment`, `FlaUI.Core`, `FlaUI.UIA3`
- **MSB_DB_PATH pattern (3 files):**

```csharp
// In all 3 files: AppDbContext.OnConfiguring(), AppDbContextFactory.CreateDbContext(),
// DependencyInjectionConfig.ConfigureServices()
var dbPath = Environment.GetEnvironmentVariable("MSB_DB_PATH")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MySecondBrain", "msb.db");
```

- **E2eFixture constructor pattern:**

```csharp
public sealed class E2eFixture : IDisposable
{
    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }

    private readonly string _testDbPath;

    public E2eFixture()
    {
        var testOutputDir = Path.Combine(Path.GetTempPath(), "MySecondBrain_E2E");
        Directory.CreateDirectory(testOutputDir);

        _testDbPath = Path.Combine(testOutputDir, "e2e-test.db");
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);

        Environment.SetEnvironmentVariable("MSB_DB_PATH", _testDbPath,
            EnvironmentVariableTarget.Process);

        var appPath = GetAppPath();
        Console.WriteLine($"[FIXTURE] Launching app: {appPath}");

        App = Application.Launch(appPath);
        Automation = new UIA3Automation();
        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));

        // Wait for NavChats (proves sidebar rendered)
        var readyCondition = Automation.ConditionFactory.ByAutomationId("NavChats");
        var sw = Stopwatch.StartNew();
        AutomationElement? ready = null;
        while (sw.Elapsed < TimeSpan.FromSeconds(8))
        {
            ready = MainWindow.FindFirst(TreeScope.Descendants, readyCondition);
            if (ready != null && ready.IsAvailable) break;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
        }
        if (ready == null || !ready.IsAvailable)
            throw new TimeoutException("App failed to initialize (NavChats not found).");

        // Wait for IncreaseFontBtn (proves ChatView UIA subtree populated)
        var chatReadyCondition = Automation.ConditionFactory.ByAutomationId("IncreaseFontBtn");
        var chatSw = Stopwatch.StartNew();
        AutomationElement? chatReady = null;
        while (chatSw.Elapsed < TimeSpan.FromSeconds(6))
        {
            chatReady = MainWindow.FindFirst(TreeScope.Descendants, chatReadyCondition);
            if (chatReady != null && chatReady.IsAvailable) break;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(150);
        }
        if (chatReady == null || !chatReady.IsAvailable)
            throw new TimeoutException("App failed to fully initialize: ChatView UIA subtree not populated.");

        Console.WriteLine($"[FIXTURE] App launched. PID={App.ProcessId}");
    }

    public void Dispose()
    {
        Console.WriteLine("[FIXTURE] Cleaning up...");
        var pid = 0;
        try { pid = App?.ProcessId ?? 0; } catch { }

        try { Automation?.Dispose(); }
        catch (Exception ex) { Console.WriteLine($"[FIXTURE] Automation dispose error: {ex.Message}"); }

        try
        {
            if (App != null && !App.HasExited)
            {
                App.Close();
                var sw = Stopwatch.StartNew();
                while (!App.HasExited && sw.Elapsed < TimeSpan.FromSeconds(5))
                    Thread.Sleep(200);
                if (!App.HasExited)
                {
                    Console.WriteLine("[FIXTURE] App did not close gracefully, killing.");
                    App.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FIXTURE] App cleanup error: {ex.Message}");
            if (pid > 0)
            {
                try { Process.GetProcessById(pid)?.Kill(); }
                catch { }
            }
        }
        finally
        {
            try { App?.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"[FIXTURE] App dispose error: {ex.Message}"); }
        }

        // Delete test database
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); }
            catch (Exception ex) { Console.WriteLine($"[FIXTURE] DB delete error: {ex.Message}"); }
        }
    }

    private static string GetAppPath()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln").Any())
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("Could not locate solution root.");
        var appPath = Path.Combine(dir.FullName, "src", "MySecondBrain.UI", "bin",
            "Debug", "net8.0-windows10.0.17763.0", "MySecondBrain.UI.exe");
        if (!File.Exists(appPath))
            throw new FileNotFoundException("MySecondBrain.UI.exe not found. Build first.", appPath);
        return appPath;
    }
}
```

- **E2eTestBase shared helpers:**

```csharp
// NOTE: ICollectionFixture<E2eFixture> is implemented by each concrete test class,
// NOT by this base class. Each test class must also have [Collection("E2E")].
public abstract class E2eTestBase
{
    protected readonly ITestOutputHelper _output;
    protected readonly E2eFixture _fixture;
    protected readonly ConditionFactory _cf;

    protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    protected const int RetryIntervalMs = 200;

    protected E2eTestBase(E2eFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _cf = _fixture.Automation.ConditionFactory;
    }

    protected async Task UseSharedAppAsync()
    {
        // Bring MainWindow to foreground
        _fixture.MainWindow.Focus();
        await Task.Delay(200);
    }

    protected AutomationElement? FindById(string automationId,
        AutomationElement? root = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var searchRoot = root ?? _fixture.MainWindow;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var element = searchRoot.FindFirst(TreeScope.Descendants,
                _cf.ByAutomationId(automationId));
            if (element != null && element.IsAvailable)
                return element;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }
        return null;
    }

    protected AutomationElement? FindByName(string name,
        AutomationElement? root = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var searchRoot = root ?? _fixture.MainWindow;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var element = searchRoot.FindFirst(TreeScope.Descendants,
                _cf.ByName(name));
            if (element != null && element.IsAvailable)
                return element;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }
        return null;
    }

    protected AutomationElement? FindByNameContains(string partialName,
        AutomationElement? root = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var searchRoot = root ?? _fixture.MainWindow;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var allElements = searchRoot.FindAllDescendants();
            foreach (var element in allElements)
            {
                if (element.Name?.Contains(partialName, StringComparison.OrdinalIgnoreCase) == true)
                    return element;
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }
        return null;
    }

    protected void NavigateToSettings()
    {
        var navSettings = FindById("NavSettings");
        Assert.NotNull(navSettings);
        navSettings!.Click();
        Thread.Sleep(400); // Screen transition
        var settingsView = FindById("SettingsView");
        Assert.NotNull(settingsView);
    }

    protected void SelectSettingsCategory(string categoryMatch)
    {
        var categoryItem = FindByNameContains(categoryMatch);
        Assert.NotNull(categoryItem);
        categoryItem!.Click();
        Thread.Sleep(300); // Category content transition
    }

    protected void ConfirmMessageBox(string expectedButton, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(3);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var windows = _fixture.Automation.GetDesktop().FindAllDescendants(
                _cf.ByControlType(ControlType.Window));
            foreach (var w in windows)
            {
                if (w.Name?.Contains("Confirm", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var btn = w.FindFirstDescendant(
                        _cf.ByControlType(ControlType.Button).And(_cf.ByName(expectedButton)));
                    btn?.Click();
                    return;
                }
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
        }
    }

    protected void SetPasswordInput(string automationId, string text)
    {
        var passwordBox = FindById(automationId);
        Assert.NotNull(passwordBox);
        // PasswordBox supports UIA Value pattern — use SetValue()
        passwordBox!.AsTextBox().Text = text;
    }
}
```

### Step 2: F5-F6 E2E Tests — App Shell, Platform Services & System Tray

- **Library:** FlaUI.Core, FlaUI.UIA3, xunit
- **Key patterns:**
  - All test classes use `[Collection("E2E")]` and inherit `E2eTestBase`
  - Navigation tests: click `RadioButton` via `FindById("Nav*")`, verify target view via `FindById("*View")`
  - Theme toggle: click `ThemeToggleBtn`, verify icon content changes, click again, verify round-trip
  - Font size: read `FontSizeDisplay.Text`, click `IncreaseFontBtn`, verify increment, click `DecreaseFontBtn`, verify decrement
  - Chat theme: expand `ChatThemeCombo`, select each option (Classic/Compact/Bubble) via `FindByNameContains`
  - DI resolution: verify services resolve via `_fixture.MainWindow` context or platform-specific checks
  - WebSocket health: use `HttpClient` to `http://127.0.0.1:{port}/health`, verify 200 OK
  - System tray context menu: enumerate UIA MenuItems, verify count=8, verify labels match expected order
  - Hotkey conflicts: verify `DetectConflict()` returns true for Win+D, Win+L, Alt+F4, Alt+Tab; false for Ctrl+Alt+Z
- **Snippet — Theme toggle round-trip:**
  ```csharp
  [Fact]
  public async Task ThemeToggle_ShouldSwitchDarkLight()
  {
      await UseSharedAppAsync();
      var toggleBtn = FindById("ThemeToggleBtn");
      Assert.NotNull(toggleBtn);
      var initialContent = toggleBtn!.Name; // e.g., "🌙" (Dark mode icon)
      toggleBtn.Click();
      await Task.Delay(500);
      var afterToggle = FindById("ThemeToggleBtn")!.Name;
      Assert.NotEqual(initialContent, afterToggle);
      // Round-trip
      FindById("ThemeToggleBtn")!.Click();
      await Task.Delay(500);
      var roundTrip = FindById("ThemeToggleBtn")!.Name;
      Assert.Equal(initialContent, roundTrip);
      _output.WriteLine("Theme toggle round-trip successful.");
  }
  ```
- **Snippet — Navigation test:**
  ```csharp
  [Fact]
  public async Task Navigation_ShouldSwitchToWikiScreen()
  {
      await UseSharedAppAsync();
      var navWiki = FindById("NavWiki");
      Assert.NotNull(navWiki);
      navWiki!.Click();
      await Task.Delay(400);
      var wikiView = FindById("WikiBrowserView");
      Assert.NotNull(wikiView);
      _output.WriteLine("Navigation to Wiki screen verified.");
  }
  ```

### Step 3: F7 E2E Tests — Model Configurations, API Keys & Personas (Self-Cleaning)

- **Library:** FlaUI.Core, FlaUI.UIA3, xunit
- **Key patterns:**
  - Self-cleaning: create → verify → delete via 🗑️ → confirm MessageBox → verify deleted
  - API key form: `AddApiKeyButton` → `ApiKeyFormTitle` "Add API Key" → `ProviderTypeCombo` select OpenAI → `DisplayNameInput` + `ApiKeyInput` (PasswordBox) → `TestKeyButton` → `SaveApiKeyButton`
  - Model config form: `AddModelConfigButton` → `DisplayNameInput` → `ModelIdentifierCombo` (editable) → temperature slider → `SaveModelConfigButton`
  - Persona form: `AddPersonaButton` → `PersonaDisplayNameInput` → `PersonaSystemPromptInput` → `PersonaDefaultModelConfigCombo` → `PersonaChatModeCombo` → `SavePersonaButton`
  - Delete: find list item by display name → find 🗑️ descendant button → click → `ConfirmMessageBox("Yes")` → verify item gone
- **Snippet — Self-cleaning API key test:**
  ```csharp
  [Fact]
  public async Task AddApiKey_ShouldSaveThenSelfDelete()
  {
      await UseSharedAppAsync();
      NavigateToSettings();
      SelectSettingsCategory("Providers");

      // Create
      var addBtn = FindById("AddApiKeyButton");
      addBtn!.Click();
      await Task.Delay(500);

      var formTitle = FindByName("Add API Key");
      Assert.NotNull(formTitle);

      // Select provider
      var providerCombo = FindById("ProviderTypeCombo")!.AsComboBox();
      providerCombo.Select("OpenAI");
      await Task.Delay(300);

      // Fill fields
      var displayInput = FindById("DisplayNameInput")!.AsTextBox();
      displayInput.Text = "E2E Test Key";
      SetPasswordInput("ApiKeyInput", "sk-test12345");
      await Task.Delay(300);

      var saveBtn = FindById("SaveApiKeyButton");
      saveBtn!.Click();
      await Task.Delay(800);

      // Verify saved
      var savedItem = FindByNameContains("E2E Test Key");
      Assert.NotNull(savedItem);

      // Self-clean: delete via 🗑️
      var deleteBtn = savedItem!.FindFirstDescendant(
          _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
      Assert.NotNull(deleteBtn);
      deleteBtn!.Click();
      await Task.Delay(500);
      ConfirmMessageBox("Yes");
      await Task.Delay(500);

      // Verify deleted
      var afterDelete = FindByNameContains("E2E Test Key");
      Assert.Null(afterDelete);

      _output.WriteLine("API key self-cleaning test passed.");
  }
  ```

### Step 4: F8 E2E Tests — Settings, Diagnostics, Appearance & Onboarding Wizard

- **Library:** FlaUI.Core, FlaUI.UIA3, xunit
- **Key patterns:**
  - Settings categories: enumerate `ListBoxItem` elements by `FindByNameContains`, verify 16 present
  - Diagnostics: find ComboBox for log level, select Information/Debug/Verbose; toggle CheckBoxes; verify buttons
  - Appearance: RadioButtons for Dark/Light; ChatThemeCombo; font controls
  - Onboarding wizard: fresh DB → wizard auto-launches on first app start → 5-step flow via `WizardGetStarted`/`WizardNext`/`WizardSkip`/`WizardLaunchStudio`
  - Onboarding ordering: static `_wizardCompleted` flag, guard in "should not appear" test with `Assert.Fail`
- **Snippet — Onboarding wizard flow:**
  ```csharp
  private static bool _wizardCompleted;

  [Fact]
  public async Task Onboarding_ShouldComplete5StepFlow()
  {
      await UseSharedAppAsync();

      // Welcome screen
      var welcomeTitle = FindByName("Welcome to MySecondBrain");
      Assert.NotNull(welcomeTitle);
      var getStartedBtn = FindById("WizardGetStarted");
      getStartedBtn!.Click();
      await Task.Delay(400);

      // Step 0: API Keys — skip
      var apiKeyTitle = FindByName("Add Your First API Key");
      Assert.NotNull(apiKeyTitle);
      var skipBtn = FindById("WizardSkip");
      skipBtn!.Click();
      await Task.Delay(400);

      // Step 1: Persona — skip
      var personaTitle = FindByName("Create Your First Persona");
      Assert.NotNull(personaTitle);
      FindById("WizardSkip")!.Click();
      await Task.Delay(400);

      // Step 2: Wiki Directory — skip
      var wikiTitle = FindByName("Choose Your Wiki Directory");
      Assert.NotNull(wikiTitle);
      FindById("WizardSkip")!.Click();
      await Task.Delay(400);

      // Step 3: Hotkeys — skip
      var hotkeyTitle = FindByName("Your Global Hotkeys");
      Assert.NotNull(hotkeyTitle);
      FindById("WizardSkip")!.Click();
      await Task.Delay(400);

      // Finish
      var launchBtn = FindById("WizardLaunchStudio");
      launchBtn!.Click();
      await Task.Delay(500);

      // Verify Studio Chat is visible
      var chatView = FindById("ChatView");
      Assert.NotNull(chatView);

      _wizardCompleted = true;
      _output.WriteLine("Onboarding wizard 5-step flow completed.");
  }

  [Fact]
  public async Task Onboarding_ShouldNotAppearAfterCompletion()
  {
      if (!_wizardCompleted)
          Assert.Fail("This test must run after Onboarding_ShouldComplete5StepFlow.");
      await UseSharedAppAsync();
      var wizardWindow = FindByName("Welcome to MySecondBrain",
          timeout: TimeSpan.FromSeconds(1));
      Assert.Null(wizardWindow);
      var chatView = FindById("ChatView");
      Assert.NotNull(chatView);
      _output.WriteLine("Onboarding correctly skipped after completion.");
  }
  ```

### Step 5: Final Suite Verification & E2E Authoring Guide Audit

- **Library:** `dotnet test`, `Microsoft.Data.Sqlite` (for DB verification), N/A for guide audit (manual review)
- **Full suite run command:**
  ```powershell
  dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug --verbosity normal
  ```
- **DB verification query (after suite):**
  ```sql
  SELECT COUNT(*) FROM ApiKeys WHERE DisplayName LIKE 'E2E%';
  SELECT COUNT(*) FROM ModelConfigurations WHERE DisplayName LIKE 'E2E%';
  SELECT COUNT(*) FROM Personas WHERE DisplayName LIKE 'E2E%';
  -- All should return 0
  ```
- **Fixture log verification:**
  - Search test output for `[FIXTURE] Launching app` — must appear exactly once
  - Search test output for `[FIXTURE] Cleaning up` — must appear exactly once
- **E2E authoring guide audit checklist:**
  - §1 Fixture Pattern: grep for `IClassFixture` in test files — must return 0 matches
  - §2 Test Database: verify `MSB_DB_PATH` is set in fixture and checked in 3 source files
  - §3 Self-Cleaning: verify all data-creating Facts have `ConfirmMessageBox` + delete pattern
  - §4 No Dead Time: grep for `Thread.Sleep(` with values > 500ms — justify each
  - §10 Test Organization: verify all 8 test class files exist
