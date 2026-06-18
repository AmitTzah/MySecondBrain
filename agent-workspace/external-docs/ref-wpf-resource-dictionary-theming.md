# WPF ResourceDictionary DynamicResource Theme Switching

## Pattern: Merged Dictionary Swap

Classic .NET 8.0 WPF approach for runtime theme switching without restart:

1. Define two `ResourceDictionary` files (Dark.xaml, Light.xaml) with the same resource keys
2. Merge one into `Application.Resources.MergedDictionaries` at startup
3. All XAML elements reference theme resources via `DynamicResource` (not `StaticResource`)
4. To switch: clear `MergedDictionaries`, add the other dictionary — all `DynamicResource` references auto-update

```csharp
// Switching themes at runtime
public void SetAppTheme(AppTheme theme)
{
    var dict = theme switch
    {
        AppTheme.Dark => new ResourceDictionary { Source = new Uri("Themes/Dark.xaml", UriKind.Relative) },
        AppTheme.Light => new ResourceDictionary { Source = new Uri("Themes/Light.xaml", UriKind.Relative) },
        _ => throw new ArgumentOutOfRangeException(nameof(theme))
    };

    var merged = Application.Current.Resources.MergedDictionaries;
    merged.Clear();
    merged.Add(dict);
}
```

## Key Rules

- Use `DynamicResource` for ALL themeable values (colors, brushes, font sizes, etc.)
- Use `StaticResource` only for non-themeable constants
- Resource keys must be identical across Dark.xaml and Light.xaml
- `DynamicResource` works on any `DependencyProperty` on any `DependencyObject`

## Example Resource Keys

```xml
<!-- Dark.xaml -->
<SolidColorBrush x:Key="AppBackground" Color="#1E1E1E"/>
<SolidColorBrush x:Key="AppForeground" Color="#E0E0E0"/>
<SolidColorBrush x:Key="SidebarBackground" Color="#252526"/>
<SolidColorBrush x:Key="ContentBackground" Color="#1E1E1E"/>

<!-- Light.xaml -->
<SolidColorBrush x:Key="AppBackground" Color="#FFFFFF"/>
<SolidColorBrush x:Key="AppForeground" Color="#1A1A1A"/>
<SolidColorBrush x:Key="SidebarBackground" Color="#F5F5F5"/>
<SolidColorBrush x:Key="ContentBackground" Color="#FFFFFF"/>
```

## Source
- Microsoft WPF Documentation: ResourceDictionary, DynamicResource markup extension
- Context7 /dotnet/wpf retrieved 2026-06-18
