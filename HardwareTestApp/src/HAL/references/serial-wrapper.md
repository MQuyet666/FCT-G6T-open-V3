# SerialWrapper Reference

## Purpose

Defines the HAL pattern for raw UART/SerialPort access.

HAL owns direct `System.IO.Ports.SerialPort` usage. Infrastructure owns protocol frames, parsing, retries, logging, and device-specific decisions.

## Required Pattern

- Wrap `System.IO.Ports.SerialPort`; never expose that type outside HAL.
- Expose async APIs only: `OpenAsync`, `CloseAsync`, `SendAsync`, `ReceiveAsync`, `ReceiveLineAsync`.
- Use caller-provided `CancellationToken` for timeout/cancel control; do not rely on `SerialPort.ReadTimeout`.
- Use `ConfigureAwait(false)` on every await in HAL.
- Do not log in HAL.

## API Surface

`ISerialPortWrapper` is the only serial API Infrastructure should call:

```csharp
bool IsOpen { get; }
Task OpenAsync(string portName, int baudRate, CancellationToken ct = default);
Task CloseAsync(CancellationToken ct = default);
Task SendAsync(byte[] data, CancellationToken ct = default);
Task<byte[]> ReceiveAsync(CancellationToken ct = default);
Task<byte[]> ReceiveAsync(byte startByte, byte endByte, CancellationToken ct = default);
Task<string> ReceiveLineAsync(CancellationToken ct = default);
void Dispose();
```

## Device Usage

- G6T COM: `SendAsync` + `ReceiveAsync()` for fixed-length binary frames.
- Detector/DT COM: `SendAsync` + `ReceiveLineAsync()` for CR/LF-terminated text protocol.
- QR COM: `SendAsync` for the trigger command, then `ReceiveLineAsync()` for the CR/LF-terminated QR value.

HAL must not build protocol frames, validate payloads, retry, or parse responses. That belongs in Infrastructure adapters.

## Error Contract

- Throw `HardwareException` when the port cannot open, is disconnected, disposed, or no longer valid.
- Throw `TimeoutException` for read/write timeouts so the adapter/Application can decide retry or fail.
- Preserve real user cancellation as `OperationCanceledException`; do not convert user cancel into timeout.
- Include COM port context in hardware/timeout messages when the port name is known.
