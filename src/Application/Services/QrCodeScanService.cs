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

    public Task StopScanAsync(CancellationToken ct = default)
    {
        return _qrCodeReaderAdapter.StopScanAsync(ct);
    }

    public async Task<QrScanResult> RunStartScanAsync(QrScanMode mode, string? qrComPort, CancellationToken ct = default)
    {
        if (mode == QrScanMode.NoUse)
        {
            return QrScanResult.Skipped("QR No use: bo qua quet QR.");
        }

        if (string.IsNullOrWhiteSpace(qrComPort))
        {
            throw new ArgumentException("Vui long chon QR COM truoc khi chay test.", nameof(qrComPort));
        }

        if (!_qrCodeReaderAdapter.IsConnected ||
            !string.Equals(_qrCodeReaderAdapter.ConnectedComPort, qrComPort, StringComparison.OrdinalIgnoreCase))
        {
            await _qrCodeReaderAdapter.ConnectAsync(qrComPort, 0, ct).ConfigureAwait(false);
        }

        try
        {
            var qrData = await _qrCodeReaderAdapter.ScanAsync(ct).ConfigureAwait(false);
            return QrScanResult.Scanned(qrData);
        }
        catch (TimeoutException ex)
        {
            return QrScanResult.Failed(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return QrScanResult.Failed(ex.Message);
        }
        finally
        {
            await _qrCodeReaderAdapter.StopScanAsync(ct).ConfigureAwait(false);
        }
    }
}
