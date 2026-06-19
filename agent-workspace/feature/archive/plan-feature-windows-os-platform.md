# Feature Implementation Plan: Windows OS Platform Infrastructure

## 1. Overall Project Context

MySecondBrain is a native Windows 10/11 desktop application built on .NET 8.0 WPF — a unified, provider-agnostic AI chat hub replacing all LLM chat platforms, paired with a personal wiki for turning conversations into lasting knowledge. The tech stack uses CommunityToolkit.Mvvm for MVVM, Microsoft.Extensions.DependencyInjection for DI, Serilog for logging, SQLite+EF Core+FTS5 for data, and Markdig for Markdown rendering. The solution has 7 projects across a strict Core ← Data ← Services ← UI dependency chain, with 76+ DI registrations, 42+ interfaces, 14 entities, 8 repositories, and 11 ViewModels — all following the stub pattern for parallelizable feature development. Features 1-5 are complete: solution scaffold, DI container, logging, data layer, and app shell with theming.

## 2. Feature-Specific Context

Feature 6 is the last Wave 2 skeleton feature — it fills in the Windows OS platform infrastructure that enables the three-tier interaction model (Tier 1 hotkey overlays, Tier 2 Command Bar, Tier 3 Studio). This feature implements the concrete platform services behind the existing stub interfaces: [`ISystemTrayService`](src/MySecondBrain.Core/Interfaces/ISystemTrayService.cs:5), [`IGlobalHotkeyService`](src/MySecondBrain.Core/Interfaces/IGlobalHotkeyService.cs:7), [`ILocalWebSocketServer`](src/MySecondBrain.Core/Interfaces/ILocalWebSocketServer.cs:5), and [`IUpdateChecker`](src/MySecondBrain.Core/Interfaces/IUpdateChecker.cs:5). These stubs are already registered in DI in [`App.xaml.cs`](src/MySecondBrain.UI/App.xaml.cs:152-197) alongside the fully implemented services from Features 1-5.

This feature also verifies the PerMonitorV2 DPI configuration (already set via [`MySecondBrain.UI.csproj`](src/MySecondBrain.UI/MySecondBrain.UI.csproj:10) `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`) and validates the MSIX packaging project builds correctly with required capabilities (`internetClient`, `runFullTrust`, `localSystemServices` already in [`Package.appxmanifest`](src/MySecondBrain.Package/Package.appxmanifest:15-19)).

**Important scope note:** This is a Wave 2 skeleton feature. The system tray context menu items (New Chat, Open Studio, Command Bar, Settings) fire events that will be wired up later. The "Recent Chats" submenu will display placeholder items until Feature 9 (Studio Chat) provides real chat data. The Tier 1/Tier 2 hotkey actions (Alt+Q/W/E/R) are reserved but will be fully implemented in Feature 13 (Text Actions & Three-Tier). The WebSocket server starts and accepts connections but the Word Add-in integration protocol is defined in Feature 13. The auto-update framework checks against a feed URL but the actual feed and MSIX packages won't exist until the first release.

## 3. Architecture and Extensibility

**Provider/Adapter Pattern (reused):** Each platform service follows the established pattern: interface in `Core/Interfaces/` (already exists), implementation in `UI/Services/` (platform-specific code stays out of the portable `Services/` project), registered as Singleton in DI. This keeps all Windows-specific P/Invoke, WinForms interop, and Kestrel dependencies in the UI project where they belong.

**GlobalHotkeyService — Two-Tier Strategy:** The primary mechanism is `RegisterHotKey` (kernel-level, reliable, less AV suspicion). The fallback is `WH_KEYBOARD_LL` (low-level hook) for key combinations `RegisterHotKey` cannot register. Both feed into the same `HotkeyTriggered` event, so consumers don't know which mechanism fired. This is the pattern validated by PowerToys at scale.

**SystemTrayService — WinForms Interop:** WPF has no native tray icon. `System.Windows.Forms.NotifyIcon` provides this via `UseWindowsForms=true` (already in .csproj). The service is self-contained — it manages its own `ContextMenuStrip` and fires events that the app shell handles.

**KestrelWebSocketServer — Embedded ASP.NET Core:** Kestrel runs in-process on `127.0.0.1` with no external network exposure. It uses the ASP.NET Core middleware pipeline for WebSocket upgrade and token validation. The auth token is auto-generated on first run, stored via `ISettingsRepository`, and displayable/regeneratable in Settings (Feature 8 will build the UI).

**AutoUpdaterDotNet — Feed-Driven:** AutoUpdater.NET checks a remote XML/JSON feed, compares versions, and downloads MSIX installers. The feed URL is configurable. The `IUpdateChecker` interface supports two implementations: `AutoUpdaterDotNet` (side-loaded) and `MsixAppInstallerUpdater` (MSIX-packaged). Both are already registered in DI.

**DPI — Platform-Configured, Not Feature-Built:** PerMonitorV2 is a platform setting, not a code feature. It is already set via `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in the .csproj. This feature verifies it works correctly across scaling levels.

## 4. Final Expected Project Structure

```
src/
  MySecondBrain.UI/
    Services/
      [MODIFIED] WinFormsSystemTrayService.cs    — Filled from stub: NotifyIcon, context menu, minimize-to-tray, generation indicator
      [MODIFIED] GlobalHotkeyService.cs          — Filled from stub: RegisterHotKey + WH_KEYBOARD_LL, HwndSource hook, conflict detection
      [MODIFIED] KestrelWebSocketServer.cs       — Filled from stub: Kestrel startup, token auth, JSON message protocol
    [MODIFIED] App.xaml.cs                       — Wire system tray startup, hotkey registration, WebSocket server start, auto-update check
    [MODIFIED] MainWindow.xaml.cs                — Wire minimize-to-tray close behavior, system tray event handlers
    [MODIFIED] MySecondBrain.UI.csproj           — Add ASP.NET Core Kestrel + WebSocket NuGet packages
  MySecondBrain.Services/
    Update/
      [MODIFIED] AutoUpdaterDotNet.cs            — Filled from stub: update feed check, version comparison, download
  MySecondBrain.Package/
    [NEW] MySecondBrain.appinstaller             — App Installer template for auto-update
```

---

## 5. Execution Steps

### [x] Step 1: Add ASP.NET Core Kestrel + WebSocket NuGet packages to UI project
- **Goal:** Add required NuGet packages for embedded Kestrel WebSocket server to [`MySecondBrain.UI.csproj`](src/MySecondBrain.UI/MySecondBrain.UI.csproj:17-32).
- **Actions:**
  - Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to the UI .csproj. This brings in Kestrel, WebSocket middleware, and the ASP.NET Core hosting primitives needed for the embedded server.
- **Automated Testing:** Run `dotnet build src/MySecondBrain.UI/MySecondBrain.UI.csproj` — must succeed with no errors. Verify the build output includes `Microsoft.AspNetCore.Server.Kestrel.dll`.
- **Live Smoke Test (Mandatory):** Open terminal, run `dotnet build src/MySecondBrain.UI/MySecondBrain.UI.csproj`. Verify build succeeds with zero errors. Run `dotnet test tests/unit/MySecondBrain.Tests.Unit/` — verify all existing 114 unit tests still pass.
- **Suggested Commit Message:** `feat: add ASP.NET Core Kestrel framework reference for embedded WebSocket server`

### [x] Step 2: Fill KestrelWebSocketServer stub — server startup with auto-port selection
- **Goal:** Replace the stub in [`KestrelWebSocketServer.cs`](src/MySecondBrain.UI/Services/KestrelWebSocketServer.cs:5) with a working Kestrel server that starts on `127.0.0.1` with auto-port selection.
- **Actions:**
  - Implement `StartAsync(int? preferredPort, CancellationToken ct)`: Build a `WebApplication` with Kestrel listening on `127.0.0.1:{port}`. If no port specified, bind to port 0 (OS auto-assigns). Store the actual port.
  - Implement `StopAsync(CancellationToken ct)`: Gracefully stop the server.
  - Implement `IsRunning` property and `Port` property.
  - Register `IHostedService`-style lifecycle: server starts in `StartAsync` called from `App.xaml.cs` OnStartup, stops in `OnExit`.
  - Log startup port via `ILogger<KestrelWebSocketServer>`.
- **Automated Testing:** Run all existing unit tests to verify DI resolution still works. Add a DI resolution test verifying `ILocalWebSocketServer` resolves from the container.
- **Live Smoke Test (Mandatory):** Launch the app. Check the log file at `%LOCALAPPDATA%\MySecondBrain\logs\msb-{date}.log` — verify a log entry contains "WebSocket server started on port" with a port number. Open PowerShell and run `netstat -ano | findstr :{port}` — verify the port is in LISTENING state on `127.0.0.1`. Close the app, run `netstat -ano | findstr :{port}` again — verify the port is no longer listening.
- **Suggested Commit Message:** `feat: implement KestrelWebSocketServer startup with auto-port selection on 127.0.0.1`

### [x] Step 3: Fill KestrelWebSocketServer — token-based authentication
- **Goal:** Add WebSocket endpoint with token-based authentication. Auto-generate token on first run, store via `ISettingsRepository`, validate on every WebSocket connection attempt.
- **Actions:**
  - Map a WebSocket endpoint (e.g., `/ws`) in the Kestrel pipeline.
  - Implement token generation: `RegenerateAuthToken()` creates a cryptographically random 32-byte hex string, stores it via `ISettingsRepository` under key `"WebSocketAuthToken"`.
  - On WebSocket connection: read `Authorization` header or `?token=` query parameter. If missing or invalid, return HTTP 401 and close.
  - Implement `AuthToken` property returning the current token.
  - Load existing token from settings on construction; if none exists, auto-generate.
- **Automated Testing:** Run `dotnet test tests/unit/` — all 114+ tests must pass.
- **Live Smoke Test (Mandatory):** Launch the app. Check log file for the port. Install a WebSocket client (e.g., [Simple WebSocket Client](https://chrome.google.com/webstore) Chrome extension, or use PowerShell: `$ws = New-Object System.Net.WebSockets.ClientWebSocket`). Try connecting to `ws://127.0.0.1:{port}/ws` WITHOUT a token — verify connection is rejected/closed. Check the log file for the auth token (run `findstr /C:"WebSocket" %LOCALAPPDATA%\MySecondBrain\logs\msb-*.log`). Connect again to `ws://127.0.0.1:{port}/ws?token={token}` — verify connection succeeds (stays open, not rejected).
- **Suggested Commit Message:** `feat: add token-based authentication to Kestrel WebSocket server`

### [x] Step 4: Fill KestrelWebSocketServer — JSON message protocol
- **Goal:** Implement bidirectional JSON messaging over the authenticated WebSocket connection. Enable `SendAsync` and `MessageReceived` event.
- **Actions:**
  - Implement receive loop: on connected WebSocket, read messages in a background loop. Parse each message as JSON. Fire `MessageReceived` event with the raw JSON string.
  - Implement `SendAsync(string message, CancellationToken ct)`: Send a JSON string to the connected client.
  - Handle client disconnect: clean up, log disconnection.
  - Handle multiple connections: only one active client connection supported at a time (or reject additional connections).
  - Log connections/disconnections via `ILogger`.
- **Automated Testing:** Run `dotnet test tests/unit/` — all tests pass.
- **Live Smoke Test (Mandatory):** Launch the app. Connect via WebSocket client to `ws://127.0.0.1:{port}/ws?token={token}`. Send a JSON message: `{"type":"ping"}`. Verify the app log file shows the received message. (The server logs the message but doesn't respond yet — the Word Add-in protocol is defined in Feature 13.) Close the WebSocket client connection. Verify the log shows "WebSocket client disconnected".
- **Suggested Commit Message:** `feat: implement JSON message protocol for Kestrel WebSocket server`

### [x] Step 5: Fill WinFormsSystemTrayService stub — NotifyIcon with basic Show/Hide/Exit
- **Goal:** Replace the stub in [`WinFormsSystemTrayService.cs`](src/MySecondBrain.UI/Services/WinFormsSystemTrayService.cs:5) with a working system tray icon using `System.Windows.Forms.NotifyIcon`. The icon appears on startup with a basic context menu (Show Studio, Exit).
- **Actions:**
  - Create `NotifyIcon` in constructor with app icon (use a placeholder `.ico` file in `Resources/` — a simple solid-color icon is fine for now; the design team provides final icon later).
  - Context menu with two items: "Open Studio" (fires `OpenStudioRequested`) and "Exit" (fires `ExitRequested`).
  - Implement `Show()` / `Hide()`: set `NotifyIcon.Visible`.
  - Implement `IsVisible` property.
  - Wire `NotifyIcon.DoubleClick` (or single left-click per Windows convention) to fire `OpenStudioRequested`.
  - Dispose `NotifyIcon` in `Dispose()`.
- **Automated Testing:** Run `dotnet test tests/unit/` — all 114+ tests must pass. Run `dotnet test tests/e2e/` — 18 E2E tests must pass (the app now has a tray icon, but tests that interact with the main window should still work).
- **Live Smoke Test (Mandatory):** Launch the app. Verify a MySecondBrain icon appears in the Windows system tray (notification area, may be in the overflow menu — click ^ arrow if not visible). Right-click the tray icon — verify context menu appears with "Open Studio" and "Exit". Click "Open Studio" — verify the main window comes to foreground. Double-click the tray icon — verify the main window restores/comes to foreground. Click "Exit" — verify the app fully closes (check Task Manager — no MySecondBrain.UI.exe process).
- **Suggested Commit Message:** `feat: implement WinFormsSystemTrayService with NotifyIcon and basic Show/Exit context menu`

### [x] Step 6: WinFormsSystemTrayService — full context menu with all events
- **Goal:** Add remaining context menu items: New Chat, Command Bar, Recent Chats (placeholder submenu), Settings. Wire up all five events.
- **Actions:**
  - Add menu items: "New Chat" (fires `NewChatRequested`), "Command Bar" (fires `CommandBarRequested`), separator, "Recent Chats" (submenu with placeholder "No recent chats" disabled item), "Settings" (fires `SettingsRequested`), separator, "Exit" (fires `ExitRequested`).
  - Implement `UpdateRecentChats(IReadOnlyList<string> recentChatTitles)`: rebuild the Recent Chats submenu. If empty, show disabled "No recent chats". If populated, show up to 5 items.
  - Each event is fired but NOT yet handled by MainWindow — the wiring will be done in Step 8 (system tray integration with MainWindow).
- **Automated Testing:** Add these unit tests to `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`:
  1. `SystemTray_ContextMenuHasCorrectItemOrder` — Resolve `ISystemTrayService`, reflect into the private `_contextMenu` field, assert 8 items in order (New Chat, Open Studio, Command Bar, separator, Recent Chats submenu, Settings, separator, Exit).
  2. `SystemTray_UpdateRecentChats_WithItems_AddsClickableItems` — Call `UpdateRecentChats(["Chat A", "Chat B"])`, reflect the Recent Chats submenu, assert 2 enabled items with correct text.
  3. `SystemTray_UpdateRecentChats_Empty_ClearsSubmenu` — Call `UpdateRecentChats([])`, assert submenu has 1 disabled "No recent chats" item.
  4. `SystemTray_Events_FireOnMenuClick` — Resolve the service, subscribe a flag to each of the 5 events, reflect into the menu items and invoke their Click handlers, assert each flag was set. Run all existing tests too.
- **Live Smoke Test (Mandatory):** Launch the app. Right-click the tray icon. Verify the context menu now shows (in order): New Chat, Open Studio, Command Bar, separator, Recent Chats (with "No recent chats" grayed out), Settings, separator, Exit. Click "New Chat" — nothing visible happens yet (event fires but no handler wired). Click "Settings" — nothing visible happens yet. Click "Command Bar" — nothing visible happens yet. Click "Exit" — app closes cleanly.
- **Suggested Commit Message:** `feat: add full system tray context menu with New Chat, Command Bar, Recent Chats, Settings`

### [x] Step 7: WinFormsSystemTrayService — minimize-to-tray and generation indicator
- **Goal:** Implement minimize-to-tray on window close (per A6 setting) and the generation indicator (icon variant with green dot overlay).
- **Actions:**
  - Implement minimize-to-tray: read `ISettingsRepository` key `"MinimizeToTray"` (default: `true`). When MainWindow close is requested and minimize-to-tray is enabled, hide window instead of exiting. Only `ExitRequested` event fully exits.
  - Wire `MainWindow.Closing` event in `MainWindow.xaml.cs`: if `ISystemTrayService.IsVisible` and minimize-to-tray enabled, `e.Cancel = true; this.Hide();`.
  - Implement `SetGenerationIndicator(bool isGenerating)`: swap `NotifyIcon.Icon` between normal icon and a variant with a green dot overlay. Build the overlay icon programmatically using `System.Drawing` (render a small green circle in the bottom-right corner of the base icon).
  - Create the two icon resources: `app.ico` (normal) and generate `app-generating.ico` programmatically on first use via `System.Drawing.Bitmap` + `Graphics`.
- **Automated Testing:** Add these unit tests to `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`:
  1. `SystemTray_SetGenerationIndicator_DoesNotThrow` — Resolve `ISystemTrayService`, call `SetGenerationIndicator(true)`, call `SetGenerationIndicator(false)`, assert no exception thrown and `IsVisible` unchanged.
  2. `SystemTray_GenerationIndicator_ProducesGreenDotIcon` — Resolve `ISystemTrayService`, reflect the `BuildGeneratingIcon()` private method, invoke it, assert returned `Icon` is not null and differs from the normal icon (compare `Size` and check for at least one green pixel via `Bitmap.GetPixel` on the bottom-right quadrant).
  3. `SystemTray_IsVisible_TracksShowHide` — Call `Show()`, assert `IsVisible == true`. Call `Hide()`, assert `IsVisible == false`. Call `Show()`, assert `IsVisible == true` again. Run all existing tests too.
- **Live Smoke Test (Mandatory):**
  1. **Minimize to tray:** Launch the app. Click the X (close) button on the MainWindow title bar. Verify the window disappears but the app icon remains in the system tray. Verify no `MySecondBrain.UI.exe` process was terminated (check Task Manager). Double-click the tray icon — verify the main window reappears. Right-click tray icon → "Exit" — verify app fully closes.
  2. **Generation indicator:** Add a temporary test hotkey in `App.xaml.cs` that toggles `ISystemTrayService.SetGenerationIndicator`. Verify the tray icon changes to include a green dot when toggled on, and reverts to the normal icon when toggled off.
- **Suggested Commit Message:** `feat: implement minimize-to-tray on close and generation indicator icon`

### [x] Step 8: Wire system tray events to MainWindow + App startup lifecycle
- **Goal:** Wire the five system tray events to actual MainWindow/MainWindowViewModel actions. Integrate system tray startup into `App.xaml.cs`.
- **Actions:**
  - In `App.xaml.cs` `OnStartup`, after `mainWindow.Show()`: resolve `ISystemTrayService`, call `Show()`, subscribe to events.
  - `OpenStudioRequested` → `MainWindow.Show()` + `MainWindow.Activate()` + `MainWindow.WindowState = Normal`.
  - `NewChatRequested` → `MainWindow.Show()` + `MainWindow.Activate()` + send `WeakReferenceMessenger` message for new chat (or call a placeholder method — full new-chat logic comes in Feature 9).
  - `CommandBarRequested` → placeholder — fires event but Tier 2 window doesn't exist yet (Feature 13). Log "CommandBarRequested — not yet implemented".
  - `SettingsRequested` → `MainWindow.Show()` + `MainWindow.Activate()` + set `MainWindowViewModel.SelectedScreen = ScreenType.Settings`.
  - `ExitRequested` → `Application.Current.Shutdown()`.
  - In `MainWindow.xaml.cs` `OnClosing`: check minimize-to-tray setting via `ISystemTrayService` and `ISettingsRepository`. If minimize-to-tray, `e.Cancel = true; this.Hide();`.
- **Automated Testing:** Add these unit tests to `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`:
  1. `SystemTray_ExitRequested_DisposesAndClearsEvents` — Resolve `ISystemTrayService`, call `Show()`, subscribe a flag to `ExitRequested`, invoke `ExitRequested`, assert flag was raised. Then call `Dispose()`, assert `IsVisible == false`. Subscribe a new handler to `ExitRequested` after dispose — assert it is now null (event handlers cleared).
  2. `SystemTray_UpdateRecentChats_AfterShow_IsIdempotent` — Call `Show()`, call `UpdateRecentChats(["Test"])`, call it again with different items, assert no double entries in the submenu.
Run `dotnet test tests/unit/` — all tests pass. Run `dotnet test tests/e2e/` — 18 E2E tests must pass.
- **Live Smoke Test (Mandatory):** Launch the app. Right-click tray icon → "Settings" — verify the main window appears with Settings screen selected. Right-click tray icon → "Open Studio" — verify main window comes to foreground. Right-click tray icon → "New Chat" — verify main window appears (new chat creation logged but not visible yet). Click MainWindow X button — verify window hides to tray. Right-click tray icon → "Exit" — verify app fully closes.
- **Suggested Commit Message:** `feat: wire system tray events to MainWindow actions and app lifecycle`

### [x] Step 9: Fill GlobalHotkeyService stub — RegisterHotKey with HwndSource hook
- **Goal:** Replace the stub in [`GlobalHotkeyService.cs`](src/MySecondBrain.UI/Services/GlobalHotkeyService.cs:7) with a working `RegisterHotKey` implementation. System-wide hotkeys fire the `HotkeyTriggered` event.
- **Actions:**
  - Create a hidden `HwndSource` window to receive `WM_HOTKEY` messages. Use `HwndSourceHook` to intercept the message pump.
  - P/Invoke `RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)` from `user32.dll`.
  - P/Invoke `UnregisterHotKey(IntPtr hWnd, int id)`.
  - Implement `RegisterHotkey(string hotkeyId, ModifierKeys modifiers, VirtualKey key)`: assign a sequential hotkey ID, call `RegisterHotKey`, store mapping of ID → `HotkeyAssignment`. Return `true` on success, `false` on failure.
  - Implement `UnregisterHotkey(string hotkeyId)`: call `UnregisterHotKey`, remove from tracking. Return `true/false`.
  - Implement `IsRegistered(string hotkeyId)` and `GetRegisteredHotkeys()`.
  - In the `HwndSourceHook`: on `WM_HOTKEY` (0x0312), extract `wParam` as hotkey ID, resolve to `HotkeyAssignment`, fire `HotkeyTriggered` event with `HotkeyTriggeredEventArgs`.
  - On construction: create the hidden message window. On `Dispose()`: unregister all hotkeys, destroy the window.
- **Automated Testing:** Add these unit tests to `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`:
  1. `GlobalHotkeyService_CanResolve_AsSingleton` — Resolve `IGlobalHotkeyService`, assert `IsType<GlobalHotkeyService>()`, assert singleton lifetime via `Assert.Same`.
  2. `GlobalHotkeyService_IsRegistered_ReturnsTrueForRegisteredHotkey` — Call `RegisterHotkey("test", ModifierKeys.Alt, VirtualKey.Q)`, assert `IsRegistered("test") == true`. Call `UnregisterHotkey("test")`, assert `IsRegistered("test") == false`.
  3. `GlobalHotkeyService_GetRegisteredHotkeys_ReturnsAll` — Register "hk1" and "hk2", assert `GetRegisteredHotkeys().Count == 2`, assert both IDs present.
  4. `GlobalHotkeyService_DetectConflict_SameKeyReturnsTrue` — Register "hkA" with Alt+W, assert `DetectConflict(ModifierKeys.Alt, VirtualKey.W) == true`.
  5. `GlobalHotkeyService_DetectConflict_DifferentKeyReturnsFalse` — Register "hkB" with Alt+E, assert `DetectConflict(ModifierKeys.Alt, VirtualKey.R) == false`.
  6. `GlobalHotkeyService_UnregisterHotkey_NonexistentIdReturnsFalse` — `UnregisterHotkey("nosuch")` returns `false`.
  7. `GlobalHotkeyService_Dispose_UnregistersAll` — Register two hotkeys, call `Dispose()`, assert `GetRegisteredHotkeys().Count == 0`.
  Note: `RegisterHotKey` P/Invoke will fail in unit tests (no HWND), so use `[StaFact]` and expect `false` from `RegisterHotkey` in the test environment. The registration tracking and conflict detection logic is validated. Hotkey dispatch (WM_HOTKEY) requires manual smoke testing.
  Run all existing tests too.
- **Live Smoke Test (Mandatory):** Launch the app. The app registers Alt+Space for the Command Bar (the default Tier 2 hotkey) on startup. **Note:** Alt+Space is the Windows system menu shortcut — if Windows intercepts it first, this test will not pass; the Command Bar hotkey may need to be changed in Feature 13 if this conflict proves unresolvable. Open any other application (e.g., Notepad). Press Alt+Space. Verify the app log file shows: "Hotkey triggered: CommandBar" (or similar log entry). Press Alt+Q (also registered by default) — verify log entry for the Rewrite hotkey. The hotkeys fire events but no UI appears yet (Tier 1/2 UIs are Feature 13). **Important:** `RegisterHotKey` intercepts keystrokes globally — these hotkeys WILL override the same shortcuts in other applications (Alt+Q in VS Code, Alt+W in browsers, etc.). This is by design for Tier 1 text actions; if conflicts arise, hotkey assignments can be changed in Settings (Feature 8).
- **Suggested Commit Message:** `feat: implement RegisterHotKey-based global hotkey service with HwndSource hook`

### [x] Step 10: GlobalHotkeyService — WH_KEYBOARD_LL fallback and conflict detection
- **Goal:** Add the `WH_KEYBOARD_LL` low-level keyboard hook as a fallback for key combinations that `RegisterHotKey` cannot handle. Implement `DetectConflict`.
- **Actions:**
  - P/Invoke `SetWindowsHookEx(WH_KEYBOARD_LL, callback, hMod, 0)` and `UnhookWindowsHookEx`.
  - Implement the low-level keyboard callback: check for registered modifier+key combinations. When matched, fire `HotkeyTriggered`. Be careful to call `CallNextHookEx` to avoid blocking other hooks.
  - The low-level hook is used as a FALLBACK only — `RegisterHotKey` is tried first. If `RegisterHotKey` fails, automatically try the low-level hook.
  - Implement `DetectConflict(ModifierKeys modifiers, VirtualKey key)`: check if any other registered hotkey uses the same combination. Also check against known Windows system hotkeys (Win+D, Win+L, Win+R, Alt+Tab, Alt+F4, Ctrl+Alt+Del, Ctrl+Shift+Esc). Return `true` if conflict found.
  - The low-level hook requires a message pump to run — the `HwndSource` window from Step 9 provides this.
- **Automated Testing:** Add these unit tests to `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`:
  1. `GlobalHotkeyService_DetectConflict_KnownSystemHotkeys` — Assert `DetectConflict(ModifierKeys.Windows, VirtualKey.D) == true` (Win+D), same for Win+L, Win+R, Alt+Tab (check `ModifierKeys.Alt, VirtualKey.Tab`), Alt+F4, Ctrl+Alt+Del, Ctrl+Shift+Esc. These must always return conflict even without registration.
  2. `GlobalHotkeyService_RegisterHotkey_RecordsFallbackWhenApiFails` — Register a hotkey with Alt+Q (expects RegisterHotKey to fail in test env). Verify via reflection that the registration was tracked in the `_fallbackRegistrations` collection (fallback path exercised).
  3. `GlobalHotkeyService_WH_KEYBOARD_LL_Hook_Created` — Verify via reflection that after construction, the `_hookId` is not `IntPtr.Zero` (the low-level hook was installed). Dispose the service, verify `_hookId` is `IntPtr.Zero` (hook was uninstalled).
  Run all existing tests too.
- **Live Smoke Test (Mandatory):** Launch the app. The app now has both `RegisterHotKey` (primary) and `WH_KEYBOARD_LL` (fallback) active. Open PowerShell and test conflict detection: this isn't directly testable by the user since `DetectConflict` is called programmatically. Instead, verify via log: the startup log should show which hotkeys were registered via `RegisterHotKey` vs. fallback. Verify Alt+Space still triggers (from Step 9). Close the app — verify all hotkeys are unregistered (try Alt+Space in another app, verify it opens the other app's behavior, not logging to MySecondBrain).
- **Suggested Commit Message:** `feat: add WH_KEYBOARD_LL fallback hook and hotkey conflict detection`

### [x] Step 11: Fill AutoUpdaterDotNet stub — update check against remote feed
- **Goal:** Replace the stub in [`AutoUpdaterDotNet.cs`](src/MySecondBrain.Services/Update/AutoUpdaterDotNet.cs:7) with a working update checker that queries a remote XML/JSON feed and compares versions.
- **Actions:**
  - Implement `CheckForUpdatesAsync(CancellationToken ct)`: fetch the update feed XML from `UpdateFeedUrl` via `HttpClient`. Parse the XML to extract latest version, release notes, download URL, and mandatory flag. Compare with `CurrentVersion` (read from `Assembly.GetEntryAssembly().GetName().Version`). Return `UpdateCheckResult` with `UpdateAvailable=true` + `UpdateInfo` if newer version exists.
  - Implement `DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct)`: download the MSIX installer to a temp file, report progress. Return a `FileStream` to the downloaded file.
  - Implement `InstallAsync(Stream updatePackage, CancellationToken ct)`: launch the MSIX installer via `Process.Start` and trigger app shutdown.
  - Implement `CurrentVersion` property: read from assembly version.
  - Implement `UpdateFeedUrl` property: hardcoded URL (configurable later via Settings in Feature 8).
  - The XML feed format follows AutoUpdater.NET convention:
    ```xml
    <item>
      <version>1.0.0.0</version>
      <url>https://updates.example.com/MySecondBrain.msix</url>
      <changelog>https://updates.example.com/changelog.html</changelog>
      <mandatory>false</mandatory>
    </item>
    ```
- **Automated Testing:** Add these unit tests to `tests/unit/MySecondBrain.Tests.Unit/DiContainerTests.cs`:
  1. `AutoUpdaterDotNet_CurrentVersion_ReadsFromAssembly` — Resolve `IUpdateChecker` concretely as `AutoUpdaterDotNet`, assert `CurrentVersion` is not null, not `0.0.0.0`.
  2. `AutoUpdaterDotNet_UpdateFeedUrl_IsNotEmpty` — Assert `UpdateFeedUrl` is not null and not empty after construction.
  3. `AutoUpdaterDotNet_CheckForUpdates_NoFeedAvailable_ReturnsNoUpdate` — Call `CheckForUpdatesAsync(CancellationToken.None)`, assert `UpdateAvailable == false` and `ErrorMessage` is not null (no real feed configured in test).
  4. `AutoUpdaterDotNet_DownloadUpdate_ThrowsForNullUpdate` — Assert `DownloadUpdateAsync(null!, null, CancellationToken.None)` throws `ArgumentNullException` or returns a faulted task.
  5. `AutoUpdaterDotNet_VersionComparison_NewerVersionDetected` — Set up a test-only `UpdateFeedUrl` pointing to a local temp file with version `99.0.0.0`, call `CheckForUpdatesAsync`, assert `UpdateAvailable == true` and `Update.Version == 99.0.0.0`. (Requires the service to expose a settable `UpdateFeedUrl` for testing — add this if not already present.)
  Run all existing tests too.
- **Live Smoke Test (Mandatory):** Since no real update feed exists yet, test via a local HTTP server:
  1. Create a temporary XML file with version `99.0.0.0` (higher than current `0.0.0.0`).
  2. Serve it via Python: `python -m http.server 8888` in the directory containing the XML.
  3. Temporarily hardcode `UpdateFeedUrl` to `http://127.0.0.1:8888/update.xml` (or use a test-only method to set it).
  4. Launch the app. Check the log file for "Update available: version 99.0.0.0" log entry.
  5. Verify the app does NOT crash or show errors even though the update isn't actually downloaded (the check succeeds but download is not triggered until user action, which comes in Feature 8).
- **Suggested Commit Message:** `feat: implement AutoUpdaterDotNet update check against remote XML feed`

### [x] Step 12: Verify PerMonitorV2 DPI rendering at multiple scaling levels
- **Goal:** Confirm that PerMonitorV2 DPI awareness (already configured) produces crisp rendering across common Windows scaling levels. Add DPI-related startup logging for diagnostics.
- **Actions:**
  - Verify [`MySecondBrain.UI.csproj`](src/MySecondBrain.UI/MySecondBrain.UI.csproj:10) has `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` (already present).
  - Add startup log entry in `App.xaml.cs` recording current DPI settings: log screen count, per-monitor DPI values, and app DPI mode.
  - Verify WPF device-independent pixels handle scaling natively — no code changes needed for WPF's automatic scaling.
- **Automated Testing:** Run `dotnet test tests/unit/` — all tests pass.
- **Live Smoke Test (Mandatory):** 
  1. Set your primary monitor to 100% scaling (Settings → Display → Scale).
  2. Launch the app. Verify all text is crisp, no blurry elements, no misaligned controls. Check log file for DPI info.
  3. Change primary monitor to 125% scaling. Relaunch the app. Verify crisp rendering.
  4. Change to 150% scaling. Relaunch. Verify crisp rendering.
  5. If you have a second monitor: set monitors to different scaling levels (e.g., primary 125%, secondary 100%). Launch app on primary, drag to secondary. Verify the window re-renders crisply on the second monitor.
  6. Check the log file — verify DPI awareness log shows "PerMonitorV2" and the detected DPI values.
- **Suggested Commit Message:** `feat: verify PerMonitorV2 DPI awareness and add DPI diagnostics logging`

### [x] Step 13: Verify MSIX Package project builds with .appinstaller template
- **Goal:** Verify the MSIX packaging project builds correctly and create the `.appinstaller` template for auto-update.
- **Actions:**
  - Verify [`MySecondBrain.Package.wapproj`](src/MySecondBrain.Package/MySecondBrain.Package.wapproj) builds with `dotnet build`.
  - Verify [`Package.appxmanifest`](src/MySecondBrain.Package/Package.appxmanifest:15-19) has required capabilities: `internetClient`, `runFullTrust` (rescap), `localSystemServices` (rescap) — already present.
  - Create [`MySecondBrain.appinstaller`](src/MySecondBrain.Package/MySecondBrain.appinstaller) template file with:
    - `MainBundle` URI placeholder (`https://updates.mysecondbrain.app/MySecondBrain.msixbundle`)
    - `UpdateSettings` with `OnLaunch` check (24-hour interval) and `AutomaticBackgroundTask`
    - Version placeholder that will be replaced during CI/CD build
  - The `.appinstaller` file is a template, not the final file — actual URIs and versions will be filled in by CI/CD during release.
- **Automated Testing:** Run `dotnet build src/MySecondBrain.Package/MySecondBrain.Package.wapproj` — must succeed with no errors. Run `dotnet test tests/unit/` — all tests pass.
- **Live Smoke Test (Mandatory):** Run `dotnet build src/MySecondBrain.Package/MySecondBrain.Package.wapproj` from the solution root. Verify the build completes with no errors. Verify the output `.msix` file exists in the build output directory. Check the generated `AppxManifest.xml` in the output — verify it includes the three required capabilities (`internetClient`, `runFullTrust`, `localSystemServices`). Open the `.appinstaller` template file in a text editor — verify it contains the correct XML structure with `MainBundle`, `UpdateSettings`, and `OnLaunch` elements.
- **Suggested Commit Message:** `feat: verify MSIX package build and add .appinstaller auto-update template`

### [x] Step 14: Run full test suite — all unit + E2E tests must pass
- **Goal:** Verify all tests pass after all platform service implementations are in place. No regressions.
- **Actions:**
  - Run `dotnet test tests/unit/MySecondBrain.Tests.Unit/` — all tests must pass (expecting 125+ unit tests including the 24 new platform service tests from Steps 6-11).
  - Run `dotnet test tests/e2e/MySecondBrain.Tests.E2E/` (or `pwsh tests/e2e/run-e2e-tests.ps1`) — all 18 tests must pass.
  - Verify DI resolution tests still pass — all 76+ registrations resolve correctly with the new implementations.
- **Automated Testing:** This step IS the automated testing run.
- **Live Smoke Test (Mandatory):** Run `dotnet test` from the solution root. Verify output shows 0 failed tests for both unit and E2E. Verify the total unit test count reflects the new tests added in Steps 6-11 (expecting 49+ tests in DiContainerTests alone: 25 existing + 24 new platform service tests).
- **Suggested Commit Message:** `test: verify all 132 tests pass after platform service implementations`

---

## 6. Shared Technical Context

*(Append-only log managed by the Feature Developer. Stores API endpoints, JSON payloads, state shapes, and abstractions created in earlier steps that later steps might need to read).*

- **WebSocket endpoint:** `ws://127.0.0.1:{port}/ws?token={token}` — JSON message protocol. Token auto-generated via `RNGCryptoServiceProvider`, 32-byte hex string. Stored in `ISettingsRepository` under key `"WebSocketAuthToken"`.
- **System tray icons:** `Resources/app.ico` (normal), `Resources/app-generating.ico` (with green dot — generated programmatically via `System.Drawing`).
- **Hotkey seed data:** 10 built-in TextActions (seeded in EF Core `HasData()`) with hotkey assignments:
  - Alt+Q → Rewrite (selection/replaceSelection)
  - Alt+W → Summarize (selection/showOnly)
  - Alt+E → Explain (selection/showOnly)
  - Alt+R → Translate (selection/replaceSelection)
  - Alt+C → Continue Writing (focusedElement/insertAtCursor)
  - Alt+Space → Command Bar (Tier 2)
- **AppSetting keys introduced:**
  - `"WebSocketAuthToken"` — auto-generated WebSocket auth token
  - `"MinimizeToTray"` — boolean, default `true`
- **KestrelWebSocketServer API (Step 2):** `ILocalWebSocketServer` resolved as `KestrelWebSocketServer` singleton. Methods: `StartAsync(int? preferredPort, CancellationToken)` — builds Kestrel `WebApplication` on `127.0.0.1:{port}` (0=auto), extracts port via `Uri.TryCreate`. `StopAsync(CancellationToken)` — graceful shutdown. `Port` (int), `IsRunning` (bool), `Dispose()` — synchronous via `GetAwaiter().GetResult()`. `/health` GET endpoint returns `"OK"`. Startup pattern in `App.xaml.cs`: fire-and-forget `StartWebSocketServerAsync()` after `MainWindow.Show()`. Shutdown in `OnExit`: resolve `ILocalWebSocketServer`, `StopAsync` with 5s timeout, then `Log.CloseAndFlush()`. Kestrel logging wired via `builder.Logging.AddSerilog()`.
- **Auto-Update XML feed format (AutoUpdater.NET):** `<item><version>X.Y.Z.W</version><url>...</url><changelog>...</changelog><mandatory>true|false</mandatory></item>`
- **DPI configuration:** `PerMonitorV2` set via `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in `.csproj`. Per-monitor DPI values logged at startup.
