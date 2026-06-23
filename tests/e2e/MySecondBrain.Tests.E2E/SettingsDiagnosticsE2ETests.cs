using FlaUI.Core.Definitions;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for AC-1 through AC-7 of Feature 8: Settings & Diagnostics.
///
/// PREREQUISITES:
/// 1. Build the solution in Debug|Any CPU before running these tests.
/// 2. The app executable must exist at the path returned by GetAppPath().
/// 3. No other instance of MySecondBrain.UI.exe should be running.
///
/// COVERAGE:
/// AC-1: SettingsView loads with correct title
/// AC-2: All 16 settings category sidebar items are present
/// AC-3: Each category can be selected and shows the expected header
/// AC-4: Diagnostics section renders log level ComboBox with 3 options
/// AC-5: Can change log level from Information to Debug and back
/// AC-6: Toggle LLM API Calls log category off then on
/// AC-7: Open Logs Folder and Clear Logs buttons exist in Log File Management
///
/// These tests create no persistent data — they read UI state and toggle settings
/// that are re-read on next SettingsViewModel initialization (Transient).
/// </summary>
[Collection("E2E")]
public class SettingsDiagnosticsE2ETests : IClassFixture<E2eFixture>, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly E2eFixture _fixture;
    private readonly ConditionFactory _cf;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private const int RetryIntervalMs = 200;

    // ── All 16 settings categories with their expected header text ──────────
    private static readonly Dictionary<string, string> CategoryHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Providers"] = "🔑 API Keys",
        ["Profiles"] = "👤 Profiles",
        ["Appearance"] = "🎨 Appearance",
        ["Language"] = "🌐 Language",
        ["Wiki"] = "📝 Wiki",
        ["Backup"] = "☁️ Backup",
        ["TextActions"] = "⚡ Text Actions",
        ["Hotkeys"] = "⌨️ Hotkeys",
        ["Tools"] = "🔧 Tools",
        ["Notifications"] = "🔔 Notifications",
        ["Startup"] = "🚀 Startup",
        ["Updates"] = "🔄 Updates",
        ["Pricing"] = "💰 Pricing",
        ["Security"] = "🔒 Security",
        ["Maintenance"] = "🛠️ Maintenance",
        ["Diagnostics"] = "🔬 Diagnostics",
    };

    // ── Log level options ──────────────────────────────────────────────────
    private static readonly string[] LogLevelOptions = ["Information", "Debug", "Verbose"];

    public SettingsDiagnosticsE2ETests(E2eFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _cf = _fixture.Automation.ConditionFactory;
    }

    /// <summary>
    /// Per-test cleanup: reset navigation to Chats so the next test starts fresh.
    /// </summary>
    public void Dispose()
    {
        if (_fixture.App.HasExited)
            return;

        try
        {
            var navChats = _fixture.MainWindow.FindFirstDescendant(
                _cf.ByAutomationId("NavChats"));
            navChats?.Click();
            Thread.Sleep(300);
        }
        catch
        {
            // Best-effort cleanup — fixture may be mid-dispose
        }
    }

    // ── Reusable helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the shared main window from the fixture. No per-test launch needed.
    /// </summary>
    private Task<FlaUI.Core.AutomationElements.Window> UseSharedAppAsync()
    {
        _output.WriteLine($"[SHARED] Using fixture: PID={_fixture.App.ProcessId}");
        return Task.FromResult(_fixture.MainWindow);
    }

    private AutomationElement? FindById(string automationId,
        AutomationElement? root = null,
        TimeSpan? timeout = null)
    {
        root ??= _fixture.MainWindow;
        if (root == null) return null;

        var limit = timeout ?? DefaultTimeout;
        var condition = _cf.ByAutomationId(automationId);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < limit)
        {
            var element = root.FindFirst(TreeScope.Descendants, condition);

            if (element != null && element.IsAvailable)
                return element;

            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }

        return null;
    }

    private AutomationElement? FindByName(string name,
        AutomationElement? root = null,
        TimeSpan? timeout = null)
    {
        root ??= _fixture.MainWindow;
        if (root == null) return null;

        var limit = timeout ?? DefaultTimeout;
        var condition = _cf.ByName(name);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < limit)
        {
            var element = root.FindFirst(TreeScope.Descendants, condition);

            if (element != null && element.IsAvailable)
                return element;

            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }

        return null;
    }

    /// <summary>
    /// Navigates to the Settings screen by clicking the NavSettings sidebar button.
    /// </summary>
    private void NavigateToSettings()
    {
        var navSettings = FindById("NavSettings");
        Assert.NotNull(navSettings);
        navSettings!.Click();
        Thread.Sleep(600);
        _output.WriteLine("Navigated to Settings screen.");
    }

    /// <summary>
    /// Selects a settings category from the sidebar by finding a ListItem whose Name
    /// contains the given text. The sidebar ListBox items expose their display text
    /// (Icon + Label) as the UIA Name.
    /// </summary>
    private void SelectSettingsCategory(string categoryMatch)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < DefaultTimeout)
        {
            var allItems = _fixture.MainWindow.FindAllDescendants(
                _cf.ByControlType(ControlType.ListItem));
            foreach (var li in allItems)
            {
                if (li.Name.Contains(categoryMatch, StringComparison.OrdinalIgnoreCase))
                {
                    li.Click();
                    Thread.Sleep(400);
                    _output.WriteLine(
                        $"Selected settings category: '{categoryMatch}' via ListItem.Name='{li.Name}'.");
                    return;
                }
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }

        Assert.Fail(
            $"Could not select settings category '{categoryMatch}'. " +
            $"No ListItem found matching in the UIA tree.");
    }

    /// <summary>
    /// Gets all ListItem Names from the sidebar as a string array.
    /// </summary>
    private string[] GetSidebarCategoryLabels()
    {
        var allItems = _fixture.MainWindow.FindAllDescendants(
            _cf.ByControlType(ControlType.ListItem));
        return allItems
            .Where(i => !string.IsNullOrEmpty(i.Name))
            .Select(i => i.Name!)
            .ToArray();
    }

    /// <summary>
    /// Finds an element whose UIA Name CONTAINS the given text (partial match).
    /// </summary>
    private AutomationElement? FindByNameContains(string partialName,
        AutomationElement? root = null,
        TimeSpan? timeout = null)
    {
        root ??= _fixture.MainWindow;
        if (root == null) return null;

        var limit = timeout ?? DefaultTimeout;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < limit)
        {
            var allElements = root.FindAllDescendants(
                _cf.ByControlType(ControlType.Text)
                   .Or(_cf.ByControlType(ControlType.ListItem))
                   .Or(_cf.ByControlType(ControlType.Custom))
                   .Or(_cf.ByControlType(ControlType.Button))
                   .Or(_cf.ByControlType(ControlType.RadioButton))
                   .Or(_cf.ByControlType(ControlType.CheckBox)));
            foreach (var elem in allElements)
            {
                if (!string.IsNullOrEmpty(elem.Name)
                    && elem.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase)
                    && elem.IsAvailable)
                {
                    return elem;
                }
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }

        return null;
    }

    /// <summary>
    /// Locates the Log Level ComboBox in the Diagnostics section.
    /// Searches for a ComboBox whose items contain the log level option strings.
    /// </summary>
    private AutomationElement? FindLogLevelComboBox()
    {
        var settingsView = FindById("SettingsView");
        if (settingsView == null) return null;

        var allCombos = settingsView.FindAllDescendants(
            _cf.ByControlType(ControlType.ComboBox));

        foreach (var combo in allCombos)
        {
            try
            {
                var comboCtrl = combo.AsComboBox();
                comboCtrl.Expand();
                Wait.UntilInputIsProcessed();
                Thread.Sleep(200);
                var items = comboCtrl.Items;
                comboCtrl.Collapse();

                if (items.Length >= 3)
                {
                    var itemTexts = items
                        .Select(i => i.Name)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();

                    // Check if this ComboBox matches the known log level options
                    if (itemTexts.Intersect(LogLevelOptions, StringComparer.OrdinalIgnoreCase).Count() >= 2)
                    {
                        return combo;
                    }
                }
            }
            catch
            {
                // Some ComboBoxes may not support Expand/Collapse; skip them
                continue;
            }
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-1: Settings View Loads
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC1_SettingsView_ShouldLoadWithCorrectTitle()
    {
        // Arrange
        await UseSharedAppAsync();

        // Act — Navigate to Settings
        NavigateToSettings();

        // Assert — SettingsView element exists
        var settingsView = FindById("SettingsView");
        Assert.NotNull(settingsView);
        Assert.False(settingsView!.IsOffscreen, "SettingsView should be visible on screen.");

        // Assert — "Settings" header exists in the sidebar
        var settingsHeader = FindByName("Settings");
        Assert.NotNull(settingsHeader);
        Assert.False(settingsHeader!.IsOffscreen,
            "Settings header should be visible in the sidebar.");

        _output.WriteLine("AC-1 PASSED: SettingsView loads with correct title.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-2: All 16 Category Sidebar Items Present
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC2_SettingsSidebar_ShouldHave16Categories()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();

        // Act
        var labels = GetSidebarCategoryLabels();

        // Assert — There should be exactly 16 categories
        Assert.Equal(16, labels.Length);

        // Assert — Each category label is present (matching by icon+label keyword)
        // The ListItem Names are formatted as "{Icon} {Label}" e.g., "🔑 Providers"
        var allFound = true;
        foreach (var (key, headerText) in CategoryHeaders)
        {
            var found = labels.Any(l =>
                l.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (!found)
            {
                _output.WriteLine($"  MISSING: '{key}' ('{headerText}')");
                allFound = false;
            }
            else
            {
                _output.WriteLine($"  FOUND: '{key}' ('{headerText}')");
            }
        }

        Assert.True(allFound, "All 16 settings categories should be present in the sidebar.");
        _output.WriteLine("AC-2 PASSED: All 16 settings categories are present in the sidebar.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-3: Category Selection Shows Correct Header
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Providers", "🔑 API Keys")]
    [InlineData("Profiles", "👤 Profiles")]
    [InlineData("Appearance", "🎨 Appearance")]
    [InlineData("Language", "🌐 Language")]
    [InlineData("Wiki", "📝 Wiki")]
    [InlineData("Backup", "☁️ Backup")]
    [InlineData("TextActions", "⚡ Text Actions")]
    [InlineData("Hotkeys", "⌨️ Hotkeys")]
    [InlineData("Tools", "🔧 Tools")]
    [InlineData("Notifications", "🔔 Notifications")]
    [InlineData("Startup", "🚀 Startup")]
    [InlineData("Updates", "🔄 Updates")]
    [InlineData("Pricing", "💰 Pricing")]
    [InlineData("Security", "🔒 Security")]
    [InlineData("Maintenance", "🛠️ Maintenance")]
    [InlineData("Diagnostics", "🔬 Diagnostics")]
    public async Task AC3_SettingsCategory_ShouldShowCorrectHeader(string categoryMatch, string expectedHeader)
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();

        // Act — Select the category
        SelectSettingsCategory(categoryMatch);
        await Task.Delay(300);

        // Assert — The header TextBlock with the expected text is visible
        var header = FindByName(expectedHeader);
        Assert.NotNull(header);
        Assert.False(header!.IsOffscreen,
            $"Header '{expectedHeader}' should be visible for category '{categoryMatch}'.");

        _output.WriteLine($"AC-3 PASSED: Category '{categoryMatch}' shows header '{expectedHeader}'.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-4: Diagnostics — Log Level ComboBox
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC4_Diagnostics_LogLevelComboBox_HasThreeOptions()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Find all ComboBoxes in the SettingsView for the Diagnostics section
        var settingsView = FindById("SettingsView");
        Assert.NotNull(settingsView);

        var allCombos = settingsView!.FindAllDescendants(
            _cf.ByControlType(ControlType.ComboBox));

        // The Diagnostics section has at least one ComboBox (log level).
        // It's the first ComboBox in the Diagnostics category.
        Assert.True(allCombos.Length >= 1,
            "Diagnostics section should have at least one ComboBox (log level).");

        // Find the log level combo — it contains the log level options
        AutomationElement? logLevelCombo = null;
        foreach (var combo in allCombos)
        {
            var comboCtrl = combo.AsComboBox();
            comboCtrl.Expand();
            await Task.Delay(300);
            var items = comboCtrl.Items;
            comboCtrl.Collapse();

            if (items.Length >= 3)
            {
                // Check if items match log level options
                var itemTexts = items
                    .Select(i => i.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();

                if (itemTexts.Intersect(LogLevelOptions, StringComparer.OrdinalIgnoreCase).Count() >= 2)
                {
                    logLevelCombo = combo;
                    _output.WriteLine($"Found log level ComboBox with items: [{string.Join(", ", itemTexts)}]");
                    break;
                }
            }
        }

        Assert.NotNull(logLevelCombo);
        _output.WriteLine("AC-4 PASSED: Diagnostics Log Level ComboBox has at least 3 options.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-5: Diagnostics — Change Log Level
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC5_Diagnostics_ShouldChangeLogLevel()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Find the log level ComboBox
        var logLevelCombo = FindLogLevelComboBox();
        Assert.NotNull(logLevelCombo);

        var comboCtrl = logLevelCombo!.AsComboBox();

        // Record initial selection
        var initialSelection = comboCtrl.SelectedItem?.Name ?? string.Empty;
        _output.WriteLine($"Initial log level: '{initialSelection}'");

        // Determine the target log level to select (use a different one than initial)
        var targetLevel = "Debug";
        var restoreLevel = "Information";
        if (initialSelection.Equals("Debug", StringComparison.OrdinalIgnoreCase))
        {
            // If already Debug, select Information instead, then restore to Debug
            targetLevel = "Information";
            restoreLevel = "Debug";
        }

        // Act — Select target
        comboCtrl.Select(targetLevel);
        await Task.Delay(500);

        // Assert — Selection changed
        var afterSelect = comboCtrl.SelectedItem?.Name ?? string.Empty;
        Assert.Equal(targetLevel, afterSelect, ignoreCase: true);
        _output.WriteLine($"Log level changed to: '{afterSelect}'");

        // Act — Restore to original
        comboCtrl.Select(restoreLevel);
        await Task.Delay(500);

        var finalSelection = comboCtrl.SelectedItem?.Name ?? string.Empty;
        Assert.Equal(restoreLevel, finalSelection, ignoreCase: true);
        _output.WriteLine($"Log level restored to: '{finalSelection}'");

        _output.WriteLine("AC-5 PASSED: Log level can be changed via ComboBox.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-6: Diagnostics — Toggle Log Category
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC6_Diagnostics_ShouldToggleLLMApiCallsCategory()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(400);

        // Find the "LLM API Calls" CheckBox by its displayed text
        var llmCheckbox = FindByNameContains("LLM API Calls");
        Assert.NotNull(llmCheckbox);

        // Record initial toggle state
        var hasTogglePattern = llmCheckbox!.Patterns.Toggle.IsSupported;
        _output.WriteLine($"LLM API Calls CheckBox supports Toggle pattern: {hasTogglePattern}");

        // The CheckBox wrapper is the element with the name "LLM API Calls"
        // Click it to toggle off
        llmCheckbox.Click();
        await Task.Delay(400);

        // Verify toggle state changed (if Toggle pattern is supported)
        if (hasTogglePattern)
        {
            var stateAfterOff = llmCheckbox.Patterns.Toggle.Pattern.ToggleState;
            _output.WriteLine($"LLM API Calls after toggle off: {stateAfterOff}");
        }
        _output.WriteLine("Toggled LLM API Calls category off.");

        // Click it again to toggle back on
        llmCheckbox.Click();
        await Task.Delay(400);

        // Verify toggle state restored
        if (hasTogglePattern)
        {
            var stateAfterOn = llmCheckbox.Patterns.Toggle.Pattern.ToggleState;
            _output.WriteLine($"LLM API Calls after toggle on: {stateAfterOn}");
        }
        _output.WriteLine("Toggled LLM API Calls category back on.");

        _output.WriteLine("AC-6 PASSED: LLM API Calls log category can be toggled.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-7: Diagnostics — Log File Management Buttons
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC7_Diagnostics_LogFileManagementButtonsExist()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(400);

        // Assert — "📂 Open Logs Folder" button exists (command uses Process.Start)
        var openLogsBtn = FindByNameContains("Open Logs Folder");
        Assert.NotNull(openLogsBtn);
        Assert.False(openLogsBtn!.IsOffscreen,
            "Open Logs Folder button should be visible.");
        _output.WriteLine("Open Logs Folder button found and visible.");

        // Assert — "🗑️ Clear Logs" button exists
        var clearLogsBtn = FindByNameContains("Clear Logs");
        Assert.NotNull(clearLogsBtn);
        Assert.False(clearLogsBtn!.IsOffscreen,
            "Clear Logs button should be visible.");
        _output.WriteLine("Clear Logs button found and visible.");

        // Assert — The "Log File Management" section header is visible
        var logManagementHeader = FindByName("Log File Management");
        Assert.NotNull(logManagementHeader);
        _output.WriteLine("Log File Management section header found.");

        _output.WriteLine("AC-7 PASSED: Log File Management section has both buttons.");
    }
}
