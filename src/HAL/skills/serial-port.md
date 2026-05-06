# Ky nang Serial HAL

## Module
Serial

Namespace bat buoc: `FCT.G6T.HAL.Serial`.

## API

### Interface `ISerialPortWrapper`
- `bool IsOpen { get; }`
- `Task OpenAsync(string portName, int baudRate, CancellationToken ct = default)`
- `Task CloseAsync(CancellationToken ct = default)`
- `Task SendAsync(byte[] data, CancellationToken ct = default)`
- `Task<byte[]> ReceiveAsync(CancellationToken ct = default)`
- `Task<byte[]> ReceiveAsync(byte startByte, byte endByte, CancellationToken ct = default)`
- `void Dispose()`

### Class `SerialPortWrapper`
- `bool IsOpen { get; }`
- `Task OpenAsync(string portName, int baudRate, CancellationToken ct = default)`
- `Task CloseAsync(CancellationToken ct = default)`
- `Task SendAsync(byte[] data, CancellationToken ct = default)`
- `Task<byte[]> ReceiveAsync(CancellationToken ct = default)`
- `Task<byte[]> ReceiveAsync(byte startByte, byte endByte, CancellationToken ct = default)`
- `void Dispose()`

## Rule
- Wrap `System.IO.Ports.SerialPort`, khong expose type nay ra ngoai HAL.
- Timeout qua `CancellationToken`, khong dung `SerialPort.ReadTimeout`.
- Moi `await` trong HAL phai co `ConfigureAwait(false)`.
- HAL chi throw exception; khong log truc tiep.
- Khong import `FCT.G6T.Application` hoac `FCT.G6T.Domain`.
