# Frontend UI Knowledge — MySecondBrain

> **Global UI components, frontend patterns, state management structures, and styling conventions.**  
> Source: Feature 1/245 — .NET 8.0 WPF Solution Scaffold.

---

## 1. UI Technology Stack

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| Framework | WPF (Windows Presentation Foundation) | .NET 8.0 | Desktop UI framework |
| MVVM Toolkit | CommunityToolkit.Mvvm | 8.x | ObservableObject, [RelayCommand], [ObservableProperty], WeakReferenceMessenger |
| Charts | LiveCharts2 | 2.x | WPF-native charting for usage dashboard |
| Spell Check | WeCantSpell.Hunspell | 4.x | Hunspell .NET port for spell checking |
| Auto-Update | Autoupdater.NET.Official | 2.x | Auto-update mechanism |
| System Tray | WinForms `NotifyIcon` | Platform | System tray integration (via `UseWindowsForms=true`) |

---

## 2. MVVM Pattern — CommunityToolkit.Mvvm

### 2.1 Base Class

All ViewModels inherit from `ObservableObject` (CommunityToolkit.Mvvm.ComponentModel):

```csharp
public partial class MyViewModel : ObservableObject
{
}
```

### 2.2 Observable Properties — `[ObservableProperty]`

Source generator eliminates manual property change notification:

```csharp
[ObservableProperty]
private string _searchText;

[ObservableProperty]
private bool _isLoading;
```

The generator creates public properties `SearchText` and `IsLoading` with `OnPropertyChanged` calls, plus partial methods `OnSearchTextChanged` and `OnIsLoadingChanged` for hooking change logic.

### 2.3 Relay Commands — `[RelayCommand]`

Source generator for command binding, including async support:

```csharp
[RelayCommand]
private async Task SendMessageAsync()
{
    // Command implementation
}

[RelayCommand]
private void ClearSearch()
{
    // Synchronous command
}
```

Generates `SendMessageCommand` and `ClearSearchCommand` properties with `CanExecute` support.

### 2.4 Cross-VM Communication — WeakReferenceMessenger

`WeakReferenceMessenger.Default` for decoupled ViewModel-to-ViewModel messaging:

```csharp
// Send
WeakReferenceMessenger.Default.Send(new ChatMessageSentMessage(threadId));

// Receive (registered in ViewModel constructor)
WeakReferenceMessenger.Default.Register<ChatMessageSentMessage>(this, (r, m) => { ... });
```

Prevents memory leaks (weak references) and avoids tight coupling between ViewModels.

---

## 3. DI Container Bootstrap Pattern (App.xaml.cs)

```csharp
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services, repositories, and ViewModels registered here
        services.AddSingleton<MainWindow>();
    }
}
```

**Key rule:** `StartupUri` is intentionally omitted from `App.xaml`. WPF would auto-create a second, non-DI `MainWindow` instance if `StartupUri` were set.

---

## 4. Theming — DynamicResource

- **Location:** `MySecondBrain.UI/Themes/`
- **Files:** `Dark.xaml`, `Light.xaml` (ResourceDictionary)
- **Mechanism:** `DynamicResource` markup extension for runtime theme switching
- **Application:** Merged into `Application.Resources` in `App.xaml`
- **Theme switching:** Swap the merged dictionary at runtime (no app restart)

---

## 5. UI Project Directory Convention

```
MySecondBrain.UI/
├── App.xaml / App.xaml.cs        # Application entry point with DI container
├── App.manifest                  # PerMonitorV2 DPI + Windows 10/11 supportedOS
├── MainWindow.xaml / .cs         # Main studio window
├── Views/                        # WPF XAML views (UserControl/Window)
├── ViewModels/                   # ViewModels (ObservableObject subclasses)
├── Controls/                     # Custom controls, content block renderers
├── Themes/                       # Dark.xaml, Light.xaml ResourceDictionaries
├── Converters/                   # IValueConverter implementations
├── Services/                     # UI-specific services (ThemeProvider, SystemTrayService)
└── Resources/                    # Icons, fonts, app.ico, images
```

---

## 6. Window Management

### MainWindow
- Default size: 1400×900, minimum 800×600
- `WindowStartupLocation="CenterScreen"`
- `ShutdownMode="OnMainWindowClose"` on `App.xaml`

### Three-Tier Window Model

| Tier | Window Type | Style | Activation |
|------|------------|-------|------------|
| Tier 1 | Overlay pill | `WS_EX_NOACTIVATE`, `WS_EX_TOPMOST`, `WS_EX_TRANSPARENT` | Never activates |
| Tier 2 | Command bar | Floating tool window | Activates on use |
| Tier 3 | Main studio | Standard window with chrome | Normal activation |

---

## 7. App.manifest — DPI & OS Support

```xml
<windowsSettings>
    <dpiAware xmlns="...">true</dpiAware>
    <dpiAwareness xmlns="...">PerMonitorV2</dpiAwareness>
</windowsSettings>
<compatibility>
    <application>
        <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>  <!-- Windows 10 -->
        <supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}"/>  <!-- Windows 11 -->
    </application>
</compatibility>
```

---

## 8. System Tray Integration

- `UseWindowsForms=true` in `.csproj` enables `System.Windows.Forms.NotifyIcon`
- No WinForms controls used — only `NotifyIcon` for system tray
- `NotifyIcon` with context menu: Show/Hide, Exit
- Minimize to tray behavior: hide main window, show `NotifyIcon`

---

## 9. Code Style (UI-Specific)

- 4-space indentation, file-scoped namespaces
- XAML: `x:Class` with fully qualified namespace
- XAML resources: `DynamicResource` for themeable values, `StaticResource` for constants
- `x:Name` for elements referenced in code-behind, `x:Key` for resource keys
- Converters registered in `App.xaml` resources or View-scoped resources
