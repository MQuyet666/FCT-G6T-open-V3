using System.IO.Ports;

namespace FCT.G6T.HAL;

public class SerialPortWrapper : ISerialPortWrapper
{
    private readonly int _readTimeoutMs;
    private readonly int _writeTimeoutMs;
    private const int ResponseLength = 8;

    private SerialPort? _port;
    private readonly object _ioLock = new();

    public SerialPortWrapper(int readTimeoutMs, int writeTimeoutMs)
    {
        _readTimeoutMs = readTimeoutMs;
        _writeTimeoutMs = writeTimeoutMs;
    }

    public bool IsOpen => _port?.IsOpen ?? false;

    public void Open(string portName, int baudRate)
    {
        _port?.Dispose();
        var port = new SerialPort(portName, baudRate)
        {
            ReadTimeout = _readTimeoutMs,
            WriteTimeout = _writeTimeoutMs,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
        };

        var opened = false;
        try
        {
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();

            // Test write to verify port is actually available
            // Some ports may open successfully but fail on first write
            try
            {
                port.Write(new byte[] { 0x00 }, 0, 1);
                port.BaseStream.Flush();
            }
            catch (Exception)
            {
                port.Dispose();
                throw new InvalidOperationException($"Không thể ghi vào cổng {portName}: cổng không khả dụng hoặc không kết nối.");
            }

            _port = port;
            opened = true;
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
        finally
        {
            if (!opened)
            {
                port.Dispose();
            }
        }
    }

    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        SerialPort? portSnapshot = null;
        lock (_ioLock)
        {
            portSnapshot = _port;
        }
        if (portSnapshot == null)
            throw new InvalidOperationException("Serial port chưa được khởi tạo.");
        if (!portSnapshot.IsOpen)
            throw new InvalidOperationException("Serial port chưa mở.");

        try
        {
            await Task.Run(() =>
            {
                lock (_ioLock)
                {
                    if (portSnapshot == null)
                        throw new InvalidOperationException("Serial port chưa được khởi tạo.");
                    if (!portSnapshot.IsOpen)
                        throw new InvalidOperationException("Serial port chưa mở.");
                    try
                    {
                        portSnapshot.Write(data, 0, data.Length);
                        portSnapshot.BaseStream.Flush();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        throw new InvalidOperationException("Serial port đã bị dispose khi ghi.", ex);
                    }
                }
            }, ct).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            var portName = portSnapshot?.PortName ?? "(unknown)";
            throw new TimeoutException($"Ghi dữ liệu vào cổng {portName} timeout sau {_writeTimeoutMs}ms.", ex);
        }
        catch (IOException ex)
        {
            var portName = portSnapshot?.PortName ?? "(unknown)";
            throw new IOException($"Lỗi I/O khi ghi vào cổng {portName}: cổng có thể đã bị ngắt kết nối.", ex);
        }
        catch (InvalidOperationException ex)
        {
            var portName = portSnapshot?.PortName ?? "(unknown)";
            throw new InvalidOperationException($"Cổng {portName} không còn hợp lệ để ghi.", ex);
        }
    }

    public async Task<byte[]> ReadVariableFrameAsync(byte startByte, byte endByte, CancellationToken ct = default)
    {
        var port = _port ?? throw new InvalidOperationException("Serial port chưa được khởi tạo.");

        try
        {
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
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Đọc dữ liệu từ cổng {port.PortName} timeout sau {_readTimeoutMs}ms.", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Lỗi I/O khi đọc từ cổng {port.PortName}: cổng có thể đã bị ngắt kết nối.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Cổng {port.PortName} không còn hợp lệ để đọc.", ex);
        }
    }

    public async Task<byte[]> ReadFrameAsync(CancellationToken ct = default)
    {
        var port = _port ?? throw new InvalidOperationException("Serial port chưa được khởi tạo.");

        try
        {
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
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Đọc dữ liệu từ cổng {port.PortName} timeout sau {_readTimeoutMs}ms.", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Lỗi I/O khi đọc từ cổng {port.PortName}: cổng có thể đã bị ngắt kết nối.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Cổng {port.PortName} không còn hợp lệ để đọc.", ex);
        }
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

