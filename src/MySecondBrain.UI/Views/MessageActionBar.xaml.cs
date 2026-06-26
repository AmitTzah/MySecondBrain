using System.Windows;
using System.Windows.Controls;
using Msg = MySecondBrain.Core.Models.Message;

namespace MySecondBrain.UI.Views;

/// <summary>
/// Per-message hover-visible action bar with Star, Copy MD, Copy Rich,
/// Edit, Delete, and Regenerate buttons.
/// Updates visibility based on message role (Regenerate visible only for assistant messages).
/// </summary>
public partial class MessageActionBar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(Msg), typeof(MessageActionBar),
            new PropertyMetadata(null, OnMessageChanged));

    public Msg? Message
    {
        get => (Msg?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public MessageActionBar()
    {
        InitializeComponent();

        StarBtn.Click += (_, _) => OnAction("Star");
        CopyMdBtn.Click += (_, _) => OnAction("CopyMarkdown");
        CopyRichBtn.Click += (_, _) => OnAction("CopyRich");
        EditBtn.Click += (_, _) => OnAction("Edit");
        DeleteBtn.Click += (_, _) => OnAction("Delete");
        RegenerateBtn.Click += (_, _) => OnAction("Regenerate");
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MessageActionBar bar && e.NewValue is Msg msg)
        {
            // Show Regenerate button only for assistant messages (case-insensitive)
            bar.RegenerateBtn.Visibility = msg.Role?.Equals("Assistant", StringComparison.OrdinalIgnoreCase) == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Raised when the user clicks an action button.
    /// The parameter is the action name (Star, CopyMarkdown, CopyRich, Edit, Delete, Regenerate).
    /// </summary>
    public event EventHandler<string>? ActionClicked;

    private void OnAction(string action)
    {
        ActionClicked?.Invoke(this, action);
    }
}
