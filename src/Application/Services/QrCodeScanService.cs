using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Services;

public sealed class QrCodeScanService : IQrCodeScanService
{
    private readonly IQrCodeReaderAdapter _qrCodeReaderAdapter;

    public QrCodeScanService(IQrCodeReaderAdapter qrCodeReaderAdapter)
    {
        _qrCodeReaderAdapter = qrCodeReaderAdapter;
    }

    public bool IsConnected => _qrCodeReaderAdapter.IsConnected;
    public string ConnectedComPort => _qrCodeReaderAdapter.ConnectedComPort;

    public Task ConnectAsync(string qrComPort, CancellationToken ct = default)
    {
        return _qrCodeReaderAdapter.ConnectAsync(qrComPort, 0, ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        return _qrCodeReaderAdapter.DisconnectAsync(ct);
    }

    public Task<QrCodeData> ScanAsync(CancellationToken ct = default)
    {
        return _qrCodeReaderAdapter.ScanAsync(ct);
    }
}
