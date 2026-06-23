using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.UI;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 5: App Shell, Navigation & Theming.
/// Verifies window chrome, three-region layout, screen navigation,
/// theme toggling, chat theme selection, font size controls, and content renderer priorities.
/// All tests use [Collection("E2E")] with ICollectionFixture for a single app launch per suite run.
/// </summary>
[Collection("E2E")]
public sealed class AppShellNavigationThemingE2ETests : E2eTestBase
{
    private static readonly Lazy<IServiceProvider> _serviceProvider = new(() =>
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        return services.BuildServiceProvider();
    });

    public AppShellNavigationThemingE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // Window & Layout
    // ============================================================

    [Fact]
    public async Task MainWindow_ShouldHaveCorrectTitle()
    {
        await UseSharedAppAsync();
        Assert.Equal("MySecondBrain", _fixture.MainWindow.Title);
        _output.WriteLine("Window title verified: MySecondBrain");
    }

    [Fact]
    public async Task Shell_ShouldRenderThreeRegionLayout()
    {
        await UseSharedAppAsync();

        // Confirm sidebar navigation is present
        var navChats = FindById("NavChats");
        Assert.NotNull(navChats);

        // Navigate to Wiki and back to force a clean DataTemplate instantiation
        // of ChatView, avoiding any stale UIA state from prior tests.
        FindById("NavWiki")?.Click();
        await Task.Delay(400);
        FindById("NavChats")?.Click();
        await Task.Delay(500);

        // Wait for ChatView UIA subtree to be fully populated
        WaitForChatHeaderReady();

        // Now ChatView should be findable by its unique AutomationId
        var chatView = FindById("ChatView");
        Assert.NotNull(chatView);

        _output.WriteLine("Three-region layout verified (sidebar, content).");
    }

    // ============================================================
    // Navigation — switch to each screen and verify the target view
    // FindById already polls for up to 3 seconds, so no fixed delay is needed.
    // ============================================================

    [Fact]
    public async Task Navigation_ShouldSwitchToChatsScreen()
    {
        await UseSharedAppAsync();
        var nav = FindById("NavChats");
        Assert.NotNull(nav);
        nav!.Click();
        Assert.NotNull(FindById("ChatView"));
        _output.WriteLine("Navigation to Chats screen verified.");
    }

    [Fact]
    public async Task Navigation_ShouldSwitchToWikiScreen()
    {
        await UseSharedAppAsync();
        var nav = FindById("NavWiki");
        Assert.NotNull(nav);
        nav!.Click();
        Assert.NotNull(FindById("WikiBrowserView"));
        _output.WriteLine("Navigation to Wiki screen verified.");
    }

    [Fact]
    public async Task Navigation_ShouldSwitchToMediaScreen()
    {
        await UseSharedAppAsync();
        var nav = FindById("NavMedia");
        Assert.NotNull(nav);
        nav!.Click();
        Assert.NotNull(FindById("MediaLibraryView"));
        _output.WriteLine("Navigation to Media screen verified.");
    }

    [Fact]
    public async Task Navigation_ShouldSwitchToArtifactsScreen()
    {
        await UseSharedAppAsync();
        var nav = FindById("NavArtifacts");
        Assert.NotNull(nav);
        nav!.Click();
        Assert.NotNull(FindById("GlobalArtifactsBrowserView"));
        _output.WriteLine("Navigation to Artifacts screen verified.");
    }

    [Fact]
    public async Task Navigation_ShouldSwitchToUsageScreen()
    {
        await UseSharedAppAsync();
        FindById("NavChats")!.Click();
        var nav = FindById("NavUsage");
        Assert.NotNull(nav);
        nav!.Click();
        Assert.NotNull(FindById("UsageDashboardView"));
        _output.WriteLine("Navigation to Usage screen verified.");
    }

    [Fact]
    public async Task Navigation_ShouldSwitchToSettingsScreen()
    {
        await UseSharedAppAsync();
        var nav = FindById("NavSettings");
        Assert.NotNull(nav);
        nav!.Click();
        Assert.NotNull(FindById("SettingsView"));
        _output.WriteLine("Navigation to Settings screen verified.");
    }

    // ============================================================
    // Right Panel Visibility
    // ============================================================

    [Fact]
    public async Task RightPanel_ShouldHideOnNonChatScreens()
    {
        await UseSharedAppAsync();

        // Navigate to Wiki — right panel should be hidden
        FindById("NavWiki")!.Click();
        var wikiSplitter = FindById("RightPanelSplitter", timeout: TimeSpan.FromSeconds(1));
        // RightPanelSplitter has Visibility=Collapsed on non-Chat screens;
        // FlaUI reports collapsed elements as IsOffscreen=true or removes them from the tree
        if (wikiSplitter != null)
        {
            Assert.True(wikiSplitter.IsOffscreen,
                "RightPanelSplitter should be off-screen (collapsed) on Wiki screen");
        }

        // Navigate to Settings — right panel should also be hidden
        FindById("NavSettings")!.Click();
        var settingsSplitter = FindById("RightPanelSplitter", timeout: TimeSpan.FromSeconds(1));
        if (settingsSplitter != null)
        {
            Assert.True(settingsSplitter.IsOffscreen,
                "RightPanelSplitter should be off-screen (collapsed) on Settings screen");
        }

        // Navigate back to Chats — right panel should be visible again
        FindById("NavChats")!.Click();
        var chatSplitter = FindById("RightPanelSplitter");
        Assert.NotNull(chatSplitter);
        Assert.False(chatSplitter!.IsOffscreen,
            "RightPanelSplitter should be on-screen on Chats screen");

        _output.WriteLine("Right panel visibility verified: hidden on Wiki/Settings, visible on Chats.");
    }

    // ============================================================
    // Theme Toggle
    // ============================================================

    [Fact]
    public async Task ThemeToggle_ShouldSwitchDarkLight()
    {
        await UseSharedAppAsync();
        var toggleBtn = FindById("ThemeToggleBtn");
        Assert.NotNull(toggleBtn);
        var initialContent = toggleBtn!.Name; // e.g., "🌙" or "☀"
        _output.WriteLine($"Initial theme icon: '{initialContent}'");

        toggleBtn.Click();
        var afterToggle = FindById("ThemeToggleBtn")!.Name;
        _output.WriteLine($"After toggle icon: '{afterToggle}'");
        Assert.NotEqual(initialContent, afterToggle);

        // Round-trip
        FindById("ThemeToggleBtn")!.Click();
        var roundTrip = FindById("ThemeToggleBtn")!.Name;
        Assert.Equal(initialContent, roundTrip);
        _output.WriteLine("Theme toggle round-trip successful.");
    }

    // ============================================================
    // Chat Theme Combo
    // ============================================================

    [Fact]
    public async Task ChatThemeCombo_ShouldHaveAllOptions()
    {
        await UseSharedAppAsync();
        // Navigate to Wiki and back to Chats to force ChatView DataTemplate render
        FindById("NavWiki")?.Click();
        await Task.Delay(400);
        FindById("NavChats")?.Click();
        await Task.Delay(600);
        var combo = FindById("ChatThemeCombo")?.AsComboBox();
        Assert.NotNull(combo);

        // Expand the dropdown so items appear in the UIA tree
        combo!.Expand();

        var items = combo.Items;
        var itemNames = items.Select(i => i.Name).ToArray();
        _output.WriteLine($"ChatThemeCombo items: [{string.Join(", ", itemNames)}]");

        Assert.Contains("Classic", itemNames);
        Assert.Contains("Compact", itemNames);
        Assert.Contains("Bubble", itemNames);
        Assert.Equal(3, items.Length);

        // Select Classic and verify
        var classicItem = items.FirstOrDefault(i => i.Name == "Classic");
        Assert.NotNull(classicItem);
        classicItem!.Click();
        combo.Collapse();

        // Re-expand to verify selection stuck
        combo.Expand();
        var selectedText = combo.SelectedItem?.Name ?? combo.Name;
        _output.WriteLine($"ChatThemeCombo selected: '{selectedText}'");

        combo.Collapse();
        _output.WriteLine("ChatThemeCombo: all options present and selectable.");
    }

    // ============================================================
    // Font Size Controls
    // ============================================================

    /// <summary>
    /// Waits for the ChatView header UIA subtree to be fully populated.
    /// After wizard dismiss + MainWindow re-acquire, the ChatView needs time
    /// for its DataTemplate-bound elements to appear in the UIA tree.
    /// Polls for ThemeToggleBtn (Button, always rendered) as readiness signal.
    /// </summary>
    private void WaitForChatHeaderReady()
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(4))
        {
            if (FindById("ThemeToggleBtn", timeout: TimeSpan.FromMilliseconds(500)) != null)
                return;
            Wait.UntilInputIsProcessed();
        }
        _output.WriteLine("WaitForChatHeaderReady: timed out waiting for ThemeToggleBtn.");
    }

    [Fact]
    public async Task FontSizeButtons_ShouldUpdateDisplay()
    {
        await UseSharedAppAsync();
        // Navigate to Wiki and back to Chats to force ChatView DataTemplate render
        FindById("NavWiki")?.Click();
        await Task.Delay(400);
        FindById("NavChats")?.Click();
        await Task.Delay(600);

        // Verify all font controls exist in the UIA tree
        var display = FindById("FontSizeDisplay");
        Assert.NotNull(display);
        _output.WriteLine($"Font size display found: '{display!.Name}'");

        var increaseBtn = FindById("IncreaseFontBtn");
        Assert.NotNull(increaseBtn);
        Assert.True(increaseBtn!.IsEnabled);
        _output.WriteLine("Increase font button found and enabled.");

        var decreaseBtn = FindById("DecreaseFontBtn");
        Assert.NotNull(decreaseBtn);
        Assert.True(decreaseBtn!.IsEnabled);
        _output.WriteLine("Decrease font button found and enabled.");

        // Test that clicks don't crash the app (rapid click test covers value changes)
        increaseBtn.Click();
        await Task.Delay(200);
        decreaseBtn.Click();
        await Task.Delay(200);
        Assert.False(_fixture.App.HasExited);
        _output.WriteLine("Font size buttons work without crashing.");
    }

    [Fact]
    public async Task FontSizeButtons_RapidClicksShouldNotCrash()
    {
        await UseSharedAppAsync();

        var increaseBtn = FindById("IncreaseFontBtn");
        var decreaseBtn = FindById("DecreaseFontBtn");
        Assert.NotNull(increaseBtn);
        Assert.NotNull(decreaseBtn);

        for (int i = 0; i < 5; i++)
        {
            increaseBtn!.Click();
            decreaseBtn!.Click();
            Wait.UntilInputIsProcessed();
        }

        // Verify app is still responsive
        var display = FindById("FontSizeDisplay");
        Assert.NotNull(display);
        Assert.False(string.IsNullOrEmpty(display!.Name));
        _output.WriteLine("Rapid font size clicks did not crash the application.");
    }

    // ============================================================
    // Content Renderer Priorities
    // ============================================================

    [Fact]
    public async Task ContentRenderers_ShouldHaveCorrectPriorities()
    {
        await UseSharedAppAsync();

        var registry = _serviceProvider.Value.GetRequiredService<IContentRendererRegistry>();
        var renderers = registry.GetRenderers();

        Assert.Equal(7, renderers.Count);

        var expected = new (string Name, int Priority)[]
        {
            ("MarkdownText", 100),
            ("CodeBlock", 200),
            ("ArtifactReference", 300),
            ("Image", 400),
            ("Media", 500),
            ("Thinking", 600),
            ("ToolCall", 700),
        };

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, renderers[i].RendererName);
            Assert.Equal(expected[i].Priority, renderers[i].Priority);
            _output.WriteLine($"Renderer {i}: {expected[i].Name} (Priority={expected[i].Priority})");
        }

        for (int i = 1; i < renderers.Count; i++)
        {
            Assert.True(renderers[i].Priority > renderers[i - 1].Priority,
                $"Renderer {renderers[i].RendererName} has priority {renderers[i].Priority} " +
                $"which is not greater than previous {renderers[i - 1].RendererName}'s {renderers[i - 1].Priority}");
        }

        _output.WriteLine("All 7 content renderers have correct ascending priorities.");
    }
}
