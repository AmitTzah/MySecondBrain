using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.ViewModels;

public partial class MediaLibraryViewModel : ObservableObject
{
    private readonly ICameraService _cameraService;
    private readonly IVideoPlayerService _videoPlayerService;
    private readonly IAudioService _audioService;
    private readonly ILogger<MediaLibraryViewModel> _logger;

    public MediaLibraryViewModel(
        ICameraService cameraService,
        IVideoPlayerService videoPlayerService,
        IAudioService audioService,
        ILogger<MediaLibraryViewModel> logger)
    {
        _cameraService = cameraService;
        _videoPlayerService = videoPlayerService;
        _audioService = audioService;
        _logger = logger;
    }
}
