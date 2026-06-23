# E2E Test Authoring Guide — MySecondBrain

This guide encodes the conventions, patterns, and principles for writing reliable, fast, and maintainable end-to-end tests for MySecondBrain. Every E2E test added to the project must follow these rules. When in doubt, refer to this document.

---

## 1. Fixture Pattern: One Launch for All Tests

**Rule:** Always use `ICollectionFixture<E2eFixture>`. Never use `IClassFixture<E2eFixture>`.

```csharp
// ✅ CORRECT — app launches once for the entire E2E test suite
[Collection("E2E")]
public class MyFeatureE2ETests : ICollectionFixture<E2eFixture>
{
    // ...
}

// ❌ WRONG — app launches once per test class
[Collection("E2E")]
public class MyFeatureE2ETests : IClassFixture<E2eFixture>
{
    // ...
}
```

**Why:** `IClassFixture` causes the app to close and restart between test classes (~14s launch + ~5s shutdown per class). With 7+ test classes, this adds minutes of dead time. `ICollectionFixture` launches the app once, runs all tests against the same instance, and closes it when the suite ends.

**Fixture lifecycle:**

| Event | What happens |
|-------|-------------|
| Suite start | `E2eFixture` constructor launches the app, waits for UIA tree population |
| Each `[Fact]` | Test runs against the shared `MainWindow` / `Automation` references |
| Suite end | `E2eFixture.Dispose()` calls `App.Close()` → graceful timeout → `App.Kill()` |

The `[Collection("E2E")]` attribute on every test class ensures they all share the same `E2eFixture` instance.

---

## 2. Test Database: Separate, Fresh, and Predictable

### How MSB_DB_PATH Works

Three files in the codebase determine the SQLite database path:

| File | Line(s) | Behavior |
|------|---------|----------|
| [`AppDbContext.cs`](../../src/MySecondBrain.Data/AppDbContext.cs) | ~29-32 | Checks `Environment.GetEnvironmentVariable("MSB_DB_PATH")` — if set, uses that path; otherwise defaults to `%LOCALAPPDATA%\MySecondBrain\msb.db` |
| [`AppDbContextFactory.cs`](../../src/MySecondBrain.Data/AppDbContextFactory.cs) | ~17-18 | Same check for EF Core migration tooling |
| [`DependencyInjectionConfig.cs`](../../src/MySecondBrain.UI/DependencyInjectionConfig.cs) | ~42-43 | Same check for DI registration |

### E2E Fixture Usage

The `E2eFixture` constructor must:

1. Create a test output directory: `{testOutputDir}\e2e-test.db`
2. Set the `MSB_DB_PATH` environment variable to that path
3. If the file exists from a previous run, delete it (fresh DB every run)
4. Launch the app — the app reads `MSB_DB_PATH` and uses the test DB
5. On `Dispose()`, delete the test database file

```csharp
// E2eFixture constructor pseudocode
var testDbPath = Path.Combine(testOutputDir, "e2e-test.db");
if (File.Exists(testDbPath))
    File.Delete(testDbPath);
Environment.SetEnvironmentVariable("MSB_DB_PATH", testDbPath, EnvironmentVariableTarget.Process);
// Then launch the app...
```

### Why This Matters

- **Fresh DB every run** → onboarding wizard fires on first launch → wizard gets tested naturally
- **Setting persisted** → `Onboarding_Completed` = "true" is stored in test DB → subsequent tests skip wizard
- **No pollution** → test data never touches the user's real `%LOCALAPPDATA%\MySecondBrain\msb.db`
- **Deterministic** → every test run starts from identical state

---

## 3. Self-Cleaning Tests: Delete What You Create

**Rule:** Every `[Fact]` that creates data must delete it within the same test body using the app's own 🗑️ delete buttons.

**Pattern: Create → Verify → Delete via 🗑️ → Verify Deleted**

```csharp
[Fact]
public async Task AC1_CreatePersona_ShouldSaveAndDisplay()
{
    // 1. ARRANGE — Navigate to the right screen
    await UseSharedAppAsync();
    NavigateToSettings();
    SelectSettingsCategory("Profiles");

    // 2. ACT — Create the entity
    var addBtn = FindById("AddPersonaButton");
    addBtn.Click();
    await Task.Delay(500);

    var nameInput = FindById("PersonaDisplayNameInput")!.AsTextBox();
    nameInput.Text = "E2E Test Persona";
    // ... fill other fields ...

    var saveBtn = FindById("SavePersonaButton");
    saveBtn.Click();
    await Task.Delay(1000);

    // 3. ASSERT — Entity appears in the list
    var savedItem = FindByNameContains("E2E Test Persona");
    Assert.NotNull(savedItem);

    // 4. CLEANUP — Delete via 🗑️ button
    var deleteBtn = savedItem!.FindFirstDescendant(
        _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
    Assert.NotNull(deleteBtn);
    deleteBtn!.Click();
    await Task.Delay(500);

    // Handle confirmation MessageBox
    ConfirmMessageBox("Yes");

    // 5. ASSERT — Entity is gone
    await Task.Delay(500);
    var afterDelete = FindByNameContains("E2E Test Persona");
    Assert.Null(afterDelete);

    _output.WriteLine("AC-1 PASSED: Created and deleted persona.");
}
```

**Anti-patterns to avoid:**

- ❌ Class-level `Dispose()` that runs cleanup
- ❌ Static `_remainingCleanups` counters
- ❌ `PerformCleanup()` that navigates Settings, searches for items, deletes them
- ❌ Tests that create data and leave it for another test to clean up
- ❌ Tests that assume data from a previous test exists

**Why:** If the app crashes during cleanup, cascading failures follow. If a test fails before reaching cleanup, stale data poisons subsequent tests. Self-cleaning tests isolate failures — one test's crash doesn't affect the next.

### MessageBox Confirmation Pattern

When deleting via 🗑️, the app shows a WPF `MessageBox` with Yes/No. Use this pattern:

```csharp
private void ConfirmMessageBox(string expectedButton, TimeSpan? timeout = null)
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
                    _cf.ByControlType(ControlType.Button)
                       .And(_cf.ByName(expectedButton)));
                btn?.Click();
                return;
            }
        }
        Wait.UntilInputIsProcessed();
        Thread.Sleep(200);
    }
}
```

---

## 4. No Dead Time: Keep the UI Moving

**Rules:**

- Maximum 3-second timeouts for UI element discovery (default in `E2eTestBase`)
- No `Thread.Sleep()` over 500ms without a comment justifying why
- Prefer `Wait.UntilInputIsProcessed()` + short polls (200ms) over long sleeps
- No polling loops that exceed 3 seconds

```csharp
// ✅ GOOD — short poll with timeout
private AutomationElement? FindById(string automationId, TimeSpan? timeout = null)
{
    var limit = timeout ?? TimeSpan.FromSeconds(3);
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < limit)
    {
        var element = _fixture.MainWindow.FindFirst(
            TreeScope.Descendants, _cf.ByAutomationId(automationId));
        if (element != null && element.IsAvailable)
            return element;
        Wait.UntilInputIsProcessed();
        Thread.Sleep(200); // Short poll — UI needs time to render
    }
    return null;
}

// ❌ BAD — blocking sleep with no progress
Thread.Sleep(3000); // What are we waiting for? Is 3s enough? Too much?
```

**Why:** Dead time is wasted test suite runtime. Constant visual activity — clicking, switching screens, typing text — keeps the suite fast and makes failures easier to diagnose (you can see what the test was doing when it failed).

---

## 5. Constant Visual Movement

Tests should read like a user session — click, observe, click, type, observe. No long stretches of nothing.

```csharp
// ✅ GOOD — active interaction sequence
navSettings.Click();           // Click Settings
await Task.Delay(400);        // Wait for screen transition
SelectSettingsCategory("Diagnostics"); // Click category
await Task.Delay(300);        // Wait for category content
comboCtrl.Select("Debug");    // Change log level
await Task.Delay(300);        // Wait for UI update
var selection = comboCtrl.SelectedItem?.Name; // Observe result
Assert.Equal("Debug", selection);

// ❌ BAD — long wait, single observation
NavigateToSettings();
Thread.Sleep(3000);           // Just waiting...
var settingsView = FindById("SettingsView"); // One check
Assert.NotNull(settingsView);
```

---

## 6. Helper Conventions

### Base Class: `E2eTestBase`

All shared helpers go in an abstract `E2eTestBase` class. Every test class inherits from it.

```csharp
public abstract class E2eTestBase : ICollectionFixture<E2eFixture>
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

    // ── Shared helpers ──────────────────────────────────────

    protected Task<Window> UseSharedAppAsync() { /* ... */ }
    protected AutomationElement? FindById(string automationId, AutomationElement? root = null, TimeSpan? timeout = null) { /* ... */ }
    protected AutomationElement? FindByName(string name, AutomationElement? root = null, TimeSpan? timeout = null) { /* ... */ }
    protected AutomationElement? FindByNameContains(string partialName, AutomationElement? root = null, TimeSpan? timeout = null) { /* ... */ }
    protected void NavigateToSettings() { /* ... */ }
    protected void SelectSettingsCategory(string categoryMatch) { /* ... */ }
    protected void ConfirmMessageBox(string expectedButton, TimeSpan? timeout = null) { /* ... */ }
    protected void SetPasswordInput(string automationId, string text) { /* ... */ }
}
```

### Per-Class Helpers

Helpers specific to one test class are private methods in that class. Examples:
- `EnsureTestApiKeyExistsAsync()` — creates prerequisite API key if not found
- `FindLogLevelComboBox()` — locates the log level ComboBox in Diagnostics
- `DeleteListItemByContent()` — finds and deletes a list item by partial name

### Rules

- No duplicated `FindById`/`FindByName`/`NavigateToSettings` across classes — these live in `E2eTestBase`
- No duplicated `FindByNameContains` — lives in `E2eTestBase`
- No duplicated `ConfirmMessageBox` — lives in `E2eTestBase`
- Per-class helpers stay private

---

## 7. Onboarding Wizard Testing

### How It Works

- Fresh test DB → no `Onboarding_Completed` setting → onboarding wizard fires on first launch
- After user completes wizard, `Onboarding_Completed` = "true" is saved to test DB
- Subsequent tests skip the wizard automatically

### Testing the 5-Step Flow

```csharp
[Fact]
public async Task OnboardingWizard_Complete5StepFlow_ShouldArriveAtStudio()
{
    // Fresh test DB triggers wizard automatically on first test run
    await UseSharedAppAsync();

    // Step 1: Welcome
    var welcomeTitle = FindByName("Welcome to MySecondBrain");
    Assert.NotNull(welcomeTitle);
    var getStartedBtn = FindById("WizardGetStartedBtn");
    getStartedBtn!.Click();
    await Task.Delay(400);

    // Step 2: API Keys
    var apiKeyTitle = FindByName("Add Your First API Key");
    Assert.NotNull(apiKeyTitle);
    // Either add a key or skip
    var skipBtn = FindById("WizardSkipBtn");
    skipBtn!.Click();
    await Task.Delay(400);

    // Step 3: Persona
    var personaTitle = FindByName("Create Your First Persona");
    Assert.NotNull(personaTitle);
    // Either create or skip
    skipBtn = FindById("WizardSkipBtn");
    skipBtn!.Click();
    await Task.Delay(400);

    // Step 4: Wiki Directory
    var wikiTitle = FindByName("Choose Your Wiki Directory");
    Assert.NotNull(wikiTitle);
    skipBtn = FindById("WizardSkipBtn");
    skipBtn!.Click();
    await Task.Delay(400);

    // Step 5: Hotkeys
    var hotkeyTitle = FindByName("Your Global Hotkeys");
    Assert.NotNull(hotkeyTitle);
    var finishBtn = FindById("WizardFinishBtn");
    finishBtn!.Click();
    await Task.Delay(500);

    // Assert: Wizard closed, Studio Chat is visible
    var chatView = FindById("ChatView");
    Assert.NotNull(chatView);

    _output.WriteLine("Onboarding wizard 5-step flow completed successfully.");
}

[Fact]
public async Task OnboardingWizard_ShouldNotAppearAfterCompletion()
{
    // This test runs AFTER the wizard completion test
    // The Onboarding_Completed=true setting persists in test DB
    await UseSharedAppAsync();

    // The wizard window should NOT be present
    var wizardWindow = FindByName("Welcome to MySecondBrain", timeout: TimeSpan.FromSeconds(1));
    Assert.Null(wizardWindow);

    // Studio Chat should be visible immediately
    var chatView = FindById("ChatView");
    Assert.NotNull(chatView);

    _output.WriteLine("Onboarding wizard correctly skipped on second launch.");
}
```

### Testing Re-run Wizard from Settings

```csharp
[Fact]
public async Task Settings_ReRunOnboardingWizard_ShouldLaunchWizard()
{
    await UseSharedAppAsync();
    NavigateToSettings();

    // Click the "🔄 Re-run Onboarding Wizard" hyperlink
    var reRunLink = FindByNameContains("Re-run Onboarding Wizard");
    Assert.NotNull(reRunLink);
    reRunLink!.Click();
    await Task.Delay(500);

    // Wizard launched — verify Welcome screen
    var welcomeTitle = FindByName("Welcome to MySecondBrain");
    Assert.NotNull(welcomeTitle);

    // Close the wizard via cancel
    var cancelBtn = FindById("WizardCancelBtn") ?? FindByName("Cancel");
    cancelBtn?.Click();
    await Task.Delay(300);

    // Verify we're back at Settings
    var settingsView = FindById("SettingsView");
    Assert.NotNull(settingsView);

    _output.WriteLine("Re-run onboarding wizard launched and dismissed.");
}
```

---

## 8. MessageBox Handling

WPF `MessageBox.Show()` is a blocking call that creates a separate top-level window. It does NOT appear as a child of the main window in the UIA tree — it's a sibling window on the desktop.

### Finding a MessageBox

```csharp
private AutomationElement? FindMsgBox(string titlePart, TimeSpan? timeout = null)
{
    var limit = timeout ?? TimeSpan.FromSeconds(3);
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < limit)
    {
        // Search ALL top-level windows on the desktop
        var windows = _fixture.Automation.GetDesktop().FindAllDescendants(
            _cf.ByControlType(ControlType.Window));
        foreach (var w in windows)
        {
            if (w.Name?.Contains(titlePart, StringComparison.OrdinalIgnoreCase) == true)
                return w;
        }
        Wait.UntilInputIsProcessed();
        Thread.Sleep(200);
    }
    return null;
}
```

### Interacting with MessageBox Buttons

WPF MessageBox uses standard Button controls with predictable names:

| MessageBox.Show() parameter | Button names |
|------------------------------|--------------|
| `MessageBoxButton.YesNo` | "Yes", "No" |
| `MessageBoxButton.OK` | "OK" |
| `MessageBoxButton.OKCancel` | "OK", "Cancel" |
| `MessageBoxButton.YesNoCancel` | "Yes", "No", "Cancel" |

```csharp
// Click "Yes" on a confirmation dialog
var msgBox = FindMsgBox("Confirm");
if (msgBox != null)
{
    var yesBtn = msgBox.FindFirstDescendant(
        _cf.ByControlType(ControlType.Button).And(_cf.ByName("Yes")));
    yesBtn?.Click();
}
```

**WARNING:** MessageBox blocks the UI thread. If a test sends a UIA action that triggers a MessageBox, the test must handle the MessageBox before the original action's `await Task.Delay()` times out. Always add `Thread.Sleep(400)` after clicking an action that triggers a MessageBox, then find and dismiss it.

---

## 9. Selector Strategy

**Priority order for finding UIA elements:**

| Priority | Selector | When to Use | Example |
|----------|----------|-------------|---------|
| 1 (best) | `AutomationId` (`x:Name` in XAML) | Always preferred. Fastest, most reliable. | `FindById("NavChats")` |
| 2 | `Name` (exact match) | When the element doesn't have an AutomationId but has a known, stable text | `FindByName("Settings")` |
| 3 | `Name` (partial match) | When the Name is dynamic or concatenated | `FindByNameContains("E2E Test Persona")` |
| 4 | `ControlType` scanning + filtering | Last resort. Slow, fragile. | Iterate all `ComboBox` elements and find by items |

### XAML Naming Conventions

To ensure E2E tests can reliably locate elements, follow these conventions when writing XAML:

| Element Type | AutomationId Convention | Example |
|-------------|------------------------|---------|
| Navigation buttons | `Nav{ScreenName}` | `NavChats`, `NavWiki`, `NavSettings` |
| Action buttons | `{Action}{Entity}Button` | `AddApiKeyButton`, `SavePersonaButton`, `DeletePersonaButton` |
| Input fields | `{Entity}{Field}Input` | `DisplayNameInput`, `ApiKeyInput`, `PersonaDisplayNameInput` |
| ComboBoxes | `{Entity}{Property}Combo` | `ProviderTypeCombo`, `ChatThemeCombo`, `ModelIdentifierCombo` |
| Views/UserControls | `{ScreenName}View` | `ChatView`, `SettingsView`, `WikiBrowserView` |
| Panels/Splitters | `{Region}Splitter` | `SidebarSplitter`, `RightPanelSplitter` |
| Dialogs | `{Feature}Dialog` | `PersonaPickerDialog` |
| Form titles | `{Entity}FormTitle` | `ApiKeyFormTitle` |
| Indicators | `{Property}Display` | `FontSizeDisplay` |

---

## 10. Test Class Organization

### Recommended File Structure

After rewrite, the test suite should have these test classes:

| Test Class | Covers | Approx. Tests | Creates Data? |
|------------|--------|---------------|---------------|
| `AppShellNavigationThemingE2ETests` | F5: Shell layout, navigation, theme toggle, font size, chat themes, ContentRendererRegistry | ~12 | No |
| `PlatformServicesE2ETests` | F6: DI resolution, DPI, WebSocket server, auto-update, all 4 platform services | ~10 | No |
| `SystemTrayHotkeyE2ETests` | F6: System tray context menu, events, recent chats, generation indicator, hotkey registration/conflicts | ~10 | No |
| `ModelConfigsApiKeysE2ETests` | F7: API key CRUD, test key, model config CRUD | ~6 | Yes (self-cleaning) |
| `PersonasE2ETests` | F7: Persona CRUD, persona picker dialog | ~4 | Yes (self-cleaning) |
| `SettingsDiagnosticsE2ETests` | F8: Settings categories, log level, log categories, log file management | ~7 | No |
| `AppearanceOnboardingE2ETests` | F8: Appearance radio buttons, theme toggle, re-run wizard hyperlink | ~5 | No |
| `OnboardingWizardE2ETests` | F8: 5-step wizard flow, skip each step, finish, wizard not shown after completion | ~6 | Yes (self-cleaning via completion flag) |

**Total: ~60 tests, 8 classes, 1 shared fixture.**

### Test Execution Order

xUnit does not guarantee test execution order within a class. However, the onboarding wizard tests rely on ordering: the "complete wizard" test must run before "wizard should not appear." Use xUnit's `[CollectionDefinition]` with `DisableParallelization = true` and rely on the fact that `[Collection("E2E")]` with `ICollectionFixture` runs tests sequentially. For the onboarding order dependency, use a shared static flag:

```csharp
private static bool _wizardCompleted;

[Fact]
public async Task Onboarding_ShouldComplete5StepFlow()
{
    // ... complete wizard ...
    _wizardCompleted = true;
}

[Fact]
public async Task Onboarding_ShouldNotAppearAfterCompletion()
{
    if (!_wizardCompleted)
        Assert.Fail("This test must run after Onboarding_ShouldComplete5StepFlow. Re-order tests.");
    // ... verify wizard doesn't appear ...
}
```

---

## 11. Running the E2E Suite

### Prerequisites

1. Build the solution in Debug|Any CPU: `dotnet build MySecondBrain.sln --configuration Debug`
2. No other instance of `MySecondBrain.UI.exe` should be running
3. .NET 8.0 SDK installed

### Run Command

```powershell
dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug --verbosity normal
```

Or use the convenience script:

```powershell
.\tests\e2e\run-e2e-tests.ps1
```

### Expected Results

- All tests pass (0 failures, 0 skipped)
- `dotnet test` exits with code 0
- No stale test data remains in `e2e-test.db` (verified by checking entity counts after suite completion)

---

## 12. Quick Reference Checklist

When writing a new E2E test, verify:

- [ ] Test class uses `ICollectionFixture<E2eFixture>` (not `IClassFixture`)
- [ ] Test class inherits from `E2eTestBase`
- [ ] `[Collection("E2E")]` attribute present
- [ ] All timeouts ≤ 3 seconds
- [ ] No `Thread.Sleep()` > 500ms without comment
- [ ] Test creates data → test deletes data within the same `[Fact]`
- [ ] No static `_remainingCleanups` counters
- [ ] No class-level `Dispose()` with multi-step cleanup
- [ ] Uses `FindById` (AutomationId) over `FindByName` over `FindByNameContains`
- [ ] Shared helpers live in `E2eTestBase`, per-class helpers are private
- [ ] Onboarding wizard ordering handled with shared static flag
- [ ] MessageBox handling: find → interact → wait for dismiss
- [ ] `MSB_DB_PATH` env var set by fixture before app launch
- [ ] Test DB deleted on fixture teardown

---

*E2E Authoring Guide — encodes the patterns validated across 70+ E2E tests. Last updated: 2026-06-23 for Feature 9 E2E Test Suite Rewrite.*
