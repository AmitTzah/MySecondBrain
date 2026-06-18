using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class UsageDashboardViewModel : ObservableObject
{
    private readonly IUsageRepository _usageRepo;
    private readonly ILLMProviderService _llmService;
    private readonly ILogger<UsageDashboardViewModel> _logger;

    public UsageDashboardViewModel(
        IUsageRepository usageRepo,
        ILLMProviderService llmService,
        ILogger<UsageDashboardViewModel> logger)
    {
        _usageRepo = usageRepo;
        _llmService = llmService;
        _logger = logger;
    }
}
