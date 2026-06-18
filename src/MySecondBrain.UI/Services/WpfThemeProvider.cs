using System.Windows;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class WpfThemeProvider : IThemeProvider
{
#pragma warning disable CS0414, CS0067
    public event EventHandler<AppTheme>? AppThemeChanged;
    public event EventHandler<ChatTheme>? ChatThemeChanged;
#pragma warning restore CS0414, CS0067

    public AppTheme CurrentAppTheme => AppTheme.Dark;
    public ChatTheme CurrentChatTheme => ChatTheme.Classic;

    public ResourceDictionary GetAppThemeResources() => new();

    public DataTemplate GetChatMessageTemplate(ChatTheme theme) => new();

    public void SetAppTheme(AppTheme theme) { }

    public void SetChatTheme(ChatTheme theme) { }

    public string FontFamily => "Segoe UI";

    public double FontSize => 14.0;

    public FontWeight FontWeight => FontWeights.Normal;

    public void SetFontSettings(string fontFamily, double fontSize, FontWeight fontWeight) { }
}
