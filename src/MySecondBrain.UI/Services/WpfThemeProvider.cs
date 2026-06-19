using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
// Resolves ambiguity with System.Windows.Forms.Application from UseWindowsForms=true
using Application = System.Windows.Application;
// Resolves ambiguity with System.Drawing.FontFamily from UseWindowsForms=true
using WpfFontFamily = System.Windows.Media.FontFamily;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class WpfThemeProvider : IThemeProvider
{
    private readonly ISettingsRepository _settings;
    private readonly ILogger<WpfThemeProvider> _logger;
    private AppTheme _currentAppTheme = AppTheme.Light;
    private ChatTheme _currentChatTheme = ChatTheme.Classic;

    public event EventHandler<AppTheme>? AppThemeChanged;
    public event EventHandler<ChatTheme>? ChatThemeChanged;

    public WpfThemeProvider(ISettingsRepository settings, ILogger<WpfThemeProvider> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public AppTheme CurrentAppTheme => _currentAppTheme;

    public ChatTheme CurrentChatTheme => _currentChatTheme;

    private static Uri GetThemeUri(AppTheme theme) =>
        new(theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);

    public ResourceDictionary GetAppThemeResources()
    {
        return new ResourceDictionary { Source = GetThemeUri(_currentAppTheme) };
    }

    public void SetAppTheme(AppTheme theme)
    {
        if (theme == _currentAppTheme) return;
        _currentAppTheme = theme;

        var dict = new ResourceDictionary { Source = GetThemeUri(theme) };

        var merged = Application.Current.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.ToString();
            if (src != null && (src.EndsWith("Dark.xaml") || src.EndsWith("Light.xaml")))
                merged.RemoveAt(i);
        }
        merged.Add(dict);

        PersistAndLog("AppTheme", theme.ToString());
        AppThemeChanged?.Invoke(this, theme);
    }

    public void SetChatTheme(ChatTheme theme)
    {
        if (theme == _currentChatTheme) return;
        _currentChatTheme = theme;
        PersistAndLog("ChatTheme", theme.ToString());
        ChatThemeChanged?.Invoke(this, theme);
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

    public string FontFamily => Application.Current?.Resources["FontFamily"] is WpfFontFamily f
        ? f.Source : "Segoe UI";

    public double FontSize => Application.Current?.Resources["FontSize"] is double d ? d : 14.0;

    public FontWeight FontWeight => Application.Current?.Resources["FontWeight"] is FontWeight w
        ? w : FontWeights.Normal;

    public void SetFontSettings(string fontFamily, double fontSize, FontWeight fontWeight)
    {
        if (fontSize < 10 || fontSize > 24)
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be 10-24px");

        Application.Current.Resources["FontFamily"] = new WpfFontFamily(fontFamily);
        Application.Current.Resources["FontSize"] = fontSize;
        Application.Current.Resources["FontWeight"] = fontWeight;

        _ = Task.WhenAll(
                _settings.SetAsync("FontFamily", fontFamily),
                _settings.SetAsync("FontSize", fontSize.ToString(CultureInfo.InvariantCulture)),
                _settings.SetAsync("FontWeight", fontWeight.ToString()))
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogError(t.Exception, "Failed to persist font settings");
            }, TaskScheduler.Default);
    }

    private void PersistAndLog(string key, string value)
    {
        _ = _settings.SetAsync(key, value).ContinueWith(t =>
        {
            if (t.IsFaulted)
                _logger.LogError(t.Exception, "Failed to persist setting {Key}", key);
        }, TaskScheduler.Default);
    }
}
