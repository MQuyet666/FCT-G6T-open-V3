// Presentation/Controls/CameraPreviewControl.cs
using System.Collections.Concurrent;
using HardwareTestApp.src.Application.Interfaces;
using HardwareTestApp.src.Domain.Models;

namespace HardwareTestApp.src.Presentation.Controls;

public partial class CameraPreviewControl : UserControl
{
    private readonly ICameraPreviewAppService _camera;
    private readonly ConcurrentQueue<Bitmap> _frameBuffer = new();
    private Bitmap? _previousFrame;
    private PictureBox pictureBox; // Thêm khai báo cho pictureBox

    // Inject interface — không new trực tiếp adapter
    public CameraPreviewControl(ICameraPreviewAppService camera)
    {
        InitializeComponent();
        _camera = camera;
    }

    private void InitializeComponent()
    {
        pictureBox = new PictureBox();
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
        Controls.Add(pictureBox);
    }

    public void StartPreview()
    {
        _camera.FrameReady += OnFrameReady;   // đăng ký event
        _camera.StartPreview();
    }

    public void StopPreview()
    {
        _camera.FrameReady -= OnFrameReady;   // hủy đăng ký trước
        _camera.StopPreview();
    }

    // ⚠️ Event này fire từ background thread → bắt buộc Invoke
    private void OnFrameReady(object sender, FrameReadyEventArgs e)
    {
        _frameBuffer.Enqueue(e.Frame);
        while (_frameBuffer.Count > 3 && _frameBuffer.TryDequeue(out var overflowFrame))
        {
            overflowFrame.Dispose();
        }

        if (pictureBox.InvokeRequired)
        {
            pictureBox.BeginInvoke(DrainFrameBuffer);
        }
        else
        {
            DrainFrameBuffer();
        }
    }

    private void DrainFrameBuffer()
    {
        Bitmap? latestFrame = null;
        while (_frameBuffer.TryDequeue(out var frame))
        {
            latestFrame?.Dispose();
            latestFrame = frame;
        }

        if (latestFrame is not null)
        {
            UpdateFrame(latestFrame);
        }
    }

    private void UpdateFrame(Bitmap newFrame)
    {
        pictureBox.Image = newFrame;   // gán frame mới

        // ⚠️ Dispose frame cũ — nếu không sẽ memory leak
        _previousFrame?.Dispose();
        _previousFrame = newFrame;
    }

    // Khi form đóng → dọn dẹp
    protected override void OnHandleDestroyed(EventArgs e)
    {
        StopPreview();
        while (_frameBuffer.TryDequeue(out var frame))
        {
            frame.Dispose();
        }

        _previousFrame?.Dispose();
        _previousFrame = null;

        base.OnHandleDestroyed(e);
    }
}