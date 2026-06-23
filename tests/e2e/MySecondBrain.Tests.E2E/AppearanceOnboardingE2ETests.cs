using FlaUI.Core.Definitions;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for AC-8 through AC-10 of Feature 8: Appearance & Onboarding.
///
/// PREREQUISITES:
/// 1. Build the solution in Debug|Any CPU before running these tests.
/// 2. The app executable must exist at the path returned by GetAppPath().
/// 3. No other instance of MySecondBrain.UI.exe should be running.
///
/// COVERAGE:
/// AC-8: Appearance section has Dark/Light theme RadioButtons
/// AC-9: Appearance theme toggle switches between Dark and Light
/// AC-10: Re-run Onboarding Wizard hyperlink exists in Settings sidebar footer
///
/// These tests create no persistent data — they read UI state and toggle settings
/// that are re-read on next SettingsViewModel initialization (Transient).
/// </summary>
[Collection("E2E")]
public class AppearanceOnboardingE2ETests : IClassFixture<E2eFixture>, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly E2eFixture _fixture;
    private readonly ConditionFactory _cf;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private const int RetryIntervalMs = 200;

    public AppearanceOnboardingE2ETests(E2eFixture fixture, ITestOutputHelper output)
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

    // ════════════════════════════════════════════════════════════════════
    // AC-8: Appearance — Dark/Light Theme RadioButtons
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC8_Appearance_ThemeRadioButtonsExist()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Appearance");
        await Task.Delay(400);

        // Assert — "🌙 Dark" RadioButton exists
        var darkRadio = FindByNameContains("Dark");
        Assert.NotNull(darkRadio);
        _output.WriteLine("Dark theme RadioButton found.");

        // Assert — "☀️ Light" RadioButton exists
        var lightRadio = FindByNameContains("Light");
        Assert.NotNull(lightRadio);
        _output.WriteLine("Light theme RadioButton found.");

        _output.WriteLine("AC-8 PASSED: Appearance section has Dark and Light theme RadioButtons.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-9: Appearance — Theme Toggle Switches
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC9_Appearance_ThemeToggleSwitchesBetweenDarkAndLight()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Appearance");
        await Task.Delay(400);

        // Find the theme RadioButtons by looking for RadioButton elements
        var settingsView = FindById("SettingsView");
        Assert.NotNull(settingsView);

        var allRadioButtons = settingsView!.FindAllDescendants(
            _cf.ByControlType(ControlType.RadioButton));

        // Find Dark and Light radio buttons
        AutomationElement? darkRadio = null;
        AutomationElement? lightRadio = null;

        foreach (var rb in allRadioButtons)
        {
            var name = rb.Name ?? string.Empty;
            if (name.Contains("🌙") || name.Contains("Dark"))
                darkRadio = rb;
            else if (name.Contains("☀️") || name.Contains("Light"))
                lightRadio = rb;
        }

        Assert.NotNull(darkRadio);
        Assert.NotNull(lightRadio);

        // Record initial state — check which is selected
        // In a RadioButton group, ToggleState.Off / ToggleState.On indicates selection
        var darkToggleSupported = darkRadio!.Patterns.Toggle.IsSupported;
        var lightToggleSupported = lightRadio!.Patterns.Toggle.IsSupported;
        var initialDark = darkToggleSupported
            ? darkRadio.Patterns.Toggle.Pattern.ToggleState == ToggleState.On
            : false;
        _output.WriteLine($"Initial state — Dark selected: {initialDark} " +
            $"(Dark toggle: {darkToggleSupported}, Light toggle: {lightToggleSupported})");

        // Act — Toggle by clicking the opposite theme
        if (initialDark)
        {
            lightRadio!.Click();
            await Task.Delay(600);

            // Assert — Light should now be selected
            if (lightToggleSupported)
            {
                Assert.Equal(ToggleState.On, lightRadio.Patterns.Toggle.Pattern.ToggleState);
            }
            _output.WriteLine("Clicked Light theme. Light should now be selected.");
        }
        else
        {
            darkRadio!.Click();
            await Task.Delay(600);

            // Assert — Dark should now be selected
            if (darkToggleSupported)
            {
                Assert.Equal(ToggleState.On, darkRadio.Patterns.Toggle.Pattern.ToggleState);
            }
            _output.WriteLine("Clicked Dark theme. Dark should now be selected.");
        }

        // Toggle back to restore original state
        if (initialDark)
        {
            darkRadio.Click();
            await Task.Delay(600);

            if (darkToggleSupported)
            {
                Assert.Equal(ToggleState.On, darkRadio.Patterns.Toggle.Pattern.ToggleState);
            }
            _output.WriteLine("Clicked Dark theme (restored).");
        }
        else
        {
            lightRadio.Click();
            await Task.Delay(600);

            if (lightToggleSupported)
            {
                Assert.Equal(ToggleState.On, lightRadio.Patterns.Toggle.Pattern.ToggleState);
            }
            _output.WriteLine("Clicked Light theme (restored).");
        }

        _output.WriteLine("AC-9 PASSED: Appearance theme toggles between Dark and Light.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-10: Re-run Onboarding Wizard Hyperlink
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC10_Settings_ReRunOnboardingHyperlinkExists()
    {
        // Arrange
        await UseSharedAppAsync();
        NavigateToSettings();

        // Assert — The "🔄 Re-run Onboarding Wizard" hyperlink text is present
        var reRunLink = FindByNameContains("Re-run Onboarding Wizard");
        Assert.NotNull(reRunLink);
        Assert.False(reRunLink!.IsOffscreen,
            "Re-run Onboarding Wizard hyperlink should be visible in the settings sidebar footer.");

        _output.WriteLine("AC-10 PASSED: Re-run Onboarding Wizard hyperlink exists in Settings sidebar.");
    }
}
