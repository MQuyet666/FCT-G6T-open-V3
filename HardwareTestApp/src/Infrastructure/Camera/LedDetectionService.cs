using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FCT.G6T.Infrastructure.Camera;

public sealed class LedDetectionService : ILedDetectionService
{
    private readonly ICameraPreviewAppService _cameraPreview;
    private readonly ILogger<LedDetectionService> _logger;

    public LedDetectionService(ICameraPreviewAppService cameraPreview, ILogger<LedDetectionService> logger)
    {
        _cameraPreview = cameraPreview;
        _logger = logger;
    }

    public Task<TestStepResult> DetectSmokeLedColorsAsync(
        RoiRegion roi,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        return DetectLedColorsAsync(
            roi,
            timeout,
            pollDelay,
            requiredColors: LedColorFlags.Red | LedColorFlags.Green,
            progress,
            ct);
    }

    public Task<TestStepResult> DetectButtonLedColorsAsync(
        RoiRegion roi,
        IReadOnlyList<RoiRegion>? buttonRois,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        return DetectButtonLedColorsCoreAsync(roi, buttonRois, timeout, pollDelay, progress, ct);
    }

    public Task<TestStepResult> DetectButtonRoi3RedAsync(
        RoiRegion roi,
        IReadOnlyList<RoiRegion>? buttonRois,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        return DetectButtonRoi3RedCoreAsync(roi, buttonRois, timeout, pollDelay, progress, ct);
    }

    private async Task<TestStepResult> DetectLedColorsAsync(
        RoiRegion roi,
        TimeSpan timeout,
        TimeSpan pollDelay,
        LedColorFlags requiredColors,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var detected = LedColorFlags.None;

        while (!timeoutCts.IsCancellationRequested)
        {
            using var frame = await _cameraPreview.CaptureFrameAsync().ConfigureAwait(false);
            var boundedRoi = BoundRoi(frame, roi);
            if (boundedRoi.Width <= 0 || boundedRoi.Height <= 0)
            {
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = false,
                    Message = "ROI1 nam ngoai khung anh.",
                };
            }

            detected |= AnalyzeRoiColors(frame, boundedRoi);
            if ((detected & requiredColors) == requiredColors)
            {
                var passMessage = "LED ROI Detect: phat hien du mau do va xanh.";
                _logger.LogInformation("{Message}", passMessage);
                progress?.Report("[ROI1][PASS]");
                progress?.Report($"[ACK][PASS] LED ROI Detect - {passMessage}");
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = true,
                    Message = $"[ACK][PASS] LED ROI Detect - {passMessage}",
                };
            }

            try
            {
                await Task.Delay(pollDelay, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var missingColors = BuildMissingColors(detected, requiredColors);
        var failMessage = $"[ACK][FAIL] LED ROI Detect - khong thay du do + xanh trong {timeout.TotalSeconds:0.#}s. {missingColors}";
        progress?.Report(failMessage);
        return new TestStepResult
        {
            StepName = "LED ROI Detect",
            IsPassed = false,
            Message = failMessage,
        };
    }

    private async Task<TestStepResult> DetectButtonLedColorsCoreAsync(
        RoiRegion roi,
        IReadOnlyList<RoiRegion>? buttonRois,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var roiNames = new[] { "ROI1", "ROI2", "ROI3" };
        var requiredByRoi = new[] { LedColorFlags.Blue, LedColorFlags.Yellow, LedColorFlags.Red };
        var detectedByRoi = new[] { LedColorFlags.None, LedColorFlags.None, LedColorFlags.None };
        string? lastSummary = null;

        while (!timeoutCts.IsCancellationRequested)
        {
            using var frame = await _cameraPreview.CaptureFrameAsync().ConfigureAwait(false);
            var boundedRoi = BoundRoi(frame, roi);
            if (boundedRoi.Width <= 0 || boundedRoi.Height <= 0)
            {
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = false,
                    Message = "ROI1 nam ngoai khung anh.",
                };
            }

            var rois = ResolveButtonRois(frame, roi, buttonRois);
            if (rois.Any(segment => segment.Width <= 0 || segment.Height <= 0))
            {
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = false,
                    Message = "Mot hoac nhieu ROI button nam ngoai khung anh.",
                };
            }

            var roiResults = rois.Select(segment => AnalyzeRoiColors(frame, segment)).ToArray();
            for (var i = 0; i < detectedByRoi.Length; i++)
            {
                detectedByRoi[i] |= roiResults[i] & requiredByRoi[i];
            }

            var summary = string.Join(", ", roiResults.Select((result, index) =>
                $"{roiNames[index]}={FormatRoiColors(result)}"));

            if (!string.Equals(summary, lastSummary, StringComparison.Ordinal))
            {
                lastSummary = summary;
                progress?.Report($"[ROI] {summary}");
            }

            if (detectedByRoi[0].HasFlag(LedColorFlags.Blue) &&
                detectedByRoi[1].HasFlag(LedColorFlags.Yellow) &&
                detectedByRoi[2].HasFlag(LedColorFlags.Red))
            {
                var passMessage = $"LED ROI Detect: {summary}.";
                _logger.LogInformation("{Message}", passMessage);
                progress?.Report("[ROI1][PASS]");
                progress?.Report($"[ACK][PASS] LED ROI Detect - {passMessage}");
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = true,
                    Message = $"[ACK][PASS] LED ROI Detect - {passMessage}",
                };
            }

            try
            {
                await Task.Delay(pollDelay, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var failMessage = $"[ACK][FAIL] LED ROI Detect - khong thay dung ROI1=Blue, ROI2=Yellow, ROI3=Red trong {timeout.TotalSeconds:0.#}s. {BuildMissingButtonRoiColors(detectedByRoi)}";
        progress?.Report(failMessage);
        return new TestStepResult
        {
            StepName = "LED ROI Detect",
            IsPassed = false,
            Message = failMessage,
        };
    }

    private async Task<TestStepResult> DetectButtonRoi3RedCoreAsync(
        RoiRegion roi,
        IReadOnlyList<RoiRegion>? buttonRois,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        string? lastSummary = null;
        while (!timeoutCts.IsCancellationRequested)
        {
            using var frame = await _cameraPreview.CaptureFrameAsync().ConfigureAwait(false);
            var rois = ResolveButtonRois(frame, roi, buttonRois);
            if (rois.Count < 3 || rois[2].Width <= 0 || rois[2].Height <= 0)
            {
                return new TestStepResult
                {
                    StepName = "Button ROI3 Red Detect",
                    IsPassed = false,
                    Message = "ROI3 button nam ngoai khung anh.",
                };
            }

            var roi3Colors = AnalyzeRoiColors(frame, rois[2]);
            var summary = $"ROI3={FormatRoiColors(roi3Colors)}";
            if (!string.Equals(summary, lastSummary, StringComparison.Ordinal))
            {
                lastSummary = summary;
                progress?.Report($"[ROI] {summary}");
            }

            if (roi3Colors.HasFlag(LedColorFlags.Red))
            {
                var passMessage = $"Button ROI3 Red Detect: {summary}.";
                _logger.LogInformation("{Message}", passMessage);
                progress?.Report("[ROI3][PASS]");
                progress?.Report($"[ACK][PASS] Button ROI3 Red Detect - {passMessage}");
                return new TestStepResult
                {
                    StepName = "Button ROI3 Red Detect",
                    IsPassed = true,
                    Message = $"[ACK][PASS] Button ROI3 Red Detect - {passMessage}",
                };
            }

            try
            {
                await Task.Delay(pollDelay, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var failMessage = $"[ACK][FAIL] Button ROI3 Red Detect - khong thay ROI3=Red trong {timeout.TotalSeconds:0.#}s.";
        progress?.Report(failMessage);
        return new TestStepResult
        {
            StepName = "Button ROI3 Red Detect",
            IsPassed = false,
            Message = failMessage,
        };
    }

    private static Rectangle BoundRoi(Bitmap frame, RoiRegion roi)
    {
        return Rectangle.Intersect(
            new Rectangle(Point.Empty, frame.Size),
            new Rectangle(roi.X, roi.Y, roi.Width, roi.Height));
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

    private static IReadOnlyList<Rectangle> ResolveButtonRois(Bitmap frame, RoiRegion roi, IReadOnlyList<RoiRegion>? buttonRois)
    {
        if (buttonRois is { Count: >= 3 })
        {
            return buttonRois
                .Take(3)
                .Select(item => BoundRoi(frame, item))
                .ToArray();
        }

        return CreateButtonRois(BoundRoi(frame, roi), frame.Size);
    }

    private static LedColorFlags AnalyzeRoiColors(Bitmap frame, Rectangle roi)
    {
        const int sampleStep = 4;
        var detected = LedColorFlags.None;

        for (var y = roi.Top; y < roi.Bottom; y += sampleStep)
        {
            for (var x = roi.Left; x < roi.Right; x += sampleStep)
            {
                var color = frame.GetPixel(x, y);

                if (LedColorThresholds.IsRed(color.R, color.G, color.B))
                {
                    detected |= LedColorFlags.Red;
                }

                if (LedColorThresholds.IsGreen(color.R, color.G, color.B))
                {
                    detected |= LedColorFlags.Green;
                }

                if (LedColorThresholds.IsBlue(color.R, color.G, color.B))
                {
                    detected |= LedColorFlags.Blue;
                }

                if (LedColorThresholds.IsYellow(color.R, color.G, color.B))
                {
                    detected |= LedColorFlags.Yellow;
                }

                if ((detected & (LedColorFlags.Red | LedColorFlags.Green | LedColorFlags.Yellow | LedColorFlags.Blue)) ==
                    (LedColorFlags.Red | LedColorFlags.Green | LedColorFlags.Yellow | LedColorFlags.Blue))
                {
                    return detected;
                }
            }
        }

        return detected;
    }

    private static string BuildMissingColors(LedColorFlags detected, LedColorFlags required)
    {
        var missing = new List<string>();
        if (required.HasFlag(LedColorFlags.Red) && !detected.HasFlag(LedColorFlags.Red))
        {
            missing.Add("Red");
        }

        if (required.HasFlag(LedColorFlags.Green) && !detected.HasFlag(LedColorFlags.Green))
        {
            missing.Add("Green");
        }

        if (required.HasFlag(LedColorFlags.Yellow) && !detected.HasFlag(LedColorFlags.Yellow))
        {
            missing.Add("Yellow");
        }

        return missing.Count == 0 ? "Missing=None" : $"Missing={string.Join(",", missing)}";
    }

    private static string BuildMissingButtonRoiColors(IReadOnlyList<LedColorFlags> detectedByRoi)
    {
        var missing = new List<string>();
        if (!detectedByRoi[0].HasFlag(LedColorFlags.Blue))
        {
            missing.Add("ROI1=Blue");
        }

        if (!detectedByRoi[1].HasFlag(LedColorFlags.Yellow))
        {
            missing.Add("ROI2=Yellow");
        }

        if (!detectedByRoi[2].HasFlag(LedColorFlags.Red))
        {
            missing.Add("ROI3=Red");
        }

        return missing.Count == 0 ? "Missing=None" : $"Missing={string.Join(",", missing)}";
    }

    private static string FormatRoiColors(LedColorFlags result)
    {
        if (result == LedColorFlags.None)
        {
            return "None";
        }

        var colors = new List<string>();
        if (result.HasFlag(LedColorFlags.Green))
        {
            colors.Add("Green");
        }

        if (result.HasFlag(LedColorFlags.Blue))
        {
            colors.Add("Blue");
        }

        if (result.HasFlag(LedColorFlags.Yellow))
        {
            colors.Add("Yellow");
        }

        if (result.HasFlag(LedColorFlags.Red))
        {
            colors.Add("Red");
        }

        return string.Join("+", colors);
    }

    [Flags]
    private enum LedColorFlags
    {
        None = 0,
        Red = 1,
        Green = 2,
        Yellow = 4,
        Blue = 8,
    }
}
