using FCT.G6T.Domain.Models;

namespace FCT.G6T.Domain.Interfaces;

public interface IQrCodeReaderAdapter : IDisposable
{
    bool IsConnected { get; }
    string ConnectedComPort { get; }
    Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<QrCodeData> ReadAsync(CancellationToken ct = default);
    Task<QrCodeData> ScanAsync(CancellationToken ct = default);
    Task StopScanAsync(CancellationToken ct = default);
}
