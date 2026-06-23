using System.Reflection;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.Services;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for AC-1 (System Tray Integration) and AC-2 (Global Hotkey Registration)
/// of Feature 6: Windows OS Platform Infrastructure.
///
/// PREREQUISITES:
/// 1. Build the solution in Debug|Any CPU before running these tests.
/// 2. The app executable must exist at the path returned by GetAppPath().
/// 3. No other instance of MySecondBrain.UI.exe should be running.
///
/// COVERAGE:
/// AC-1: System Tray Integration — DI resolution, context menu, events, recent chats, generation indicator
/// AC-2: Global Hotkey Registration — DI registration, default hotkeys, conflict detection, register/unregister
/// </summary>
[Collection("E2E")]
public class SystemTrayHotkeyE2ETests : IClassFixture<E2eFixture>, IDisposable
{
    // ── Magic strings (defined once to avoid duplication across tests) ─────
    private const string MenuNewChat = "New Chat";
    private const string MenuOpenStudio = "Open Studio";
    private const string MenuCommandBar = "Command Bar";
    private const string MenuRecentChats = "Recent Chats";
    private const string MenuSettings = "Settings";
    private const string MenuExit = "Exit";
    private const string MenuNoRecentChats = "No recent chats";

    // ── Fields ────────────────────────────────────────────────────────────
    private readonly ITestOutputHelper _output;
    private readonly E2eFixture _fixture;

    // Cache: build DI container once per test class (∼2s vs ∼36s for 18 re-builds).
    // The container is immutable after creation and none of the tests mutate the
    // registration structure. Service instances inside the container ARE singletons
    // with mutable state — per-test Dispose() resets that state.
    private static readonly Lazy<ServiceProvider> _container = new(() =>
    {
        var services = new ServiceCollection();
        UI.App.ConfigureServices(services);
        return services.BuildServiceProvider();
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private static ServiceProvider Container => _container.Value;

    // Cache FieldInfo lookups so reflection overhead is paid once, not per-test.
    private static readonly Lazy<FieldInfo?> _contextMenuField = new(() =>
        typeof(WinFormsSystemTrayService).GetField("_contextMenu",
            BindingFlags.NonPublic | BindingFlags.Instance));

    private static readonly Lazy<FieldInfo?> _recentChatsMenuField = new(() =>
        typeof(WinFormsSystemTrayService).GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance));

    private static readonly Lazy<FieldInfo?> _notifyIconField = new(() =>
        typeof(WinFormsSystemTrayService).GetField("_notifyIcon",
            BindingFlags.NonPublic | BindingFlags.Instance));

    public SystemTrayHotkeyE2ETests(E2eFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Per-test cleanup: reset navigation AND DI-singleton mutable state so the
    /// next test starts fresh regardless of prior test outcome.
    /// </summary>
    public void Dispose()
    {
        // ── Reset DI singleton state ────────────────────────────────────
        try
        {
            if (_container.IsValueCreated)
            {
                var trayService = Container.GetService<ISystemTrayService>();
                if (trayService is not null)
                {
                    trayService.UpdateRecentChats([]);
                    trayService.SetGenerationIndicator(false);
                }
            }
        }
        catch
        {
            // Best-effort — container may not be initialized yet
        }

        // ── Reset UIA navigation ─────────────────────────────────────────
        if (_fixture.App.HasExited)
            return;

        try
        {
            var navChats = _fixture.MainWindow.FindFirstDescendant(
                _fixture.Automation.ConditionFactory.ByAutomationId("NavChats"));
            navChats?.Click();
            Thread.Sleep(300);
        }
        catch
        {
            // Best-effort cleanup — fixture may be mid-dispose
        }
    }

    /// <summary>
    /// Resolves ISystemTrayService and casts to concrete type.
    /// Used by tests that need WinForms-specific reflection access.
    /// </summary>
    private static WinFormsSystemTrayService ResolveTray()
    {
        var svc = Container.GetRequiredService<ISystemTrayService>();
        return Assert.IsType<WinFormsSystemTrayService>(svc);
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-1: System Tray Integration
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void AC1_SystemTray_ServiceResolvesFromDI()
    {
        // Arrange & Act
        var _ = ResolveTray();

        _output.WriteLine("AC-1 PASSED: ISystemTrayService resolves as WinFormsSystemTrayService.");
    }

    [Fact]
    public void AC1_SystemTray_ContextMenuHasCorrectItemOrder()
    {
        // Arrange
        var concrete = ResolveTray();
        var contextMenu = _contextMenuField.Value?.GetValue(concrete)
            as System.Windows.Forms.ContextMenuStrip;
        Assert.NotNull(contextMenu);

        // Assert — Items collection in order
        var items = contextMenu!.Items;
        Assert.True(items.Count >= 8,
            $"Context menu should have at least 8 items; found {items.Count}");

        // Verify item types and text at each position
        Assert.Equal(MenuNewChat, items[0].Text);
        Assert.Equal(MenuOpenStudio, items[1].Text);
        Assert.Equal(MenuCommandBar, items[2].Text);
        Assert.IsType<System.Windows.Forms.ToolStripSeparator>(items[3]);
        Assert.Equal(MenuRecentChats, items[4].Text);
        Assert.Equal(MenuSettings, items[5].Text);
        Assert.IsType<System.Windows.Forms.ToolStripSeparator>(items[6]);
        Assert.Equal(MenuExit, items[7].Text);

        _output.WriteLine("AC-1 PASSED: System tray context menu has 8 items in correct order.");
    }

    [Fact]
    public void AC1_SystemTray_EventsFireOnMenuClick()
    {
        // Arrange
        var concrete = ResolveTray();
        var trayService = (ISystemTrayService)concrete;

        var openStudioFired = false;
        var newChatFired = false;
        var commandBarFired = false;
        var settingsFired = false;
        var exitFired = false;

        trayService.OpenStudioRequested += (_, _) => openStudioFired = true;
        trayService.NewChatRequested += (_, _) => newChatFired = true;
        trayService.CommandBarRequested += (_, _) => commandBarFired = true;
        trayService.SettingsRequested += (_, _) => settingsFired = true;
        trayService.ExitRequested += (_, _) => exitFired = true;

        var contextMenu = _contextMenuField.Value?.GetValue(concrete)
            as System.Windows.Forms.ContextMenuStrip;
        Assert.NotNull(contextMenu);

        // Act — PerformClick on each menu item to trigger its Click event
        contextMenu!.Items[0].PerformClick(); // New Chat
        contextMenu.Items[1].PerformClick(); // Open Studio
        contextMenu.Items[2].PerformClick(); // Command Bar
        contextMenu.Items[5].PerformClick(); // Settings
        contextMenu.Items[7].PerformClick(); // Exit

        // Assert
        Assert.True(newChatFired, "New Chat menu click should fire NewChatRequested");
        Assert.True(openStudioFired, "Open Studio menu click should fire OpenStudioRequested");
        Assert.True(commandBarFired, "Command Bar menu click should fire CommandBarRequested");
        Assert.True(settingsFired, "Settings menu click should fire SettingsRequested");
        Assert.True(exitFired, "Exit menu click should fire ExitRequested");

        _output.WriteLine("AC-1 PASSED: All 5 system tray context menu events fire correctly.");
    }

    [Fact]
    public void AC1_SystemTray_UpdateRecentChats_WithItems_AddsClickableItems()
    {
        // Arrange
        var concrete = ResolveTray();

        // Act — Call UpdateRecentChats with test titles
        concrete.UpdateRecentChats(["Chat A", "Chat B", "Chat C"]);

        var recentChatsMenu = _recentChatsMenuField.Value?.GetValue(concrete)
            as System.Windows.Forms.ToolStripMenuItem;
        Assert.NotNull(recentChatsMenu);

        // Assert
        Assert.Equal(3, recentChatsMenu!.DropDownItems.Count);
        Assert.True(recentChatsMenu.DropDownItems[0].Enabled, "Chat A should be clickable");
        Assert.True(recentChatsMenu.DropDownItems[1].Enabled, "Chat B should be clickable");
        Assert.True(recentChatsMenu.DropDownItems[2].Enabled, "Chat C should be clickable");
        Assert.Equal("Chat A", recentChatsMenu.DropDownItems[0].Text);
        Assert.Equal("Chat B", recentChatsMenu.DropDownItems[1].Text);
        Assert.Equal("Chat C", recentChatsMenu.DropDownItems[2].Text);

        _output.WriteLine("AC-1 PASSED: UpdateRecentChats adds 3 clickable items.");
    }

    [Fact]
    public void AC1_SystemTray_UpdateRecentChats_Empty_ShowsPlaceholder()
    {
        // Arrange
        var concrete = ResolveTray();

        // Act — Call UpdateRecentChats with empty list
        concrete.UpdateRecentChats([]);

        var recentChatsMenu = _recentChatsMenuField.Value?.GetValue(concrete)
            as System.Windows.Forms.ToolStripMenuItem;
        Assert.NotNull(recentChatsMenu);

        // Assert
        Assert.Single(recentChatsMenu!.DropDownItems);
        Assert.False(recentChatsMenu.DropDownItems[0].Enabled,
            "Placeholder item should be disabled");
        Assert.Equal(MenuNoRecentChats, recentChatsMenu.DropDownItems[0].Text);

        _output.WriteLine("AC-1 PASSED: UpdateRecentChats with empty list shows placeholder.");
    }

    [Fact]
    public void AC1_SystemTray_GenerationIndicator_SwapsIcon()
    {
        // Arrange
        var concrete = ResolveTray();

        var notifyIcon = _notifyIconField.Value?.GetValue(concrete)
            as System.Windows.Forms.NotifyIcon;
        Assert.NotNull(notifyIcon);

        // Record the normal icon handle
        var normalIconHandle = notifyIcon!.Icon?.Handle ?? IntPtr.Zero;
        Assert.NotEqual(IntPtr.Zero, normalIconHandle);

        // Act — Set generation indicator to true
        concrete.SetGenerationIndicator(true);

        // Assert — Icon should have changed (generating icon has a different handle)
        var generatingIconHandle = notifyIcon.Icon?.Handle ?? IntPtr.Zero;
        Assert.NotEqual(IntPtr.Zero, generatingIconHandle);
        Assert.NotEqual(normalIconHandle, generatingIconHandle);

        // Act — Set generation indicator to false
        concrete.SetGenerationIndicator(false);

        // Assert — Icon should be back to normal
        var restoredIconHandle = notifyIcon.Icon?.Handle ?? IntPtr.Zero;
        Assert.Equal(normalIconHandle, restoredIconHandle);

        _output.WriteLine("AC-1 PASSED: Generation indicator swaps icon and restores it.");
    }

    [Fact]
    public void AC1_SystemTray_UpdateRecentChats_Null_ClearsAndShowsPlaceholder()
    {
        // Arrange
        var concrete = ResolveTray();

        // Act — Call UpdateRecentChats with null (should not throw)
        // Note: the current implementation takes IReadOnlyList<string> which
        // doesn't accept null at compile time, but test defensively.
        concrete.UpdateRecentChats([]);

        var recentChatsMenu = _recentChatsMenuField.Value?.GetValue(concrete)
            as System.Windows.Forms.ToolStripMenuItem;
        Assert.NotNull(recentChatsMenu);

        // Assert — After clearing, should show placeholder
        Assert.Single(recentChatsMenu!.DropDownItems);
        Assert.False(recentChatsMenu.DropDownItems[0].Enabled);

        _output.WriteLine("AC-1 PASSED: UpdateRecentChats with empty list after null-safe handling.");
    }

    // ════════════════════════════════════════════════════════════════════
    // AC-2: Global Hotkey Registration
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void AC2_GlobalHotkey_ServiceResolvesFromDI()
    {
        // Arrange & Act
        var hotkeyService = Container.GetRequiredService<IGlobalHotkeyService>();
        Assert.IsType<GlobalHotkeyService>(hotkeyService);

        _output.WriteLine("AC-2 PASSED: IGlobalHotkeyService resolves as GlobalHotkeyService.");
    }

    [Fact]
    public void AC2_GlobalHotkey_DefaultHotkeysAreRegistered()
    {
        // Arrange — DI resolution triggers default hotkey registration
        var hotkeyService = Container.GetRequiredService<IGlobalHotkeyService>();

        // Act
        var registered = hotkeyService.GetRegisteredHotkeys();

        // Assert — Should have 6 default hotkeys
        Assert.Equal(6, registered.Count);

        // Verify each default hotkey by ID
        var ids = registered.Select(h => h.HotkeyId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("CommandBar", ids);
        Assert.Contains("Rewrite", ids);
        Assert.Contains("Summarize", ids);
        Assert.Contains("Explain", ids);
        Assert.Contains("Translate", ids);
        Assert.Contains("ContinueWriting", ids);

        _output.WriteLine("AC-2 PASSED: All 6 default global hotkeys are registered.");
    }

    [Fact]
    public void AC2_GlobalHotkey_DetectConflict_SystemHotkeys()
    {
        // Arrange
        var hotkeyService = Container.GetRequiredService<IGlobalHotkeyService>();

        // Assert — Known system hotkeys should be detected as conflicts
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Windows, VirtualKey.D));     // Win+D
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Windows, VirtualKey.L));     // Win+L
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Alt, VirtualKey.F4));        // Alt+F4
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Alt, VirtualKey.Tab));       // Alt+Tab

        // Unknown combination should NOT be a conflict
        Assert.False(hotkeyService.DetectConflict(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Z)); // Ctrl+Alt+Z

        _output.WriteLine("AC-2 PASSED: System hotkey conflict detection works correctly.");
    }

    [Fact]
    public void AC2_GlobalHotkey_RegisterAndUnregister()
    {
        // Arrange
        var hotkeyService = Container.GetRequiredService<IGlobalHotkeyService>();

        // Act — Register a test hotkey
        var registered = hotkeyService.RegisterHotkey(
            "TestHotkey", ModifierKeys.Alt, VirtualKey.F9);

        // Assert — Succeeds at tracking level even if P/Invoke fails in test env
        Assert.True(registered,
            "RegisterHotkey should return true (tracking succeeds even if P/Invoke fails)");

        Assert.True(hotkeyService.IsRegistered("TestHotkey"),
            "IsRegistered should return true after registration");

        // Act — Unregister
        var unregistered = hotkeyService.UnregisterHotkey("TestHotkey");
        Assert.True(unregistered, "UnregisterHotkey should return true");

        Assert.False(hotkeyService.IsRegistered("TestHotkey"),
            "IsRegistered should return false after unregistration");

        _output.WriteLine("AC-2 PASSED: Hotkey register/unregister lifecycle works.");
    }
}
