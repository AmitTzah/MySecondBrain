using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class Tier1OverlayViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly ITextInjectionService _textInjection;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILogger<Tier1OverlayViewModel> _logger;

    public Tier1OverlayViewModel(
        IClipboardService clipboardService,
        ITextInjectionService textInjection,
        IGlobalHotkeyService hotkeyService,
        ILogger<Tier1OverlayViewModel> logger)
    {
        _clipboardService = clipboardService;
        _textInjection = textInjection;
        _hotkeyService = hotkeyService;
        _logger = logger;
    }
}
