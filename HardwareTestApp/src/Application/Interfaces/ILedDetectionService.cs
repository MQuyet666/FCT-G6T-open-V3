using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface ILedDetectionService
{
    Task<TestStepResult> DetectSmokeLedColorsAsync(
        RoiRegion roi,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct = default);

    Task<TestStepResult> DetectButtonLedColorsAsync(
        RoiRegion roi,
        IReadOnlyList<RoiRegion>? buttonRois,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct = default);

    Task<TestStepResult> DetectButtonRoi3RedAsync(
        RoiRegion roi,
        IReadOnlyList<RoiRegion>? buttonRois,
        TimeSpan timeout,
        TimeSpan pollDelay,
        IProgress<string>? progress,
        CancellationToken ct = default);
}
