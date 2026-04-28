namespace FCT.G6T.HAL;

public interface ISerialPortWrapper : IDisposable
{
    bool IsOpen { get; }
    void Open(string portName, int baudRate);
    void Close();
    Task WriteAsync(byte[] data, CancellationToken ct = default);
    Task<byte[]> ReadFrameAsync(CancellationToken ct = default);
    Task<byte[]> ReadVariableFrameAsync(byte startByte, byte endByte, CancellationToken ct = default);
}

