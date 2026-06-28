using System.Windows;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IThemeProvider
{
    ChatTheme CurrentChatTheme { get; }

    DataTemplate GetChatMessageTemplate(ChatTheme theme);

    void SetChatTheme(ChatTheme theme);

    event EventHandler<ChatTheme>? ChatThemeChanged;
}
