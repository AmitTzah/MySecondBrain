using System.IO;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

public class AForgeCameraService : ICameraService
{
    public IReadOnlyList<string> GetAvailableCameras() => Array.Empty<string>();

    public bool IsCameraAvailable => false;

    public Task<byte[]> CaptureStillAsync(string? cameraDeviceId = null, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<Stream> GetPreviewStreamAsync(CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public void StopPreview() { }

    public void Dispose() { }
}
