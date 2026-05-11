# Serial Adapters

Serial adapters translate Application/Domain contracts into device protocol frames and parse hardware responses. They sit in `FCT.G6T.Infrastructure.Serial` and use `FCT.G6T.HAL.Serial.ISerialPortWrapper` for raw COM I/O.

## Existing Implementations

- `G6TAdapter`: implements `IG6TAdapter`; controls the G6T board through framed UART commands.
- `DetectorAdapter`: implements `IDetectorAdapter`; reads temperature, smoke, and LoRa values from the detector/DT COM.
- `QrCodeReaderAdapter`: implements `IQrCodeReaderAdapter`; sends QR trigger `1B 5A 0D`, then reads one QR text line from COM.
- `ComPortProvider`: implements `IComPortProvider`; lists OS COM ports.

## Public Contracts

`IG6TAdapter`:

```csharp
event EventHandler<G6TTraceEventArgs>? Trace;
bool IsConnected { get; }
string ConnectedComPort { get; }
Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default);
Task DisconnectAsync(CancellationToken ct = default);
Task<G6TResponse> SendCommandAsync(G6TCommand command, CancellationToken ct = default);
Task SendCommandNoAckAsync(G6TCommand command, CancellationToken ct = default);
void Dispose();
```

`IDetectorAdapter`:

```csharp
event EventHandler<DetectorTraceEventArgs>? Trace;
bool IsConnected { get; }
string ConnectedComPort { get; }
Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default);
Task DisconnectAsync(CancellationToken ct = default);
Task<DetectorResponse> ReadTemperatureAsync(CancellationToken ct = default);
Task<DetectorResponse> ReadSmokeAsync(CancellationToken ct = default);
Task<DetectorResponse> ReadLoraAsync(CancellationToken ct = default);
void Dispose();
```

`IQrCodeReaderAdapter`:

```csharp
bool IsConnected { get; }
string ConnectedComPort { get; }
Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default);
Task DisconnectAsync(CancellationToken ct = default);
Task<QrCodeData> ReadAsync(CancellationToken ct = default);
Task<QrCodeData> ScanAsync(CancellationToken ct = default);
void Dispose();
```

`IComPortProvider`:

```csharp
IReadOnlyList<string> GetAvailableComPorts();
```

## Runtime Settings

Use values from `SerialSettings`/`config/appsettings.json`; do not hardcode these in UI or adapters:

- `G6tBaudRate`
- `DetectorBaudRate`
- `QrBaudRate`
- `FrameRetryCount`
- `DetectorRetryCount`
- `FrameAckTimeoutSeconds`
- `DetectorAckTimeoutSeconds`
- `QrReadTimeoutSeconds`

If an adapter accepts `baudRate <= 0`, resolve it to its configured/default baud rate inside Infrastructure.

## Rules

- Use `ISerialPortWrapper` from HAL; do not expose `SerialPort`.
- Build protocol frames in Infrastructure, not Application or HAL.
- G6T binary protocol uses `SendAsync` + `ReceiveAsync()`.
- Detector/DT text protocol uses `SendAsync` + `ReceiveLineAsync()`.
- QR scanner uses `SendAsync` for trigger `1B 5A 0D`, then `ReceiveLineAsync()` for CR/LF-terminated data.
- QR scan timeout belongs to `Serial:QrReadTimeoutSeconds`; UI should not hardcode `5s`.
- Validate header, direction byte, payload format, BCC/checksum, or expected text prefix before returning success when the device protocol has those fields.
- Include `[PORT]`, `[TX]`, and `[RX]` context in timeout, invalid-frame, and hardware errors where possible.
- Use `ILogger<T>` for TX/RX trace, connect/disconnect, warnings, retry, and recoverable failures.
- Do not convert `HardwareException` to a generic exception type; preserve hardware context for Application-level handling.
- Retry only inside adapter-level protocol rules, using configured retry counts.
- Use `ConfigureAwait(false)` on awaits inside Infrastructure.
