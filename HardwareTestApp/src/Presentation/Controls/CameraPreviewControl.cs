// Presentation/Controls/CameraPreviewControl.cs
using System.Collections.Concurrent;
using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Presentation.Controls;

public partial class CameraPreviewControl : UserControl
{
    // Edit these values to set initial ROI positions in source camera coordinates.
    private static readonly Rectangle INITIAL_ROI = new(1040, 220, 200, 200);
    private static readonly Rectangle INITIAL_BUTTON_ROI1_BLUE = new(1170, 1220, 150, 150);
    private static readonly Rectangle INITIAL_BUTTON_ROI2_YELLOW = new(1170, 950, 150, 150);
    private static readonly Rectangle INITIAL_BUTTON_ROI3_RED = new(1170, 750, 150, 150);

    private readonly ICameraPreviewAppService _camera;
    private readonly ConcurrentQueue<Bitmap> _frameBuffer = new();
    private Bitmap? _previousFrame;
    private PictureBox pictureBox = null!;
    private Size _latestFrameSize;
    private Rectangle _roi1SourceRect;
    private Rectangle _roi1SegmentRect;
    private Rectangle _roi2SourceRect;
    private Rectangle _roi3SourceRect;
    private Color _roi1Color = Color.Red;
    private Color _roi2Color = Color.Red;
    private Color _roi3Color = Color.Red;
    private bool _roi1Customized;
    private bool _buttonRoiMode;
    private int _draggedButtonRoiIndex = -1;
    private Point _dragOffsetSource;

    // Inject interface � kh�ng new tr?c ti?p adapter
    public CameraPreviewControl(ICameraPreviewAppService camera)
    {
        InitializeComponent();
        _camera = camera;
        SetRoi1(INITIAL_ROI);
        SetButtonRois(new[] { INITIAL_BUTTON_ROI1_BLUE, INITIAL_BUTTON_ROI2_YELLOW, INITIAL_BUTTON_ROI3_RED });
    }

    private void InitializeComponent()
    {
        pictureBox = new PictureBox();
        ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
        SuspendLayout();
        // 
        // pictureBox
        // 
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.Location = new Point(0, 0);
        pictureBox.Name = "pictureBox";
        pictureBox.Size = new Size(150, 150);
        pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
        pictureBox.TabIndex = 0;
        pictureBox.TabStop = false;
        pictureBox.Click += pictureBox_Click;
        pictureBox.Paint += PictureBox_Paint;
        pictureBox.MouseClick += PictureBox_MouseClick;
        pictureBox.MouseDown += PictureBox_MouseDown;
        pictureBox.MouseMove += PictureBox_MouseMove;
        pictureBox.MouseUp += PictureBox_MouseUp;
        // 
        // CameraPreviewControl
        // 
        Controls.Add(pictureBox);
        Name = "CameraPreviewControl";
        ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
        ResumeLayout(false);
    }

    public void SetRoi1(Rectangle sourceRect)
    {
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRect), "ROI1 ph?i c� k�ch thu?c > 0.");
        }

        _roi1SourceRect = sourceRect;
        _roi1Customized = true;
        pictureBox.Invalidate();
    }

    public void SetButtonRois(IReadOnlyList<Rectangle> sourceRects)
    {
        if (sourceRects.Count < 3)
        {
            throw new ArgumentException("Button ROI requires 3 rectangles.", nameof(sourceRects));
        }

        ValidateRoi(sourceRects[0], "ROI1-BLUE");
        ValidateRoi(sourceRects[1], "ROI2-YELLOW");
        ValidateRoi(sourceRects[2], "ROI3-RED");

        _roi1SegmentRect = sourceRects[0];
        _roi2SourceRect = sourceRects[1];
        _roi3SourceRect = sourceRects[2];
        pictureBox.Invalidate();
    }

    public void SetRoi1Detected(bool detected)
    {
        _roi1Color = detected ? Color.Lime : Color.Red;
        pictureBox.Invalidate();
    }

    public void SetButtonRoiMode(bool enabled)
    {
        _buttonRoiMode = enabled;
        if (enabled)
        {
            _roi1Color = Color.Red;
            _roi2Color = Color.Red;
            _roi3Color = Color.Red;

            // Nếu ROI1 chưa được gán hoặc kích thước nhỏ, tự động gán ROI mặc định đủ lớn
            if (_roi1SegmentRect.Width <= 0 || _roi2SourceRect.Width <= 0 || _roi3SourceRect.Width <= 0)
            {
                SetButtonRois(new[] { INITIAL_BUTTON_ROI1_BLUE, INITIAL_BUTTON_ROI2_YELLOW, INITIAL_BUTTON_ROI3_RED });
            }
        }
        else
        {
            _roi2Color = Color.Red;
            _roi3Color = Color.Red;
        }

        pictureBox.Invalidate();
    }

    /// <summary>
    /// Đặt trạng thái phát hiện cho ROI2 (vàng) khi ở chế độ button.
    /// </summary>
    public void SetRoi2Detected(bool detected)
    {
        _roi2Color = detected ? Color.Lime : Color.Red;
        pictureBox.Invalidate();
    }

    /// <summary>
    /// Đặt trạng thái phát hiện cho ROI3 (đỏ) khi ở chế độ button.
    /// </summary>
    public void SetRoi3Detected(bool detected)
    {
        _roi3Color = detected ? Color.Lime : Color.Red;
        pictureBox.Invalidate();
    }

    public Rectangle GetRoi1SourceRect()
    {
        return _roi1SourceRect;
    }

    public IReadOnlyList<Rectangle> GetButtonRoiSourceRects()
    {
        EnsureButtonRoisInitialized();
        return new[] { _roi1SegmentRect, _roi2SourceRect, _roi3SourceRect };
    }

    public void StartPreview()
    {
        _camera.FrameReady += OnFrameReady;   // dang k� event
        _camera.StartPreview();
    }

    public void StopPreview()
    {
        _camera.FrameReady -= OnFrameReady;   // h?y dang k� tru?c
        _camera.StopPreview();
    }

    // ?? Event n�y fire t? background thread ? b?t bu?c Invoke
    private void OnFrameReady(object? sender, BitmapFrameReadyEventArgs e)
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

        if (!_buttonRoiMode)
        {
            _roi1Color = IsLedDetectedInRoi(newFrame, _roi1SourceRect) ? Color.Lime : Color.Red;
        }
        else
        {
            EnsureButtonRoisInitialized();
            _roi1Color = IsExpectedLedDetectedInRoi(newFrame, _roi1SegmentRect, ExpectedLedColor.Blue) ? Color.Lime : Color.Red;
            _roi2Color = IsExpectedLedDetectedInRoi(newFrame, _roi2SourceRect, ExpectedLedColor.Yellow) ? Color.Lime : Color.Red;
            _roi3Color = IsExpectedLedDetectedInRoi(newFrame, _roi3SourceRect, ExpectedLedColor.Red) ? Color.Lime : Color.Red;
        }

        pictureBox.Image = newFrame;   // g�n frame m?i
        pictureBox.Invalidate();

        // ?? Dispose frame cu � n?u kh�ng s? memory leak
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
        return LedColorThresholds.IsRed(color.R, color.G, color.B) ||
            LedColorThresholds.IsGreen(color.R, color.G, color.B);
    }

    private static bool IsExpectedLedDetectedInRoi(Bitmap frame, Rectangle roi, ExpectedLedColor expectedColor)
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
                if (IsExpectedLedColor(frame.GetPixel(x, y), expectedColor))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsExpectedLedColor(Color color, ExpectedLedColor expectedColor)
    {
        return expectedColor switch
        {
            ExpectedLedColor.Blue => LedColorThresholds.IsBlue(color.R, color.G, color.B),
            ExpectedLedColor.Yellow => LedColorThresholds.IsYellow(color.R, color.G, color.B),
            ExpectedLedColor.Red => LedColorThresholds.IsRed(color.R, color.G, color.B),
            _ => false,
        };
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        if (_latestFrameSize.Width <= 0 || _latestFrameSize.Height <= 0 || _roi1SourceRect.Width <= 0 || _roi1SourceRect.Height <= 0)
        {
            return;
        }

        var scaleX = pictureBox.ClientSize.Width / (float)_latestFrameSize.Width;
        var scaleY = pictureBox.ClientSize.Height / (float)_latestFrameSize.Height;

        if (!_buttonRoiMode)
        {
            var roiOnView = new Rectangle(
                (int)Math.Round(_roi1SourceRect.X * scaleX),
                (int)Math.Round(_roi1SourceRect.Y * scaleY),
                Math.Max(1, (int)Math.Round(_roi1SourceRect.Width * scaleX)),
                Math.Max(1, (int)Math.Round(_roi1SourceRect.Height * scaleY)));

            DrawRoi(e.Graphics, roiOnView, _roi1Color, "ROI");
        }
        else
        {
            var roi1OnView = ScaleRoi(_roi1SegmentRect, scaleX, scaleY);
            var roi2OnView = ScaleRoi(_roi2SourceRect, scaleX, scaleY);
            var roi3OnView = ScaleRoi(_roi3SourceRect, scaleX, scaleY);

            DrawRoi(e.Graphics, roi1OnView, _roi1Color, "ROI1-BLUE");
            DrawRoi(e.Graphics, roi2OnView, _roi2Color, "ROI2-YELLOW");
            DrawRoi(e.Graphics, roi3OnView, _roi3Color, "ROI3-RED");
        }
    }

    private void PictureBox_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_buttonRoiMode)
        {
            return;
        }

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
        if (_buttonRoiMode)
        {
            UpdateButtonRois();
        }

        pictureBox.Invalidate();
    }

    private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!_buttonRoiMode || e.Button != MouseButtons.Left || !TryGetSourcePoint(e.Location, out var sourcePoint))
        {
            return;
        }

        EnsureButtonRoisInitialized();
        var rois = new[] { _roi1SegmentRect, _roi2SourceRect, _roi3SourceRect };
        for (var i = rois.Length - 1; i >= 0; i--)
        {
            if (!rois[i].Contains(sourcePoint))
            {
                continue;
            }

            _draggedButtonRoiIndex = i;
            _dragOffsetSource = new Point(sourcePoint.X - rois[i].X, sourcePoint.Y - rois[i].Y);
            pictureBox.Capture = true;
            pictureBox.Cursor = Cursors.SizeAll;
            return;
        }
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_buttonRoiMode)
        {
            return;
        }

        if (_draggedButtonRoiIndex < 0)
        {
            pictureBox.Cursor = TryGetSourcePoint(e.Location, out var hoverPoint) && IsAnyButtonRoiHit(hoverPoint)
                ? Cursors.SizeAll
                : Cursors.Default;
            return;
        }

        if (!TryGetSourcePoint(e.Location, out var sourcePoint))
        {
            return;
        }

        var current = GetButtonRoiByIndex(_draggedButtonRoiIndex);
        var next = new Rectangle(
            sourcePoint.X - _dragOffsetSource.X,
            sourcePoint.Y - _dragOffsetSource.Y,
            current.Width,
            current.Height);

        SetButtonRoiByIndex(_draggedButtonRoiIndex, ClampRoiToFrame(next, _latestFrameSize));
        pictureBox.Invalidate();
    }

    private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_draggedButtonRoiIndex < 0)
        {
            return;
        }

        _draggedButtonRoiIndex = -1;
        pictureBox.Capture = false;
        pictureBox.Cursor = Cursors.Default;
    }

    private static Rectangle ClampRoiToFrame(Rectangle roi, Size frameSize)
    {
        var maxX = Math.Max(0, frameSize.Width - roi.Width);
        var maxY = Math.Max(0, frameSize.Height - roi.Height);

        var clampedX = Math.Clamp(roi.X, 0, maxX);
        var clampedY = Math.Clamp(roi.Y, 0, maxY);

        return new Rectangle(clampedX, clampedY, roi.Width, roi.Height);
    }

    private static void ValidateRoi(Rectangle roi, string name)
    {
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roi), $"{name} must have positive size.");
        }
    }

    private static Rectangle CreateDefaultRoi(Size frameSize)
    {
        var roiWidth = Math.Max(20, frameSize.Width / 5);
        var roiHeight = Math.Max(20, frameSize.Height / 5);
        var roiX = (frameSize.Width - roiWidth) / 2;
        var roiY = (frameSize.Height - roiHeight) / 2;
        return new Rectangle(roiX, roiY, roiWidth, roiHeight);
    }

    private void UpdateButtonRois()
    {
        var frameSize = _latestFrameSize.Width > 0 ? _latestFrameSize : pictureBox.ClientSize;
        var rois = CreateButtonRois(_roi1SourceRect, frameSize);
        _roi1SegmentRect = rois[0];
        _roi2SourceRect = rois[1];
        _roi3SourceRect = rois[2];
    }

    private static IReadOnlyList<Rectangle> CreateButtonRois(Rectangle roi, Size frameSize)
    {
        var buttonRoiWidth = Math.Max(1, roi.Width);
        var buttonRoiHeight = Math.Max(1, roi.Height);
        var groupWidth = buttonRoiWidth * 3;
        var maxGroupX = Math.Max(0, frameSize.Width - groupWidth);
        var maxY = Math.Max(0, frameSize.Height - buttonRoiHeight);
        var groupX = Math.Clamp(roi.X, 0, maxGroupX);
        var y = Math.Clamp(roi.Y, 0, maxY);

        return new[]
        {
            new Rectangle(groupX, y, buttonRoiWidth, buttonRoiHeight),
            new Rectangle(groupX + buttonRoiWidth, y, buttonRoiWidth, buttonRoiHeight),
            new Rectangle(groupX + buttonRoiWidth * 2, y, buttonRoiWidth, buttonRoiHeight),
        };
    }

    private void EnsureButtonRoisInitialized()
    {
        if (_roi1SegmentRect.Width <= 0 || _roi2SourceRect.Width <= 0 || _roi3SourceRect.Width <= 0)
        {
            UpdateButtonRois();
        }
    }

    private bool TryGetSourcePoint(Point viewPoint, out Point sourcePoint)
    {
        sourcePoint = Point.Empty;
        if (_latestFrameSize.Width <= 0 || _latestFrameSize.Height <= 0 || pictureBox.ClientSize.Width <= 0 || pictureBox.ClientSize.Height <= 0)
        {
            return false;
        }

        var scaleX = _latestFrameSize.Width / (float)pictureBox.ClientSize.Width;
        var scaleY = _latestFrameSize.Height / (float)pictureBox.ClientSize.Height;
        sourcePoint = new Point(
            (int)Math.Round(viewPoint.X * scaleX),
            (int)Math.Round(viewPoint.Y * scaleY));
        return true;
    }

    private bool IsAnyButtonRoiHit(Point sourcePoint)
    {
        return _roi1SegmentRect.Contains(sourcePoint) ||
            _roi2SourceRect.Contains(sourcePoint) ||
            _roi3SourceRect.Contains(sourcePoint);
    }

    private Rectangle GetButtonRoiByIndex(int index)
    {
        return index switch
        {
            0 => _roi1SegmentRect,
            1 => _roi2SourceRect,
            2 => _roi3SourceRect,
            _ => Rectangle.Empty,
        };
    }

    private void SetButtonRoiByIndex(int index, Rectangle roi)
    {
        switch (index)
        {
            case 0:
                _roi1SegmentRect = roi;
                _roi1SourceRect = roi;
                break;
            case 1:
                _roi2SourceRect = roi;
                break;
            case 2:
                _roi3SourceRect = roi;
                break;
        }
    }

    private enum ExpectedLedColor
    {
        Blue,
        Yellow,
        Red,
    }

    private static Rectangle ScaleRoi(Rectangle roi, float scaleX, float scaleY)
    {
        return new Rectangle(
            (int)Math.Round(roi.X * scaleX),
            (int)Math.Round(roi.Y * scaleY),
            Math.Max(1, (int)Math.Round(roi.Width * scaleX)),
            Math.Max(1, (int)Math.Round(roi.Height * scaleY)));
    }

    private static void DrawRoi(Graphics graphics, Rectangle roiOnView, Color color, string label)
    {
        using var roiPen = new Pen(color, 2);
        using var roiBrush = new SolidBrush(color);
        graphics.DrawRectangle(roiPen, roiOnView);
        var labelSize = graphics.MeasureString(label, SystemFonts.DefaultFont);
        var labelX = Math.Max(0, roiOnView.X - (int)Math.Ceiling(labelSize.Width) - 4);
        var labelY = Math.Max(0, roiOnView.Y + (roiOnView.Height - (int)Math.Ceiling(labelSize.Height)) / 2);
        graphics.DrawString(label, SystemFonts.DefaultFont, roiBrush, labelX, labelY);
    }

    // Khi form d�ng ? d?n d?p
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

    private void pictureBox_Click(object? sender, EventArgs e)
    {

    }
}
