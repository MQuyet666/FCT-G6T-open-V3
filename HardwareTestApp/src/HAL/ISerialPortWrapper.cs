namespace FCT.G6T.HAL.Serial;

public interface ISerialPortWrapper : IDisposable
{
    bool IsOpen { get; }
    Task OpenAsync(string portName, int baudRate, CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);
    Task SendAsync(byte[] data, CancellationToken ct = default);
    Task<byte[]> ReceiveAsync(CancellationToken ct = default);
    Task<byte[]> ReceiveAsync(byte startByte, byte endByte, CancellationToken ct = default);
    Task<string> ReceiveLineAsync(CancellationToken ct = default);
}

