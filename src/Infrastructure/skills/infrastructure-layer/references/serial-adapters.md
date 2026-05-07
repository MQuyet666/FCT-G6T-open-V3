# Serial Adapters

Serial adapters translate application commands into protocol frames and parse hardware responses.

## Existing Implementations

- `G6TAdapter`: implements `IG6TAdapter`; controls G6T board through framed UART commands.
- `DetectorAdapter`: implements `IDetectorAdapter`; reads temperature, smoke, and LoRa values from DUT.
- `QrCodeReaderAdapter`: implements `IQrCodeReaderAdapter`; sends QR trigger `1B 5A 0D`, then reads one QR text line from COM at 9600 baud by default.
- `ComPortProvider`: implements `IComPortProvider`; lists OS COM ports.

## Rules

- Use `ISerialPortWrapper` from HAL; do not expose `SerialPort`.
- Build protocol frames in Infrastructure, not Application.
- Use `ReceiveLineAsync` for QR scanner input that terminates with CR/LF.
- QR scan timeout is 5s in `Serial:QrReadTimeoutSeconds`; UI may continue the existing test flow after timeout.
- Validate header, direction byte, payload format, and BCC before returning success.
- Include `[PORT]`, `[TX]`, and `[RX]` context in timeout/invalid-frame errors.
- Retry only within adapter-level protocol rules.
- Use `ILogger<T>` for TX/RX trace and warning logs.
