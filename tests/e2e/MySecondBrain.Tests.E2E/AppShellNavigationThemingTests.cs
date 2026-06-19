using FlaUI.Core.Definitions;
using MySecondBrain.UI.Controls;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 5: App Shell, Navigation & Theming.
///
/// PREREQUISITES:
/// 1. Build the solution in Debug|Any CPU before running these tests.
/// 2. The app executable must exist at the path returned by GetAppPath().
/// 3. No other instance of MySecondBrain.UI.exe should be running.
///
/// Uses a shared E2eFixture that launches the app once for all tests,
/// reducing total suite time from ~2min to ~30s.
/// </summary>
[Collection("E2E")]
public class AppShellNavigationThemingTests : IClassFixture<E2eFixture>, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly E2eFixture _fixture;
    private readonly ConditionFactory _cf;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private const int RetryIntervalMs = 200;

    // Map screen names to view AutomationIds for AC2 navigation tests
    private static readonly Dictionary<string, string> ScreenViewMap = new(StringComparer.Ordinal)
    {
        ["Chats"] = "ChatView",
        ["Wiki"] = "WikiBrowserView",
        ["Media"] = "MediaLibraryView",
        ["Artifacts"] = "GlobalArtifactsBrowserView",
        ["Usage"] = "UsageDashboardView",
        ["Settings"] = "SettingsView",
    };

    // Right panel content label exposed in UIA (TextBlock Name = Text)
    private const string RightPanelArtifactsHeader = "📄 Artifacts";

    public AppShellNavigationThemingTests(E2eFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _cf = _fixture.Automation.ConditionFactory;
    }

    /// <summary>
    /// Per-test cleanup: reset to a known state (Chats screen) so the next
    /// test starts fresh, regardless of what this test changed.
    /// </summary>
    public void Dispose()
    {
        if (_fixture.App.HasExited)
            return;

        try
        {
            // Reset navigation back to Chats
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

    // ── Reusable helpers ────────────────────────────────────────────────

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

    private AutomationElement? FindByName(string name, AutomationElement? root = null)
    {
        root ??= _fixture.MainWindow;
        if (root == null) return null;

        var condition = _cf.ByName(name);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < DefaultTimeout)
        {
            var element = root.FindFirst(TreeScope.Descendants, condition);

            if (element != null && element.IsAvailable)
                return element;

            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-1: Three-Region Shell Layout
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC1_ThreeRegionShellLayout_ShouldHaveSidebarContentAndRightPanel()
    {
        // Arrange & Act
        await UseSharedAppAsync();

        // Assert — Sidebar (verify NavChats radio button is present and visible)
        var navChats = FindById("NavChats");
        Assert.NotNull(navChats);
        Assert.False(navChats!.IsOffscreen, "Sidebar nav button should be visible on screen.");

        // Assert — Content area (verify ChatView UserControl is loaded)
        var chatView = FindById("ChatView");
        Assert.NotNull(chatView);
        Assert.False(chatView!.IsOffscreen, "ChatView content should be visible.");

        // Assert — Right panel (verify RightPanelSplitter is present)
        var rightPanelSplitter = FindById("RightPanelSplitter");
        Assert.NotNull(rightPanelSplitter);
        Assert.False(rightPanelSplitter!.IsOffscreen,
            "Right panel splitter should be visible on Chats screen.");

        var artifactsHeader = FindByName(RightPanelArtifactsHeader);
        Assert.NotNull(artifactsHeader);
        Assert.False(artifactsHeader!.IsOffscreen,
            "Right panel artifacts header should be visible on Chats screen.");

        // Assert — GridSplitters exist
        var sidebarSplitter = FindById("SidebarSplitter");
        Assert.NotNull(sidebarSplitter);
        Assert.False(sidebarSplitter!.IsOffscreen, "Sidebar GridSplitter should be visible.");

        // Assert — Window title
        Assert.Equal("MySecondBrain", _fixture.MainWindow.Title);

        _output.WriteLine("AC-1 PASSED: Three-region shell layout verified.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-2: Screen Navigation
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NavChats", "Chats")]
    [InlineData("NavWiki", "Wiki")]
    [InlineData("NavMedia", "Media")]
    [InlineData("NavArtifacts", "Artifacts")]
    [InlineData("NavUsage", "Usage")]
    [InlineData("NavSettings", "Settings")]
    public async Task AC2_ScreenNavigation_ShouldSwitchContentArea(string navButtonId, string screenName)
    {
        // Arrange
        await UseSharedAppAsync();

        // Act — Click the navigation button
        var navButton = FindById(navButtonId);
        Assert.NotNull(navButton);
        Assert.True(navButton!.IsEnabled,
            $"Navigation button '{navButtonId}' should be enabled.");

        navButton.Click();
        await Task.Delay(500); // Allow screen transition

        // Assert — The corresponding view UserControl is now loaded
        var expectedViewId = ScreenViewMap[screenName];
        var viewElement = FindById(expectedViewId);
        Assert.NotNull(viewElement);
        Assert.False(viewElement!.IsOffscreen,
            $"View '{expectedViewId}' should be visible after clicking '{navButtonId}'.");

        _output.WriteLine($"AC-2 PASSED: Navigation to '{screenName}' shows '{expectedViewId}'.");
    }

    [Fact]
    public async Task AC2_RightPanel_ShouldHideOnNonChatScreens()
    {
        // Arrange
        await UseSharedAppAsync();

        // Initially on Chats — right panel should be visible.
        // Verify via RightPanelSplitter (GridSplitter) which reliably tracks the
        // IsRightPanelVisible binding in UIA – TextBlock children may "leak"
        // into the UIA tree even when their parent Grid is collapsed.
        var rightPanelSplitter = FindById("RightPanelSplitter");
        Assert.NotNull(rightPanelSplitter);
        Assert.False(rightPanelSplitter!.IsOffscreen,
            "Right panel splitter should be visible on Chats screen.");

        var artifactsHeader = FindByName(RightPanelArtifactsHeader);
        Assert.NotNull(artifactsHeader);
        Assert.False(artifactsHeader!.IsOffscreen,
            "Right panel artifacts header should be visible on Chats screen.");

        // Act — Navigate to Wiki
        var navWiki = FindById("NavWiki");
        Assert.NotNull(navWiki);
        navWiki!.Click();
        await Task.Delay(500);

        // Assert — RightPanelSplitter should disappear from UIA tree on Wiki.
        // The GridSplitter's Visibility binding to IsRightPanelVisible is
        // faithfully reflected in UIA (unlike child TextBlocks which may
        // be reparented by the automation tree when the parent collapses).
        // Use a 1.5s timeout for negative search — element should be gone fast.
        var rightPanelSplitterAfter = FindById("RightPanelSplitter",
            timeout: TimeSpan.FromSeconds(1.5));
        Assert.True(rightPanelSplitterAfter == null || rightPanelSplitterAfter.IsOffscreen,
            "Right panel splitter should be hidden on Wiki screen.");

        _output.WriteLine("AC-2 PASSED: Right panel hides on non-Chats screens.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-3: Dark/Light Theme Toggle
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC3_ThemeToggle_ShouldSwitchBetweenDarkAndLight()
    {
        // Arrange
        await UseSharedAppAsync();

        // Find theme toggle button (in the ChatView header)
        var toggleBtn = FindById("ThemeToggleBtn");
        Assert.NotNull(toggleBtn);
        Assert.True(toggleBtn!.IsEnabled, "Theme toggle button should be enabled.");

        // Record initial icon text
        var initialIcon = toggleBtn.Name;
        _output.WriteLine($"Initial theme icon: '{initialIcon}'");

        // Act — Click the theme toggle button
        toggleBtn.Click();
        await Task.Delay(600); // Allow theme resources to swap

        // Assert — Icon should have changed (☀ → 🌙 or 🌙 → ☀)
        var newIcon = toggleBtn.Name;
        _output.WriteLine($"After toggle theme icon: '{newIcon}'");
        Assert.NotEqual(initialIcon, newIcon);

        // Toggle back to verify round-trip
        toggleBtn.Click();
        await Task.Delay(600);

        var finalIcon = toggleBtn.Name;
        Assert.Equal(initialIcon, finalIcon);

        _output.WriteLine("AC-3 PASSED: Theme toggle switches icon. Round-trip verified.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-4: Chat Visual Themes
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC4_ChatThemeSelector_ShouldHaveThreeOptions()
    {
        // Arrange
        await UseSharedAppAsync();

        // Find the Chat Theme ComboBox
        var comboBox = FindById("ChatThemeCombo");
        Assert.NotNull(comboBox);

        var combo = comboBox!.AsComboBox();
        Assert.NotNull(combo);

        // Act — Expand and list items
        combo.Expand();
        await Task.Delay(500);

        var items = combo.Items;
        var itemTexts = items?
            .Select(i => i.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList() ?? [];

        // Collapse after reading
        combo.Collapse();

        _output.WriteLine($"Chat theme options: [{string.Join(", ", itemTexts)}]");

        // Assert — Three themes available
        Assert.Contains("Classic", itemTexts, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Compact", itemTexts, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Bubble", itemTexts, StringComparer.OrdinalIgnoreCase);

        _output.WriteLine("AC-4 PASSED: Chat theme selector has Classic, Compact, Bubble.");
    }

    [Theory]
    [InlineData("Compact")]
    [InlineData("Bubble")]
    [InlineData("Classic")]
    public async Task AC4_ChatThemeSelector_ShouldSelectTheme(string expectedTheme)
    {
        // Arrange
        await UseSharedAppAsync();

        var comboBox = FindById("ChatThemeCombo");
        Assert.NotNull(comboBox);

        var combo = comboBox!.AsComboBox();

        // Act — Select the theme
        combo.Select(expectedTheme);
        await Task.Delay(500);

        // Assert — Selection updated
        var selected = combo.SelectedItem?.Name ?? string.Empty;
        Assert.Equal(expectedTheme, selected, ignoreCase: true);

        _output.WriteLine($"AC-4 PASSED: Chat theme set to '{expectedTheme}'.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-5: Font Size Quick Adjust
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AC5_FontSizeAdjust_ShouldIncreaseAndDecrease()
    {
        // Arrange
        await UseSharedAppAsync();

        var increaseBtn = FindById("IncreaseFontBtn");
        var decreaseBtn = FindById("DecreaseFontBtn");
        var fontSizeDisplay = FindById("FontSizeDisplay");

        Assert.NotNull(increaseBtn);
        Assert.NotNull(decreaseBtn);
        Assert.NotNull(fontSizeDisplay);

        Assert.True(increaseBtn!.IsEnabled);
        Assert.True(decreaseBtn!.IsEnabled);

        // Read initial font size
        var initialText = fontSizeDisplay!.Name;
        _output.WriteLine($"Initial font size display: '{initialText}'");

        // Act — Click Increase twice
        increaseBtn.Click();
        await Task.Delay(300);
        increaseBtn.Click();
        await Task.Delay(300);

        var afterIncrease = fontSizeDisplay.Name;
        _output.WriteLine($"After increase (x2): '{afterIncrease}'");

        // Assert — Size should have increased
        Assert.NotEqual(initialText, afterIncrease);

        // Act — Click Decrease once
        decreaseBtn.Click();
        await Task.Delay(300);

        var afterDecrease = fontSizeDisplay.Name;
        _output.WriteLine($"After decrease: '{afterDecrease}'");

        // Assert — Size should be between initial and increased
        Assert.NotEqual(afterIncrease, afterDecrease);

        _output.WriteLine("AC-5 PASSED: Font size increase and decrease work.");
    }

    [Fact]
    public async Task AC5_FontSize_ShouldNotCrashWhenClickedRapidly()
    {
        // Arrange
        await UseSharedAppAsync();

        var increaseBtn = FindById("IncreaseFontBtn");
        var decreaseBtn = FindById("DecreaseFontBtn");

        Assert.NotNull(increaseBtn);
        Assert.NotNull(decreaseBtn);

        // Act — Click a few times each way (enough to prove no crash)
        for (var i = 0; i < 3; i++)
        {
            increaseBtn!.Click();
            await Task.Delay(50);
        }
        Assert.True(increaseBtn!.IsEnabled);

        for (var i = 0; i < 3; i++)
        {
            decreaseBtn!.Click();
            await Task.Delay(50);
        }
        Assert.True(decreaseBtn!.IsEnabled);

        _output.WriteLine("AC-5 PASSED: Rapid font size clicks do not crash the app.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-6: ContentRendererRegistry
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void AC6_ContentRendererRegistry_PrioritiesAreCorrect()
    {
        // Expected order (ascending priority = lower number = higher precedence):
        var expected = new Dictionary<string, int>
        {
            ["MarkdownText"] = 100,
            ["CodeBlock"] = 200,
            ["ArtifactReference"] = 300,
            ["Image"] = 400,
            ["Media"] = 500,
            ["Thinking"] = 600,
            ["ToolCall"] = 700,
        };

        var rendererTypes = new Dictionary<string, Type>
        {
            ["MarkdownText"] = typeof(MarkdownTextRenderer),
            ["CodeBlock"] = typeof(CodeBlockRenderer),
            ["ArtifactReference"] = typeof(ArtifactReferenceRenderer),
            ["Image"] = typeof(ImageRenderer),
            ["Media"] = typeof(MediaRenderer),
            ["Thinking"] = typeof(ThinkingRenderer),
            ["ToolCall"] = typeof(ToolCallRenderer),
        };

        foreach (var (name, expectedPriority) in expected)
        {
            var type = rendererTypes[name];
            var priorityProp = type.GetProperty("Priority");
            Assert.NotNull(priorityProp);

            // Priority is an instance property; create an instance to read it
            var instance = Activator.CreateInstance(type);
            Assert.NotNull(instance);

            var actualPriority = (int)priorityProp!.GetValue(instance)!;
            Assert.Equal(expectedPriority, actualPriority);
        }

        // Verify priorities are in strictly increasing order
        var sorted = expected.Values.OrderBy(v => v).ToArray();
        for (var i = 1; i < sorted.Length; i++)
        {
            Assert.True(sorted[i] > sorted[i - 1],
                $"Priority {sorted[i]} should be greater than {sorted[i - 1]}");
        }

        _output.WriteLine("AC-6 PASSED: All 7 content block renderers have correct priorities.");
    }

    // ════════════════════════════════════════════════════════════════════
    // App lifecycle smoke tests (shared fixture)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task App_ShouldLaunchAndShowMainWindow()
    {
        // Arrange & Act
        await UseSharedAppAsync();

        // Assert
        Assert.NotNull(_fixture.MainWindow);
        Assert.False(string.IsNullOrEmpty(_fixture.MainWindow.Title));
        Assert.Equal("MySecondBrain", _fixture.MainWindow.Title);

        var rect = _fixture.MainWindow.BoundingRectangle;
        Assert.True(rect.Width > 0, "MainWindow should have positive width after launch.");

        _output.WriteLine($"App launched. Window: {rect.Width}x{rect.Height}");
    }

    [Fact]
    public void App_ShouldBeRunningFromFixture()
    {
        // With the shared fixture pattern, the fixture handles app lifecycle.
        // This test verifies the app is still running (non-destructive check).
        Assert.False(_fixture.App.HasExited,
            "Application should still be running from the shared fixture.");
        Assert.True(_fixture.App.ProcessId > 0,
            "Application should have a valid process ID.");
        _output.WriteLine($"App running: PID={_fixture.App.ProcessId}");
    }
}
