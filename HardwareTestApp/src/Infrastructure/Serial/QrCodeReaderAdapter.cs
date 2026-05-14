using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using FCT.G6T.HAL.Serial;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using DomainHardwareException = FCT.G6T.Domain.Exceptions.HardwareException;
using HalHardwareException = FCT.G6T.HAL.Serial.HardwareException;

namespace FCT.G6T.Infrastructure.Serial;

public sealed class QrCodeReaderAdapter : IQrCodeReaderAdapter
{
    private const int FallbackBaudRate = 9600;
    private static readonly QrCommandSet OpticonCommands = new(
        "Opticon",
        "065A",
        "A002",
        new byte[] { 0x1B, (byte)'Z', 0x0D },
        new byte[] { 0x1B, (byte)'Y', 0x0D });
    private static readonly QrCommandSet Gm65Commands = new(
        "GM65",
        "6666",
        "7777",
        new byte[] { 0x7E, 0x00, 0x08, 0x01, 0x00, 0x02, 0x01, 0xAB, 0xCD },
        new byte[] { 0x7E, 0x00, 0x08, 0x01, 0x00, 0x02, 0x00, 0xAB, 0xCD });
    private static readonly QrCommandSet[] KnownCommandSets = { OpticonCommands, Gm65Commands };

    private readonly ISerialPortWrapper _port;
    private readonly ILogger<QrCodeReaderAdapter> _logger;
    private readonly TimeSpan _readTimeout;
    private readonly int _defaultBaudRate;
    private string _connectedComPort = string.Empty;
    private int _connectedBaudRate;
    private QrCommandSet _activeCommandSet = Gm65Commands;

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

        _activeCommandSet = ResolveCommandSet(comPort);
        var effectiveBaudRate = baudRate > 0 ? baudRate : _defaultBaudRate;
        try
        {
            await _port.OpenAsync(comPort, effectiveBaudRate, ct).ConfigureAwait(false);
        }
        catch (HalHardwareException ex) when (ex.InnerException is UnauthorizedAccessException)
        {
            throw new DomainHardwareException($"COM {comPort} dang bi chiem. Hay dong ung dung khac dang su dung cong.", ex);
        }
        catch (HalHardwareException ex)
        {
            throw new DomainHardwareException($"Khong mo duoc COM QR {comPort}. {ex.Message}", ex);
        }

        _connectedComPort = comPort;
        _connectedBaudRate = effectiveBaudRate;
        _logger.LogInformation(
            "QR reader connected on {ComPort} @ {BaudRate} | Profile={Profile} VID:PID={Vid}:{Pid}",
            comPort,
            effectiveBaudRate,
            _activeCommandSet.Name,
            _activeCommandSet.Vid,
            _activeCommandSet.Pid);
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
        _activeCommandSet = Gm65Commands;
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
            while (true)
            {
                var rawValue = await _port.ReceiveLineAsync(readCts.Token).ConfigureAwait(false);
                var value = NormalizeQrValue(rawValue);
                if (string.IsNullOrWhiteSpace(value))
                {
                    _logger.LogInformation("QR RX ignored control/empty data [{ComPort}]: {Value}", portName, ToHex(rawValue));
                    continue;
                }

                _logger.LogInformation("QR RX [{ComPort}]: {Value}", portName, value);
                return new QrCodeData
                {
                    ComPort = portName,
                    BaudRate = _connectedBaudRate,
                    Value = value,
                    ReceivedAt = DateTimeOffset.Now,
                };
            }
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
        catch (HalHardwareException ex)
        {
            throw new DomainHardwareException($"QR COM {portName} hardware error while reading data.", ex);
        }
    }

    public async Task<QrCodeData> ScanAsync(CancellationToken ct = default)
    {
        if (!_port.IsOpen)
        {
            throw new InvalidOperationException("QR reader chua ket noi COM.");
        }

        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;
        _logger.LogInformation("QR TX [{ComPort}] {Profile}: {Frame}", portName, _activeCommandSet.Name, ToHex(_activeCommandSet.TriggerScanCommand));
        try
        {
            await _port.SendAsync(_activeCommandSet.TriggerScanCommand, ct).ConfigureAwait(false);
        }
        catch (HalHardwareException ex)
        {
            throw new DomainHardwareException($"QR COM {portName} hardware error while triggering scan.", ex);
        }

        return await ReadAsync(ct).ConfigureAwait(false);
    }

    public async Task StopScanAsync(CancellationToken ct = default)
    {
        if (!_port.IsOpen)
        {
            return;
        }

        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;
        _logger.LogInformation("QR TX [{ComPort}] {Profile}: {Frame}", portName, _activeCommandSet.Name, ToHex(_activeCommandSet.StopScanCommand));
        try
        {
            await _port.SendAsync(_activeCommandSet.StopScanCommand, ct).ConfigureAwait(false);
        }
        catch (HalHardwareException ex)
        {
            throw new DomainHardwareException($"QR COM {portName} hardware error while stopping scan.", ex);
        }
    }

    public void Dispose()
    {
        _connectedComPort = string.Empty;
        _connectedBaudRate = _defaultBaudRate;
        _activeCommandSet = Gm65Commands;
        _port.Dispose();
    }

    private static string ToHex(byte[] data)
    {
        return data.Length == 0
            ? "<empty>"
            : string.Join(" ", data.Select(b => $"{b:X2}"));
    }

    private static string ToHex(string value)
    {
        return string.IsNullOrEmpty(value)
            ? "<empty>"
            : string.Join(" ", value.Select(ch => $"{(int)ch:X2}"));
    }

    private static string NormalizeQrValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return new string(value.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
    }

    private QrCommandSet ResolveCommandSet(string comPort)
    {
        var hardwareId = TryGetHardwareIdForComPort(comPort);
        if (!string.IsNullOrWhiteSpace(hardwareId))
        {
            var commandSet = KnownCommandSets.FirstOrDefault(set =>
                hardwareId.Contains($"VID_{set.Vid}", StringComparison.OrdinalIgnoreCase) &&
                hardwareId.Contains($"PID_{set.Pid}", StringComparison.OrdinalIgnoreCase));
            if (commandSet is not null)
            {
                _logger.LogInformation("QR profile detected on {ComPort}: {Profile} | HWID={HardwareId}", comPort, commandSet.Name, hardwareId);
                return commandSet;
            }

            _logger.LogWarning("QR profile unknown on {ComPort} | HWID={HardwareId}. Fallback profile={Profile}", comPort, hardwareId, Gm65Commands.Name);
        }
        else
        {
            _logger.LogWarning("QR HWID not found for {ComPort}. Fallback profile={Profile}", comPort, Gm65Commands.Name);
        }

        return Gm65Commands;
    }

    private static string TryGetHardwareIdForComPort(string comPort)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum");
            return root is null ? string.Empty : FindHardwareId(root, root.Name, comPort);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindHardwareId(RegistryKey key, string keyPath, string comPort)
    {
        using (var deviceParameters = key.OpenSubKey("Device Parameters"))
        {
            var portName = deviceParameters?.GetValue("PortName") as string;
            if (string.Equals(portName, comPort, StringComparison.OrdinalIgnoreCase))
            {
                return keyPath;
            }
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            try
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey is null)
                {
                    continue;
                }

                var hardwareId = FindHardwareId(subKey, $@"{keyPath}\{subKeyName}", comPort);
                if (!string.IsNullOrWhiteSpace(hardwareId))
                {
                    return hardwareId;
                }
            }
            catch
            {
                // Some Enum branches are protected or transient. Skip them.
            }
        }

        return string.Empty;
    }

    private sealed record QrCommandSet(
        string Name,
        string Vid,
        string Pid,
        byte[] TriggerScanCommand,
        byte[] StopScanCommand);
}
