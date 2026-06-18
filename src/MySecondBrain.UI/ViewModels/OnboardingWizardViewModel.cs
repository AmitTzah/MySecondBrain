using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class OnboardingWizardViewModel : ObservableObject
{
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly ILogger<OnboardingWizardViewModel> _logger;

    public OnboardingWizardViewModel(
        IApiKeyRepository apiKeyRepo,
        ISettingsRepository settingsRepo,
        IModelConfigurationRepository modelConfigRepo,
        ILogger<OnboardingWizardViewModel> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _settingsRepo = settingsRepo;
        _modelConfigRepo = modelConfigRepo;
        _logger = logger;
    }
}
