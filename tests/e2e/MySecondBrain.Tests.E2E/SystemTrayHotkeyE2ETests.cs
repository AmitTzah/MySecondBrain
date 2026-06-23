using System.Reflection;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI;
using MySecondBrain.UI.Services;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 6: System Tray & Global Hotkeys.
/// Verifies context menu structure, menu click events, recent chats submenu,
/// generation indicator icon swapping, default hotkey registration,
/// system hotkey conflict detection, and register/unregister lifecycle.
///
/// NOTE: System tray context menu interaction uses reflection + PerformClick()
/// rather than UIA, because WinForms NotifyIcon context menus are not
/// accessible through the UIA automation tree. This is the pragmatic approach
/// for testing event wiring in a desktop E2E context.
/// </summary>
[Collection("E2E")]
public sealed class SystemTrayHotkeyE2ETests : E2eTestBase
{
    private static readonly Lazy<IServiceProvider> _serviceProvider = new(() =>
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        return services.BuildServiceProvider();
    });

    public SystemTrayHotkeyE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // Preflight: Validate Reflection Field Names
    // ============================================================

    [Fact]
    public async Task SystemTray_ReflectionFields_ShouldExist()
    {
        await UseSharedAppAsync();
        var trayService = ResolveService<ISystemTrayService>();
        Assert.NotNull(trayService);

        var type = trayService!.GetType();
        Assert.NotNull(type.GetField("_contextMenu",
            BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(type.GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(type.GetField("_notifyIcon",
            BindingFlags.NonPublic | BindingFlags.Instance));
        _output.WriteLine("All reflected private fields exist on WinFormsSystemTrayService.");
    }

    // ============================================================
    // System Tray — Context Menu Structure
    // ============================================================

    [Fact]
    public async Task SystemTray_ContextMenu_ShouldHave8ItemsInCorrectOrder()
    {
        await UseSharedAppAsync();
        var trayService = ResolveService<ISystemTrayService>();
        Assert.NotNull(trayService);

        var menuItems = GetContextMenuItems(trayService);
        Assert.NotNull(menuItems);

        Assert.Equal(8, menuItems!.Length);
        _output.WriteLine($"Context menu has {menuItems.Length} items.");

        Assert.Equal("New Chat", menuItems[0].Text);
        Assert.Equal("Open Studio", menuItems[1].Text);
        Assert.Equal("Command Bar", menuItems[2].Text);
        Assert.IsType<ToolStripSeparator>(menuItems[3]);
        Assert.Equal("Recent Chats", menuItems[4].Text);
        Assert.Equal("Settings", menuItems[5].Text);
        Assert.IsType<ToolStripSeparator>(menuItems[6]);
        Assert.Equal("Exit", menuItems[7].Text);
        _output.WriteLine("Context menu structure verified: 8 items in correct order.");
    }

    // ============================================================
    // System Tray — Menu Click Events
    // Uses PerformClick() via reflection as a proxy because WinForms
    // NotifyIcon context menus are not accessible through UIA.
    // ============================================================

    [Fact]
    public async Task SystemTray_MenuClickEvents_ShouldFireCorrectly()
    {
        await UseSharedAppAsync();
        var trayService = ResolveService<ISystemTrayService>();
        Assert.NotNull(trayService);

        var openStudioFired = false;
        var newChatFired = false;
        var commandBarFired = false;
        var settingsFired = false;

        trayService!.OpenStudioRequested += (_, _) => openStudioFired = true;
        trayService.NewChatRequested += (_, _) => newChatFired = true;
        trayService.CommandBarRequested += (_, _) => commandBarFired = true;
        trayService.SettingsRequested += (_, _) => settingsFired = true;

        var menuItems = GetContextMenuItems(trayService);
        Assert.NotNull(menuItems);

        menuItems![1].PerformClick();
        Assert.True(openStudioFired, "OpenStudioRequested should have fired");
        _output.WriteLine("OpenStudioRequested event fired.");

        menuItems[0].PerformClick();
        Assert.True(newChatFired, "NewChatRequested should have fired");
        _output.WriteLine("NewChatRequested event fired.");

        menuItems[2].PerformClick();
        Assert.True(commandBarFired, "CommandBarRequested should have fired");
        _output.WriteLine("CommandBarRequested event fired.");

        menuItems[5].PerformClick();
        Assert.True(settingsFired, "SettingsRequested should have fired");
        _output.WriteLine("SettingsRequested event fired.");

        Assert.Equal("Exit", menuItems[7].Text);
        _output.WriteLine("Exit menu item verified (click not performed to avoid shutdown).");
        _output.WriteLine("All 4 tested menu click events fire correctly.");
    }

    // ============================================================
    // System Tray — Recent Chats Submenu
    // ============================================================

    [Fact]
    public async Task SystemTray_UpdateRecentChats_ShouldPopulateSubmenu()
    {
        await UseSharedAppAsync();
        var trayService = ResolveService<ISystemTrayService>();
        Assert.NotNull(trayService);

        var titles = new[] { "Chat about E2E testing", "Refactoring discussion", "API design review" };
        trayService!.UpdateRecentChats(titles);

        var recentChatsMenu = GetRecentChatsDropdown(trayService);
        Assert.NotNull(recentChatsMenu);

        Assert.Equal(3, recentChatsMenu!.DropDownItems.Count);
        for (int i = 0; i < titles.Length; i++)
        {
            Assert.Equal(titles[i], recentChatsMenu.DropDownItems[i].Text);
            Assert.True(recentChatsMenu.DropDownItems[i].Enabled,
                $"Recent chat '{titles[i]}' should be clickable (enabled)");
        }
        _output.WriteLine("UpdateRecentChats populated 3 clickable submenu items.");
    }

    [Fact]
    public async Task SystemTray_EmptyRecentChats_ShouldShowDisabledPlaceholder()
    {
        await UseSharedAppAsync();
        var trayService = ResolveService<ISystemTrayService>();
        Assert.NotNull(trayService);

        trayService!.UpdateRecentChats(Array.Empty<string>());

        var recentChatsMenu = GetRecentChatsDropdown(trayService);
        Assert.NotNull(recentChatsMenu);

        var items = recentChatsMenu!.DropDownItems;
#pragma warning disable xUnit2013 // ToolStripItemCollection is non-generic IEnumerable; Assert.Single returns object
        Assert.Equal(1, items.Count);
#pragma warning restore xUnit2013
        var placeholder = items[0]!;
        Assert.Equal("No recent chats", placeholder.Text);
        Assert.False(placeholder.Enabled, "Empty placeholder should be disabled");
        _output.WriteLine("Empty recent chats shows disabled placeholder.");
    }

    // ============================================================
    // System Tray — Generation Indicator
    // ============================================================

    [Fact]
    public async Task SystemTray_SetGenerationIndicator_ShouldSwapIcons()
    {
        await UseSharedAppAsync();
        var trayService = ResolveService<ISystemTrayService>();
        Assert.NotNull(trayService);

        var notifyIcon = GetNotifyIcon(trayService);
        Assert.NotNull(notifyIcon);

        var initialHandle = notifyIcon!.Icon?.Handle ?? IntPtr.Zero;
        _output.WriteLine($"Initial icon handle: {initialHandle}");

        // Set generating — icon should change
        trayService!.SetGenerationIndicator(true);
        var generatingHandle = notifyIcon.Icon?.Handle ?? IntPtr.Zero;
        _output.WriteLine($"Generating icon handle: {generatingHandle}");

        // Set not generating — icon should restore to original
        trayService.SetGenerationIndicator(false);
        var restoredHandle = notifyIcon.Icon?.Handle ?? IntPtr.Zero;
        _output.WriteLine($"Restored icon handle: {restoredHandle}");

        Assert.NotEqual(initialHandle, generatingHandle);
        Assert.Equal(initialHandle, restoredHandle);
        _output.WriteLine("SetGenerationIndicator swaps icons correctly and restores on stop.");
    }

    // ============================================================
    // Global Hotkeys — Default Registration
    // ============================================================

    [Fact]
    public async Task Hotkeys_DefaultHotkeys_ShouldBeRegistered()
    {
        await UseSharedAppAsync();
        var hotkeyService = ResolveService<IGlobalHotkeyService>();
        Assert.NotNull(hotkeyService);

        var expected = new (string Id, ModifierKeys Modifiers, VirtualKey Key)[]
        {
            ("CommandBar", ModifierKeys.Alt, VirtualKey.Space),
            ("Rewrite", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.Q),
            ("Summarize", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.W),
            ("Explain", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.E),
            ("Translate", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.R),
            ("ContinueWriting", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.C),
        };

        foreach (var (id, _, _) in expected)
        {
            Assert.True(hotkeyService!.IsRegistered(id),
                $"Default hotkey '{id}' is NOT registered");
        }
        _output.WriteLine($"All {expected.Length} default hotkeys are registered.");
    }

    [Fact]
    public async Task Hotkeys_DefaultHotkeys_ShouldHaveCorrectBindings()
    {
        await UseSharedAppAsync();
        var hotkeyService = ResolveService<IGlobalHotkeyService>();
        Assert.NotNull(hotkeyService);

        var registrations = hotkeyService!.GetRegisteredHotkeys();
        _output.WriteLine($"Total registered hotkeys: {registrations.Count}");

        Assert.True(registrations.Count >= 6,
            $"Expected at least 6 registered hotkeys, found {registrations.Count}");

        var commandBar = registrations.FirstOrDefault(r =>
            r.HotkeyId == "CommandBar");
        Assert.NotNull(commandBar);
        Assert.Equal(ModifierKeys.Alt, commandBar!.Modifiers);
        Assert.Equal(VirtualKey.Space, commandBar.Key);

        var rewrite = registrations.FirstOrDefault(r =>
            r.HotkeyId == "Rewrite");
        Assert.NotNull(rewrite);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, rewrite!.Modifiers);
        Assert.Equal(VirtualKey.Q, rewrite.Key);

        _output.WriteLine("Default hotkey bindings verified.");
    }

    // ============================================================
    // Global Hotkeys — Conflict Detection
    // ============================================================

    [Fact]
    public async Task Hotkeys_ConflictDetection_ShouldDetectSystemHotkeys()
    {
        await UseSharedAppAsync();
        var hotkeyService = ResolveService<IGlobalHotkeyService>();
        Assert.NotNull(hotkeyService);

        Assert.True(hotkeyService!.DetectConflict(ModifierKeys.Windows, VirtualKey.D),
            "Should detect Win+D as system hotkey");
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Windows, VirtualKey.L),
            "Should detect Win+L as system hotkey");
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Alt, VirtualKey.F4),
            "Should detect Alt+F4 as system hotkey");
        Assert.True(hotkeyService.DetectConflict(ModifierKeys.Alt, VirtualKey.Tab),
            "Should detect Alt+Tab as system hotkey");

        _output.WriteLine("System hotkey conflict detection verified: Win+D, Win+L, Alt+F4, Alt+Tab.");
    }

    [Fact]
    public async Task Hotkeys_ConflictDetection_ShouldNotDetectFreeHotkey()
    {
        await UseSharedAppAsync();
        var hotkeyService = ResolveService<IGlobalHotkeyService>();
        Assert.NotNull(hotkeyService);

        var isConflict = hotkeyService!.DetectConflict(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Z);
        Assert.False(isConflict, "Ctrl+Alt+Z should NOT be detected as a conflict");

        _output.WriteLine("Free hotkey (Ctrl+Alt+Z) correctly not detected as conflict.");
    }

    // ============================================================
    // Global Hotkeys — Register/Unregister Lifecycle
    // NOTE: Ctrl+Alt+X must be free on the test machine.
    // The test uses try/finally to guarantee cleanup if registration succeeds.
    // ============================================================

    [Fact]
    public async Task Hotkeys_RegisterUnregister_ShouldWork()
    {
        await UseSharedAppAsync();
        var hotkeyService = ResolveService<IGlobalHotkeyService>();
        Assert.NotNull(hotkeyService);

        const string testId = "E2ETestHotkey";

        try
        {
            // Register
            var registered = hotkeyService!.RegisterHotkey(
                testId, ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.X);
            Assert.True(registered, "Should register test hotkey (Ctrl+Alt+X must not be in use)");
            Assert.True(hotkeyService.IsRegistered(testId), "Hotkey should be registered");
            _output.WriteLine($"Registered test hotkey '{testId}'.");
        }
        finally
        {
            // Unregister — always attempt cleanup
            if (hotkeyService!.IsRegistered(testId))
            {
                var unregistered = hotkeyService.UnregisterHotkey(testId);
                Assert.True(unregistered, "Should unregister test hotkey");
                Assert.False(hotkeyService.IsRegistered(testId),
                    "Hotkey should NOT be registered after unregister");
                _output.WriteLine($"Unregistered test hotkey '{testId}'.");
            }
        }

        // Double-unregister should return false (hotkey already cleaned up)
        var secondUnregister = hotkeyService.UnregisterHotkey(testId);
        Assert.False(secondUnregister, "Double-unregister should return false");
        _output.WriteLine("Register/unregister lifecycle works correctly.");
    }

    // ============================================================
    // Helpers — Reflection-based access to private WinForms fields
    // ============================================================

    private static T? ResolveService<T>() where T : class =>
        _serviceProvider.Value.GetService<T>();

    private static ToolStripItem[]? GetContextMenuItems(ISystemTrayService trayService)
    {
        var field = trayService.GetType().GetField("_contextMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(trayService) is not ContextMenuStrip menu)
            return null;

        var items = new ToolStripItem[menu.Items.Count];
        menu.Items.CopyTo(items, 0);
        return items;
    }

    private static ToolStripMenuItem? GetRecentChatsDropdown(ISystemTrayService trayService)
    {
        var field = trayService.GetType().GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(trayService) as ToolStripMenuItem;
    }

    private static NotifyIcon? GetNotifyIcon(ISystemTrayService trayService)
    {
        var field = trayService.GetType().GetField("_notifyIcon",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(trayService) as NotifyIcon;
    }
}
