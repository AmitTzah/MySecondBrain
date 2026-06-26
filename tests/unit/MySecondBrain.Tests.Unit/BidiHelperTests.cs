using WpfFlowDirection = System.Windows.FlowDirection;
using MySecondBrain.Core.Utilities;

namespace MySecondBrain.Tests.Unit;

public class BidiHelperTests
{
    [Fact]
    public void GetMessageFlowDirection_PureHebrew_ReturnsRightToLeft()
    {
        var result = BidiHelper.GetMessageFlowDirection("שלום עולם");
        Assert.Equal(WpfFlowDirection.RightToLeft, result);
    }

    [Fact]
    public void GetMessageFlowDirection_PureEnglish_ReturnsLeftToRight()
    {
        var result = BidiHelper.GetMessageFlowDirection("Hello World");
        Assert.Equal(WpfFlowDirection.LeftToRight, result);
    }

    [Fact]
    public void GetMessageFlowDirection_Mixed40PercentHebrew_ReturnsRightToLeft()
    {
        // 3 Hebrew letters out of 7 total letters = ~43% > 30% threshold
        var result = BidiHelper.GetMessageFlowDirection("abcשלךdef");
        Assert.Equal(WpfFlowDirection.RightToLeft, result);
    }

    [Fact]
    public void GetMessageFlowDirection_Mixed20PercentHebrew_ReturnsLeftToRight()
    {
        // 1 Hebrew letter out of 6 total letters = ~17% < 30% threshold
        var result = BidiHelper.GetMessageFlowDirection("abcשdef");
        Assert.Equal(WpfFlowDirection.LeftToRight, result);
    }

    [Fact]
    public void GetMessageFlowDirection_EmptyString_ReturnsLeftToRight()
    {
        var result = BidiHelper.GetMessageFlowDirection(string.Empty);
        Assert.Equal(WpfFlowDirection.LeftToRight, result);
    }

    [Fact]
    public void GetMessageFlowDirection_NullString_ReturnsLeftToRight()
    {
        var result = BidiHelper.GetMessageFlowDirection(null);
        Assert.Equal(WpfFlowDirection.LeftToRight, result);
    }

    [Fact]
    public void GetMessageFlowDirection_OnlyNumbers_ReturnsLeftToRight()
    {
        var result = BidiHelper.GetMessageFlowDirection("12345 67890");
        Assert.Equal(WpfFlowDirection.LeftToRight, result);
    }

    [Fact]
    public void GetMessageFlowDirection_OnlyHebrewNumbers_ReturnsRightToLeft()
    {
        var result = BidiHelper.GetMessageFlowDirection("שלום 123");
        Assert.Equal(WpfFlowDirection.RightToLeft, result);
    }

    [Fact]
    public void CodeBlockFlowDirection_AlwaysLeftToRight()
    {
        Assert.Equal(WpfFlowDirection.LeftToRight, BidiHelper.CodeBlockFlowDirection);
    }

    [Fact]
    public void GetMessageFlowDirection_WhitespaceOnly_ReturnsLeftToRight()
    {
        var result = BidiHelper.GetMessageFlowDirection("   \t\n  ");
        Assert.Equal(WpfFlowDirection.LeftToRight, result);
    }

    [Fact]
    public void GetMessageFlowDirection_MixedRtlLtr_WithCodeSnippet_DetectsRtlStill()
    {
        var result = BidiHelper.GetMessageFlowDirection(
            "שלום עולם, הנה קוד `var x = 1;` בסוף");
        Assert.Equal(WpfFlowDirection.RightToLeft, result);
    }
}
