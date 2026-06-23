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

        // Find the Dark and Light RadioButtons by AutomationId
        var darkRadioBtn = FindById("AppearanceDarkRadioBtn");
        var lightRadioBtn = FindById("AppearanceLightRadioBtn");

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

        // Find the Chat Theme ComboBox by AutomationId
        var chatThemeCombo = FindById("AppearanceChatThemeCombo")?.AsComboBox();
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

        // Font Family ComboBox — find by AutomationId
        var fontFamilyCombo = FindById("AppearanceFontFamilyCombo")?.AsComboBox();
        Assert.NotNull(fontFamilyCombo);
        _output.WriteLine("Appearance: Font Family ComboBox found.");

        // Font Size Slider — find by AutomationId
        var fontSizeSlider = FindById("AppearanceFontSizeSlider");
        Assert.NotNull(fontSizeSlider);
        _output.WriteLine("Appearance: Font Size Slider found.");

        // Font Weight ComboBox — find by AutomationId
        var fontWeightCombo = FindById("AppearanceFontWeightCombo")?.AsComboBox();
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
        var reRunLink = FindById("ReRunOnboardingLink");
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

        // Find the "Compact Database" button by AutomationId
        var compactBtn = FindById("MaintenanceCompactDatabaseBtn");
        Assert.NotNull(compactBtn);
        _output.WriteLine("Maintenance: 'Compact Database' button exists.");
    }

}
