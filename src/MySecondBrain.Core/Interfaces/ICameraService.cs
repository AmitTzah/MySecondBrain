namespace MySecondBrain.Core.Interfaces;

public interface ICameraService : IDisposable
{
    IReadOnlyList<string> GetAvailableCameras();
    bool IsCameraAvailable { get; }
    Task<byte[]> CaptureStillAsync(string? cameraDeviceId = null, CancellationToken ct = default);
    Task<Stream> GetPreviewStreamAsync(CancellationToken ct = default);
    void StopPreview();
}
