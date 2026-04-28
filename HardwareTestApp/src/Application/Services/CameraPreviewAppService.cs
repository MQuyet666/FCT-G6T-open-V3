using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Services;

public class CameraPreviewAppService : ICameraPreviewAppService, IDisposable
{
    private readonly ICameraService _cameraService;
    private readonly CameraConfig _cameraConfig;

    public event EventHandler<FrameReadyEventArgs>? FrameReady;

    public bool IsRunning => _cameraService.IsRunning;

    public CameraPreviewAppService(ICameraService cameraService, CameraConfig cameraConfig)
    {
        _cameraService = cameraService;
        _cameraConfig = cameraConfig;
    }

    public void StartPreview()
    {
        _cameraService.FrameReady += OnFrameReady;
        _cameraService.StartPreview(_cameraConfig);
    }

    public void StopPreview()
    {
        _cameraService.FrameReady -= OnFrameReady;
        _cameraService.StopPreview();
    }

    public Task<Bitmap> CaptureFrameAsync()
    {
        return _cameraService.CaptureFrameAsync();
    }

    private void OnFrameReady(object? sender, FrameReadyEventArgs e)
    {
        FrameReady?.Invoke(this, e);
    }

    public void Dispose()
    {
        StopPreview();
        (_cameraService as IDisposable)?.Dispose();
    }
}

