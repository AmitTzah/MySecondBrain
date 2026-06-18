using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class ModelComparisonViewModel : ObservableObject
{
    private readonly ILLMProviderService _llmService;
    private readonly ILLMProviderFactory _llmFactory;
    private readonly ILogger<ModelComparisonViewModel> _logger;

    public ModelComparisonViewModel(
        ILLMProviderService llmService,
        ILLMProviderFactory llmFactory,
        ILogger<ModelComparisonViewModel> logger)
    {
        _llmService = llmService;
        _llmFactory = llmFactory;
        _logger = logger;
    }
}
