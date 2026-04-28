using System.Collections.Concurrent;
using System.IO;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using OpenCvSharp;

namespace FCT.G6T.Infrastructure.Camera;

// Infrastructure/Camera/OpenCvCameraAdapter.cs
public class OpenCvCameraAdapter : ICameraService, IDisposable
{
    public event EventHandler<FrameReadyEventArgs>? FrameReady;

    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<Bitmap> _frameBuffer = new();
    private readonly int _frameDelayMs;
    private readonly int _frameBufferSize;
    public bool IsRunning { get; private set; }

    public OpenCvCameraAdapter(int frameDelayMs, int frameBufferSize)
    {
        _frameDelayMs = frameDelayMs;
        _frameBufferSize = Math.Max(1, frameBufferSize);
    }

    public void StartPreview(CameraConfig config)
    {
        if (IsRunning) return;

        _capture?.Dispose();
        _capture = new VideoCapture(config.DeviceIndex, VideoCaptureAPIs.DSHOW);
        if (!_capture.IsOpened())
        {
            _capture.Dispose();
            _capture = null;
            throw new InvalidOperationException($"Kh�ng th? m? camera v?i DeviceIndex={config.DeviceIndex}.");
        }

        _capture.Set(VideoCaptureProperties.FrameWidth, config.Width);
        _capture.Set(VideoCaptureProperties.FrameHeight, config.Height);
        _capture.Set(VideoCaptureProperties.Fps, config.TargetFps);

        _cts = new CancellationTokenSource();
        IsRunning = true;

        // Ch?y loop tr�n background thread
        _ = Task.Run(() => CaptureLoop(_cts.Token));
    }

    private async Task CaptureLoop(CancellationToken ct)
    {
        using var mat = new Mat();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_capture is null || !_capture.Read(mat) || mat.Empty())
                {
                    continue;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            // Convert Mat ? Bitmap r?i raise event
            var bitmap = ConvertMatToBitmap(mat);
            EnqueueFrame(bitmap);

            // Gi?i h?n ~30fps
            try
            {
                await Task.Delay(_frameDelayMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void EnqueueFrame(Bitmap frame)
    {
        _frameBuffer.Enqueue(frame);
        while (_frameBuffer.Count > _frameBufferSize && _frameBuffer.TryDequeue(out var overflow))
        {
            overflow.Dispose();
        }

        Bitmap? latest = null;
        while (_frameBuffer.TryDequeue(out var current))
        {
            latest?.Dispose();
            latest = current;
        }

        if (latest is not null)
        {
            FrameReady?.Invoke(this, new FrameReadyEventArgs
            {
                Frame = latest,
                Timestamp = DateTime.Now
            });
        }
    }

    public void StopPreview()
    {
        var cts = _cts;

        _cts = null;

        cts?.Cancel();

        cts?.Dispose();
        _capture?.Dispose();
        _capture = null;
        while (_frameBuffer.TryDequeue(out var frame))
        {
            frame.Dispose();
        }
        IsRunning = false;
    }

    public async Task<Bitmap> CaptureFrameAsync()
    {
        if (_capture is null || !_capture.IsOpened())
        {
            throw new InvalidOperationException("Camera chua du?c kh?i d?ng.");
        }

        using var mat = new Mat();
        await Task.Run(() => _capture.Read(mat)).ConfigureAwait(false);
        return ConvertMatToBitmap(mat);
    }

    private static Bitmap ConvertMatToBitmap(Mat mat)
    {
        Cv2.ImEncode(".bmp", mat, out var imageBytes);
        using var ms = new MemoryStream(imageBytes);
        using var temp = new Bitmap(ms);
        return new Bitmap(temp);
    }

    public void Dispose() => StopPreview();
}
