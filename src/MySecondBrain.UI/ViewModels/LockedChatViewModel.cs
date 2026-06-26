using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// ViewModel for the Lock/Unlock chat password dialog.
/// </summary>
public partial class LockedChatViewModel : ObservableObject
{
    /// <summary>The chat thread ID being locked or unlocked.</summary>
    public string ThreadId { get; }

    /// <summary>True for Lock mode, false for Unlock mode.</summary>
    public bool IsLockMode { get; }

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Dialog title based on mode.</summary>
    public string DialogTitle => IsLockMode ? "Lock Chat" : "Unlock Chat";

    /// <summary>Action button text based on mode.</summary>
    public string ActionButtonText => IsLockMode ? "Lock" : "Unlock";

    public LockedChatViewModel(string threadId, bool isLockMode)
    {
        ThreadId = threadId;
        IsLockMode = isLockMode;
    }

    /// <summary>
    /// Validates and returns the password result.
    /// Returns (true, password) on success or (false, errorMessage) on validation failure.
    /// </summary>
    public Task<(bool Success, object? Result)> ConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            return Task.FromResult<(bool, object?)>((
                false, "Password cannot be empty."));
        }

        if (IsLockMode && Password != ConfirmPassword)
        {
            return Task.FromResult<(bool, object?)>((
                false, "Passwords do not match."));
        }

        if (IsLockMode && Password.Length < 4)
        {
            return Task.FromResult<(bool, object?)>((
                false, "Password must be at least 4 characters."));
        }

        return Task.FromResult<(bool, object?)>((true, Password));
    }
}
