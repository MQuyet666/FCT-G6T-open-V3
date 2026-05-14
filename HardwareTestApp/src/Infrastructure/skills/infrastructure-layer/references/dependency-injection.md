# Dependency Injection

Register Infrastructure/HAL implementations only in `Program.cs` or a dedicated composition root.

## Current Bindings

```csharp
IComPortProvider -> ComPortProvider
ICameraService -> SdkCameraAdapter
IG6TAdapter -> G6TAdapter
IDetectorAdapter -> DetectorAdapter
IQrCodeReaderAdapter -> QrCodeReaderAdapter
ITestCaseProvider -> JsonTestCaseProvider
ILoggerProvider -> FileLoggerProvider
ISerialPortWrapper -> SerialPortWrapper
```

Application services may also be registered in the same composition root, but Forms must receive Application interfaces through constructors and must not instantiate Infrastructure or HAL classes directly.

## Ownership

- `ISerialPortWrapper` should be transient so each serial adapter owns its own wrapper instance.
- G6T, Detector/DT, and QR adapters may be singleton when each receives a distinct transient wrapper during construction.
- Long-lived providers and adapters should dispose their owned wrappers/resources.
- Runtime settings are bound from configuration POCOs in `Program.cs` and passed into constructors.

## Rules

- Register concrete Infrastructure/HAL classes only at the composition root.
- Do not wire DI inside Forms or UserControls.
- Do not use service locator patterns inside Application, Infrastructure, or HAL classes.
- When adding a serial adapter, add its Domain/Application interface, Infrastructure implementation, HAL wrapper dependency, logger, settings, and `Program.cs` registration together.
