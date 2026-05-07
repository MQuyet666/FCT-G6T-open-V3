using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface IQrCodeScanService
{
    bool IsConnected { get; }
    string ConnectedComPort { get; }
    Task ConnectAsync(string qrComPort, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<QrCodeData> ScanAsync(CancellationToken ct = default);
}
