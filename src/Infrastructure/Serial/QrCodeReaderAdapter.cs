using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using FCT.G6T.HAL.Serial;
using Microsoft.Extensions.Logging;

namespace FCT.G6T.Infrastructure.Serial;

public sealed class QrCodeReaderAdapter : IQrCodeReaderAdapter
{
    private const int FallbackBaudRate = 9600;
    private static readonly byte[] TriggerScanCommand = { 0x1B, (byte)'Z', 0x0D };
    private static readonly byte[] StopScanCommand = { 0x1B, (byte)'Y', 0x0D };

    private readonly ISerialPortWrapper _port;
    private readonly ILogger<QrCodeReaderAdapter> _logger;
    private readonly TimeSpan _readTimeout;
    private readonly int _defaultBaudRate;
    private string _connectedComPort = string.Empty;
    private int _connectedBaudRate;

    public QrCodeReaderAdapter(
        ISerialPortWrapper port,
        ILogger<QrCodeReaderAdapter> logger,
        TimeSpan readTimeout,
        int defaultBaudRate)
    {
        _port = port;
        _logger = logger;
        _readTimeout = readTimeout;
        _defaultBaudRate = defaultBaudRate > 0 ? defaultBaudRate : FallbackBaudRate;
        _connectedBaudRate = _defaultBaudRate;
    }

    public bool IsConnected => _port.IsOpen;
    public string ConnectedComPort => _connectedComPort;

    public async Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comPort))
        {
            throw new ArgumentException("COM port khong hop le.", nameof(comPort));
        }

        if (_port.IsOpen)
        {
            await _port.CloseAsync(ct).ConfigureAwait(false);
        }

        var effectiveBaudRate = baudRate > 0 ? baudRate : _defaultBaudRate;
        try
        {
            await _port.OpenAsync(comPort, effectiveBaudRate, ct).ConfigureAwait(false);
        }
        catch (HardwareException ex) when (ex.InnerException is UnauthorizedAccessException)
        {
            throw new HardwareException($"COM {comPort} dang bi chiem. Hay dong ung dung khac dang su dung cong.", ex);
        }
        catch (HardwareException ex)
        {
            throw new HardwareException($"Khong mo duoc COM QR {comPort}. {ex.Message}", ex);
        }

        _connectedComPort = comPort;
        _connectedBaudRate = effectiveBaudRate;
        _logger.LogInformation("QR reader connected on {ComPort} @ {BaudRate}", comPort, effectiveBaudRate);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_port.IsOpen)
        {
            await _port.CloseAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("QR reader disconnected.");
        }

        _connectedComPort = string.Empty;
        _connectedBaudRate = _defaultBaudRate;
    }

    public async Task<QrCodeData> ReadAsync(CancellationToken ct = default)
    {
        if (!_port.IsOpen)
        {
            throw new InvalidOperationException("QR reader chua ket noi COM.");
        }

        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readCts.CancelAfter(_readTimeout);
        try
        {
            var value = await _port.ReceiveLineAsync(readCts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"QR data rong | [PORT][{portName}]");
            }

            _logger.LogInformation("QR RX [{ComPort}]: {Value}", portName, value);
            return new QrCodeData
            {
                ComPort = portName,
                BaudRate = _connectedBaudRate,
                Value = value.Trim(),
                ReceivedAt = DateTimeOffset.Now,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("QR read timeout | [PORT][{ComPort}]", portName);
            throw new TimeoutException($"QR read timeout | [PORT][{portName}]");
        }
        catch (TimeoutException ex)
        {
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            _logger.LogWarning(ex, "QR read timeout | [PORT][{ComPort}]", portName);
            throw new TimeoutException($"QR read timeout | [PORT][{portName}]", ex);
        }
    }

    public async Task<QrCodeData> ScanAsync(CancellationToken ct = default)
    {
        if (!_port.IsOpen)
        {
            throw new InvalidOperationException("QR reader chua ket noi COM.");
        }

        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;
        _logger.LogInformation("QR TX [{ComPort}]: {Frame}", portName, ToHex(TriggerScanCommand));
        await _port.SendAsync(TriggerScanCommand, ct).ConfigureAwait(false);

        return await ReadAsync(ct).ConfigureAwait(false);
    }

    public async Task StopScanAsync(CancellationToken ct = default)
    {
        if (!_port.IsOpen)
        {
            return;
        }

        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;
        _logger.LogInformation("QR TX [{ComPort}]: {Frame}", portName, ToHex(StopScanCommand));
        await _port.SendAsync(StopScanCommand, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _connectedComPort = string.Empty;
        _connectedBaudRate = _defaultBaudRate;
        _port.Dispose();
    }

    private static string ToHex(byte[] data)
    {
        return data.Length == 0
            ? "<empty>"
            : string.Join(" ", data.Select(b => $"{b:X2}"));
    }
}
