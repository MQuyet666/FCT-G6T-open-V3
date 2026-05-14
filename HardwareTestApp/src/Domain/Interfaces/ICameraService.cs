using FCT.G6T.Domain.Models;

namespace FCT.G6T.Domain.Interfaces;

public interface ICameraService
{
    event EventHandler<FrameReadyEventArgs> FrameReady;
    void StartPreview(CameraConfig config);
    void StopPreview();
    Task<CameraFrame> CaptureFrameAsync();
    bool IsRunning { get; }
}

