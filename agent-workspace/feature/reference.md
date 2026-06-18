# Feature Reference: App Shell, Navigation & Theming

## Global & Shared Documentation

### WPF ResourceDictionary Theme Switching
- **Pattern:** Merged dictionary swap via `Application.Current.Resources.MergedDictionaries.Clear()` + `Add()`
- **All XAML uses `DynamicResource`** (not `StaticResource`) for themeable values
- **Resource keys** identical across `Dark.xaml` and `Light.xaml`
- **Full reference:** `agent-workspace/external-docs/ref-wpf-resource-dictionary-theming.md`

### MVVM + DI Patterns
- **Base class:** `ObservableObject` (CommunityToolkit.Mvvm)
- **Properties:** `[ObservableProperty]` source generator
- **Commands:** `[RelayCommand]` for async/sync binding
- **DI lifetime:** Services=Singleton, ViewModels=Transient, `MainWindow`=Singleton
- **Full reference:** `agent-workspace/knowledge/architecture.md`, `agent-workspace/knowledge/frontend-ui.md`

### ISettingsRepository API
- `Task<string?> GetAsync(string key)` — raw string value
- `Task<T?> GetAsync<T>(string key)` — JSON-deserialized value
- `Task SetAsync(string key, string value)` — upsert string
- `Task SetAsync<T>(string key, T value)` — serialize to JSON then upsert
- `Task DeleteAsync(string key)` — remove key
- Persists to SQLite `AppSettings` table via EF Core

### Existing DI Registrations (Relevant)
```csharp
services.AddSingleton<IThemeProvider, WpfThemeProvider>();
services.AddSingleton<IContentRendererRegistry, ContentRendererRegistry>();
services.AddSingleton<IContentBlockRenderer, MarkdownTextRenderer>();
services.AddSingleton<IContentBlockRenderer, CodeBlockRenderer>();
services.AddSingleton<IContentBlockRenderer, ArtifactReferenceRenderer>();
services.AddSingleton<IContentBlockRenderer, ImageRenderer>();
services.AddSingleton<IContentBlockRenderer, MediaRenderer>();
services.AddSingleton<IContentBlockRenderer, ThinkingRenderer>();
services.AddSingleton<IContentBlockRenderer, ToolCallRenderer>();
services.AddSingleton<ISettingsRepository, SettingsRepository>();
services.AddTransient<MainWindowViewModel>();
// ... all 10 other ViewModels registered as Transient
services.AddSingleton<MainWindow>();
```

---

## Step-Specific Documentation

### Step 1: Dark & Light Theme ResourceDictionaries + App.xaml wiring

- **Library:** WPF `ResourceDictionary` (platform, no NuGet needed)
- **Import:** `System.Windows.ResourceDictionary`, `System.Windows.Media`
- **Key XAML pattern:**
```xml
<!-- In App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Themes/Dark.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

```xml
<!-- In MainWindow.xaml -->
<Window Background="{DynamicResource AppBackground}">
```

```xml
<!-- Dark.xaml key structure -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="AppBackground" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="AppForeground" Color="#E0E0E0"/>
    <SolidColorBrush x:Key="SidebarBackground" Color="#252526"/>
    <!-- ... 22+ more resources -->
</ResourceDictionary>
```

- **Resource key catalog (Dark theme values):**
  - `AppBackground`: `#1E1E1E` (dark editor background)
  - `AppForeground`: `#E0E0E0`
  - `SidebarBackground`: `#252526` (VS Code sidebar)
  - `SidebarForeground`: `#CCCCCC`
  - `ContentBackground`: `#1E1E1E`
  - `ContentForeground`: `#D4D4D4`
  - `PanelBackground`: `#252526`
  - `PanelForeground`: `#CCCCCC`
  - `TabBarBackground`: `#2D2D2D`
  - `TabActiveBackground`: `#1E1E1E`
  - `TabInactiveBackground`: `#2D2D2D`
  - `HeaderBackground`: `#2D2D2D`
  - `InputBackground`: `#2D2D2D`
  - `AccentBrush`: `#2563EB` (blue accent)
  - `AccentForeground`: `#FFFFFF`
  - `BorderBrush`: `#3E3E3E`
  - `SubtleBrush`: `#555555`
  - `SuccessBrush`: `#22C55E`
  - `WarningBrush`: `#F59E0B`
  - `ErrorBrush`: `#EF4444`
  - `ScrollBarBrush`: `#424242`
  - `GridSplitterBrush`: `#3E3E3E`
  - `NavActiveBackground`: `#2563EB`
  - `NavInactiveForeground`: `#999999`
  - `FontFamily`: `Segoe UI`
  - `FontSize`: `14` (double)

- **Light theme corresponding values:**
  - `AppBackground`: `#FFFFFF`
  - `AppForeground`: `#1A1A1A`
  - `SidebarBackground`: `#F5F5F5`
  - `SidebarForeground`: `#333333`
  - `ContentBackground`: `#FFFFFF`
  - `ContentForeground`: `#1A1A1A`
  - `PanelBackground`: `#FAFAFA`
  - `PanelForeground`: `#333333`
  - `TabBarBackground`: `#EEEEEE`
  - `TabActiveBackground`: `#FFFFFF`
  - `TabInactiveBackground`: `#EEEEEE`
  - `HeaderBackground`: `#F5F5F5`
  - `InputBackground`: `#FFFFFF`
  - `BorderBrush`: `#DDDDDD`
  - `ScrollBarBrush`: `#CCCCCC`
  - `GridSplitterBrush`: `#DDDDDD`
  - `NavInactiveForeground`: `#666666`
  - (AccentBrush, SuccessBrush, WarningBrush, ErrorBrush same as Dark)

---

### Step 2: MainWindow Three-Region Grid Shell with GridSplitters & Sidebar Nav

- **Library:** WPF `Grid`, `GridSplitter` (platform)
- **Key XAML pattern:**
```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="280" MinWidth="150" MaxWidth="500"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="320" MinWidth="200" MaxWidth="500"/>
    </Grid.ColumnDefinitions>

    <!-- Sidebar (Column 0) -->
    <StackPanel Grid.Column="0" Background="{DynamicResource SidebarBackground}">
        <Button Content="💬 Chats" Command="{Binding NavigateCommand}" CommandParameter="Chats"
                Style="{StaticResource NavButtonStyle}"/>
        <!-- ... 5 more nav buttons -->
    </StackPanel>

    <!-- GridSplitter (Column 1) -->
    <GridSplitter Grid.Column="1" Width="4" Background="{DynamicResource GridSplitterBrush}"
                  ResizeBehavior="PreviousAndNext" HorizontalAlignment="Stretch"/>

    <!-- Center Content (Column 2) -->
    <Grid Grid.Column="2" Background="{DynamicResource ContentBackground}"/>

    <!-- GridSplitter (Column 3) -->
    <GridSplitter Grid.Column="3" Width="4" Background="{DynamicResource GridSplitterBrush}"
                  ResizeBehavior="PreviousAndNext" HorizontalAlignment="Stretch"/>

    <!-- Right Panel (Column 4) -->
    <Grid Grid.Column="4" Background="{DynamicResource PanelBackground}"/>
</Grid>
```

- **Nav button style:**
```xml
<Style x:Key="NavButtonStyle" TargetType="RadioButton">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="{DynamicResource NavInactiveForeground}"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Padding" Value="12,8"/>
    <Setter Property="Template">...</Setter>
    <Style.Triggers>
        <Trigger Property="IsChecked" Value="True">
            <Setter Property="Background" Value="{DynamicResource NavActiveBackground}"/>
            <Setter Property="Foreground" Value="{DynamicResource AccentForeground}"/>
        </Trigger>
    </Style.Triggers>
</Style>
```

---

### Step 3: Screen Navigation System + 8 Screen UserControl Shells

- **Library:** WPF `ContentControl`, `DataTemplate` (platform)
- **ViewModel pattern:**
```csharp
public enum ScreenType { Chats, Wiki, Media, Artifacts, Usage, Settings }

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ScreenType _selectedScreen = ScreenType.Chats;

    [RelayCommand]
    private void Navigate(string screenName)
    {
        if (Enum.TryParse<ScreenType>(screenName, out var screen))
            SelectedScreen = screen;
    }
}
```

- **ContentControl + DataTemplate mapping (XAML):**
```xml
<ContentControl Grid.Column="2" Content="{Binding SelectedScreen}"
                Background="{DynamicResource ContentBackground}">
    <ContentControl.Resources>
        <DataTemplate DataType="{x:Type local:ScreenType}" x:Key="ChatsTemplate">
            <views:ChatView/>
        </DataTemplate>
        <!-- ... but implicit DataTemplate by x:Type won't work with enum.
             Use a DataTemplateSelector instead: -->
    </ContentControl.Resources>
</ContentControl>
```

- **Better approach — DataTemplateSelector:**
```csharp
public class ScreenTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ChatsTemplate { get; set; }
    public DataTemplate? WikiTemplate { get; set; }
    // ... one property per screen

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            ScreenType.Chats => ChatsTemplate,
            ScreenType.Wiki => WikiTemplate,
            ScreenType.Media => MediaTemplate,
            ScreenType.Artifacts => ArtifactsTemplate,
            ScreenType.Usage => UsageTemplate,
            ScreenType.Settings => SettingsTemplate,
            _ => null
        };
    }
}
```

- **Each screen shell binds to its existing ViewModel stub:**
```xml
<!-- Example: SettingsView.xaml -->
<UserControl x:Class="MySecondBrain.UI.Views.SettingsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:MySecondBrain.UI.ViewModels"
             Background="{DynamicResource ContentBackground}">
    <UserControl.DataContext>
        <vm:SettingsViewModel/> <!-- Will be overridden by DI View resolution later -->
    </UserControl.DataContext>
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="200"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <!-- Category sidebar -->
        <ListBox Grid.Column="0" Background="{DynamicResource SidebarBackground}">
            <ListBoxItem>🔑 Providers</ListBoxItem>
            <!-- ... 15 more categories -->
        </ListBox>
        <!-- Content area -->
        <Border Grid.Column="1" Background="{DynamicResource ContentBackground}">
            <TextBlock Text="Select a category" Foreground="{DynamicResource SubtleBrush}"
                       HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="14"/>
        </Border>
    </Grid>
</UserControl>
```

---

### Step 4: WpfThemeProvider Implementation + Theme Toggle Button

- **Library:** WPF `ResourceDictionary`, `Application.Current.Resources` (platform)
- **External ref:** `agent-workspace/external-docs/ref-wpf-resource-dictionary-theming.md`
- **Full WpfThemeProvider implementation pattern:**
```csharp
public class WpfThemeProvider : IThemeProvider
{
    private readonly ISettingsRepository _settings;
    private AppTheme _currentAppTheme = AppTheme.Dark;
    private ChatTheme _currentChatTheme = ChatTheme.Classic;

    public WpfThemeProvider(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public AppTheme CurrentAppTheme => _currentAppTheme;
    public ChatTheme CurrentChatTheme => _currentChatTheme;
    public string FontFamily => Application.Current.Resources["FontFamily"] is FontFamily f
        ? f.Source : "Segoe UI";
    public double FontSize => Application.Current.Resources["FontSize"] is double d ? d : 14.0;
    public FontWeight FontWeight => FontWeights.Normal;

    public event EventHandler<AppTheme>? AppThemeChanged;
    public event EventHandler<ChatTheme>? ChatThemeChanged;

    public ResourceDictionary GetAppThemeResources()
    {
        var uri = _currentAppTheme == AppTheme.Dark
            ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        return new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
    }

    public void SetAppTheme(AppTheme theme)
    {
        if (theme == _currentAppTheme) return;
        _currentAppTheme = theme;

        var uri = theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(dict);

        _ = _settings.SetAsync("AppTheme", theme.ToString());
        AppThemeChanged?.Invoke(this, theme);
    }

    public void SetFontSettings(string fontFamily, double fontSize, FontWeight fontWeight)
    {
        if (fontSize < 10 || fontSize > 24)
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be 10-24px");

        Application.Current.Resources["FontFamily"] = new FontFamily(fontFamily);
        Application.Current.Resources["FontSize"] = fontSize;
        Application.Current.Resources["FontWeight"] = fontWeight;

        _ = Task.WhenAll(
            _settings.SetAsync("FontFamily", fontFamily),
            _settings.SetAsync("FontSize", fontSize.ToString(CultureInfo.InvariantCulture)),
            _settings.SetAsync("FontWeight", fontWeight.ToString()));
    }

    public DataTemplate GetChatMessageTemplate(ChatTheme theme) =>
        theme switch
        {
            ChatTheme.Classic => Application.Current.Resources["ClassicMessageTemplate"] as DataTemplate
                ?? new DataTemplate(),
            ChatTheme.Compact => Application.Current.Resources["CompactMessageTemplate"] as DataTemplate
                ?? new DataTemplate(),
            ChatTheme.Bubble => Application.Current.Resources["BubbleMessageTemplate"] as DataTemplate
                ?? new DataTemplate(),
            _ => new DataTemplate()
        };

    public void SetChatTheme(ChatTheme theme)
    {
        if (theme == _currentChatTheme) return;
        _currentChatTheme = theme;
        _ = _settings.SetAsync("ChatTheme", theme.ToString());
        ChatThemeChanged?.Invoke(this, theme);
    }
}
```

- **ToggleThemeCommand (MainWindowViewModel):**
```csharp
[RelayCommand]
private void ToggleTheme()
{
    var newTheme = _themeProvider.CurrentAppTheme == AppTheme.Dark
        ? AppTheme.Light : AppTheme.Dark;
    _themeProvider.SetAppTheme(newTheme);
}
```

- **☀/🌙 button in ChatView header:**
```xml
<Button Command="{Binding ToggleThemeCommand}" Content="☀" 
        ToolTip="Toggle dark/light mode"
        Background="Transparent" BorderThickness="0" FontSize="14"/>
```

---

### Step 5: Apply Saved Theme & Font Settings on Startup

- **Library:** `ISettingsRepository` (already registered), `IThemeProvider` (already registered)
- **Code placement:** In `App.xaml.cs` `OnStartup`, after `db.Database.Migrate()` and before `mainWindow.Show()`
- **Pattern:**
```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    var services = new ServiceCollection();
    ConfigureServices(services);
    _serviceProvider = services.BuildServiceProvider();

    // ... db.Migrate(); startupLogger.LogInformation("MySecondBrain started");

    // Restore saved theme and font
    var themeProvider = _serviceProvider.GetRequiredService<IThemeProvider>();
    var settings = _serviceProvider.GetRequiredService<ISettingsRepository>();

    var savedTheme = await settings.GetAsync("AppTheme");
    if (savedTheme is not null && Enum.TryParse<AppTheme>(savedTheme, out var theme))
        themeProvider.SetAppTheme(theme);

    var savedFontFamily = await settings.GetAsync("FontFamily");
    var savedFontSize = await settings.GetAsync("FontSize");
    var savedFontWeight = await settings.GetAsync("FontWeight");
    var fontSize = 14.0;
    var fontWeight = FontWeights.Normal;
    if (savedFontSize is not null)
        double.TryParse(savedFontSize, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize);
    if (savedFontWeight is not null && Enum.TryParse<FontWeight>(savedFontWeight, out var parsedWeight))
        fontWeight = parsedWeight;
    if (savedFontFamily is not null)
    {
        themeProvider.SetFontSettings(savedFontFamily, fontSize, fontWeight);
    }

    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
    mainWindow.Show();
}
```

- **SQLite verification queries:**
```sql
-- Check saved theme
SELECT Key, Value FROM Settings WHERE Key = 'AppTheme';

-- Check saved font
SELECT Key, Value FROM Settings WHERE Key IN ('FontFamily', 'FontSize', 'FontWeight');
```

---

### Step 6: Font Size Quick-Adjust Buttons + Font Settings Persistence

- **Library:** WPF Button binding, `IThemeProvider.SetFontSettings()` (already implemented)
- **ViewModel additions (MainWindowViewModel):**
```csharp
[ObservableProperty]
private double _fontSizeDisplay;

partial void OnSelectedScreenChanged(ScreenType value)
{
    FontSizeDisplay = _themeProvider.FontSize;
}

[RelayCommand]
private void IncreaseFont()
{
    var newSize = Math.Min(_themeProvider.FontSize + 1, 24);
    _themeProvider.SetFontSettings(_themeProvider.FontFamily, newSize, _themeProvider.FontWeight);
    FontSizeDisplay = newSize;
}

[RelayCommand]
private void DecreaseFont()
{
    var newSize = Math.Max(_themeProvider.FontSize - 1, 10);
    _themeProvider.SetFontSettings(_themeProvider.FontFamily, newSize, _themeProvider.FontWeight);
    FontSizeDisplay = newSize;
}
```

- **A⁻/A⁺ buttons in ChatView header:**
```xml
<StackPanel Orientation="Horizontal" VerticalAlignment="Center">
    <Button Content="A⁻" Command="{Binding DecreaseFontCommand}"
            Background="Transparent" BorderThickness="0" FontSize="13"
            ToolTip="Decrease font size"/>
    <TextBlock Text="{Binding FontSizeDisplay}" VerticalAlignment="Center"
               FontSize="11" Foreground="{DynamicResource SubtleBrush}"
               MinWidth="20" TextAlignment="Center"/>
    <Button Content="A⁺" Command="{Binding IncreaseFontCommand}"
            Background="Transparent" BorderThickness="0" FontSize="13"
            ToolTip="Increase font size"/>
</StackPanel>
```

---

### Step 7: Three Chat Visual Theme DataTemplates (Classic/Compact/Bubble)

- **Library:** WPF `DataTemplate` (platform), `ComboBox` binding
- **Add to MainWindowViewModel:**
```csharp
[ObservableProperty]
private ChatTheme _currentChatTheme = ChatTheme.Classic;

[RelayCommand]
private void SetChatTheme(string themeName)
{
    if (Enum.TryParse<ChatTheme>(themeName, out var theme))
    {
        _themeProvider.SetChatTheme(theme);
        CurrentChatTheme = theme;
    }
}
```

- **Theme selector ComboBox in ChatView header:**
```xml
<ComboBox SelectedItem="{Binding CurrentChatTheme}"
          ItemsSource="{Binding Source={StaticResource ChatThemeOptions}}"
          FontSize="11" Width="100">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}"/>
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

- **Three DataTemplate designs (in ChatView.xaml Resources):**

**Classic Template:**
```xml
<DataTemplate x:Key="ClassicMessageTemplate">
    <StackPanel Margin="0,0,0,20">
        <TextBlock FontSize="10" FontWeight="Bold" Foreground="{DynamicResource SubtleBrush}"
                   Text="{Binding Role}" Margin="0,0,0,2"/>
        <Border Padding="12,8" CornerRadius="6" BorderThickness="1"
                BorderBrush="{DynamicResource BorderBrush}"
                Background="{DynamicResource ContentBackground}">
            <TextBlock Text="{Binding Content}" FontSize="{DynamicResource FontSize}"
                       TextWrapping="Wrap" Foreground="{DynamicResource ContentForeground}"/>
        </Border>
        <TextBlock FontSize="10" Foreground="{DynamicResource SubtleBrush}"
                   Text="{Binding Timestamp}" Margin="0,2,0,0"/>
    </StackPanel>
</DataTemplate>
```

**Compact Template:**
```xml
<DataTemplate x:Key="CompactMessageTemplate">
    <StackPanel Margin="0,0,0,8">
        <StackPanel Orientation="Horizontal">
            <Ellipse Width="8" Height="8" Margin="0,0,6,0">
                <Ellipse.Style>
                    <Style TargetType="Ellipse">
                        <Setter Property="Fill" Value="{DynamicResource AccentBrush}"/>
                    </Style>
                </Ellipse.Style>
            </Ellipse>
            <TextBlock Text="{Binding Content}" FontSize="{DynamicResource FontSize}"
                       TextWrapping="Wrap" Foreground="{DynamicResource ContentForeground}"/>
        </StackPanel>
    </StackPanel>
</DataTemplate>
```

**Bubble Template:**
```xml
<DataTemplate x:Key="BubbleMessageTemplate">
    <Grid Margin="0,0,0,12">
        <Border Padding="12,8" CornerRadius="12" BorderThickness="1"
                BorderBrush="{DynamicResource AccentBrush}"
                Background="{DynamicResource AccentBrush}33" MaxWidth="400">
            <StackPanel>
                <TextBlock Text="{Binding Content}" FontSize="{DynamicResource FontSize}"
                           TextWrapping="Wrap" Foreground="{DynamicResource ContentForeground}"/>
                <TextBlock Text="{Binding Timestamp}" FontSize="9"
                           Foreground="{DynamicResource SubtleBrush}"
                           HorizontalAlignment="Right" Margin="0,4,0,0"/>
            </StackPanel>
        </Border>
    </Grid>
</DataTemplate>
```

---

### Step 8: ContentRendererRegistry Priority Fix + DI Resolution Verification

- **Library:** `IEnumerable<T>` DI injection, xUnit
- **Priority fixes in 6 renderer stub files:**
  - `CodeBlockRenderer.cs`: `public int Priority => 90;` → `public int Priority => 200;`
  - `ArtifactReferenceRenderer.cs`: `public int Priority => 80;` → `public int Priority => 300;`
  - `ImageRenderer.cs`: `public int Priority => 70;` → `public int Priority => 400;`
  - `MediaRenderer.cs`: `public int Priority => 60;` → `public int Priority => 500;`
  - `ThinkingRenderer.cs`: `public int Priority => 50;` → `public int Priority => 600;`
  - `ToolCallRenderer.cs`: `public int Priority => 40;` → `public int Priority => 700;`

- **Registry constructor fix:**
```csharp
// ContentRendererRegistry constructor — add priority sort
public ContentRendererRegistry(IEnumerable<IContentBlockRenderer> renderers)
{
    _renderers.AddRange(renderers.OrderBy(r => r.Priority));
}
```

- **Unit test (add to DataLayerTests.cs or new RendererTests.cs):**
```csharp
[Fact]
public void ContentRendererRegistry_ResolvesAllSevenRenderersInCorrectPriorityOrder()
{
    // Arrange
    var services = new ServiceCollection();
    App.ConfigureServices(services);
    var provider = services.BuildServiceProvider(
        new ServiceProviderOptions { ValidateOnBuild = true });

    // Act
    var registry = provider.GetRequiredService<IContentRendererRegistry>();
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
```

- **CLI test command:**
```bash
cd c:\Users\Amit\Projects\MySecondBrain
dotnet test tests/unit/MySecondBrain.Tests.Unit/ --filter "FullyQualifiedName~ContentRendererRegistry"
```
