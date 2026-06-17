using System.Windows;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IThemeProvider
{
    AppTheme CurrentAppTheme { get; }
    ChatTheme CurrentChatTheme { get; }

    ResourceDictionary GetAppThemeResources();
    DataTemplate GetChatMessageTemplate(ChatTheme theme);

    void SetAppTheme(AppTheme theme);
    void SetChatTheme(ChatTheme theme);

    string FontFamily { get; }
    double FontSize { get; }
    FontWeight FontWeight { get; }
    void SetFontSettings(string fontFamily, double fontSize, FontWeight fontWeight);

    event EventHandler<AppTheme>? AppThemeChanged;
    event EventHandler<ChatTheme>? ChatThemeChanged;
}
