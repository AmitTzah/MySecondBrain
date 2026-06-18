using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class GlobalArtifactsBrowserViewModel : ObservableObject
{
    private readonly IChatThreadService _chatService;
    private readonly ILogger<GlobalArtifactsBrowserViewModel> _logger;

    public GlobalArtifactsBrowserViewModel(
        IChatThreadService chatService,
        ILogger<GlobalArtifactsBrowserViewModel> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }
}
