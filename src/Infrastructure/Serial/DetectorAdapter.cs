using System.IO;
using System.Text;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using FCT.G6T.HAL.Serial;
using Microsoft.Extensions.Logging;

namespace FCT.G6T.Infrastructure.Serial;

public class DetectorAdapter : IDetectorAdapter
{
    private const int DefaultBaudRate = 9600;
    private readonly int _retryCount;
    private readonly TimeSpan _ackTimeout;

    private const byte Soh = 0x01;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;
    private static readonly TimeSpan InvalidFrameRetryDelay = TimeSpan.FromSeconds(1);

    private readonly ISerialPortWrapper _port;
    private readonly ILogger<DetectorAdapter> _logger;
    private string _connectedComPort = string.Empty;

    public event EventHandler<DetectorTraceEventArgs>? Trace;

    public DetectorAdapter(ISerialPortWrapper port, ILogger<DetectorAdapter> logger, TimeSpan ackTimeout, int retryCount)
    {
        _port = port;
        _logger = logger;
        _ackTimeout = ackTimeout;
        _retryCount = Math.Max(0, retryCount);
    }

    public bool IsConnected => _port.IsOpen;
    public string ConnectedComPort => _connectedComPort;

    public async Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comPort))
        {
            throw new ArgumentException("COM port không hợp lệ.", nameof(comPort));
        }

        if (_port.IsOpen)
        {
            await _port.CloseAsync(ct).ConfigureAwait(false);
        }

        var effectiveBaudRate = baudRate > 0 ? baudRate : DefaultBaudRate;
        try
        {
            await _port.OpenAsync(comPort, effectiveBaudRate, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"COM {comPort} đang bị chiếm. Hãy đóng ứng dụng khác đang sử dụng cổng.", ex);
        }
        catch (HardwareException ex) when (ex.InnerException is UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"COM {comPort} đang bị chiếm. Hãy đóng ứng dụng khác đang sử dụng cổng.", ex);
        }
        catch (HardwareException ex)
        {
            throw new InvalidOperationException($"Không mở được COM {comPort}. {ex.Message}", ex);
        }

        _connectedComPort = comPort;
        _logger.LogInformation("Detector connected on {ComPort} @ {BaudRate}", comPort, effectiveBaudRate);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_port.IsOpen)
        {
            await _port.CloseAsync(ct).ConfigureAwait(false);
        }

        _connectedComPort = string.Empty;
    }


    // Theo Rule.md: PascalCase cho method, UPPER_SNAKE_CASE cho constant
    private const string ReadTemperatureValuePayload = "1.0.3()";
    private const string ReadTemperatureValuePrefix = "1.0.3(";
    private const string ReadSmokeValuePayload = "1.0.5()";
    private const string ReadSmokeValuePrefix = "1.0.5(";
    private const string ReadLoraRssiPayload = "1.0.H()";
    private const string ReadLoraRssiPrefix = "1.0.H(";

    public Task<DetectorResponse> ReadTemperatureValueAsync(CancellationToken ct = default)
    {
        return SendReadCommandAsync(ReadTemperatureValuePayload, ReadTemperatureValuePrefix, ct);
    }

    public Task<DetectorResponse> ReadTemperatureAsync(CancellationToken ct = default)
    {
        return ReadTemperatureValueAsync(ct);
    }

    public Task<DetectorResponse> ReadSmokeValueAsync(CancellationToken ct = default)
    {
        return SendReadCommandAsync(ReadSmokeValuePayload, ReadSmokeValuePrefix, ct);
    }

    public Task<DetectorResponse> ReadSmokeAsync(CancellationToken ct = default)
    {
        return ReadSmokeValueAsync(ct);
    }

    public Task<DetectorResponse> ReadLoraRssiAsync(CancellationToken ct = default)
    {
        return SendReadCommandAsync(ReadLoraRssiPayload, ReadLoraRssiPrefix, ct);
    }

    public Task<DetectorResponse> ReadLoraAsync(CancellationToken ct = default)
    {
        return ReadLoraRssiAsync(ct);
    }

    private async Task<DetectorResponse> SendReadCommandAsync(string payload, string expectedPrefix, CancellationToken ct)
    {
        var tx = BuildRequest(payload);
        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;
        Exception? lastError = null;
        var traceLines = new List<string>();

        for (var attempt = 1; attempt <= _retryCount + 1; attempt++)
        {
            var txMessage = $"DT TX [{portName}] attempt {attempt}: {ToHex(tx)}";
            traceLines.Add(txMessage);
            _logger.LogInformation("{Message}", txMessage);
            Trace?.Invoke(this, new DetectorTraceEventArgs(txMessage));
            await _port.SendAsync(tx, CancellationToken.None).ConfigureAwait(false);

            using (var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                attemptCts.CancelAfter(_ackTimeout);

                while (!attemptCts.IsCancellationRequested)
                {
                    byte[] rx;
                    try
                    {
                        rx = await _port.ReceiveAsync(Stx, Etx, attemptCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (TimeoutException ex)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(ct);
                        }

                        lastError = ex;
                        break;
                    }

                    try
                    {
                        var parsedPayload = ParseResponse(rx, expectedPrefix);
                        var rxMessage = $"DT RX [{portName}] attempt {attempt}: {ToHex(rx)}";
                        traceLines.Add(rxMessage);
                        _logger.LogDebug("{Message}", rxMessage);
                        Trace?.Invoke(this, new DetectorTraceEventArgs(rxMessage));
                        return new DetectorResponse
                        {
                            ComPort = portName,
                            TxFrame = tx,
                            RxFrame = rx,
                            Payload = parsedPayload,
                            Value = ExtractValue(parsedPayload),
                            TraceLines = traceLines.ToList(),
                        };
                    }
                    catch (InvalidDataException ex)
                    {
                        lastError = new InvalidDataException($"{ex.Message} | [TX][{portName}] {ToHex(tx)} | [RX][{portName}] {ToHex(rx)}", ex);
                        var retryMessage = $"DT RX [{portName}] attempt {attempt}: invalid frame, retry";
                        traceLines.Add(retryMessage);
                        _logger.LogWarning("{Message}", retryMessage);
                        Trace?.Invoke(this, new DetectorTraceEventArgs(retryMessage));
                    }
                }
            }

            if (attempt <= _retryCount)
            {
                var retryMessage = $"DT RX [{portName}] attempt {attempt}: timeout/invalid frame, retry after {InvalidFrameRetryDelay.TotalSeconds:0.#}s";
                traceLines.Add(retryMessage);
                _logger.LogWarning("{Message}", retryMessage);
                Trace?.Invoke(this, new DetectorTraceEventArgs(retryMessage));
                await Task.Delay(InvalidFrameRetryDelay, ct).ConfigureAwait(false);
            }
        }

        throw lastError ?? new TimeoutException($"Detector timeout | [TX][{portName}] {ToHex(tx)} | [RX][{portName}] <empty>");
    }

    private static string FormatDetectorValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<empty>";
        // represent SOH as <SOH>
        var sb = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            if (ch == (char)1)
            {
                sb.Append("<SOH>");
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static byte[] BuildRequest(string payload)
    {
        var body = new List<byte>
        {
            Soh,
            (byte)'R',
            (byte)'1',
            Stx,
        };

        body.AddRange(Encoding.ASCII.GetBytes(payload));
        body.Add(Etx);
        body.Add(CalcBcc(body.ToArray()));
        return body.ToArray();
    }

    private static string ParseResponse(byte[] frame, string expectedPrefix)
    {
        if (frame.Length < 4)
        {
            throw new InvalidDataException($"Response quá ngắn: {frame.Length} bytes");
        }

        if (frame[0] != Stx)
        {
            throw new InvalidDataException($"STX không hợp lệ: 0x{frame[0]:X2}");
        }

        var etxIndex = Array.IndexOf(frame, Etx, 1);
        if (etxIndex < 0 || etxIndex >= frame.Length - 1)
        {
            throw new InvalidDataException("Không tìm thấy ETX/BCC hợp lệ trong response.");
        }

        var frameWithoutBcc = frame[..^1];
        var expectedBcc = CalcBcc(frameWithoutBcc);
        var actualBcc = frame[^1];
        if (expectedBcc != actualBcc)
        {
            throw new InvalidDataException($"BCC không khớp — expected 0x{expectedBcc:X2}, got 0x{actualBcc:X2}");
        }

        var payload = Encoding.ASCII.GetString(frame, 1, etxIndex - 1);
        if (!payload.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Payload không đúng định dạng mong đợi: {payload}");
        }

        return payload;
    }

    private static string ExtractValue(string payload)
    {
        var start = payload.IndexOf('(');
        var end = payload.LastIndexOf(')');
        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        return payload[(start + 1)..end];
    }

    private static byte CalcBcc(byte[] buffer)
    {
        byte bcc = 0;
        for (var i = 1; i < buffer.Length; i++)
        {
            bcc ^= buffer[i];
        }

        return bcc;
    }

    private static string ToHex(byte[] data)
    {
        if (data.Length == 0)
        {
            return "<empty>";
        }

        return string.Join(" ", data.Select(b => $"{b:X2}"));
    }

    public void Dispose()
    {
        _connectedComPort = string.Empty;
        _port.Dispose();
    }
}

