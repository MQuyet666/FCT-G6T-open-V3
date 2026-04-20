using System.IO;
using HardwareTestApp.src.Domain.Interfaces;
using HardwareTestApp.src.Domain.Models;
using HardwareTestApp.src.HAL;
using Microsoft.Extensions.Logging;

namespace HardwareTestApp.src.Infrastructure.Serial;

public class G6TAdapter : IG6TAdapter
{
    private const int DefaultG6tBaudRate = 9600;
    private const int FrameRetryCount = 1;
    private static readonly TimeSpan FrameAckTimeout = TimeSpan.FromSeconds(3);
    private static readonly byte[] FrameHeader = { 0x1F, 0x2F, 0x3F, 0xFF };
    private const byte DirSend = 0x00;
    private const byte DirReceive = 0x01;

    private readonly ISerialPortWrapper _port;
    private readonly ILogger<G6TAdapter> _logger;
    private string _connectedComPort = string.Empty;

    public bool IsConnected => _port.IsOpen;
    public string ConnectedComPort => _connectedComPort;

    public G6TAdapter(ISerialPortWrapper port, ILogger<G6TAdapter> logger)
    {
        _port = port;
        _logger = logger;
    }

    public void Connect(string comPort, int baudRate)
    {
        if (string.IsNullOrWhiteSpace(comPort))
        {
            throw new ArgumentException("COM port không hợp lệ.", nameof(comPort));
        }

        if (_port.IsOpen)
        {
            _port.Close();
        }

        var effectiveBaudRate = baudRate > 0 ? baudRate : DefaultG6tBaudRate;
        _port.Open(comPort, effectiveBaudRate);
        _connectedComPort = comPort;
        _logger.LogInformation("G6T connected on {ComPort} @ {BaudRate}", comPort, effectiveBaudRate);
    }

    public void Disconnect()
    {
        if (_port.IsOpen)
        {
            _port.Close();
            _logger.LogInformation("G6T disconnected.");
        }

        _connectedComPort = string.Empty;
    }

    public async Task<G6TResponse> SendCommandAsync(G6TCommand command, CancellationToken ct = default)
    {
        var frame = BuildFrame(command);
        var portName = string.IsNullOrWhiteSpace(_connectedComPort) ? "UNKNOWN" : _connectedComPort;

        Exception? lastException = null;
        for (var attempt = 1; attempt <= FrameRetryCount + 1; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("G6T TX [{Port}] attempt {Attempt}: {Frame}", portName, attempt, BytesToHex(frame));
            // Ensure the TX is written even if the caller's timeout token is cancelled
            await _port.WriteAsync(frame, CancellationToken.None).ConfigureAwait(false);

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(FrameAckTimeout);

            while (!attemptCts.IsCancellationRequested)
            {
                var raw = await _port.ReadFrameAsync(attemptCts.Token).ConfigureAwait(false);
                _logger.LogDebug("G6T RX [{Port}] attempt {Attempt}: {Frame}", portName, attempt, BytesToHex(raw));

                if (raw.Length < 8)
                {
                    lastException = new InvalidDataException($"Response quá ngắn: {raw.Length} bytes | [PORT][{portName}] IsOpen={_port.IsOpen} | [TX][{portName}] {BytesToHex(frame)} | [RX][{portName}] {BytesToHex(raw)}");
                    continue;
                }

                G6TResponse parsed;
                try
                {
                    parsed = ParseResponse(raw, frame, portName);
                }
                catch (InvalidDataException ex)
                {
                    lastException = ex;
                    continue;
                }

                if (parsed.CommandId != command.CommandId)
                {
                    lastException = new InvalidDataException(
                        $"ACK command mismatch — expected {command.CommandId}, got {parsed.CommandId} | [PORT][{portName}] IsOpen={_port.IsOpen} | [TX][{portName}] {BytesToHex(frame)} | [RX][{portName}] {BytesToHex(raw)}");
                    _logger.LogWarning("{Message}", lastException.Message);
                    continue;
                }

                return new G6TResponse
                {
                    CommandId = parsed.CommandId,
                    Status = parsed.Status,
                    ComPort = portName,
                    IsOpen = _port.IsOpen,
                    TxFrame = frame,
                    RxFrame = raw,
                };
            }
        }

        throw lastException ?? new TimeoutException($"ACK timeout | [PORT][{portName}] IsOpen={_port.IsOpen} | [TX][{portName}] {BytesToHex(frame)} | [RX][{portName}] <empty>");
    }

    internal byte[] BuildFrame(G6TCommand command)
    {
        var body = new List<byte>();
        body.AddRange(FrameHeader);
        body.Add(DirSend);
        body.Add((byte)command.CommandId);
        body.AddRange(command.Data);

        var bcc = CalcBcc(body.ToArray());
        body.Add(bcc);

        return body.ToArray();
    }

    internal static byte CalcBcc(byte[] frame)
    {
        byte bcc = 0;
        for (var i = 1; i < frame.Length; i++)
        {
            bcc ^= frame[i];
        }

        return bcc;
    }

    private G6TResponse ParseResponse(byte[] raw, byte[] txFrame, string portName)
    {
        if (raw[0] != FrameHeader[0] || raw[1] != FrameHeader[1] || raw[2] != FrameHeader[2] || raw[3] != FrameHeader[3])
        {
            throw new InvalidDataException($"Header response không hợp lệ | [PORT][{portName}] IsOpen={_port.IsOpen} | [TX][{portName}] {BytesToHex(txFrame)} | [RX][{portName}] {BytesToHex(raw)}");
        }

        var bodyWithoutBcc = raw[..^1];
        var expectedBcc = CalcBcc(bodyWithoutBcc);
        var actualBcc = raw[^1];

        if (expectedBcc != actualBcc)
        {
            throw new InvalidDataException($"BCC không khớp — expected 0x{expectedBcc:X2}, got 0x{actualBcc:X2} | [PORT][{portName}] IsOpen={_port.IsOpen} | [TX][{portName}] {BytesToHex(txFrame)} | [RX][{portName}] {BytesToHex(raw)}");
        }

        if (raw[4] != DirReceive)
        {
            throw new InvalidDataException($"Direction byte không hợp lệ: 0x{raw[4]:X2} | [PORT][{portName}] IsOpen={_port.IsOpen} | [TX][{portName}] {BytesToHex(txFrame)} | [RX][{portName}] {BytesToHex(raw)}");
        }

        var cmdId = (G6TCommandId)raw[5];
        var status = (G6TStatus)raw[6];

        return new G6TResponse
        {
            CommandId = cmdId,
            Status = status,
        };
    }

    private static string BytesToHex(byte[] data)
    {
        if (data is null || data.Length == 0)
        {
            return "<empty>";
        }

        return string.Join(" ", data.Select(b => $"{b:X2}"));
    }

    public void Dispose()
    {
        Disconnect();
        _port.Dispose();
    }
}
