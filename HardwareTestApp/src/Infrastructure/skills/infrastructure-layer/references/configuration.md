# Configuration

Configuration classes model values from `config/appsettings.json` and device JSON files.

## Existing Files

- `SerialSettings`
- `CameraRuntimeSettings`
- `TestTimeoutSettings`
- `LoggingSettings`
- `JsonTestCaseProvider`

## SerialSettings

```csharp
int G6tBaudRate { get; set; }
int DetectorBaudRate { get; set; }
int QrBaudRate { get; set; }
int ReadTimeoutMs { get; set; }
int WriteTimeoutMs { get; set; }
int FrameRetryCount { get; set; }
int DetectorRetryCount { get; set; }
int FrameAckTimeoutSeconds { get; set; }
int DetectorAckTimeoutSeconds { get; set; }
int QrReadTimeoutSeconds { get; set; }
```

Use serial settings as the single source for baud rates, retry counts, and serial timeouts. UI and Application code should pass cancellation tokens and selected COM names, not hardcoded serial constants.

## Rules

- Keep settings as simple POCO classes with defaults.
- Do not read config files from Application services.
- Bind settings in `Program.cs`.
- Use provider interfaces when Application needs data from files.
- Return empty collections for missing optional test-case files when that is current behavior.
- Add new serial/device settings to `SerialSettings` and `config/appsettings.json` together.
