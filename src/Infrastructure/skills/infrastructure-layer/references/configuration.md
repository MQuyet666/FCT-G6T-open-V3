# Configuration

Configuration classes model values from `config/appsettings.json` and device JSON files.

## Existing Files

- `SerialSettings`
- `CameraRuntimeSettings`
- `TestTimeoutSettings`
- `LoggingSettings`
- `JsonTestCaseProvider`

## Rules

- Keep settings as simple POCO classes with defaults.
- Do not read config files from Application services.
- Bind settings in `Program.cs`.
- Use provider interfaces when Application needs data from files.
- Return empty collections for missing optional test-case files when that is current behavior.
