namespace HardwareTestApp.src.HAL;

public interface ISerialPortWrapper : IDisposable
{
    bool IsOpen { get; }
    void Open(string portName, int baudRate = 115200);
    void Close();
    Task WriteAsync(byte[] data, CancellationToken ct = default);
    Task<byte[]> ReadFrameAsync(CancellationToken ct = default);
}
