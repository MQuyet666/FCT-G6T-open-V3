# Dependency Injection

Register Infrastructure implementations in `Program.cs`.

## Current Bindings

```csharp
IComPortProvider -> ComPortProvider
ICameraService -> SdkCameraAdapter
ICameraPreviewAppService -> CameraPreviewAppService
IG6TAdapter -> G6TAdapter
IDetectorAdapter -> DetectorAdapter
IQrCodeReaderAdapter -> QrCodeReaderAdapter
ITestCaseProvider -> JsonTestCaseProvider
ILoggerProvider -> FileLoggerProvider
```

## Rules

- Register concrete Infrastructure classes only at the composition root.
- Prefer singleton for long-lived device adapters and providers.
- Prefer transient for low-level wrappers when each adapter owns its own wrapper instance.
- Pass runtime settings from bound POCOs into constructors.
- Do not instantiate Infrastructure classes in Forms or Application services.
