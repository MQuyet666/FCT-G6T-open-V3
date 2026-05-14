using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FCT.G6T.Application.Services;

public class CameraPreviewAppService : ICameraPreviewAppService, IDisposable
{
    private readonly ICameraService _cameraService;
    private readonly CameraConfig _cameraConfig;

    public event EventHandler<BitmapFrameReadyEventArgs>? FrameReady;

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

    public async Task<Bitmap> CaptureFrameAsync()
    {
        var frame = await _cameraService.CaptureFrameAsync().ConfigureAwait(false);
        return ToBitmap(frame);
    }

    private void OnFrameReady(object? sender, FrameReadyEventArgs e)
    {
        FrameReady?.Invoke(this, new BitmapFrameReadyEventArgs
        {
            Frame = ToBitmap(e.Frame),
            Timestamp = e.Timestamp,
        });
    }

    public void Dispose()
    {
        StopPreview();
        (_cameraService as IDisposable)?.Dispose();
    }

    private static Bitmap ToBitmap(CameraFrame frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format24bppRgb);
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, frame.Width, frame.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            for (var row = 0; row < frame.Height; row++)
            {
                var sourceOffset = row * frame.Stride;
                var destination = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                Marshal.Copy(frame.Bgr24Data, sourceOffset, destination, frame.Stride);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }
}

