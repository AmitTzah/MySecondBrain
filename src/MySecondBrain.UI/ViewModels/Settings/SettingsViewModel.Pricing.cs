using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Pricing settings: monthly budget limit, warning threshold, block API on limit.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Pricing — Budget limits and alerts
    // ================================================================

    [ObservableProperty]
    private decimal? _monthlyBudgetLimit;

    [ObservableProperty]
    private int _warningThreshold = 80;

    [ObservableProperty]
    private bool _blockApiOnLimit;

    partial void OnMonthlyBudgetLimitChanged(decimal? value)
        => _ = _settingsRepo.SetAsync("MonthlyBudgetLimit", value?.ToString("F2") ?? string.Empty);

    partial void OnWarningThresholdChanged(int value)
    {
        var clamped = Math.Clamp(value, 50, 100);
        if (clamped != value)
        {
            WarningThreshold = clamped;
            return;
        }
        _ = _settingsRepo.SetAsync("WarningThreshold", value.ToString());
    }

    partial void OnBlockApiOnLimitChanged(bool value)
        => _ = _settingsRepo.SetAsync("BlockApiOnLimit", value ? "true" : "false");
}
