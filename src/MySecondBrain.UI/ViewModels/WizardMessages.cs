using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Sent when the Onboarding Wizard should close and the Studio should open.
/// </summary>
public sealed class LaunchStudioMessage : RequestMessage<bool>
{
}

/// <summary>
/// Sent when the user requests to re-run the Onboarding Wizard from Settings.
/// </summary>
public sealed class ReRunOnboardingMessage
{
}

/// <summary>
/// Sent when the API keys list in the Settings → Providers tab should be refreshed.
/// </summary>
public sealed class RefreshApiKeysMessage
{
}
