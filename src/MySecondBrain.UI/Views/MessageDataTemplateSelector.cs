using System.Windows;
using System.Windows.Controls;
using Msg = MySecondBrain.Core.Models.Message;

namespace MySecondBrain.UI.Views;

/// <summary>
/// Selects the appropriate DataTemplate for a chat message based on its Role.
/// Supports User, Assistant, and System message roles (case-insensitive).
/// </summary>
public class MessageDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserMessageTemplate { get; set; }
    public DataTemplate? AssistantMessageTemplate { get; set; }
    public DataTemplate? SystemMessageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is Msg message)
        {
            var role = message.Role ?? string.Empty;
            if (role.Equals("User", StringComparison.OrdinalIgnoreCase))
                return UserMessageTemplate;
            if (role.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
                return AssistantMessageTemplate;
            if (role.Equals("System", StringComparison.OrdinalIgnoreCase))
                return SystemMessageTemplate;
            return AssistantMessageTemplate;
        }

        return base.SelectTemplate(item, container);
    }
}
