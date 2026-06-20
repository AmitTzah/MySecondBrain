namespace MySecondBrain.Core.Interfaces;

/// <summary>
/// Service for showing confirmation dialogs. Mockable for unit testing.
/// </summary>
public interface IConfirmationService
{
    bool Confirm(string message, string title);
}
