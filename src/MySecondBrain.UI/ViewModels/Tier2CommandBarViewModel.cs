using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class Tier2CommandBarViewModel : ObservableObject
{
    private readonly IChatThreadService _chatService;
    private readonly IWikiService _wikiService;
    private readonly IChatSearchService _searchService;
    private readonly ILogger<Tier2CommandBarViewModel> _logger;

    public Tier2CommandBarViewModel(
        IChatThreadService chatService,
        IWikiService wikiService,
        IChatSearchService searchService,
        ILogger<Tier2CommandBarViewModel> logger)
    {
        _chatService = chatService;
        _wikiService = wikiService;
        _searchService = searchService;
        _logger = logger;
    }
}
