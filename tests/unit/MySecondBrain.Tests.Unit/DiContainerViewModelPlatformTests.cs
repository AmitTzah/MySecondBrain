using System.Reflection;
using System.Windows.Forms;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Services.Update;
using MySecondBrain.UI;
using MySecondBrain.UI.Services;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class DiContainerViewModelPlatformTests : IDisposable
{
    private readonly IServiceProvider _provider;

    public DiContainerViewModelPlatformTests()
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        });
    }

    public void Dispose()
    {
        (_provider as IDisposable)?.Dispose();
    }

    [Fact]
    public void CanResolve_AllViewModels()
    {
        Assert.NotNull(_provider.GetRequiredService<MainWindowViewModel>());
        Assert.NotNull(_provider.GetRequiredService<ChatThreadViewModel>());
        Assert.NotNull(_provider.GetRequiredService<SettingsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<WikiBrowserViewModel>());
        Assert.NotNull(_provider.GetRequiredService<UsageDashboardViewModel>());
        Assert.NotNull(_provider.GetRequiredService<MediaLibraryViewModel>());
        Assert.NotNull(_provider.GetRequiredService<GlobalArtifactsBrowserViewModel>());
        Assert.NotNull(_provider.GetRequiredService<Tier1OverlayViewModel>());
        Assert.NotNull(_provider.GetRequiredService<Tier2CommandBarViewModel>());
        Assert.NotNull(_provider.GetRequiredService<ModelComparisonViewModel>());
        Assert.NotNull(_provider.GetRequiredService<OnboardingWizardViewModel>());
    }

    [Fact]
    public void CanResolve_AllProviders()
    {
        // ILLMProvider: 4 implementations
        var llmProviders = _provider.GetServices<ILLMProvider>().ToList();
        Assert.Equal(4, llmProviders.Count);

        // ISTTProvider: 3 implementations
        var sttProviders = _provider.GetServices<ISTTProvider>().ToList();
        Assert.Equal(3, sttProviders.Count);

        // IBackupProvider: 2 implementations
        var backupProviders = _provider.GetServices<IBackupProvider>().ToList();
        Assert.Equal(2, backupProviders.Count);

        // ISearchProvider: 2 implementations
        var searchProviders = _provider.GetServices<ISearchProvider>().ToList();
        Assert.Equal(2, searchProviders.Count);

        // ITokenizer: 3 implementations
        var tokenizers = _provider.GetServices<ITokenizer>().ToList();
        Assert.Equal(3, tokenizers.Count);

        // IChatImporter: 2 implementations
        var importers = _provider.GetServices<IChatImporter>().ToList();
        Assert.Equal(2, importers.Count);

        // IToolExecutor: 10 implementations
        var toolExecutors = _provider.GetServices<IToolExecutor>().ToList();
        Assert.Equal(10, toolExecutors.Count);

        // IUpdateChecker: 2 implementations
        var updateCheckers = _provider.GetServices<IUpdateChecker>().ToList();
        Assert.Equal(2, updateCheckers.Count);

        // Verify AutoUpdaterDotNet.CurrentVersion reads from assembly (non-zero)
        var autoUpdater = updateCheckers.OfType<AutoUpdaterDotNet>().FirstOrDefault();
        Assert.NotNull(autoUpdater);
        Assert.NotNull(autoUpdater.CurrentVersion);
        Assert.NotEqual(new Version(0, 0, 0), autoUpdater.CurrentVersion);

        // IContentBlockRenderer: 7 implementations
        var renderers = _provider.GetServices<IContentBlockRenderer>().ToList();
        Assert.Equal(7, renderers.Count);
    }

    [Fact]
    public void ContentRendererRegistry_ResolvesAllSevenRenderersInCorrectPriorityOrder()
    {
        // Arrange
        var registry = _provider.GetRequiredService<IContentRendererRegistry>();

        // Act
        var renderers = registry.GetRenderers();

        // Assert
        Assert.Equal(7, renderers.Count);
        Assert.Equal("MarkdownText", renderers[0].RendererName);
        Assert.Equal(100, renderers[0].Priority);
        Assert.Equal("CodeBlock", renderers[1].RendererName);
        Assert.Equal(200, renderers[1].Priority);
        Assert.Equal("ArtifactReference", renderers[2].RendererName);
        Assert.Equal(300, renderers[2].Priority);
        Assert.Equal("Image", renderers[3].RendererName);
        Assert.Equal(400, renderers[3].Priority);
        Assert.Equal("Media", renderers[4].RendererName);
        Assert.Equal(500, renderers[4].Priority);
        Assert.Equal("Thinking", renderers[5].RendererName);
        Assert.Equal(600, renderers[5].Priority);
        Assert.Equal("ToolCall", renderers[6].RendererName);
        Assert.Equal(700, renderers[6].Priority);
    }

    [StaFact]
    public void CanResolve_MainWindow()
    {
        var mainWindow = _provider.GetRequiredService<MainWindow>();
        Assert.NotNull(mainWindow);
        Assert.IsType<MainWindowViewModel>(mainWindow.DataContext);
    }

    [Fact]
    public void CanResolve_AppDbContext()
    {
        var dbContext = _provider.GetRequiredService<AppDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void CanResolve_Logger()
    {
        var logger = _provider.GetRequiredService<ILogger<DiContainerViewModelPlatformTests>>();
        Assert.NotNull(logger);
    }

    [Fact]
    public void CanResolve_KestrelWebSocketServer()
    {
        var server = _provider.GetRequiredService<ILocalWebSocketServer>();
        Assert.NotNull(server);
        Assert.IsType<KestrelWebSocketServer>(server);

        // Verify singleton lifetime
        var first = _provider.GetRequiredService<ILocalWebSocketServer>();
        var second = _provider.GetRequiredService<ILocalWebSocketServer>();
        Assert.Same(first, second);
    }

    [Fact]
    public void KestrelWebSocketServer_AuthToken_IsNonEmptyHexString()
    {
        var server = (KestrelWebSocketServer)_provider.GetRequiredService<ILocalWebSocketServer>();

        var token = server.AuthToken;

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Equal(64, token.Length); // 32 bytes = 64 hex chars
        Assert.Matches("^[0-9A-F]{64}$", token); // uppercase hex, no dashes
    }

    [Fact]
    public void KestrelWebSocketServer_RegenerateAuthToken_ReturnsNewToken()
    {
        var server = (KestrelWebSocketServer)_provider.GetRequiredService<ILocalWebSocketServer>();

        var original = server.AuthToken;
        var regenerated = server.RegenerateAuthToken();

        Assert.NotEqual(original, regenerated);
        Assert.Equal(64, regenerated.Length);
        Assert.Equal(regenerated, server.AuthToken); // property updated
    }

    [Fact]
    public void KestrelWebSocketServer_AuthToken_SameAcrossSingletonResolves()
    {
        var first = (KestrelWebSocketServer)_provider.GetRequiredService<ILocalWebSocketServer>();
        var second = (KestrelWebSocketServer)_provider.GetRequiredService<ILocalWebSocketServer>();

        Assert.Equal(first.AuthToken, second.AuthToken);
    }

    [Fact]
    public void SystemTray_ContextMenuHasCorrectItemOrder()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var contextMenuField = typeof(WinFormsSystemTrayService).GetField("_contextMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var contextMenu = (ContextMenuStrip)contextMenuField.GetValue(service)!;
        var items = contextMenu.Items;

        Assert.Equal(8, items.Count);

        // Index 0: New Chat
        var item0 = Assert.IsType<ToolStripMenuItem>(items[0]);
        Assert.Equal("New Chat", item0.Text);

        // Index 1: Open Studio
        var item1 = Assert.IsType<ToolStripMenuItem>(items[1]);
        Assert.Equal("Open Studio", item1.Text);

        // Index 2: Command Bar
        var item2 = Assert.IsType<ToolStripMenuItem>(items[2]);
        Assert.Equal("Command Bar", item2.Text);

        // Index 3: Separator
        Assert.IsType<ToolStripSeparator>(items[3]);

        // Index 4: Recent Chats (submenu)
        var item4 = Assert.IsType<ToolStripMenuItem>(items[4]);
        Assert.Equal("Recent Chats", item4.Text);
        Assert.NotNull(item4.DropDownItems);

        // Index 5: Settings
        var item5 = Assert.IsType<ToolStripMenuItem>(items[5]);
        Assert.Equal("Settings", item5.Text);

        // Index 6: Separator
        Assert.IsType<ToolStripSeparator>(items[6]);

        // Index 7: Exit
        var item7 = Assert.IsType<ToolStripMenuItem>(items[7]);
        Assert.Equal("Exit", item7.Text);
    }

    [Fact]
    public void SystemTray_UpdateRecentChats_WithItems_AddsClickableItems()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var recentChatsField = typeof(WinFormsSystemTrayService).GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var recentChats = (ToolStripMenuItem)recentChatsField.GetValue(service)!;

        service.UpdateRecentChats(["Chat A", "Chat B"]);

        Assert.Equal(2, recentChats.DropDownItems.Count);

        var item0 = Assert.IsType<ToolStripMenuItem>(recentChats.DropDownItems[0]);
        Assert.Equal("Chat A", item0.Text);
        Assert.True(item0.Enabled, $"Expected 'Chat A' to be enabled");

        var item1 = Assert.IsType<ToolStripMenuItem>(recentChats.DropDownItems[1]);
        Assert.Equal("Chat B", item1.Text);
        Assert.True(item1.Enabled, $"Expected 'Chat B' to be enabled");
    }

    [Fact]
    public void SystemTray_UpdateRecentChats_TruncatesToMaxFive()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var recentChatsField = typeof(WinFormsSystemTrayService).GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var recentChats = (ToolStripMenuItem)recentChatsField.GetValue(service)!;

        var titles = new[] { "A", "B", "C", "D", "E", "F", "G" };
        service.UpdateRecentChats(titles);

        Assert.Equal(5, recentChats.DropDownItems.Count);
        for (var i = 0; i < 5; i++)
        {
            var item = Assert.IsType<ToolStripMenuItem>(recentChats.DropDownItems[i]);
            Assert.Equal(titles[i], item.Text);
            Assert.True(item.Enabled, $"Expected item '{titles[i]}' to be enabled");
        }
    }

    [Fact]
    public void SystemTray_UpdateRecentChats_Empty_ClearsSubmenu()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var recentChatsField = typeof(WinFormsSystemTrayService).GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var recentChats = (ToolStripMenuItem)recentChatsField.GetValue(service)!;

        service.UpdateRecentChats([]);

        var single = Assert.Single(recentChats.DropDownItems);

        var placeholder = Assert.IsType<ToolStripMenuItem>(single);
        Assert.Equal("No recent chats", placeholder.Text);
        Assert.False(placeholder.Enabled);
    }

    [Fact]
    public void SystemTray_Events_FireOnMenuClick()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var contextMenuField = typeof(WinFormsSystemTrayService).GetField("_contextMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var contextMenu = (ContextMenuStrip)contextMenuField.GetValue(service)!;
        var items = contextMenu.Items;

        var newChatFired = false;
        var openStudioFired = false;
        var commandBarFired = false;
        var settingsFired = false;
        var exitFired = false;

        service.NewChatRequested += (_, _) => newChatFired = true;
        service.OpenStudioRequested += (_, _) => openStudioFired = true;
        service.CommandBarRequested += (_, _) => commandBarFired = true;
        service.SettingsRequested += (_, _) => settingsFired = true;
        service.ExitRequested += (_, _) => exitFired = true;

        // New Chat (index 0) — only NewChatRequested fires
        newChatFired = openStudioFired = commandBarFired = settingsFired = exitFired = false;
        ((ToolStripMenuItem)items[0]).PerformClick();
        Assert.True(newChatFired, "NewChatRequested should fire on 'New Chat' click");
        Assert.False(openStudioFired, "OpenStudioRequested should not fire on 'New Chat' click");
        Assert.False(commandBarFired, "CommandBarRequested should not fire on 'New Chat' click");
        Assert.False(settingsFired, "SettingsRequested should not fire on 'New Chat' click");
        Assert.False(exitFired, "ExitRequested should not fire on 'New Chat' click");

        // Open Studio (index 1) — only OpenStudioRequested fires
        newChatFired = openStudioFired = commandBarFired = settingsFired = exitFired = false;
        ((ToolStripMenuItem)items[1]).PerformClick();
        Assert.True(openStudioFired, "OpenStudioRequested should fire on 'Open Studio' click");
        Assert.False(newChatFired, "NewChatRequested should not fire on 'Open Studio' click");
        Assert.False(commandBarFired, "CommandBarRequested should not fire on 'Open Studio' click");
        Assert.False(settingsFired, "SettingsRequested should not fire on 'Open Studio' click");
        Assert.False(exitFired, "ExitRequested should not fire on 'Open Studio' click");

        // Command Bar (index 2) — only CommandBarRequested fires
        newChatFired = openStudioFired = commandBarFired = settingsFired = exitFired = false;
        ((ToolStripMenuItem)items[2]).PerformClick();
        Assert.True(commandBarFired, "CommandBarRequested should fire on 'Command Bar' click");
        Assert.False(newChatFired, "NewChatRequested should not fire on 'Command Bar' click");
        Assert.False(openStudioFired, "OpenStudioRequested should not fire on 'Command Bar' click");
        Assert.False(settingsFired, "SettingsRequested should not fire on 'Command Bar' click");
        Assert.False(exitFired, "ExitRequested should not fire on 'Command Bar' click");

        // Settings (index 5) — only SettingsRequested fires
        newChatFired = openStudioFired = commandBarFired = settingsFired = exitFired = false;
        ((ToolStripMenuItem)items[5]).PerformClick();
        Assert.True(settingsFired, "SettingsRequested should fire on 'Settings' click");
        Assert.False(newChatFired, "NewChatRequested should not fire on 'Settings' click");
        Assert.False(openStudioFired, "OpenStudioRequested should not fire on 'Settings' click");
        Assert.False(commandBarFired, "CommandBarRequested should not fire on 'Settings' click");
        Assert.False(exitFired, "ExitRequested should not fire on 'Settings' click");

        // Exit (index 7) — only ExitRequested fires
        newChatFired = openStudioFired = commandBarFired = settingsFired = exitFired = false;
        ((ToolStripMenuItem)items[7]).PerformClick();
        Assert.True(exitFired, "ExitRequested should fire on 'Exit' click");
        Assert.False(newChatFired, "NewChatRequested should not fire on 'Exit' click");
        Assert.False(openStudioFired, "OpenStudioRequested should not fire on 'Exit' click");
        Assert.False(commandBarFired, "CommandBarRequested should not fire on 'Exit' click");
        Assert.False(settingsFired, "SettingsRequested should not fire on 'Exit' click");
    }

    [Fact]
    public void SystemTray_SetGenerationIndicator_DoesNotThrow()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var notifyIconField = typeof(WinFormsSystemTrayService).GetField("_notifyIcon",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var normalIconField = typeof(WinFormsSystemTrayService).GetField("_normalIcon",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var generatingIconField = typeof(WinFormsSystemTrayService).GetField("_generatingIcon",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var normalIcon = (Icon)normalIconField.GetValue(service)!;
        var notifyIcon = (NotifyIcon)notifyIconField.GetValue(service)!;

        // Before SetGenerationIndicator, Icon should be the normal icon
        Assert.Same(normalIcon, notifyIcon.Icon);

        // Act: switch to generating icon
        var exception = Record.Exception(() => service.SetGenerationIndicator(true));
        Assert.Null(exception);
        Assert.False(service.IsVisible); // unchanged

        // Verify icon was swapped to generating icon
        var generatingIcon = (Icon?)generatingIconField.GetValue(service);
        Assert.NotNull(generatingIcon);
        Assert.Same(generatingIcon, notifyIcon.Icon);

        // Act: switch back to normal icon
        exception = Record.Exception(() => service.SetGenerationIndicator(false));
        Assert.Null(exception);
        Assert.False(service.IsVisible); // unchanged

        // Verify icon restored to normal
        Assert.Same(normalIcon, notifyIcon.Icon);
    }

    [Fact]
    public void SystemTray_GenerationIndicator_ProducesGreenDotIcon()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var method = typeof(WinFormsSystemTrayService).GetMethod("BuildGeneratingIcon",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        // Get the normal icon via reflection to pass as source
        var normalIconField = typeof(WinFormsSystemTrayService).GetField("_normalIcon",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var normalIcon = (Icon)normalIconField.GetValue(service)!;

        var generatingIcon = (Icon)method.Invoke(null, [normalIcon])!;

        Assert.NotNull(generatingIcon);
        Assert.NotSame(normalIcon, generatingIcon);
        Assert.Equal(normalIcon.Width, generatingIcon.Width);
        Assert.Equal(normalIcon.Height, generatingIcon.Height);

        // Verify the generated icon has a green pixel at the dot center.
        // Green dot: FilledEllipse at (Width-dotSize-margin, Height-dotSize-margin, dotSize, dotSize)
        // with dotSize=5, margin=1 → bounding rect (10,10)-(14,14) for a 16x16 icon.
        // Center of dot is at (12, 12).
        using var bitmap = generatingIcon.ToBitmap();
        var dotCenter = bitmap.GetPixel(12, 12);
        Assert.True(dotCenter.G > 200,
            $"Expected green channel > 200 at dot center (12,12), got G={dotCenter.G}");
        Assert.True(dotCenter.G > dotCenter.R && dotCenter.G > dotCenter.B,
            $"Expected green to dominate at dot center, got R={dotCenter.R} G={dotCenter.G} B={dotCenter.B}");
    }

    [StaFact]
    public void SystemTray_IsVisible_TracksShowHide()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();

        Assert.False(service.IsVisible);

        service.Show();
        Assert.True(service.IsVisible);

        service.Hide();
        Assert.False(service.IsVisible);

        service.Show();
        Assert.True(service.IsVisible);
    }

    [StaFact]
    public void SystemTray_ExitRequested_DisposesAndClearsEvents()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var contextMenuField = typeof(WinFormsSystemTrayService).GetField("_contextMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var contextMenu = (ContextMenuStrip)contextMenuField.GetValue(service)!;
        var exitItem = (ToolStripMenuItem)contextMenu.Items[7];

        // Subscribe to ExitRequested and fire it
        var exitFired = false;
        service.ExitRequested += (_, _) => exitFired = true;
        exitItem.PerformClick();
        Assert.True(exitFired, "ExitRequested should fire on Exit click");

        // Dispose — verify cleanup
        service.Dispose();
        Assert.False(service.IsVisible, "IsVisible should be false after Dispose");

        // Verify all event backing fields are nulled after Dispose
        var eventNames = new[] { "OpenStudioRequested", "NewChatRequested", "CommandBarRequested",
            "SettingsRequested", "ExitRequested" };
        foreach (var evt in eventNames)
        {
            var field = typeof(WinFormsSystemTrayService).GetField(evt,
                BindingFlags.NonPublic | BindingFlags.Instance);
            var delegateValue = (Delegate?)field!.GetValue(service);
            Assert.Null(delegateValue);
        }
    }

    [StaFact]
    public void SystemTray_UpdateRecentChats_SequentialUpdates_NoDuplicateEntries()
    {
        var service = (WinFormsSystemTrayService)_provider.GetRequiredService<ISystemTrayService>();
        var recentChatsField = typeof(WinFormsSystemTrayService).GetField("_recentChatsMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var recentChats = (ToolStripMenuItem)recentChatsField.GetValue(service)!;

        // Show must be called before UpdateRecentChats works in real flow
        service.Show();

        service.UpdateRecentChats(["Test A"]);
        service.UpdateRecentChats(["Test B"]);

        // After two sequential updates, only the last set should remain (no double entries)
        var item = Assert.IsType<ToolStripMenuItem>(Assert.Single(recentChats.DropDownItems));
        Assert.Equal("Test B", item.Text);
    }

    [StaFact]
    public void GlobalHotkeyService_CanResolve_AsSingleton()
    {
        var first = _provider.GetRequiredService<IGlobalHotkeyService>();
        var second = _provider.GetRequiredService<IGlobalHotkeyService>();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [StaFact]
    public void GlobalHotkeyService_IsRegistered_TrueAfterRegister()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        var registered = service.RegisterHotkey("TestHotkey", ModifierKeys.Alt, VirtualKey.A);
        Assert.True(registered, "RegisterHotkey should return true");

        Assert.True(service.IsRegistered("TestHotkey"), "IsRegistered should be true after Register");

        var unregistered = service.UnregisterHotkey("TestHotkey");
        Assert.True(unregistered, "UnregisterHotkey should return true");

        Assert.False(service.IsRegistered("TestHotkey"), "IsRegistered should be false after Unregister");
    }

    [StaFact]
    public void GlobalHotkeyService_GetRegisteredHotkeys_ReturnsAll()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        // Register two additional hotkeys (default ones exist already)
        service.RegisterHotkey("Test_A", ModifierKeys.Alt, VirtualKey.A);
        service.RegisterHotkey("Test_B", ModifierKeys.Control, VirtualKey.B);

        var all = service.GetRegisteredHotkeys();

        // At least 8: 6 defaults + 2 we just registered
        Assert.Equal(8, all.Count);

        // Cleanup
        service.UnregisterHotkey("Test_A");
        service.UnregisterHotkey("Test_B");
    }

    [StaFact]
    public void GlobalHotkeyService_DetectConflict_SameKeyReturnsTrue()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        service.RegisterHotkey("ConflictTest", ModifierKeys.Alt, VirtualKey.F);
        Assert.True(service.DetectConflict(ModifierKeys.Alt, VirtualKey.F),
            "DetectConflict should return true for registered hotkey");

        service.UnregisterHotkey("ConflictTest");
    }

    [StaFact]
    public void GlobalHotkeyService_DetectConflict_DifferentKeyReturnsFalse()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        // Unusual modifier+key combos not registered by default and not system hotkeys
        Assert.False(service.DetectConflict(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.F1),
            "Ctrl+Shift+F1 should not conflict");
        Assert.False(service.DetectConflict(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.F2),
            "Ctrl+Alt+F2 should not conflict");
    }

    [StaFact]
    public void GlobalHotkeyService_UnregisterHotkey_NonexistentIdReturnsFalse()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        var result = service.UnregisterHotkey("NoSuchHotkey_XYZ");
        Assert.False(result);
    }

    [StaFact]
    public void GlobalHotkeyService_Dispose_UnregistersAll()
    {
        var service = (GlobalHotkeyService)_provider.GetRequiredService<IGlobalHotkeyService>();

        service.RegisterHotkey("DisposeTest_A", ModifierKeys.Alt, VirtualKey.G);
        service.RegisterHotkey("DisposeTest_B", ModifierKeys.Control, VirtualKey.H);

        var beforeCount = service.GetRegisteredHotkeys().Count;
        Assert.True(beforeCount >= 2, "Should have at least 2 registered hotkeys before dispose");

        service.Dispose();

        var afterCount = service.GetRegisteredHotkeys().Count;
        Assert.Equal(0, afterCount);
    }

    [StaFact]
    public void GlobalHotkeyService_DetectConflict_KnownSystemHotkeys()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        Assert.True(service.DetectConflict(ModifierKeys.Windows, VirtualKey.D),   "Win+D");
        Assert.True(service.DetectConflict(ModifierKeys.Windows, VirtualKey.L),   "Win+L");
        Assert.True(service.DetectConflict(ModifierKeys.Windows, VirtualKey.R),   "Win+R");
        Assert.True(service.DetectConflict(ModifierKeys.Alt, VirtualKey.Tab),      "Alt+Tab");
        Assert.True(service.DetectConflict(ModifierKeys.Alt, VirtualKey.F4),       "Alt+F4");
        Assert.True(service.DetectConflict(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Delete), "Ctrl+Alt+Del");
        Assert.True(service.DetectConflict(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.Escape), "Ctrl+Shift+Esc");
    }

    [StaFact]
    public void GlobalHotkeyService_RegisterHotkey_SucceedsAndTracksCorrectly()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        // RegisterHotKey P/Invoke will fail in test (no HwndSource on MTA) → falls to WH_KEYBOARD_LL
        var result = service.RegisterHotkey("FallbackTest", ModifierKeys.Alt, VirtualKey.Z);
        Assert.True(result, "RegisterHotkey should return true even with fallback");

        Assert.True(service.IsRegistered("FallbackTest"),
            "Hotkey should be registered (even if via fallback)");

        var unregistered = service.UnregisterHotkey("FallbackTest");
        Assert.True(unregistered, "UnregisterHotkey should return true for fallback-registered hotkey");

        Assert.False(service.IsRegistered("FallbackTest"),
            "IsRegistered should be false after unregister");
    }

    [StaFact]
    public void GlobalHotkeyService_WH_KEYBOARD_LL_Hook_Created()
    {
        var service = (GlobalHotkeyService)_provider.GetRequiredService<IGlobalHotkeyService>();

        // Verify the WH_KEYBOARD_LL hook was installed (hook handle non-zero)
        var hookIdField = typeof(GlobalHotkeyService).GetField("_hookId",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var hookId = (IntPtr)hookIdField.GetValue(service)!;
        Assert.NotEqual(IntPtr.Zero, hookId);

        // Verify the service is functional
        Assert.True(service.RegisterHotkey("HookTest", ModifierKeys.Alt, VirtualKey.F1),
            "Should register hotkey (any available mechanism)");

        Assert.True(service.IsRegistered("HookTest"),
            "Hotkey should be registered");

        Assert.True(service.UnregisterHotkey("HookTest"),
            "Should unregister hotkey");
    }

    [StaFact]
    public void GlobalHotkeyService_RegisterHotkey_DuplicateIdReturnsFalse()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        Assert.True(service.RegisterHotkey("DupTest", ModifierKeys.Alt, VirtualKey.Z),
            "First registration should succeed");

        // Second registration with same ID should return false
        Assert.False(service.RegisterHotkey("DupTest", ModifierKeys.Control, VirtualKey.Y),
            "Duplicate registration should return false");

        // Unregister once
        Assert.True(service.UnregisterHotkey("DupTest"),
            "Unregister should succeed");
    }

    [StaFact]
    public void GlobalHotkeyService_RegisterHotkey_AfterDisposeReturnsFalse()
    {
        var service = (GlobalHotkeyService)_provider.GetRequiredService<IGlobalHotkeyService>();

        service.Dispose();

        Assert.False(service.RegisterHotkey("PostDispose", ModifierKeys.Alt, VirtualKey.X),
            "RegisterHotkey should return false after Dispose");
    }

    [StaFact]
    public void GlobalHotkeyService_DetectConflict_SystemHotkeyStillTrueWhenAlsoRegistered()
    {
        var service = _provider.GetRequiredService<IGlobalHotkeyService>();

        // Alt+Tab is a known system hotkey AND the service may have registered it
        Assert.True(service.DetectConflict(ModifierKeys.Alt, VirtualKey.Tab),
            "Alt+Tab should always be conflicted (system hotkey)");

        Assert.True(service.DetectConflict(ModifierKeys.Windows, VirtualKey.D),
            "Win+D should always be conflicted (system hotkey)");
    }

    [StaFact]
    public void GlobalHotkeyService_HotkeyTriggered_NulledAfterDispose()
    {
        var service = (GlobalHotkeyService)_provider.GetRequiredService<IGlobalHotkeyService>();

        // Subscribe a handler so the backing field is non-null
        service.HotkeyTriggered += (_, _) => { };

        var hotkeyEventField = typeof(GlobalHotkeyService).GetField("HotkeyTriggered",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var beforeValue = (Delegate?)hotkeyEventField.GetValue(service);
        Assert.NotNull(beforeValue); // Should be non-null after subscribing

        service.Dispose();

        var afterValue = (Delegate?)hotkeyEventField.GetValue(service);
        Assert.Null(afterValue); // Dispose should null the backing field
    }
}
