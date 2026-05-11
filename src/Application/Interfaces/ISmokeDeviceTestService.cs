using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface ISmokeDeviceTestService
{
    bool IsConnected { get; }
    bool IsDetectorConnected { get; }
    Task ConnectAsync(string g6tComPort, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task ConnectDetectorAsync(string detectorComPort, CancellationToken ct = default);
    Task DisconnectDetectorAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TestStepResult>> RunStartSequenceAsync(
        string g6tComPort,
        string detectorComPort,
        Rectangle roi1,
        string deviceType,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<TestStepResult>> SendResetAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default);
    Task PrepareOnConnectAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default);
}

