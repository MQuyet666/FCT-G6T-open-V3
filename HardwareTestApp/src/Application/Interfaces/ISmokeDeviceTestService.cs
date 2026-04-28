using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface ISmokeDeviceTestService
{
    bool IsConnected { get; }
    bool IsDetectorConnected { get; }
    void Connect(string g6tComPort);
    void Disconnect();
    void ConnectDetector(string detectorComPort);
    void DisconnectDetector();
    Task<IReadOnlyList<TestStepResult>> RunStartSequenceAsync(
        string g6tComPort,
        string detectorComPort,
        Rectangle roi1,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
    Task SendResetAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default);
    Task PrepareOnConnectAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default);
}

