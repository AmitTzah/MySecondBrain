using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// A floating button that smoothly scrolls the associated ScrollViewer to the bottom.
/// Typically bound to a "IsScrolledUp" or "AutoScrollPaused" property for visibility.
/// </summary>
public class ScrollToBottomButton : System.Windows.Controls.Button
{
    /// <summary>
    /// The ScrollViewer this button should scroll when clicked.
    /// </summary>
    public static readonly DependencyProperty TargetScrollViewerProperty =
        DependencyProperty.Register(nameof(TargetScrollViewer), typeof(ScrollViewer), typeof(ScrollToBottomButton),
            new PropertyMetadata(null));

    public ScrollViewer? TargetScrollViewer
    {
        get => (ScrollViewer?)GetValue(TargetScrollViewerProperty);
        set => SetValue(TargetScrollViewerProperty, value);
    }

    /// <summary>
    /// Duration of the smooth-scroll animation in milliseconds.
    /// </summary>
    public static readonly DependencyProperty ScrollDurationProperty =
        DependencyProperty.Register(nameof(ScrollDuration), typeof(int), typeof(ScrollToBottomButton),
            new PropertyMetadata(200));

    public int ScrollDuration
    {
        get => (int)GetValue(ScrollDurationProperty);
        set => SetValue(ScrollDurationProperty, value);
    }

    static ScrollToBottomButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ScrollToBottomButton),
            new FrameworkPropertyMetadata(typeof(ScrollToBottomButton)));
    }

    public ScrollToBottomButton()
    {
        Content = "\u2193"; // down arrow
        ToolTip = "Scroll to bottom";
        base.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        base.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
        Margin = new Thickness(0, 0, 20, 20);
        Width = 36;
        Height = 36;
        Click += OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        var sv = TargetScrollViewer;
        if (sv is null) return;

        if (sv.ScrollableHeight <= 0) return;

        var targetOffset = sv.ScrollableHeight;
        var startOffset = sv.VerticalOffset;
        var duration = ScrollDuration;

        // Use a DoubleAnimation for smooth scrolling
        var anim = new DoubleAnimation
        {
            From = startOffset,
            To = targetOffset,
            Duration = new Duration(TimeSpan.FromMilliseconds(duration)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(anim, sv);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(ScrollViewer.VerticalOffset)"));
        storyboard.Children.Add(anim);
        storyboard.Begin();
    }
}
