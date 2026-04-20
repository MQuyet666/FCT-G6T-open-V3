using HardwareTestApp.src.Domain.Models;

namespace HardwareTestApp.src.Application.Interfaces;

public interface ICameraPreviewAppService
{
    event EventHandler<FrameReadyEventArgs> FrameReady;
    bool IsRunning { get; }
    void StartPreview();
    void StopPreview();
    Task<Bitmap> CaptureFrameAsync();
}
