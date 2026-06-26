using System.Windows;
using System.Windows.Media;
using MySecondBrain.UI.Styles;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// A colored progress-bar control that displays token context window fill level.
/// Color changes based on percentage: green (under 70%), yellow (70-90%), red (over 90%).
/// </summary>
public partial class TokenContextBar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ContextPercentageProperty =
        DependencyProperty.Register(nameof(ContextPercentage), typeof(double), typeof(TokenContextBar),
            new PropertyMetadata(0.0, OnPercentageChanged));

    public double ContextPercentage
    {
        get => (double)GetValue(ContextPercentageProperty);
        set => SetValue(ContextPercentageProperty, Math.Clamp(value, 0, 1));
    }

    public static readonly DependencyProperty ContextTokensProperty =
        DependencyProperty.Register(nameof(ContextTokens), typeof(int), typeof(TokenContextBar),
            new PropertyMetadata(0, OnPercentageChanged));

    public int ContextTokens
    {
        get => (int)GetValue(ContextTokensProperty);
        set => SetValue(ContextTokensProperty, value);
    }

    public static readonly DependencyProperty ContextMaxTokensProperty =
        DependencyProperty.Register(nameof(ContextMaxTokens), typeof(int), typeof(TokenContextBar),
            new PropertyMetadata(128000, OnPercentageChanged));

    public int ContextMaxTokens
    {
        get => (int)GetValue(ContextMaxTokensProperty);
        set => SetValue(ContextMaxTokensProperty, value);
    }

    public TokenContextBar()
    {
        InitializeComponent();
        UpdateFill();
    }

    private static void OnPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TokenContextBar bar)
            bar.UpdateFill();
    }

    private void UpdateFill()
    {
        var pct = ContextPercentage;
        if (pct <= 0 && ContextMaxTokens > 0)
            pct = (double)ContextTokens / ContextMaxTokens;

        pct = Math.Clamp(pct, 0, 1);

        var fillWidth = ActualWidth * pct;
        if (fillWidth < 0) fillWidth = 0;
        if (fillWidth > ActualWidth) fillWidth = ActualWidth;

        FillBar.Width = fillWidth;

        // Use shared TokenColors for consistency with TokenCountToColorConverter
        FillBar.Background = TokenColors.GetBrush(pct);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateFill();
    }
}
