using System.Windows;
using System.Windows.Controls;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Views;

public class ScreenTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ChatsTemplate { get; set; }
    public DataTemplate? WikiTemplate { get; set; }
    public DataTemplate? MediaTemplate { get; set; }
    public DataTemplate? ArtifactsTemplate { get; set; }
    public DataTemplate? UsageTemplate { get; set; }
    public DataTemplate? SettingsTemplate { get; set; }

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
