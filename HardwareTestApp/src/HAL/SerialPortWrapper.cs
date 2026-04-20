using System.IO.Ports;

namespace HardwareTestApp.src.HAL;

public class SerialPortWrapper : ISerialPortWrapper
{
    private const int ReadTimeoutMs = 100;
    private const int ResponseLength = 8;

    private SerialPort? _port;
    private readonly object _ioLock = new();

    public bool IsOpen => _port?.IsOpen ?? false;

    public void Open(string portName, int baudRate)
    {
        _port?.Dispose();
        var port = new SerialPort(portName, baudRate)
        {
            ReadTimeout = ReadTimeoutMs,
            WriteTimeout = 1000,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
        };

        try
        {
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            _port = port;
        }
        catch (UnauthorizedAccessException ex)
        {
            port.Dispose();
            throw new InvalidOperationException($"Không thể mở cổng {portName}: cổng đang bị chiếm hoặc không có quyền truy cập.", ex);
        }
        catch (IOException ex)
        {
            port.Dispose();
            throw new InvalidOperationException($"Không thể mở cổng {portName}: lỗi I/O khi mở cổng.", ex);
        }
        catch (ArgumentException ex)
        {
            port.Dispose();
            throw new InvalidOperationException($"Không thể mở cổng {portName}: tên cổng không hợp lệ.", ex);
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        var port = _port ?? throw new InvalidOperationException("Serial port chưa được khởi tạo.");

        if (!port.IsOpen)
        {
            throw new InvalidOperationException("Serial port chưa mở.");
        }

        await Task.Run(() =>
        {
            lock (_ioLock)
            {
                // write must be atomic with respect to any concurrent read
                port.Write(data, 0, data.Length);
                port.BaseStream.Flush();
            }
        }, ct).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadVariableFrameAsync(byte startByte, byte endByte, CancellationToken ct = default)
    {
        var port = _port ?? throw new InvalidOperationException("Serial port chưa được khởi tạo.");

        return await Task.Run(() =>
        {
            lock (_ioLock)
            {
                var buffer = new List<byte>();
                var started = false;

                while (true)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return Array.Empty<byte>();
                    }

                    if (!port.IsOpen)
                    {
                        return Array.Empty<byte>();
                    }

                    if (port.BytesToRead <= 0)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    var value = port.ReadByte();
                    if (value < 0)
                    {
                        continue;
                    }

                    var current = (byte)value;
                    if (!started)
                    {
                        if (current == startByte)
                        {
                            started = true;
                            buffer.Add(current);
                        }

                        continue;
                    }

                    buffer.Add(current);
                    if (current == endByte)
                    {
                        while (port.BytesToRead <= 0)
                        {
                            if (ct.IsCancellationRequested)
                            {
                                return Array.Empty<byte>();
                            }

                            if (!port.IsOpen)
                            {
                                return Array.Empty<byte>();
                            }

                            Thread.Sleep(5);
                        }

                        var bcc = port.ReadByte();
                        if (bcc < 0)
                        {
                            return Array.Empty<byte>();
                        }

                        buffer.Add((byte)bcc);
                        return buffer.ToArray();
                    }
                }
            }
        }, ct).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadFrameAsync(CancellationToken ct = default)
    {
        var port = _port ?? throw new InvalidOperationException("Serial port chưa được khởi tạo.");

        return await Task.Run(() =>
        {
            lock (_ioLock)
            {
                var buffer = new byte[ResponseLength];
                var read = 0;

                while (read < ResponseLength)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return Array.Empty<byte>();
                    }

                    if (!port.IsOpen)
                    {
                        return Array.Empty<byte>();
                    }

                    if (port.BytesToRead <= 0)
                    {
                        // small sleep to avoid busy loop
                        Thread.Sleep(5);
                        continue;
                    }

                    try
                    {
                        var value = port.ReadByte();
                        if (value < 0)
                        {
                            continue;
                        }

                        buffer[read++] = (byte)value;
                    }
                    catch (InvalidOperationException)
                    {
                        throw new InvalidOperationException("Serial port không còn hợp lệ khi đọc dữ liệu.");
                    }
                }

                return buffer;
            }
        }, ct).ConfigureAwait(false);
    }

    public void Close()
    {
        _port?.Close();
    }

    public void Dispose()
    {
        Close();
        _port?.Dispose();
    }
}
