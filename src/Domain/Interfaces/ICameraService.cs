using FCT.G6T.Domain.Models;

namespace FCT.G6T.Domain.Interfaces;

public interface ICameraService
{
    event EventHandler<FrameReadyEventArgs> FrameReady;
    void StartPreview(CameraConfig config);
    void StopPreview();
    Task<Bitmap> CaptureFrameAsync();
    bool IsRunning { get; }
}

