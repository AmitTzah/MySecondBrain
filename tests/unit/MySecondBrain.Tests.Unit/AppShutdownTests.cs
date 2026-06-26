using MySecondBrain.UI;

namespace MySecondBrain.Tests.Unit;

public class AppShutdownTests
{
    [Fact]
    public void ShouldShutdownOnWizardClose_WhenStudioNotLaunched_ReturnsTrue()
    {
        // Wizard X-button close without completing onboarding → should shut down
        Assert.True(App.ShouldShutdownOnWizardClose(false));
    }

    [Fact]
    public void ShouldShutdownOnWizardClose_WhenStudioLaunched_ReturnsFalse()
    {
        // Wizard completed → LaunchStudioRequested was raised → should NOT shut down
        Assert.False(App.ShouldShutdownOnWizardClose(true));
    }
}
