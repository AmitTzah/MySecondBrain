# Platform Notes — WPF-Specific Patterns, Constraints & Conventions

## Overview

MySecondBrain is built on .NET 8.0 WPF. This document captures the WPF-specific architectural patterns, Windows integration conventions, and known pitfalls that every developer on the project must understand. It supplements [`architecture.md`](architecture.md) with implementation-level guidance specific to the WPF platform.

---

## 1. MVVM Pattern — CommunityToolkit.Mvvm

### Pattern Enforcement

All UI follows MVVM (Model-View-ViewModel) strictly. Code-behind files must contain ONLY view-specific logic: focus management, overlay positioning, window Z-order, drag-drop handling, and HWND interop. All business logic, state, and commands live in ViewModels.

### CommunityToolkit.Mvvm Conventions

**NuGet:** `CommunityToolkit.Mvvm` (MIT license, latest stable)

**Base Class:** All ViewModels inherit from `ObservableObject`.

```csharp
public partial class ChatThreadViewModel : ObservableObject
{
    // Source-generated observable property
    [ObservableProperty]
    private string? _title;

    // Source-generated relay command
    [RelayCommand]
    private async Task SendMessageAsync(CancellationToken ct)
    {
        // ...
    }
}
```

**Key Source Generators:**

| Attribute | Generates | Usage |
|-----------|-----------|-------|
| `[ObservableProperty]` | Public property with `OnPropertyChanged` + partial `On{Name}Changed` hook | All bindable properties |
| `[RelayCommand]` | `IAsyncRelayCommand` or `IRelayCommand` property with `CanExecute` logic | All user actions |
| `[NotifyPropertyChangedFor]` | Property change notification for dependent properties | Computed/derived properties |

**Messenger:** `WeakReferenceMessenger.Default` for cross-ViewModel communication:
- `ChatThreadCreatedMessage` — when a new thread is created from Tier 1/2, MainWindow opens it
- `GenerationCompletedMessage` — cross-tab completion alert (C35)
- `ThemeChangedMessage` — instant theme switch across all windows (A5)
- `ChatThreadDeletedMessage` — sidebar/tab cleanup on soft-delete

**DI Integration:** ViewModels are registered in `Microsoft.Extensions.DependencyInjection` and constructor-injected with services.

```csharp
services.AddTransient<ChatThreadViewModel>();
services.AddTransient<SettingsViewModel>();
services.AddSingleton<MainWindowViewModel>();
```

### MVVM Rules

1. ViewModels NEVER reference WPF types (`Window`, `UserControl`, `TextBox`, etc.). Only .NET primitives and service interfaces.
2. Views bind to ViewModels via `DataContext`. Views are resolved via `DataTemplate` or DI View location.
3. Dialog/popup interactions use a `IDialogService` abstraction (ViewModels request dialogs; views implement them).
4. The `Messenger` is the ONLY cross-ViewModel communication mechanism. No direct ViewModel→ViewModel references.

---

## 2. XAML Data Binding & DataTemplate Patterns

### Chat Message Rendering

Chat messages are heterogeneous: user messages, assistant messages, system messages, tool-call results, and thinking blocks each need different visual templates. Use `DataTemplateSelector` or implicit `DataTemplate` based on `Message` properties.

```xml
<!-- Example: selecting template by message role -->
<ListBox ItemsSource="{Binding ActiveBranchMessages}"
         ItemTemplateSelector="{StaticResource MessageTemplateSelector}">
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

**Three Chat Themes (A3):**
- **Classic:** Role label + timestamp header, Markdown body, distinct background per role
- **Compact:** Minimal header, tighter spacing, role indicated by color accent only
- **Bubble:** Speech-bubble style with tails, alternating sides for user/assistant

Each theme is a separate `DataTemplate` resource. `IThemeProvider.ChatTheme` property drives which template is active. Switch via `DynamicResource` or `ContentTemplateSelector`.

### Wiki Browser

Three-region split (N4): `Grid` with two `GridSplitter`s — one vertical (tree vs. viewer) and one horizontal (viewer vs. info panel). All three regions are resizable; sizes persisted to SQLite and restored on next open.

### Settings Screen

16 categories (plus System Info as 17th planned in F11) organized as a list of section headers. Use `ListBox` with grouping or a flat `ScrollViewer` with `Expander` controls per section. Each section's content bound to a subsection ViewModel.

### Code Block Rendering

Fenced code blocks (` ```language ... ``` `) are rendered with:
1. Syntax highlighting via AvalonEdit's `HighlightingManager` (100+ languages)
2. A "Copy" button (top-right, visible on hover)
3. Language label (top-left)
4. Horizontal scrolling for long lines (no wrapping)

### Bidirectional Text (Hebrew RTL)

WPF `FlowDocument` natively implements the Unicode Bidi Algorithm. Per-message direction:

```xml
<FlowDocument FlowDirection="{Binding MessageFlowDirection}">
    <!-- Blocks added by IContentBlockRenderer -->
</FlowDocument>
```

`MessageFlowDirection` is a computed property: scan content for Hebrew Unicode range (U+0590–U+05FF). If >threshold% Hebrew characters → `RightToLeft`. Code blocks always enforce `FlowDirection="LeftToRight"` regardless of content.

---

## 3. Dependency Injection — Microsoft.Extensions.DependencyInjection

### Service Registration

`Microsoft.Extensions.DependencyInjection` is the DI container. Registration happens in `App.xaml.cs` at startup.

```csharp
public partial class App : Application
{
    private IServiceProvider _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Services — singleton (shared across all windows/tiers)
        services.AddSingleton<IChatThreadService, ChatThreadService>();
        services.AddSingleton<ILLMProviderService, LLMProviderService>();
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
        services.AddSingleton<IWikiService, WikiService>();
        services.AddSingleton<IThemeProvider, WpfThemeProvider>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<ISystemTrayService, WinFormsSystemTrayService>();
        services.AddSingleton<IToolOrchestrator, ToolOrchestrator>();

        // Services — scoped or transient
        services.AddTransient<IClipboardService, WpfClipboardService>();
        services.AddTransient<IAudioService, NaudioAudioService>();
        services.AddTransient<ICameraService, AForgeCameraService>();

        // Repositories — singleton (single DbContext)
        services.AddSingleton<AppDbContext>();
        services.AddSingleton<IChatThreadRepository, ChatThreadRepository>();
        services.AddSingleton<IMessageRepository, MessageRepository>();
        services.AddSingleton<IPersonaRepository, PersonaRepository>();
        services.AddSingleton<IModelConfigurationRepository, ModelConfigurationRepository>();
        services.AddSingleton<IApiKeyRepository, ApiKeyRepository>();
        services.AddSingleton<IWikiIndexRepository, WikiIndexRepository>();
        services.AddSingleton<IUsageRepository, UsageRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();

        // ViewModels — transient (new instance per window/tab)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ChatThreadViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<WikiBrowserViewModel>();
        services.AddTransient<UsageDashboardViewModel>();
        services.AddTransient<MediaLibraryViewModel>();
        services.AddTransient<GlobalArtifactsBrowserViewModel>();
        services.AddTransient<Tier1OverlayViewModel>();
        services.AddTransient<Tier2CommandBarViewModel>();
        services.AddTransient<ModelComparisonViewModel>();
        services.AddTransient<OnboardingWizardViewModel>();

        // Content block renderers
        services.AddSingleton<IContentRendererRegistry, ContentRendererRegistry>();
        services.AddSingleton<IContentBlockRenderer, MarkdownTextRenderer>();
        services.AddSingleton<IContentBlockRenderer, CodeBlockRenderer>();
        services.AddSingleton<IContentBlockRenderer, ArtifactReferenceRenderer>();
        services.AddSingleton<IContentBlockRenderer, ImageRenderer>();
        services.AddSingleton<IContentBlockRenderer, MediaRenderer>();
        services.AddSingleton<IContentBlockRenderer, ThinkingRenderer>();
        services.AddSingleton<IContentBlockRenderer, ToolCallRenderer>();

        _serviceProvider = services.BuildServiceProvider();
    }
}
```

### Lifetime Management Rules

| Lifetime | Used For | Rationale |
|----------|----------|-----------|
| **Singleton** | Services, repositories, theme provider, hotkey service, system tray | Shared state across all windows. One database. One LLM connection pool. |
| **Transient** | ViewModels, clipboard service, audio service | Fresh state per window/tab/chat. No cross-tab state leakage. |
| **Scoped** | Not used | Single-user app with no request/response cycle. Singleton + transient cover all needs. |

### EF Core DbContext

`AppDbContext` is registered as singleton. Since this is a single-user desktop app with no concurrent requests, a single DbContext instance is safe. All repository methods are thread-safe via `SemaphoreSlim` on write operations. Read operations can proceed concurrently.

### View Resolution

ViewModels don't know about Views. Window/View resolution pattern:

```csharp
// In App.xaml.cs or a ViewLocator
public Window ResolveWindow<TViewModel>(TViewModel vm) where TViewModel : ObservableObject
{
    return vm switch
    {
        MainWindowViewModel => new MainWindow { DataContext = vm },
        Tier1OverlayViewModel => new Tier1OverlayWindow { DataContext = vm },
        Tier2CommandBarViewModel => new Tier2CommandBarWindow { DataContext = vm },
        // ...
    };
}
```

---

## 4. Three-Tier Window Management

### Window Types

| Window | Type | Style | Z-Order | Activation |
|--------|------|-------|---------|------------|
| **MainWindow** | Standard `Window` | Resizable, chrome or custom | Normal | Activated (gets focus) |
| **Tier2CommandBarWindow** | `Window` | `WindowStyle=None`, `AllowsTransparency=True` | `Topmost=True` | Activated (spotlight bar needs focus for input) |
| **Tier1OverlayWindow** | `Window` | `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent` | `Topmost=True`, `WS_EX_NOACTIVATE` | Does NOT steal focus |

### WS_EX_NOACTIVATE

The Tier 1 pill overlay must NOT steal focus from the source application. This requires P/Invoke:

```csharp
// In Tier1OverlayWindow constructor or OnSourceInitialized
private const int WS_EX_NOACTIVATE = 0x08000000;
private const int GWL_EXSTYLE = -20;

[DllImport("user32.dll")]
private static extern int GetWindowLong(IntPtr hwnd, int index);

[DllImport("user32.dll")]
private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    var hwnd = new WindowInteropHelper(this).Handle;
    var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
}
```

### Focus Management

- **Tier 1:** NEVER takes focus. `WS_EX_NOACTIVATE` + `ShowActivated=false`. On Accept with Apply: the result is injected into the source window, which retains focus.
- **Tier 2:** Takes focus (user must type). On dismiss, returns focus to previously focused window.
- **Tier 3 (MainWindow):** Normal window activation. When opened from Tier 1/2 elevation, MainWindow activates and Tier 1/2 closes.

### Overlay Positioning

- **Tier 1 pill:** Positioned near cursor. Calculate via `GetCursorPos` (P/Invoke). Pill appears above or below cursor depending on screen edge proximity. Multi-monitor: constrain to monitor bounds.
- **Tier 2 Command Bar:** Centered on primary monitor (or monitor containing cursor). `WindowStartupLocation=CenterScreen`.

---

## 5. System Tray Integration

### NotifyIcon via WinForms Interop

WPF has no native tray icon. Use `System.Windows.Forms.NotifyIcon`:

```xml
<!-- .csproj: add reference -->
<UseWindowsForms>true</UseWindowsForms>
```

```csharp
public class SystemTrayService : ISystemTrayService, IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public SystemTrayService()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon("Resources/app.ico"),
            Visible = true,
            Text = "MySecondBrain"
        };

        _notifyIcon.DoubleClick += (s, e) => RestoreMainWindow();
        // Right-click context menu built via ContextMenuStrip
    }
}
```

**Context Menu Items:** New Chat, Open Studio, Command Bar, Recent Chats (submenu), Settings, Exit.

**Generation Indicator:** Change `NotifyIcon.Icon` to a variant with a pulsing/green dot overlay. Use a timer to alternate icons for a subtle animation effect. Alternative: `NotifyIcon.ShowBalloonTip()` for completion notification.

**Minimize to Tray:** Configured via Settings (A6). On MainWindow close:
- If minimize-to-tray enabled: `e.Cancel = true; Hide();`
- If disabled: normal close → app exit

---

## 6. Global Hotkeys — P/Invoke

### RegisterHotKey (Primary)

```csharp
[DllImport("user32.dll", SetLastError = true)]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll", SetLastError = true)]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

// Modifiers: MOD_ALT=0x0001, MOD_CONTROL=0x0002, MOD_SHIFT=0x0004, MOD_WIN=0x0008
// Virtual keys: VkKeyScan() or Keys enum cast to uint
```

**Registration:** Each TextAction with a hotkey assigned registers via `RegisterHotKey`. Hotkey IDs are sequentially assigned (0, 1, 2...). The `HwndSource` hook in `GlobalHotkeyService` processes `WM_HOTKEY` messages from the message pump.

**Conflict Detection:** Before registering, check if any existing TextAction uses the same combination. Warn user: "Alt+Q is already assigned to 'Rewrite'. Reassign it?"

### WH_KEYBOARD_LL (Fallback)

For key combinations that `RegisterHotKey` cannot handle:

```csharp
[DllImport("user32.dll", SetLastError = true)]
private static extern IntPtr SetWindowsHookEx(int hookType, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

private const int WH_KEYBOARD_LL = 13;
```

**Note:** Low-level hooks may trigger AV false positives. Code-signed binary mitigates this. Prefer `RegisterHotKey` for all assignable combinations.

### Message Pump Integration

```csharp
// In GlobalHotkeyService
private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == WM_HOTKEY)
    {
        int hotkeyId = wParam.ToInt32();
        var textAction = GetTextActionByHotkeyId(hotkeyId);
        if (textAction != null)
        {
            _messenger.Send(new HotkeyTriggeredMessage(textAction));
            handled = true;
        }
    }
    return IntPtr.Zero;
}
```

---

## 7. Per-Monitor DPI (PerMonitorV2)

### app.manifest Configuration

```xml
<!-- app.manifest -->
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  </windowsSettings>
</application>
```

### WPF DPI Behavior

- WPF uses device-independent pixels (1/96 inch). The framework handles scaling natively.
- Vector-based UI (text, shapes, standard controls) scales crisply at any DPI.
- Bitmap resources: provide multi-resolution assets (16x16, 32x32, 48x48, 64x64, 256x256) or use SVG icons via `SharpVectors` NuGet to avoid blurriness.
- Tier 1 pill positioning near cursor must account for DPI: convert screen coordinates to WPF coordinates using `PointFromScreen` and `PointToScreen`.

### Testing Matrix

| Config | Primary Monitor | Secondary Monitor |
|--------|----------------|-------------------|
| 100% + 100% | 1920x1080 @ 96 DPI | 1920x1080 @ 96 DPI |
| 125% + 100% | 1920x1080 @ 120 DPI | 1920x1080 @ 96 DPI |
| 150% + 125% | 2560x1440 @ 144 DPI | 1920x1080 @ 120 DPI |
| 200% + 150% | 3840x2160 @ 192 DPI | 2560x1440 @ 144 DPI |
| 100% + 200% | 1920x1080 @ 96 DPI | 3840x2160 @ 192 DPI |

Window drag between monitors with different DPI must not cause layout glitches.

---

## 8. Bidirectional Text — FlowDocument / FlowDirection

### Per-Message Direction Detection

```csharp
public FlowDirection GetMessageFlowDirection(string content)
{
    if (string.IsNullOrEmpty(content)) return FlowDirection.LeftToRight;

    int hebrewChars = content.Count(c => c >= 0x0590 && c <= 0x05FF);
    int totalLetters = content.Count(char.IsLetter);

    if (totalLetters == 0) return FlowDirection.LeftToRight;
    return (double)hebrewChars / totalLetters > 0.3
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;
}
```

### Code Block Enforcement

Code blocks must always be LTR regardless of content:

```xml
<Paragraph FlowDirection="LeftToRight" FontFamily="Cascadia Code">
    <Run Text="{Binding CodeContent}"/>
</Paragraph>
```

### Mixed LTR/RTL Segments

WPF's `FlowDocument` handles mixed content via the Unicode Bidi Algorithm automatically. For manually segmented content (e.g., quoted English in Hebrew message), insert explicit `FlowDirection` on `Run` elements:

```csharp
// Within a FlowDocument
var paragraph = new Paragraph { FlowDirection = FlowDirection.RightToLeft };
paragraph.Inlines.Add(new Run("שלום ") { FlowDirection = FlowDirection.RightToLeft });
paragraph.Inlines.Add(new Run("hello world") { FlowDirection = FlowDirection.LeftToRight });
paragraph.Inlines.Add(new Run(" מה נשמע"));
```

### Textbox Input Direction

Auto-detect based on first strong directional character typed. Toggle via Settings: "Auto-detect RTL" (Q). Use `FlowDirection` attached property on the textbox.

---

## 9. MSIX Packaging & Deployment

### MSIX Structure

MySecondBrain is packaged as an MSIX installer. Key considerations:

- **App Installer:** Enables auto-update via `.appinstaller` file hosted alongside the MSIX
- **Code Signing:** EV Code Signing Certificate required. Mitigates SmartScreen warnings and AV false positives (Flag #6)
- **App Identity:** Package Identity (`Publisher`, `PackageFamilyName`) required for some Windows APIs

### MSIX Capabilities

```xml
<!-- Package.appxmanifest -->
<Capabilities>
    <Capability Name="internetClient"/>          <!-- API calls, GCS, web search -->
    <rescap:Capability Name="runFullTrust"/>      <!-- P/Invoke, global hooks, HWND access -->
    <rescap:Capability Name="localSystemServices"/> <!-- System.Diagnostics.Process -->
</Capabilities>
```

### App Data Isolation

MSIX virtualizes `%LOCALAPPDATA%`. App data is stored in the virtualized location, which is preserved across updates but not across uninstall (unless user opts to keep data). Document this: "Uninstalling will remove all chat history and settings. Backup first."

### Side-Loading vs. Store

- **Primary:** Direct download MSIX from project website (side-loaded)
- **Optional:** Microsoft Store submission for discoverability
- Auto-update via App Installer works for both paths

---

## 10. Auto-Update Mechanism

### App Installer Auto-Update (MSIX)

```xml
<!-- .appinstaller file hosted at update feed URL -->
<AppInstaller Uri="https://updates.mysecondbrain.app/MySecondBrain.appinstaller"
              Version="1.2.0.0">
    <MainBundle Uri="https://updates.mysecondbrain.app/MySecondBrain.msixbundle"
                Version="1.2.0.0"/>
    <UpdateSettings>
        <OnLaunch HoursBetweenUpdateChecks="24"/>
        <AutomaticBackgroundTask/>
        <ForceUpdateFromAnyVersion>true</ForceUpdateFromAnyVersion>
    </UpdateSettings>
</AppInstaller>
```

### AutoUpdater.NET (Backup)

If App Installer auto-update is unavailable (non-MSIX deployment), use `AutoUpdater.NET`:

1. Check update feed (JSON) on startup or per schedule
2. Compare version with current assembly version
3. If update available: show notification with release notes
4. User clicks "Update" → download MSIX → launch installer → app exits → installer runs → app restarts

**Configuration:** `UpdateFeedUrl` (string), check frequency (enum: Startup, Daily, Weekly, Manual).

---

## 11. Known WPF Pitfalls & Workarounds

### 1. `AllowsTransparency=True` Performance

**Problem:** Transparent windows with `AllowsTransparency=True` disable hardware rendering (render on CPU via layered windows). This causes poor performance for animated/styled overlays.

**Workaround for Tier 1 pill:** The pill is small and short-lived — `AllowsTransparency` is acceptable. For Tier 2 Command Bar, consider using `WindowChrome` with `GlassFrameThickness=0` instead of `AllowsTransparency` for better rendering performance.

### 2. `MediaElement` Codec Limitations

**Problem:** WPF `MediaElement` uses Windows Media Player codecs and may fail on some MP4 variants (H.265/HEVC).

**Workaround:** Implement `IVideoPlayerService` with primary `MediaElement` and fallback `LibVLCSharp` (`VideoView` control). `LibVLCSharp` supports virtually all codecs via bundled VLC. Detect codec failure and auto-switch.

### 3. `FileSystemWatcher` Buffer Overflow

**Problem:** Rapid file changes (e.g., git checkout, bulk edit) can overflow the internal buffer, causing the watcher to miss events.

**Workaround:** Increase internal buffer size (`InternalBufferSize = 65536`). Implement polling fallback (30-second scan of wiki directory, compare last-modified timestamps). Re-index on any discrepancy.

### 4. `System.Windows.Clipboard` Thread Affinity

**Problem:** `Clipboard.GetDataObject()` must be called from a thread with STA apartment state. Background threads cannot access clipboard.

**Workaround:** All clipboard operations go through `IClipboardService`, which marshals calls to the UI thread via `Application.Current.Dispatcher.Invoke()`. This is already the pattern in WPF.

### 5. Dialog Z-Order with Topmost Windows

**Problem:** `Topmost` windows (Tier 1, Tier 2) stay above everything, including modal dialogs opened from the MainWindow.

**Workaround:** Before showing a modal dialog from MainWindow, temporarily set `Topmost=false` on Tier 1/2 windows. Restore on dialog close. Alternatively, set the dialog's `Owner` to the main window and use `ShowDialog()` — WPF handles Z-order relative to the owner.

### 6. DataTemplate Memory Leak

**Problem:** `DataTemplate` with `x:Shared="False"` or complex bindings can prevent ViewModel garbage collection, especially in chat with many messages.

**Workaround:** Use `VirtualizingStackPanel` with `VirtualizationMode="Recycling"` for the message list. Implement `IDisposable` on ViewModels and clean up Messenger registrations. Monitor with memory profiler during long chat sessions.

### 7. WebView2 as Rendering Fallback

**Problem:** Markdig-to-FlowDocument rendering is complex, especially for tables and nested lists during streaming.

**Workaround (fallback only):** If FlowDocument rendering proves insufficient for certain Markdown constructs, embed a `WebView2` control for those specific blocks (e.g., complex tables). WebView2 adds ~100MB but only loaded if needed. Primary rendering remains FlowDocument.

### 8. MSIX App Data on Uninstall

**Problem:** MSIX virtualized `%LOCALAPPDATA%` is deleted on uninstall by default. Users lose all data.

**Workaround:** In Settings → Maintenance, add "Export All Data" button (SQLite DB + wiki directory + artifacts → ZIP). Add uninstall warning in documentation. Consider using `%USERPROFILE%\Documents\MySecondBrain\` for wiki directory (outside virtualized storage).

### 9. `NotifyIcon` Disposal on App Exit

**Problem:** `NotifyIcon` must be explicitly disposed. If the app crashes or is killed, a zombie icon remains in the system tray until the user hovers over it.

**Workaround:** `SystemTrayService` implements `IDisposable`. In `App.OnExit()`, dispose the service. Register `AppDomain.CurrentDomain.UnhandledException` handler to attempt cleanup. The zombie icon issue is a known Windows behavior and not fully fixable — document it.

### 10. Slow First-Chance WPF Rendering

**Problem:** First launch after install shows slow window rendering due to JIT compilation and font cache loading.

**Workaround:** Use ReadyToRun (R2R) compilation in `.csproj` to reduce startup JIT. NGen the WPF assemblies. Show a splash screen on startup while the MainWindow initializes.

---

## 12. UIA Capture Pipeline (Tier 1)

The graduated UIA capture pipeline powers Tier 1 Text Action capture scope. It is built as part of Feature 13 (Text Actions & Three-Tier System) on top of existing Wave 1–2 infrastructure (`IHwndCaptureService`, `ITextInjectionService`). The pipeline attempts capture methods in order of reliability, falling back progressively based on the TextAction's `captureScope` flags.

### UIA Patterns Used

| Pattern | Scope Flag | Purpose | Fallback |
|---------|-----------|---------|----------|
| `TextPattern` | `selection` | Read highlighted/selected text | Simulate Ctrl+C, read clipboard, restore original |
| `ValuePattern` | `focusedElement` | Read entire focused textbox/editor content | Skip flag if pattern unavailable |
| `TreeWalker` | `surroundingContext` | Navigate from focused element to capture parent (2 levels up) + sibling text | Skip flag if navigation fails |
| `DocumentRange` | `fullDocument` | Read all accessible text in active window | Full UIA tree traversal collecting text |
| Win32 `PrintWindow`/`BitBlt` | `screenshot` | Capture visual screenshot of active window client area | Proceed without screenshot; fail if sole flag |

### Key Implementation Notes

- **Additive capture:** All flags run, each successful capture adds to total content. Order: selection → focusedElement → surroundingContext → fullDocument → screenshot.
- **Clipboard restoration:** When clipboard fallback is used (Ctrl+C simulation), original clipboard content and format info is saved before capture and restored after. Prevents user clipboard corruption.
- **UIA permissions:** Standard desktop app — no elevation required. Some legacy Win32/custom-drawn apps may not expose full UIA patterns. Pipeline handles this gracefully.
- **Screenshot limitations:** Minimized or fully occluded windows produce incomplete captures. Warning shown in result popup: "Screenshot may be incomplete."
- **Vision-only input:** When `screenshot` is the sole capture flag, the image is sent as a vision attachment. AI prompt should account for vision-only input.
- **Forward reference:** Full spec in [`../vision/features/windows-os-integration.md`](../vision/features/windows-os-integration.md) P9 and [`../vision/flows/tier1-hotkey-rewrite.md`](../vision/flows/tier1-hotkey-rewrite.md).

---

## 13. Diagnostics Logging — Logs Folder & Configuration

### Log File Location

Diagnostic logs are written to `%LOCALAPPDATA%\MySecondBrain\logs\` via Serilog rolling file sink (Feature 3). Log files are structured JSON named `mysecondbrain-{Date}.json`, rotated daily, retained for 30 days.

### Folder Access Edge Cases

- **First launch (folder doesn't exist):** The "Open Logs Folder" button (A11c) creates the folder if it doesn't exist before opening Explorer.
- **Folder inaccessible (permissions, disk error):** Logging falls back to an in-memory buffer (last 1000 events). A warning is shown in Settings → Diagnostics: "⚠️ Log directory is not accessible. Logging to memory buffer only."
- **Files locked during Clear:** If other applications hold file handles to log files during "Clear Logs" (A11d), some files may not be deletable. Error toast: "Could not clear all log files. [N] files could not be deleted."
- **MSIX app data isolation:** The virtualized `%LOCALAPPDATA%` is preserved across updates but not across uninstall. Logs are user-local and do not require admin privileges.

### Configuration Persistence

Nine diagnostic settings are stored as `AppSetting` key-value pairs via `ISettingsRepository` (Feature 4). Changes take effect immediately — no save button. Categories 1-3 ON by default, 4-8 OFF. Log level defaults to Information. See [`data-model.md §AppSetting Keys for Diagnostics`](data-model.md#appsetting-keys-for-diagnostics-v).

### API Key Redaction

A Serilog `IDestructuringPolicy` (`ApiKeyDestructuringPolicy`) is registered at startup to globally redact `ApiKey` string values as `"[REDACTED]"` in all structured log output. This applies across all 8 log categories. See [`abstractions.md §14`](abstractions.md#14-diagnostics--logging--serilog-destructuring-policy).

---

## 14. E2E Testing Conventions

All E2E tests for MySecondBrain follow the conventions documented in the [E2E Authoring Guide](e2e-authoring-guide.md). Every developer writing E2E tests must reference that guide. Key platform-specific notes:

### FlaUI.UIA3 + xUnit

E2E tests use FlaUI.UIA3 (UIAutomationClient) to drive the WPF application through its UIA provider. Tests are written with xUnit and organized under `tests/e2e/MySecondBrain.Tests.E2E/`.

### Fixture Pattern

Use `ICollectionFixture<E2eFixture>` with `[Collection("E2E")]` for all test classes. The `E2eFixture` launches the app once for the entire suite, waits for the UIA tree to populate, and tears down when the suite ends.

### Test Database Isolation

Set `MSB_DB_PATH` environment variable to a test-specific path (`{testOutputDir}\e2e-test.db`) before launching the app. Three files check this variable: [`AppDbContext.cs`](../../src/MySecondBrain.Data/AppDbContext.cs), [`AppDbContextFactory.cs`](../../src/MySecondBrain.Data/AppDbContextFactory.cs), and [`DependencyInjectionConfig.cs`](../../src/MySecondBrain.UI/DependencyInjectionConfig.cs). Delete the test database on fixture teardown.

### UIA Selector Strategy

Prefer `AutomationId` (`x:Name` in XAML) over `Name` over `ControlType` scanning. WPF elements expose `x:Name` values as UIA `AutomationId`. See the authoring guide for naming conventions.

### Message Box Handling

WPF `MessageBox.Show()` creates a separate top-level window. Use `_fixture.Automation.GetDesktop().FindAllDescendants()` to find MessageBox windows by title, then interact with their Button children.

### Known WPF UIA Limitations

- WPF Grid/Panel elements don't expose AutomationId to UIA — use child elements as anchors
- PasswordBox supports the UIA Value pattern — prefer `SetValue()` over keyboard simulation
- ComboBox items are only discoverable in UIA when the dropdown is expanded
- VirtualizingStackPanel hides off-screen items from UIA — scroll items into view before finding them

---

## 4. Agent Skills Platform Adaptation

### bash Tool on Windows

The `bash` tool is named to match Anthropic's `bash_20250124` trained-in schema but executes via Windows shells:

```
bash tool receives command
    │
    ├── Is it a .sh script?
    │   ├── Try: "C:\Program Files\Git\bin\bash.exe" script.sh
    │   ├── Try: wsl bash -c "script.sh"
    │   └── Neither? → Error: "bash or WSL required for .sh scripts"
    │
    ├── Contains heredoc (cat > file << 'EOF')?
    │   └── Redirect: write file via write_to_file or apply_diff tool instead
    │
    └── Everything else → cmd.exe /c "command"
         (python, pip, npm, pandoc — cross-platform, no translation needed)
```

**Bash detection at startup:**
- Check `C:\Program Files\Git\bin\bash.exe` (Git for Windows)
- Check `wsl --status` (Windows Subsystem for Linux)
- Store availability in tool description for model awareness
- If neither available: model adapts — uses write_to_file/apply_diff for file writes, skips .sh scripts

**Shared scripts between skills:** The `scripts/office/` directory is identical in both docx and xlsx skills. At runtime, the skill loader copies bundled scripts to the per-chat workspace so both skills can reference them.

### Workspace Isolation

All `bash` commands execute in per-chat `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/`:

- Working directory set to per-chat workspace path via `Process.StartInfo.WorkingDirectory`
- Absolute paths outside workspace detected and blocked pre-execution (scan for `C:\`, `%`, `~`)
- Wiki directory read-only from bash
- Wiki writes blocked from bash; must go through apply_diff/write_to_file + Write-to-Wiki pipeline
- Workspace created on chat creation, deleted on chat deletion
- Orphan workspace directories (no matching chat in SQLite) cleaned up on app startup

### WebView2 for Artifacts Panel

The artifacts panel uses an embedded Microsoft Edge WebView2 control:

- **Integration:** `Microsoft.Web.WebView2.Wpf` NuGet package
- **Runtime:** Edge WebView2 Runtime (pre-installed on Windows 11, auto-installed on Windows 10 via app installer)
- **Theme bridge:** WPF theme changes (dark/light) injected into WebView2 via `CoreWebView2.ExecuteScriptAsync()` to toggle CSS classes
- **File access:** Artifacts loaded via `file:///` URLs pointing to the artifacts directory
- **Security:** WebView2 runs in isolated context. No access to app filesystem beyond artifacts directory.
- **DPI:** WebView2 respects WPF's PerMonitorV2 DPI awareness. Test at 100%, 125%, 150%, 200% scaling.

### Skill Discovery on Windows

Skills are discovered from two locations on Windows (cross-client path scanning removed per 2026-06-25 vision update):

| Location | Path | Purpose |
|----------|------|---------|
| Built-in | Embedded resources in `MySecondBrain.UI.dll` | 11 Anthropic skills, updated with app |
| User | `%LOCALAPPDATA%/MySecondBrain/skills/` | User-created or downloaded community skills |

The skill loader scans each directory for subdirectories containing `SKILL.md`. Built-in skills are read from embedded resources via `Assembly.GetManifestResourceStream()`. Name collisions: user overrides built-in.

### Embedded Resource Configuration

Skills in `src/MySecondBrain.UI/Skills/anthropic/` must be marked as embedded resources in the `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Skills\anthropic\**\*" />
</ItemGroup>
```

At runtime, the skill loader accesses them via:
```csharp
var assembly = Assembly.GetExecutingAssembly();
var resourceName = $"MySecondBrain.UI.Skills.anthropic.xlsx.SKILL.md";
using var stream = assembly.GetManifestResourceStream(resourceName);
```

### System Prompt Construction on Windows

The system prompt includes platform context for the model:
```
You are running on Windows. Shell commands use Command Prompt (cmd.exe).
- python, pip, npm work as expected
- .sh scripts require Git Bash or WSL
- File paths use backslashes: C:\Users\...
- The workspace is at %WORKSPACE%
```

---

*Platform notes updated 2026-06-24. Added bash-on-Windows adaptation, workspace isolation, WebView2 artifacts panel integration, skill discovery paths, and embedded resource configuration.*
