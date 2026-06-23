using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Appearance settings: AppTheme, ChatTheme, FontFamily, FontSize, FontWeight.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Appearance — AppTheme
    // ================================================================

    [ObservableProperty]
    private AppTheme _appTheme = AppTheme.Dark;

    partial void OnAppThemeChanged(AppTheme value)
    {
        _themeProvider.SetAppTheme(value);
        _ = _settingsRepo.SetAsync("AppTheme", value.ToString());
    }

    // ================================================================
    // Appearance — ChatTheme
    // ================================================================

    [ObservableProperty]
    private ChatTheme _chatTheme = ChatTheme.Classic;

    public IReadOnlyList<ChatTheme> ChatThemeOptions { get; } =
    [
        ChatTheme.Classic,
        ChatTheme.Compact,
        ChatTheme.Bubble,
    ];

    partial void OnChatThemeChanged(ChatTheme value)
    {
        _themeProvider.SetChatTheme(value);
        _ = _settingsRepo.SetAsync("ChatTheme", value.ToString());
    }

    // ================================================================
    // Appearance — Font settings
    // ================================================================

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private double _fontSize = 13.0;

    [ObservableProperty]
    private string _fontWeight = "Normal";

    public IReadOnlyList<string> FontFamilyOptions { get; } =
    [
        "Segoe UI",
        "Consolas",
        "Calibri",
        "Arial",
        "Courier New",
        "Georgia",
        "Times New Roman",
        "Verdana",
        "Trebuchet MS",
        "Lucida Console",
    ];

    public static string FontPreviewText => "The quick brown fox jumps over the lazy dog. 0123456789";

    partial void OnFontFamilyChanged(string value)
    {
        if (_suppressFontPersistence) return;
        PersistFontSettings();
    }

    partial void OnFontSizeChanged(double value)
    {
        if (value < 10.0 || value > 24.0)
        {
            FontSize = Math.Clamp(value, 10.0, 24.0);
            return;
        }
        if (_suppressFontPersistence) return;
        PersistFontSettings();
    }

    partial void OnFontWeightChanged(string value)
    {
        if (_suppressFontPersistence) return;
        PersistFontSettings();
    }

    private void PersistFontSettings()
    {
        if (_suppressFontPersistence) return;
        var wpfWeight = FontWeightStringToWpf(FontWeight);
        _themeProvider.SetFontSettings(FontFamily, FontSize, wpfWeight);
        _ = _settingsRepo.SetAsync("FontFamily", FontFamily);
        _ = _settingsRepo.SetAsync("FontSize", FontSize.ToString("F1", CultureInfo.InvariantCulture));
        _ = _settingsRepo.SetAsync("FontWeight", FontWeight);
    }

    private static System.Windows.FontWeight FontWeightStringToWpf(string weight)
    {
        return weight switch
        {
            "Bold" => System.Windows.FontWeights.Bold,
            _ => System.Windows.FontWeights.Normal,
        };
    }

    public IReadOnlyList<string> FontWeightOptions { get; } =
    [
        "Normal",
        "Bold",
    ];
}
