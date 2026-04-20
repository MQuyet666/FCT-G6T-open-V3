using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DVPCameraType;
using HardwareTestApp.src.Domain.Interfaces;
using HardwareTestApp.src.Domain.Models;

namespace HardwareTestApp.src.Infrastructure.Camera;

public class SdkCameraAdapter : ICameraService, IDisposable
{
    public event EventHandler<FrameReadyEventArgs>? FrameReady;

    private readonly object _sync = new();
    private DVPCamera.dvpStreamCallback? _streamCallback;

    private uint _handle;
    private Bitmap? _latestFrame;

    public bool IsRunning { get; private set; }

    public void StartPreview(CameraConfig config)
    {
        if (IsRunning)
        {
            return;
        }

        uint cameraCount = 0;
        var refreshStatus = DVPCamera.dvpRefresh(ref cameraCount);
        EnsureOk(refreshStatus, "Không thể refresh danh sách camera SDK.");

        if (cameraCount == 0)
        {
            throw new InvalidOperationException("Không tìm thấy camera SDK.");
        }

        if (config.DeviceIndex < 0 || config.DeviceIndex >= cameraCount)
        {
            throw new ArgumentOutOfRangeException(nameof(config.DeviceIndex), $"DeviceIndex hợp lệ từ 0..{cameraCount - 1}.");
        }

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
    }

    public void StopPreview()
    {
        if (!IsRunning)
        {
            return;
        }

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

        lock (_sync)
        {
            _latestFrame?.Dispose();
            _latestFrame = new Bitmap(bitmap);
        }

        FrameReady?.Invoke(this, new FrameReadyEventArgs
        {
            Frame = bitmap,
            Timestamp = DateTime.Now,
        });

        return 1;
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
