# Serial Adapters

Serial adapters translate application commands into protocol frames and parse hardware responses.

## Existing Implementations

- `G6TAdapter`: implements `IG6TAdapter`; controls G6T board through framed UART commands.
- `DetectorAdapter`: implements `IDetectorAdapter`; reads temperature, smoke, and LoRa values from DUT.
- `ComPortProvider`: implements `IComPortProvider`; lists OS COM ports.

## Rules

- Use `ISerialPortWrapper` from HAL; do not expose `SerialPort`.
- Build protocol frames in Infrastructure, not Application.
- Validate header, direction byte, payload format, and BCC before returning success.
- Include `[PORT]`, `[TX]`, and `[RX]` context in timeout/invalid-frame errors.
- Retry only within adapter-level protocol rules.
- Use `ILogger<T>` for TX/RX trace and warning logs.
