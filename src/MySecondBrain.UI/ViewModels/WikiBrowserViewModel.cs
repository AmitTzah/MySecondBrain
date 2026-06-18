using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class WikiBrowserViewModel : ObservableObject
{
    private readonly IWikiService _wikiService;
    private readonly ILogger<WikiBrowserViewModel> _logger;

    public WikiBrowserViewModel(
        IWikiService wikiService,
        ILogger<WikiBrowserViewModel> logger)
    {
        _wikiService = wikiService;
        _logger = logger;
    }
}
