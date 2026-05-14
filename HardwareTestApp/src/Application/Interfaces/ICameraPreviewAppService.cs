namespace FCT.G6T.Application.Interfaces;

public interface ICameraPreviewAppService
{
    event EventHandler<BitmapFrameReadyEventArgs> FrameReady;
    bool IsRunning { get; }
    void StartPreview();
    void StopPreview();
    Task<Bitmap> CaptureFrameAsync();
}

