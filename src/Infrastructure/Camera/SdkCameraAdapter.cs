using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DVPCameraType;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Infrastructure.Camera;

public class SdkCameraAdapter : ICameraService, IDisposable
{
    private readonly TimeSpan _cameraRetryInterval;
    private readonly int _frameBufferSize;

    public event EventHandler<FrameReadyEventArgs>? FrameReady;

    private readonly object _sync = new();
    private readonly ConcurrentQueue<Bitmap> _frameBuffer = new();
    private DVPCamera.dvpStreamCallback? _streamCallback;
    private CancellationTokenSource? _startPreviewCts;
    private Task? _startPreviewTask;

    private uint _handle;
    private Bitmap? _latestFrame;

    public bool IsRunning { get; private set; }

    public SdkCameraAdapter(TimeSpan cameraRetryInterval, int frameBufferSize)
    {
        _cameraRetryInterval = cameraRetryInterval;
        _frameBufferSize = Math.Max(1, frameBufferSize);
    }

    public void StartPreview(CameraConfig config)
    {
        if (IsRunning || (_startPreviewTask is not null && !_startPreviewTask.IsCompleted))
        {
            return;
        }

        _startPreviewCts = new CancellationTokenSource();
        _startPreviewTask = Task.Run(() => WaitForCameraAndStartAsync(config, _startPreviewCts.Token));
    }

    private async Task WaitForCameraAndStartAsync(CameraConfig config, CancellationToken ct)
    {
        try
        {
            uint cameraCount = 0;

            while (!ct.IsCancellationRequested)
            {
                var refreshStatus = DVPCamera.dvpRefresh(ref cameraCount);
                EnsureOk(refreshStatus, "Không thể refresh danh sách camera SDK.");

                if (cameraCount == 0)
                {
                    await Task.Delay(_cameraRetryInterval, ct).ConfigureAwait(false);
                    continue;
                }

                if (config.DeviceIndex < 0 || config.DeviceIndex >= cameraCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(config.DeviceIndex), $"DeviceIndex hợp lệ từ 0..{cameraCount - 1}.");
                }

                ct.ThrowIfCancellationRequested();

                var openStatus = DVPCamera.dvpOpen((uint)config.DeviceIndex, dvpOpenMode.OPEN_NORMAL, ref _handle);
                EnsureOk(openStatus, $"Không thể mở camera SDK với DeviceIndex={config.DeviceIndex}.");

                var targetFormatStatus = DVPCamera.dvpSetTargetFormat(_handle, dvpStreamFormat.S_BGR24);
                EnsureOk(targetFormatStatus, "Không thể set target format BGR24.");

                _streamCallback = OnStreamArrived;
                var registerStatus = DVPCamera.dvpRegisterStreamCallback(_handle, _streamCallback, dvpStreamEvent.STREAM_EVENT_PROCESSED, IntPtr.Zero);
                EnsureOk(registerStatus, "Không thể đăng ký callback frame camera.");

                var startStatus = DVPCamera.dvpStart(_handle);
                EnsureOk(startStatus, "Không thể start luồng camera SDK.");

                IsRunning = true;
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void StopPreview()
    {
        var startPreviewCts = _startPreviewCts;
        _startPreviewCts = null;
        startPreviewCts?.Cancel();
        startPreviewCts?.Dispose();
        _startPreviewTask = null;

        if (_handle != 0)
        {
            if (_streamCallback is not null)
            {
                _ = DVPCamera.dvpUnregisterStreamCallback(_handle, _streamCallback, dvpStreamEvent.STREAM_EVENT_PROCESSED, IntPtr.Zero);
            }

            _ = DVPCamera.dvpStop(_handle);
            _ = DVPCamera.dvpClose(_handle);
            _handle = 0;
        }

        lock (_sync)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }

        while (_frameBuffer.TryDequeue(out var frame))
        {
            frame.Dispose();
        }

        _streamCallback = null;
        IsRunning = false;
    }

    public Task<Bitmap> CaptureFrameAsync()
    {
        lock (_sync)
        {
            if (_latestFrame is null)
            {
                throw new InvalidOperationException("Chưa có frame từ camera SDK.");
            }

            return Task.FromResult(new Bitmap(_latestFrame));
        }
    }

    private int OnStreamArrived(uint handle, dvpStreamEvent streamEvent, IntPtr context, ref dvpFrame sourceFrame, IntPtr sourceBuffer)
    {
        if (streamEvent != dvpStreamEvent.STREAM_EVENT_PROCESSED || sourceBuffer == IntPtr.Zero)
        {
            return 0;
        }

        if (sourceFrame.iWidth <= 0 || sourceFrame.iHeight <= 0)
        {
            return 0;
        }

        var bitmap = ConvertBgr24ToBitmap(sourceBuffer, sourceFrame.iWidth, sourceFrame.iHeight);

        EnqueueFrame(bitmap);

        return 1;
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

        if (latest is null)
        {
            return;
        }

        lock (_sync)
        {
            _latestFrame?.Dispose();
            _latestFrame = new Bitmap(latest);
        }

        FrameReady?.Invoke(this, new FrameReadyEventArgs
        {
            Frame = latest,
            Timestamp = DateTime.Now,
        });
    }

    private static Bitmap ConvertBgr24ToBitmap(IntPtr sourceBuffer, int width, int height)
    {
        var srcStride = width * 3;
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        try
        {
            for (var row = 0; row < height; row++)
            {
                var srcPtr = IntPtr.Add(sourceBuffer, row * srcStride);
                var dstPtr = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                var rowData = new byte[srcStride];
                Marshal.Copy(srcPtr, rowData, 0, srcStride);
                Marshal.Copy(rowData, 0, dstPtr, srcStride);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    private static void EnsureOk(dvpStatus status, string message)
    {
        if (status != dvpStatus.DVP_STATUS_OK)
        {
            throw new InvalidOperationException($"{message} Status={status}");
        }
    }

    public void Dispose()
    {
        StopPreview();
    }
}

