using FCT.G6T.Domain.Models;

namespace FCT.G6T.Domain.Interfaces;

public interface IDetectorAdapter : IDisposable
{
    bool IsConnected { get; }
    string ConnectedComPort { get; }
    void Connect(string comPort, int baudRate);
    void Disconnect();
    Task<DetectorResponse> ReadTemperatureAsync(CancellationToken ct = default);
    Task<DetectorResponse> ReadSmokeAsync(CancellationToken ct = default);
    Task<DetectorResponse> ReadLoraAsync(CancellationToken ct = default);
}

