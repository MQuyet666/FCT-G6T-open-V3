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
    private PictureBox pictureBox;
    private Size _latestFrameSize;
    private Rectangle _roi1SourceRect;
    private Color _roi1Color = Color.Red;
    private bool _roi1Customized;

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
        pictureBox.Paint += PictureBox_Paint;
        pictureBox.MouseClick += PictureBox_MouseClick;
        Controls.Add(pictureBox);
    }

    public void SetRoi1(Rectangle sourceRect)
    {
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRect), "ROI1 phải có kích thước > 0.");
        }

        _roi1SourceRect = sourceRect;
        _roi1Customized = true;
        pictureBox.Invalidate();
    }

    public void SetRoi1Detected(bool detected)
    {
        _roi1Color = detected ? Color.Lime : Color.Red;
        pictureBox.Invalidate();
    }

    public Rectangle GetRoi1SourceRect()
    {
        return _roi1SourceRect;
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
        _latestFrameSize = newFrame.Size;
        if (!_roi1Customized)
        {
            _roi1SourceRect = CreateDefaultRoi(_latestFrameSize);
        }

        _roi1Color = IsLedDetectedInRoi(newFrame, _roi1SourceRect) ? Color.Lime : Color.Red;

        pictureBox.Image = newFrame;   // gán frame mới
        pictureBox.Invalidate();

        // ⚠️ Dispose frame cũ — nếu không sẽ memory leak
        _previousFrame?.Dispose();
        _previousFrame = newFrame;
    }

    private static bool IsLedDetectedInRoi(Bitmap frame, Rectangle roi)
    {
        var boundedRoi = Rectangle.Intersect(new Rectangle(Point.Empty, frame.Size), roi);
        if (boundedRoi.Width <= 0 || boundedRoi.Height <= 0)
        {
            return false;
        }

        const int sampleStep = 4;
        for (var y = boundedRoi.Top; y < boundedRoi.Bottom; y += sampleStep)
        {
            for (var x = boundedRoi.Left; x < boundedRoi.Right; x += sampleStep)
            {
                var color = frame.GetPixel(x, y);
                if (IsLedColor(color))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsLedColor(Color color)
    {
        var isRed = color.R > 180 && color.G < 140 && color.B < 140;
        var isGreen = color.G > 170 && color.R < 160 && color.B < 160;
        var isYellow = color.R > 180 && color.G > 160 && color.B < 120;
        var isCyan = color.B > 160 && color.G > 160 && color.R < 140;

        return isRed || isGreen || isYellow || isCyan;
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        if (_latestFrameSize.Width <= 0 || _latestFrameSize.Height <= 0 || _roi1SourceRect.Width <= 0 || _roi1SourceRect.Height <= 0)
        {
            return;
        }

        var scaleX = pictureBox.ClientSize.Width / (float)_latestFrameSize.Width;
        var scaleY = pictureBox.ClientSize.Height / (float)_latestFrameSize.Height;

        var roiOnView = new Rectangle(
            (int)Math.Round(_roi1SourceRect.X * scaleX),
            (int)Math.Round(_roi1SourceRect.Y * scaleY),
            Math.Max(1, (int)Math.Round(_roi1SourceRect.Width * scaleX)),
            Math.Max(1, (int)Math.Round(_roi1SourceRect.Height * scaleY)));

        using var roiPen = new Pen(_roi1Color, 2);
        using var roiBrush = new SolidBrush(_roi1Color);
        e.Graphics.DrawRectangle(roiPen, roiOnView);
        e.Graphics.DrawString("ROI1", Font, roiBrush, roiOnView.X, Math.Max(0, roiOnView.Y - 20));
    }

    private void PictureBox_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_latestFrameSize.Width <= 0 || _latestFrameSize.Height <= 0 || _roi1SourceRect.Width <= 0 || _roi1SourceRect.Height <= 0)
        {
            return;
        }

        var scaleX = _latestFrameSize.Width / (float)pictureBox.ClientSize.Width;
        var scaleY = _latestFrameSize.Height / (float)pictureBox.ClientSize.Height;

        var sourceX = (int)Math.Round(e.X * scaleX);
        var sourceY = (int)Math.Round(e.Y * scaleY);

        MoveRoi1ToSourceCenter(sourceX, sourceY);
    }

    private void MoveRoi1ToSourceCenter(int centerX, int centerY)
    {
        var roiX = centerX - (_roi1SourceRect.Width / 2);
        var roiY = centerY - (_roi1SourceRect.Height / 2);
        var boundedRoi = ClampRoiToFrame(new Rectangle(roiX, roiY, _roi1SourceRect.Width, _roi1SourceRect.Height), _latestFrameSize);

        _roi1SourceRect = boundedRoi;
        _roi1Customized = true;
        pictureBox.Invalidate();
    }

    private static Rectangle ClampRoiToFrame(Rectangle roi, Size frameSize)
    {
        var maxX = Math.Max(0, frameSize.Width - roi.Width);
        var maxY = Math.Max(0, frameSize.Height - roi.Height);

        var clampedX = Math.Clamp(roi.X, 0, maxX);
        var clampedY = Math.Clamp(roi.Y, 0, maxY);

        return new Rectangle(clampedX, clampedY, roi.Width, roi.Height);
    }

    private static Rectangle CreateDefaultRoi(Size frameSize)
    {
        var roiWidth = Math.Max(20, frameSize.Width / 5);
        var roiHeight = Math.Max(20, frameSize.Height / 5);
        var roiX = (frameSize.Width - roiWidth) / 2;
        var roiY = (frameSize.Height - roiHeight) / 2;
        return new Rectangle(roiX, roiY, roiWidth, roiHeight);
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