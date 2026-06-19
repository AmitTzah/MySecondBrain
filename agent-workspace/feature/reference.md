# Feature Reference: Windows OS Platform Infrastructure

## Global & Shared Documentation

### Libraries Used Across Multiple Steps

| Library | Package/API | Usage | External Doc |
|---------|-------------|-------|-------------|
| ASP.NET Core Kestrel | `Microsoft.AspNetCore.App` (FrameworkReference) | Embedded WebSocket server on 127.0.0.1 | [`kestrel-websocket.md`](../external-docs/kestrel-websocket.md) |
| AutoUpdater.NET | `Autoupdater.NET.Official` v1.* | Auto-update framework | [`autoupdater-net.md`](../external-docs/autoupdater-net.md) |
| WinForms NotifyIcon | `System.Windows.Forms` (UseWindowsForms=true) | System tray integration | [`notifyicon-system-tray.md`](../external-docs/notifyicon-system-tray.md) |
| RegisterHotKey / WH_KEYBOARD_LL | `user32.dll` P/Invoke | Global hotkey registration | [`registerhotkey-global-hooks.md`](../external-docs/registerhotkey-global-hooks.md) |
| WPF HwndSource | `System.Windows.Interop.HwndSource` | Message pump integration for WM_HOTKEY | [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource) |

### Key Interfaces (Existing — Being Filled from Stub)

| Interface | Location | Implementation | Purpose |
|-----------|----------|---------------|---------|
| [`ILocalWebSocketServer`](../../src/MySecondBrain.Core/Interfaces/ILocalWebSocketServer.cs:5) | Core | `KestrelWebSocketServer` (UI) | Embedded WebSocket on 127.0.0.1 |
| [`ISystemTrayService`](../../src/MySecondBrain.Core/Interfaces/ISystemTrayService.cs:5) | Core | `WinFormsSystemTrayService` (UI) | System tray icon + context menu |
| [`IGlobalHotkeyService`](../../src/MySecondBrain.Core/Interfaces/IGlobalHotkeyService.cs:7) | Core | `GlobalHotkeyService` (UI) | System-wide hotkey registration |
| [`IUpdateChecker`](../../src/MySecondBrain.Core/Interfaces/IUpdateChecker.cs:5) | Core | `AutoUpdaterDotNet` (Services) | Auto-update check + download |

### AppSetting Keys (New)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `"WebSocketAuthToken"` | `string` | Auto-generated (32-char hex) | WebSocket authentication token |
| `"MinimizeToTray"` | `string` | `"true"` | Minimize to tray on close (A6 setting) |

---

## Step-Specific Documentation

### Step 1: Add ASP.NET Core Kestrel + WebSocket NuGet packages to UI project
- **Library:** `Microsoft.AspNetCore.App` framework reference
- **Import:** No C# using changes needed — the `FrameworkReference` brings in all required namespaces: `Microsoft.AspNetCore.Builder`, `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.WebSockets`, `System.Net.WebSockets`
- **Snippet:**
```xml
<!-- In MySecondBrain.UI.csproj, add inside <ItemGroup>: -->
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```
- **Key types acquired:**
  - `WebApplication` / `WebApplicationBuilder` — minimal hosting API
  - `WebSocket` / `WebSocketReceiveResult` — `System.Net.WebSockets`
  - `WebSocketMiddleware` — `app.UseWebSockets()`
  - `KestrelServerOptions` — `options.Listen(IPAddress, port)`

### Step 2: Fill KestrelWebSocketServer stub — server startup with auto-port selection
- **Library:** ASP.NET Core Kestrel (see [`kestrel-websocket.md`](../external-docs/kestrel-websocket.md))
- **Import:**
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
```
- **Snippet:**
```csharp
// StartAsync — build minimal Kestrel server on auto-port
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseKestrel(options =>
{
    options.Listen(IPAddress.Loopback, preferredPort ?? 0);
});
var app = builder.Build();
app.MapGet("/health", () => "OK");
var url = app.Urls.FirstOrDefault();
if (url != null && int.TryParse(url.Split(':').LastOrDefault(), out var parsedPort))
    _port = parsedPort;
_logger.LogInformation("WebSocket server started on port {Port}", _port);
await app.StartAsync(ct);
```
- **Reference:** [Kestrel external doc §"Building a Minimal Kestrel WebSocket Server"](../external-docs/kestrel-websocket.md)

### Step 3: Fill KestrelWebSocketServer — token-based authentication
- **Library:** ASP.NET Core Kestrel WebSocket middleware
- **Import:**
```csharp
using System.Security.Cryptography;
using MySecondBrain.Core.Interfaces; // ISettingsRepository
```
- **Snippet (token generation):**
```csharp
private string GenerateToken()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    var token = Convert.ToHexString(bytes);  // 64-char hex string
    _settings.SetAsync("WebSocketAuthToken", token);
    return token;
}
```
- **Snippet (token validation middleware):**
```csharp
app.Map("/ws", async (HttpContext context) =>
{
    var token = context.Request.Query["token"].FirstOrDefault()
        ?? context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

    if (string.IsNullOrEmpty(token) || token != _authToken)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    if (context.WebSockets.IsWebSocketRequest)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        await ReceiveLoop(ws);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});
```
- **Reference:** [Kestrel external doc §"Security"](../external-docs/kestrel-websocket.md)

### Step 4: Fill KestrelWebSocketServer — JSON message protocol
- **Library:** `System.Net.WebSockets.WebSocket`, `System.Text.Json`
- **Import:**
```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
```
- **Snippet (receive loop):**
```csharp
private async Task ReceiveLoop(WebSocket webSocket)
{
    var buffer = new byte[4096];
    _logger.LogInformation("WebSocket client connected");
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                break;
            }
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            _logger.LogDebug("WebSocket received: {Message}", message);
            MessageReceived?.Invoke(this, message);
        }
    }
    catch (WebSocketException ex)
    {
        _logger.LogWarning(ex, "WebSocket error");
    }
    finally
    {
        _logger.LogInformation("WebSocket client disconnected");
    }
}
```
- **Reference:** [Kestrel external doc §"WebSocket Receive Loop"](../external-docs/kestrel-websocket.md)

### Step 5: Fill WinFormsSystemTrayService stub — NotifyIcon with basic Show/Hide/Exit
- **Library:** `System.Windows.Forms.NotifyIcon` (WinForms interop, `UseWindowsForms=true` already in .csproj)
- **Import:**
```csharp
using System.Windows.Forms;
using System.Drawing;
using Application = System.Windows.Application; // Resolves ambiguity with WinForms Application
```
- **Snippet:**
```csharp
public WinFormsSystemTrayService(ILogger<WinFormsSystemTrayService> logger)
{
    _logger = logger;
    _notifyIcon = new NotifyIcon
    {
        // Note: Relative path depends on working directory. In WPF apps, prefer
        // loading from embedded resources: new Icon(Application.GetResourceStream(
        //     new Uri("pack://application:,,,/Resources/app.ico")).Stream)
        Icon = new Icon("Resources/app.ico"),
        Visible = false,
        Text = "MySecondBrain"
    };

    var contextMenu = new ContextMenuStrip();
    var openStudio = new ToolStripMenuItem("Open Studio");
    openStudio.Click += (s, e) => OpenStudioRequested?.Invoke(this, EventArgs.Empty);
    contextMenu.Items.Add(openStudio);

    var exit = new ToolStripMenuItem("Exit");
    exit.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
    contextMenu.Items.Add(exit);

    _notifyIcon.ContextMenuStrip = contextMenu;
    _notifyIcon.DoubleClick += (s, e) => OpenStudioRequested?.Invoke(this, EventArgs.Empty);
}
```
- **Reference:** [NotifyIcon external doc §"Creating a NotifyIcon"](../external-docs/notifyicon-system-tray.md)

### Step 6: WinFormsSystemTrayService — full context menu with all events
- **Library:** `System.Windows.Forms.ToolStripMenuItem` / `ContextMenuStrip`
- **Import:** Same as Step 5
- **Snippet (full menu):**
```csharp
var newChat = new ToolStripMenuItem("New Chat");
newChat.Click += (s, e) => NewChatRequested?.Invoke(this, EventArgs.Empty);

var commandBar = new ToolStripMenuItem("Command Bar");
commandBar.Click += (s, e) => CommandBarRequested?.Invoke(this, EventArgs.Empty);

_recentChatsMenu = new ToolStripMenuItem("Recent Chats");
_recentChatsMenu.DropDownItems.Add(new ToolStripMenuItem("No recent chats") { Enabled = false });

var settings = new ToolStripMenuItem("Settings");
settings.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

contextMenu.Items.AddRange(new ToolStripItem[]
{
    newChat, openStudio, commandBar,
    new ToolStripSeparator(), _recentChatsMenu,
    settings, new ToolStripSeparator(), exit
});
```
- **Reference:** [NotifyIcon external doc §"Context Menu"](../external-docs/notifyicon-system-tray.md)

### Step 7: WinFormsSystemTrayService — minimize-to-tray and generation indicator
- **Library:** `System.Drawing` (GDI+ for programmatic icon generation)
- **Import:**
```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using MySecondBrain.Core.Interfaces; // ISettingsRepository
```
- **Snippet (generation indicator icon):**
```csharp
private Icon BuildGeneratingIcon()
{
    if (_generatingIcon != null) return _generatingIcon;

    var bitmap = new Bitmap(32, 32);
    using (var g = Graphics.FromImage(bitmap))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawIcon(_normalIcon, 0, 0);
        using (var brush = new SolidBrush(Color.FromArgb(0, 220, 0)))
        {
            g.FillEllipse(brush, 22, 22, 10, 10);
        }
    }
    _generatingIcon = Icon.FromHandle(bitmap.GetHicon());
    return _generatingIcon;
}

public void SetGenerationIndicator(bool isGenerating)
{
    _notifyIcon.Icon = isGenerating ? BuildGeneratingIcon() : _normalIcon;
}
```
- **Snippet (MainWindow.Closing — minimize to tray):**
```csharp
// In MainWindow.xaml.cs
protected override async void OnClosing(CancelEventArgs e)
{
    var settings = App.Current.Services.GetRequiredService<ISettingsRepository>();
    var minimizeToTray = await settings.GetAsync("MinimizeToTray") ?? "true";
    if (minimizeToTray == "true" && _trayService.IsVisible)
    {
        e.Cancel = true;
        this.Hide();
    }
    base.OnClosing(e);
}
```
- **Reference:** [NotifyIcon external doc §"Programmatic Icon Generation" and "Minimize to Tray Pattern"](../external-docs/notifyicon-system-tray.md)

### Step 8: Wire system tray events to MainWindow + App startup lifecycle
- **Library:** `Microsoft.Extensions.DependencyInjection` (resolving services), `CommunityToolkit.Mvvm.Messaging` (WeakReferenceMessenger)
- **Import:**
```csharp
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.UI.ViewModels;
```
- **Snippet (App.xaml.cs wiring):**
```csharp
// In OnStartup, after mainWindow.Show():
var trayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
trayService.Show();

trayService.OpenStudioRequested += (s, e) =>
{
    mainWindow.Dispatcher.Invoke(() =>
    {
        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    });
};

trayService.ExitRequested += (s, e) =>
{
    // Set flag to bypass minimize-to-tray
    App.Current.Shutdown();
};

// ... same pattern for NewChatRequested, CommandBarRequested, SettingsRequested
```
- **Reference:** [App.xaml.cs](../../src/MySecondBrain.UI/App.xaml.cs:37-104) (existing startup pattern), [MainWindow.xaml.cs](../../src/MySecondBrain.UI/MainWindow.xaml.cs:5-13)

### Step 9: Fill GlobalHotkeyService stub — RegisterHotKey with HwndSource hook
- **Library:** `user32.dll` P/Invoke (`RegisterHotKey`, `UnregisterHotKey`), `System.Windows.Interop.HwndSource`
- **Import:**
```csharp
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
```
- **Snippet (HwndSource creation):**
```csharp
private void CreateMessageWindow()
{
    var parameters = new HwndSourceParameters("MSB_HotkeyWindow")
    {
        Width = 0,
        Height = 0,
        WindowStyle = 0,
    };
    _hwndSource = new HwndSource(parameters);
    _hwndSource.AddHook(WndProc);
    _windowHandle = _hwndSource.Handle;
}

private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    const int WM_HOTKEY = 0x0312;
    if (msg == WM_HOTKEY)
    {
        int hotkeyId = wParam.ToInt32();
        if (_registrations.TryGetValue(hotkeyId, out var assignment))
        {
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(assignment.HotkeyId));
            handled = true;
        }
    }
    return IntPtr.Zero;
}
```
- **Snippet (RegisterHotKey P/Invoke):**
```csharp
[DllImport("user32.dll", SetLastError = true)]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll", SetLastError = true)]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

private const uint MOD_ALT = 0x0001;
private const uint MOD_CONTROL = 0x0002;
private const uint MOD_SHIFT = 0x0004;
private const uint MOD_WIN = 0x0008;

public bool RegisterHotkey(string hotkeyId, ModifierKeys modifiers, VirtualKey key)
{
    uint fsModifiers = MapModifiers(modifiers);
    uint vk = (uint)key;
    int id = Interlocked.Increment(ref _nextId);

    bool success = RegisterHotKey(_windowHandle, id, fsModifiers, vk);
    if (success)
    {
        _registrations[id] = new HotkeyAssignment(hotkeyId, modifiers, key);
    }
    return success;
}
```
- **Reference:** [RegisterHotKey external doc §"RegisterHotKey (Primary)"](../external-docs/registerhotkey-global-hooks.md)

### Step 10: GlobalHotkeyService — WH_KEYBOARD_LL fallback and conflict detection
- **Library:** `user32.dll` P/Invoke (`SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`)
- **Import:**
```csharp
using System.Diagnostics;
```
- **Snippet (WH_KEYBOARD_LL callback):**
```csharp
private const int WH_KEYBOARD_LL = 13;
private const int WM_KEYDOWN = 0x0100;
private const int WM_SYSKEYDOWN = 0x0104;

private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode >= 0)
    {
        bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
        if (isKeyDown)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            foreach (var reg in _fallbackRegistrations.Values)
            {
                if (IsModifierMatch(reg.Modifiers) && (VirtualKey)vkCode == reg.Key)
                {
                    HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(reg.HotkeyId));
                    return (IntPtr)1; // Suppress
                }
            }
        }
    }
    return CallNextHookEx(_hookId, nCode, wParam, lParam);
}
```
- **Snippet (DetectConflict):**
```csharp
private static readonly HashSet<(ModifierKeys, VirtualKey)> SystemHotkeys = new()
{
    // Win combinations
    (ModifierKeys.Windows, VirtualKey.D), (ModifierKeys.Windows, VirtualKey.L),
    (ModifierKeys.Windows, VirtualKey.R), (ModifierKeys.Windows, VirtualKey.E),
    // Alt combinations
    (ModifierKeys.Alt, VirtualKey.Tab), (ModifierKeys.Alt, VirtualKey.F4),
    (ModifierKeys.Alt, VirtualKey.Space),
    // Ctrl+Alt+Del and Ctrl+Shift+Esc are not representable as simple
    // (ModifierKeys,VirtualKey) pairs because they are handled at the
    // Secure Attention Sequence (SAS) level and cannot be intercepted
    // by user-mode hooks.
};

public bool DetectConflict(ModifierKeys modifiers, VirtualKey key)
{
    if (SystemHotkeys.Contains((modifiers, key))) return true;
    return _registrations.Values.Any(r => r.Modifiers == modifiers && r.Key == key)
        || _fallbackRegistrations.Values.Any(r => r.Modifiers == modifiers && r.Key == key);
}
```
- **Reference:** [RegisterHotKey external doc §"WH_KEYBOARD_LL (Fallback)"](../external-docs/registerhotkey-global-hooks.md)

### Step 11: Fill AutoUpdaterDotNet stub — update check against remote feed
- **Library:** `Autoupdater.NET.Official` (already in .csproj)
- **Import:**
```csharp
using System.Net.Http;
using System.Reflection;
using System.Xml.Linq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
```
- **Snippet (UpdateCheckAsync):**
```csharp
public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
{
    try
    {
        using var http = new HttpClient();
        var xml = await http.GetStringAsync(UpdateFeedUrl, ct);
        var doc = XDocument.Parse(xml);
        var versionStr = doc.Descendants("version").FirstOrDefault()?.Value;
        var downloadUrl = doc.Descendants("url").FirstOrDefault()?.Value;
        var changelog = doc.Descendants("changelog").FirstOrDefault()?.Value;
        var mandatoryStr = doc.Descendants("mandatory").FirstOrDefault()?.Value;

        if (Version.TryParse(versionStr, out var latestVersion) && latestVersion > CurrentVersion)
        {
            return new UpdateCheckResult(true, new UpdateInfo(
                latestVersion, changelog ?? "", DateTimeOffset.UtcNow,
                0, downloadUrl ?? "", mandatoryStr == "true"), null);
        }
        return new UpdateCheckResult(false, null, null);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Update check failed");
        return new UpdateCheckResult(false, null, ex.Message);
    }
}

public Version CurrentVersion =>
    Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 0, 0);
```
- **Snippet (DownloadUpdateAsync + InstallAsync):**
```csharp
public async Task<Stream> DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct)
{
    using var http = new HttpClient();
    var response = await http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();
    var tempPath = Path.Combine(Path.GetTempPath(), $"MSB_Update_{update.Version}.msix");
    var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
        8192, useAsync: true);
    await using (fileStream.ConfigureAwait(false))
    {
        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;
            if (totalBytes > 0)
                progress?.Report((int)(totalRead * 100 / totalBytes));
        }
    }
    return File.OpenRead(tempPath); // Re-open read-only for caller
}

public async Task InstallAsync(Stream updatePackage, CancellationToken ct)
{
    var tempPath = ((FileStream)updatePackage).Name;
    updatePackage.Dispose();
    var psi = new ProcessStartInfo(tempPath) { UseShellExecute = true };
    Process.Start(psi);
    // Trigger app shutdown after launching installer
    Environment.Exit(0);
}
```
- **Reference:** [AutoUpdater.NET external doc §"XML Feed Format" and "Manual Update Check Pattern"](../external-docs/autoupdater-net.md)

### Step 12: Verify PerMonitorV2 DPI rendering at multiple scaling levels
- **Library:** None — platform configuration only
- **Import:** N/A (no code changes — verification only)
- **Snippet (DPI diagnostics logging in App.xaml.cs):**
```csharp
// In OnStartup, after mainWindow.Show():
var dpiLogger = _serviceProvider.GetRequiredService<ILogger<App>>();
dpiLogger.LogInformation("DPI mode: PerMonitorV2");
// For per-monitor DPI, WPF reports DPI per-window via PresentationSource.
// Screen-level DPI from WinForms Graphics always reflects the primary monitor.
// Individual screen DPIs are logged when windows move between monitors.
foreach (var screen in System.Windows.Forms.Screen.AllScreens)
{
    // Note: Screen.AllScreens reports monitor bounds and primary status,
    // but GDI+ Graphics.FromHwnd(IntPtr.Zero) only reflects primary monitor DPI.
    // Per-monitor DPI values are obtained via WPF's VisualTreeHelper.GetDpi()
    // on the MainWindow when the DpiChanged event fires.
    dpiLogger.LogInformation("Monitor detected: {DeviceName}, Bounds: {Bounds}, Primary: {Primary}",
        screen.DeviceName, screen.Bounds, screen.Primary);
}
```
- **Configuration verified:**
```xml
<!-- MySecondBrain.UI.csproj — already present -->
<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
```
- **Reference:** [Platform notes §7 — Per-Monitor DPI](../../agent-workspace/project-director/planning/platform-notes.md#7-per-monitor-dpi-permonitorv2)

### Step 13: Verify MSIX Package project builds with .appinstaller template
- **Library:** None — MSIX packaging tooling (built into Windows SDK)
- **Import:** N/A (XML template file only)
- **Snippet (.appinstaller template):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller Uri="https://updates.mysecondbrain.app/MySecondBrain.appinstaller"
              Version="0.0.0.0"
              xmlns="http://schemas.microsoft.com/appx/appinstaller/2018">
    <MainBundle Name="MySecondBrain"
                Version="0.0.0.0"
                Publisher="CN=MySecondBrain"
                Uri="https://updates.mysecondbrain.app/MySecondBrain.msixbundle" />
    <UpdateSettings>
        <OnLaunch HoursBetweenUpdateChecks="24" />
        <AutomaticBackgroundTask />
        <ForceUpdateFromAnyVersion>true</ForceUpdateFromAnyVersion>
    </UpdateSettings>
</AppInstaller>
```
- **Reference:** [Platform notes §9 — MSIX Packaging & Deployment](../../agent-workspace/project-director/planning/platform-notes.md#9-msix-packaging--deployment), [Platform notes §10 — Auto-Update Mechanism](../../agent-workspace/project-director/planning/platform-notes.md#10-auto-update-mechanism)

### Step 14: Run full test suite — 114 unit + 18 E2E tests must pass
- **Library:** xUnit (test framework), coverlet (coverage)
- **Import:** N/A (test execution only)
- **Snippet:** `dotnet test` from solution root
- **Reference:** [CI/CD — GitHub Actions workflow](../../.github/workflows/) (trigger: push/PR to main, runner: windows-latest)
