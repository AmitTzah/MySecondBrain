using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 8: Appearance Settings & Maintenance.
/// Verifies appearance controls (Dark/Light theme RadioButtons, ChatThemeCombo, font settings),
/// the "Re-run Onboarding Wizard" link, and the Maintenance category's Compact Database button.
///
/// All tests use the shared E2E collection fixture for a single app launch per suite run.
/// </summary>
[Collection("E2E")]
public sealed class AppearanceOnboardingE2ETests : E2eTestBase
{
    public AppearanceOnboardingE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // Appearance — Dark/Light Theme RadioButtons
    // ============================================================

    [Fact]
    public async Task Appearance_DarkLightThemeRadioButtons()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Appearance");
        await Task.Delay(300);

        // Find the Dark and Light RadioButtons in the Theme section
        var darkRadioBtn = FindByNameContains("Dark");
        var lightRadioBtn = FindByNameContains("Light");

        Assert.NotNull(darkRadioBtn);
        Assert.NotNull(lightRadioBtn);

        // Determine which is currently selected
        var darkSelected = darkRadioBtn!.Patterns.SelectionItem?.Pattern.IsSelected ?? false;
        var lightSelected = lightRadioBtn!.Patterns.SelectionItem?.Pattern.IsSelected ?? false;
        _output.WriteLine($"Theme RadioButtons: Dark selected={darkSelected}, Light selected={lightSelected}");

        // Toggle: if Dark is selected, click Light; otherwise click Dark
        if (darkSelected)
        {
            lightRadioBtn.Click();
            await Task.Delay(400);
            var lightNow = lightRadioBtn.Patterns.SelectionItem?.Pattern.IsSelected ?? false;
            Assert.True(lightNow, "Light RadioButton should be selected after click.");
            _output.WriteLine("Toggled from Dark → Light.");
        }
        else
        {
            darkRadioBtn.Click();
            await Task.Delay(400);
            var darkNow = darkRadioBtn.Patterns.SelectionItem?.Pattern.IsSelected ?? false;
            Assert.True(darkNow, "Dark RadioButton should be selected after click.");
            _output.WriteLine("Toggled from Light → Dark.");
        }

        // Round-trip back
        if (darkSelected)
        {
            darkRadioBtn.Click();
            await Task.Delay(400);
            var darkRound = darkRadioBtn.Patterns.SelectionItem?.Pattern.IsSelected ?? false;
            Assert.True(darkRound, "Dark RadioButton should be selected after round-trip.");
            _output.WriteLine("Round-trip back to Dark.");
        }
        else
        {
            lightRadioBtn.Click();
            await Task.Delay(400);
            var lightRound = lightRadioBtn.Patterns.SelectionItem?.Pattern.IsSelected ?? false;
            Assert.True(lightRound, "Light RadioButton should be selected after round-trip.");
            _output.WriteLine("Round-trip back to Light.");
        }

        _output.WriteLine("Appearance Dark/Light theme RadioButtons verified.");
    }

    // ============================================================
    // Appearance — Chat Theme ComboBox
    // ============================================================

    [Fact]
    public async Task Appearance_ChatThemeCombo_ShouldBePresent()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Appearance");
        await Task.Delay(300);

        // Find the Chat Theme ComboBox (second ComboBox in Appearance: "Chat Theme" section)
        var chatThemeCombo = FindComboBoxNearLabel("Chat Theme");
        Assert.NotNull(chatThemeCombo);

        chatThemeCombo!.Expand();
        await Task.Delay(300);

        var items = chatThemeCombo.Items;
        var itemNames = items.Select(i => i.Name).ToArray();
        _output.WriteLine($"ChatTheme items: [{string.Join(", ", itemNames)}]");

        Assert.True(items.Length > 0, "ChatThemeCombo should have at least 1 option.");

        chatThemeCombo.Collapse();
        _output.WriteLine("Appearance Chat Theme ComboBox is present with options.");
    }

    // ============================================================
    // Appearance — Font Settings
    // ============================================================

    [Fact]
    public async Task Appearance_FontSettings_ShouldBePresent()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Appearance");
        await Task.Delay(300);

        // Font Family ComboBox
        var fontFamilyLabel = FindByNameContains("Font Family");
        Assert.NotNull(fontFamilyLabel);
        _output.WriteLine("Appearance: 'Font Family' label found.");

        // Assert the Font Family ComboBox is present (matches test name "ShouldBePresent")
        var fontFamilyCombo = FindComboBoxNearLabel("Font Family");
        Assert.NotNull(fontFamilyCombo);
        _output.WriteLine("Appearance: Font Family ComboBox found.");

        // Font Size — find the label (Slider has no AutomationId)
        var fontSizeLabel = FindByNameContains("Font Size");
        Assert.NotNull(fontSizeLabel);
        _output.WriteLine("Appearance: 'Font Size' label found.");

        // Font Weight ComboBox
        var fontWeightLabel = FindByNameContains("Font Weight");
        Assert.NotNull(fontWeightLabel);
        _output.WriteLine("Appearance: 'Font Weight' label found.");

        // Assert the Font Weight ComboBox is present (matches test name "ShouldBePresent")
        var fontWeightCombo = FindComboBoxNearLabel("Font Weight");
        Assert.NotNull(fontWeightCombo);

        fontWeightCombo!.Expand();
        await Task.Delay(200);
        var weightItems = fontWeightCombo.Items;
        _output.WriteLine($"Font Weight options: [{string.Join(", ", weightItems.Select(i => i.Name))}]");
        Assert.True(weightItems.Length > 0, "Font Weight ComboBox should have options.");
        fontWeightCombo.Collapse();

        _output.WriteLine("Appearance font settings controls verified.");
    }

    // ============================================================
    // Appearance — Re-run Onboarding Link
    // ============================================================

    [Fact]
    public async Task Appearance_ReRunOnboardingLink_ShouldExist()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        await Task.Delay(300);

        // The "🔄 Re-run Onboarding Wizard" hyperlink is in the Settings sidebar footer
        var reRunLink = FindByNameContains("Re-run Onboarding Wizard");
        Assert.NotNull(reRunLink);
        _output.WriteLine("Settings sidebar: '🔄 Re-run Onboarding Wizard' hyperlink found.");
    }

    // ============================================================
    // Maintenance — Vacuum / Compact Database
    // ============================================================

    [Fact]
    public async Task Settings_Maintenance_Vacuum_ShouldExist()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Maintenance");
        await Task.Delay(300);

        // Find the "Compact Database" button (content may vary based on VM state)
        var compactBtn = FindByNameContains("Compact Database");
        Assert.NotNull(compactBtn);
        _output.WriteLine("Maintenance: 'Compact Database' button exists.");
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// Finds a ComboBox that is the nearest sibling after a TextBlock with matching label text.
    /// Used for controls without explicit AutomationIds — we locate them by nearby label.
    ///
    /// NOTE: This helper is also duplicated in SettingsDiagnosticsE2ETests.cs.
    /// Keep both copies in sync if modifying.
    /// </summary>
    private FlaUI.Core.AutomationElements.ComboBox? FindComboBoxNearLabel(string labelText)
    {
        var label = FindByNameContains(labelText, timeout: TimeSpan.FromSeconds(2));
        if (label == null) return null;

        // Walk up to find the containing card border, then find the ComboBox within it
        var parent = label.Parent;
        while (parent != null)
        {
            var combo = parent.FindFirstChild(_cf.ByControlType(ControlType.ComboBox));
            if (combo != null && combo.IsAvailable)
                return combo.AsComboBox();
            parent = parent.Parent;
        }

        // Fallback: find any ComboBox in the SettingsView
        _output.WriteLine($"[WARN] FindComboBoxNearLabel('{labelText}'): label-parent walk failed, using SettingsView-wide fallback.");
        var settingsView = FindById("SettingsView");
        if (settingsView == null) return null;

        var allCombos = settingsView.FindAllDescendants(_cf.ByControlType(ControlType.ComboBox));
        var firstCombo = allCombos.FirstOrDefault(c => c.IsAvailable);
        if (firstCombo != null)
        {
            _output.WriteLine($"[WARN] Fallback returned ComboBox '{firstCombo.Name}' which may not be the correct one.");
        }
        return firstCombo?.AsComboBox();
    }
}
