using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FCT.G6T.Application.Services;

public class HeatDeviceTestService : SmokeDeviceTestService, IHeatDeviceTestService
{
    public HeatDeviceTestService(
        TestOrchestrator testOrchestrator,
        IG6TAdapter g6tAdapter,
        IDetectorAdapter detectorAdapter,
        ICameraPreviewAppService cameraPreview,
        ILogger<SmokeDeviceTestService> logger,
        int g6tBaudRate,
        int detectorBaudRate,
        TimeSpan ackTimeout,
        TimeSpan detectorAckTimeout,
        TimeSpan ledDetectTimeout,
        TimeSpan buttonTestTimeout,
        TimeSpan ledDetectPollDelay)
        : base(
            testOrchestrator,
            g6tAdapter,
            detectorAdapter,
            cameraPreview,
            logger,
            g6tBaudRate,
            detectorBaudRate,
            ackTimeout,
            detectorAckTimeout,
            ledDetectTimeout,
            buttonTestTimeout,
            ledDetectPollDelay)
    {
    }

    protected override Task<DetectorResponse> ReadDetectorValueAsync(CancellationToken ct)
    {
        return _detectorAdapter.ReadTemperatureAsync(ct);
    }

    protected override bool ValidateDetectorValueResponse(DetectorResponse response)
    {
        return response.Payload.StartsWith("1.0.3(", StringComparison.Ordinal) &&
            response.Payload.EndsWith(")", StringComparison.Ordinal);
    }
}
