using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 8: Settings, Diagnostics & Appearance.
/// Verifies all 16 settings categories are present, diagnostics log level/category controls,
/// and log file management buttons.
///
/// All tests use the shared E2E collection fixture for a single app launch per suite run.
/// </summary>
[Collection("E2E")]
public sealed class SettingsDiagnosticsE2ETests : E2eTestBase
{
    // The 16 expected settings categories in the sidebar, ordered as they appear
    private static readonly string[] ExpectedCategories =
    [
        "Providers",
        "Profiles",
        "Appearance",
        "Wiki",
        "Backup",
        "Text Actions",
        "Hotkeys",
        "Tools",
        "Language",
        "Notifications",
        "Startup",
        "Updates",
        "Pricing",
        "Security",
        "Diagnostics",
        "Maintenance",
    ];

    public SettingsDiagnosticsE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // Settings Categories
    // ============================================================

    [Fact]
    public async Task SettingsView_ShouldLoadAllCategories()
    {
        await UseSharedAppAsync();
        NavigateToSettings();

        // Verify each category appears in the sidebar
        foreach (var category in ExpectedCategories)
        {
            var item = FindByNameContains(category);
            Assert.NotNull(item);
            _output.WriteLine($"Settings category found: '{category}'");
        }

        _output.WriteLine($"All {ExpectedCategories.Length} settings categories verified.");
    }

    [Fact]
    public async Task SettingsCategory_ShouldShowCorrectHeader()
    {
        await UseSharedAppAsync();
        NavigateToSettings();

        // Select each category and verify the content area header
        foreach (var category in ExpectedCategories)
        {
            SelectSettingsCategory(category);
            await Task.Delay(300);

            // Find the SettingsView content area (Column 1 of the outer Grid)
            var settingsView = FindById("SettingsView");
            Assert.NotNull(settingsView);

            // Search for a TextBlock that contains the category name and is sized as a header
            // (FontSize 16, FontWeight SemiBold). We search with ByControlType to avoid
            // matching the sidebar ListBoxItem, which is in a different column.
            AutomationElement? contentHeader = null;
            var allElements = settingsView!.FindAllDescendants();
            foreach (var el in allElements)
            {
                if (el.ControlType == ControlType.Text &&
                    el.Name?.Contains(category, StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Verify it's a header-sized element by checking the bounding rectangle
                    // Sidebar items are ~12px font; headers are more prominent
                    if (el.BoundingRectangle.Height >= 16)
                    {
                        contentHeader = el;
                        break;
                    }
                }
            }

            Assert.NotNull(contentHeader);
            _output.WriteLine($"Category '{category}' content area header verified.");
        }
    }

    // ============================================================
    // Diagnostics — Log Level ComboBox
    // ============================================================

    [Fact]
    public async Task Diagnostics_LogLevelCombo_ShouldHaveAllOptions()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Find the log level ComboBox by AutomationId
        var logLevelCombo = FindById("DiagnosticsLogLevelCombo")?.AsComboBox();
        Assert.NotNull(logLevelCombo);

        // Expand to see options
        logLevelCombo!.Expand();
        await Task.Delay(300);

        var items = logLevelCombo.Items;
        var itemNames = items.Select(i => i.Name).ToArray();
        _output.WriteLine($"Log level options: [{string.Join(", ", itemNames)}]");

        Assert.Contains(itemNames, n => n.Contains("Information", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemNames, n => n.Contains("Debug", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemNames, n => n.Contains("Verbose", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, items.Length);

        logLevelCombo.Collapse();
        _output.WriteLine("Diagnostics log level ComboBox: all 3 options present.");
    }

    [Fact]
    public async Task Diagnostics_LogLevel_RoundTripRestore()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        var logLevelCombo = FindById("DiagnosticsLogLevelCombo")?.AsComboBox();
        Assert.NotNull(logLevelCombo);

        // Record initial selection
        logLevelCombo!.Expand();
        await Task.Delay(200);
        var initialSelection = logLevelCombo.SelectedItem?.Name ?? logLevelCombo.Name;
        _output.WriteLine($"Initial log level: '{initialSelection}'");
        logLevelCombo.Collapse();

        // Pick a different level than the current one.
        // If current is "Information" or "Verbose", switch to "Debug" for the toggle test.
        // If current is "Debug", switch to "Verbose".
        // This ensures we always change to a distinctly different level.
        logLevelCombo.Expand();
        await Task.Delay(200);
        var targetLevel = initialSelection.Contains("Debug", StringComparison.OrdinalIgnoreCase)
            ? "Verbose"
            : "Debug";
        var targetItem = logLevelCombo.Items.FirstOrDefault(i =>
            i.Name.Contains(targetLevel, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(targetItem);
        targetItem!.Click();
        logLevelCombo.Collapse();
        await Task.Delay(300);

        // Navigate away and back
        SelectSettingsCategory("Appearance");
        await Task.Delay(300);
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Verify selection persisted across navigation
        var comboAfterReturn = FindById("DiagnosticsLogLevelCombo")?.AsComboBox();
        Assert.NotNull(comboAfterReturn);
        comboAfterReturn!.Expand();
        await Task.Delay(200);
        var selectedAfterReturn = comboAfterReturn.SelectedItem?.Name ?? comboAfterReturn.Name;
        _output.WriteLine($"Log level after round-trip: '{selectedAfterReturn}'");

        Assert.True(selectedAfterReturn.Contains(targetLevel, StringComparison.OrdinalIgnoreCase),
            $"Selected log level '{selectedAfterReturn}' should contain '{targetLevel}'.");
        comboAfterReturn.Collapse();

        // Restore original selection
        comboAfterReturn.Expand();
        await Task.Delay(200);
        var restoreItem = comboAfterReturn.Items.FirstOrDefault(i =>
            i.Name.Equals(initialSelection, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(restoreItem); // Original option should still exist
        restoreItem!.Click();
        comboAfterReturn.Collapse();
        _output.WriteLine($"Restored log level to '{initialSelection}'.");

        _output.WriteLine("Diagnostics log level round-trip restore verified.");
    }

    [Fact]
    public async Task Diagnostics_ToggleLogCategory()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Find the "LLM API Calls" CheckBox by AutomationId
        var llmCheckBox = FindById("DiagnosticsLLMApiCallsCheckBox")?.AsCheckBox();
        Assert.NotNull(llmCheckBox);

        // Toggle off if currently on
        if (llmCheckBox!.IsChecked == true)
        {
            llmCheckBox.Click();
            await Task.Delay(300);
            _output.WriteLine("LLM API Calls toggled OFF.");
        }
        else
        {
            _output.WriteLine("LLM API Calls already OFF.");
        }

        // Refresh and verify unchecked
        var afterOff = FindById("DiagnosticsLLMApiCallsCheckBox")?.AsCheckBox();
        Assert.NotNull(afterOff);
        Assert.False(afterOff!.IsChecked == true, "LLM API Calls should be unchecked.");
        _output.WriteLine("LLM API Calls confirmed unchecked.");

        // Toggle on
        afterOff.Click();
        await Task.Delay(300);

        var afterOn = FindById("DiagnosticsLLMApiCallsCheckBox")?.AsCheckBox();
        Assert.NotNull(afterOn);
        Assert.True(afterOn!.IsChecked == true, "LLM API Calls should be checked.");
        _output.WriteLine("LLM API Calls toggled back ON.");

        _output.WriteLine("Diagnostics log category toggle verified.");
    }

    [Fact]
    public async Task Diagnostics_OpenLogsFolder_ShouldExist()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Find the "Open Logs Folder" button by AutomationId
        var openLogsBtn = FindById("DiagnosticsOpenLogsFolderBtn");
        Assert.NotNull(openLogsBtn);
        _output.WriteLine("Diagnostics: 'Open Logs Folder' button exists.");
    }

    [Fact]
    public async Task Diagnostics_ClearLogs_ShouldExist()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Diagnostics");
        await Task.Delay(300);

        // Find the "Clear Logs" button by AutomationId
        var clearLogsBtn = FindById("DiagnosticsClearLogsBtn");
        Assert.NotNull(clearLogsBtn);
        _output.WriteLine("Diagnostics: 'Clear Logs' button exists.");
    }

}
