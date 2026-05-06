using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface ICameraPreviewAppService
{
    event EventHandler<FrameReadyEventArgs> FrameReady;
    bool IsRunning { get; }
    void StartPreview();
    void StopPreview();
    Task<Bitmap> CaptureFrameAsync();
}

