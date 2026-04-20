using HardwareTestApp.src.Domain.Models;

namespace HardwareTestApp.src.Domain.Interfaces;

public interface ICameraService
{
    event EventHandler<FrameReadyEventArgs> FrameReady;
    void StartPreview(CameraConfig config);
    void StopPreview();
    Task<Bitmap> CaptureFrameAsync();
    bool IsRunning { get; }
}
